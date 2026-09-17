using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{

    public partial class SusRouter
    {
        // ─── Route registry ───
        private readonly Dictionary<string, SusRouteRecord> _routeMap = new();
        private readonly List<SusRouteRecord> _routes = new();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly HashSet<SusRouteRecord> _usedRoutes = new();
        private bool _deadRouteAuditScheduled;
#endif

        // ─── Named routes ───
        private readonly Dictionary<string, SusRouteRecord> _namedRoutes = new();

        // ─── Aliases ───
        private readonly Dictionary<string, SusRouteRecord> _aliasMap = new();

        // ─── History stack with cursor ───
        private readonly List<SusRoute> _history = new();
        private int _historyIndex = -1;

        /// <summary>
        /// P2.2: max entries in the history stack. When exceeded on Push, the oldest
        /// entries are evicted (cursor shifts). <c>0</c> or less — unlimited
        /// (dev builds then warn about unbounded growth).
        /// Default: 100.
        /// </summary>
        public int MaxHistory { get; set; } = 100;

        /// <summary>
        /// P2.6: when <c>true</c>, the KeepAlive cache ignores the query string in the key, so
        /// <c>/page?q=1</c> and <c>/page?q=2</c> share one cached screen instance
        /// (query is still merged into Props). Default <c>false</c> — each unique
        /// FullPath is cached separately.
        /// </summary>
        public bool KeepAliveIgnoreQuery { get; set; } = false;

        /// <summary>
        /// P2.6: unified KeepAlive cache key for a route (used both when caching
        /// and when retrieving). Respects <see cref="KeepAliveIgnoreQuery"/>.
        ///
        /// <b>KeepAlive screen teardown contract:</b>
        ///  • leave screen (KeepAlive) → DOM-detach → <c>Unmounted()</c> (UITK), screen
        ///    is cached, <c>Left()</c> is NOT called (screen stays "alive", only paused);
        ///  • return → retrieve from cache → <c>Mounted()</c> + <c>Entered()</c>;
        ///  • LRU eviction / <c>ClearKeepAliveCache</c> → <c>OnScreenEvicted</c> →
        ///    <c>Left()</c> (final teardown).
        /// This is an intentional router "off-DOM cache", not core-<c>SusKeepAlive</c>.
        /// </summary>
        public string KeepAliveKey(SusRoute route)
        {
            if (route == null) return null;
            var full = route.FullPath ?? string.Empty;
            if (!KeepAliveIgnoreQuery) return full;
            var q = full.IndexOf('?');
            return q >= 0 ? full.Substring(0, q) : full;
        }

        // ─── Re-entrancy guard ───
        private bool _isNavigating;
        private bool _isInitialized; // guards against double Init (P2.5)

        // ─── Global guards ───
        private readonly List<SusRouterGuard> _beforeEachGuards = new();
        private readonly List<SusRouterGuard> _beforeResolveGuards = new();  // C.1
        private readonly List<SusRouterAfterHook> _afterEachHooks = new();

        // ─── Async guards (P2) ───
        private readonly List<SusRouterAsyncGuard> _beforeEachAsyncGuards = new();
        private readonly List<SusRouterAsyncGuard> _beforeResolveAsyncGuards = new();

        // ─── Services ───
        public SusModalService ModalService { get; private set; }
        public SusTransitionService TransitionService { get; private set; }

        /// <summary>
        /// Aggregated overlay services (Modal, Transition, Console, World).
        /// Set by Init(). TooltipService lives in downstream component libraries — use its own singleton.
        /// </summary>
        public SusOverlayServices OverlayServices { get; private set; }

        /// <summary>
        /// Shared overlay portal for all layers (Transition, Tooltip, Modal, Console).
        /// Set by Init() and available to external services (component libraries, Console, etc.).
        /// </summary>
        public OverlayHost OverlayHost => OverlayServices?.Host;

        // ─── Visual container references ───
        private SusRouteView _routeView;

        /// <summary>
        /// Currently displayed route. <see cref="Prop{T}.Value"/> is null until the first
        /// successful navigation.
        /// </summary>
        public Prop<SusRoute> CurrentRoute { get; } = new Prop<SusRoute>();

        /// <summary>True when the history cursor is past the oldest entry (<see cref="Back"/> can succeed).</summary>
        public bool CanGoBack => _historyIndex > 0;
        /// <summary>True when the history cursor is before the newest entry (<see cref="Forward"/> can succeed).</summary>
        public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;
        /// <summary>Read-only history stack. Index 0 is oldest; <see cref="HistoryIndex"/> is the cursor.</summary>
        public IReadOnlyList<SusRoute> History => _history.AsReadOnly();
        /// <summary>Cursor into <see cref="History"/>. <see cref="Push(string, Dictionary{string, object})"/> increments; <see cref="Back"/> decrements.</summary>
        public int HistoryIndex => _historyIndex;
        /// <summary>Number of registered routes (not history length).</summary>
        public int RouteCount => _routes.Count;

        /// <summary>
        /// All registered routes (read-only).
        /// Analogous to router.getRoutes() in Vue Router.
        /// </summary>
        public IReadOnlyList<SusRouteRecord> Routes => _routes.AsReadOnly();

        /// <summary>
        /// Checks whether a named route is registered.
        /// Analogous to router.hasRoute(name) in Vue Router.
        /// </summary>
        public bool HasRoute(string name) => !string.IsNullOrEmpty(name) && _namedRoutes.ContainsKey(name);

        /// <summary>
        /// Navigation error event (NotFound, Aborted, etc.).
        /// Analogous to router.onError() in Vue Router.
        /// </summary>
        public event System.Action<NavigationError> OnNavigationError;

        private void FireError(NavigationResult result, SusRoute from, SusRoute to, string rejectedBy = null)
        {
            OnNavigationError?.Invoke(new NavigationError
            {
                Result = result,
                From = from,
                To = to,
                RejectedBy = rejectedBy ?? result.ToString()
            });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// DeadRouteAudit: warns about registered routes that were never navigated to.
        /// Call this after the app has been running for a few seconds
        /// (e.g. in a timer or on a Debug key press).
        /// </summary>
        public void AuditUnusedRoutes()
        {
            var unused = _routes.Where(r => !_usedRoutes.Contains(r)).ToList();
            if (unused.Count == 0) return;

            SusLog.Verbose($"[DeadRouteAudit] {unused.Count} registered routes " +
                $"were never navigated to:");
            foreach (var r in unused)
            {
                var name = !string.IsNullOrEmpty(r.Config?.Name) ? $" ({r.Config.Name})" : "";
                SusLog.Verbose($"  - {r.Path}{name}");
            }
        }
#endif

        // ════════════════════════════════════════════════════════════════
        //  Route registration
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Registers a route. screenType may be null when config.LazyFactory is set.
        /// </summary>
        public SusRouteRecord Register(string path, Type screenType, SusRouteConfig config = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (screenType == null && (config?.LazyFactory == null))
                throw new ArgumentNullException(nameof(screenType),
                    "screenType must not be null when LazyFactory is not set");
            if (screenType != null && !typeof(SusScreen).IsAssignableFrom(screenType))
                throw new ArgumentException(
                    $"Screen type {screenType.Name} must inherit from SusScreen", nameof(screenType));

            var record = new SusRouteRecord(path, screenType, config ?? new SusRouteConfig());

            // Remove old entry if this path was already registered
            if (_routeMap.TryGetValue(path, out var oldRecord))
                _routes.Remove(oldRecord);

            _routeMap[path] = record;
            _routes.Add(record);

            if (!string.IsNullOrEmpty(record.Config.Name))
            {
                if (_namedRoutes.ContainsKey(record.Config.Name))
                    throw new ArgumentException(
                        $"Route name '{record.Config.Name}' is already registered", nameof(config));
                _namedRoutes[record.Config.Name] = record;
            }

            if (record.Config.Alias != null)
            {
                foreach (var alias in record.Config.Alias)
                {
                    if (!string.IsNullOrEmpty(alias))
                        _aliasMap[alias] = record;
                }
            }

            if (record.Config.Children != null)
            {
                foreach (var child in record.Config.Children)
                {
                    child.Parent = record;
                    var childFullPath = (path.TrimEnd('/') + "/" + child.Path.TrimStart('/'));
                    _routeMap[childFullPath] = child;
                    _routes.Add(child);

                    if (!string.IsNullOrEmpty(child.Config.Name))
                    {
                        if (_namedRoutes.ContainsKey(child.Config.Name))
                            throw new ArgumentException(
                                $"Route name '{child.Config.Name}' is already registered");
                        _namedRoutes[child.Config.Name] = child;
                    }
                }
            }

            return record;
        }

        /// <summary>
        /// Finds a registered route. Query parameters (?key=val) are ignored when matching.
        /// </summary>
        public SusRouteRecord Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Strip query string before lookup
            var queryStart = path.IndexOf('?');
            var pathOnly = queryStart >= 0 ? path.Substring(0, queryStart) : path;

            if (_routeMap.TryGetValue(pathOnly, out var exactMatch))
                return exactMatch;

            if (_aliasMap.TryGetValue(pathOnly, out var aliasMatch))
                return aliasMatch;

            return _routes.FirstOrDefault(r => r.Match(path) != null);
        }

        /// <summary>
        /// Removes a named route from the registry. Returns false if the name is not found.
        /// Analogous to router.removeRoute(name) in Vue Router.
        /// </summary>
        public bool RemoveRoute(string name)
        {
            if (string.IsNullOrEmpty(name) || !_namedRoutes.TryGetValue(name, out var record))
                return false;
            _routeMap.Remove(record.Path);
            _routes.Remove(record);
            _namedRoutes.Remove(name);
            // Clear aliases pointing at this record
            var aliasesToRemove = _aliasMap
                .Where(kv => kv.Value == record)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var a in aliasesToRemove)
                _aliasMap.Remove(a);
            // Clear child routes from the same dictionaries
            if (record.Config.Children != null)
            {
                foreach (var child in record.Config.Children)
                {
                    var childFullPath = (record.Path.TrimEnd('/') + "/" + child.Path.TrimStart('/'));
                    _routeMap.Remove(childFullPath);
                    _routes.Remove(child);
                    if (!string.IsNullOrEmpty(child.Config.Name))
                        _namedRoutes.Remove(child.Config.Name);
                }
            }
            return true;
        }

        /// <summary>
        /// Builds the route chain from root to leaf for nested routes.
        /// Walks Parent links upward and reverses the list (root → leaf).
        /// </summary>
        public List<SusRouteRecord> ResolveChain(string path)
        {
            var leaf = Resolve(path);
            if (leaf == null) return null;

            var chain = new List<SusRouteRecord>();
            var current = leaf;
            while (current != null)
            {
                chain.Add(current);
                current = current.Parent;
            }
            chain.Reverse(); // root → leaf
            return chain;
        }

        /// <summary>
        /// Finds the common-prefix depth of two chains (number of matching
        /// records from the root). Used for nested-route diff updates.
        /// </summary>
        public static int FindCommonPrefixDepth(
            List<SusRouteRecord> fromChain, List<SusRouteRecord> toChain)
        {
            if (fromChain == null || toChain == null) return 0;
            int max = System.Math.Min(fromChain.Count, toChain.Count);
            for (int i = 0; i < max; i++)
            {
                if (fromChain[i] != toChain[i])
                    return i;
            }
            return max;
        }

        // ════════════════════════════════════════════════════════════════
        //  Global guards
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Registers a global guard run on every navigation after <c>CanLeave</c> and before <c>CanEnter</c>.
        /// Return <c>false</c> to abort. Analogous to Vue Router <c>beforeEach</c>.
        /// </summary>
        public void BeforeEach(SusRouterGuard guard)
        {
            if (guard != null)
                _beforeEachGuards.Add(guard);
        }

        /// <summary>
        /// Registers a beforeResolve guard (C.1).
        /// Called AFTER screen creation and BeforeEnter, but BEFORE stack update.
        /// Analogous to router.beforeResolve in Vue Router.
        /// </summary>
        public void BeforeResolve(SusRouterGuard guard)
        {
            if (guard != null)
                _beforeResolveGuards.Add(guard);
        }

        /// <summary>
        /// Registers a hook run after a successful navigation, once <see cref="CurrentRoute"/> is updated.
        /// Analogous to Vue Router <c>afterEach</c>.
        /// </summary>
        public void AfterEach(SusRouterAfterHook hook)
        {
            if (hook != null)
                _afterEachHooks.Add(hook);
        }

    }
}

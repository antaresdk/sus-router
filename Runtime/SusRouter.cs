using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    public enum NavigationResult
    {
        /// <summary>Navigation completed successfully.</summary>
        Success,
        /// <summary>Navigation was rejected by a guard or lifecycle hook.</summary>
        Aborted,
        /// <summary>Route not found — the path does not match any registered route.</summary>
        NotFound,
        /// <summary>Cannot go back — history is at the oldest entry.</summary>
        CantGoBack,
        /// <summary>Cannot go forward — history is at the newest entry.</summary>
        CantGoForward,
        /// <summary>
        /// Router is already processing a navigation. The request was dropped.
        /// Callers (e.g. tab handlers) should not retry; they may resync their UI
        /// to CurrentRoute (which reflects the active navigation once it completes).
        /// </summary>
        Busy,
    }

    public delegate bool SusRouterGuard(SusRoute from, SusRoute to);
    public delegate void SusRouterAfterHook(SusRoute from, SusRoute to);
    public delegate System.Threading.Tasks.Task<bool> SusRouterAsyncGuard(SusRoute from, SusRoute to);

    /// <summary>
    /// Typed navigation failure. Contains the result code, from/to routes,
    /// and which step rejected the navigation.
    /// </summary>
    public class NavigationError
    {
        public NavigationResult Result;
        public SusRoute From;
        public SusRoute To;
        /// <summary>Which guard/lifecycle hook rejected the navigation: "CanLeave", "BeforeEach", "CanEnter", "BeforeEnter", "BeforeResolve", "BeforeEnter(screen)".</summary>
        public string RejectedBy;
    }

    public class SusRouter
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
        /// Set by Init(). TooltipService is in sus-kit — use its own singleton.
        /// </summary>
        public SusOverlayServices OverlayServices { get; private set; }

        /// <summary>
        /// Shared overlay portal for all layers (Transition, Tooltip, Modal, Console).
        /// Set by Init() and available to external services (sus-kit, Console, etc.).
        /// </summary>
        public OverlayHost OverlayHost => OverlayServices?.Host;

        // ─── Visual container references ───
        private SusRouteView _routeView;

        public Prop<SusRoute> CurrentRoute { get; } = new Prop<SusRoute>();

        public bool CanGoBack => _historyIndex > 0;
        public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;
        public IReadOnlyList<SusRoute> History => _history.AsReadOnly();
        public int HistoryIndex => _historyIndex;
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

            UnityEngine.Debug.LogWarning($"[DeadRouteAudit] {unused.Count} registered routes " +
                $"were never navigated to:");
            foreach (var r in unused)
            {
                var name = !string.IsNullOrEmpty(r.Config?.Name) ? $" ({r.Config.Name})" : "";
                UnityEngine.Debug.LogWarning($"  - {r.Path}{name}");
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

        public void AfterEach(SusRouterAfterHook hook)
        {
            if (hook != null)
                _afterEachHooks.Add(hook);
        }

        // ─── Async guard registration (P2) ───

        /// <summary>
        /// Registers an async beforeEach guard.
        /// Used when calling NavigateAsync (FullPath-based PushAsync/ReplaceAsync).
        /// Sync and async guards run sequentially.
        /// </summary>
        public void BeforeEachAsync(SusRouterAsyncGuard guard)
        {
            if (guard != null)
                _beforeEachAsyncGuards.Add(guard);
        }

        /// <summary>
        /// Registers an async beforeResolve guard.
        /// </summary>
        public void BeforeResolveAsync(SusRouterAsyncGuard guard)
        {
            if (guard != null)
                _beforeResolveAsyncGuards.Add(guard);
        }

        // ════════════════════════════════════════════════════════════════
        //  Navigation
        // ════════════════════════════════════════════════════════════════

        public NavigationResult Push(string path, Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[NavigationAudit] Push('{path}') — route not found.");
#endif
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, path, null));
                return NavigationResult.NotFound;
            }
            return PushRecord(record, path, props);
        }

        public NavigationResult Replace(string path, Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[NavigationAudit] Replace('{path}') — route not found.");
#endif
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, path, null));
                return NavigationResult.NotFound;
            }
            return ReplaceRecord(record, path, props);
        }

        public NavigationResult PushNamed(string name,
            Dictionary<string, string> pathParams = null,
            Dictionary<string, object> props = null)
        {
            if (!_namedRoutes.TryGetValue(name, out var record))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[NavigationAudit] PushNamed('{name}') — named route not found.");
#endif
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, name, null));
                return NavigationResult.NotFound;
            }
            var path = BuildPath(record, pathParams);
            if (path == null)
            {
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, name, null));
                return NavigationResult.NotFound;
            }
            return PushRecord(record, path, props);
        }

        public NavigationResult ReplaceNamed(string name,
            Dictionary<string, string> pathParams = null,
            Dictionary<string, object> props = null)
        {
            if (!_namedRoutes.TryGetValue(name, out var record))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[NavigationAudit] ReplaceNamed('{name}') — named route not found.");
#endif
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, name, null));
                return NavigationResult.NotFound;
            }
            var path = BuildPath(record, pathParams);
            if (path == null)
            {
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, name, null));
                return NavigationResult.NotFound;
            }
            return ReplaceRecord(record, path, props);
        }

        public string ResolvePath(string name, Dictionary<string, string> pathParams = null)
        {
            if (!_namedRoutes.TryGetValue(name, out var record))
                return null;
            return BuildPath(record, pathParams);
        }

        private NavigationResult PushRecord(SusRouteRecord record, string path,
            Dictionary<string, object> props)
        {
            var pathParams = record.Match(path);
            if (pathParams == null && record.ParamNames.Count > 0)
                return NavigationResult.NotFound;

            var toRoute = new SusRoute(record, path, pathParams)
            {
                MatchedChain = ResolveChain(path) ?? new List<SusRouteRecord> { record }
            };
            toRoute.Props = BuildMergedProps(record, toRoute, props);

            var fromRoute = CurrentRoute.Value ?? SusRoute.None;
            return Navigate(fromRoute, toRoute, isReplace: false, stepOffset: 0);
        }

        private NavigationResult ReplaceRecord(SusRouteRecord record, string path,
            Dictionary<string, object> props)
        {
            var pathParams = record.Match(path);
            if (pathParams == null && record.ParamNames.Count > 0)
                return NavigationResult.NotFound;

            var toRoute = new SusRoute(record, path, pathParams)
            {
                MatchedChain = ResolveChain(path) ?? new List<SusRouteRecord> { record }
            };
            toRoute.Props = BuildMergedProps(record, toRoute, props);

            var fromRoute = CurrentRoute.Value ?? SusRoute.None;
            return Navigate(fromRoute, toRoute, isReplace: true, stepOffset: 0);
        }

        public NavigationResult Back()
        {
            if (!CanGoBack)
            {
                FireError(NavigationResult.CantGoBack,
                    CurrentRoute.Value ?? SusRoute.None, SusRoute.None);
                return NavigationResult.CantGoBack;
            }
            var fromRoute = _history[_historyIndex];
            var toRoute = _history[_historyIndex - 1];
            return Navigate(fromRoute, toRoute, isReplace: false, stepOffset: -1);
        }

        public NavigationResult Forward()
        {
            if (!CanGoForward)
            {
                FireError(NavigationResult.CantGoForward,
                    CurrentRoute.Value ?? SusRoute.None, SusRoute.None);
                return NavigationResult.CantGoForward;
            }
            var fromRoute = _history[_historyIndex];
            var toRoute = _history[_historyIndex + 1];
            return Navigate(fromRoute, toRoute, isReplace: false, stepOffset: +1);
        }

        public NavigationResult Go(int n)
        {
            if (n > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    var r = Forward();
                    if (r != NavigationResult.Success) return r;
                }
            }
            else if (n < 0)
            {
                for (int i = 0; i < -n; i++)
                {
                    var r = Back();
                    if (r != NavigationResult.Success) return r;
                }
            }
            return NavigationResult.Success;
        }

        /// <summary>
        /// Navigates with a fade-out→replace→fade-in transition.
        /// If the route has SusRouteConfig.Transition — uses the
        /// per-route animation (Fade/SlideLeft/SlideRight) via PlayOut/PlayIn.
        /// Otherwise — curtain-based fade via TransitionService.
        /// </summary>
        /// <param name="path">Target route path.</param>
        /// <param name="duration">Fade duration in seconds (default 0.3f). Used only when no per-route transition.</param>
        /// <param name="props">Optional route props.</param>
        public void NavigateWithTransition(string path,
            float duration = 0.3f,
            Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[NavigationAudit] NavigateWithTransition('{path}') — route not found.");
#endif
                return;
            }

            var perRouteTransition = record.Config?.Transition;
            if (perRouteTransition != null)
            {
                // Per-route animation (Fade, SlideLeft, SlideRight)
                var currentScreen = _routeView?.CurrentScreen;
                if (currentScreen != null)
                    perRouteTransition.PlayOut(currentScreen);

                if (currentScreen != null && perRouteTransition.Duration > 0)
                {
                    currentScreen.schedule.Execute(() =>
                    {
                        Replace(path, props);
                        var newScreen = _routeView?.CurrentScreen;
                        if (newScreen != null)
                            perRouteTransition.PlayIn(newScreen);
                    }).StartingIn((long)(perRouteTransition.Duration * 1000));
                }
                else
                {
                    Replace(path, props);
                }
            }
            else
            {
                // Curtain-based fade (current behavior)
                TransitionService?.FadeOut(duration, () =>
                {
                    Replace(path, props);
                    TransitionService?.FadeIn(duration);
                });
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Active-route checks
        // ════════════════════════════════════════════════════════════════

        public bool IsRouteActive(string path)
        {
            // Strip query for comparison
            var queryStart = path.IndexOf('?');
            var pathOnly = queryStart >= 0 ? path.Substring(0, queryStart) : path;

            for (int i = 0; i < _history.Count; i++)
            {
                var histPath = _history[i].FullPath;
                var hq = histPath.IndexOf('?');
                var histPathOnly = hq >= 0 ? histPath.Substring(0, hq) : histPath;
                if (histPathOnly == pathOnly)
                    return true;
            }
            return false;
        }

        public bool IsRouteActiveExact(string path)
        {
            return CurrentRoute.Value?.FullPath == path;
        }

        // ════════════════════════════════════════════════════════════════
        //  Guard pipeline
        // ════════════════════════════════════════════════════════════════

        private NavigationResult Navigate(SusRoute fromRoute, SusRoute toRoute, bool isReplace,
            int stepOffset = 0, bool asyncGuardsHandled = false)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Sync navigation cannot await async guards — warn so they are not silently skipped.
            if (!asyncGuardsHandled
                && (_beforeEachAsyncGuards.Count > 0 || _beforeResolveAsyncGuards.Count > 0))
            {
                UnityEngine.Debug.LogWarning(
                    "[GuardAudit] Sync navigation (Push/Replace/Back/Forward) skips async guards " +
                    "(BeforeEachAsync/BeforeResolveAsync). Use PushAsync/ReplaceAsync to run them.");
            }
#endif
            // ── Re-entrancy guard ──
            if (_isNavigating)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning($"[SusRouter] Navigation to '{toRoute?.FullPath}' dropped — router is busy (concurrent Push/Replace). Resync UI to CurrentRoute.");
#endif
                return NavigationResult.Busy;
            }
            _isNavigating = true;

            try
            {
                var result = NavigateCore(fromRoute, toRoute, isReplace, stepOffset);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (result == NavigationResult.Success)
                {
                    var root = _routeView?.panel?.visualTree;
                    if (root != null)
                    {
                        Sharq.Core.Diagnostics.ScreenAudit.LayoutDump(root);
                        Sharq.Core.Diagnostics.ScreenAudit.FullPropsDump(root);
                    }
                }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (result == NavigationResult.Aborted)
                    UnityEngine.Debug.LogWarning($"[GuardAudit] Nav from '{fromRoute.FullPath}' " +
                        $"→ '{toRoute.FullPath}' was rejected by a guard or lifecycle hook " +
                        $"(BeforeLeave/CanLeave/BeforeEach/CanEnter/BeforeResolve/BeforeEnter).");
#endif
                if (result != NavigationResult.Success)
                    FireError(result, fromRoute, toRoute);
                return result;
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private NavigationResult NavigateCore(SusRoute fromRoute, SusRoute toRoute, bool isReplace, int stepOffset)
        {
            // ── Redirect ──
            if (!string.IsNullOrEmpty(toRoute.Record?.Config?.Redirect))
            {
                var redirectRecord = Resolve(toRoute.Record.Config.Redirect);
                if (redirectRecord == null) return NavigationResult.NotFound;

                var redirectPath = toRoute.Record.Config.Redirect;
                var redirectParams = redirectRecord.Match(redirectPath);
                var carriedProps = toRoute.Props;
                toRoute = new SusRoute(redirectRecord, redirectPath, redirectParams)
                {
                    MatchedChain = ResolveChain(redirectPath)
                        ?? new List<SusRouteRecord> { redirectRecord }
                };
                // Carry the pre-redirect props as explicit (highest priority),
                // then merge redirect target's params/query/defaults underneath.
                toRoute.Props = BuildMergedProps(redirectRecord, toRoute, carriedProps);
            }

            // ── Step 0: No-op ──
            if (fromRoute.FullPath == toRoute.FullPath && fromRoute.Record == toRoute.Record)
                return NavigationResult.Success;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // DeadRouteAudit: track which routes have been used
            if (toRoute.Record != null)
            {
                _usedRoutes.Add(toRoute.Record);
                // Schedule deferred audit after first navigation
                if (!_deadRouteAuditScheduled && _usedRoutes.Count == 1)
                {
                    _deadRouteAuditScheduled = true;
                    // Use a timer via SusBootstrap or just schedule after 10s
                    // We'll track it — actual audit runs on demand via AuditUnusedRoutes()
                }
            }
#endif

            var fromKeepAlive = fromRoute.Record?.Config?.KeepAlive ?? false;
            var targetKeepAlive = toRoute.Record?.Config?.KeepAlive ?? false;

            // ── Step 0.5: beforeRouteUpdate (same record + active screen) ──
            if (fromRoute.Record == toRoute.Record && fromRoute.IsActive)
            {
                if (!fromRoute.Screen.BeforeRouteUpdate(toRoute))
                    return NavigationResult.Aborted;

                // Global guards also run on param-update (as in Vue Router)
                foreach (var guard in _beforeEachGuards)
                {
                    if (!guard(fromRoute, toRoute))
                        return NavigationResult.Aborted;
                }

                fromRoute.Screen.Props = toRoute.Props;
                toRoute.Screen = fromRoute.Screen;

                // beforeResolve on param-update (as in Vue Router)
                foreach (var guard in _beforeResolveGuards)
                {
                    if (!guard(fromRoute, toRoute))
                        return NavigationResult.Aborted;
                }

                if (stepOffset == 0 && !isReplace)
                {
                    if (!fromKeepAlive)
                        fromRoute.Screen = null;
                    _history[_historyIndex] = toRoute;
                }
                else if (isReplace)
                {
                    if (!fromKeepAlive)
                        fromRoute.Screen = null;
                    _history[_historyIndex] = toRoute;
                }

                CurrentRoute.Value = toRoute;
                foreach (var hook in _afterEachHooks)
                    hook(fromRoute, toRoute);
                return NavigationResult.Success;
            }

            // ── Step 1: BeforeLeave ──
            if (fromRoute.IsActive && fromRoute.Screen != null)
            {
                if (!fromRoute.Screen.BeforeLeave(toRoute))
                    return NavigationResult.Aborted;
            }

            // ── Step 2: ISusRouteGuard.CanLeave ──
            var currentGuard = fromRoute.Record?.Config?.Guard;
            if (currentGuard != null)
            {
                if (!currentGuard.CanLeave(fromRoute, toRoute))
                    return NavigationResult.Aborted;
            }

            // ── Step 3: BeforeEach ──
            foreach (var guard in _beforeEachGuards)
            {
                if (!guard(fromRoute, toRoute))
                    return NavigationResult.Aborted;
            }

            // ── Step 4: ISusRouteGuard.CanEnter ──
            var targetGuard = toRoute.Record?.Config?.Guard;
            if (targetGuard != null)
            {
                if (!targetGuard.CanEnter(fromRoute, toRoute))
                    return NavigationResult.Aborted;
            }

            // ── Step 4.5: per-route BeforeEnter (function-based, analogous to beforeEnter in Vue Router) ──
            var targetBeforeEnter = toRoute.Record?.Config?.BeforeEnter;
            if (targetBeforeEnter != null)
            {
                if (!targetBeforeEnter(fromRoute, toRoute))
                    return NavigationResult.Aborted;
            }

            // ── Step 5: Left ──
            // For chain routes, teardown is handled in Step 6 (per-level, skips common prefix)
            bool isFromChainRoute = fromRoute.ChainScreens != null && fromRoute.ChainScreens.Count > 1;
            if (fromRoute.IsActive && fromRoute.Screen != null && !fromKeepAlive && !isFromChainRoute)
            {
                fromRoute.Screen.Left();
            }

            // ── Step 5.5: BeforeResolve — BEFORE screen creation (avoids side effects on abort) ──
            foreach (var guard in _beforeResolveGuards)
            {
                if (!guard(fromRoute, toRoute))
                    return NavigationResult.Aborted;
            }

            // ── Step 6: Create/reuse screen (chain-aware) ──
            var chain = toRoute.MatchedChain;
            var fromChain = fromRoute.MatchedChain;
            bool hasMultiLevelChain = chain != null && chain.Count > 1;
            // Chain levels at index >= chainNewDepth were freshly created (need Entered()).
            int chainNewDepth = 0;

            if (hasMultiLevelChain)
            {
                int commonDepth = FindCommonPrefixDepth(fromChain, chain);
                chainNewDepth = commonDepth;

                // Teardown old screens beyond common prefix
                if (fromRoute.ChainScreens != null)
                {
                    for (int i = commonDepth; i < fromRoute.ChainScreens.Count; i++)
                    {
                        var oldScreen = fromRoute.ChainScreens[i];
                        if (oldScreen != null && !(fromRoute.Record?.Config?.KeepAlive ?? false))
                        {
                            oldScreen.Left();
                            // Detach from its parent ChildView so shorter chains
                            // (e.g. /a/b/c → /a/b) don't leave orphaned children visible.
                            oldScreen.RemoveFromHierarchy();
                        }
                    }
                }

                // Build chain screens
                var chainScreens = new List<SusScreen>();
                for (int i = 0; i < chain.Count; i++)
                {
                    SusScreen levelScreen;

                    if (i < commonDepth && fromRoute.ChainScreens != null
                        && i < fromRoute.ChainScreens.Count)
                    {
                        // Reuse screen from shared prefix — NO recreation
                        levelScreen = fromRoute.ChainScreens[i];
                        levelScreen.Props = toRoute.Props;
                        levelScreen.BeforeRouteUpdate(toRoute);
                    }
                    else
                    {
                        var record = chain[i];
                        levelScreen = record.Config.LazyFactory?.Invoke()
                            ?? (SusScreen)Activator.CreateInstance(record.ScreenType, nonPublic: true);
                        levelScreen.Router = this;
                        levelScreen.Props = toRoute.Props;
                        levelScreen.BeforeEnter(fromRoute);
                    }

                    chainScreens.Add(levelScreen);
                }

                toRoute.ChainScreens = chainScreens;
                toRoute.Screen = chainScreens[0]; // root screen
            }
            else
            {
                // Single-level route — existing logic
                if (targetKeepAlive && toRoute.IsActive && toRoute.Screen != null)
                {
                    toRoute.Screen.BeforeEnter(fromRoute);
                }
                else if (targetKeepAlive && _routeView != null
                    && _routeView.TryGetKeepAliveScreen(KeepAliveKey(toRoute), out var cachedScreen))
                {
                    cachedScreen.BeforeEnter(fromRoute);
                    toRoute.Screen = cachedScreen;
                }
                else
                {
                    if (toRoute.IsActive)
                    {
                        toRoute.Screen?.Left();
                        toRoute.Screen = null;
                    }

                    var screen = toRoute.Record.Config.LazyFactory?.Invoke()
                        ?? (SusScreen)Activator.CreateInstance(toRoute.Record.ScreenType, nonPublic: true);
                    screen.Router = this;
                    screen.Props = toRoute.Props;

                    if (!screen.BeforeEnter(fromRoute))
                        return NavigationResult.Aborted;

                    toRoute.Screen = screen;
                }
            }

            // ── Step 7: Stack update ──
            if (stepOffset == 0 && isReplace && _historyIndex >= 0)
            {
                _history[_historyIndex] = toRoute;
            }
            else if (stepOffset == 0)
            {
                if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

                _history.Add(toRoute);
                _historyIndex = _history.Count - 1;

                // P2.2: bound stack growth — evict oldest entries on overflow (Push only).
                if (MaxHistory > 0 && _history.Count > MaxHistory)
                {
                    int overflow = _history.Count - MaxHistory;
                    _history.RemoveRange(0, overflow);
                    _historyIndex -= overflow;
                    if (_historyIndex < 0) _historyIndex = 0;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else if (MaxHistory <= 0 && _history.Count > 50)
                    UnityEngine.Debug.LogWarning($"[StackDepthAudit] Router history has {_history.Count} entries " +
                        $"and MaxHistory is unbounded (<= 0). Possible circular navigation or unbounded " +
                        $"stack growth. Consider using Replace() instead of Push(), or set Router.MaxHistory.");
#endif
            }
            else
            {
                _historyIndex += stepOffset;
            }

            // ── Step 8: RouteView (must run BEFORE nulling fromRoute.Screen) ──
            _routeView?.OnRouteChanged(fromRoute, toRoute);

            // Nested chain: mount child screens into their parents' ChildView.
            if (hasMultiLevelChain && toRoute.ChainScreens != null)
                _routeView?.RenderNestedChain(toRoute.ChainScreens);

            // Null old screen only AFTER OnRouteChanged has removed it from DOM.
            // For a reused chain root (same instance in to/from) keep the reference.
            if (!fromKeepAlive && fromRoute.Screen != toRoute.Screen)
                fromRoute.Screen = null;

            // ── Step 9: Entered (only on freshly created screens) ──
            if (hasMultiLevelChain && toRoute.ChainScreens != null)
            {
                for (int i = chainNewDepth; i < toRoute.ChainScreens.Count; i++)
                    toRoute.ChainScreens[i]?.Entered();
            }
            else
            {
                toRoute.Screen?.Entered();
            }

            // ── Step 10: CurrentRoute + AfterEach ──
            CurrentRoute.Value = toRoute;

            foreach (var hook in _afterEachHooks)
            {
                hook(fromRoute, toRoute);
            }

            return NavigationResult.Success;
        }

        // ════════════════════════════════════════════════════════════════
        //  Modal windows
        // ════════════════════════════════════════════════════════════════

        // ─── ModalService / ModalLayer ────────────────────────────────

        // Obsolete SusModalLayer for backward compatibility in tests.
        // New code should use ModalService directly.
#pragma warning disable CS0618
        private SusModalLayer _modalLayer;

        /// <summary>
        /// Injects a SusModalLayer spy/stub for testing (obsolete path).
        /// When set, Modal() and CloseModal() delegate to the layer instead of ModalService.
        /// </summary>
        internal void SetModalLayer(SusModalLayer layer)
        {
            _modalLayer = layer;
        }
#pragma warning restore CS0618

        /// <summary>
        /// Sets the current route and prepopulates history for testing.
        /// Sets _historyIndex = 0 and CurrentRoute to this route.
        /// Public for test access.
        /// </summary>
        public void SetCurrentForTest(SusRoute route)
        {
            _history.Clear();
            _history.Add(route);
            _historyIndex = 0;
            route.IsActive = true;
            CurrentRoute.Value = route;
        }

        public void Modal(Type dialogType, Dictionary<string, object> props = null)
        {
            if (_modalLayer != null)
            {
                _modalLayer.ShowModal(dialogType, props);
                return;
            }
            var modal = ModalService?.Show(dialogType, props);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (modal == null)
                UnityEngine.Debug.LogWarning($"[SusRouter.Modal] Failed to show modal '{dialogType.Name}'. Check OverlayHost init and MaxModalDepth.");
#endif
        }

        /// <summary>
        /// Closes the current modal (top of ModalService / ModalLayer stack).
        /// </summary>
        public void CloseModal()
        {
            if (_modalLayer != null)
                _modalLayer.CloseModal();
            else
                ModalService?.Close();
        }

        public void SetRouteView(SusRouteView view)
        {
            _routeView = view;
        }

        // ════════════════════════════════════════════════════════════════
        //  Init / Mount
        // ════════════════════════════════════════════════════════════════

        public void Init(OverlayHost overlayHost)
        {
            if (overlayHost == null)
                throw new ArgumentNullException(nameof(overlayHost));

            if (_isInitialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogWarning("[SusRouter] Init() called more than once — no-op. " +
                    "This is harmless but indicates a duplicate Init() call in bootstrapping code. " +
                    "Prefer calling Init() once before Mount(), or let Mount() call Init() internally.");
#endif
                return;
            }
            _isInitialized = true;

            ModalService = new SusModalService { OverlayHost = overlayHost, Router = this };
            TransitionService = new SusTransitionService { OverlayHost = overlayHost };

            OverlayServices = new SusOverlayServices(overlayHost, ModalService, TransitionService);

            // Auto-cleanup: close all modal dialogs on navigation.
            BeforeEach((from, to) =>
            {
                ModalService?.CloseAll();
                return true;
            });
        }

        public NavigationResult Mount(VisualElement container, string initialPath,
            Dictionary<string, object> props = null)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));
            if (string.IsNullOrEmpty(initialPath))
                throw new ArgumentNullException(nameof(initialPath));

            var globalSheet = UnityEngine.Resources.Load<StyleSheet>("SusRuntime/_global");
            if (globalSheet != null && !container.styleSheets.Contains(globalSheet))
                container.styleSheets.Add(globalSheet);

            _routeView = new SusRouteView { Router = this };
            // Absolute fill via USS (.sus-route-view--root). Nested SusRouteViews
            // stay flex-only — do not add --root there (lobby child regions).
            _routeView.AddToClassList(SusRouteView.RootUssClassName);

            // Mount screens into the app's fixed ScreenHost slot when present (SusApp scaffold),
            // so the route view sits BELOW the OverlayHost and ABOVE the world-marker layer.
            // Falls back to the container itself for manual bootstraps without a scaffold.
            var screenSlot = container.Q<ScreenHost>(name: ScreenHost.ScreenHostName)
                             ?? container;
            screenSlot.Add(_routeView);

            // Overlay host must be the LAST child of the ROOT container so modals/tooltips render
            // on top of the route content (UI Toolkit has no z-index; paint order follows sibling
            // order). Created on the container/root — never inside the ScreenHost.
            if (ModalService == null)
            {
                var overlayHost = SusBootstrap.GetOrCreateOverlay(container);
                Init(overlayHost);
            }

            return Replace(initialPath, props);
        }

        public NavigationResult Mount(UIDocument uiDocument, string initialPath,
            Dictionary<string, object> props = null)
        {
            if (uiDocument == null)
                throw new ArgumentNullException(nameof(uiDocument));
            return Mount(uiDocument.rootVisualElement, initialPath, props);
        }

        // ════════════════════════════════════════════════════════════════
        //  Utilities
        // ════════════════════════════════════════════════════════════════

        public static string BuildPath(SusRouteRecord record, Dictionary<string, string> pathParams)
        {
            if (record.ParamNames.Count == 0)
                return record.Path;

            if (pathParams == null)
                return null;

            var parts = record.Path.Trim('/').Split('/');
            var resultParts = new List<string>();

            foreach (var part in parts)
            {
                if (part.StartsWith(":"))
                {
                    var name = part.Substring(1);
                    if (!pathParams.TryGetValue(name, out var value))
                        return null;
                    resultParts.Add(value);
                }
                else
                {
                    resultParts.Add(part);
                }
            }

            return "/" + string.Join("/", resultParts);
        }

        /// <summary>
        /// Builds the final Props dictionary for a route, merging (lowest → highest priority):
        /// PropsFn → DefaultProps → query → route params → explicit props.
        /// Route params and query are merged so <see cref="SusScreen.GetParam"/> /
        /// <see cref="SusScreen.GetQuery"/> work as documented.
        /// </summary>
        private static Dictionary<string, object> BuildMergedProps(
            SusRouteRecord record, SusRoute route, Dictionary<string, object> explicitProps)
        {
            var merged = new Dictionary<string, object>();

            // 1. PropsFn (base, function-based)
            if (record?.Config?.PropsFn != null)
            {
                var fnProps = record.Config.PropsFn(route);
                if (fnProps != null)
                    foreach (var kv in fnProps) merged[kv.Key] = kv.Value;
            }

            // 2. DefaultProps
            if (record?.Config?.DefaultProps != null)
                foreach (var kv in record.Config.DefaultProps) merged[kv.Key] = kv.Value;

            // 3. Query (?key=val) — lower priority than path params
            if (route?.Query != null)
                foreach (var kv in route.Query) merged[kv.Key] = kv.Value;

            // 4. Route params (:id) — override query on name clash
            if (route?.Params != null)
                foreach (var kv in route.Params) merged[kv.Key] = kv.Value;

            // 5. Explicit props (highest priority — caller intent wins)
            if (explicitProps != null)
                foreach (var kv in explicitProps) merged[kv.Key] = kv.Value;

            return merged;
        }

        // ════════════════════════════════════════════════════════════════
        //  Async Navigation (P2)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Async push — resolves the record synchronously, then runs guard pipeline
        /// with both sync and async guards, awaiting each.
        /// </summary>
        public async Task<NavigationResult> PushAsync(string path,
            Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
            {
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, path, null));
                return NavigationResult.NotFound;
            }

            var pathParams = record.Match(path);
            if (pathParams == null && record.ParamNames.Count > 0)
            {
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, path, null));
                return NavigationResult.NotFound;
            }

            var toRoute = new SusRoute(record, path, pathParams)
            {
                MatchedChain = ResolveChain(path) ?? new List<SusRouteRecord> { record }
            };
            toRoute.Props = BuildMergedProps(record, toRoute, props);

            var fromRoute = CurrentRoute.Value ?? SusRoute.None;
            return await NavigateAsync(fromRoute, toRoute, isReplace: false, stepOffset: 0);
        }

        /// <summary>
        /// Async replace.
        /// </summary>
        public async Task<NavigationResult> ReplaceAsync(string path,
            Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
            {
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, path, null));
                return NavigationResult.NotFound;
            }

            var pathParams = record.Match(path);
            if (pathParams == null && record.ParamNames.Count > 0)
            {
                FireError(NavigationResult.NotFound,
                    CurrentRoute.Value ?? SusRoute.None,
                    new SusRoute(null, path, null));
                return NavigationResult.NotFound;
            }

            var toRoute = new SusRoute(record, path, pathParams)
            {
                MatchedChain = ResolveChain(path) ?? new List<SusRouteRecord> { record }
            };
            toRoute.Props = BuildMergedProps(record, toRoute, props);

            var fromRoute = CurrentRoute.Value ?? SusRoute.None;
            return await NavigateAsync(fromRoute, toRoute, isReplace: true, stepOffset: 0);
        }

        private async Task<NavigationResult> NavigateAsync(
            SusRoute fromRoute, SusRoute toRoute, bool isReplace, int stepOffset)
        {
            // ── Async beforeEach guards ──
            foreach (var guard in _beforeEachAsyncGuards)
            {
                if (!await guard(fromRoute, toRoute))
                {
                    FireError(NavigationResult.Aborted, fromRoute, toRoute, "BeforeEachAsync");
                    return NavigationResult.Aborted;
                }
            }

            // ── Async beforeResolve guards (awaited before screen creation) ──
            foreach (var guard in _beforeResolveAsyncGuards)
            {
                if (!await guard(fromRoute, toRoute))
                {
                    FireError(NavigationResult.Aborted, fromRoute, toRoute, "BeforeResolveAsync");
                    return NavigationResult.Aborted;
                }
            }

            // ── Delegate to sync navigate core (runs sync guards + screen creation) ──
            return Navigate(fromRoute, toRoute, isReplace, stepOffset, asyncGuardsHandled: true);
        }
    }
}

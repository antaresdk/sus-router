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
        // ════════════════════════════════════════════════════════════════
        //  Modal windows
        // ════════════════════════════════════════════════════════════════

        // ─── ModalService ─────────────────────────────────────────────

        /// <summary>
        /// Sets the current route and prepopulates history for EditMode/PlayMode tests
        /// (InternalsVisibleTo test assemblies). Not part of the public buyer API.
        /// </summary>
        internal void SetCurrentForTest(SusRoute route)
        {
            _history.Clear();
            _history.Add(route);
            _historyIndex = 0;
            route.IsActive = true;
            CurrentRoute.Value = route;
        }

        /// <summary>
        /// Opens a modal dialog of <paramref name="dialogType"/> via <see cref="ModalService"/>.
        /// Requires <see cref="Init"/> / Mount so an overlay host exists.
        /// </summary>
        public void Modal(Type dialogType, Dictionary<string, object> props = null)
        {
            var modal = ModalService?.Show(dialogType, props);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (modal == null)
                SusLog.Warn($"[SusRouter.Modal] Failed to show modal '{dialogType.Name}'. Check OverlayHost init and MaxModalDepth.");
#endif
        }

        /// <summary>
        /// Closes the current modal (top of <see cref="ModalService"/> stack).
        /// </summary>
        public void CloseModal()
        {
            ModalService?.Close();
        }

        /// <summary>
        /// Assigns the outlet that renders the current route. Mount creates one;
        /// call this only for a custom outlet.
        /// </summary>
        public void SetRouteView(SusRouteView view)
        {
            _routeView = view;
        }

        // ════════════════════════════════════════════════════════════════
        //  Init / Mount
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wires overlay services (<see cref="ModalService"/>, <see cref="TransitionService"/>,
        /// <see cref="OverlayServices"/>) onto <paramref name="overlayHost"/> and registers
        /// a BeforeEach that closes all modals on navigation. No-op if already initialized;
        /// Mount calls this when no overlay host exists yet.
        /// </summary>
        public void Init(OverlayHost overlayHost)
        {
            if (overlayHost == null)
                throw new ArgumentNullException(nameof(overlayHost));

            if (_isInitialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SusLog.Warn("[SusRouter] Init() called more than once — no-op. " +
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

        /// <summary>
        /// Low-level mount: creates a <see cref="SusRouteView"/> on <paramref name="container"/>
        /// (or its <see cref="ScreenHost"/> slot), ensures an overlay host, then
        /// <see cref="Replace(string, Dictionary{string, object})"/>s to <paramref name="initialPath"/>.
        /// Prefer <c>SusApp.UseRouter</c> — it runs this at the documented <see cref="SusApp"/>
        /// finalization point (after the token cascade and layer scaffold, before the theme).
        /// Use this overload for tests, extra panels, or hosts that already applied TSS/scaffold.
        /// </summary>
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

        /// <summary>
        /// Convenience overload of <see cref="Mount(VisualElement, string, Dictionary{string, object})"/>
        /// targeting <paramref name="uiDocument"/>.rootVisualElement. Same low-level contract;
        /// prefer <c>SusApp.UseRouter</c> for application entry.
        /// </summary>
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

        /// <summary>
        /// Fills <c>:param</c> placeholders in <paramref name="record"/>.Path from <paramref name="pathParams"/>.
        /// Returns the template unchanged when there are no params; null when required params are missing.
        /// </summary>
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
                return FireNotFound(path);
            return await NavigateToRecordAsync(record, path, props, isReplace: false);
        }

        /// <summary>
        /// Async replace.
        /// </summary>
        public async Task<NavigationResult> ReplaceAsync(string path,
            Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
                return FireNotFound(path);
            return await NavigateToRecordAsync(record, path, props, isReplace: true);
        }

        /// <summary>
        /// Builds the target route from an already-resolved record and runs it through the
        /// async navigate pipeline. Shared by PushAsync/ReplaceAsync (the async counterpart
        /// of <see cref="NavigateToRecord"/>).
        /// </summary>
        private async Task<NavigationResult> NavigateToRecordAsync(SusRouteRecord record, string path,
            Dictionary<string, object> props, bool isReplace)
        {
            if (!TryBuildToRoute(record, path, props, out var toRoute))
                return NavigationResult.NotFound;

            var fromRoute = CurrentRoute.Value ?? SusRoute.None;
            return await NavigateAsync(fromRoute, toRoute, isReplace, stepOffset: 0);
        }

        /// <summary>
        /// Async navigate pipeline: awaits the async guards, then runs the same core+audit
        /// path as sync navigation.
        ///
        /// <b>Re-entrancy:</b> the <c>_isNavigating</c> guard is acquired HERE,
        /// before the first await, and held for the full method — including while awaiting
        /// BeforeEachAsync/BeforeResolveAsync — not just around the final sync step. Async
        /// guards can yield for real time; if the guard were acquired only inside
        /// <see cref="Navigate"/> (i.e. after these awaits), two concurrent NavigateAsync
        /// calls could both run their guards concurrently against the same stale
        /// fromRoute/CurrentRoute snapshot, and both eventually commit — the second one
        /// mutating a fromRoute object the first navigation had already torn down (its
        /// Screen nulled/Left() twice). Holding the guard up front makes the second call
        /// return Busy immediately instead of racing. Calls <see cref="RunCoreAndAudit"/>
        /// directly (not <see cref="Navigate"/>) for the final step, since re-entering
        /// Navigate would see this method's own guard as "busy" and abort.
        /// </summary>
        private async Task<NavigationResult> NavigateAsync(
            SusRoute fromRoute, SusRoute toRoute, bool isReplace, int stepOffset)
        {
            if (_isNavigating)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SusLog.Warn($"[SusRouter] Async navigation to '{toRoute?.FullPath}' dropped — router is busy (concurrent navigation). Resync UI to CurrentRoute.");
#endif
                return NavigationResult.Busy;
            }
            _isNavigating = true;
            try
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

                // ── Run sync guards + screen creation (re-entrancy guard already held above) ──
                return RunCoreAndAudit(fromRoute, toRoute, isReplace, stepOffset);
            }
            finally
            {
                _isNavigating = false;
            }
        }
    }
}

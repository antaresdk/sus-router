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

        /// <summary>
        /// Navigates to <paramref name="path"/>, pushing a new history entry (truncates the forward tail).
        /// Returns <see cref="NavigationResult.NotFound"/> when no route matches.
        /// </summary>
        public NavigationResult Push(string path, Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SusLog.Verbose($"[NavigationAudit] Push('{path}') — route not found.");
#endif
                return FireNotFound(path);
            }
            return NavigateToRecord(record, path, props, isReplace: false);
        }

        /// <summary>
        /// Navigates to <paramref name="path"/>, replacing the current history entry.
        /// Returns <see cref="NavigationResult.NotFound"/> when no route matches.
        /// </summary>
        public NavigationResult Replace(string path, Dictionary<string, object> props = null)
        {
            var record = Resolve(path);
            if (record == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SusLog.Verbose($"[NavigationAudit] Replace('{path}') — route not found.");
#endif
                return FireNotFound(path);
            }
            return NavigateToRecord(record, path, props, isReplace: true);
        }

        /// <summary>
        /// Pushes the named route, filling <c>:param</c> placeholders from <paramref name="pathParams"/>.
        /// Returns <see cref="NavigationResult.NotFound"/> when the name is unknown or params are missing.
        /// </summary>
        public NavigationResult PushNamed(string name,
            Dictionary<string, string> pathParams = null,
            Dictionary<string, object> props = null)
        {
            if (!_namedRoutes.TryGetValue(name, out var record))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SusLog.Verbose($"[NavigationAudit] PushNamed('{name}') — named route not found.");
#endif
                return FireNotFound(name);
            }
            var path = BuildPath(record, pathParams);
            if (path == null)
                return FireNotFound(name);
            return NavigateToRecord(record, path, props, isReplace: false);
        }

        /// <summary>
        /// Replaces the current history entry with the named route.
        /// Returns <see cref="NavigationResult.NotFound"/> when the name is unknown or params are missing.
        /// </summary>
        public NavigationResult ReplaceNamed(string name,
            Dictionary<string, string> pathParams = null,
            Dictionary<string, object> props = null)
        {
            if (!_namedRoutes.TryGetValue(name, out var record))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SusLog.Verbose($"[NavigationAudit] ReplaceNamed('{name}') — named route not found.");
#endif
                return FireNotFound(name);
            }
            var path = BuildPath(record, pathParams);
            if (path == null)
                return FireNotFound(name);
            return NavigateToRecord(record, path, props, isReplace: true);
        }

        /// <summary>
        /// Builds a concrete path for the named route using <paramref name="pathParams"/>.
        /// Returns null when the name is unknown or required params are missing.
        /// </summary>
        public string ResolvePath(string name, Dictionary<string, string> pathParams = null)
        {
            if (!_namedRoutes.TryGetValue(name, out var record))
                return null;
            return BuildPath(record, pathParams);
        }

        /// <summary>
        /// Fires a NotFound navigation error for <paramref name="pathOrName"/> (a path
        /// for Push/Replace/PushAsync/ReplaceAsync, a route name for PushNamed/ReplaceNamed)
        /// and returns NavigationResult.NotFound. Shared NotFound-reporting shape used by
        /// every navigation entry point.
        /// </summary>
        private NavigationResult FireNotFound(string pathOrName)
        {
            FireError(NavigationResult.NotFound,
                CurrentRoute.Value ?? SusRoute.None,
                new SusRoute(null, pathOrName, null));
            return NavigationResult.NotFound;
        }

        /// <summary>
        /// Resolves route params for <paramref name="record"/>/<paramref name="path"/>, builds
        /// the target SusRoute (MatchedChain + merged Props), and reports NotFound when the
        /// path params don't match the record. Shared route-build step used by every
        /// navigation entry point (sync and async).
        /// </summary>
        private bool TryBuildToRoute(SusRouteRecord record, string path,
            Dictionary<string, object> props, out SusRoute toRoute)
        {
            var pathParams = record.Match(path);
            if (pathParams == null && record.ParamNames.Count > 0)
            {
                FireNotFound(path);
                toRoute = null;
                return false;
            }

            toRoute = new SusRoute(record, path, pathParams)
            {
                MatchedChain = ResolveChain(path) ?? new List<SusRouteRecord> { record }
            };
            toRoute.Props = BuildMergedProps(record, toRoute, props);
            return true;
        }

        /// <summary>
        /// Builds the target route from an already-resolved record and runs it through the
        /// sync guard pipeline. Shared by Push/Replace/PushNamed/ReplaceNamed.
        /// </summary>
        private NavigationResult NavigateToRecord(SusRouteRecord record, string path,
            Dictionary<string, object> props, bool isReplace)
        {
            if (!TryBuildToRoute(record, path, props, out var toRoute))
                return NavigationResult.NotFound;

            var fromRoute = CurrentRoute.Value ?? SusRoute.None;
            return Navigate(fromRoute, toRoute, isReplace, stepOffset: 0);
        }

        /// <summary>
        /// Moves the history cursor one entry back. Returns <see cref="NavigationResult.CantGoBack"/>
        /// when already at the oldest entry.
        /// </summary>
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

        /// <summary>
        /// Moves the history cursor one entry forward. Returns <see cref="NavigationResult.CantGoForward"/>
        /// when already at the newest entry.
        /// </summary>
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

        /// <summary>
        /// Moves the history cursor by <paramref name="n"/> entries (positive = forward, negative = back).
        /// Stops and returns the first non-success result if a step cannot complete.
        /// </summary>
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
                SusLog.Verbose($"[NavigationAudit] NavigateWithTransition('{path}') — route not found.");
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

        /// <summary>
        /// Returns true if <paramref name="path"/> (query stripped) matches any history entry's path.
        /// For the current route including query, use <see cref="IsRouteActiveExact"/>.
        /// </summary>
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

        /// <summary>
        /// Returns true if <see cref="CurrentRoute"/>'s <see cref="SusRoute.FullPath"/> equals
        /// <paramref name="path"/> (query included).
        /// </summary>
        public bool IsRouteActiveExact(string path)
        {
            return CurrentRoute.Value?.FullPath == path;
        }

        // ════════════════════════════════════════════════════════════════
        //  Guard pipeline
        // ════════════════════════════════════════════════════════════════

        private NavigationResult Navigate(SusRoute fromRoute, SusRoute toRoute, bool isReplace,
            int stepOffset = 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Sync navigation cannot await async guards — warn so they are not silently skipped.
            if (_beforeEachAsyncGuards.Count > 0 || _beforeResolveAsyncGuards.Count > 0)
            {
                SusLog.Verbose(
                    "[GuardAudit] Sync navigation (Push/Replace/Back/Forward) skips async guards " +
                    "(BeforeEachAsync/BeforeResolveAsync). Use PushAsync/ReplaceAsync to run them.");
            }
#endif
            // ── Re-entrancy guard ──
            if (_isNavigating)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                SusLog.Warn($"[SusRouter] Navigation to '{toRoute?.FullPath}' dropped — router is busy (concurrent Push/Replace). Resync UI to CurrentRoute.");
#endif
                return NavigationResult.Busy;
            }
            _isNavigating = true;
            try
            {
                return RunCoreAndAudit(fromRoute, toRoute, isReplace, stepOffset);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// Runs NavigateCore and the surrounding dev-mode audit/FireError bookkeeping.
        /// Shared by the sync entry point (<see cref="Navigate"/>, which owns the
        /// <c>_isNavigating</c> re-entrancy guard) and <see cref="NavigateAsync"/> (which holds
        /// the same guard itself across its awaited guard pipeline — see the re-entrancy note
        /// there — and so calls this directly instead of re-entering <see cref="Navigate"/>).
        /// </summary>
        private NavigationResult RunCoreAndAudit(SusRoute fromRoute, SusRoute toRoute, bool isReplace, int stepOffset)
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
            if (result == NavigationResult.Aborted)
                SusLog.Verbose($"[GuardAudit] Nav from '{fromRoute.FullPath}' " +
                    $"→ '{toRoute.FullPath}' was rejected by a guard or lifecycle hook " +
                    $"(BeforeLeave/CanLeave/BeforeEach/CanEnter/BeforeResolve/BeforeEnter).");
#endif
            if (result != NavigationResult.Success)
                FireError(result, fromRoute, toRoute);
            return result;
        }

    }
}

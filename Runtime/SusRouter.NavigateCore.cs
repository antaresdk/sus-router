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
        // ── NavigateCore steps ──────────────────────────────────────────────
        // NavigateCore is split into five private steps, run in sequence. Each step
        // either mutates fromRoute/toRoute in place (both are reference types) or,
        // for the redirect step, reassigns toRoute wholesale via a ref parameter.

        private NavigationResult NavigateCore(SusRoute fromRoute, SusRoute toRoute, bool isReplace, int stepOffset)
        {
            // ── Step A: Redirect ──
            if (!TryResolveRedirect(ref toRoute))
                return NavigationResult.NotFound;

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

            // ── Step B: same-record param update (beforeRouteUpdate) ──
            if (fromRoute.Record == toRoute.Record && fromRoute.IsActive)
                return NavigateSameRecordUpdate(fromRoute, toRoute, isReplace, stepOffset, fromKeepAlive);

            // ── Step C: leave/enter guard pipeline (Steps 1–4.5) ──
            var guardResult = RunNavigationGuards(fromRoute, toRoute);
            if (guardResult != NavigationResult.Success)
                return guardResult;

            // ── Step D: teardown + BeforeResolve + create/reuse screens (Steps 5, 5.5, 6) ──
            var screenResult = PrepareTargetScreens(fromRoute, toRoute, fromKeepAlive, targetKeepAlive,
                out int chainNewDepth, out bool hasMultiLevelChain);
            if (screenResult != NavigationResult.Success)
                return screenResult;

            // ── Step E: stack update, RouteView, Entered, CurrentRoute + AfterEach (Steps 7–10) ──
            return FinalizeNavigation(fromRoute, toRoute, isReplace, stepOffset, fromKeepAlive,
                hasMultiLevelChain, chainNewDepth);
        }

        /// <summary>
        /// Step A: if the target route config has a Redirect path, resolves it and replaces
        /// <paramref name="toRoute"/> with the redirect target's route (carrying pre-redirect
        /// props as explicit/highest priority). Returns false (NotFound) when the redirect
        /// target itself does not resolve.
        /// </summary>
        private bool TryResolveRedirect(ref SusRoute toRoute)
        {
            if (string.IsNullOrEmpty(toRoute.Record?.Config?.Redirect))
                return true;

            var redirectRecord = Resolve(toRoute.Record.Config.Redirect);
            if (redirectRecord == null) return false;

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
            return true;
        }

        /// <summary>
        /// Step B: fromRoute and toRoute share the same route record and the screen is
        /// active — reuse the existing screen instance (BeforeRouteUpdate) instead of
        /// tearing it down and recreating it.
        /// </summary>
        private NavigationResult NavigateSameRecordUpdate(SusRoute fromRoute, SusRoute toRoute,
            bool isReplace, int stepOffset, bool fromKeepAlive)
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

            if (stepOffset == 0 || isReplace)
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

        /// <summary>
        /// Steps 1–4.5: leave/enter guard pipeline — BeforeLeave → CanLeave → BeforeEach →
        /// CanEnter → per-route BeforeEnter(fn). Returns Aborted from the first guard that
        /// rejects, else Success.
        /// </summary>
        private NavigationResult RunNavigationGuards(SusRoute fromRoute, SusRoute toRoute)
        {
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

            return NavigationResult.Success;
        }

        /// <summary>
        /// Steps 5, 5.5, 6: tear down the outgoing screen (Left), run BeforeResolve guards
        /// (before screen creation, so aborting has no side effects), then create or reuse
        /// the target screen(s) — chain-aware for nested routes. Outputs the chain depth info
        /// <see cref="FinalizeNavigation"/> needs for Entered().
        /// </summary>
        private NavigationResult PrepareTargetScreens(SusRoute fromRoute, SusRoute toRoute,
            bool fromKeepAlive, bool targetKeepAlive,
            out int chainNewDepth, out bool hasMultiLevelChain)
        {
            chainNewDepth = 0;
            hasMultiLevelChain = false;

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
            hasMultiLevelChain = chain != null && chain.Count > 1;
            // Chain levels at index >= chainNewDepth were freshly created (need Entered()).

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

            return NavigationResult.Success;
        }

        /// <summary>
        /// Steps 7–10: history stack update, RouteView mount/diff, Entered() on freshly
        /// created screens, and CurrentRoute + AfterEach hooks.
        /// </summary>
        private NavigationResult FinalizeNavigation(SusRoute fromRoute, SusRoute toRoute,
            bool isReplace, int stepOffset, bool fromKeepAlive,
            bool hasMultiLevelChain, int chainNewDepth)
        {
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
                    SusLog.Verbose($"[StackDepthAudit] Router history has {_history.Count} entries " +
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

    }
}

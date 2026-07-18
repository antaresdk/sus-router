using System.Collections.Generic;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Router screens outlet: renders the current route's <see cref="SusScreen"/>.
    ///
    /// The generic mounting + KeepAlive (LRU) plumbing lives in the core primitive
    /// <see cref="SusScreenOutlet{TScreen}"/>; this class adds only route-aware logic
    /// (<see cref="OnRouteChanged"/>, nested-chain rendering) and the screen teardown
    /// hook. Created by SusRouter.Mount(), one per router instance.
    /// </summary>
    public class SusRouteView : SusScreenOutlet<SusScreen>
    {
        /// <summary>
        /// USS class for nested outlets. Root mount also gets
        /// <c>sus-route-view--root</c> (absolute fill) in <see cref="SusRouter.Mount"/>.
        /// </summary>
        public const string UssClassName = "sus-route-view";

        /// <summary>Root outlet mounted by <see cref="SusRouter.Mount"/> — fills container.</summary>
        public const string RootUssClassName = "sus-route-view--root";

        /// <summary>Reference to the owning router.</summary>
        public SusRouter Router { get; set; }

        public SusRouteView()
        {
            AddToClassList(UssClassName);
        }

        /// <summary>Run the screen lifecycle teardown when evicted from KeepAlive.</summary>
        protected override void OnScreenEvicted(SusScreen screen) => screen.Left();

        /// <summary>
        /// Called by SusRouter.Navigate() when the route changes.
        /// Handles screen swap including KeepAlive caching.
        /// </summary>
        public void OnRouteChanged(SusRoute fromRoute, SusRoute toRoute)
        {
            var fromKeepAlive = fromRoute?.Record?.Config?.KeepAlive ?? false;

            // A nested chain can reuse the shared-prefix root screen instance across
            // navigations. In that case fromRoute.Screen == toRoute.Screen — do NOT
            // remove/cache it (that would tear down its child views).
            var reusedRoot = fromRoute?.Screen != null && fromRoute.Screen == toRoute?.Screen;

            // ── Remove old screen (if not KeepAlive) ──
            if (!reusedRoot && fromRoute != null && fromRoute.Screen != null && !fromKeepAlive)
            {
                if (fromRoute.Screen.parent != null)
                    fromRoute.Screen.parent.Remove(fromRoute.Screen);
            }
            else if (!reusedRoot && fromRoute != null && fromRoute.Screen != null && fromKeepAlive)
            {
                // KeepAlive: cache the screen instead of removing
                if (fromRoute.Screen.parent != null)
                    fromRoute.Screen.parent.Remove(fromRoute.Screen);

                CacheKeepAliveScreen(Router?.KeepAliveKey(fromRoute) ?? fromRoute.FullPath, fromRoute.Screen);
                fromRoute.Screen = null;
                fromRoute.IsActive = false;
            }

            // ── Show new screen ──
            if (toRoute?.Screen != null)
            {
                if (toRoute.Screen.parent != this)
                    Add(toRoute.Screen);
                toRoute.Screen.style.flexGrow = 1f;
                toRoute.IsActive = true;

                if (toRoute != fromRoute)
                    CurrentScreen = toRoute.Screen;
            }
        }

        /// <summary>
        /// Mounts a nested chain of screens. The root (index 0) is already rendered
        /// into this view by <see cref="OnRouteChanged"/>; each subsequent screen is
        /// mounted into the previous screen's registered ChildView (a nested SusRouteView).
        /// </summary>
        public void RenderNestedChain(List<SusScreen> chainScreens)
        {
            if (chainScreens == null || chainScreens.Count <= 1) return;

            for (int i = 1; i < chainScreens.Count; i++)
            {
                var parent = chainScreens[i - 1];
                var child = chainScreens[i];
                if (parent == null || child == null) continue;

                var childView = parent.ChildView;
                if (childView == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    UnityEngine.Debug.LogWarning(
                        $"[NestedRouting] Screen '{parent.GetType().Name}' has no registered " +
                        $"ChildView — nested child '{child.GetType().Name}' cannot mount. " +
                        $"Add a <SusRouteView> to the parent template or call RegisterChildView().");
#endif
                    continue;
                }

                childView.MountChild(child);
            }
        }
    }
}

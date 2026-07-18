using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>Navigation mode for SusRouteLink.</summary>
    public enum LinkMode { Push, Replace }

    /// <summary>
    /// Navigation link component. Renders a clickable element that triggers
    /// router navigation. Supports active-state CSS classes.
    ///
    /// Usage:
    ///   var link = new SusRouteLink { To = "/home", Exact = true };
    ///   link.Bind(myRouter);
    ///   Add(link);
    ///
    /// The link automatically subscribes to CurrentRoute.Changed on Bind()
    /// and refreshes router-link-active / router-link-exact-active CSS classes.
    /// </summary>
    public class SusRouteLink : VisualElement
    {
        /// <summary>Target route path.</summary>
        public string To { get; set; }

        /// <summary>Whether to use exact match for active class.</summary>
        public bool Exact { get; set; }

        /// <summary>Navigation mode: Push (default) or Replace.</summary>
        public LinkMode Mode { get; set; } = LinkMode.Push;

        /// <summary>Reference to the router (set via Bind()).</summary>
        public SusRouter Router { get; private set; }

        public SusRouteLink()
        {
            this.AddToClassList("sus-route-link");

            RegisterCallback<ClickEvent>(OnClick);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Sharq.Core.Diagnostics.ClickAuditService.Instance.Register(this, "route-link");
#endif
        }

        private void OnClick(ClickEvent evt)
        {
            if (Router == null || string.IsNullOrEmpty(To)) return;
            if (Mode == LinkMode.Replace)
                Router.Replace(To);
            else
                Router.Push(To);
            evt.StopPropagation();
        }

        /// <summary>
        /// Binds this link to a router. Subscribes to route changes
        /// and automatically refreshes active CSS classes.
        /// </summary>
        public void Bind(SusRouter router)
        {
            Router = router;
            if (Router?.CurrentRoute != null)
            {
                Router.CurrentRoute.Changed += (o, n) => RefreshActiveClass();
            }
        }

        /// <summary>
        /// Refreshes active CSS classes based on current route.
        /// Called automatically when bound to a router.
        /// </summary>
        public void RefreshActiveClass()
        {
            if (Router?.CurrentRoute?.Value == null) return;

            var currentPath = Router.CurrentRoute.Value.FullPath;
            // Strip query for comparison
            var qIdx = currentPath.IndexOf('?');
            var currentPathOnly = qIdx >= 0 ? currentPath.Substring(0, qIdx) : currentPath;

            EnableInClassList("router-link-active", !Exact && currentPathOnly.StartsWith(To));
            EnableInClassList("router-link-exact-active", Exact && currentPathOnly == To);
        }
    }
}

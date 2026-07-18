using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Route guard interface — analogous to navigation guards in Vue Router.
    /// Can be global (BeforeEach) or per-route (SusRouteConfig.Guard).
    /// </summary>
    public interface ISusRouteGuard
    {
        /// <summary>Checks whether the route may be entered. Returning false aborts navigation.</summary>
        bool CanEnter(SusRoute from, SusRoute to);

        /// <summary>Checks whether the route may be left. Returning false aborts navigation.</summary>
        bool CanLeave(SusRoute from, SusRoute to);
    }
}

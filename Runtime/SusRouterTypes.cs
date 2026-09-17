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
        /// <summary>Outcome of the failed navigation.</summary>
        public NavigationResult Result;
        /// <summary>Route being left, or <see cref="SusRoute.None"/> when there is no current route.</summary>
        public SusRoute From;
        /// <summary>Intended target of the failed navigation.</summary>
        public SusRoute To;
        /// <summary>Which guard/lifecycle hook rejected the navigation: "CanLeave", "BeforeEach", "CanEnter", "BeforeEnter", "BeforeResolve", "BeforeEnter(screen)".</summary>
        public string RejectedBy;
    }
}

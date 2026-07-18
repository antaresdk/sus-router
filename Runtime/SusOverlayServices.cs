using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Aggregates all overlay-related services for SusRouter.
    /// Provides a single access point to ModalService, TransitionService, and OverlayHost.
    /// </summary>
    public class SusOverlayServices
    {
        public OverlayHost Host { get; }
        public SusModalService Modal { get; }
        public SusTransitionService Transition { get; }

        public SusOverlayServices(OverlayHost host, SusModalService modal, SusTransitionService transition)
        {
            Host = host;
            Modal = modal;
            Transition = transition;
        }
    }
}

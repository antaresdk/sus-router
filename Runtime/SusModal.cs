using System.Collections.Generic;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Base class for router-managed modal dialog screens.
    /// Extends <see cref="SusModalBase"/> (sus-core) — so a router modal IS a real
    /// <see cref="SusComponent"/> pinned to the Modal overlay layer (C2 two-tier model),
    /// while adding router navigation on top. It is NOT a route/screen.
    ///
    /// Lifecycle:
    ///   1. Constructor → SusComponent base ctor runs Created() → Build()
    ///   2. Router and Props are set by SusModalService
    ///   3. Shown() — modal is in the overlay DOM, ready to animate
    ///   4. BeforeDismiss() — guard (return false to prevent close)
    ///   5. Dismissed() — cleanup before removal
    ///
    /// Usage:
    ///   router.ModalService.Show(typeof(MyDialog), new() { ["title"] = "Hello" });
    ///   // Inside the dialog:
    ///   Dismiss(); // close self
    /// </summary>
    public abstract class SusRouterModal : SusModalBase
    {
        /// <summary>Reference to the router (injected by SusModalService).</summary>
        public SusRouter Router { get; set; }

        /// <summary>Props passed during Show().</summary>
        public Dictionary<string, object> Props { get; set; }

        /// <summary>Reference back to the modal service that owns us.</summary>
        internal SusModalService ModalService { get; set; }

        // Build() is declared abstract on SusComponent and invoked by its constructor;
        // subclasses override it. Router/Props are NOT yet available inside Build()
        // (same contract as SusScreen.Build()).

        /// <summary>
        /// Called AFTER the modal is added to the overlay DOM.
        /// Router and Props ARE available here.
        /// </summary>
        protected internal virtual void Shown() { }

        /// <summary>
        /// Guard called before the modal is closed.
        /// Return false to prevent dismissal.
        /// </summary>
        public virtual bool BeforeDismiss() => true;

        /// <summary>
        /// Called before the modal is removed from DOM.
        /// Clean up resources here.
        /// </summary>
        public virtual void Dismissed() { }

        /// <summary>
        /// Close this modal (triggers BeforeDismiss → Dismissed).
        /// </summary>
        protected void Dismiss()
        {
            ModalService?.Close();
        }
    }
}

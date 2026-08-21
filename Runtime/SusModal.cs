using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// Awaitable result (ShowAsync):
    ///   Complete(result) stores the value and dismisses; Close/Dismiss without
    ///   Complete yields <see cref="AsyncDismissResult"/> (default null).
    ///
    /// Usage:
    ///   router.ModalService.Show(typeof(MyDialog), new() { ["title"] = "Hello" });
    ///   var answer = await router.ModalService.ShowAsync&lt;string&gt;(typeof(MyDialog));
    ///   // Inside the dialog:
    ///   Complete("ok"); // or Dismiss() for cancel/default
    /// </summary>
    public abstract class SusRouterModal : SusModalBase
    {
        /// <summary>Reference to the router (injected by SusModalService).</summary>
        public SusRouter Router { get; set; }

        /// <summary>Props passed during Show().</summary>
        public Dictionary<string, object> Props { get; set; }

        /// <summary>Reference back to the modal service that owns us.</summary>
        internal SusModalService ModalService { get; set; }

        object _asyncResult;
        bool _hasAsyncResult;
        TaskCompletionSource<object> _asyncTcs;

        // Build() is declared abstract on SusComponent and invoked by its constructor;
        // subclasses override it. Router/Props are NOT yet available inside Build()
        // (same contract as SusScreen.Build()).

        /// <summary>
        /// Called by <see cref="SusModalService"/> AFTER the modal is added to the overlay DOM.
        /// Delegates to <see cref="Shown"/>. Do NOT override this method — override Shown instead.
        /// </summary>
        public void NotifyShown() => Shown();

        /// <summary>
        /// Override to react after the modal is in the overlay DOM.
        /// Router and Props ARE available here.
        /// </summary>
        protected virtual void Shown() { }

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
        /// Result used when the modal is closed without <see cref="Complete"/>.
        /// Override for typed cancel sentinels (e.g. -1 for choice index).
        /// </summary>
        protected virtual object AsyncDismissResult => null;

        /// <summary>
        /// Complete an awaitable <see cref="SusModalService.ShowAsync{TResult}"/> with
        /// <paramref name="result"/> and dismiss this modal.
        /// </summary>
        public void Complete(object result)
        {
            _asyncResult = result;
            _hasAsyncResult = true;
            Dismiss();
        }

        internal void BindAsyncCompletion(TaskCompletionSource<object> tcs)
        {
            _asyncTcs = tcs;
        }

        internal void NotifyAsyncCompleted()
        {
            if (_asyncTcs == null) return;
            var tcs = _asyncTcs;
            _asyncTcs = null;
            var value = _hasAsyncResult ? _asyncResult : AsyncDismissResult;
            tcs.TrySetResult(value);
        }

        /// <summary>
        /// Close this modal (triggers BeforeDismiss → Dismissed).
        /// </summary>
        protected void Dismiss()
        {
            ModalService?.Close();
        }
    }

    /// <summary>
    /// Typed router modal: <see cref="Complete(TResult)"/> boxes into the shared async path.
    /// </summary>
    public abstract class SusRouterModal<TResult> : SusRouterModal
    {
        /// <summary>Complete with a typed result and dismiss.</summary>
        public void Complete(TResult result) => base.Complete(result);

        /// <inheritdoc />
        protected override object AsyncDismissResult => default(TResult);
    }
}

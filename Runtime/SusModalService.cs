using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Modal dialog manager. Stack of SusRouterModal instances rendered through
    /// OverlayHost (Modal category). Last shown = topmost.
    ///
    /// Modals are NOT screens — they extend SusRouterModal (not SusScreen),
    /// have their own lifecycle (Shown / BeforeDismiss / Dismissed),
    /// and are explicitly placed in OverlayHost.
    ///
    /// Focus lifecycle (ARCH-LUNA-GAP-B D2–D3): on first Show (0→1) snapshot
    /// background <c>focusable</c>+<c>pickingMode</c>, install focus trap on the
    /// wrapper, focus first focusable inside the modal; on last Close (1→0)
    /// restore background and prior focus.
    ///
    /// Awaitable: <see cref="ShowAsync{TResult}(Type, Dictionary{string, object})"/> /
    /// <see cref="ShowAsync{TResult}(Func{SusRouterModal}, Dictionary{string, object})"/> —
    /// completes when the modal is dismissed (via <see cref="SusRouterModal.Complete"/>
    /// or Close without a result).
    ///
    /// Usage:
    ///   router.ModalService.Show(typeof(MyModal));
    ///   router.ModalService.Show(typeof(MyModal), new() { { "mode", "login" } });
    ///   var answer = await router.ModalService.ShowAsync&lt;string&gt;(typeof(MyModal));
    ///   router.ModalService.Close();
    ///   router.ModalService.CloseAll();
    /// </summary>
    public class SusModalService
    {
        public OverlayHost OverlayHost { get; set; }

        /// <summary>Reference to the router. Needed to pass to modal instances.</summary>
        public SusRouter Router { get; set; }

        /// <summary>Reactive stack depth. Updated on every Show/Close/CloseAll.</summary>
        public Prop<int> CountProp { get; } = new(0);

        private readonly Stack<ModalEntry> _stack = new();
        static StyleSheet _susModalSheet;

        List<BackgroundInteractState> _backgroundSnapshot;
        WeakReference<VisualElement> _priorFocus;

        private void SyncCount() => CountProp.Value = _stack.Count;

        private class ModalEntry
        {
            public SusRouterModal Modal;
            public VisualElement Wrapper; // scrim + contentBox
            public OverlayEntry Overlay;
            public TaskCompletionSource<object> AsyncTcs;
        }

        struct BackgroundInteractState
        {
            public VisualElement Element;
            public bool Focusable;
            public PickingMode PickingMode;
        }

        /// <summary>
        /// Maximum number of stacked modals. When exceeded, Show() logs a
        /// warning and does NOT push a new modal (prevents stack overflow).
        /// 0 = unlimited (default).
        /// </summary>
        public int MaxModalDepth { get; set; } = 0;

        /// <summary>
        /// Shows a modal dialog. The type must inherit SusModal.
        /// </summary>
        public SusRouterModal Show(Type modalType, Dictionary<string, object> props = null)
        {
            if (modalType == null) throw new ArgumentNullException(nameof(modalType));
            if (!typeof(SusRouterModal).IsAssignableFrom(modalType))
                throw new ArgumentException($"Modal type {modalType.Name} must inherit SusRouterModal", nameof(modalType));

            return ShowCore(() => (SusRouterModal)Activator.CreateInstance(modalType), props, modalType.Name);
        }

        /// <summary>
        /// Shows a modal created by <paramref name="factory"/>.
        /// </summary>
        public SusRouterModal Show(Func<SusRouterModal> factory, Dictionary<string, object> props = null)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return ShowCore(factory, props, "factory");
        }

        /// <summary>
        /// Shows a modal and awaits its result. Completes when the modal is dismissed —
        /// via <see cref="SusRouterModal.Complete"/> or Close without an explicit result
        /// (<see cref="SusRouterModal"/> dismiss default / typed subclass default).
        /// </summary>
        public Task<TResult> ShowAsync<TResult>(Type modalType, Dictionary<string, object> props = null)
        {
            if (modalType == null) throw new ArgumentNullException(nameof(modalType));
            if (!typeof(SusRouterModal).IsAssignableFrom(modalType))
                throw new ArgumentException($"Modal type {modalType.Name} must inherit SusRouterModal", nameof(modalType));

            return ShowAsyncCore<TResult>(() => (SusRouterModal)Activator.CreateInstance(modalType), props, modalType.Name);
        }

        /// <summary>
        /// Shows a factory-built modal and awaits its result.
        /// </summary>
        public Task<TResult> ShowAsync<TResult>(Func<SusRouterModal> factory, Dictionary<string, object> props = null)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return ShowAsyncCore<TResult>(factory, props, "factory");
        }

        Task<TResult> ShowAsyncCore<TResult>(Func<SusRouterModal> factory, Dictionary<string, object> props, string debugName)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var modal = ShowCore(factory, props, debugName, tcs);
            if (modal == null)
            {
                tcs.TrySetResult(default(TResult));
                return UnwrapAsyncResult<TResult>(tcs.Task);
            }

            return UnwrapAsyncResult<TResult>(tcs.Task);
        }

        static async Task<TResult> UnwrapAsyncResult<TResult>(Task<object> boxed)
        {
            var value = await boxed.ConfigureAwait(true);
            if (value is TResult typed)
                return typed;
            if (value == null)
                return default;
            return (TResult)value;
        }

        SusRouterModal ShowCore(
            Func<SusRouterModal> factory,
            Dictionary<string, object> props,
            string debugName,
            TaskCompletionSource<object> asyncTcs = null)
        {
            if (OverlayHost == null)
            {
                SusLog.Error("[SusModalService] OverlayHost is null. Call Router.Init(overlayHost) first.");
                return null;
            }
            if (MaxModalDepth > 0 && _stack.Count >= MaxModalDepth)
            {
                SusLog.Warn($"[SusModalService] MaxModalDepth ({MaxModalDepth}) exceeded. " +
                    $"Rejecting Show({debugName}). Close existing modals first.");
                return null;
            }

            var openingFirst = _stack.Count == 0;
            if (openingFirst)
            {
                CapturePriorFocus();
                SnapshotBackgroundInteractivity();
            }

            var modal = factory();
            if (modal == null)
                throw new InvalidOperationException($"Modal factory for {debugName} returned null.");

            modal.Router = Router;
            modal.ModalService = this;
            modal.Props = props ?? new Dictionary<string, object>();

            // ── Wrapper: full-screen scrim + centered content (SusModal.g.uss) ──
            var wrapper = new VisualElement { name = "modal-wrapper" };
            wrapper.AddToClassList("modal-wrapper");
            wrapper.pickingMode = PickingMode.Position;
            EnsureSusModalSheet(wrapper);

            // Scrim — dims background, click outside dismisses via Close()
            // (respects BeforeDismiss; keeps SusModalService stack in sync —
            // OverlayHost dismissOnClickOutside alone would desync our stack).
            var scrim = new VisualElement { name = "modal-scrim" };
            scrim.AddToClassList("modal-scrim");
            scrim.pickingMode = PickingMode.Position;
            scrim.RegisterCallback<ClickEvent>(evt =>
            {
                Close();
                evt.StopPropagation();
            });
            wrapper.Add(scrim);

            // Centered content box — sizes to child (do NOT StretchFill the modal)
            var contentBox = new VisualElement { name = "modal-content" };
            contentBox.AddToClassList("modal-content");
            contentBox.Add(modal);
            wrapper.Add(contentBox);

            // ── Add to overlay host ──
            // dismissOnClickOutside: false — dismissal is owned by scrim → Close()
            // so BeforeDismiss / Dismissed / CountProp stay consistent.
            var overlayEntry = OverlayHost.AddToOverlay(wrapper, OverlayCategory.Modal,
                dismissOnClickOutside: false);

            OverlayHost.InstallFocusTrap(wrapper);

            if (asyncTcs != null)
                modal.BindAsyncCompletion(asyncTcs);

            _stack.Push(new ModalEntry
            {
                Modal = modal,
                Wrapper = wrapper,
                Overlay = overlayEntry,
                AsyncTcs = asyncTcs,
            });

            SyncCount();

            // ── Lifecycle: Shown + focus-in on next panel update (DOM ready) ──
            modal.schedule.Execute(() =>
            {
                modal.NotifyShown();
                SusFocusUtil.FindFirstFocusable(modal)?.Focus();
            });

            return modal;
        }

        /// <summary>
        /// Closes the topmost modal. Calls BeforeDismiss → Dismissed → remove.
        /// </summary>
        public void Close()
        {
            if (_stack.Count == 0) return;
            var entry = _stack.Pop();
            CloseEntry(entry, force: false);
            SyncCount();
            if (_stack.Count == 0)
                OnModalStackEmptied();
        }

        /// <summary>
        /// Closes all open modals. Forces dismissal (ignores BeforeDismiss) so
        /// a refusing top modal cannot trap the stack / TearDown in a loop.
        /// </summary>
        public void CloseAll()
        {
            while (_stack.Count > 0)
            {
                var entry = _stack.Pop();
                CloseEntry(entry, force: true);
            }
            SyncCount();
            OnModalStackEmptied();
        }

        /// <summary>
        /// Number of currently open modals.
        /// </summary>
        public int Count => _stack.Count;

        void OnModalStackEmptied()
        {
            RestoreBackgroundInteractivity();
            RestorePriorFocus();
        }

        void CapturePriorFocus()
        {
            var focused = OverlayHost?.focusController?.focusedElement as VisualElement;
            _priorFocus = focused != null
                ? new WeakReference<VisualElement>(focused)
                : null;
        }

        void RestorePriorFocus()
        {
            if (_priorFocus != null
                && _priorFocus.TryGetTarget(out var prior)
                && prior.panel != null
                && SusFocusUtil.IsFocusCandidate(prior))
            {
                prior.Focus();
                _priorFocus = null;
                return;
            }

            _priorFocus = null;
            Router?.CurrentRoute?.Value?.Screen?.ApplyAutoFocus();
        }

        /// <summary>
        /// Snapshot ScreenHost (or OverlayHost siblings) focusable+pickingMode once per 0→1.
        /// Does not touch the modal wrapper / OverlayHost itself.
        /// </summary>
        internal void SnapshotBackgroundInteractivity()
        {
            if (_backgroundSnapshot != null) return;
            _backgroundSnapshot = new List<BackgroundInteractState>(64);

            foreach (var root in EnumerateBackgroundRoots())
                WalkSnapshot(root);
        }

        /// <summary>
        /// Restore background interactivity once per 1→0.
        /// </summary>
        internal void RestoreBackgroundInteractivity()
        {
            if (_backgroundSnapshot == null) return;

            for (int i = _backgroundSnapshot.Count - 1; i >= 0; i--)
            {
                var s = _backgroundSnapshot[i];
                if (s.Element == null) continue;
                s.Element.focusable = s.Focusable;
                s.Element.pickingMode = s.PickingMode;
            }

            _backgroundSnapshot = null;
        }

        IEnumerable<VisualElement> EnumerateBackgroundRoots()
        {
            var parent = OverlayHost?.parent;
            if (parent == null) yield break;

            var screenHost = parent.Q<ScreenHost>();
            if (screenHost != null)
            {
                yield return screenHost;
                yield break;
            }

            foreach (var child in parent.Children())
            {
                if (ReferenceEquals(child, OverlayHost)) continue;
                yield return child;
            }
        }

        void WalkSnapshot(VisualElement ve)
        {
            if (ve == null) return;
            if (ve is OverlayHost) return;

            if (ve.focusable || ve.pickingMode != PickingMode.Ignore)
            {
                _backgroundSnapshot.Add(new BackgroundInteractState
                {
                    Element = ve,
                    Focusable = ve.focusable,
                    PickingMode = ve.pickingMode,
                });
                ve.focusable = false;
                ve.pickingMode = PickingMode.Ignore;
            }

            var count = ve.childCount;
            for (int i = 0; i < count; i++)
                WalkSnapshot(ve[i]);
        }

        private void CloseEntry(ModalEntry entry, bool force)
        {
            if (entry == null) return;
            if (entry.Modal == null) return;

            if (!force && !entry.Modal.BeforeDismiss())
            {
                // Guard cancelled — push back onto stack
                _stack.Push(entry);
                return;
            }

            entry.Modal.Dismissed();
            OverlayHost?.RemoveFromOverlay(entry.Overlay);
            entry.Modal.NotifyAsyncCompleted();
        }

        /// <summary>
        /// Optional modal card styling. Loaded from any package's Resources/SusRuntime
        /// (downstream component libraries ship their modal styles there). Router itself has no such dependency;
        /// without it the modal host is unstyled but fully functional.
        /// </summary>
        static void EnsureSusModalSheet(VisualElement host)
        {
            if (host == null) return;
            if (_susModalSheet == null)
                _susModalSheet = Resources.Load<StyleSheet>("SusRuntime/SusModal.g");
            if (_susModalSheet != null && !host.styleSheets.Contains(_susModalSheet))
                host.styleSheets.Add(_susModalSheet);
        }
    }
}

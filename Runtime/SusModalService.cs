using System.Collections.Generic;
using System;
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
    /// Usage:
    ///   router.ModalService.Show(typeof(MyModal));
    ///   router.ModalService.Show(typeof(MyModal), new() { { "mode", "login" } });
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

        private void SyncCount() => CountProp.Value = _stack.Count;

        private class ModalEntry
        {
            public SusRouterModal Modal;
            public VisualElement Wrapper; // scrim + contentBox
            public OverlayEntry Overlay;
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
            if (OverlayHost == null)
            {
                Debug.LogError("[SusModalService] OverlayHost is null. Call Router.Init(overlayHost) first.");
                return null;
            }
            if (MaxModalDepth > 0 && _stack.Count >= MaxModalDepth)
            {
                Debug.LogWarning($"[SusModalService] MaxModalDepth ({MaxModalDepth}) exceeded. " +
                    $"Rejecting Show({modalType.Name}). Close existing modals first.");
                return null;
            }

            var modal = (SusRouterModal)Activator.CreateInstance(modalType);
            modal.Router = Router;
            modal.ModalService = this;
            modal.Props = props ?? new Dictionary<string, object>();

            // ── Wrapper: full-screen scrim + centered content (SusModal.g.uss) ──
            var wrapper = new VisualElement { name = "modal-wrapper" };
            wrapper.AddToClassList("modal-wrapper");
            wrapper.pickingMode = PickingMode.Position;
            EnsureSusModalSheet(wrapper);

            // Scrim — dims background, click outside dismisses
            var scrim = new VisualElement { name = "modal-scrim" };
            scrim.AddToClassList("modal-scrim");
            scrim.pickingMode = PickingMode.Position;
            wrapper.Add(scrim);

            // Centered content box — sizes to child (do NOT StretchFill the modal)
            var contentBox = new VisualElement { name = "modal-content" };
            contentBox.AddToClassList("modal-content");
            contentBox.Add(modal);
            wrapper.Add(contentBox);

            // ── Add to overlay host ──
            var overlayEntry = OverlayHost.AddToOverlay(wrapper, OverlayCategory.Modal,
                dismissOnClickOutside: true);

            _stack.Push(new ModalEntry { Modal = modal, Wrapper = wrapper, Overlay = overlayEntry });

            SyncCount();

            // ── Lifecycle: Shown after DOM is ready ──
            modal.schedule.Execute(() =>
            {
                modal.Shown();
            }).ExecuteLater(16);

            return modal;
        }

        /// <summary>
        /// Closes the topmost modal. Calls BeforeDismiss → Dismissed → remove.
        /// </summary>
        public void Close()
        {
            if (_stack.Count == 0) return;
            var entry = _stack.Pop();
            CloseEntry(entry);
            SyncCount();
        }

        /// <summary>
        /// Closes all open modals.
        /// </summary>
        public void CloseAll()
        {
            while (_stack.Count > 0)
            {
                var entry = _stack.Pop();
                CloseEntry(entry);
            }
            SyncCount();
        }

        /// <summary>
        /// Number of currently open modals.
        /// </summary>
        public int Count => _stack.Count;

        private void CloseEntry(ModalEntry entry)
        {
            if (entry == null) return;
            if (entry.Modal == null) return;

            if (!entry.Modal.BeforeDismiss())
            {
                // Guard cancelled — push back onto stack
                _stack.Push(entry);
                return;
            }

            entry.Modal.Dismissed();
            OverlayHost?.RemoveFromOverlay(entry.Overlay);
        }

        /// <summary>
        /// Optional modal card styling. Loaded from any package's Resources/SusRuntime
        /// (sus-kit ships SusModal.g there). Router itself does not depend on sus-kit;
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

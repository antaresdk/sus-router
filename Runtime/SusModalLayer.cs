using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// [Obsolete] Use SusModalService via router.ModalService.Show().
    /// Kept for backward compatibility. Will be removed in the next major version.
    /// </summary>
    [Obsolete("Use SusModalService via router.ModalService.Show() instead.")]
    public class SusModalLayer : VisualElement
    {
        /// <summary>Reference to the router.</summary>
        public SusRouter Router { get; set; }

        private readonly VisualElement _overlay;
        private readonly VisualElement _dialogContainer;
        private SusScreen _currentDialog;

        public SusModalLayer()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.top = 0;
            style.left = 0;
            style.right = 0;
            style.bottom = 0;

            // Dimmed backdrop
            _overlay = new VisualElement
            {
                pickingMode = PickingMode.Position,
            };
            _overlay.style.position = Position.Absolute;
            _overlay.style.top = 0;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new StyleColor(new UnityEngine.Color(0f, 0f, 0f, 0.5f));
            _overlay.style.display = DisplayStyle.None;
            _overlay.RegisterCallback<ClickEvent>(_ => { /* backdrop click — no-op */ });
            Add(_overlay);

            // Dialog container (centered)
            _dialogContainer = new VisualElement
            {
                pickingMode = PickingMode.Position,
            };
            _dialogContainer.style.position = Position.Absolute;
            _dialogContainer.style.top = 0;
            _dialogContainer.style.left = 0;
            _dialogContainer.style.right = 0;
            _dialogContainer.style.bottom = 0;
            _dialogContainer.style.alignItems = Align.Center;
            _dialogContainer.style.justifyContent = Justify.Center;
            _dialogContainer.style.display = DisplayStyle.None;
            Add(_dialogContainer);
        }

        /// <summary>
        /// Shows a modal dialog.
        /// </summary>
        protected internal virtual void ShowModal(Type dialogType, Dictionary<string, object> props)
        {
            CloseModal(); // close previous if any

            if (dialogType == null || !typeof(SusScreen).IsAssignableFrom(dialogType))
                return;

            var dialog = (SusScreen)Activator.CreateInstance(dialogType);
            dialog.Router = Router;
            dialog.Props = props ?? new Dictionary<string, object>();

            // BeforeEnter on the dialog
            if (!dialog.BeforeEnter(SusRoute.None))
                return;

            _currentDialog = dialog;

            // Show overlay
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.style.opacity = 0f;
            _overlay.schedule.Execute(() => _overlay.style.opacity = 1f).StartingIn(16);

            // Show dialog
            _dialogContainer.style.display = DisplayStyle.Flex;
            _dialogContainer.Add(dialog);
            dialog.Entered();
        }

        /// <summary>
        /// Closes the current modal dialog.
        /// </summary>
        protected internal virtual void CloseModal()
        {
            if (_currentDialog == null) return;

            _currentDialog.BeforeLeave(SusRoute.None);
            _currentDialog.Left();

            _dialogContainer.Remove(_currentDialog);
            _dialogContainer.style.display = DisplayStyle.None;
            _overlay.style.display = DisplayStyle.None;

            _currentDialog = null;
        }
    }
}

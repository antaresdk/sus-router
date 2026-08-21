using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// T-677 / ARCH-LUNA-GAP-B D1–D4 — PlayMode focus lifecycle:
    /// screen AutoFocus, modal background block, trap focus-in, restore on Close.
    /// </summary>
    public class SusFocusLifecyclePlaymodeTests
    {
        GameObject _go;
        UIDocument _doc;
        OverlayHost _host;
        ScreenHost _screenHost;
        SusRouter _router;
        SusModalService _svc;

        class FocusScreen : SusScreen
        {
            public Button FirstBtn;
            public Button SecondBtn;

            protected override void Build()
            {
                FirstBtn = new Button { name = "screen-first", text = "First", focusable = true };
                SecondBtn = new Button { name = "screen-second", text = "Second", focusable = true };
                Add(FirstBtn);
                Add(SecondBtn);
            }
        }

        class FocusModal : SusRouterModal
        {
            public Button OkBtn;

            protected override void Build()
            {
                OkBtn = new Button { name = "modal-ok", text = "OK", focusable = true };
                Add(OkBtn);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestFocusLifecycleUI", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();
            _doc.panelSettings = SusTestPanelFactory.Create("SusTestPanelSettings_Focus");

            var root = _doc.rootVisualElement;
            _screenHost = new ScreenHost();
            _host = new OverlayHost { name = OverlayHost.OverlayHostName };
            root.Add(_screenHost);
            root.Add(_host);

            _router = new SusRouter();
            _svc = new SusModalService { OverlayHost = _host, Router = _router };
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.CloseAll();
            if (_go != null) Object.DestroyImmediate(_go);
            _svc = null;
            _router = null;
        }

        [UnityTest]
        public IEnumerator Entered_AutoFocusesFirstFocusable()
        {
            yield return null;

            var screen = new FocusScreen();
            _screenHost.Add(screen);
            yield return null;

            screen.Entered();
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreSame(screen.FirstBtn, screen.focusController?.focusedElement as VisualElement);
        }

        [UnityTest]
        public IEnumerator Entered_AutoFocusFalse_DoesNotFocus()
        {
            yield return null;

            var screen = new FocusScreen { AutoFocus = false };
            _screenHost.Add(screen);
            yield return null;

            screen.Entered();
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreNotSame(screen.FirstBtn, screen.focusController?.focusedElement as VisualElement);
        }

        [UnityTest]
        public IEnumerator ShowModal_BlocksBackground_FocusesModal_CloseRestores()
        {
            yield return null;

            var screen = new FocusScreen();
            _screenHost.Add(screen);
            yield return null;

            screen.Entered();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreSame(screen.FirstBtn, screen.focusController?.focusedElement as VisualElement);

            // Move focus to second so restore path is exercised
            screen.SecondBtn.Focus();
            yield return null;
            Assert.AreSame(screen.SecondBtn, screen.focusController?.focusedElement as VisualElement);

            var modal = (FocusModal)_svc.Show(typeof(FocusModal));
            Assert.IsNotNull(modal);

            Assert.IsFalse(screen.FirstBtn.focusable);
            Assert.IsFalse(screen.SecondBtn.focusable);
            Assert.AreEqual(PickingMode.Ignore, screen.FirstBtn.pickingMode);

            for (int i = 0; i < 8; i++)
            {
                if (ReferenceEquals(modal.OkBtn, modal.focusController?.focusedElement))
                    break;
                yield return null;
            }

            Assert.AreSame(modal.OkBtn, modal.focusController?.focusedElement as VisualElement,
                "focus should move into the modal");

            _svc.Close();
            yield return null;

            Assert.AreEqual(0, _svc.Count);
            Assert.IsTrue(screen.FirstBtn.focusable);
            Assert.IsTrue(screen.SecondBtn.focusable);
            Assert.AreEqual(PickingMode.Position, screen.SecondBtn.pickingMode);
            Assert.AreSame(screen.SecondBtn, screen.focusController?.focusedElement as VisualElement,
                "prior focus restored after Close");
        }

        [UnityTest]
        public IEnumerator Left_ThenEntered_RestoresSavedFocus_WhenPossible()
        {
            yield return null;

            var screen = new FocusScreen();
            _screenHost.Add(screen);
            yield return null;

            screen.Entered();
            for (int i = 0; i < 5; i++) yield return null;

            screen.SecondBtn.Focus();
            yield return null;
            screen.Left();

            // Simulate KeepAlive re-enter
            screen.Entered();
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreSame(screen.SecondBtn, screen.focusController?.focusedElement as VisualElement,
                "D4: restore saved focus on re-enter");
        }
    }
}

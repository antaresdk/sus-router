using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// M3 / T-011 — SusModalService + SusRouterModal playmode tests.
    /// Covers: Show/Close stack, CloseAll, lifecycle, BeforeDismiss guard,
    /// MaxModalDepth, scrim dismiss, OverlayHost cleanup, props injection.
    /// </summary>
    public class SusModalServiceTests
    {
        private GameObject _go;
        private UIDocument _doc;
        private OverlayHost _host;
        private SusRouter _router;
        private SusModalService _svc;

        private class InfoModal : SusRouterModal
        {
            public bool ShownCalled;
            public bool DismissedCalled;
            public int BeforeDismissCalls;
            public bool AllowDismiss = true;

            protected override void Build()
            {
                Add(new Label { name = "info-modal-label", text = "Info" });
            }

            protected override void Shown()
            {
                ShownCalled = true;
            }

            public override bool BeforeDismiss()
            {
                BeforeDismissCalls++;
                return AllowDismiss;
            }

            public override void Dismissed()
            {
                DismissedCalled = true;
            }
        }

        private class ConfirmModal : SusRouterModal
        {
            protected override void Build()
            {
                Add(new Label { name = "confirm-modal-label", text = "Confirm" });
            }
        }

        private class StackModal : SusRouterModal
        {
            protected override void Build()
            {
                Add(new Label { name = "stack-modal-label", text = "Stack" });
            }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestModalUI", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();

            var settings = SusTestPanelFactory.Create("SusTestPanelSettings_Modal");
            _doc.panelSettings = settings;

            _host = new OverlayHost();
            _doc.rootVisualElement.Add(_host);

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
        public IEnumerator Show_AddsModalToOverlay_AndIncrementsCount()
        {
            yield return null;

            var baseline = _host.Count;
            var modal = _svc.Show(typeof(InfoModal));

            Assert.IsNotNull(modal);
            Assert.IsInstanceOf<InfoModal>(modal);
            Assert.AreEqual(1, _svc.Count);
            Assert.AreEqual(1, _svc.CountProp.Value);
            Assert.AreEqual(baseline + 1, _host.Count);
            Assert.IsNotNull(_host.Q("modal-wrapper"));
            Assert.IsNotNull(_host.Q("info-modal-label"));
        }

        [UnityTest]
        public IEnumerator Show_InjectsRouterPropsAndService()
        {
            yield return null;

            var props = new Dictionary<string, object> { ["title"] = "Hello" };
            var modal = (InfoModal)_svc.Show(typeof(InfoModal), props);

            Assert.AreSame(_router, modal.Router);
            Assert.AreEqual("Hello", modal.Props["title"]);
            Assert.AreEqual(1, _svc.Count);
        }

        [UnityTest]
        public IEnumerator Shown_FiresAfterSchedule()
        {
            yield return null;

            var modal = (InfoModal)_svc.Show(typeof(InfoModal));
            Assert.IsFalse(modal.ShownCalled, "Shown is deferred to next panel update");

            for (int i = 0; i < 10; i++)
            {
                if (modal.ShownCalled) break;
                yield return null;
            }

            Assert.IsTrue(modal.ShownCalled, "Shown should fire after schedule");
        }

        [UnityTest]
        public IEnumerator Stack_ShowClose_LIFO()
        {
            yield return null;

            _svc.Show(typeof(InfoModal));
            _svc.Show(typeof(ConfirmModal));
            _svc.Show(typeof(StackModal));
            Assert.AreEqual(3, _svc.Count);
            Assert.AreEqual(3, _host.Count);

            _svc.Close();
            Assert.AreEqual(2, _svc.Count);
            Assert.IsNull(_host.Q("stack-modal-label"));
            Assert.IsNotNull(_host.Q("confirm-modal-label"));

            _svc.Close();
            Assert.AreEqual(1, _svc.Count);
            Assert.IsNotNull(_host.Q("info-modal-label"));

            _svc.Close();
            Assert.AreEqual(0, _svc.Count);
            Assert.AreEqual(0, _host.Count);
        }

        [UnityTest]
        public IEnumerator CloseAll_ClearsStackAndOverlay()
        {
            yield return null;

            _svc.Show(typeof(InfoModal));
            _svc.Show(typeof(ConfirmModal));
            _svc.Show(typeof(StackModal));
            Assert.AreEqual(3, _svc.Count);

            _svc.CloseAll();
            Assert.AreEqual(0, _svc.Count);
            Assert.AreEqual(0, _svc.CountProp.Value);
            Assert.AreEqual(0, _host.Count);
        }

        [UnityTest]
        public IEnumerator Close_CallsBeforeDismissAndDismissed()
        {
            yield return null;

            var modal = (InfoModal)_svc.Show(typeof(InfoModal));
            for (int i = 0; i < 5; i++) yield return null;

            _svc.Close();
            yield return null;

            Assert.AreEqual(1, modal.BeforeDismissCalls);
            Assert.IsTrue(modal.DismissedCalled);
            Assert.AreEqual(0, _svc.Count);
        }

        [UnityTest]
        public IEnumerator BeforeDismiss_False_KeepsModalOpen()
        {
            yield return null;

            var modal = (InfoModal)_svc.Show(typeof(InfoModal));
            modal.AllowDismiss = false;

            _svc.Close();
            yield return null;

            Assert.AreEqual(1, modal.BeforeDismissCalls);
            Assert.IsFalse(modal.DismissedCalled);
            Assert.AreEqual(1, _svc.Count);
            Assert.AreEqual(1, _host.Count);
            Assert.IsNotNull(_host.Q("info-modal-label"));
            // TearDown → CloseAll(force) must not hang (regression for CloseAll loop)
        }

        [UnityTest]
        public IEnumerator CloseAll_ForceCloses_EvenWhenBeforeDismissFalse()
        {
            yield return null;

            var modal = (InfoModal)_svc.Show(typeof(InfoModal));
            modal.AllowDismiss = false;

            _svc.CloseAll();
            yield return null;

            Assert.IsTrue(modal.DismissedCalled, "CloseAll forces dismiss");
            Assert.AreEqual(0, _svc.Count);
            Assert.AreEqual(0, _host.Count);
        }

        [UnityTest]
        public IEnumerator ScrimClick_ClosesTopModal()
        {
            yield return null;

            var modal = (InfoModal)_svc.Show(typeof(InfoModal));
            for (int i = 0; i < 5; i++) yield return null;

            var scrim = _host.Q("modal-scrim");
            Assert.IsNotNull(scrim);

            using (var evt = ClickEvent.GetPooled())
            {
                evt.target = scrim;
                scrim.SendEvent(evt);
            }

            yield return null;

            Assert.IsTrue(modal.DismissedCalled);
            Assert.AreEqual(0, _svc.Count);
            Assert.AreEqual(0, _host.Count);
        }

        [UnityTest]
        public IEnumerator MaxModalDepth_RejectsExtraShow()
        {
            yield return null;

            _svc.MaxModalDepth = 2;
            Assert.IsNotNull(_svc.Show(typeof(InfoModal)));
            Assert.IsNotNull(_svc.Show(typeof(ConfirmModal)));
            Assert.AreEqual(2, _svc.Count);

            LogAssert.Expect(LogType.Warning, new Regex("MaxModalDepth"));
            var rejected = _svc.Show(typeof(StackModal));
            Assert.IsNull(rejected);
            Assert.AreEqual(2, _svc.Count);
        }

        [UnityTest]
        public IEnumerator Show_WrongType_Throws()
        {
            yield return null;

            Assert.Throws<System.ArgumentException>(() =>
                _svc.Show(typeof(Label)));
        }

        [UnityTest]
        public IEnumerator Show_NullOverlayHost_ReturnsNull()
        {
            yield return null;

            var orphan = new SusModalService { Router = _router, OverlayHost = null };
            LogAssert.Expect(LogType.Error, new Regex("OverlayHost is null"));
            Assert.IsNull(orphan.Show(typeof(InfoModal)));
        }

        [UnityTest]
        public IEnumerator Router_Init_WiresModalService()
        {
            yield return null;

            var router = new SusRouter();
            router.Init(_host);
            Assert.IsNotNull(router.ModalService);
            Assert.AreSame(_host, router.ModalService.OverlayHost);

            router.ModalService.Show(typeof(InfoModal));
            Assert.AreEqual(1, router.ModalService.Count);

            router.CloseModal();
            Assert.AreEqual(0, router.ModalService.Count);
        }

        [UnityTest]
        public IEnumerator AfterClose_OverlayHostCount_ReturnsToBaseline()
        {
            yield return null;

            var baseline = _host.Count;
            _svc.Show(typeof(InfoModal));
            _svc.Show(typeof(ConfirmModal));
            Assert.AreEqual(baseline + 2, _host.Count);

            _svc.CloseAll();
            Assert.AreEqual(baseline, _host.Count);
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// T3 — Transition service playmode tests.
    /// Covers: FadeOut/FadeIn, SlideOut/SlideIn, CrossFade, Cancel, re-entrancy.
    /// </summary>
    public class SusTransitionServiceTests
    {
        private GameObject _go;
        private UIDocument _doc;
        private OverlayHost _host;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestUI", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();

            // Programmatic PanelSettings — required for schedule.Execute to work
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "SusTestPanelSettings_Transition";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            _doc.panelSettings = settings;

            _host = new OverlayHost();
            _doc.rootVisualElement.Add(_host);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [UnityTest]
        public IEnumerator FadeOut_AddsCurtainToOverlay()
        {
            var svc = new SusTransitionService { OverlayHost = _host };
            bool complete = false;

            svc.FadeOut(0.01f, () => complete = true);

            for (int i = 0; i < 20; i++) yield return null;
            Assert.IsTrue(complete, "FadeOut should complete");
            Assert.IsTrue(svc.IsTransitioning, "Curtain should still be present");
        }

        [UnityTest]
        public IEnumerator FadeOut_FadeIn_RemovesCurtain()
        {
            var svc = new SusTransitionService { OverlayHost = _host };

            svc.FadeOut(0.01f);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(1, _host.Count, "Curtain should be in overlay");

            svc.FadeIn(0.01f);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(0, _host.Count, "Curtain should be removed after FadeIn");
            Assert.IsFalse(svc.IsTransitioning);
        }

        [UnityTest]
        public IEnumerator Cancel_RemovesCurtainImmediately()
        {
            var svc = new SusTransitionService { OverlayHost = _host };

            svc.FadeOut(0.5f);
            yield return null;

            svc.Cancel();
            yield return null;
            Assert.AreEqual(0, _host.Count, "Cancel should remove curtain");
            Assert.IsFalse(svc.IsTransitioning);
        }

        [UnityTest]
        public IEnumerator DoubleFadeOut_ReusesCurtain()
        {
            var svc = new SusTransitionService { OverlayHost = _host };

            svc.FadeOut(0.01f);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(1, _host.Count);

            svc.FadeOut(0.01f); // second FadeOut cancels first, creates new
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(1, _host.Count, "Should still be only one curtain");
        }

        [UnityTest]
        public IEnumerator CrossFade_CompletesAndRemoves()
        {
            var svc = new SusTransitionService { OverlayHost = _host };

            svc.CrossFade(0.02f);
            for (int i = 0; i < 60; i++) yield return null;

            Assert.AreEqual(0, _host.Count, "CrossFade should remove curtain on complete");
            Assert.IsFalse(svc.IsTransitioning);
        }
    }
}

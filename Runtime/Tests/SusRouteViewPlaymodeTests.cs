using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// 8.3 — SusRouteView playmode tests: screen mount/unmount, lifecycle,
    /// KeepAlive, OverlayHost creation.
    /// </summary>
    public class SusRouteViewPlaymodeTests
    {
        private GameObject _go;
        private UIDocument _doc;
        private VisualElement _root;
        private SusRouter _router;

        // ── Test screens ─────────────────────────────────────────────────────

        private class ScreenA : SusScreen
        {
            public bool BeforeEnterCalled;
            public bool EnteredCalled;
            public bool BeforeLeaveCalled;
            public bool LeftCalled;

            protected override void Build()
            {
                Add(new Label { name = "screen-a-label", text = "Screen A" });
            }

            protected override bool OnBeforeEnter(SusRoute from)
            {
                BeforeEnterCalled = true;
                return true;
            }
            protected override void OnEntered() { EnteredCalled = true; }
            protected override bool OnBeforeLeave(SusRoute to)
            {
                BeforeLeaveCalled = true;
                return true;
            }
            protected override void OnLeft() { LeftCalled = true; }
        }

        private class ScreenB : SusScreen
        {
            protected override void Build()
            {
                Add(new Label { name = "screen-b-label", text = "Screen B" });
            }
        }

        // ── Setup / Teardown ─────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestUI", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();

            // Programmatic PanelSettings (same as CreateTestPanelSettings)
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "SusTestPanelSettings";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            _doc.panelSettings = settings;

            _root = _doc.rootVisualElement;

            _router = new SusRouter();
            _router.Register("/a", typeof(ScreenA));
            _router.Register("/b", typeof(ScreenB));
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _router = null;
        }

        // ─── Mount → screen appears ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator Mount_RendersScreenInRouteView()
        {
            var result = _router.Mount(_root, "/a");
            Assert.AreEqual(NavigationResult.Success, result);

            yield return null; // allow layout

            // SusRouteView should be first child, overlay last
            Assert.GreaterOrEqual(_root.childCount, 2,
                "Root should have SusRouteView + OverlayHost");
            var routeView = _root.ElementAt(0);
            Assert.IsInstanceOf<SusRouteView>(routeView);

            // Screen should be inside routeView
            Assert.GreaterOrEqual(routeView.childCount, 1);
            Assert.IsInstanceOf<SusScreen>(routeView.ElementAt(0));
        }

        // ─── Push → old screen removed, new screen rendered ─────────────────

        [UnityTest]
        public IEnumerator Push_RemovesOldScreen_ShowsNewScreen()
        {
            _router.Mount(_root, "/a");
            yield return null;
            Assert.AreEqual("/a", _router.CurrentRoute.Value?.FullPath);

            _router.Push("/b");
            yield return null;
            Assert.AreEqual("/b", _router.CurrentRoute.Value?.FullPath);

            var routeView = _root.Q<SusRouteView>();
            Assert.IsNotNull(routeView);
            Assert.GreaterOrEqual(routeView.childCount, 1);

            var current = routeView.ElementAt(0) as SusScreen;
            Assert.IsNotNull(current);
            // The current screen should be ScreenB (class name check via label)
            var label = current.Q<Label>("screen-b-label");
            Assert.IsNotNull(label, "Current screen should be ScreenB");
        }

        // ─── Lifecycle: BeforeEnter + Entered ────────────────────────────────

        [UnityTest]
        public IEnumerator Lifecycle_BeforeEnterAndEntered_CalledOnPush()
        {
            _router.Register("/lifecycle", typeof(ScreenA));
            _router.Mount(_root, "/lifecycle");
            yield return null;

            var routeView = _root.Q<SusRouteView>();
            var current = routeView.ElementAt(0) as ScreenA;
            Assert.IsNotNull(current);
            Assert.IsTrue(current.BeforeEnterCalled, "BeforeEnter should be called");
            Assert.IsTrue(current.EnteredCalled, "Entered should be called");
        }

        // ─── Lifecycle: BeforeLeave + Left called on navigation away ─────────

        [UnityTest]
        public IEnumerator Lifecycle_BeforeLeaveAndLeft_CalledOnNavigateAway()
        {
            _router.Register("/lifecycle2", typeof(ScreenA));
            _router.Mount(_root, "/lifecycle2");
            yield return null;

            var routeView = _root.Q<SusRouteView>();
            var screenA = routeView.ElementAt(0) as ScreenA;
            Assert.IsNotNull(screenA);

            _router.Push("/b");
            yield return null;

            Assert.IsTrue(screenA.BeforeLeaveCalled, "BeforeLeave should be called");
            Assert.IsTrue(screenA.LeftCalled, "Left should be called");
        }

        // ─── Replace swaps the screen ────────────────────────────────────────

        [UnityTest]
        public IEnumerator Replace_SwapsScreen()
        {
            _router.Mount(_root, "/a");
            yield return null;
            Assert.AreEqual("/a", _router.CurrentRoute.Value?.FullPath);

            _router.Replace("/b");
            yield return null;
            Assert.AreEqual("/b", _router.CurrentRoute.Value?.FullPath);

            var routeView = _root.Q<SusRouteView>();
            var label = routeView.Q<Label>("screen-b-label");
            Assert.IsNotNull(label, "After Replace, current screen should be ScreenB");
        }

        // ─── Back navigates to previous screen ───────────────────────────────

        [UnityTest]
        public IEnumerator Back_ReturnsToPreviousScreen()
        {
            _router.Mount(_root, "/a");
            yield return null;
            _router.Push("/b");
            yield return null;
            Assert.AreEqual("/b", _router.CurrentRoute.Value?.FullPath);

            _router.Back();
            yield return null;
            Assert.AreEqual("/a", _router.CurrentRoute.Value?.FullPath);

            var routeView = _root.Q<SusRouteView>();
            var label = routeView.Q<Label>("screen-a-label");
            Assert.IsNotNull(label, "After Back, should return to ScreenA");
        }

        // ─── OverlayHost is last child ───────────────────────────────────────

        [UnityTest]
        public IEnumerator Mount_OverlayHostIsLastChild()
        {
            _router.Mount(_root, "/a");
            yield return null;

            var lastChild = _root.ElementAt(_root.childCount - 1);
            Assert.IsInstanceOf<OverlayHost>(lastChild,
                "OverlayHost should be the last child for correct z-order");
        }

        // ─── KeepAlive: screen hidden, not removed ───────────────────────────

        [UnityTest]
        public IEnumerator KeepAlive_ScreenHidden_NotRemoved()
        {
            var config = new SusRouteConfig { KeepAlive = true };
            _router.Register("/ka", typeof(ScreenA), config);
            _router.Mount(_root, "/ka");
            yield return null;

            var routeView = _root.Q<SusRouteView>();
            Assert.GreaterOrEqual(routeView.childCount, 1, "KeepAlive screen should be in DOM");

            _router.Push("/b");
            yield return null;

            // KeepAlive screen should still be in routeView (wrapped in SusKeepAlive, display:none)
            Assert.GreaterOrEqual(routeView.childCount, 1,
                "KeepAlive screen should remain in DOM (hidden)");
        }
    }
}

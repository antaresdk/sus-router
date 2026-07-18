using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// K1 — KeepAlive playmode tests.
    /// Covers: screen wrap/hide/show, cache reuse, LRU eviction, teardown on evict.
    /// </summary>
    public class SusRouterKeepAliveTests
    {
        private GameObject _go;
        private UIDocument _doc;
        private SusRouter _router;
        private SusRouteView _view;
        private SusRouteRecord _recordA;
        private SusRouteRecord _recordB;
        private SusRouteRecord _recordC;

        private class StubScreen : SusScreen
        {
            public static int InstanceCount;
            public readonly int Id;
            public bool EnteredCalled;
            public bool LeftCalled;
            public bool UpdateCalled;
            public readonly VisualElement Marker;

            public StubScreen() { Id = ++InstanceCount; }

            protected override void Build()
            {
                var label = new Label { text = $"Stub {Id}" };
                label.name = $"Stub_{Id}";
                Add(label);
            }

            protected override bool OnBeforeEnter(SusRoute from)
            {
                return true;
            }

            protected override void OnEntered()
            {
                EnteredCalled = true;
            }

            protected override void OnLeft()
            {
                LeftCalled = true;
                base.OnLeft();
            }

            protected override bool OnBeforeRouteUpdate(SusRoute to)
            {
                UpdateCalled = true;
                return true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            StubScreen.InstanceCount = 0;

            _go = new GameObject("TestKeepAlive", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();

            _router = new SusRouter();
            _view = new SusRouteView { Router = _router };
            _router.SetRouteView(_view);

            _doc.rootVisualElement.Add(_view);

            _recordA = _router.Register("/pageA", typeof(StubScreen),
                new SusRouteConfig { KeepAlive = true });
            _recordB = _router.Register("/pageB", typeof(StubScreen),
                new SusRouteConfig { KeepAlive = true });
            _recordC = _router.Register("/pageC", typeof(StubScreen),
                new SusRouteConfig { KeepAlive = true });
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // ════════════════════════════════════════════════════════════════
        //  Cache: show/hide via display, screen reuse
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator Push_KeepAliveScreen_IsInDOM()
        {
            _router.Push("/pageA");
            yield return null;

            Assert.Greater(_doc.rootVisualElement.childCount, 0,
                "SusRouteView should have children after Push");
            Assert.Greater(StubScreen.InstanceCount, 0,
                "Screen instance should be created");
        }

        [UnityTest]
        public IEnumerator NavigateAway_KeepAliveScreen_StaysInDOM()
        {
            _router.Push("/pageA");
            yield return null;

            int childCountBefore = _doc.rootVisualElement.childCount;

            _router.Push("/pageB");
            yield return null;

            // Screen A should still be in DOM (wrapped in SusKeepAlive, hidden)
            Assert.GreaterOrEqual(_doc.rootVisualElement.childCount, childCountBefore,
                "KeepAlive screen should remain in DOM after navigation");
        }

        [UnityTest]
        public IEnumerator NavigateBack_KeepAliveScreen_IsReused_NotRecreated()
        {
            _router.Push("/pageA");
            yield return null;
            int countAfterA = StubScreen.InstanceCount;
            var screenA = _router.History[0].Screen as StubScreen;
            int screenAId = screenA.Id;

            _router.Push("/pageB");
            yield return null;

            _router.Back();
            yield return null;

            var screenAgain = _router.CurrentRoute.Value.Screen as StubScreen;
            int screenAgainId = screenAgain.Id;

            Assert.AreEqual(screenAId, screenAgainId,
                "Same screen instance should be reused when navigating back");
            Assert.IsTrue(screenAgain.EnteredCalled,
                "Reused screen should receive Entered()");
        }

        [UnityTest]
        public IEnumerator KeepAliveScreen_IsHiddenNotRemoved_OnNavigationAway()
        {
            _router.Push("/pageA");
            yield return null;

            var screenA = _router.CurrentRoute.Value.Screen as StubScreen;

            _router.Push("/pageB");
            yield return null;

            // Both A and B should exist in the view hierarchy
            Assert.IsFalse(screenA.LeftCalled,
                "Left() should NOT be called on KeepAlive screen");
        }

        // ════════════════════════════════════════════════════════════════
        //  LRU eviction
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator LRU_Eviction_AtCapacity_RemovesOldestScreen()
        {
            _view.MaxKeepAlive = 2;

            _router.Push("/pageA");
            yield return null;
            var screenA = _router.History[0].Screen as StubScreen;

            _router.Push("/pageB");
            yield return null;

            _router.Push("/pageC");
            yield return null;

            // Cache now: [pageA, pageB] — at capacity (2)
            Assert.IsTrue(_view.TryGetKeepAliveScreen("/pageA", out _),
                "pageA should be in KeepAlive cache before eviction");
            Assert.IsTrue(_view.TryGetKeepAliveScreen("/pageB", out _),
                "pageB should be in KeepAlive cache before eviction");

            // Push a 4th page — should evict the oldest (pageA)
            _router.Register("/pageD", typeof(StubScreen),
                new SusRouteConfig { KeepAlive = true });
            _router.Push("/pageD");
            yield return null;

            // pageA should be evicted — Left() called
            Assert.IsTrue(screenA.LeftCalled,
                "Oldest screen should receive Left() on LRU eviction");
            Assert.IsFalse(_view.TryGetKeepAliveScreen("/pageA", out _),
                "pageA should be removed from KeepAlive cache after eviction");
        }

        [UnityTest]
        public IEnumerator LRU_AccessOrder_UpdatesOnRevisit()
        {
            _view.MaxKeepAlive = 2;

            _router.Push("/pageA");
            yield return null;
            var screenA = _router.History[0].Screen as StubScreen;

            _router.Push("/pageB");
            yield return null;

            // Revisit A — should move A to most-recent in LRU
            _router.Back();
            yield return null;

            // Now history after Back: [pageA] with pageA screen reused
            // Push pageC (new) — pageB should be cached
            _router.Push("/pageC");
            yield return null;

            // Push pageD to trigger eviction — pageB (now oldest in cache) should go
            _router.Register("/pageD", typeof(StubScreen),
                new SusRouteConfig { KeepAlive = true });
            _router.Push("/pageD");
            yield return null;

            // A should survive because it was revisited (LRU bump)
            Assert.IsFalse(screenA.LeftCalled,
                "A should survive after being revisited (LRU)");
        }

        // ════════════════════════════════════════════════════════════════
        //  TryGetKeepAliveScreen direct API
        // ════════════════════════════════════════════════════════════════

        [UnityTest]
        public IEnumerator TryGetKeepAliveScreen_ReturnsCachedScreen()
        {
            _router.Push("/pageA");
            yield return null;

            _router.Push("/pageB");
            yield return null;

            bool found = _view.TryGetKeepAliveScreen("/pageA", out var cachedScreen);

            Assert.IsTrue(found, "Path /pageA should be in KeepAlive cache");
            Assert.IsNotNull(cachedScreen);
        }

        [UnityTest]
        public IEnumerator TryGetKeepAliveScreen_UnknownPath_ReturnsFalse()
        {
            yield return null;
            bool found = _view.TryGetKeepAliveScreen("/nonexistent", out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        // ════════════════════════════════════════════════════════════════
        //  Non-KeepAlive: screen is removed from DOM
        // ════════════════════════════════════════════════════════════════

        private class StubScreenNoKeep : SusScreen
        {
            public bool LeftCalled;
            protected override void Build() { Add(new Label("no-keep")); }
            protected override void OnLeft()
            {
                LeftCalled = true;
                base.OnLeft();
            }
        }

        [UnityTest]
        public IEnumerator NonKeepAlive_Screen_ReceivesLeft_OnNavigationAway()
        {
            var router = new SusRouter();
            var view = new SusRouteView { Router = router };
            router.SetRouteView(view);
            _doc.rootVisualElement.Clear();
            _doc.rootVisualElement.Add(view);

            router.Register("/normal", typeof(StubScreenNoKeep),
                new SusRouteConfig { KeepAlive = false });
            router.Register("/other", typeof(StubScreenNoKeep),
                new SusRouteConfig { KeepAlive = false });

            router.Push("/normal");
            yield return null;
            var screen = router.CurrentRoute.Value.Screen as StubScreenNoKeep;

            router.Push("/other");
            yield return null;

            Assert.IsTrue(screen.LeftCalled,
                "Non-KeepAlive screen should receive Left() on navigation away");
        }
    }
}

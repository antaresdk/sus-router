using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// T-011 — SusRouteLink playmode tests.
    /// Covers: Bind, Push/Replace navigation, active CSS classes (prefix + exact).
    /// </summary>
    public class SusRouteLinkTests
    {
        private GameObject _go;
        private UIDocument _doc;
        private VisualElement _root;
        private SusRouter _router;

        private class HomeScreen : SusScreen
        {
            protected override void Build()
            {
                Add(new Label { name = "home-label", text = "Home" });
            }
        }

        private class AboutScreen : SusScreen
        {
            protected override void Build()
            {
                Add(new Label { name = "about-label", text = "About" });
            }
        }

        private class NestedScreen : SusScreen
        {
            protected override void Build()
            {
                Add(new Label { name = "nested-label", text = "Nested" });
            }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestRouteLinkUI", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();

            var settings = SusTestPanelFactory.Create("SusTestPanelSettings_RouteLink");
            _doc.panelSettings = settings;

            _root = _doc.rootVisualElement;
            _router = new SusRouter();
            _router.Register("/home", typeof(HomeScreen));
            _router.Register("/about", typeof(AboutScreen));
            _router.Register("/home/settings", typeof(NestedScreen));
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _router = null;
        }

        [UnityTest]
        public IEnumerator Click_PushMode_NavigatesToTarget()
        {
            _router.Mount(_root, "/home");
            yield return null;

            var link = new SusRouteLink { To = "/about", Mode = LinkMode.Push };
            link.Bind(_router);
            _root.Add(link);
            yield return null;

            using (var evt = ClickEvent.GetPooled())
            {
                evt.target = link;
                link.SendEvent(evt);
            }
            yield return null;

            Assert.AreEqual("/about", _router.CurrentRoute.Value?.FullPath);
            Assert.GreaterOrEqual(_router.History.Count, 2,
                "Push should grow history");
        }

        [UnityTest]
        public IEnumerator Click_ReplaceMode_ReplacesCurrent()
        {
            _router.Mount(_root, "/home");
            yield return null;
            var historyBefore = _router.History.Count;

            var link = new SusRouteLink { To = "/about", Mode = LinkMode.Replace };
            link.Bind(_router);
            _root.Add(link);
            yield return null;

            using (var evt = ClickEvent.GetPooled())
            {
                evt.target = link;
                link.SendEvent(evt);
            }
            yield return null;

            Assert.AreEqual("/about", _router.CurrentRoute.Value?.FullPath);
            Assert.AreEqual(historyBefore, _router.History.Count,
                "Replace should not grow history");
        }

        [UnityTest]
        public IEnumerator Click_WithoutBind_DoesNothing()
        {
            _router.Mount(_root, "/home");
            yield return null;

            var link = new SusRouteLink { To = "/about" };
            // no Bind
            _root.Add(link);
            yield return null;

            using (var evt = ClickEvent.GetPooled())
            {
                evt.target = link;
                link.SendEvent(evt);
            }
            yield return null;

            Assert.AreEqual("/home", _router.CurrentRoute.Value?.FullPath);
        }

        [UnityTest]
        public IEnumerator ActiveClass_PrefixMatch_WhenNotExact()
        {
            _router.Mount(_root, "/home");
            yield return null;

            var link = new SusRouteLink { To = "/home", Exact = false };
            link.Bind(_router);
            link.RefreshActiveClass();

            Assert.IsTrue(link.ClassListContains("router-link-active"));
            Assert.IsFalse(link.ClassListContains("router-link-exact-active"));

            _router.Push("/home/settings");
            yield return null;
            link.RefreshActiveClass();

            Assert.IsTrue(link.ClassListContains("router-link-active"),
                "Prefix /home should stay active on /home/settings");
        }

        [UnityTest]
        public IEnumerator ActiveClass_ExactMatch_OnlyOnExactPath()
        {
            _router.Mount(_root, "/home");
            yield return null;

            var link = new SusRouteLink { To = "/home", Exact = true };
            link.Bind(_router);
            link.RefreshActiveClass();

            Assert.IsTrue(link.ClassListContains("router-link-exact-active"));
            Assert.IsFalse(link.ClassListContains("router-link-active"));

            _router.Push("/home/settings");
            yield return null;
            link.RefreshActiveClass();

            Assert.IsFalse(link.ClassListContains("router-link-exact-active"),
                "Exact link must deactivate on nested path");
        }

        [UnityTest]
        public IEnumerator Bind_SubscribesAndRefreshesOnRouteChange()
        {
            _router.Mount(_root, "/home");
            yield return null;

            var link = new SusRouteLink { To = "/about", Exact = true };
            link.Bind(_router);
            link.RefreshActiveClass();
            Assert.IsFalse(link.ClassListContains("router-link-exact-active"));

            _router.Push("/about");
            yield return null;
            // Changed handler should have refreshed
            Assert.IsTrue(link.ClassListContains("router-link-exact-active"));
        }

        [UnityTest]
        public IEnumerator Constructor_AddsBaseUssClass()
        {
            yield return null;
            var link = new SusRouteLink();
            Assert.IsTrue(link.ClassListContains("sus-route-link"));
        }
    }
}

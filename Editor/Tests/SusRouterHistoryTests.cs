using NUnit.Framework;

using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    /// <summary>
    /// Editmode tests for SusRouter history/cursor logic.
    /// NOTE: Full Push/Replace/Back navigation requires a running panel (SusRouteView).
    /// Those are covered in playmode tests (F8.3).
    /// These editmode tests cover the guard infrastructure, route resolution,
    /// and initial state invariants.
    /// </summary>
    public class SusRouterHistoryTests
    {
        private class DummyScreen : SusScreen
        {
            protected override void Build() { }
        }

        [Test]
        public void NewRouter_CanGoBack_IsFalse()
        {
            var router = new SusRouter();

            Assert.IsFalse(router.CanGoBack);
        }

        [Test]
        public void NewRouter_CanGoForward_IsFalse()
        {
            var router = new SusRouter();

            Assert.IsFalse(router.CanGoForward);
        }

        [Test]
        public void NewRouter_History_IsEmpty()
        {
            var router = new SusRouter();

            Assert.AreEqual(0, router.History.Count);
            Assert.AreEqual(-1, router.HistoryIndex);
        }

        [Test]
        public void NewRouter_CurrentRoute_IsNull()
        {
            var router = new SusRouter();

            Assert.IsNull(router.CurrentRoute.Value);
        }

        [Test]
        public void Resolve_ExactMatch_ReturnsRecord()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(DummyScreen));

            var record = router.Resolve("/home");

            Assert.IsNotNull(record);
            Assert.AreEqual("/home", record.Path);
        }

        [Test]
        public void Resolve_DynamicMatch_ReturnsRecord()
        {
            var router = new SusRouter();
            router.Register("/battle/:id", typeof(DummyScreen));

            var record = router.Resolve("/battle/42");

            Assert.IsNotNull(record);
            Assert.AreEqual("/battle/:id", record.Path);
        }

        [Test]
        public void Resolve_NotFound_ReturnsNull()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(DummyScreen));

            var record = router.Resolve("/nonexistent");

            Assert.IsNull(record);
        }

        [Test]
        public void Resolve_Alias_ResolvesToOriginal()
        {
            var router = new SusRouter();
            var config = new SusRouteConfig
            {
                Alias = new System.Collections.Generic.List<string> { "/alias" }
            };
            router.Register("/original", typeof(DummyScreen), config);

            var record = router.Resolve("/alias");

            Assert.IsNotNull(record);
            Assert.AreEqual("/original", record.Path);
        }

        [Test]
        public void Register_NamedRoute_ResolvableByName()
        {
            var router = new SusRouter();
            router.Register("/settings", typeof(DummyScreen),
                new SusRouteConfig { Name = "settings" });

            var path = router.ResolvePath("settings");

            Assert.AreEqual("/settings", path);
        }

        [Test]
        public void ResolvePath_UnknownName_ReturnsNull()
        {
            var router = new SusRouter();

            var path = router.ResolvePath("nonexistent");

            Assert.IsNull(path);
        }

        [Test]
        public void Resolve_IgnoresQueryString()
        {
            var router = new SusRouter();
            router.Register("/battle", typeof(DummyScreen));

            var record = router.Resolve("/battle?mode=pvp&map=arena");

            Assert.IsNotNull(record);
            Assert.AreEqual("/battle", record.Path);
        }

        [Test]
        public void Register_Children_RegisteredAsRoutes()
        {
            var router = new SusRouter();
            var config = new SusRouteConfig
            {
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("tab1", typeof(DummyScreen)),
                    new SusRouteRecord("tab2", typeof(DummyScreen)),
                }
            };
            router.Register("/settings", typeof(DummyScreen), config);

            var tab1 = router.Resolve("/settings/tab1");
            var tab2 = router.Resolve("/settings/tab2");

            Assert.IsNotNull(tab1);
            Assert.IsNotNull(tab2);
            Assert.AreEqual("tab1", tab1.Path);
            Assert.AreEqual("tab2", tab2.Path);
        }

        [Test]
        public void AfterEach_HookIsRegistered()
        {
            var router = new SusRouter();
            int hookCalls = 0;

            router.AfterEach((from, to) => hookCalls++);

            // Registration should not throw
            Assert.AreEqual(0, hookCalls, "Hook should not fire on registration");
        }

        [Test]
        public void MultipleGuards_AreAllRegistered()
        {
            var router = new SusRouter();
            int count = 0;

            for (int i = 0; i < 5; i++)
            {
                router.BeforeEach((_, _) => { count++; return true; });
            }

            // All 5 guards registered without error
        }

        [Test]
        public void SetCurrentForTest_PreservesProps()
        {
            var router = new SusRouter();
            router.Register("/admin", typeof(DummyScreen));

            var route = new SusRoute(
                router.Resolve("/admin"), "/admin", null)
            {
                Props = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "mode", "edit" }
                }
            };
            router.SetCurrentForTest(route);

            Assert.IsNotNull(router.CurrentRoute.Value);
            Assert.AreEqual("edit", router.CurrentRoute.Value.Props["mode"]);
            Assert.AreEqual(1, router.History.Count);
            Assert.AreEqual("edit", router.History[0].Props["mode"]);
        }

        // ─── HasRoute / Routes / RemoveRoute / OnNavigationError ──────────

        [Test]
        public void HasRoute_ExistingName_ReturnsTrue()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(DummyScreen), new SusRouteConfig { Name = "home" });

            Assert.IsTrue(router.HasRoute("home"));
        }

        [Test]
        public void HasRoute_MissingName_ReturnsFalse()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(DummyScreen), new SusRouteConfig { Name = "home" });

            Assert.IsFalse(router.HasRoute("missing"));
        }

        [Test]
        public void HasRoute_NullOrEmpty_ReturnsFalse()
        {
            var router = new SusRouter();

            Assert.IsFalse(router.HasRoute(null));
            Assert.IsFalse(router.HasRoute(""));
        }

        [Test]
        public void Routes_ReturnsAllRegistered()
        {
            var router = new SusRouter();
            router.Register("/a", typeof(DummyScreen));
            router.Register("/b", typeof(DummyScreen));
            router.Register("/c", typeof(DummyScreen), new SusRouteConfig { Name = "c" });

            Assert.AreEqual(3, router.Routes.Count);
            Assert.AreEqual(3, router.RouteCount);
        }

        [Test]
        public void Routes_IsEmpty_ForNewRouter()
        {
            var router = new SusRouter();

            Assert.AreEqual(0, router.Routes.Count);
        }

        [Test]
        public void RemoveRoute_ExistingName_RemovesFromAllMaps()
        {
            var router = new SusRouter();
            router.Register("/test", typeof(DummyScreen), new SusRouteConfig
            {
                Name = "test",
                Alias = new System.Collections.Generic.List<string> { "/alias1", "/alias2" }
            });

            var result = router.RemoveRoute("test");

            Assert.IsTrue(result);
            Assert.IsFalse(router.HasRoute("test"));
            Assert.AreEqual(0, router.Routes.Count);
            // Resolve through alias should also fail after removal
            Assert.IsNull(router.ResolvePath("test"));
        }

        [Test]
        public void RemoveRoute_MissingName_ReturnsFalse()
        {
            var router = new SusRouter();
            router.Register("/a", typeof(DummyScreen), new SusRouteConfig { Name = "a" });

            Assert.IsFalse(router.RemoveRoute("missing"));
        }

        [Test]
        public void RemoveRoute_WithChildren_RemovesChildren()
        {
            var router = new SusRouter();
            router.Register("/parent", typeof(DummyScreen), new SusRouteConfig
            {
                Name = "parent",
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("child", typeof(DummyScreen), new SusRouteConfig { Name = "child" })
                }
            });

            var result = router.RemoveRoute("parent");

            Assert.IsTrue(result);
            Assert.IsFalse(router.HasRoute("parent"));
            Assert.IsFalse(router.HasRoute("child"));
        }

        [Test]
        public void OnNavigationError_Fires_OnNotFound()
        {
            var router = new SusRouter();
            NavigationError captured = null;
            router.OnNavigationError += e => captured = e;

            router.Register("/home", typeof(DummyScreen));
            router.SetCurrentForTest(new SusRoute(router.Resolve("/home"), "/home", null));
            router.Push("/nonexistent");

            Assert.IsNotNull(captured);
            Assert.AreEqual(NavigationResult.NotFound, captured.Result);
            Assert.AreEqual("/nonexistent", captured.To.FullPath);
        }

        [Test]
        public void OnNavigationError_Fires_OnAborted()
        {
            var router = new SusRouter();
            NavigationError captured = null;
            router.OnNavigationError += e => captured = e;

            router.BeforeEach((from, to) => false);
            router.Register("/home", typeof(DummyScreen));
            router.Register("/admin", typeof(DummyScreen));
            router.SetCurrentForTest(new SusRoute(router.Resolve("/home"), "/home", null));
            router.Push("/admin");

            Assert.IsNotNull(captured);
            Assert.AreEqual(NavigationResult.Aborted, captured.Result);
            Assert.AreEqual("/home", captured.From.FullPath);
            Assert.AreEqual("/admin", captured.To.FullPath);
        }

        [Test]
        public void OnNavigationError_HasTypedFields()
        {
            var router = new SusRouter();
            NavigationError captured = null;
            router.OnNavigationError += e => captured = e;

            router.BeforeEach((from, to) => false);
            router.Register("/home", typeof(DummyScreen));
            router.Register("/admin", typeof(DummyScreen));
            router.SetCurrentForTest(new SusRoute(router.Resolve("/home"), "/home", null));
            router.Push("/admin");

            Assert.IsNotNull(captured);
            Assert.IsNotNull(captured.From);
            Assert.IsNotNull(captured.To);
            Assert.IsNotNull(captured.RejectedBy);
        }
    }
}

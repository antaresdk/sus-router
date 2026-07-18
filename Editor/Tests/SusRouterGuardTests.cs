using NUnit.Framework;
using System.Collections.Generic;

using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    /// <summary>
    /// Tests for guard registration and re-entrancy protection in SusRouter.
    /// Full playmode guard execution (with SusRouteView + panel) is in playmode tests.
    /// </summary>
    public class SusRouterGuardTests
    {
        // Minimal screen type for route registration
        private class DummyScreen : SusScreen
        {
            protected override void Build() { }
        }

        [Test]
        public void BeforeEach_GuardIsAddedToList()
        {
            var router = new SusRouter();
            int guardCallCount = 0;

            router.BeforeEach((from, to) =>
            {
                guardCallCount++;
                return true;
            });

            // Register a route to verify guard infrastructure
            router.Register("/test", typeof(DummyScreen));

            Assert.AreEqual(1, router.RouteCount);
            // Guard registration should not throw
            Assert.DoesNotThrow(() => router.BeforeEach((_, _) => true));
        }

        [Test]
        public void BeforeEach_NullGuard_IsIgnored()
        {
            var router = new SusRouter();

            Assert.DoesNotThrow(() => router.BeforeEach(null));
            Assert.DoesNotThrow(() => router.BeforeResolve(null));
        }

        [Test]
        public void BeforeResolve_GuardIsAdded()
        {
            var router = new SusRouter();

            router.BeforeResolve((from, to) => true);

            Assert.DoesNotThrow(() =>
                router.Register("/home", typeof(DummyScreen)));
        }

        [Test]
        public void Register_MissingScreenType_ThrowsArgumentNull()
        {
            var router = new SusRouter();

            Assert.Throws<System.ArgumentNullException>(() =>
                router.Register("/path", null));
        }

        [Test]
        public void Register_NonSusScreenType_ThrowsArgumentException()
        {
            var router = new SusRouter();

            Assert.Throws<System.ArgumentException>(() =>
                router.Register("/path", typeof(object)));
        }

        [Test]
        public void Register_LazyFactory_AllowsNullScreenType()
        {
            var router = new SusRouter();

            var config = new SusRouteConfig
            {
                LazyFactory = () => new DummyScreen()
            };

            Assert.DoesNotThrow(() =>
                router.Register("/lazy", null, config));
        }

        [Test]
        public void Register_DuplicatePath_Overwrites()
        {
            var router = new SusRouter();
            var first = router.Register("/dup", typeof(DummyScreen));
            var second = router.Register("/dup", typeof(DummyScreen));

            Assert.AreEqual(1, router.RouteCount, "Duplicate path should overwrite, not add");
        }

        [Test]
        public void Register_DuplicateName_Throws()
        {
            var router = new SusRouter();
            router.Register("/a", typeof(DummyScreen), new SusRouteConfig { Name = "dup" });

            Assert.Throws<System.ArgumentException>(() =>
                router.Register("/b", typeof(DummyScreen), new SusRouteConfig { Name = "dup" }));
        }

        [Test]
        public void Push_NotFound_ReturnsNotFound()
        {
            var router = new SusRouter();

            var result = router.Push("/nonexistent");

            Assert.AreEqual(NavigationResult.NotFound, result);
        }

        [Test]
        public void Back_WhenHistoryEmpty_ReturnsCantGoBack()
        {
            var router = new SusRouter();

            var result = router.Back();

            Assert.AreEqual(NavigationResult.CantGoBack, result);
        }

        [Test]
        public void Forward_WhenHistoryEmpty_ReturnsCantGoForward()
        {
            var router = new SusRouter();

            var result = router.Forward();

            Assert.AreEqual(NavigationResult.CantGoForward, result);
        }

        // ─── Per-route BeforeEnter config ─────────────────────────────────

        [Test]
        public void BeforeEnter_PerRoute_Allows_WhenTrue()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(DummyScreen));
            router.Register("/admin", typeof(DummyScreen), new SusRouteConfig
            {
                BeforeEnter = (from, to) => true
            });
            router.SetCurrentForTest(new SusRoute(router.Resolve("/home"), "/home", null));

            var result = router.Push("/admin");

            Assert.AreEqual(NavigationResult.Success, result);
        }

        [Test]
        public void BeforeEnter_PerRoute_Aborts_WhenFalse()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(DummyScreen));
            router.Register("/admin", typeof(DummyScreen), new SusRouteConfig
            {
                BeforeEnter = (from, to) => false
            });
            router.SetCurrentForTest(new SusRoute(router.Resolve("/home"), "/home", null));

            var result = router.Push("/admin");

            Assert.AreEqual(NavigationResult.Aborted, result);
        }

        // ─── PropsFn ──────────────────────────────────────────────────────

        [Test]
        public void PropsFn_IsCalled_OnPush()
        {
            var router = new SusRouter();
            bool propsFnCalled = false;
            router.Register("/user/:id", typeof(DummyScreen), new SusRouteConfig
            {
                PropsFn = (route) =>
                {
                    propsFnCalled = true;
                    return new Dictionary<string, object> { { "userId", route.Params["id"] } };
                }
            });
            router.SetCurrentForTest(new SusRoute(null, "<none>", null));

            router.Push("/user/42");

            Assert.IsTrue(propsFnCalled);
            Assert.AreEqual("42", router.CurrentRoute.Value.Props["userId"]);
        }

        [Test]
        public void PropsFn_Merges_WithDefaultProps()
        {
            var router = new SusRouter();
            router.Register("/page", typeof(DummyScreen), new SusRouteConfig
            {
                DefaultProps = new Dictionary<string, object>
                {
                    { "base", "default" },
                    { "overridable", "dflt" }
                },
                PropsFn = (route) => new Dictionary<string, object>
                {
                    { "fromFn", "yes" },
                    { "overridable", "fromFn" }
                }
            });
            router.SetCurrentForTest(new SusRoute(null, "<none>", null));

            router.Push("/page", new Dictionary<string, object>
            {
                { "overridable", "explicit" }
            });

            Assert.AreEqual("default", router.CurrentRoute.Value.Props["base"]);
            Assert.AreEqual("yes", router.CurrentRoute.Value.Props["fromFn"]);
            Assert.AreEqual("explicit", router.CurrentRoute.Value.Props["overridable"]);
        }
    }
}

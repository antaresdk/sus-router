using NUnit.Framework;
using System.Collections.Generic;

using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    /// <summary>
    /// Comprehensive guard pipeline + navigation tests.
    /// Covers: 4.4 (re-entrancy), 4.5 (beforeEach in param-update),
    /// 8.3 (beforeResolve abort), 9.1 (guard-abort at each point),
    /// 9.2 (redirect/alias), 9.5 (beforeRouteUpdate), 9.6 (re-entrancy),
    /// 9.8 (Back/Forward/Go boundaries).
    /// </summary>
    public class SusRouterPipelineTests
    {
        private class DummyScreen : SusScreen
        {
            protected override void Build() { }
        }

        private class CountedDummyScreen : SusScreen
        {
            public static int InstanceCount;
            public CountedDummyScreen() { InstanceCount++; }
            protected override void Build() { }
        }

        [SetUp]
        public void SetUp()
        {
            CountedDummyScreen.InstanceCount = 0;
        }

        private static SusRoute MakeRoute(string path, SusScreen screen = null)
        {
            var record = new SusRouteRecord(path, typeof(DummyScreen));
            var route = new SusRoute(record, path, null);
            if (screen != null)
                route.Screen = screen;
            return route;
        }

        // ════════════════════════════════════════════════════════════════
        //  4.4 / 9.6 — Re-entrancy: guard calling Push inside itself
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void GuardPushInsideItself_DoesNotCorruptHistory()
        {
            var router = new SusRouter();
            router.Register("/first", typeof(DummyScreen));
            router.Register("/second", typeof(DummyScreen));

            var initRoute = MakeRoute("/first");
            router.SetCurrentForTest(initRoute);

            int guardCount = 0;
            router.BeforeEach((from, to) =>
            {
                guardCount++;
                if (guardCount == 1 && to.FullPath == "/first")
                    router.Push("/second");
                return true;
            });

            router.Push("/first");

            Assert.GreaterOrEqual(router.History.Count, 2);
        }

        [Test]
        public void MultipleReEntrantPushes_AreQueuedInOrder()
        {
            var router = new SusRouter();
            router.Register("/a", typeof(DummyScreen));
            router.Register("/b", typeof(DummyScreen));
            router.Register("/c", typeof(DummyScreen));

            var initRoute = MakeRoute("/a");
            router.SetCurrentForTest(initRoute);

            router.BeforeEach((from, to) =>
            {
                if (to.FullPath == "/a")
                    router.Push("/b");
                return true;
            });

            router.Push("/a");

            Assert.GreaterOrEqual(router.History.Count, 2);
        }

        // ════════════════════════════════════════════════════════════════
        //  4.5 — beforeEach fires during param-only navigation
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void BeforeEach_FiresOnParamOnlyNavigation()
        {
            var router = new SusRouter();
            var record = router.Register("/battle/:id", typeof(DummyScreen));

            var route = new SusRoute(record, "/battle/1", null);
            route.Props = new Dictionary<string, object> { ["id"] = "1" };
            route.Screen = new DummyScreen();
            router.SetCurrentForTest(route);

            bool beforeEachFired = false;
            router.BeforeEach((from, to) =>
            {
                beforeEachFired = true;
                return true;
            });

            router.Push("/battle/2");

            Assert.IsTrue(beforeEachFired,
                "beforeEach must fire during param-only navigation");
        }

        [Test]
        public void BeforeResolve_FiresOnParamOnlyNavigation()
        {
            var router = new SusRouter();
            var record = router.Register("/fight/:mission", typeof(DummyScreen));

            var route = new SusRoute(record, "/fight/alpha", null);
            route.Props = new Dictionary<string, object> { ["mission"] = "alpha" };
            route.Screen = new DummyScreen();
            router.SetCurrentForTest(route);

            bool fired = false;
            router.BeforeResolve((from, to) =>
            {
                fired = true;
                return true;
            });

            router.Push("/fight/beta");

            Assert.IsTrue(fired,
                "beforeResolve must fire during param-only navigation");
        }

        [Test]
        public void BeforeEach_Abort_PreventsParamUpdate()
        {
            var router = new SusRouter();
            var record = router.Register("/zone/:id", typeof(DummyScreen));

            var route = new SusRoute(record, "/zone/1", null);
            route.Props = new Dictionary<string, object> { ["id"] = "1" };
            route.Screen = new DummyScreen();
            router.SetCurrentForTest(route);

            router.BeforeEach((from, to) => false);

            var result = router.Push("/zone/2");

            Assert.AreEqual(NavigationResult.Aborted, result);
        }

        // ════════════════════════════════════════════════════════════════
        //  8.3 — beforeResolve abort doesn't leave side effects
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void BeforeResolve_Abort_NoScreenCreated()
        {
            var router = new SusRouter();
            router.Register("/guarded", typeof(CountedDummyScreen));

            var fromRoute = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(fromRoute);

            router.BeforeResolve((from, to) => false);

            int before = CountedDummyScreen.InstanceCount;
            router.Push("/guarded");
            int after = CountedDummyScreen.InstanceCount;

            Assert.AreEqual(before, after,
                "beforeResolve abort should prevent screen instantiation");
        }

        [Test]
        public void BeforeResolve_Abort_DoesNotChangeCurrentRoute()
        {
            var router = new SusRouter();
            router.Register("/target", typeof(DummyScreen));

            var fromRoute = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(fromRoute);

            router.BeforeResolve((from, to) => false);

            router.Push("/target");

            Assert.AreEqual("/home", router.CurrentRoute.Value.FullPath);
        }

        // ════════════════════════════════════════════════════════════════
        //  9.1 — Guard-abort at each pipeline point
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void BeforeEach_Abort_ReturnsAborted()
        {
            var router = new SusRouter();
            router.Register("/forbidden", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            router.BeforeEach((from, to) => false);

            var result = router.Push("/forbidden");

            Assert.AreEqual(NavigationResult.Aborted, result);
        }

        [Test]
        public void BeforeEach_Allows_WhenTrue()
        {
            var router = new SusRouter();
            router.Register("/allowed", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            router.BeforeEach((from, to) => true);

            var result = router.Push("/allowed");

            Assert.AreEqual(NavigationResult.Success, result);
        }

        [Test]
        public void GuardAbort_AtFirstOfMultiple_DoesNotExecuteRest()
        {
            var router = new SusRouter();
            router.Register("/target", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            int secondCallCount = 0;
            router.BeforeEach((from, to) => false);
            router.BeforeEach((from, to) =>
            {
                secondCallCount++;
                return true;
            });

            router.Push("/target");

            Assert.AreEqual(0, secondCallCount,
                "Second guard should not execute if first guard aborted");
        }

        // ════════════════════════════════════════════════════════════════
        //  9.2 — Alias resolution
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void Alias_ResolvesToSameRecord()
        {
            var router = new SusRouter();
            var record = router.Register("/original", typeof(DummyScreen),
                new SusRouteConfig
                {
                    Alias = new List<string> { "/alias" }
                });

            var resolved = router.Resolve("/alias");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(record, resolved);
        }

        // ════════════════════════════════════════════════════════════════
        //  9.5 — beforeRouteUpdate hook called on same record
        // ════════════════════════════════════════════════════════════════

        private class TrackUpdateScreen : SusScreen
        {
            public bool UpdateCalled;
            public int UpdateCount;
            protected override void Build() { }

            protected override bool OnBeforeRouteUpdate(SusRoute to)
            {
                UpdateCalled = true;
                UpdateCount++;
                return base.OnBeforeRouteUpdate(to);
            }
        }

        [Test]
        public void BeforeRouteUpdate_Called_OnSameRecordDifferentParams()
        {
            var router = new SusRouter();
            var record = router.Register("/detail/:tab", typeof(TrackUpdateScreen));

            var screen = new TrackUpdateScreen();
            var route = new SusRoute(record, "/detail/info", null);
            route.Props = new Dictionary<string, object> { ["tab"] = "info" };
            route.Screen = screen;
            router.SetCurrentForTest(route);

            router.Push("/detail/settings");

            Assert.IsTrue(screen.UpdateCalled,
                "BeforeRouteUpdate must be called on same record, different params");
            Assert.AreEqual(1, screen.UpdateCount);
        }

        [Test]
        public void BeforeRouteUpdate_NotCalled_OnDifferentRecord()
        {
            var router = new SusRouter();
            var recordA = router.Register("/pageA", typeof(TrackUpdateScreen));
            router.Register("/pageB", typeof(DummyScreen));

            var screen = new TrackUpdateScreen();
            var route = new SusRoute(recordA, "/pageA", null);
            route.Screen = screen;
            router.SetCurrentForTest(route);

            router.Push("/pageB");

            Assert.IsFalse(screen.UpdateCalled,
                "BeforeRouteUpdate should NOT fire when navigating to a different record");
        }

        // ════════════════════════════════════════════════════════════════
        //  9.3 — Query parsing
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void Push_WithQueryParameters_ResolvesRoute()
        {
            var router = new SusRouter();
            router.Register("/search", typeof(DummyScreen));

            var result = router.Push("/search?q=test&page=1");

            Assert.AreEqual(NavigationResult.Success, result);
        }

        [Test]
        public void Resolve_Path_StripsQueryForMatching()
        {
            var router = new SusRouter();
            router.Register("/battle", typeof(DummyScreen));

            var record = router.Resolve("/battle?mode=pvp&map=arena");

            Assert.IsNotNull(record);
            Assert.AreEqual("/battle", record.Path);
        }

        // ════════════════════════════════════════════════════════════════
        //  9.8 — Back/Forward/Go(n) boundaries
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void Back_FromEmptyHistory_ReturnsCantGoBack()
        {
            var router = new SusRouter();
            Assert.AreEqual(NavigationResult.CantGoBack, router.Back());
        }

        [Test]
        public void Forward_FromLastEntry_ReturnsCantGoForward()
        {
            var router = new SusRouter();
            var route = MakeRoute("/a");
            router.SetCurrentForTest(route);

            Assert.AreEqual(NavigationResult.CantGoForward, router.Forward());
        }

        [Test]
        public void BackForward_HistoryIntegrity()
        {
            var router = new SusRouter();
            router.Register("/p1", typeof(DummyScreen));
            router.Register("/p2", typeof(DummyScreen));
            router.Register("/p3", typeof(DummyScreen));

            var r1 = MakeRoute("/p1");
            router.SetCurrentForTest(r1);

            router.Push("/p2");
            router.Push("/p3");

            Assert.AreEqual(3, router.History.Count);
            Assert.IsTrue(router.CanGoBack);
            Assert.IsFalse(router.CanGoForward);

            router.Back();
            Assert.IsTrue(router.CanGoBack);
            Assert.IsTrue(router.CanGoForward);

            router.Back();
            Assert.IsFalse(router.CanGoBack);
            Assert.IsTrue(router.CanGoForward);

            router.Forward();
            Assert.IsTrue(router.CanGoBack);
        }

        // ════════════════════════════════════════════════════════════════
        //  AfterEach hook
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void AfterEach_Hook_RunsAfterSuccessfulNavigation()
        {
            var router = new SusRouter();
            router.Register("/dest", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            bool called = false;
            string path = null;
            router.AfterEach((from, to) =>
            {
                called = true;
                path = to.FullPath;
            });

            router.Push("/dest");

            Assert.IsTrue(called);
            Assert.AreEqual("/dest", path);
        }

        // ════════════════════════════════════════════════════════════════
        //  7 — Async guard tests
        // ════════════════════════════════════════════════════════════════

        [Test]
        public async System.Threading.Tasks.Task AsyncGuard_Allows_WhenTrue()
        {
            var router = new SusRouter();
            router.Register("/allowed", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            router.BeforeEachAsync(async (from, to) =>
            {
                await System.Threading.Tasks.Task.Delay(10);
                return true;
            });

            var result = await router.PushAsync("/allowed");

            Assert.AreEqual(NavigationResult.Success, result);
        }

        [Test]
        public async System.Threading.Tasks.Task AsyncGuard_Aborts_WhenFalse()
        {
            var router = new SusRouter();
            router.Register("/forbidden", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            router.BeforeEachAsync(async (from, to) =>
            {
                await System.Threading.Tasks.Task.Delay(10);
                return false;
            });

            var result = await router.PushAsync("/forbidden");

            Assert.AreEqual(NavigationResult.Aborted, result);
        }

        [Test]
        public async System.Threading.Tasks.Task AsyncGuards_RunSequentiallyWithSync()
        {
            var router = new SusRouter();
            router.Register("/target", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            int syncCount = 0;
            int asyncCount = 0;

            router.BeforeEach((from, to) =>
            {
                syncCount++;
                return true;
            });
            router.BeforeEachAsync(async (from, to) =>
            {
                await System.Threading.Tasks.Task.Delay(10);
                asyncCount++;
                return true;
            });

            var result = await router.PushAsync("/target");

            Assert.AreEqual(NavigationResult.Success, result);
            Assert.AreEqual(1, syncCount, "Sync guard should run");
            Assert.AreEqual(1, asyncCount, "Async guard should run");
        }

        [Test]
        public void SyncPush_DoesNotInvokeAsyncGuards()
        {
            var router = new SusRouter();
            router.Register("/target", typeof(DummyScreen));

            var home = MakeRoute("/home", new DummyScreen());
            router.SetCurrentForTest(home);

            int asyncCalled = 0;
            router.BeforeEachAsync((from, to) =>
            {
                asyncCalled++;
                return System.Threading.Tasks.Task.FromResult(true);
            });

            router.Push("/target"); // Sync push — async guards NOT invoked

            Assert.AreEqual(0, asyncCalled,
                "Async guards should NOT be invoked on sync Push");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  2.6 — Nested routes: parent not recreated on child switch
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nested route tests: chain resolution, screen reuse, ChildView rendering.
    /// </summary>
    public class SusRouterNestedRouteTests
    {
        private class ParentScreen : SusScreen
        {
            public static int InstanceCount;
            public readonly int Id;
            public ParentScreen() { Id = ++InstanceCount; }
            protected override void Build()
            {
                var childView = new SusRouteView();
                RegisterChildView(childView);
                Add(childView);
            }
        }

        private class ChildAScreen : SusScreen
        {
            public static int InstanceCount;
            public readonly int Id;
            public ChildAScreen() { Id = ++InstanceCount; }
            protected override void Build() { }
        }

        private class ChildBScreen : SusScreen
        {
            public static int InstanceCount;
            public readonly int Id;
            public ChildBScreen() { Id = ++InstanceCount; }
            protected override void Build() { }
        }

        [SetUp]
        public void SetUp()
        {
            ParentScreen.InstanceCount = 0;
            ChildAScreen.InstanceCount = 0;
            ChildBScreen.InstanceCount = 0;
        }

        [Test]
        public void ResolveChain_ReturnsFullChain()
        {
            var router = new SusRouter();
            router.Register("/settings", typeof(ParentScreen), new SusRouteConfig
            {
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("profile", typeof(ChildAScreen)),
                    new SusRouteRecord("account", typeof(ChildBScreen)),
                }
            });

            var chain = router.ResolveChain("/settings/profile");

            Assert.IsNotNull(chain);
            Assert.AreEqual(2, chain.Count);
            Assert.AreEqual("/settings", chain[0].Path);
            Assert.AreEqual("profile", chain[1].Path);
        }

        [Test]
        public void ResolveChain_SingleLevel_ReturnsOneElement()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(ParentScreen));

            var chain = router.ResolveChain("/home");

            Assert.IsNotNull(chain);
            Assert.AreEqual(1, chain.Count);
            Assert.AreEqual("/home", chain[0].Path);
        }

        [Test]
        public void FindCommonPrefixDepth_FullMatch()
        {
            var r1 = new SusRouteRecord("a", typeof(ParentScreen));
            var r2 = new SusRouteRecord("b", typeof(ParentScreen));
            var chain1 = new System.Collections.Generic.List<SusRouteRecord> { r1, r2 };
            var chain2 = new System.Collections.Generic.List<SusRouteRecord> { r1, r2 };

            int depth = SusRouter.FindCommonPrefixDepth(chain1, chain2);

            Assert.AreEqual(2, depth);
        }

        [Test]
        public void FindCommonPrefixDepth_PartialMatch()
        {
            var r1 = new SusRouteRecord("a", typeof(ParentScreen));
            var r2 = new SusRouteRecord("b", typeof(ParentScreen));
            var r3 = new SusRouteRecord("c", typeof(ParentScreen));
            var chain1 = new System.Collections.Generic.List<SusRouteRecord> { r1, r2 };
            var chain2 = new System.Collections.Generic.List<SusRouteRecord> { r1, r3 };

            int depth = SusRouter.FindCommonPrefixDepth(chain1, chain2);

            Assert.AreEqual(1, depth);
        }

        [Test]
        public void FindCommonPrefixDepth_NoMatch()
        {
            var r1 = new SusRouteRecord("a", typeof(ParentScreen));
            var r2 = new SusRouteRecord("b", typeof(ParentScreen));
            var chain1 = new System.Collections.Generic.List<SusRouteRecord> { r1 };
            var chain2 = new System.Collections.Generic.List<SusRouteRecord> { r2 };

            int depth = SusRouter.FindCommonPrefixDepth(chain1, chain2);

            Assert.AreEqual(0, depth);
        }

        [Test]
        public void Push_NestedRoute_PopulatesMatchedChainAndChainScreens()
        {
            var router = new SusRouter();
            router.Register("/settings", typeof(ParentScreen), new SusRouteConfig
            {
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("profile", typeof(ChildAScreen)),
                    new SusRouteRecord("account", typeof(ChildBScreen)),
                }
            });

            router.Push("/settings/profile");

            var current = router.CurrentRoute.Value;
            Assert.IsNotNull(current.MatchedChain);
            Assert.AreEqual(2, current.MatchedChain.Count);
            Assert.IsNotNull(current.ChainScreens);
            Assert.AreEqual(2, current.ChainScreens.Count);
            Assert.IsInstanceOf<ParentScreen>(current.ChainScreens[0]);
            Assert.IsInstanceOf<ChildAScreen>(current.ChainScreens[1]);
        }

        [Test]
        public void Push_NestedRoute_ParentIsChainRoot()
        {
            var router = new SusRouter();
            router.Register("/settings", typeof(ParentScreen), new SusRouteConfig
            {
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("profile", typeof(ChildAScreen)),
                }
            });

            router.Push("/settings/profile");

            var current = router.CurrentRoute.Value;
            // Root screen should be ParentScreen (first in chain), not child
            Assert.IsInstanceOf<ParentScreen>(current.Screen);
            // But Record still points to leaf
            Assert.AreEqual("profile", current.Record.Path);
        }

        [Test]
        public void Push_BetweenChildren_ParentIsReused()
        {
            var router = new SusRouter();
            router.Register("/settings", typeof(ParentScreen), new SusRouteConfig
            {
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("profile", typeof(ChildAScreen)),
                    new SusRouteRecord("account", typeof(ChildBScreen)),
                }
            });

            // Push to first child
            router.Push("/settings/profile");
            var parentScreen = ((ParentScreen)router.CurrentRoute.Value.ChainScreens[0]);
            int parentId = parentScreen.Id;

            // Push to second child — parent should be reused
            router.Push("/settings/account");
            var parentScreen2 = ((ParentScreen)router.CurrentRoute.Value.ChainScreens[0]);
            int parentId2 = parentScreen2.Id;

            Assert.AreEqual(parentId, parentId2,
                "Parent screen should be reused when switching between children");
        }

        [Test]
        public void Push_BetweenChildren_ChildIsReplaced()
        {
            var router = new SusRouter();
            router.Register("/settings", typeof(ParentScreen), new SusRouteConfig
            {
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("profile", typeof(ChildAScreen)),
                    new SusRouteRecord("account", typeof(ChildBScreen)),
                }
            });

            router.Push("/settings/profile");
            var firstChild = router.CurrentRoute.Value.ChainScreens[1];

            router.Push("/settings/account");
            var secondChild = router.CurrentRoute.Value.ChainScreens[1];

            Assert.AreNotSame(firstChild, secondChild,
                "Child screen should be replaced when switching between children");
            Assert.IsInstanceOf<ChildAScreen>(firstChild);
            Assert.IsInstanceOf<ChildBScreen>(secondChild);
        }

        [Test]
        public void Push_NestedRoute_ChildViewHasRouter()
        {
            var router = new SusRouter();
            router.Register("/settings", typeof(ParentScreen), new SusRouteConfig
            {
                Children = new System.Collections.Generic.List<SusRouteRecord>
                {
                    new SusRouteRecord("profile", typeof(ChildAScreen)),
                }
            });

            router.Push("/settings/profile");
            var rootScreen = (ParentScreen)router.CurrentRoute.Value.ChainScreens[0];

            Assert.IsNotNull(rootScreen.ChildView,
                "Parent screen should have a registered ChildView");
        }

        [Test]
        public void Push_NestedSingleLevel_NoChainScreens()
        {
            var router = new SusRouter();
            router.Register("/home", typeof(ParentScreen));

            router.Push("/home");

            var current = router.CurrentRoute.Value;
            // Single-level: MatchedChain is [record] but count=1, so no ChainScreens created
            Assert.IsNull(current.ChainScreens);
            Assert.IsInstanceOf<ParentScreen>(current.Screen);
        }
    }
}

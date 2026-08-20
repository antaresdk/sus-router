using NUnit.Framework;
using System.Threading.Tasks;

using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    /// <summary>
    /// P0.5 regression: NavigateAsync awaits BeforeResolveAsync (previously ignored),
    /// and sync Push does not run async guards.
    /// </summary>
    public class SusRouterAsyncResolveTests
    {
        private class DummyScreen : SusScreen
        {
            protected override void Build() { }
        }

        private class CountedScreen : SusScreen
        {
            public static int InstanceCount;
            public CountedScreen() { InstanceCount++; }
            protected override void Build() { }
        }

        private static void SetHome(SusRouter router)
        {
            var rec = new SusRouteRecord("/home", typeof(DummyScreen));
            var route = new SusRoute(rec, "/home", null) { Screen = new DummyScreen() };
            router.SetCurrentForTest(route);
        }

        [Test]
        public async Task BeforeResolveAsync_Allows_WhenTrue()
        {
            var router = new SusRouter();
            router.Register("/target", typeof(DummyScreen));
            SetHome(router);

            bool fired = false;
            router.BeforeResolveAsync(async (from, to) =>
            {
                await Task.Delay(10);
                fired = true;
                return true;
            });

            var result = await router.PushAsync("/target");

            Assert.AreEqual(NavigationResult.Success, result);
            Assert.IsTrue(fired, "BeforeResolveAsync must be awaited");
        }

        [Test]
        public async Task BeforeResolveAsync_Aborts_WhenFalse()
        {
            var router = new SusRouter();
            router.Register("/target", typeof(DummyScreen));
            SetHome(router);

            router.BeforeResolveAsync(async (from, to) =>
            {
                await Task.Delay(10);
                return false;
            });

            var result = await router.PushAsync("/target");

            Assert.AreEqual(NavigationResult.Aborted, result);
            Assert.AreEqual("/home", router.CurrentRoute.Value.FullPath);
        }

        [Test]
        public async Task BeforeResolveAsync_Abort_PreventsScreenCreation()
        {
            CountedScreen.InstanceCount = 0;
            var router = new SusRouter();
            router.Register("/guarded", typeof(CountedScreen));
            SetHome(router);

            router.BeforeResolveAsync(async (from, to) =>
            {
                await Task.Delay(10);
                return false;
            });

            int before = CountedScreen.InstanceCount;
            await router.PushAsync("/guarded");

            Assert.AreEqual(before, CountedScreen.InstanceCount,
                "Async beforeResolve abort must prevent screen instantiation");
        }

        [Test]
        public void SyncPush_DoesNotInvokeBeforeResolveAsync()
        {
            var router = new SusRouter();
            router.Register("/target", typeof(DummyScreen));
            SetHome(router);

            int called = 0;
            router.BeforeResolveAsync((from, to) =>
            {
                called++;
                return System.Threading.Tasks.Task.FromResult(true);
            });

            router.Push("/target"); // sync — async guards skipped

            Assert.AreEqual(0, called,
                "Async beforeResolve must NOT run on sync Push");
        }

        /// <summary>
        /// Regression: NavigateAsync must hold the re-entrancy guard for its full
        /// duration (including the awaited guards), not just around the final sync step.
        /// Otherwise two concurrent PushAsync calls can both run their async guards against
        /// the same stale fromRoute/CurrentRoute snapshot before either commits — the second
        /// one then mutates a fromRoute object the first navigation has already torn down.
        /// </summary>
        [Test, Timeout(2000)]
        public async Task ConcurrentPushAsync_SecondCallDroppedBusy_DoesNotRaceFromTo()
        {
            var router = new SusRouter();
            router.Register("/a", typeof(DummyScreen));
            router.Register("/b", typeof(DummyScreen));
            SetHome(router);

            var gate = new TaskCompletionSource<bool>();
            int guardCalls = 0;
            router.BeforeEachAsync(async (from, to) =>
            {
                guardCalls++;
                await gate.Task;
                return true;
            });

            // First call starts and suspends mid-guard, awaiting the gate. If the
            // re-entrancy guard is held across the await (the fix), this already
            // means _isNavigating == true at this point.
            var firstTask = router.PushAsync("/a");

            // A second call issued while the first is still in-flight must be dropped
            // as Busy WITHOUT ever invoking the guard (it never gets to race fromRoute).
            var secondResult = await router.PushAsync("/b");

            Assert.AreEqual(NavigationResult.Busy, secondResult,
                "A concurrent PushAsync issued while the first is awaiting its guards " +
                "must be dropped as Busy, not race the in-flight navigation's from/to.");
            Assert.AreEqual(1, guardCalls,
                "The busy-dropped call must not invoke guards at all.");

            gate.SetResult(true);
            var firstResult = await firstTask;

            Assert.AreEqual(NavigationResult.Success, firstResult);
            Assert.AreEqual("/a", router.CurrentRoute.Value.FullPath,
                "The winning navigation must land on its own target route.");
        }
    }
}

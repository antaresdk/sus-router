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
    }
}

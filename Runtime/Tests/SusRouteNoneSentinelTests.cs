using NUnit.Framework;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// T-1117 / R-C5: <see cref="SusRoute.None"/> must behave as an immutable sentinel — mutating
    /// one read's Props/IsActive/Screen or the exposed Params/Query dictionaries must never leak
    /// into the next read (statics survive Play sessions when domain reload is disabled, so a
    /// single shared instance would corrupt every later "no route" fallback for the rest of the
    /// session).
    /// </summary>
    public class SusRouteNoneSentinelTests
    {
        [Test]
        public void None_IsDistinctInstancePerAccess()
        {
            Assert.AreNotSame(SusRoute.None, SusRoute.None);
        }

        [Test]
        public void None_MutatingProps_DoesNotAffectNextRead()
        {
            var a = SusRoute.None;
            a.IsActive = true;
            a.Props = new() { ["x"] = 1 };

            var b = SusRoute.None;
            Assert.IsFalse(b.IsActive);
            Assert.IsNull(b.Props);
        }

        [Test]
        public void None_MutatingParamsDictionary_DoesNotAffectNextRead()
        {
            var a = SusRoute.None;
            a.Params["injected"] = "leak";

            var b = SusRoute.None;
            Assert.IsFalse(b.Params.ContainsKey("injected"));
        }

        [Test]
        public void None_HasExpectedShape()
        {
            var none = SusRoute.None;
            Assert.IsNull(none.Record);
            Assert.AreEqual("<none>", none.FullPath);
            Assert.IsNotNull(none.Params);
            Assert.AreEqual(0, none.Params.Count);
        }
    }
}

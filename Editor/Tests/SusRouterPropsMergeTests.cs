using NUnit.Framework;
using System.Collections.Generic;

using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    /// <summary>
    /// P0.2 regression: route params (:id) and query (?k=v) are merged into
    /// SusScreen.Props with priority PropsFn → DefaultProps → query → params → explicit.
    /// GetParam()/GetQuery() therefore return values as documented.
    /// </summary>
    public class SusRouterPropsMergeTests
    {
        private class DummyScreen : SusScreen
        {
            protected override void Build() { }
        }

        /// <summary>Captures GetParam/GetQuery during OnBeforeEnter (Props set beforehand).</summary>
        private class ParamReaderScreen : SusScreen
        {
            public string CapturedId;
            public string CapturedTab;
            protected override void Build() { }
            protected override bool OnBeforeEnter(SusRoute from)
            {
                CapturedId = GetParam("id");
                CapturedTab = GetQuery("tab");
                return true;
            }
        }

        private static void SetHome(SusRouter router)
        {
            var rec = new SusRouteRecord("/home", typeof(DummyScreen));
            var route = new SusRoute(rec, "/home", null) { Screen = new DummyScreen() };
            router.SetCurrentForTest(route);
        }

        [Test]
        public void Push_RouteParam_MergedIntoProps()
        {
            var router = new SusRouter();
            router.Register("/user/:id", typeof(DummyScreen));
            SetHome(router);

            router.Push("/user/42");

            Assert.AreEqual("42", router.CurrentRoute.Value.Props["id"]);
        }

        [Test]
        public void Push_Query_MergedIntoProps()
        {
            var router = new SusRouter();
            router.Register("/search", typeof(DummyScreen));
            SetHome(router);

            router.Push("/search?q=hello&page=1");

            Assert.AreEqual("hello", router.CurrentRoute.Value.Props["q"]);
            Assert.AreEqual("1", router.CurrentRoute.Value.Props["page"]);
        }

        [Test]
        public void Push_ParamsAndQuery_BothMerged()
        {
            var router = new SusRouter();
            router.Register("/user/:id", typeof(DummyScreen));
            SetHome(router);

            router.Push("/user/42?tab=info");

            var props = router.CurrentRoute.Value.Props;
            Assert.AreEqual("42", props["id"]);
            Assert.AreEqual("info", props["tab"]);
        }

        [Test]
        public void GetParam_And_GetQuery_ReturnMergedValues()
        {
            var router = new SusRouter();
            router.Register("/user/:id", typeof(ParamReaderScreen));
            SetHome(router);

            router.Push("/user/42?tab=info");

            var screen = (ParamReaderScreen)router.CurrentRoute.Value.Screen;
            Assert.AreEqual("42", screen.CapturedId);
            Assert.AreEqual("info", screen.CapturedTab);
        }

        [Test]
        public void ExplicitProps_OverrideRouteParams()
        {
            var router = new SusRouter();
            router.Register("/user/:id", typeof(DummyScreen));
            SetHome(router);

            router.Push("/user/42", new Dictionary<string, object> { ["id"] = "99" });

            Assert.AreEqual("99", router.CurrentRoute.Value.Props["id"]);
        }

        [Test]
        public void RouteParams_OverrideQuery_OnNameClash()
        {
            var router = new SusRouter();
            router.Register("/item/:id", typeof(DummyScreen));
            SetHome(router);

            // Both a path param and a query named "id" — path param wins.
            router.Push("/item/5?id=99");

            Assert.AreEqual("5", router.CurrentRoute.Value.Props["id"]);
        }

        [Test]
        public void Query_OverridesDefaultProps()
        {
            var router = new SusRouter();
            router.Register("/page", typeof(DummyScreen), new SusRouteConfig
            {
                DefaultProps = new Dictionary<string, object> { ["x"] = "default" }
            });
            SetHome(router);

            router.Push("/page?x=fromQuery");

            Assert.AreEqual("fromQuery", router.CurrentRoute.Value.Props["x"]);
        }

        [Test]
        public void DefaultProps_UsedWhenNoParamOrQuery()
        {
            var router = new SusRouter();
            router.Register("/page", typeof(DummyScreen), new SusRouteConfig
            {
                DefaultProps = new Dictionary<string, object> { ["x"] = "default" }
            });
            SetHome(router);

            router.Push("/page");

            Assert.AreEqual("default", router.CurrentRoute.Value.Props["x"]);
        }
    }
}

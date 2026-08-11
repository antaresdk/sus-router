using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    /// <summary>
    /// T-011 — SusScreen editmode tests.
    /// Covers: USS class, Template-Method lifecycle hooks, GetProp/GetParam/GetQuery,
    /// RegisterChildView / ChildView. Navigation integration lives in playmode suites.
    /// </summary>
    public class SusScreenTests
    {
        private class ProbeScreen : SusScreen
        {
            public int BeforeEnterCalls;
            public int EnteredCalls;
            public int BeforeLeaveCalls;
            public int LeftCalls;
            public int BeforeRouteUpdateCalls;
            public bool AllowEnter = true;
            public bool AllowLeave = true;
            public bool AllowUpdate = true;
            public SusRoute LastFrom;
            public SusRoute LastTo;

            public string ReadTitleDefault() => GetProp("title", "fallback");
            public int ReadCount() => GetProp("count", -1);
            public string ReadParamId() => GetParam("id", "none");
            public string ReadQueryQ() => GetQuery("q", "none");
            public object ReadRaw() => GetProp("missing", "default-obj");

            protected override void Build() { }

            protected override bool OnBeforeEnter(SusRoute from)
            {
                BeforeEnterCalls++;
                LastFrom = from;
                return AllowEnter;
            }

            protected override void OnEntered()
            {
                EnteredCalls++;
            }

            protected override bool OnBeforeLeave(SusRoute to)
            {
                BeforeLeaveCalls++;
                LastTo = to;
                return AllowLeave;
            }

            protected override void OnLeft()
            {
                LeftCalls++;
            }

            protected override bool OnBeforeRouteUpdate(SusRoute to)
            {
                BeforeRouteUpdateCalls++;
                LastTo = to;
                return AllowUpdate;
            }

            public void RegisterView(SusRouteView view) => RegisterChildView(view);
        }

        private static SusRoute MakeRoute(string path)
        {
            var record = new SusRouteRecord(path, typeof(ProbeScreen));
            return new SusRoute(record, path, null);
        }

        [Test]
        public void Constructor_AddsSusScreenUssClass()
        {
            var screen = new ProbeScreen();
            Assert.IsTrue(screen.ClassListContains(SusScreen.UssClassName));
        }

        [Test]
        public void Lifecycle_PublicMethods_DelegateToHooks()
        {
            var screen = new ProbeScreen();
            var from = MakeRoute("/from");
            var to = MakeRoute("/to");

            Assert.IsTrue(screen.BeforeEnter(from));
            screen.Entered();
            Assert.IsTrue(screen.BeforeRouteUpdate(to));
            Assert.IsTrue(screen.BeforeLeave(to));
            screen.Left();

            Assert.AreEqual(1, screen.BeforeEnterCalls);
            Assert.AreEqual(1, screen.EnteredCalls);
            Assert.AreEqual(1, screen.BeforeRouteUpdateCalls);
            Assert.AreEqual(1, screen.BeforeLeaveCalls);
            Assert.AreEqual(1, screen.LeftCalls);
            Assert.AreSame(from, screen.LastFrom);
            Assert.AreSame(to, screen.LastTo);
        }

        [Test]
        public void BeforeEnter_False_BlocksEntry()
        {
            var screen = new ProbeScreen { AllowEnter = false };
            Assert.IsFalse(screen.BeforeEnter(MakeRoute("/x")));
            Assert.AreEqual(1, screen.BeforeEnterCalls);
        }

        [Test]
        public void BeforeLeave_False_BlocksLeave()
        {
            var screen = new ProbeScreen { AllowLeave = false };
            Assert.IsFalse(screen.BeforeLeave(MakeRoute("/y")));
            Assert.AreEqual(1, screen.BeforeLeaveCalls);
            Assert.AreEqual(0, screen.LeftCalls);
        }

        [Test]
        public void BeforeRouteUpdate_False_BlocksUpdate()
        {
            var screen = new ProbeScreen { AllowUpdate = false };
            Assert.IsFalse(screen.BeforeRouteUpdate(MakeRoute("/z")));
            Assert.AreEqual(1, screen.BeforeRouteUpdateCalls);
        }

        [Test]
        public void GetProp_Typed_ReturnsValueOrDefault()
        {
            var screen = new ProbeScreen();
            screen.Props = new Dictionary<string, object>
            {
                ["title"] = "Hello",
                ["count"] = 7,
            };

            Assert.AreEqual("Hello", screen.ReadTitleDefault());
            Assert.AreEqual(7, screen.ReadCount());
            Assert.AreEqual("fallback", new ProbeScreen().ReadTitleDefault());
        }

        [Test]
        public void GetProp_Convertible_ConvertsTypes()
        {
            var screen = new ProbeScreen();
            screen.Props = new Dictionary<string, object> { ["count"] = "42" };
            Assert.AreEqual(42, screen.ReadCount());
        }

        [Test]
        public void GetParam_And_GetQuery_AliasGetProp()
        {
            var screen = new ProbeScreen();
            screen.Props = new Dictionary<string, object>
            {
                ["id"] = "unit-1",
                ["q"] = "search",
            };

            Assert.AreEqual("unit-1", screen.ReadParamId());
            Assert.AreEqual("search", screen.ReadQueryQ());
            Assert.AreEqual("none", new ProbeScreen().ReadParamId());
            Assert.AreEqual("default-obj", screen.ReadRaw());
        }

        [Test]
        public void RegisterChildView_ExposesChildView()
        {
            var screen = new ProbeScreen();
            Assert.IsNull(screen.ChildView);

            var view = new SusRouteView();
            screen.RegisterView(view);

            Assert.AreSame(view, screen.ChildView);
        }

        [Test]
        public void RegisterChildView_Null_IsNoOp()
        {
            var screen = new ProbeScreen();
            screen.RegisterView(null);
            Assert.IsNull(screen.ChildView);
        }

        [Test]
        public void Props_NeverNull_ByDefault()
        {
            var screen = new ProbeScreen();
            Assert.IsNotNull(screen.Props);
            Assert.AreEqual(0, screen.Props.Count);
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// SusRouteTransition tests (EditMode + fixed SusMotion ticks).
    /// Covers: factory helpers, None no-op, PlayIn/PlayOut start state + KeywordNull.
    /// </summary>
    public class SusRouteTransitionTests
    {
        private GameObject _go;
        private UIDocument _doc;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestTransitionUI", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();
            _doc.panelSettings = SusTestPanelFactory.Create("SusTestPanelSettings_RouteTransition");
            _root = _doc.rootVisualElement;
            Assert.IsNotNull(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            _doc = null;
            _root = null;
        }

        [Test]
        public void Factories_SetIdAndDuration()
        {
            Assert.IsNull(SusRouteTransition.None().Id);
            Assert.AreEqual(0f, SusRouteTransition.None().Duration);

            Assert.AreEqual("fade", SusRouteTransition.Fade().Id);
            Assert.AreEqual(0.3f, SusRouteTransition.Fade().Duration);
            Assert.AreEqual(0.05f, SusRouteTransition.Fade(0.05f).Duration);

            Assert.AreEqual("slide-left", SusRouteTransition.SlideLeft().Id);
            Assert.AreEqual(0.3f, SusRouteTransition.SlideLeft().Duration);

            Assert.AreEqual("slide-right", SusRouteTransition.SlideRight().Id);
            Assert.AreEqual(0.3f, SusRouteTransition.SlideRight().Duration);
        }

        [Test]
        public void None_PlayOut_DoesNotRemoveElement()
        {
            var el = new VisualElement { name = "none-out" };
            _root.Add(el);

            SusRouteTransition.None().PlayOut(el);

            Assert.AreSame(_root, el.parent, "Duration 0 PlayOut must be a no-op");
        }

        [Test]
        public void None_PlayIn_LeavesElementAttached()
        {
            var el = new VisualElement { name = "none-in" };
            _root.Add(el);

            SusRouteTransition.None().PlayIn(el);

            Assert.AreSame(_root, el.parent);
        }

        [Test]
        public void Fade_PlayOut_SchedulesWithoutThrowing()
        {
            var el = new VisualElement { name = "fade-out" };
            el.style.opacity = 1f;
            _root.Add(el);

            Assert.DoesNotThrow(() => SusRouteTransition.Fade(0.05f).PlayOut(el));
            Assert.IsNotNull(el);
            Assert.IsNotNull(TryGetActiveMotion(el));
        }

        [Test]
        public void Fade_PlayIn_SetsInitialOutState()
        {
            var el = new VisualElement { name = "fade-in" };
            _root.Add(el);

            SusRouteTransition.Fade(0.3f).PlayIn(el);
            Assert.LessOrEqual(el.style.opacity.value, 0.05f);
            Assert.AreSame(_root, el.parent);
        }

        [Test]
        public void SlideLeft_PlayIn_SetsInitialOffset()
        {
            var el = new VisualElement { name = "slide-in" };
            _root.Add(el);

            SusRouteTransition.SlideLeft(0.3f).PlayIn(el);
            Assert.LessOrEqual(el.style.opacity.value, 0.05f);
            Assert.LessOrEqual(el.style.translate.value.x.value, -25f);
            Assert.AreSame(_root, el.parent);
        }

        [Test]
        public void PlayOut_NullElement_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SusRouteTransition.Fade().PlayOut(null));
            Assert.DoesNotThrow(() => SusRouteTransition.Fade().PlayIn(null));
        }

        [Test]
        public void Fade_PlayIn_KeywordNull_ClearsInlineOpacity()
        {
            var el = new VisualElement { name = "fade-in-null" };
            _root.Add(el);

            SusRouteTransition.Fade(0.048f).PlayIn(el);
            var motion = TryGetActiveMotion(el);
            Assert.IsNotNull(motion, "PlayIn should start SusMotion on target");

            AdvanceFixed(motion, 5);

            Assert.AreEqual(StyleKeyword.Null, el.style.opacity.keyword);
            Assert.AreSame(_root, el.parent);
        }

        [Test]
        public void Fade_PlayOut_KeywordNull_ThenRemoves()
        {
            var el = new VisualElement { name = "fade-out-null" };
            el.style.opacity = 1f;
            _root.Add(el);

            SusRouteTransition.Fade(0.048f).PlayOut(el);
            var motion = TryGetActiveMotion(el);
            Assert.IsNotNull(motion, "PlayOut should start SusMotion on target");

            AdvanceFixed(motion, 5);

            Assert.AreEqual(StyleKeyword.Null, el.style.opacity.keyword);
            Assert.IsNull(el.parent, "PlayOut must remove from hierarchy after complete");
        }

        static SusMotion TryGetActiveMotion(VisualElement el)
        {
            var field = typeof(SusMotion).GetField(
                "ActiveByTarget",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "SusMotion.ActiveByTarget");
            var dict = field.GetValue(null) as IDictionary;
            Assert.IsNotNull(dict);
            return dict.Contains(el) ? dict[el] as SusMotion : null;
        }

        static void AdvanceFixed(SusMotion motion, int ticks)
        {
            var method = typeof(SusMotion).GetMethod(
                "AdvanceFixedTickForTests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "SusMotion.AdvanceFixedTickForTests");
            for (int i = 0; i < ticks; i++)
                method.Invoke(motion, null);
        }
    }
}

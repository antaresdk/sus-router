using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Sharq.Core;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>
    /// SusScreen.Leaving / OnLeaving — PlayMode matrix on a live panel.
    ///
    /// Shape under test: a root shell screen with a registered ChildView plus child routes
    /// (/a/x, /a/y, /a/k KeepAlive), a second shell (/c/z), and single-level routes
    /// (/b, /d, /p/:id, /k1, /k2 KeepAlive).
    ///
    /// OnLeaving contract: once per leave, for every screen leaving the active chain,
    /// leaf → root, with parent and panel live, after every guard, before any detach.
    ///
    /// OnLeft is pinned AS OBSERVED (its timing is unchanged by OnLeaving). Rows marked
    /// "current OnLeft behavior" document paths where OnLeft runs outside the tree or does
    /// not run at all; they are pinned so a future OnLeft fix flips them deliberately.
    /// </summary>
    public class SusRouteLeavingTests
    {
        GameObject _go;
        UIDocument _doc;
        VisualElement _root;
        SusRouter _router;

        static readonly List<string> Log = new();
        static readonly List<ProbeScreen> Instances = new();

        // ── Probe screens ────────────────────────────────────────────────────

        abstract class ProbeScreen : SusScreen
        {
            public int LeavingCount;
            public int LeftCount;
            public bool LeavingParentLive;
            public bool LeavingPanelLive;
            public bool LeavingShellLive;
            public bool LeftPanelLive;
            public SusRoute LeavingTarget;

            protected ProbeScreen() { Instances.Add(this); }

            protected override void Build()
            {
                Add(new Label { text = GetType().Name });
            }

            protected override void OnLeaving(SusRoute toRoute)
            {
                LeavingCount++;
                LeavingTarget = toRoute;
                LeavingParentLive = parent != null;
                LeavingPanelLive = panel != null;
                LeavingShellLive = GetFirstAncestorOfType<ShellScreen>() != null;
                Log.Add("Leaving:" + GetType().Name);
            }

            protected override void OnLeft()
            {
                LeftCount++;
                LeftPanelLive = panel != null;
                Log.Add("Left:" + GetType().Name);
            }

            protected override void OnEntered()
            {
                Log.Add("Entered:" + GetType().Name);
            }
        }

        abstract class ShellScreen : ProbeScreen
        {
            public bool AllowLeave = true;

            protected override void Build()
            {
                base.Build();
                var childView = new SusRouteView();
                RegisterChildView(childView);
                Add(childView);
            }

            protected override bool OnBeforeLeave(SusRoute to) => AllowLeave;
        }

        class ShellA : ShellScreen { }
        class ShellC : ShellScreen { }
        class PaneX : ProbeScreen { }
        class PaneY : ProbeScreen { }
        class PaneK : ProbeScreen { }
        class PaneZ : ProbeScreen { }
        class PageB : ProbeScreen { }
        class PageD : ProbeScreen { }
        class PageP : ProbeScreen { }
        class PageK1 : ProbeScreen { }
        class PageK2 : ProbeScreen { }

        // ── Setup / Teardown ─────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            Log.Clear();
            Instances.Clear();

            _go = new GameObject("TestRouteLeaving", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();
            _doc.panelSettings = SusTestPanelFactory.Create("SusTestPanelSettings_Leaving");
            _root = _doc.rootVisualElement;

            _router = new SusRouter();
            _router.Register("/a", typeof(ShellA), new SusRouteConfig
            {
                Children = new List<SusRouteRecord>
                {
                    new SusRouteRecord("x", typeof(PaneX)),
                    new SusRouteRecord("y", typeof(PaneY)),
                    new SusRouteRecord("k", typeof(PaneK), new SusRouteConfig { KeepAlive = true }),
                }
            });
            _router.Register("/c", typeof(ShellC), new SusRouteConfig
            {
                Children = new List<SusRouteRecord> { new SusRouteRecord("z", typeof(PaneZ)) }
            });
            _router.Register("/b", typeof(PageB));
            _router.Register("/d", typeof(PageD));
            _router.Register("/p/:id", typeof(PageP));
            _router.Register("/k1", typeof(PageK1), new SusRouteConfig { KeepAlive = true });
            _router.Register("/k2", typeof(PageK2), new SusRouteConfig { KeepAlive = true });
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _router = null;
            Log.Clear();
            Instances.Clear();
        }

        T Single<T>() where T : ProbeScreen
        {
            T found = null;
            foreach (var s in Instances)
            {
                if (s is T t)
                {
                    Assert.IsNull(found, $"expected exactly one {typeof(T).Name} instance");
                    found = t;
                }
            }
            Assert.IsNotNull(found, $"no {typeof(T).Name} instance was created");
            return found;
        }

        int IndexOf(string entry) => Log.IndexOf(entry);

        static void AssertLeftLive(ProbeScreen s)
        {
            Assert.AreEqual(1, s.LeavingCount, $"{s.GetType().Name}: OnLeaving exactly once");
            Assert.IsTrue(s.LeavingParentLive, $"{s.GetType().Name}: parent live in OnLeaving");
            Assert.IsTrue(s.LeavingPanelLive, $"{s.GetType().Name}: panel live in OnLeaving");
        }

        IEnumerator MountAt(string path)
        {
            Assert.AreEqual(NavigationResult.Success, _router.Mount(_root, path));
            yield return null;
            Assert.IsNotNull(_root.panel, "harness: root must be on a live panel");
            Log.Clear();
        }

        // ── Matrix: nested chain (shell with ChildView + child route) ─────────

        [UnityTest]
        public IEnumerator ChildToSibling_LeafLeaves_ShellStays()
        {
            yield return MountAt("/a/x");
            var x = Single<PaneX>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/a/y"));
            yield return null;

            AssertLeftLive(x);
            Assert.IsTrue(x.LeavingShellLive, "leaf still sees its shell in OnLeaving");
            Assert.AreEqual("/a/y", x.LeavingTarget.FullPath);
            Assert.AreEqual(0, Single<ShellA>().LeavingCount, "shared shell does not leave");

            // Current OnLeft behavior: called once, in the tree, after OnLeaving.
            Assert.AreEqual(1, x.LeftCount);
            Assert.IsTrue(x.LeftPanelLive);
            Assert.Less(IndexOf("Leaving:PaneX"), IndexOf("Left:PaneX"));
            Assert.Less(IndexOf("Left:PaneX"), IndexOf("Entered:PaneY"));
        }

        [UnityTest]
        public IEnumerator ChildToParent_LeafThenShellLeave_LeafToRoot()
        {
            yield return MountAt("/a/x");
            var x = Single<PaneX>();
            var shell = (ShellA)Instances.Find(s => s is ShellA);

            Assert.AreEqual(NavigationResult.Success, _router.Push("/a"));
            yield return null;

            AssertLeftLive(x);
            Assert.IsTrue(x.LeavingShellLive);
            AssertLeftLive(shell);
            Assert.Less(IndexOf("Leaving:PaneX"), IndexOf("Leaving:ShellA"), "leaf → root");
            Assert.AreNotSame(shell, _router.CurrentRoute.Value.Screen,
                "a single-level target recreates the shell");

            // Current OnLeft behavior: chain → single-level route skips OnLeft for the
            // whole outgoing chain.
            Assert.AreEqual(0, x.LeftCount, "current OnLeft behavior: leaf gets no OnLeft");
            Assert.AreEqual(0, shell.LeftCount, "current OnLeft behavior: shell gets no OnLeft");
        }

        [UnityTest]
        public IEnumerator ChildToOtherRoute_WholeChainLeaves_LeafToRoot()
        {
            yield return MountAt("/a/x");
            var x = Single<PaneX>();
            var shell = Single<ShellA>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/b"));
            yield return null;

            AssertLeftLive(x);
            Assert.IsTrue(x.LeavingShellLive);
            AssertLeftLive(shell);
            Assert.Less(IndexOf("Leaving:PaneX"), IndexOf("Leaving:ShellA"), "leaf → root");
            Assert.Less(IndexOf("Leaving:ShellA"), IndexOf("Entered:PageB"));

            // Current OnLeft behavior: chain → single-level route skips OnLeft.
            Assert.AreEqual(0, x.LeftCount, "current OnLeft behavior: leaf gets no OnLeft");
            Assert.AreEqual(0, shell.LeftCount, "current OnLeft behavior: shell gets no OnLeft");
        }

        [UnityTest]
        public IEnumerator ChildToOtherChain_WholeChainLeaves_LeftOfLeafRunsDetached()
        {
            yield return MountAt("/a/x");
            var x = Single<PaneX>();
            var shell = Single<ShellA>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/c/z"));
            yield return null;

            AssertLeftLive(x);
            AssertLeftLive(shell);
            Assert.Less(IndexOf("Leaving:PaneX"), IndexOf("Leaving:ShellA"), "leaf → root");
            Assert.Less(IndexOf("Leaving:ShellA"), IndexOf("Left:ShellA"));

            // Current OnLeft behavior: root → leaf; the shell is detached before the
            // leaf's OnLeft, so the leaf gets OnLeft without a panel.
            Assert.AreEqual(1, shell.LeftCount);
            Assert.AreEqual(1, x.LeftCount);
            Assert.IsTrue(shell.LeftPanelLive);
            Assert.IsFalse(x.LeftPanelLive, "current OnLeft behavior: leaf OnLeft runs detached");
            Assert.Less(IndexOf("Left:ShellA"), IndexOf("Left:PaneX"));
        }

        [UnityTest]
        public IEnumerator KeepAliveChildToSibling_LeafLeavesOnce_ShellStays()
        {
            yield return MountAt("/a/k");
            var k = Single<PaneK>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/a/x"));
            yield return null;

            AssertLeftLive(k);
            Assert.IsTrue(k.LeavingShellLive);
            Assert.AreEqual(0, Single<ShellA>().LeavingCount);

            // Current OnLeft behavior: a KeepAlive leaf inside a chain is swapped out of the
            // ChildView without OnLeft.
            Assert.AreEqual(0, k.LeftCount, "current OnLeft behavior: KeepAlive chain leaf gets no OnLeft");
            Assert.IsNull(k.parent, "leaf is out of the tree after the navigation");
        }

        // ── Matrix: single-level routes ──────────────────────────────────────

        [UnityTest]
        public IEnumerator SingleToSingle_LeavingBeforeLeft_BothInTree()
        {
            yield return MountAt("/b");
            var b = Single<PageB>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/d"));
            yield return null;

            AssertLeftLive(b);
            Assert.AreEqual(1, b.LeftCount);
            Assert.IsTrue(b.LeftPanelLive);
            Assert.Less(IndexOf("Leaving:PageB"), IndexOf("Left:PageB"));
        }

        [UnityTest]
        public IEnumerator SingleToChain_SingleScreenLeaves()
        {
            yield return MountAt("/b");
            var b = Single<PageB>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/a/x"));
            yield return null;

            AssertLeftLive(b);
            Assert.AreEqual(1, b.LeftCount);
            Assert.Less(IndexOf("Leaving:PageB"), IndexOf("Left:PageB"));
        }

        [UnityTest]
        public IEnumerator KeepAliveToOther_LeavesOnce_InTree_NoLeftUntilEviction()
        {
            yield return MountAt("/k1");
            var k1 = Single<PageK1>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/b"));
            yield return null;

            AssertLeftLive(k1);
            Assert.AreEqual(0, k1.LeftCount, "KeepAlive screen is cached, not torn down");
            Assert.IsNull(k1.parent, "cached screen is out of the tree");

            // Re-entry from the cache is not a leave; the next leave counts once more.
            Assert.AreEqual(NavigationResult.Success, _router.Push("/k1"));
            yield return null;
            Assert.AreEqual(1, k1.LeavingCount, "re-entry from the cache does not call OnLeaving");
            Assert.AreEqual(NavigationResult.Success, _router.Push("/b"));
            yield return null;
            Assert.AreEqual(2, k1.LeavingCount, "one OnLeaving per leave");
        }

        [UnityTest]
        public IEnumerator KeepAliveEviction_NoSecondLeaving_LeftRunsDetached()
        {
            yield return MountAt("/k1");
            var view = _root.Q<SusRouteView>(className: SusRouteView.RootUssClassName);
            Assert.IsNotNull(view);
            view.MaxKeepAlive = 1;
            var k1 = Single<PageK1>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/k2"));
            yield return null;
            AssertLeftLive(k1);

            // Leaving /k2 caches it and evicts /k1 (capacity 1).
            Assert.AreEqual(NavigationResult.Success, _router.Push("/b"));
            yield return null;

            AssertLeftLive(Single<PageK2>());
            Assert.AreEqual(1, k1.LeavingCount, "eviction does not call OnLeaving again");
            Assert.AreEqual(1, k1.LeftCount, "eviction runs OnLeft");
            Assert.IsFalse(k1.LeftPanelLive, "evicted screen gets OnLeft outside the tree");
        }

        [UnityTest]
        public IEnumerator SameRecordPropsUpdate_IsNotALeave()
        {
            yield return MountAt("/p/1");
            var p = Single<PageP>();

            Assert.AreEqual(NavigationResult.Success, _router.Push("/p/2"));
            yield return null;

            Assert.AreEqual(0, p.LeavingCount);
            Assert.AreEqual(0, p.LeftCount);
        }

        // ── Guards run first ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator BeforeLeaveRejects_NoLeaving()
        {
            yield return MountAt("/a/x");
            var shell = Single<ShellA>();
            shell.AllowLeave = false;

            Assert.AreEqual(NavigationResult.Aborted, _router.Push("/b"));
            yield return null;

            Assert.AreEqual(0, shell.LeavingCount);
            Assert.AreEqual(0, Single<PaneX>().LeavingCount);
        }

        [UnityTest]
        public IEnumerator BeforeEachRejects_NoLeaving()
        {
            yield return MountAt("/b");
            _router.BeforeEach((from, to) => to.FullPath != "/d");

            Assert.AreEqual(NavigationResult.Aborted, _router.Push("/d"));
            yield return null;

            Assert.AreEqual(0, Single<PageB>().LeavingCount);
        }

        [UnityTest]
        public IEnumerator RouteBeforeEnterRejects_NoLeaving()
        {
            _router.Register("/blocked", typeof(PageD), new SusRouteConfig
            {
                BeforeEnter = (from, to) => false
            });
            yield return MountAt("/a/x");

            Assert.AreEqual(NavigationResult.Aborted, _router.Push("/blocked"));
            yield return null;

            Assert.AreEqual(0, Single<PaneX>().LeavingCount);
            Assert.AreEqual(0, Single<ShellA>().LeavingCount);
        }

        // ── Async path ends in the same pass ─────────────────────────────────

        [UnityTest]
        public IEnumerator PushAsync_ChainToOther_LeavesOnce_LeafToRoot()
        {
            yield return MountAt("/a/x");
            var x = Single<PaneX>();
            var shell = Single<ShellA>();

            var task = _router.PushAsync("/b");
            while (!task.IsCompleted) yield return null;
            Assert.AreEqual(NavigationResult.Success, task.Result);
            yield return null;

            AssertLeftLive(x);
            AssertLeftLive(shell);
            Assert.Less(IndexOf("Leaving:PaneX"), IndexOf("Leaving:ShellA"));
        }
    }
}

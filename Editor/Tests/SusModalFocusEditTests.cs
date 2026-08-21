using NUnit.Framework;
using UnityEngine.UIElements;
using Sharq.Core;

namespace Sharq.Router.Editor.Tests
{
    /// <summary>
    /// T-677 — EditMode: SusModalService background snapshot/restore (D2).
    /// Focus trap / Focus() need a panel — covered in playmode suite.
    /// </summary>
    public class SusModalFocusEditTests
    {
        [Test]
        public void Show_SnapshotsBackground_Close_Restores()
        {
            var root = new VisualElement { name = "root" };
            var screenHost = new ScreenHost();
            var overlay = new OverlayHost { name = OverlayHost.OverlayHostName };
            root.Add(screenHost);
            root.Add(overlay);

            var bgBtn = new Button { name = "bg-btn", text = "BG", focusable = true };
            bgBtn.pickingMode = PickingMode.Position;
            screenHost.Add(bgBtn);

            var svc = new SusModalService
            {
                OverlayHost = overlay,
                Router = new SusRouter(),
            };

            Assert.IsTrue(bgBtn.focusable);
            Assert.AreEqual(PickingMode.Position, bgBtn.pickingMode);

            svc.Show(typeof(FocusProbeModal));
            Assert.AreEqual(1, svc.Count);
            Assert.IsFalse(bgBtn.focusable, "background should be non-focusable under modal");
            Assert.AreEqual(PickingMode.Ignore, bgBtn.pickingMode);

            // Second Show must NOT restore yet (snapshot stays until 1→0)
            svc.Show(typeof(FocusProbeModal));
            Assert.AreEqual(2, svc.Count);
            Assert.IsFalse(bgBtn.focusable);

            svc.Close();
            Assert.AreEqual(1, svc.Count);
            Assert.IsFalse(bgBtn.focusable, "still blocked while stack non-empty");

            svc.Close();
            Assert.AreEqual(0, svc.Count);
            Assert.IsTrue(bgBtn.focusable, "restored after 1→0");
            Assert.AreEqual(PickingMode.Position, bgBtn.pickingMode);
        }

        [Test]
        public void CloseAll_RestoresBackground()
        {
            var root = new VisualElement();
            var screenHost = new ScreenHost();
            var overlay = new OverlayHost();
            root.Add(screenHost);
            root.Add(overlay);

            var bg = new Button { name = "bg", focusable = true };
            bg.pickingMode = PickingMode.Position;
            screenHost.Add(bg);

            var svc = new SusModalService { OverlayHost = overlay, Router = new SusRouter() };
            svc.Show(typeof(FocusProbeModal));
            svc.Show(typeof(FocusProbeModal));
            Assert.IsFalse(bg.focusable);

            svc.CloseAll();
            Assert.AreEqual(0, svc.Count);
            Assert.IsTrue(bg.focusable);
            Assert.AreEqual(PickingMode.Position, bg.pickingMode);
        }

        [Test]
        public void Snapshot_DoesNotTouchModalWrapper()
        {
            var root = new VisualElement();
            var screenHost = new ScreenHost();
            var overlay = new OverlayHost();
            root.Add(screenHost);
            root.Add(overlay);

            var svc = new SusModalService { OverlayHost = overlay, Router = new SusRouter() };
            svc.Show(typeof(FocusProbeModal));

            var wrapper = overlay.Q("modal-wrapper");
            Assert.IsNotNull(wrapper);
            Assert.AreEqual(PickingMode.Position, wrapper.pickingMode,
                "modal wrapper must stay interactive");
        }

        class FocusProbeModal : SusRouterModal
        {
            protected override void Build()
            {
                Add(new Button { name = "modal-ok", text = "OK", focusable = true });
            }
        }
    }
}

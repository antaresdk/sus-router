using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Sharq.Core;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Sharq.Router.Runtime.Tests
{
    /// <summary>T-683 / ARCH-LUNA-GAP-B D11 — ShowAsync complete + dismiss without Complete.</summary>
    public class SusModalShowAsyncTests
    {
        GameObject _go;
        UIDocument _doc;
        OverlayHost _host;
        SusRouter _router;
        SusModalService _svc;

        sealed class ResultModal : SusRouterModal
        {
            protected override void Build()
            {
                Add(new Label { name = "result-modal", text = "Result" });
                var ok = new Button { name = "result-ok", text = "OK" };
                ok.clicked += () => Complete("done");
                Add(ok);
            }
        }

        sealed class ChoiceIndexModal : SusRouterModal<int>
        {
            protected override object AsyncDismissResult => -1;

            protected override void Build()
            {
                Add(new Label { name = "choice-modal", text = "Choice" });
                var pick = new Button { name = "choice-pick", text = "Pick" };
                pick.clicked += () => Complete(2);
                Add(pick);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestModalAsyncUI", typeof(UIDocument));
            _doc = _go.GetComponent<UIDocument>();
            _doc.panelSettings = SusTestPanelFactory.Create("SusTestPanelSettings_ModalAsync");
            _host = new OverlayHost();
            _doc.rootVisualElement.Add(_host);
            _router = new SusRouter();
            _svc = new SusModalService { OverlayHost = _host, Router = _router };
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.CloseAll();
            if (_go != null) Object.DestroyImmediate(_go);
            _svc = null;
            _router = null;
        }

        [UnityTest]
        public IEnumerator ShowAsync_Complete_ReturnsResult()
        {
            yield return null;
            Application.runInBackground = true;

            ResultModal captured = null;
            var task = _svc.ShowAsync<string>(() =>
            {
                captured = new ResultModal();
                return captured;
            });
            yield return null;

            Assert.IsNotNull(captured);
            Assert.AreEqual(1, _svc.Count);
            captured.Complete("done");

            yield return WaitTask(task);
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
            Assert.AreEqual("done", task.Result);
            Assert.AreEqual(0, _svc.Count);
        }

        [UnityTest]
        public IEnumerator ShowAsync_DismissWithoutComplete_ReturnsDefault()
        {
            yield return null;
            Application.runInBackground = true;

            var task = _svc.ShowAsync<string>(typeof(ResultModal));
            yield return null;
            Assert.AreEqual(1, _svc.Count);

            _svc.Close();
            yield return WaitTask(task);

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
            Assert.IsNull(task.Result);
            Assert.AreEqual(0, _svc.Count);
        }

        [UnityTest]
        public IEnumerator ShowAsync_TypedSubclass_DismissReturnsSentinel()
        {
            yield return null;
            Application.runInBackground = true;

            var task = _svc.ShowAsync<int>(typeof(ChoiceIndexModal));
            yield return null;
            _svc.Close();
            yield return WaitTask(task);

            Assert.AreEqual(-1, task.Result);
        }

        [UnityTest]
        public IEnumerator ShowAsync_Factory_CompleteTyped()
        {
            yield return null;
            Application.runInBackground = true;

            ChoiceIndexModal captured = null;
            var task = _svc.ShowAsync<int>(() =>
            {
                captured = new ChoiceIndexModal();
                return captured;
            });
            yield return null;

            Assert.IsNotNull(captured);
            captured.Complete(2);

            yield return WaitTask(task);
            Assert.AreEqual(2, task.Result);
        }

        static IEnumerator WaitTask(Task task, int maxFrames = 60)
        {
            for (int i = 0; i < maxFrames && !task.IsCompleted; i++)
                yield return null;
            Assert.IsTrue(task.IsCompleted, "async modal task did not complete in time");
        }
    }
}

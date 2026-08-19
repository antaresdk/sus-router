using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;

namespace Sharq.Router.Examples
{
    /// <summary>
    /// KeepAlive — screen caching.
    /// Demonstrates: KeepAlive = true/false, state preserved on leave.
    /// Tabs with a [K] indicator for KeepAlive screens.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class KeepAliveExample : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private SusRouter _router;
        private SimpleTabBar _navTabs;

        private void OnEnable()
        {
            try { BuildUI(); }
            catch (Exception ex)
            {
                Debug.LogError($"[KeepAlive] OnEnable failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void BuildUI()
        {
            var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
            if (doc == null) { Debug.LogError("[KeepAlive] No UIDocument found!"); return; }

            var ps = Resources.Load<PanelSettings>("PanelSettings");
            if (ps != null) doc.panelSettings = ps;

            var root = doc.rootVisualElement;
            root.style.flexGrow = 1f;
            root.style.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

            SusApp.Create(root)
                  .UseTheme(SusTheme.Dark)
                  .Configure(BuildContent)
                  .Run();
        }

        private void BuildContent(VisualElement root)
        {
            var screens = root.Q<ScreenHost>(name: ScreenHost.ScreenHostName) ?? root;
            screens.style.flexGrow = 1f;

            _navTabs = new SimpleTabBar(new[]
            {
                ("[K] Counter", "/counter"),
                ("[K] Form", "/form"),
                ("Settings", "/settings"),
            }, "/counter");
            _navTabs.Root.style.marginBottom = 8;
            screens.Add(_navTabs.Root);

            var routeSlot = new VisualElement { name = "route-slot" };
            routeSlot.style.flexGrow = 1f;
            screens.Add(routeSlot);

            _router = new SusRouter();
            _router.Init(SusBootstrap.GetOrCreateOverlay(root));

            _router.Register("/counter", typeof(CounterScreen), new SusRouteConfig { KeepAlive = true });
            _router.Register("/form", typeof(FormScreen), new SusRouteConfig { KeepAlive = true });
            _router.Register("/settings", typeof(SettingsScreen));

            _router.Mount(routeSlot, "/counter");

            _navTabs.OnChanged += path =>
            {
                _router.Push(path);
                _navTabs.SetValue(_router.CurrentRoute.Value?.Record?.Path ?? "/counter");
            };

            _router.CurrentRoute.Changed += (o, n) =>
            {
                if (n?.Record?.Path != null)
                    _navTabs.SetValue(n.Record.Path);
            };

            Debug.Log("[KeepAlive] Ready. Switch tabs to test KeepAlive.");
        }

        internal static Button MakeButton(string text)
        {
            var b = new Button { text = text };
            b.style.marginRight = 4;
            return b;
        }

        internal sealed class SimpleTabBar
        {
            public VisualElement Root { get; }
            public event Action<string> OnChanged;
            private readonly Dictionary<string, Button> _buttons = new();
            private string _value;

            public SimpleTabBar(IEnumerable<(string title, string value)> items, string initial)
            {
                Root = new VisualElement();
                Root.style.flexDirection = FlexDirection.Row;
                Root.style.flexGrow = 1f;
                _value = initial;
                foreach (var (title, value) in items)
                {
                    var path = value;
                    var btn = new Button(() =>
                    {
                        SetValue(path);
                        OnChanged?.Invoke(path);
                    }) { text = title };
                    btn.style.marginRight = 4;
                    _buttons[path] = btn;
                    Root.Add(btn);
                }
                RefreshStyles();
            }

            public void SetValue(string path)
            {
                _value = path;
                RefreshStyles();
            }

            private void RefreshStyles()
            {
                foreach (var kv in _buttons)
                {
                    var on = kv.Key == _value;
                    kv.Value.style.backgroundColor = on
                        ? new Color(0.25f, 0.45f, 0.75f)
                        : new Color(0.18f, 0.18f, 0.22f);
                    kv.Value.style.color = Color.white;
                }
            }
        }

        internal class CounterScreen : SusScreen
        {
            private int _count;
            private Label _countLabel;

            protected override void Build()
            {
                style.flexGrow = 1f;
                style.paddingTop = 32;
                style.paddingLeft = 32;
                style.paddingRight = 32;

                Add(new Label("Counter (KeepAlive)") { style = { fontSize = 24, color = Color.cyan, marginBottom = 16 } });

                _countLabel = new Label($"Count: {_count}");
                _countLabel.style.fontSize = 20;
                _countLabel.style.color = Color.white;
                _countLabel.style.marginBottom = 16;
                Add(_countLabel);

                var incBtn = MakeButton("+1");
                incBtn.clicked += () => { _count++; _countLabel.text = $"Count: {_count}"; };
                Add(incBtn);

                var resetBtn = MakeButton("Reset");
                resetBtn.style.marginTop = 8;
                resetBtn.clicked += () => { _count = 0; _countLabel.text = "Count: 0"; };
                Add(resetBtn);

                var hint = new Label("Switch to Form tab and back — count stays!");
                hint.style.color = new Color(0.5f, 0.9f, 0.5f);
                hint.style.fontSize = 14;
                hint.style.marginTop = 24;
                Add(hint);
            }

            protected override bool OnBeforeEnter(SusRoute from)
            {
                Debug.Log($"[Counter] BeforeEnter ← {from.FullPath}. Count={_count}");
                return true;
            }
        }

        internal class FormScreen : SusScreen
        {
            protected override void Build()
            {
                style.flexGrow = 1f;
                style.paddingTop = 32;
                style.paddingLeft = 32;
                style.paddingRight = 32;
                style.maxWidth = 400;

                Add(new Label("Form (KeepAlive)") { style = { fontSize = 24, color = Color.cyan, marginBottom = 16 } });

                var nameField = new TextField("Your Name");
                nameField.style.marginBottom = 8;
                Add(nameField);

                Add(new TextField("Email"));

                var hint = new Label("Type something, switch tab, come back — text preserved!");
                hint.style.color = new Color(0.5f, 0.9f, 0.5f);
                hint.style.fontSize = 14;
                hint.style.marginTop = 24;
                Add(hint);
            }

            protected override bool OnBeforeEnter(SusRoute from)
            {
                Debug.Log($"[Form] BeforeEnter ← {from.FullPath}");
                return true;
            }
        }

        internal class SettingsScreen : SusScreen
        {
            protected override void Build()
            {
                style.flexGrow = 1f;
                style.paddingTop = 32;
                style.paddingLeft = 32;
                style.paddingRight = 32;

                Add(new Label("Settings (NO KeepAlive)") { style = { fontSize = 24, color = Color.magenta, marginBottom = 16 } });

                var darkToggle = new Toggle("Dark mode");
                darkToggle.style.marginBottom = 8;
                Add(darkToggle);

                Add(new Toggle("Notifications"));

                var hint = new Label("Switch tab and come back — state RESETS!");
                hint.style.color = new Color(1f, 0.4f, 0.4f);
                hint.style.fontSize = 14;
                hint.style.marginTop = 24;
                Add(hint);
            }

            protected override bool OnBeforeEnter(SusRoute from)
            {
                Debug.Log($"[Settings] BeforeEnter ← {from.FullPath} (no KeepAlive — always fresh)");
                return true;
            }
        }
    }
}

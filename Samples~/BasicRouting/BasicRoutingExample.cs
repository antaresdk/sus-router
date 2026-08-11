using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;

/// <summary>
/// Basic Routing — simple navigation with a UITK tab bar.
/// Demonstrates: Push, Replace, Back, Home.
/// Navbar: tab buttons (4) + Back + CurrentRoute chip.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class BasicRoutingExample : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private SusRouter _router;
    private VisualElement _navBar;
    private Label _routeChip;
    private Button _backBtn;
    private SimpleTabBar _navTabs;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (Exception ex)
        {
            Debug.LogError($"[BasicRouting] OnEnable failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void BuildUI()
    {
        var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
        if (doc == null) { Debug.LogError("[BasicRouting] No UIDocument found!"); return; }

        var ps = Resources.Load<PanelSettings>("PanelSettings");
        if (ps != null) doc.panelSettings = ps;

        var root = doc.rootVisualElement;
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

        _navBar = new VisualElement();
        _navBar.style.flexDirection = FlexDirection.Row;
        _navBar.style.alignItems = Align.Center;
        _navBar.style.paddingTop = 8;
        _navBar.style.paddingBottom = 8;
        _navBar.style.paddingLeft = 16;
        _navBar.style.paddingRight = 16;
        _navBar.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        _navBar.style.flexShrink = 0;

        _backBtn = MakeButton("<- Back");

        _navTabs = new SimpleTabBar(new[]
        {
            ("Home", "/home"),
            ("About", "/about"),
            ("Contact", "/contact"),
            ("Settings", "/settings"),
        }, "/home");

        _routeChip = MakeChip("/");

        _navBar.Add(_backBtn);
        _navBar.Add(_navTabs.Root);
        _navBar.Add(_routeChip);
        screens.Add(_navBar);

        var routeSlot = new VisualElement { name = "route-slot" };
        routeSlot.style.flexGrow = 1f;
        screens.Add(routeSlot);

        _router = new SusRouter();
        _router.Init(SusBootstrap.GetOrCreateOverlay(root));

        _router.Register("/home", typeof(HomeScreen));
        _router.Register("/about", typeof(AboutScreen));
        _router.Register("/contact", typeof(ContactScreen));
        _router.Register("/settings", typeof(SettingsScreen));

        _router.Mount(routeSlot, "/home");

        _navTabs.OnChanged += path =>
        {
            _router.Push(path);
            _navTabs.SetValue(_router.CurrentRoute.Value?.Record?.Path ?? "/home");
        };
        _backBtn.clicked += () =>
        {
            _router.Back();
            UpdateBackButton();
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            _routeChip.text = n?.FullPath ?? "/";
            if (n?.Record?.Path != null)
                _navTabs.SetValue(n.Record.Path);
            UpdateBackButton();
        };

        UpdateBackButton();
    }

    private void UpdateBackButton() =>
        _backBtn.style.display = (_router?.CanGoBack ?? false)
            ? DisplayStyle.Flex : DisplayStyle.None;

    internal static Button MakeButton(string text)
    {
        var b = new Button { text = text };
        b.style.marginRight = 4;
        return b;
    }

    internal static Label MakeChip(string text)
    {
        var l = new Label(text);
        l.style.backgroundColor = new Color(0.25f, 0.25f, 0.32f);
        l.style.color = Color.white;
        l.style.fontSize = 12;
        l.style.paddingLeft = 8;
        l.style.paddingRight = 8;
        l.style.paddingTop = 4;
        l.style.paddingBottom = 4;
        l.style.marginLeft = 8;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        return l;
    }

    /// <summary>Minimal UITK tab strip (Button row) for router samples.</summary>
    internal sealed class SimpleTabBar
    {
        public VisualElement Root { get; }
        public event Action<string> OnChanged;

        private readonly Dictionary<string, Button> _buttons = new();
        private string _value;

        public SimpleTabBar(IEnumerable<(string title, string value)> items, string initial,
            bool vertical = false)
        {
            Root = new VisualElement();
            Root.style.flexDirection = vertical ? FlexDirection.Column : FlexDirection.Row;
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
                btn.style.marginRight = vertical ? 0 : 4;
                btn.style.marginBottom = vertical ? 4 : 0;
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

    internal class HomeScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label("Home") { style = { fontSize = 28, color = Color.white, marginBottom = 16 } });

            var btn1 = MakeButton("Go to About");
            btn1.clicked += () => Router.Push("/about");
            Add(btn1);

            var btn2 = MakeButton("Go to Contact");
            btn2.style.marginTop = 8;
            btn2.clicked += () => Router.Push("/contact");
            Add(btn2);

            var btn3 = MakeButton("Replace with Settings");
            btn3.style.marginTop = 8;
            btn3.clicked += () => Router.Replace("/settings");
            Add(btn3);

            var chip = MakeChip("Home");
            chip.style.marginTop = 24;
            chip.style.marginLeft = 0;
            Add(chip);
        }
    }

    internal class AboutScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label("About Us") { style = { fontSize = 28, color = Color.white, marginBottom = 16 } });

            var link = new SusRouteLink { To = "/home", Exact = true };
            link.Bind(Router);
            var linkLabel = new Label("<- Back to Home");
            linkLabel.style.color = new Color(0.3f, 0.6f, 1f);
            linkLabel.style.fontSize = 16;
            link.Add(linkLabel);
            Add(link);

            var placeholder = new VisualElement();
            placeholder.style.width = 120;
            placeholder.style.height = 80;
            placeholder.style.marginTop = 16;
            placeholder.style.backgroundColor = new Color(0.2f, 0.22f, 0.28f);
            Add(placeholder);
            Add(new Label("(image placeholder)")
            {
                style = { color = new Color(0.5f, 0.5f, 0.55f), fontSize = 12, marginTop = 4 }
            });

            Add(new Label("About this app")
            {
                style = { color = new Color(0.6f, 0.6f, 0.7f), fontSize = 14, marginTop = 16 }
            });
        }
    }

    internal class ContactScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;
            style.maxWidth = 400;

            Add(new Label("Contact") { style = { fontSize = 28, color = Color.white, marginBottom = 16 } });

            var nameField = new TextField("Name");
            nameField.style.marginBottom = 8;
            Add(nameField);

            var emailField = new TextField("Email");
            emailField.style.marginBottom = 16;
            Add(emailField);

            var submitBtn = MakeButton("Submit");
            submitBtn.clicked += () => Debug.Log("[Contact] Submitted");
            Add(submitBtn);
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

            Add(new Label("Settings") { style = { fontSize = 28, color = Color.white, marginBottom = 16 } });

            var darkToggle = new Toggle("Dark theme");
            darkToggle.style.marginBottom = 8;
            Add(darkToggle);

            Add(new Toggle("Notifications"));
        }
    }
}

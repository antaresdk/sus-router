using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;

/// <summary>
/// Full Demo — ALL SusRouter features in one sample.
/// - Sidebar with vertical UITK tab buttons (Push — history works)
/// - 5 screens: Dashboard, Users, UserDetail, Settings, About
/// - KeepAlive, guards, modals, transitions, named routes, nested routes
/// - Theming via SusThemeService (reactive, whole UI responds)
/// - ScrollView on the content area
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class FullDemoExample : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private SusRouter _router;
    private Label _statusChip;
    private Label _statsChip;
    private Toggle _themeToggle;
    private VisualElement _root;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (Exception ex)
        {
            Debug.LogError($"[FullDemo] OnEnable failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void BuildUI()
    {
        var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
        if (doc == null) { Debug.LogError("[FullDemo] No UIDocument found!"); return; }

        var ps = Resources.Load<PanelSettings>("PanelSettings");
        if (ps != null) doc.panelSettings = ps;

        _root = doc.rootVisualElement;

        SusApp.Create(_root)
              .UseTheme(SusTheme.Dark)
              .UseCustomStyles("SusRuntime/_global")
              .Configure(_ => BuildContent())
              .Run();
    }

    private void BuildContent()
    {
        var screens = _root.Q<ScreenHost>(name: ScreenHost.ScreenHostName) ?? _root;
        screens.style.flexGrow = 1f;

        var mainRow = new VisualElement();
        mainRow.style.flexGrow = 1f;
        mainRow.style.flexDirection = FlexDirection.Row;
        screens.Add(mainRow);

        var sidebar = new VisualElement();
        sidebar.AddToClassList("demo-sidebar");
        sidebar.style.width = 200;
        sidebar.style.flexShrink = 0;
        sidebar.style.paddingTop = 16;
        sidebar.style.paddingLeft = 12;
        sidebar.style.paddingRight = 12;
        ApplyThemeBg(sidebar, "#0f0f14", "#f0f0f5");

        var logo = new Label("SUS Demo");
        logo.AddToClassList("demo-logo");
        logo.style.fontSize = 18;
        logo.style.unityFontStyleAndWeight = FontStyle.Bold;
        logo.style.marginBottom = 16;
        ApplyThemeFg(logo, "#e6e6f0", "#1a1b23");
        sidebar.Add(logo);

        var navTabs = new SimpleTabBar(new[]
        {
            ("Dashboard", "/dashboard"),
            ("Users", "/users"),
            ("Settings", "/settings"),
            ("About", "/about"),
        }, "/dashboard", vertical: true);
        sidebar.Add(navTabs.Root);

        var actionSection = new Label("Actions");
        actionSection.style.fontSize = 11;
        actionSection.style.marginTop = 24;
        actionSection.style.marginBottom = 8;
        ApplyThemeFg(actionSection, "#606070", "#808090");
        sidebar.Add(actionSection);

        var modalBtn = MakeButton("Open Modal");
        modalBtn.clicked += () =>
        {
            _router.ModalService?.Show(typeof(AboutDialog),
                new() { ["title"] = "About", ["message"] = "SusRouter Full Demo v0.2" });
        };
        sidebar.Add(modalBtn);

        var themeLabel = new Label("Theme");
        themeLabel.style.fontSize = 11;
        themeLabel.style.marginTop = 16;
        themeLabel.style.marginBottom = 4;
        ApplyThemeFg(themeLabel, "#606070", "#808090");
        sidebar.Add(themeLabel);

        _themeToggle = new Toggle("Dark") { value = true };
        _themeToggle.RegisterValueChangedCallback(evt =>
        {
            SusThemeService.Instance.SetTheme(_root, evt.newValue ? SusTheme.Dark : SusTheme.Light);
        });
        sidebar.Add(_themeToggle);

        var statsLabel = new Label("Stats");
        statsLabel.style.fontSize = 11;
        statsLabel.style.marginTop = 16;
        statsLabel.style.marginBottom = 4;
        ApplyThemeFg(statsLabel, "#606070", "#808090");
        sidebar.Add(statsLabel);

        _statsChip = MakeChip("Routes: 5  Guards: 2");
        sidebar.Add(_statsChip);

        _statusChip = MakeChip("/dashboard");
        _statusChip.style.marginTop = 4;
        sidebar.Add(_statusChip);

        var backFwdRow = new VisualElement();
        backFwdRow.style.flexDirection = FlexDirection.Row;
        backFwdRow.style.marginTop = 12;

        var backBtn = MakeButton("←");
        backBtn.clicked += () =>
        {
            var result = _router.Back();
            Debug.Log($"[FullDemo] Back result: {result}");
            UpdateChip();
        };
        backFwdRow.Add(backBtn);

        var fwdBtn = MakeButton("→");
        fwdBtn.style.marginLeft = 4;
        fwdBtn.clicked += () =>
        {
            var result = _router.Forward();
            Debug.Log($"[FullDemo] Forward result: {result}");
            UpdateChip();
        };
        backFwdRow.Add(fwdBtn);

        sidebar.Add(backFwdRow);
        mainRow.Add(sidebar);

        var contentScroll = new ScrollView();
        contentScroll.style.flexGrow = 1f;
        contentScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        mainRow.Add(contentScroll);

        var content = new VisualElement();
        content.style.flexGrow = 1f;
        content.name = "router-content";
        contentScroll.Add(content);

        var overlayHost = SusBootstrap.GetOrCreateOverlay(_root);

        _router = new SusRouter();
        _router.Init(overlayHost);

        _router.BeforeEach((from, to) =>
        {
            Debug.Log($"[FullDemo beforeEach] {from.FullPath} → {to.FullPath}");
            return true;
        });

        _router.AfterEach((from, to) =>
        {
            Debug.Log($"[FullDemo afterEach] Done: {to.FullPath}");
        });

        _router.Register("/dashboard", typeof(DashboardScreen), new SusRouteConfig
        {
            KeepAlive = true,
            Transition = SusRouteTransition.Fade()
        });
        _router.Register("/users", typeof(UsersScreen), new SusRouteConfig
        {
            Name = "users",
            KeepAlive = true,
            Children = new List<SusRouteRecord>
            {
                new SusRouteRecord(":id", typeof(UserDetailScreen), new SusRouteConfig
                {
                    Name = "user-detail",
                    Guard = new UserDetailGuard()
                })
            }
        });
        _router.Register("/settings", typeof(SettingsScreen), new SusRouteConfig
        {
            Transition = SusRouteTransition.Fade()
        });
        _router.Register("/about", typeof(AboutScreen));

        _router.Mount(content, "/dashboard");

        navTabs.OnChanged += path =>
        {
            _router.Push(path);
            navTabs.SetValue(_router.CurrentRoute.Value?.Record?.Path ?? "/dashboard");
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            if (n?.Record?.Path != null)
                navTabs.SetValue(n.Record.Path);
            UpdateChip();
        };

        UpdateChip();
        Debug.Log("[FullDemo] Ready. All features active.");
    }

    private void UpdateChip()
    {
        var route = _router?.CurrentRoute?.Value;
        _statusChip.text = route?.FullPath ?? "/";
        _statsChip.text = $"Routes: {_router?.RouteCount ?? 0}  " +
            $"History: {_router?.History.Count ?? 0}  " +
            $"CanBack: {(_router?.CanGoBack ?? false)}";
    }

    private void ApplyThemeBg(VisualElement el, string darkHex, string lightHex)
    {
        ColorUtility.TryParseHtmlString(darkHex, out var dark);
        ColorUtility.TryParseHtmlString(lightHex, out var light);
        el.style.backgroundColor = SusThemeService.Current.Value == SusTheme.Dark ? dark : light;
        SusThemeService.Current.Changed += (_, next) =>
            el.style.backgroundColor = next == SusTheme.Dark ? dark : light;
    }

    private void ApplyThemeFg(VisualElement el, string darkHex, string lightHex)
    {
        ColorUtility.TryParseHtmlString(darkHex, out var dark);
        ColorUtility.TryParseHtmlString(lightHex, out var light);
        el.style.color = SusThemeService.Current.Value == SusTheme.Dark ? dark : light;
        SusThemeService.Current.Changed += (_, next) =>
            el.style.color = next == SusTheme.Dark ? dark : light;
    }

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
        l.style.fontSize = 11;
        l.style.paddingLeft = 8;
        l.style.paddingRight = 8;
        l.style.paddingTop = 4;
        l.style.paddingBottom = 4;
        l.style.whiteSpace = WhiteSpace.Normal;
        return l;
    }

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
                btn.style.width = vertical ? Length.Percent(100) : StyleKeyword.Auto;
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

    private class UserDetailGuard : ISusRouteGuard
    {
        public bool CanEnter(SusRoute from, SusRoute to)
        {
            Debug.Log($"[UserDetailGuard] CanEnter: {from.FullPath} → {to.FullPath}");
            return true;
        }

        public bool CanLeave(SusRoute from, SusRoute to)
        {
            Debug.Log($"[UserDetailGuard] CanLeave: {from.FullPath} → {to.FullPath}");
            return true;
        }
    }

    private static Color ThemeBg => SusThemeService.Current.Value == SusTheme.Dark
        ? new Color(0.12f, 0.13f, 0.18f)
        : new Color(0.95f, 0.95f, 0.98f);

    private static Color ThemeText => SusThemeService.Current.Value == SusTheme.Dark
        ? new Color(0.9f, 0.9f, 0.95f)
        : new Color(0.10f, 0.10f, 0.14f);

    private static Color ThemeSubtext => SusThemeService.Current.Value == SusTheme.Dark
        ? new Color(0.6f, 0.6f, 0.7f)
        : new Color(0.4f, 0.4f, 0.5f);

    private static Color ThemeCardBg => SusThemeService.Current.Value == SusTheme.Dark
        ? new Color(0.15f, 0.15f, 0.22f)
        : new Color(0.88f, 0.88f, 0.94f);

    private static void MarkThemeAware(VisualElement el)
    {
        el.style.backgroundColor = ThemeBg;
        SusThemeService.Current.Changed += (_, __) =>
        {
            el.style.backgroundColor = ThemeBg;
        };
    }

    internal class DashboardScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 24;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            MarkThemeAware(this);

            var title = new Label("Dashboard");
            title.style.fontSize = 24;
            title.style.marginBottom = 16;
            title.style.color = ThemeText;
            SusThemeService.Current.Changed += (_, __) => title.style.color = ThemeText;
            Add(title);

            var cardsRow = new VisualElement();
            cardsRow.style.flexDirection = FlexDirection.Row;

            AddCard(cardsRow, "Users", "128", Color.cyan);
            AddCard(cardsRow, "Battles", "42", Color.green);
            AddCard(cardsRow, "Online", "3", new Color(1f, 0.9f, 0.3f));

            Add(cardsRow);

            var statusChip = MakeChip("KeepAlive active");
            statusChip.style.marginTop = 16;
            Add(statusChip);
        }

        private void AddCard(VisualElement row, string title, string value, Color color)
        {
            var card = new VisualElement();
            card.style.width = 120;
            card.style.height = 80;
            card.style.backgroundColor = ThemeCardBg;
            card.style.marginRight = 12;
            card.style.paddingTop = 12;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.alignItems = Align.Center;
            SusThemeService.Current.Changed += (_, __) =>
                card.style.backgroundColor = ThemeCardBg;

            var valLabel = new Label(value);
            valLabel.style.fontSize = 28;
            valLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valLabel.style.color = color;
            card.Add(valLabel);

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.color = ThemeSubtext;
            SusThemeService.Current.Changed += (_, __) =>
                titleLabel.style.color = ThemeSubtext;
            card.Add(titleLabel);

            row.Add(card);
        }

        protected override bool OnBeforeEnter(SusRoute from)
        {
            Debug.Log($"[Dashboard] BeforeEnter ← {from.FullPath}");
            return true;
        }
    }

    internal class UsersScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 24;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            MarkThemeAware(this);

            var title = new Label("Users");
            title.style.fontSize = 24;
            title.style.marginBottom = 16;
            title.style.color = ThemeText;
            SusThemeService.Current.Changed += (_, __) => title.style.color = ThemeText;
            Add(title);

            var users = new[] { "Alice (ID:1)", "Bob (ID:2)", "Charlie (ID:42)" };
            foreach (var user in users)
            {
                var chip = MakeChip(user);
                chip.style.marginBottom = 8;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    var id = user.Split('(')[1].TrimEnd(')').Split(':')[1].Trim();
                    Router.Push($"/users/{id}");
                });
                Add(chip);
            }

            var keepAliveChip = MakeChip("KeepAlive active");
            keepAliveChip.style.marginTop = 16;
            Add(keepAliveChip);
        }

        protected override bool OnBeforeEnter(SusRoute from)
        {
            Debug.Log($"[Users] BeforeEnter ← {from.FullPath}");
            return true;
        }
    }

    internal class UserDetailScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 24;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            style.maxWidth = 400;
            MarkThemeAware(this);

            var id = GetParam("id", "?");
            var title = new Label($"User #{id}");
            title.style.fontSize = 24;
            title.style.color = Color.cyan;
            title.style.marginBottom = 16;
            Add(title);

            var nameField = new TextField("Name") { value = $"User {id}" };
            nameField.style.marginBottom = 8;
            Add(nameField);

            var roleField = new TextField("Role") { value = "Member" };
            roleField.style.marginBottom = 8;
            Add(roleField);

            var activeToggle = new Toggle("Active") { value = true };
            activeToggle.style.marginBottom = 16;
            Add(activeToggle);

            var saveBtn = MakeButton("Save");
            saveBtn.clicked += () => Debug.Log($"[UserDetail] Saved user #{id}");
            Add(saveBtn);

            var backBtn = MakeButton("Back to Users");
            backBtn.style.marginTop = 8;
            backBtn.clicked += () => Router.Push("/users");
            Add(backBtn);
        }

        protected override bool OnBeforeEnter(SusRoute from)
        {
            Debug.Log($"[UserDetail] BeforeEnter ← {from.FullPath}. id={GetParam("id")}");
            return true;
        }

        protected override bool OnBeforeLeave(SusRoute to)
        {
            Debug.Log($"[UserDetail] BeforeLeave → {to.FullPath}");
            return true;
        }
    }

    internal class SettingsScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 24;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            style.maxWidth = 400;
            MarkThemeAware(this);

            var title = new Label("Settings");
            title.style.fontSize = 24;
            title.style.marginBottom = 16;
            title.style.color = ThemeText;
            SusThemeService.Current.Changed += (_, __) => title.style.color = ThemeText;
            Add(title);

            var darkToggle = new Toggle("Dark theme") { value = true };
            darkToggle.style.marginBottom = 8;
            Add(darkToggle);

            var notifToggle = new Toggle("Notifications") { value = true };
            notifToggle.style.marginBottom = 8;
            Add(notifToggle);

            Add(new Toggle("Sound effects") { value = true });
        }

        protected override bool OnBeforeEnter(SusRoute from)
        {
            Debug.Log($"[Settings] BeforeEnter ← {from.FullPath}");
            return true;
        }
    }

    internal class AboutScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 24;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            MarkThemeAware(this);

            var title = new Label("About");
            title.style.fontSize = 24;
            title.style.marginBottom = 16;
            title.style.color = ThemeText;
            SusThemeService.Current.Changed += (_, __) => title.style.color = ThemeText;
            Add(title);

            Add(MakeChip("SusRouter v0.2.17"));

            var aboutText = new Label(
                "Vue Router-like navigation for Unity UI Toolkit.\n\n" +
                "Features: Push/Replace/Back/Forward, Named/Nested/KeepAlive routes,\n" +
                "Guard pipeline (beforeEach/CanEnter/CanLeave/BeforeResolve),\n" +
                "Modals, Transitions, Query params, Lazy loading.");
            aboutText.style.color = ThemeSubtext;
            aboutText.style.fontSize = 14;
            aboutText.style.marginTop = 16;
            aboutText.style.whiteSpace = WhiteSpace.Normal;
            SusThemeService.Current.Changed += (_, __) => aboutText.style.color = ThemeSubtext;
            Add(aboutText);
        }
    }

    internal class AboutDialog : SusRouterModal
    {
        private Label _titleLabel;
        private Label _msgLabel;

        protected override void Build()
        {
            style.width = 340;
            style.height = 200;
            style.backgroundColor = new Color(0.15f, 0.2f, 0.3f, 0.95f);
            AddToClassList("modal-dlg-rounded");
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.flexDirection = FlexDirection.Column;

            _titleLabel = new Label("...");
            _titleLabel.style.fontSize = 20;
            _titleLabel.style.color = Color.white;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 12;
            Add(_titleLabel);

            _msgLabel = new Label("...");
            _msgLabel.style.fontSize = 14;
            _msgLabel.style.color = new Color(0.7f, 0.7f, 0.75f);
            _msgLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _msgLabel.style.marginBottom = 20;
            Add(_msgLabel);

            var closeBtn = MakeButton("Close");
            closeBtn.clicked += () => Dismiss();
            Add(closeBtn);
        }

        protected override void Shown()
        {
            var title = Props.TryGetValue("title", out var t) ? t?.ToString() : "About";
            var msg = Props.TryGetValue("message", out var m) ? m?.ToString() : "";
            _titleLabel.text = title;
            _msgLabel.text = msg;
            Debug.Log($"[AboutDialog] Shown: title='{title}' msg='{msg}'");
        }
    }
}

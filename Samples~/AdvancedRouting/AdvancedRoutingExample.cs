using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;

/// <summary>
/// Nested & Named Routes — nested routes, PushNamed, params, query, alias, redirect.
/// Demonstrates: nested children, PushNamed, :id, ?q=, alias, redirect, lazy loading.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AdvancedRoutingExample : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private SusRouter _router;
    private SimpleTabBar _navTabs;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (Exception ex)
        {
            Debug.LogError($"[Nested] OnEnable failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void BuildUI()
    {
        var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
        if (doc == null) { Debug.LogError("[Nested] No UIDocument found!"); return; }

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

        var navBar = new VisualElement();
        navBar.style.flexDirection = FlexDirection.Row;
        navBar.style.alignItems = Align.Center;
        navBar.style.paddingTop = 8;
        navBar.style.paddingBottom = 8;
        navBar.style.paddingLeft = 16;
        navBar.style.paddingRight = 16;
        navBar.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f);

        _navTabs = new SimpleTabBar(new[]
        {
            ("Menu", "/main-menu"),
            ("Battle#42", "/battle/42"),
            ("Settings", "/settings"),
            ("Search", "/search"),
            ("Lazy", "/lazy"),
        }, "/main-menu");
        navBar.Add(_navTabs.Root);

        var backBtn = MakeButton("<- Back");
        backBtn.style.marginLeft = 8;
        backBtn.clicked += () => Debug.Log($"[Router] Back → {_router.Back()}");
        navBar.Add(backBtn);

        screens.Add(navBar);

        var routeSlot = new VisualElement { name = "route-slot" };
        routeSlot.style.flexGrow = 1f;
        screens.Add(routeSlot);

        _router = new SusRouter();
        _router.Init(SusBootstrap.GetOrCreateOverlay(root));

        _router.Register("/battle/:id", typeof(BattleScreen), new SusRouteConfig
        {
            Name = "battle",
            Transition = SusRouteTransition.SlideLeft()
        });

        _router.Register("/main-menu", typeof(MenuScreen), new SusRouteConfig
        {
            Alias = new List<string> { "/menu" }
        });

        _router.Register("/old-menu", typeof(MenuScreen), new SusRouteConfig
        {
            Redirect = "/main-menu"
        });

        _router.Register("/settings", typeof(SettingsScreen), new SusRouteConfig
        {
            Name = "settings",
            Children = new List<SusRouteRecord>
            {
                new SusRouteRecord("profile", typeof(LabelScreen), new SusRouteConfig { Name = "profile" }),
                new SusRouteRecord("privacy", typeof(LabelScreen), new SusRouteConfig { Name = "privacy" })
            }
        });

        _router.Register("/search", typeof(SearchScreen));

        _router.Register("/lazy", null, new SusRouteConfig
        {
            LazyFactory = () =>
            {
                Debug.Log("[LazyFactory] Creating screen on first access");
                return new LabelScreen { LabelText = "Lazy Screen (loaded on demand)" };
            }
        });

        _router.BeforeEach((from, to) =>
        {
            Debug.Log($"[beforeEach] {from.FullPath} → {to.FullPath}");
            return true;
        });

        _router.Mount(routeSlot, "/main-menu");

        _navTabs.OnChanged += path =>
        {
            if (path == "/battle/42")
                _router.PushNamed("battle", new() { ["id"] = "42" });
            else if (path == "/settings")
                _router.PushNamed("settings");
            else
                _router.Push(path);
            _navTabs.SetValue(_router.CurrentRoute.Value?.Record?.Path ?? "/main-menu");
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            if (n?.Record?.Path != null)
                _navTabs.SetValue(n.Record.Path);

            if (n?.Query?.Count > 0)
            {
                Debug.Log($"[Router] Query params: q={n.Query.GetValueOrDefault("q")}, " +
                    $"page={n.Query.GetValueOrDefault("page")}");
            }
        };

        Debug.Log("[Nested] Ready. Tab navigation with named/nested/query/lazy routes.");
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
        l.style.fontSize = 12;
        l.style.paddingLeft = 8;
        l.style.paddingRight = 8;
        l.style.paddingTop = 4;
        l.style.paddingBottom = 4;
        l.style.marginBottom = 8;
        return l;
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

    internal class MenuScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label("Main Menu") { style = { fontSize = 28, color = Color.green, marginBottom = 16 } });
            Add(new Label("(alias: /menu → /main-menu)")
            {
                style = { color = new Color(0.6f, 0.6f, 0.7f), fontSize = 14, marginBottom = 8 }
            });
            Add(new Label("(redirect: /old-menu → /main-menu)")
            {
                style = { color = new Color(0.6f, 0.6f, 0.7f), fontSize = 14 }
            });
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

            var childTabs = new SimpleTabBar(new[]
            {
                ("Profile", "/settings/profile"),
                ("Privacy", "/settings/privacy"),
            }, "/settings/profile");
            childTabs.OnChanged += p =>
            {
                Router.Push(p);
                childTabs.SetValue(Router.CurrentRoute.Value?.Record?.Path ?? "/settings/profile");
            };
            Add(childTabs.Root);

            var childLabel = new Label("(nested child route content here)");
            childLabel.style.color = new Color(0.6f, 0.6f, 0.7f);
            childLabel.style.fontSize = 14;
            childLabel.style.marginTop = 16;
            Add(childLabel);
        }
    }

    internal class BattleScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            var id = GetParam("id", "?");
            var mode = GetProp("mode", "?");

            Add(new Label($"Battle #{id}") { style = { fontSize = 24, color = Color.red, marginBottom = 16 } });
            Add(new Label($"mode: {mode}") { style = { color = new Color(0.6f, 0.6f, 0.7f), fontSize = 16 } });

            var namedBtn = MakeButton("PushNamed(\"battle\", id=99)");
            namedBtn.clicked += () =>
                Router.PushNamed("battle", new() { ["id"] = "99" });
            namedBtn.style.marginTop = 16;
            Add(namedBtn);
        }

        protected override bool OnBeforeEnter(SusRoute from)
        {
            Debug.Log($"[Battle] BeforeEnter ← {from.FullPath}. id={GetParam("id")}");
            return true;
        }
    }

    internal class SearchScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label("Search") { style = { fontSize = 28, color = Color.cyan, marginBottom = 16 } });

            var q = GetQuery("q", "(none)");
            var page = GetQuery("page", "(none)");

            Add(MakeChip($"q={q}"));
            Add(MakeChip($"page={page}"));

            var searchBtn = MakeButton("Push /search?q=hello&page=1");
            searchBtn.style.marginTop = 16;
            searchBtn.clicked += () => Router.Push("/search?q=hello&page=1");
            Add(searchBtn);
        }
    }

    internal class LabelScreen : SusScreen
    {
        public string LabelText = "~ Screen ~";

        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label(LabelText)
            {
                style = { fontSize = 24, color = Color.white, unityTextAlign = TextAnchor.MiddleCenter }
            });
        }
    }
}

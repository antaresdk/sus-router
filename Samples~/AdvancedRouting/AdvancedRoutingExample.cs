using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;
using Sharq.Kit;
using TabItem = SusTabs.TabItem;

/// <summary>
/// Nested & Named Routes — nested routes, PushNamed, params, query, alias, redirect.
/// Demonstrates: nested children, PushNamed, :id, ?q=, alias, redirect, lazy loading.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AdvancedRoutingExample : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private SusRouter _router;
    private SusTabs _navTabs;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (System.Exception ex)
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

        // Unified bootstrap: EventSystem + token cascade, then BuildContent (nav + router
        // mount + overlay), then theme applied LAST so the OverlayHost inherits it.
        SusApp.Create(root)
              .UseTheme(SusTheme.Dark)
              .Configure(BuildContent)
              .Run();
    }

    private void BuildContent(VisualElement root)
    {
        // ── Navbar ──
        var navBar = new VisualElement();
        navBar.style.flexDirection = FlexDirection.Row;
        navBar.style.alignItems = Align.Center;
        navBar.style.paddingTop = 8;
        navBar.style.paddingBottom = 8;
        navBar.style.paddingLeft = 16;
        navBar.style.paddingRight = 16;
        navBar.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f);

        _navTabs = new SusTabs();
        _navTabs.Items.Value = new List<TabItem>
        {
            new() { Title = "Menu", Value = "/main-menu" },
            new() { Title = "Battle#42", Value = "/battle/42" },
            new() { Title = "Settings", Value = "/settings" },
            new() { Title = "Search", Value = "/search" },
            new() { Title = "Lazy", Value = "/lazy" },
        };
        _navTabs.Model.Value = "/main-menu";
        navBar.Add(_navTabs);

        var backBtn = new SusButton();
        backBtn.Text.Value = "<- Back";
        backBtn.style.marginLeft = 8;
        backBtn.RegisterCallback<ClickEvent>(_ =>
        {
            Debug.Log($"[Router] Back → {_router.Back()}");
        });
        navBar.Add(backBtn);

        root.Add(navBar);

        // ── Router ──
        _router = new SusRouter();

        // Named route
        _router.Register("/battle/:id", typeof(BattleScreen), new SusRouteConfig
        {
            Name = "battle",
            Transition = SusRouteTransition.SlideLeft()
        });

        // Alias
        _router.Register("/main-menu", typeof(MenuScreen), new SusRouteConfig
        {
            Alias = new List<string> { "/menu" }
        });

        // Redirect
        _router.Register("/old-menu", typeof(MenuScreen), new SusRouteConfig
        {
            Redirect = "/main-menu"
        });

        // Nested
        _router.Register("/settings", typeof(SettingsScreen), new SusRouteConfig
        {
            Name = "settings",
            Children = new List<SusRouteRecord>
            {
                new SusRouteRecord("profile", typeof(LabelScreen), new SusRouteConfig { Name = "profile" }),
                new SusRouteRecord("privacy", typeof(LabelScreen), new SusRouteConfig { Name = "privacy" })
            }
        });

        // Query params
        _router.Register("/search", typeof(SearchScreen));

        // Lazy loading
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

        _router.Mount(root, "/main-menu");
        // Mount() already calls Init() internally (guarded). No need for explicit Init().

        _navTabs.OnTabChanged += path =>
        {
            if (path == "/battle/42")
                _router.PushNamed("battle", new() { ["id"] = "42" });
            else if (path == "/settings")
                _router.PushNamed("settings");
            else
                _router.Push(path);
            _navTabs.Model.Value = _router.CurrentRoute.Value?.Record?.Path ?? "/main-menu";
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            if (n?.Record?.Path != null)
                _navTabs.Model.Value = n.Record.Path;

            if (n?.Query?.Count > 0)
            {
                Debug.Log($"[Router] Query params: q={n.Query.GetValueOrDefault("q")}, " +
                    $"page={n.Query.GetValueOrDefault("page")}");
            }
        };

        Debug.Log("[Nested] Ready. Tab navigation with named/nested/query/lazy routes.");
    }

    // ════════════════════════════════════════════════════════════════
    //  Screens
    // ════════════════════════════════════════════════════════════════

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

            // Nested child tabs
            var childTabs = new SusTabs();
            childTabs.Items.Value = new List<TabItem>
            {
                new() { Title = "Profile", Value = "/settings/profile" },
                new() { Title = "Privacy", Value = "/settings/privacy" },
            };
            childTabs.Model.Value = "/settings/profile";
            childTabs.OnTabChanged += p =>
            {
                Router.Push(p);
                childTabs.Model.Value = Router.CurrentRoute.Value?.Record?.Path ?? "/settings/profile";
            };
            Add(childTabs);

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

            // PushNamed example button
            var namedBtn = new SusButton();
            namedBtn.Text.Value = "PushNamed(\"battle\", id=99)";
            namedBtn.Variant.Value = "primary";
            namedBtn.RegisterCallback<ClickEvent>(_ =>
                Router.PushNamed("battle", new() { ["id"] = "99" }));
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

            var qChip = new SusChip();
            qChip.Label.Value = $"q={q}";
            qChip.style.marginBottom = 8;
            Add(qChip);

            var pageChip = new SusChip();
            pageChip.Label.Value = $"page={page}";
            Add(pageChip);

            var searchBtn = new SusButton();
            searchBtn.Text.Value = "Push /search?q=hello&page=1";
            searchBtn.Variant.Value = "primary";
            searchBtn.style.marginTop = 16;
            searchBtn.RegisterCallback<ClickEvent>(_ => Router.Push("/search?q=hello&page=1"));
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

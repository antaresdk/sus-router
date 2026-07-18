using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;
using Sharq.Kit;
using TabItem = SusTabs.TabItem;

/// <summary>
/// Basic Routing — simple navigation with SusTabs.
/// Demonstrates: Push, Replace, Back, Home.
/// Navbar: SusTabs (4 tabs) + Back + CurrentRoute chip.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class BasicRoutingExample : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private SusRouter _router;
    private VisualElement _navBar;
    private SusChip _routeChip;
    private SusButton _backBtn;
    private SusTabs _navTabs;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (System.Exception ex)
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

        // Unified bootstrap: EventSystem + token cascade, then BuildContent (nav + router
        // mount + overlay), then theme applied LAST so the OverlayHost inherits it.
        SusApp.Create(root)
              .UseTheme(SusTheme.Dark)
              .Configure(BuildContent)
              .Run();
    }

    private void BuildContent(VisualElement root)
    {
        root.style.flexGrow = 1f;

        // ── Navbar ──
        _navBar = new VisualElement();
        _navBar.style.flexDirection = FlexDirection.Row;
        _navBar.style.alignItems = Align.Center;
        _navBar.style.paddingTop = 8;
        _navBar.style.paddingBottom = 8;
        _navBar.style.paddingLeft = 16;
        _navBar.style.paddingRight = 16;
        _navBar.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        _navBar.style.flexShrink = 0;

        // Back button
        _backBtn = new SusButton();
        _backBtn.Text.Value = "<- Back";

        // SusTabs navigation
        _navTabs = new SusTabs();
        _navTabs.Items.Value = new List<TabItem>
        {
            new() { Title = "Home", Value = "/home" },
            new() { Title = "About", Value = "/about" },
            new() { Title = "Contact", Value = "/contact" },
            new() { Title = "Settings", Value = "/settings" },
        };
        _navTabs.Model.Value = "/home";

        // Current route chip
        _routeChip = new SusChip();
        _routeChip.Label.Value = "/";

        _navBar.Add(_backBtn);
        _navBar.Add(_navTabs);
        _navBar.Add(_routeChip);
        root.Add(_navBar);

        // ── Overlay + Router ──
        _router = new SusRouter();

        _router.Register("/home", typeof(HomeScreen));
        _router.Register("/about", typeof(AboutScreen));
        _router.Register("/contact", typeof(ContactScreen));
        _router.Register("/settings", typeof(SettingsScreen));

        _router.Mount(root, "/home");
        // Overlay host: Mount() already calls Init() internally, adding overlay as last child.
        // No need for explicit Init() — it would be a no-op (guarded by _isInitialized).

        // Wire SusTabs and Back button
        _navTabs.OnTabChanged += path =>
        {
            _router.Push(path);
            _navTabs.Model.Value = _router.CurrentRoute.Value?.Record?.Path ?? "/home";
        };
        _backBtn.RegisterCallback<ClickEvent>(_ =>
        {
            _router.Back();
            UpdateBackButton();
        });

        _router.CurrentRoute.Changed += (o, n) =>
        {
            _routeChip.Label.Value = n?.FullPath ?? "/";
            // Sync SusTabs Model with current route
            if (n?.Record?.Path != null)
                _navTabs.Model.Value = n.Record.Path;
            UpdateBackButton();
            UpdateNavTabsModel(n?.Record?.Path);
        };

        UpdateBackButton();
    }

    private void UpdateBackButton() =>
        _backBtn.style.display = (_router?.CanGoBack ?? false)
            ? DisplayStyle.Flex : DisplayStyle.None;

    private void UpdateNavTabsModel(string path) =>
        _navTabs.Model.Value = path ?? "/home";

    // ════════════════════════════════════════════════════════════════
    //  Screens
    // ════════════════════════════════════════════════════════════════

    internal class HomeScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label("Home") { style = { fontSize = 28, color = Color.white, marginBottom = 16 } });

            var btn1 = new SusButton();
            btn1.Text.Value = "Go to About";
            btn1.Variant.Value = "primary";
            btn1.RegisterCallback<ClickEvent>(_ => Router.Push("/about"));
            Add(btn1);

            var btn2 = new SusButton();
            btn2.Text.Value = "Go to Contact";
            btn2.Variant.Value = "secondary";
            btn2.RegisterCallback<ClickEvent>(_ => Router.Push("/contact"));
            btn2.style.marginTop = 8;
            Add(btn2);

            var btn3 = new SusButton();
            btn3.Text.Value = "Replace with Settings";
            btn3.Variant.Value = "warning";
            btn3.RegisterCallback<ClickEvent>(_ => Router.Replace("/settings"));
            btn3.style.marginTop = 8;
            Add(btn3);

            var chip = new SusChip();
            chip.Label.Value = "Home";
            chip.style.marginTop = 24;
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

            Add(new SusImg());

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

            var nameField = new SusTextfield();
            nameField.Label.Value = "Name";
            nameField.style.marginBottom = 8;
            Add(nameField);

            var emailField = new SusTextfield();
            emailField.Label.Value = "Email";
            emailField.style.marginBottom = 16;
            Add(emailField);

            var submitBtn = new SusButton();
            submitBtn.Text.Value = "Submit";
            submitBtn.Variant.Value = "success";
            submitBtn.RegisterCallback<ClickEvent>(_ => Debug.Log("[Contact] Submitted"));
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

            var darkToggle = new SusToggle();
            darkToggle.Label.Value = "Dark theme";
            darkToggle.style.marginBottom = 8;
            Add(darkToggle);

            var notifToggle = new SusToggle();
            notifToggle.Label.Value = "Notifications";
            Add(notifToggle);
        }
    }
}

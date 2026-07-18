using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;
using Sharq.Kit;
using TabItem = SusTabs.TabItem;

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
    private SusTabs _navTabs;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (System.Exception ex)
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

        // Unified bootstrap: EventSystem + token cascade, then BuildContent (nav + router
        // mount + overlay), then theme applied LAST so the OverlayHost inherits it.
        SusApp.Create(root)
              .UseTheme(SusTheme.Dark)
              .Configure(BuildContent)
              .Run();
    }

    private void BuildContent(VisualElement root)
    {
        // ── Tabs ──
        _navTabs = new SusTabs();
        _navTabs.Items.Value = new List<TabItem>
        {
            new() { Title = "[K] Counter", Value = "/counter" },
            new() { Title = "[K] Form", Value = "/form" },
            new() { Title = "Settings", Value = "/settings" },
        };
        _navTabs.Model.Value = "/counter";
        _navTabs.style.marginBottom = 8;
        root.Add(_navTabs);

        // ── Router ──
        _router = new SusRouter();

        _router.Register("/counter", typeof(CounterScreen), new SusRouteConfig { KeepAlive = true });
        _router.Register("/form", typeof(FormScreen), new SusRouteConfig { KeepAlive = true });
        _router.Register("/settings", typeof(SettingsScreen)); // NO KeepAlive

        _router.Mount(root, "/counter");
        // Mount() already calls Init() internally (guarded). No need for explicit Init().

        _navTabs.OnTabChanged += path =>
        {
            _router.Push(path);
            _navTabs.Model.Value = _router.CurrentRoute.Value?.Record?.Path ?? "/counter";
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            if (n?.Record?.Path != null)
                _navTabs.Model.Value = n.Record.Path;
        };

        Debug.Log("[KeepAlive] Ready. Switch tabs to test KeepAlive.");
    }

    // ════════════════════════════════════════════════════════════════
    //  Screens
    // ════════════════════════════════════════════════════════════════

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

            var incBtn = new SusButton();
            incBtn.Text.Value = "+1";
            incBtn.Variant.Value = "primary";
            incBtn.RegisterCallback<ClickEvent>(_ => { _count++; _countLabel.text = $"Count: {_count}"; });
            Add(incBtn);

            var resetBtn = new SusButton();
            resetBtn.Text.Value = "Reset";
            resetBtn.Variant.Value = "secondary";
            resetBtn.style.marginTop = 8;
            resetBtn.RegisterCallback<ClickEvent>(_ => { _count = 0; _countLabel.text = "Count: 0"; });
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

            var nameField = new SusTextfield();
            nameField.Label.Value = "Your Name";
            nameField.style.marginBottom = 8;
            Add(nameField);

            var emailField = new SusTextfield();
            emailField.Label.Value = "Email";
            Add(emailField);

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

            var darkToggle = new SusToggle();
            darkToggle.Label.Value = "Dark mode";
            darkToggle.style.marginBottom = 8;
            Add(darkToggle);

            var notifToggle = new SusToggle();
            notifToggle.Label.Value = "Notifications";
            Add(notifToggle);

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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;
using Sharq.Kit;
using TabItem = SusTabs.TabItem;

/// <summary>
/// Modals & Transitions — modals + transition animation.
/// Demonstrates: SusRouterModal (info/confirm/stack),
/// NavigateWithTransition (FadeOut/FadeIn), SusModalService.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ModalExample : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private SusRouter _router;
    private bool _animateTransition = true;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Modal] OnEnable failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void BuildUI()
    {
        var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
        if (doc == null) { Debug.LogError("[Modal] No UIDocument found!"); return; }

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
        // ── Navbar: tabs + transitions ──
        var navBar = new VisualElement();
        navBar.style.flexDirection = FlexDirection.Row;
        navBar.style.alignItems = Align.Center;
        navBar.style.paddingTop = 8;
        navBar.style.paddingBottom = 8;
        navBar.style.paddingLeft = 16;
        navBar.style.paddingRight = 16;
        navBar.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        navBar.style.flexShrink = 0;

        var navTabs = new SusTabs();
        navTabs.Items.Value = new List<TabItem>
        {
            new() { Title = "Page 1", Value = "/page-1" },
            new() { Title = "Page 2", Value = "/page-2" },
            new() { Title = "Page 3", Value = "/page-3" },
        };
        navTabs.Model.Value = "/page-1";
        navBar.Add(navTabs);

        var animToggle = new SusToggle();
        animToggle.Label.Value = "Animate transition";
        animToggle.Model.Value = true; // ON by default — show the fade animation
        animToggle.style.marginLeft = 16;
        animToggle.OnChange += v => _animateTransition = v;
        navBar.Add(animToggle);

        var hintChip = new SusChip();
        hintChip.Label.Value = "Fade 0.3s";
        hintChip.Variant.Value = "outlined";
        hintChip.style.marginLeft = 12;
        navBar.Add(hintChip);

        animToggle.Model.Changed += (_, v) =>
            hintChip.Label.Value = v ? "Fade 0.3s" : "No animation";

        root.Add(navBar);

        // ── Modal buttons row ──
        var modalHint = new Label(
            "← Modals appear in the overlay. Popup → Modals → Info/Confirm/Stack.");
        modalHint.style.color = new Color(0.5f, 0.5f, 0.6f);
        modalHint.style.fontSize = 12;
        modalHint.style.paddingLeft = 16;
        modalHint.style.paddingTop = 10;
        modalHint.style.paddingBottom = 6;
        root.Add(modalHint);

        var modalRow = new VisualElement();
        modalRow.style.flexDirection = FlexDirection.Row;
        modalRow.style.paddingLeft = 16;
        modalRow.style.paddingRight = 16;
        modalRow.style.paddingBottom = 6;

        var infoBtn = new SusButton();
        infoBtn.Text.Value = "Open Info";
        infoBtn.Variant.Value = "primary";
        infoBtn.RegisterCallback<ClickEvent>(_ =>
            _router.ModalService?.Show(typeof(InfoDialog),
                new() { ["title"] = "Information", ["message"] = "This is an info dialog." }));
        modalRow.Add(infoBtn);

        var confirmBtn = new SusButton();
        confirmBtn.Text.Value = "Open Confirm";
        confirmBtn.Variant.Value = "warning";
        confirmBtn.style.marginLeft = 8;
        confirmBtn.RegisterCallback<ClickEvent>(_ =>
            _router.ModalService?.Show(typeof(ConfirmDialog),
                new() { ["message"] = "Are you sure?" }));
        modalRow.Add(confirmBtn);

        var stackBtn = new SusButton();
        stackBtn.Text.Value = "Stack 3";
        stackBtn.Variant.Value = "secondary";
        stackBtn.style.marginLeft = 8;
        stackBtn.RegisterCallback<ClickEvent>(_ =>
        {
            _router.ModalService?.Show(typeof(ConfirmDialog),
                new() { ["message"] = "First (bottom)" });
            _router.ModalService?.Show(typeof(InfoDialog),
                new() { ["title"] = "Second", ["message"] = "On top of first" });
            _router.ModalService?.Show(typeof(ConfirmDialog),
                new() { ["message"] = "Third (top)" });
        });
        modalRow.Add(stackBtn);

        root.Add(modalRow);

        // ── Router ──
        _router = new SusRouter();

        _router.Register("/page-1", typeof(PageScreen));
        _router.Register("/page-2", typeof(PageScreen));
        _router.Register("/page-3", typeof(PageScreen));

        _router.Mount(root, "/page-1");
        // Mount() already calls Init() internally (guarded). No need for explicit Init().

        // ── Floating modal controls (overlay, always above modals) ──
        BuildModalControls(overlayHost);

        navTabs.OnTabChanged += path =>
        {
            if (_animateTransition)
                _router.NavigateWithTransition(path, 0.3f);
            else
            {
                _router.Push(path);
                navTabs.Model.Value = _router.CurrentRoute.Value?.Record?.Path ?? "/page-1";
            }
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            if (n?.Record?.Path != null)
                navTabs.Model.Value = n.Record.Path;
        };

        Debug.Log("[Modal] Ready. Use buttons to open modals, tabs to switch pages.");
    }

    private void BuildModalControls(OverlayHost overlayHost)
    {
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.bottom = 16;
        panel.style.right = 16;
        panel.style.flexDirection = FlexDirection.Row;
        panel.style.alignItems = Align.Center;
        panel.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        panel.style.paddingTop = 6;
        panel.style.paddingBottom = 6;
        panel.style.paddingLeft = 12;
        panel.style.paddingRight = 12;
        panel.pickingMode = PickingMode.Position; // catch clicks, don't pass through

        var counterChip = new SusChip();
        counterChip.Label.Value = "0 modals";
        counterChip.Variant.Value = "outlined";
        panel.Add(counterChip);

        var closeBtn = new SusButton();
        closeBtn.Text.Value = "Close Top";
        closeBtn.Variant.Value = "danger";
        closeBtn.style.marginLeft = 10;
        closeBtn.RegisterCallback<ClickEvent>(_ =>
            _router.ModalService?.Close());
        panel.Add(closeBtn);

        // ── Reactive binding: counter follows actual modal stack depth ──
        if (_router.ModalService != null)
        {
            _router.ModalService.CountProp.Changed += (_, newCount) =>
                counterChip.Label.Value = newCount == 0
                    ? "0 modals"
                    : $"{newCount} modal{(newCount == 1 ? "" : "s")}";
        }

        overlayHost.AddToOverlay(panel, OverlayCategory.Dropdown);
    }

    // ════════════════════════════════════════════════════════════════
    //  Screens
    // ════════════════════════════════════════════════════════════════

    internal class PageScreen : SusScreen
    {
        private Label _titleLabel;

        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            _titleLabel = new Label("Page ?");
            _titleLabel.style.fontSize = 28;
            _titleLabel.style.color = Color.white;
            _titleLabel.style.marginBottom = 16;
            Add(_titleLabel);
        }

        protected override bool OnBeforeEnter(SusRoute from)
        {
            var path = Router?.CurrentRoute?.Value?.FullPath ?? "?";
            _titleLabel.text = $"Page {path}";
            Debug.Log($"[PageScreen] BeforeEnter ← {from.FullPath}");
            return true;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Modal dialogs
    // ════════════════════════════════════════════════════════════════

    internal class ConfirmDialog : Sharq.Router.SusRouterModal
    {
        private Label _msgLabel;

        protected override void Build()
        {
            style.width = 360;
            style.height = 180;
            style.backgroundColor = new Color(0.2f, 0.2f, 0.3f, 0.95f);
            AddToClassList("modal-dlg-rounded");
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.flexDirection = FlexDirection.Column;

            _msgLabel = new Label("...");
            _msgLabel.style.color = Color.white;
            _msgLabel.style.fontSize = 18;
            _msgLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _msgLabel.style.marginBottom = 20;
            Add(_msgLabel);

            var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            var okBtn = new SusButton();
            okBtn.Text.Value = "OK";
            okBtn.Variant.Value = "primary";
            okBtn.RegisterCallback<ClickEvent>(_ => Dismiss());
            btnRow.Add(okBtn);

            var cancelBtn = new SusButton();
            cancelBtn.Text.Value = "Cancel";
            cancelBtn.Variant.Value = "secondary";
            cancelBtn.style.marginLeft = 8;
            cancelBtn.RegisterCallback<ClickEvent>(_ => Dismiss());
            btnRow.Add(cancelBtn);

            Add(btnRow);
        }

        protected internal override void Shown()
        {
            var msg = Props.TryGetValue("message", out var v) ? v?.ToString() : "Confirm?";
            _msgLabel.text = msg;
            Debug.Log($"[ConfirmDialog] Shown: '{msg}'");
        }
    }

    internal class InfoDialog : Sharq.Router.SusRouterModal
    {
        private Label _bodyLabel;

        protected override void Build()
        {
            style.width = 300;
            style.height = 160;
            style.backgroundColor = new Color(0.15f, 0.25f, 0.15f, 0.95f);
            AddToClassList("modal-dlg-rounded");
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.flexDirection = FlexDirection.Column;

            _bodyLabel = new Label("...");
            _bodyLabel.style.color = Color.white;
            _bodyLabel.style.fontSize = 16;
            _bodyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _bodyLabel.style.marginBottom = 16;
            Add(_bodyLabel);

            var closeBtn = new SusButton();
            closeBtn.Text.Value = "Close";
            closeBtn.Variant.Value = "primary";
            closeBtn.RegisterCallback<ClickEvent>(_ => Dismiss());
            Add(closeBtn);
        }

        protected internal override void Shown()
        {
            var title = Props.TryGetValue("title", out var t) ? t?.ToString() : "Info";
            var msg = Props.TryGetValue("message", out var m) ? m?.ToString() : "";
            _bodyLabel.text = $"{title}:\n{msg}";
            Debug.Log($"[InfoDialog] Shown: '{title}' / '{msg}'");
        }
    }
}

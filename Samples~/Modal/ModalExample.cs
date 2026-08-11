using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;

/// <summary>
/// Modals & Transitions — modals + transition animation.
/// Demonstrates: SusRouterModal (info/confirm/stack),
/// NavigateWithTransition (FadeOut/FadeIn).
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
        catch (Exception ex)
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
        navBar.style.flexShrink = 0;

        var navTabs = new SimpleTabBar(new[]
        {
            ("Page 1", "/page-1"),
            ("Page 2", "/page-2"),
            ("Page 3", "/page-3"),
        }, "/page-1");
        navBar.Add(navTabs.Root);

        var animToggle = new Toggle("Animate transition") { value = true };
        animToggle.style.marginLeft = 16;
        animToggle.RegisterValueChangedCallback(evt => _animateTransition = evt.newValue);
        navBar.Add(animToggle);

        var hintChip = MakeChip("Fade 0.3s");
        hintChip.style.marginLeft = 12;
        navBar.Add(hintChip);

        animToggle.RegisterValueChangedCallback(evt =>
            hintChip.text = evt.newValue ? "Fade 0.3s" : "No animation");

        screens.Add(navBar);

        var modalHint = new Label(
            "← Modals appear in the overlay. Use Open Info / Confirm / Stack.");
        modalHint.style.color = new Color(0.5f, 0.5f, 0.6f);
        modalHint.style.fontSize = 12;
        modalHint.style.paddingLeft = 16;
        modalHint.style.paddingTop = 10;
        modalHint.style.paddingBottom = 6;
        screens.Add(modalHint);

        var modalRow = new VisualElement();
        modalRow.style.flexDirection = FlexDirection.Row;
        modalRow.style.paddingLeft = 16;
        modalRow.style.paddingRight = 16;
        modalRow.style.paddingBottom = 6;

        var infoBtn = MakeButton("Open Info");
        infoBtn.clicked += () =>
            _router.ModalService?.Show(typeof(InfoDialog),
                new() { ["title"] = "Information", ["message"] = "This is an info dialog." });
        modalRow.Add(infoBtn);

        var confirmBtn = MakeButton("Open Confirm");
        confirmBtn.style.marginLeft = 8;
        confirmBtn.clicked += () =>
            _router.ModalService?.Show(typeof(ConfirmDialog),
                new() { ["message"] = "Are you sure?" });
        modalRow.Add(confirmBtn);

        var stackBtn = MakeButton("Stack 3");
        stackBtn.style.marginLeft = 8;
        stackBtn.clicked += () =>
        {
            _router.ModalService?.Show(typeof(ConfirmDialog),
                new() { ["message"] = "First (bottom)" });
            _router.ModalService?.Show(typeof(InfoDialog),
                new() { ["title"] = "Second", ["message"] = "On top of first" });
            _router.ModalService?.Show(typeof(ConfirmDialog),
                new() { ["message"] = "Third (top)" });
        };
        modalRow.Add(stackBtn);

        screens.Add(modalRow);

        var routeSlot = new VisualElement { name = "route-slot" };
        routeSlot.style.flexGrow = 1f;
        screens.Add(routeSlot);

        var overlayHost = SusBootstrap.GetOrCreateOverlay(root);
        _router = new SusRouter();
        _router.Init(overlayHost);

        _router.Register("/page-1", typeof(PageScreen));
        _router.Register("/page-2", typeof(PageScreen));
        _router.Register("/page-3", typeof(PageScreen));

        _router.Mount(routeSlot, "/page-1");

        BuildModalControls(overlayHost);

        navTabs.OnChanged += path =>
        {
            if (_animateTransition)
                _router.NavigateWithTransition(path, 0.3f);
            else
            {
                _router.Push(path);
                navTabs.SetValue(_router.CurrentRoute.Value?.Record?.Path ?? "/page-1");
            }
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            if (n?.Record?.Path != null)
                navTabs.SetValue(n.Record.Path);
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
        panel.pickingMode = PickingMode.Position;

        var counterChip = MakeChip("0 modals");
        panel.Add(counterChip);

        var closeBtn = MakeButton("Close Top");
        closeBtn.style.marginLeft = 10;
        closeBtn.clicked += () => _router.ModalService?.Close();
        panel.Add(closeBtn);

        if (_router.ModalService != null)
        {
            _router.ModalService.CountProp.Changed += (_, newCount) =>
                counterChip.text = newCount == 0
                    ? "0 modals"
                    : $"{newCount} modal{(newCount == 1 ? "" : "s")}";
        }

        overlayHost.AddToOverlay(panel, OverlayCategory.Dropdown);
    }

    internal static Button MakeButton(string text)
    {
        var b = new Button { text = text };
        b.AddToClassList("modal-btn");
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
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
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

    internal class ConfirmDialog : SusRouterModal
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

            var okBtn = MakeButton("OK");
            okBtn.AddToClassList("modal-btn--ok");
            okBtn.clicked += () => Dismiss();
            btnRow.Add(okBtn);

            var cancelBtn = MakeButton("Cancel");
            cancelBtn.AddToClassList("modal-btn--cancel");
            cancelBtn.style.marginLeft = 8;
            cancelBtn.clicked += () => Dismiss();
            btnRow.Add(cancelBtn);

            Add(btnRow);
        }

        protected override void Shown()
        {
            var msg = Props.TryGetValue("message", out var v) ? v?.ToString() : "Confirm?";
            _msgLabel.text = msg;
            Debug.Log($"[ConfirmDialog] Shown: '{msg}'");
        }
    }

    internal class InfoDialog : SusRouterModal
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

            var closeBtn = MakeButton("Close");
            closeBtn.AddToClassList("modal-btn--ok");
            closeBtn.clicked += () => Dismiss();
            Add(closeBtn);
        }

        protected override void Shown()
        {
            var title = Props.TryGetValue("title", out var t) ? t?.ToString() : "Info";
            var msg = Props.TryGetValue("message", out var m) ? m?.ToString() : "";
            _bodyLabel.text = $"{title}:\n{msg}";
            Debug.Log($"[InfoDialog] Shown: '{title}' / '{msg}'");
        }
    }
}

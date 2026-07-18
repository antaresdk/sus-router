using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;
using Sharq.Kit;
using TabItem = SusTabs.TabItem;

/// <summary>
/// Guards — route protection.
/// Demonstrates:
/// • beforeEach   — auth check (SusToggle "Logged in")
/// • CanLeave     — confirm on unsaved changes (modal)
/// • Redirect     — redirect /old-admin → /admin (via Redirect config)
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GuardsExample : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private SusRouter _router;
    private bool _isLoggedIn;
    private SusChip _statusChip;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Guards] OnEnable failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  UI
    // ════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
        if (doc == null) { Debug.LogError("[Guards] No UIDocument found!"); return; }

        var ps = Resources.Load<PanelSettings>("PanelSettings");
        if (ps != null) doc.panelSettings = ps;

        var root = doc.rootVisualElement;
        root.style.flexGrow = 1f;
        root.style.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

        // Remove stray overlays (splash/loading) if the bootstrapper inserted them.
        foreach (var stray in root.Query<VisualElement>().Build())
        {
            if (stray.name == "sus-splash-screen" || stray.name == "sus-loading-screen")
            {
                stray.RemoveFromHierarchy();
                Debug.Log($"[Guards] Removed stray overlay: {stray.name}");
            }
        }

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

        var navTabs = new SusTabs();
        navTabs.Items.Value = new List<TabItem>
        {
            new() { Title = "Home",     Value = "/home" },
            new() { Title = "Admin",    Value = "/admin" },
            new() { Title = "Profile",  Value = "/profile" },
            new() { Title = "OldAdmin", Value = "/old-admin" },
        };
        navTabs.Model.Value = "/home";
        navBar.Add(navTabs);

        var loginToggle = new SusToggle();
        loginToggle.Label.Value = "Logged in";
        loginToggle.style.marginLeft = 16;
        loginToggle.OnChange += v => { _isLoggedIn = v; UpdateStatusChip(); };
        navBar.Add(loginToggle);

        _statusChip = new SusChip();
        _statusChip.Label.Value = "Not logged in";
        _statusChip.style.marginLeft = 16;
        navBar.Add(_statusChip);

        root.Add(navBar);

        // ── Router + Guards ──
        _router = new SusRouter();

        // beforeEach: auth check
        _router.BeforeEach((from, to) =>
        {
            if (to.FullPath == "/home") return true;
            if (!_isLoggedIn)
            {
                Debug.Log($"[Guard] beforeEach BLOCKED: {from.FullPath} → {to.FullPath}");
                _statusChip.Label.Value = $"🔒 Login required";
                return false;
            }
            return true;
        });

        // Redirect — /old-admin redirected to /admin via Redirect config (not through beforeResolve).
        // Redirect config is handled BEFORE screen creation — no nested navigation.
        _router.Register("/home", typeof(HomeScreen));
        _router.Register("/admin", typeof(AdminScreen), new SusRouteConfig
        {
            Guard = new AdminGuard()
        });
        _router.Register("/profile", typeof(ProfileScreen));
        _router.Register("/old-admin", typeof(AdminScreen), new SusRouteConfig
        {
            Redirect = "/admin"
        });

        _router.Mount(root, "/home");
        // Mount() already calls Init() internally (guarded). No need for explicit Init().

        navTabs.OnTabChanged += path =>
        {
            var result = _router.Push(path);
            // Always resync — redirects can cause a no-op that doesn't fire
            // CurrentRoute.Changed (fromRoute == toRoute after redirect → no-op).
            navTabs.Model.Value = _router.CurrentRoute.Value?.Record?.Path ?? "/home";
            if (result == NavigationResult.Busy)
            {
                Debug.Log($"[Guards] Router busy — request dropped.");
            }
        };

        _router.CurrentRoute.Changed += (o, n) =>
        {
            if (n?.Record?.Path != null)
                navTabs.Model.Value = n.Record.Path;
            UpdateStatusChip();
        };

        Debug.Log("[Guards] Ready.");
    }

    private void UpdateStatusChip()
    {
        _statusChip.Label.Value = _isLoggedIn ? "✅ Logged in" : "Not logged in";
    }

    // ════════════════════════════════════════════════════════════════
    //  AdminGuard — per-route: checks dirty form via statics
    // ════════════════════════════════════════════════════════════════

    private class AdminGuard : ISusRouteGuard
    {
        public bool CanEnter(SusRoute from, SusRoute to) => true;

        public bool CanLeave(SusRoute from, SusRoute to)
        {
            if (AdminScreen.IsDirty)
            {
                Debug.Log("[AdminGuard] CanLeave: blocked — form is dirty");
                // Show the modal only if one is not already open
                if (!AdminScreen.IsShowingLeaveModal)
                    AdminScreen.ShowLeaveConfirmation?.Invoke(from, to);
                return false; // Always block when dirty
            }
            return true;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  HomeScreen — instructions and buttons
    // ════════════════════════════════════════════════════════════════

    internal class HomeScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label("🔐 Navigation Guards")
            {
                style = { fontSize = 28, color = Color.white, marginBottom = 20 },
            });

            // Info panel: what guards are and how to test them
            var info = InfoPanel(new[]
            {
                ("What this is", "Guards protect routes. They block navigation " +
                    "to a page without auth or leaving a form with unsaved changes."),
                ("Step 1", "Press \"Go to Admin\" — navigation is blocked (not logged in)."),
                ("Step 2", "Turn on the \"Logged in\" toggle in the navbar — navigation is allowed."),
                ("Step 3", "On the Admin page type into the field → try to leave → " +
                    "the CanLeave guard asks for confirmation."),
                ("Step 4", "\"OldAdmin\" tab → Redirect config to /admin."),
                ("Step 5", "\"Profile\" is available only when logged in — " +
                    "beforeEach check."),
            });
            Add(info);

            Add(Section("Try it"));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 8;

            var adminBtn = new SusButton();
            adminBtn.Text.Value = "Go to Admin";
            adminBtn.Variant.Value = "primary";
            adminBtn.RegisterCallback<ClickEvent>(_ => Router.Push("/admin"));
            row.Add(adminBtn);

            var profileBtn = new SusButton();
            profileBtn.Text.Value = "Go to Profile";
            profileBtn.style.marginLeft = 12;
            profileBtn.RegisterCallback<ClickEvent>(_ => Router.Push("/profile"));
            row.Add(profileBtn);

            Add(row);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  AdminScreen — form + dirty + CanLeave guard
    // ════════════════════════════════════════════════════════════════

    internal class AdminScreen : SusScreen
    {
        public static bool IsDirty { get; private set; }
        public static bool IsShowingLeaveModal { get; private set; }
        public static System.Action<SusRoute, SusRoute> ShowLeaveConfirmation { get; set; }

        private SusTextfield _titleField;
        private SusChip _dirtyChip;

        protected override void Build()
        {
            ShowLeaveConfirmation = (fromRoute, toRoute) =>
                ShowDirtyModal(fromRoute, toRoute);
            IsDirty = false;

            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;
            style.maxWidth = 500;

            Add(new Label("⚙️ Admin Panel")
            {
                style =
                {
                    fontSize = 24, color = new Color(1f, 0.85f, 0.3f),
                    marginBottom = 16,
                },
            });

            var info = InfoPanel(new[]
            {
                ("CanLeave guard", "Changing the field marks the form \"dirty\". " +
                    "Trying to leave opens a confirmation modal."),
                ("How to verify", "Type text → click another tab → " +
                    "the \"Discard changes?\" dialog appears."),
            });
            Add(info);

            Add(Section("Announcement"));

            _titleField = new SusTextfield();
            _titleField.Label.Value = "Title";
            _titleField.Placeholder.Value = "Type something...";
            _titleField.Model.Changed += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_titleField.Model.Value))
                {
                    IsDirty = true;
                    _dirtyChip.Label.Value = "📝 Unsaved changes";
                    _dirtyChip.style.display = DisplayStyle.Flex;
                }
            };
            _titleField.style.marginBottom = 12;
            Add(_titleField);

            _dirtyChip = new SusChip();
            _dirtyChip.Label.Value = "📝 Unsaved changes";
            _dirtyChip.style.display = DisplayStyle.None;
            _dirtyChip.style.marginBottom = 8;
            Add(_dirtyChip);

            var saveBtn = new SusButton();
            saveBtn.Text.Value = "Save";
            saveBtn.Variant.Value = "tonal";
            saveBtn.RegisterCallback<ClickEvent>(_ =>
            {
                IsDirty = false;
                _dirtyChip.style.display = DisplayStyle.None;
                _dirtyChip.Label.Value = "✅ Saved";
                _dirtyChip.style.display = DisplayStyle.Flex;
                schedule.Execute(() =>
                {
                    _dirtyChip.style.display = DisplayStyle.None;
                }).StartingIn(2000);
            });
            Add(saveBtn);

            var hint = new Label("↑ Press Save OR switch tabs\n" +
                "   to see the CanLeave guard in action.")
            {
                style =
                {
                    color = new Color(0.5f, 0.5f, 0.6f),
                    fontSize = 13,
                    marginTop = 20,
                    whiteSpace = WhiteSpace.Normal,
                },
            };
            Add(hint);
        }

        private void ShowDirtyModal(SusRoute from, SusRoute to)
        {
            if (IsShowingLeaveModal) return;
            IsShowingLeaveModal = true;

            var modal = new SusModal();

            // Overlay backdrop
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            modal.Add(overlay);

            // Card
            var card = new SusCard();
            card.Variant.Value = "elevated";
            card.style.minWidth = 320f;
            card.style.paddingTop = 24f;
            card.style.paddingBottom = 24f;
            card.style.paddingLeft = 24f;
            card.style.paddingRight = 24f;

            var title = new Label("Unsaved changes");
            title.style.fontSize = 18f;
            title.style.color = new Color(0.9f, 0.9f, 0.95f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12f;
            card.Add(title);

            var body = new Label(
                "You have unsaved changes. Leave without saving?");
            body.style.color = new Color(0.6f, 0.6f, 0.7f);
            body.style.marginBottom = 16f;
            body.style.whiteSpace = WhiteSpace.Normal;
            card.Add(body);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;

            var stayBtn = new SusButton();
            stayBtn.Text.Value = "Stay";
            stayBtn.Variant.Value = "outlined";
            stayBtn.RegisterCallback<ClickEvent>(_ =>
            {
                IsShowingLeaveModal = false;
                modal.Close();
            });
            btnRow.Add(stayBtn);

            var leaveBtn = new SusButton();
            leaveBtn.Text.Value = "Leave";
            leaveBtn.Variant.Value = "tonal";
            leaveBtn.style.marginLeft = 12;
            leaveBtn.RegisterCallback<ClickEvent>(_ =>
            {
                IsDirty = false;
                IsShowingLeaveModal = false;
                modal.Close();
                Router.Push(to.FullPath);
            });
            btnRow.Add(leaveBtn);
            card.Add(btnRow);

            overlay.Add(card);

            // Also handle escape / backdrop click
            modal.OnClosed += () => { IsShowingLeaveModal = false; };

            // Add modal to screen (parent = router content area)
            Add(modal);
            modal.Open();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ProfileScreen — demo: screen available only when authenticated
    // ════════════════════════════════════════════════════════════════

    internal class ProfileScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f;
            style.paddingTop = 32;
            style.paddingLeft = 32;
            style.paddingRight = 32;

            Add(new Label("👤 Profile")
            {
                style = { fontSize = 28, color = Color.white, marginBottom = 20 },
            });

            var info = InfoPanel(new[]
            {
                ("Why this screen", "Profile is available only when the \"Logged in\" toggle is on. " +
                    "Turn the toggle off and try to navigate — beforeEach will block."),
                ("How it works", "beforeEach checks `_isLoggedIn` for all routes except /home. " +
                    "If not logged in — navigation is cancelled, the tab returns to Home."),
            });
            Add(info);

            Add(Section("User info"));

            var idCard = Card();
            var idLabel = new Label("👋 User: demo@example.com");
            idLabel.style.fontSize = 20;
            idLabel.style.color = Color.cyan;
            idCard.Add(idLabel);

            var roleChip = new SusChip();
            roleChip.Label.Value = "Role: Editor";
            roleChip.style.marginTop = 8;
            idCard.Add(roleChip);

            Add(idCard);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════

    private static VisualElement InfoPanel((string title, string body)[] items)
    {
        var card = new VisualElement();
        card.style.backgroundColor = new Color(0.15f, 0.16f, 0.22f);
        card.style.borderTopLeftRadius = 8;
        card.style.borderTopRightRadius = 8;
        card.style.borderBottomLeftRadius = 8;
        card.style.borderBottomRightRadius = 8;
        card.style.paddingTop = 16;
        card.style.paddingBottom = 16;
        card.style.paddingLeft = 20;
        card.style.paddingRight = 20;
        card.style.marginBottom = 20;

        foreach (var (title, body) in items)
        {
            var row = new VisualElement();
            row.style.marginBottom = 10;

            var t = new Label(title);
            t.style.fontSize = 13;
            t.style.color = new Color(0.5f, 0.7f, 1f);
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.marginBottom = 2;
            row.Add(t);

            var b = new Label(body);
            b.style.fontSize = 13;
            b.style.color = new Color(0.7f, 0.7f, 0.8f);
            b.style.whiteSpace = WhiteSpace.Normal;
            row.Add(b);

            card.Add(row);
        }
        return card;
    }

    private static VisualElement Card()
    {
        var c = new VisualElement();
        c.style.backgroundColor = new Color(0.1f, 0.11f, 0.16f);
        c.style.borderTopLeftRadius = 8;
        c.style.borderTopRightRadius = 8;
        c.style.borderBottomLeftRadius = 8;
        c.style.borderBottomRightRadius = 8;
        c.style.paddingTop = 16;
        c.style.paddingBottom = 16;
        c.style.paddingLeft = 20;
        c.style.paddingRight = 20;
        c.style.marginBottom = 12;
        return c;
    }

    private static VisualElement Section(string title)
    {
        var l = new Label(title);
        l.style.fontSize = 14;
        l.style.color = new Color(0.9f, 0.9f, 1f);
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.marginBottom = 8;
        return l;
    }
}

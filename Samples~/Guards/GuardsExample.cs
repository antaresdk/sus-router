using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;

namespace Sharq.Router.Examples
{
    /// <summary>
    /// Guards — route protection.
    /// Demonstrates:
    /// • beforeEach   — auth check (Toggle "Logged in")
    /// • CanLeave     — confirm on unsaved changes (modal)
    /// • Redirect     — redirect /old-admin → /admin (via Redirect config)
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GuardsExample : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private SusRouter _router;
        private bool _isLoggedIn;
        private Label _statusChip;

        private void OnEnable()
        {
            try { BuildUI(); }
            catch (Exception ex)
            {
                Debug.LogError($"[Guards] OnEnable failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void BuildUI()
        {
            var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
            if (doc == null) { Debug.LogError("[Guards] No UIDocument found!"); return; }

            var ps = Resources.Load<PanelSettings>("PanelSettings");
            if (ps != null) doc.panelSettings = ps;

            var root = doc.rootVisualElement;
            root.style.flexGrow = 1f;
            root.style.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

            foreach (var stray in root.Query<VisualElement>().Build())
            {
                if (stray.name == "sus-splash-screen" || stray.name == "sus-loading-screen")
                {
                    stray.RemoveFromHierarchy();
                    Debug.Log($"[Guards] Removed stray overlay: {stray.name}");
                }
            }

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

            var navTabs = new SimpleTabBar(new[]
            {
                ("Home", "/home"),
                ("Admin", "/admin"),
                ("Profile", "/profile"),
                ("OldAdmin", "/old-admin"),
            }, "/home");
            navBar.Add(navTabs.Root);

            var loginToggle = new Toggle("Logged in");
            loginToggle.style.marginLeft = 16;
            loginToggle.RegisterValueChangedCallback(evt =>
            {
                _isLoggedIn = evt.newValue;
                UpdateStatusChip();
            });
            navBar.Add(loginToggle);

            _statusChip = MakeChip("Not logged in");
            _statusChip.style.marginLeft = 16;
            navBar.Add(_statusChip);

            screens.Add(navBar);

            var routeSlot = new VisualElement { name = "route-slot" };
            routeSlot.style.flexGrow = 1f;
            screens.Add(routeSlot);

            _router = new SusRouter();
            _router.Init(SusBootstrap.GetOrCreateOverlay(root));

            _router.BeforeEach((from, to) =>
            {
                if (to.FullPath == "/home") return true;
                if (!_isLoggedIn)
                {
                    Debug.Log($"[Guard] beforeEach BLOCKED: {from.FullPath} → {to.FullPath}");
                    _statusChip.text = "Login required";
                    return false;
                }
                return true;
            });

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

            _router.Mount(routeSlot, "/home");

            navTabs.OnChanged += path =>
            {
                var result = _router.Push(path);
                navTabs.SetValue(_router.CurrentRoute.Value?.Record?.Path ?? "/home");
                if (result == NavigationResult.Busy)
                    Debug.Log("[Guards] Router busy — request dropped.");
            };

            _router.CurrentRoute.Changed += (o, n) =>
            {
                if (n?.Record?.Path != null)
                    navTabs.SetValue(n.Record.Path);
                UpdateStatusChip();
            };

            Debug.Log("[Guards] Ready.");
        }

        private void UpdateStatusChip()
        {
            _statusChip.text = _isLoggedIn ? "Logged in" : "Not logged in";
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

        private class AdminGuard : ISusRouteGuard
        {
            public bool CanEnter(SusRoute from, SusRoute to) => true;

            public bool CanLeave(SusRoute from, SusRoute to)
            {
                if (AdminScreen.IsDirty)
                {
                    Debug.Log("[AdminGuard] CanLeave: blocked — form is dirty");
                    if (!AdminScreen.IsShowingLeaveModal)
                        AdminScreen.ShowLeaveConfirmation?.Invoke(from, to);
                    return false;
                }
                return true;
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

                Add(new Label("Navigation Guards")
                {
                    style = { fontSize = 28, color = Color.white, marginBottom = 20 },
                });

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

                var adminBtn = MakeButton("Go to Admin");
                adminBtn.clicked += () => Router.Push("/admin");
                row.Add(adminBtn);

                var profileBtn = MakeButton("Go to Profile");
                profileBtn.style.marginLeft = 12;
                profileBtn.clicked += () => Router.Push("/profile");
                row.Add(profileBtn);

                Add(row);
            }
        }

        internal class AdminScreen : SusScreen
        {
            public static bool IsDirty { get; private set; }
            public static bool IsShowingLeaveModal { get; private set; }
            public static Action<SusRoute, SusRoute> ShowLeaveConfirmation { get; set; }

            private TextField _titleField;
            private Label _dirtyChip;

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

                Add(new Label("Admin Panel")
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

                _titleField = new TextField("Title");
                _titleField.value = "";
                _titleField.RegisterValueChangedCallback(_ =>
                {
                    if (!string.IsNullOrEmpty(_titleField.value))
                    {
                        IsDirty = true;
                        _dirtyChip.text = "Unsaved changes";
                        _dirtyChip.style.display = DisplayStyle.Flex;
                    }
                });
                _titleField.style.marginBottom = 12;
                Add(_titleField);

                _dirtyChip = MakeChip("Unsaved changes");
                _dirtyChip.style.display = DisplayStyle.None;
                _dirtyChip.style.marginBottom = 8;
                Add(_dirtyChip);

                var saveBtn = MakeButton("Save");
                saveBtn.clicked += () =>
                {
                    IsDirty = false;
                    _dirtyChip.text = "Saved";
                    _dirtyChip.style.display = DisplayStyle.Flex;
                    schedule.Execute(() =>
                    {
                        _dirtyChip.style.display = DisplayStyle.None;
                    }).StartingIn(2000);
                };
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

                var overlay = new VisualElement();
                overlay.style.position = Position.Absolute;
                overlay.style.left = 0;
                overlay.style.top = 0;
                overlay.style.right = 0;
                overlay.style.bottom = 0;
                overlay.style.backgroundColor = new Color(0, 0, 0, 0.5f);
                overlay.style.alignItems = Align.Center;
                overlay.style.justifyContent = Justify.Center;

                var card = Card();
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

                void CloseModal()
                {
                    IsShowingLeaveModal = false;
                    overlay.RemoveFromHierarchy();
                }

                var stayBtn = MakeButton("Stay");
                stayBtn.clicked += CloseModal;
                btnRow.Add(stayBtn);

                var leaveBtn = MakeButton("Leave");
                leaveBtn.style.marginLeft = 12;
                leaveBtn.clicked += () =>
                {
                    IsDirty = false;
                    CloseModal();
                    Router.Push(to.FullPath);
                };
                btnRow.Add(leaveBtn);
                card.Add(btnRow);

                overlay.Add(card);
                Add(overlay);
            }
        }

        internal class ProfileScreen : SusScreen
        {
            protected override void Build()
            {
                style.flexGrow = 1f;
                style.paddingTop = 32;
                style.paddingLeft = 32;
                style.paddingRight = 32;

                Add(new Label("Profile")
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
                var idLabel = new Label("User: demo@example.com");
                idLabel.style.fontSize = 20;
                idLabel.style.color = Color.cyan;
                idCard.Add(idLabel);

                var roleChip = MakeChip("Role: Editor");
                roleChip.style.marginTop = 8;
                idCard.Add(roleChip);

                Add(idCard);
            }
        }

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
}

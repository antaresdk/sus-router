using UnityEngine;
using UnityEngine.UIElements;
using Sharq.Core;
using Sharq.Router;

/// <summary>
/// SusRouteLink example: clickable links with automatic toggling of
/// CSS classes router-link-active / router-link-exact-active.
///
/// Three links in a Row: Home, Battle, Settings. The active link is highlighted
/// (green background for exact-active, gray for active).
///
/// Standalone: [RequireComponent(typeof(UIDocument))], picks up UIDocument itself.
///
/// Keys: H=Home B=Battle S=Settings ←=Back →=Forward
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class RouteLinkExample : MonoBehaviour
{
    private UIDocument _uiDocument;
    private SusRouter _router;

    // Links shown in the UI
    private SusRouteLink _homeLink;
    private SusRouteLink _battleLink;
    private SusRouteLink _settingsLink;

    private void Start()
    {
        _uiDocument = GetOrCreateUIDocument();
        var root = _uiDocument.rootVisualElement;

        // Unified bootstrap: token cascade + Dark theme; content (router mount into a
        // sub-container + links) built in Configure so the theme applies after mount.
        SusApp.Create(root)
              .UseTheme(SusTheme.Dark)
              .Configure(BuildContent)
              .Run();
    }

    private void BuildContent(VisualElement root)
    {
        _router = new SusRouter();

        _router.Register("/home", typeof(LabelScreen));
        _router.Register("/battle/:id", typeof(LabelScreen));
        _router.Register("/settings", typeof(LabelScreen));

        // Navigation links
        var navbar = new VisualElement();
        navbar.style.flexDirection = FlexDirection.Row;
        navbar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        navbar.style.paddingTop = 8; navbar.style.paddingBottom = 8;
        navbar.style.paddingLeft = 16; navbar.style.paddingRight = 16;

        _homeLink = CreateLink("Home", "/home", exact: true);
        _battleLink = CreateLink("Battle", "/battle/42", exact: false);
        _settingsLink = CreateLink("Settings", "/settings", exact: true);

        navbar.Add(_homeLink);
        navbar.Add(_battleLink);
        navbar.Add(_settingsLink);

        // Content container
        var contentContainer = new VisualElement();
        contentContainer.name = "content";
        contentContainer.style.flexGrow = 1f;

        root.Add(navbar);
        root.Add(contentContainer);

        // Mount router into the content container
        var result = _router.Mount(contentContainer, "/home");
        Debug.Log($"[RouteLink] Mount: {result}");

        // Bind links to the router
        _homeLink.Bind(_router);
        _battleLink.Bind(_router);
        _settingsLink.Bind(_router);

        AddKeyHint("H Home    B Battle    S Settings    ← Back    → Forward    L Log");
    }

    private void Update()
    {
        if (_router == null) return;

        if (Input.GetKeyDown(KeyCode.H)) _router.Push("/home");
        if (Input.GetKeyDown(KeyCode.B)) _router.Push("/battle/42");
        if (Input.GetKeyDown(KeyCode.S)) _router.Push("/settings");
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            Debug.Log($"[RouteLink] Back → {_router.Back()}");
        if (Input.GetKeyDown(KeyCode.RightArrow))
            Debug.Log($"[RouteLink] Forward → {_router.Forward()}");
        if (Input.GetKeyDown(KeyCode.L))
            Debug.Log($"[RouteLink] Current: {_router.CurrentRoute.Value?.FullPath}, " +
                $"CanGoBack={_router.CanGoBack}, CanGoForward={_router.CanGoForward}");
    }

    private SusRouteLink CreateLink(string text, string to, bool exact)
    {
        var link = new SusRouteLink { To = to, Exact = exact };

        var label = new Label(text);
        label.style.fontSize = 18;
        label.style.paddingTop = 4; label.style.paddingBottom = 4;
        label.style.paddingLeft = 12; label.style.paddingRight = 12;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        link.Add(label);

        // Subscribe to route changes to highlight active link
        _router.CurrentRoute.Changed += (o, n) =>
        {
            var isExact = n?.FullPath == to;
            // Style: exact match = green bg; active (parent matches) = gray bg
            label.style.backgroundColor = isExact
                ? new Color(0.2f, 0.6f, 0.2f)
                : (n != null && n.FullPath.StartsWith(to))
                    ? new Color(0.4f, 0.4f, 0.4f)
                    : Color.clear;
            label.style.color = (isExact || (n != null && n.FullPath.StartsWith(to)))
                ? Color.white
                : new Color(0.7f, 0.7f, 0.7f);
        };

        return link;
    }

    private void AddKeyHint(string text)
    {
        var hint = new Label(text)
        {
            name = "key-hint",
            pickingMode = PickingMode.Ignore
        };
        hint.style.position = Position.Absolute;
        hint.style.left = 0;
        hint.style.right = 0;
        hint.style.bottom = 0;
        hint.style.paddingTop = 8;
        hint.style.paddingBottom = 8;
        hint.style.paddingLeft = 12;
        hint.style.paddingRight = 12;
        hint.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        hint.style.color = new Color(0.9f, 0.9f, 0.95f);
        hint.style.fontSize = 14;
        hint.style.unityTextAlign = TextAnchor.MiddleCenter;
        hint.style.whiteSpace = WhiteSpace.Normal;
        _uiDocument.rootVisualElement.Add(hint);
    }

    private UIDocument GetOrCreateUIDocument()
    {
        var doc = GetComponent<UIDocument>();
        var ps = Resources.Load<PanelSettings>("PanelSettings");
        if (ps != null)
            doc.panelSettings = ps;
        else
            doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        return doc;
    }

    internal class LabelScreen : SusScreen
    {
        protected override void Build()
        {
            style.flexGrow = 1f; style.alignItems = Align.Center; style.justifyContent = Justify.Center;
            Add(new Label("~ Screen ~") { style = { color = Color.white, fontSize = 32, unityTextAlign = TextAnchor.MiddleCenter } });
        }
        protected override bool OnBeforeEnter(SusRoute from)
        {
            Debug.Log($"[Screen] {GetType().Name} ← {from.FullPath}");
            return true;
        }
    }
}

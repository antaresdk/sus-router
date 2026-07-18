# Plan: working scenes of SusRouter examples

> Date: 2026-07-06  
> Package: `com.sharq-it.sus.router`  
> Dependencies: `sus-core`, `sus-kit`  
> Status: 🔵 Planning

---

## 0. State BEFORE

| What | Where | Status |
|---|---|---|
| Router documentation | `sus-router/docs/` | ❌ Empty (0 files) |
| Router examples | `sus-router/Samples~/` | ❌ Empty (0 files) |
| Whale example (standard) | `sus-kit/Samples~/BasicKit/` | ✅ Done |

### Reference pattern from BasicKitExample

Each router example will follow the same pattern:```csharp
public class ExampleName : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;

    private void OnEnable()
    {
        try { BuildUI(); }
        catch (System.Exception ex) { Debug.LogError($"[Example] ... {ex}"); }
    }

    private void BuildUI()
    {
        var doc = _uiDocument != null ? _uiDocument : GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        root.style.flexGrow = 1f;
        root.style.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

        // 1. Token cascade (sus-core + sus-kit tokens)
        SusBootstrap.LoadTokenCascade(root);

        // 2. Create OverlayHost (last sibling → always on top)
        var overlayHost = SusBootstrap.GetOrCreateOverlay(root);

        // 3. Create a router
        var router = new SusRouter();
        router.Init(overlayHost);

        // 4. Register routes
        router.Register("/home", typeof(HomeScreen));

        // 5. Mount - router itself will create SusRouteView inside root
        router.Mount(root, "/home");
    }
}
```### Using kit components

All screens inside the examples use `sus-kit` components:```csharp
internal class HomeScreen : SusScreen
{
    protected override void Build()
    {
        style.flexGrow = 1f;

        var title = new Label("Home") { style = { fontSize = 28, color = Color.white, marginBottom = 16 } };
        Add(title);

        var btn = new SusButton { Label = "Go to About", Color = SusButtonColor.Primary };
        btn.RegisterCallback<ClickEvent>(_ => Router.Push("/about"));
        Add(btn);
    }
}
```---

## 1. BasicRouting - simple navigation

**File:** `sus-router/Samples~/BasicRouting/BasicRoutingExample.cs`  
**Scene:** `BasicRouting.unity` (GameObject + UIDocument with PanelSettings)  
**Demonstrates:** Push, Replace, Back, Home, current route header

### Scheme```
┌─────────────────────────────────────────────────┐
│ [◀ Back] [🏠 Home] ║ Current: /home │ ← navbar (SusButton + Label)
├─────────────────────────────────────────────────┤
│                                                 │
│  [Go to About]         (Push)                   │  ← SusButton.Primary
│  [Go to Contact]       (Push)                   │  ← SusButton.Secondary
│  [Replace with Settings]                        │  ← SusButton.Warning
│                                                 │
│  <SusRouteView />                               │
│   — HomeScreen:    "Home Page"  + hero Card     │
│   — AboutScreen:   "About Us"   + text          │
│   — ContactScreen: "Contact"    + form          │
│   — SettingsScreen:"Settings"   + SusToggle     │
└─────────────────────────────────────────────────┘
```### Screens

| Screen | Path | Contents (whale components) |
|---|---|---|
| `HomeScreen` | `/home` | `SusButton("Go to About", Primary)` + `SusButton("Go to Contact", Secondary)` + `SusButton("Replace with Settings", Warning)` + `SusChip("Home")` |
| `AboutScreen` | `/about` | `SusLink(Text="Back to Home", Route="/home")` + `SusImg(Rounded)` |
| `ContactScreen` | `/contact` | `SusTextfield(Label="Name")` + `SusTextfield(Label="Email")` + `SusButton("Submit", Success)` |
| `SettingsScreen` | `/settings` | `SusToggle(Label="Dark theme")` + `SusToggle(Label="Notifications")` |

### Navbar

- `SusTabs` - 4 tabs: Home, About, Contact, Settings. `OnTabChanged` → `Router.Push("/xxx")`
- `SusButton` "◀ Back" → `Router.Back()`. `display` = `none` if `!Router.CanGoBack`
- `SusChip` with text `Router.CurrentRoute.Value.FullPath` (reactive via Prop)```csharp
// Navbar via SusTabs
var navTabs = new SusTabs();
navTabs.Items.Value = new List<TabItem>
{
    new() { Title = "Home", Value = "/home" },
    new() { Title = "About", Value = "/about" },
    new() { Title = "Contact", Value = "/contact" },
    new() { Title = "Settings", Value = "/settings" },
};
navTabs.OnTabChanged += path => Router.Push(path);
```### Props (settings via GameObject)```csharp
[SerializeField] private UIDocument _uiDocument;
[SerializeField] private PanelSettings _panelSettings;  // can be loaded from Resources
```### Tasks

- [ ] **1.1** `BasicRoutingExample.cs` — MonoBehaviour + BuildUI + router setup
- [ ] **1.2** `HomeScreen` - 3 buttons (Go to About / Go to Contact / Replace with Settings) + SusChip
- [ ] **1.3** `AboutScreen` — SusLink + SusImg
- [ ] **1.4** `ContactScreen` - 2 SusTextfield + Submit
- [ ] **1.5** `SettingsScreen` — 2 SusToggle
- [ ] **1.6** Navbar: SusTabs (4 tabs) + Back (with CanGoBack condition) + CurrentRoute chip
- [ ] **1.7** Scene `BasicRouting.unity` (GameObject + UIDocument)
- [ ] **1.8** `package.json` — add sample "Basic Routing Example"

---

## 2. KeepAlive - screen caching

**File:** `sus-router/Samples~/KeepAlive/KeepAliveExample.cs`  
**Scene:** `KeepAlive.unity`  
**Demonstrates:** `KeepAlive = true` - the screen state is saved when leaving

### Scheme```
┌─────────────────────────────────────────────────┐
│ [Counter] [Form] [Settings] │ ← tabs (SusButton in a row)
├─────────────────────────────────────────────────┤
│  <SusRouteView />                                │
│                                                  │
│  CounterScreen (KeepAlive):                      │
│   "Count: 42"  [+1]  [Reset]                     │
│ When leaving on Form and returning, the counter is the same │
│                                                  │
│  FormScreen (KeepAlive):                         │
│   SusTextfield "Name"                            │
│ When leaving and returning, the text is not reset │
│                                                  │
│ SettingsScreen (WITHOUT KeepAlive): │
│   SusToggle "Dark mode"                          │
│ When leaving and returning, the state is reset │
└─────────────────────────────────────────────────┘
```### Screens

| Screen | KeepAlive | What are we testing |
|---|---|---|
| `CounterScreen` | ✅ | The counter does not reset |
| `FormScreen` | ✅ | `SusTextfield` (text is not lost) |
| `SettingsScreen` | ❌ | State reset (control) |

### Key Points

- **SusTabs** for navigation: 3 tabs (Counter, Form, Settings). `OnTabChanged` → `Router.Push("/counter")` etc.
- `CounterScreen`: `SusButton("+1")` increments the `_count` field, `Label` shows the value. `_count` - regular class field (not Prop) - **will not be reset during KeepAlive**
- `FormScreen`: `SusTextfield` with `Model` prop. **Won't reset** when KeepAlive
- `SettingsScreen`: `SusToggle` **will be reset** when recreated (without KeepAlive)
- Navbar: SusChip shows `[K]` for KeepAlive screens```csharp
// Navbar via SusTabs with KeepAlive indicator
var tabs = new SusTabs();
tabs.Items.Value = new List<TabItem>
{
    new() { Title = "[K] Counter", Value = "/counter" },    // KeepAlive
    new() { Title = "[K] Form", Value = "/form" },          // KeepAlive
new() { Title = "Settings", Value = "/settings" }, // without KeepAlive
};
tabs.OnTabChanged += path => Router.Push(path);
```### Tasks

- [ ] **2.1** `KeepAliveExample.cs` - MonoBehaviour + router with KeepAlive configs
- [ ] **2.2** `CounterScreen` - counter + buttons + KeepAlive
- [ ] **2.3** `FormScreen` - SusTextfield + KeepAlive
- [ ] **2.4** `SettingsScreen` - SusToggle WITHOUT KeepAlive (control)
- [ ] **2.5** Navbar with tabs + `[K]`-indicator
- [ ] **2.6** Scene `KeepAlive.unity`

---

## 3. Guards - protecting routes

**File:** `sus-router/Samples~/Guards/GuardsExample.cs`  
**Scene:** `Guards.unity`  
**Demonstrates:** `BeforeEach`, `CanEnter`, `CanLeave` (dirty form), `BeforeResolve`

### Scenario

1. User at `/home`. Clicks "Go to Admin" → **beforeEach** checks "isLoggedIn". If not → shows "Login required" (SusChip.Error) + Aborted
2. "Login" (SusToggle "Logged in") -> beforeEach skips
3. On `/admin` there is a form (`SusTextfield`). If the text is changed (dirty) → `CanLeave` returns false → SusModal "Discard changes?" (we use SusModal from whale!)
4. `BeforeResolve`: redirect `/old-admin` → `/admin` (demo)

### Screens

| Screen | Path | Guard |
|---|---|---|
| `HomeScreen` | `/home` | — |
| `AdminScreen` | `/admin` | `CanEnter` checks the role "admin" (from props) |
| `ProfileScreen` | `/profile/:id` | `CanEnter` shows `id` from URL |
| `OldAdminScreen` | `/old-admin` | Redirect → `/admin` (via `beforeResolve`) |

### Key Points

- **beforeEach**: checks `_isLoggedIn` bool against MonoBehaviour. If false - all Push are interrupted
- **CanLeave**: `AdminScreen` checks `_isDirty`. If true → `SusModal` with SusButton "Discard" / "Cancel"
- **SusModal from whale**: `SusModal` component: `<SusModal Model="{showModal}" Persistent="false">`
- **beforeResolve**: redirect `/old-admin` → `NavigationResult.Redirect` (if supported) or `router.Replace("/admin")` in the hook

### Tasks

- [ ] **3.1** `GuardsExample.cs` — MonoBehaviour + `_isLoggedIn` toggle + router with guards
- [ ] **3.2** `HomeScreen` - "Go to Admin" (can be blocked) + "Login" SusToggle
- [ ] **3.3** `AdminScreen` — SusTextfield + dirty tracking + `CanLeave` guard
- [ ] **3.4** `ProfileScreen` - shows `:id` from URL via SusChip
- [ ] **3.5** SusModal "Discard changes?" - when trying to leave a dirty form
- [ ] **3.6** Redirect `/old-admin` → `/admin` via beforeResolve
- [ ] **3.7** `Guards.unity`

---

## 4. Modals + Transitions - modals and animations

**File:** `sus-router/Samples~/Modals/ModalsExample.cs`  
**Scene:** `Modals.unity`  
**Demonstrates:** `SusModal` (from whale), `SusRouteTransition.Fade/SlideLeft/SlideRight`, `NavigateWithTransition`

### Scheme```
┌─────────────────────────────────────────────────┐
│ [Fade] [Slide→] [Slide←] [None] ║ /page-1 │ ← transition selection buttons
├─────────────────────────────────────────────────┤
│  <SusRouteView />                                │
│   Page1Screen — "Page 1"                         │
│   Page2Screen — "Page 2"                         │
│   Page3Screen — "Page 3"                         │
│                                                  │
│ [Open Modal (info)] — SusModal (kit) │
│  [Open Modal (confirm)] — SusModal + form        │
│ [Stack modals] - 3 modals in a row │
└─────────────────────────────────────────────────┘
```### Screens

| Screen | Path |
|---|---|
| `Page1Screen` | `/page-1` |
| `Page2Screen` | `/page-2` |
| `Page3Screen` | `/page-3` |

###Modals

| Modalka | Component | Contents |
|---|---|---|
| Info modal | `SusModal` | `SusIcon("info")` + text + `SusButton("OK")` |
| Confirm modal | `SusModal(Persistent=false)` | `SusTextfield` + `SusButton("Confirm", Danger)` + `SusButton("Cancel")` |
| Stack test | 3 × `SusModal` | Sequentially open, one at a time closed (LIFO) |

### Transitions

- `router.NavigateWithTransition(path, duration: 0.3f)` - with animation
- Selecting animation via `SusSelect` or `SusButton`-radio-group
- `SusRouteTransition.Fade()` / `.SlideLeft()` / `.SlideRight()` / `.None()`

### Tasks

- [ ] **4.1** `ModalsExample.cs` - MonoBehaviour + router with transitions
- [ ] **4.2** PageScreens (3 pieces) - minimal: `SusChip("Page N")`
- [ ] **4.3** Transition selection panel: `SusSelect` or `SusButton`-group
- [ ] **4.4** Info modal - `SusModal` + SusIcon + text + OK
- [ ] **4.5** Confirm modal - `SusModal` + SusTextfield + Confirm/Cancel
- [ ] **4.6** Stack test - 3 modals in a row, closing LIFO
- [ ] **4.7** `Modals.unity`

---

## 5. Nested + Named - nested and named routes

**File:** `sus-router/Samples~/NestedRouting/NestedRoutingExample.cs`  
**Scene:** `NestedRouting.unity`  
**Demonstrates:** child routes, `PushNamed`, URL parameters (`:id`), query parameters, Redirect, Alias

### Scheme```
┌─────────────────────────────────────────────────┐
│ [Dashboard] [Users] [Settings] │ ← top-level tabs
├─────────────────────────────────────────────────┤
│ <SusRouteView /> (main) │
│                                                  │
│  DashboardScreen ("/dashboard"):                 │
│   SusCard "Welcome!" + SusChip(stats)            │
│                                                  │
│  UsersScreen ("/users"):                         │
│ [← Back to Users] (if on a child) │
│ <SusRouteView /> (child) │
│ /users → list │
│ /users/42 → user details #42 │
│ /users/search?q=john → search │
│                                                  │
│  SettingsScreen ("/settings"):                   │
│ SusTabs (Profile / Privacy) │ ← SusTabs with OnTabChanged → Router.Push
│ <SusRouteView /> (child) │
│    /settings/profile  → SusForm                  │
│    /settings/privacy  → SusToggle × 3           │
│                                                  │
│  Alias: /menu → /dashboard                       │
│  Redirect: /old-users → /users                   │
│  Named: PushNamed("user-detail", new {id=42})    │
│         → /users/42                              │
└─────────────────────────────────────────────────┘
```### Routes```csharp
// Main
router.Register("/dashboard", typeof(DashboardScreen));
router.Register("/users", typeof(UsersScreen), new SusRouteConfig {
    Name = "users",
    Children = {
        router.Register("/users/:id", typeof(UserDetailScreen), new SusRouteConfig { Name = "user-detail" }),
        router.Register("/users/search", typeof(UserSearchScreen)),
    }
});
router.Register("/settings", typeof(SettingsScreen), new SusRouteConfig {
    Children = {
        router.Register("/settings/profile", typeof(ProfileScreen)),
        router.Register("/settings/privacy", typeof(PrivacyScreen)),
    }
});

// Alias + Redirect
router.Register("/menu", typeof(DashboardScreen), new SusRouteConfig { Alias = "/dashboard" });
router.Register("/old-users", typeof(UsersScreen), new SusRouteConfig { Redirect = "/users" });
```### Key Points

- `UserDetailScreen`: reads `:id` from URL → `GetParam("id")` → shows in `SusChip`
- `UserSearchScreen`: reads `?q=` → `GetQuery("q")` → shows in `SusTextfield`
- Navigation by name: `Router.PushNamed("user-detail", new() { ["id"] = 42 })`
- Alias `/menu` - opens, but the URL remains `/menu`, the screen is `DashboardScreen`
- Redirect `/old-users` - with Push → automatically `/users`

### Tasks

- [ ] **5.1** `NestedRoutingExample.cs` — MonoBehaviour + router with nested, named, alias, redirect
- [ ] **5.2** `DashboardScreen` — SusCard + SusChip(stats)
- [ ] **5.3** `UsersScreen` - list (SusChip × N) + child SusRouteView
- [ ] **5.4** `UserDetailScreen` - user details `:id` from URL
- [ ] **5.5** `UserSearchScreen` - search with `?q=` query parameter
- [ ] **5.6** `SettingsScreen` - child tabs via SusTabs (Profile/Privacy) + OnTabChanged → Router.Push
- [ ] **5.7** `ProfileScreen` - SusForm with SusTextfield
- [ ] **5.8** `PrivacyScreen` — SusToggle × 3
- [ ] **5.9** Alias `/menu` → `/dashboard` and Redirect `/old-users` → `/users`
- [ ] **5.10** `NestedRouting.unity`

---

## 6. FullDemo - full demonstration

**File:** `sus-router/Samples~/FullDemo/FullDemoExample.cs`  
**Scene:** `FullDemo.unity`  
**Demonstrates:** EVERYTHING together - all the features of the router + maximum kit components in one example

### Scheme```
┌───────────────────────────────────────────────────────────────┐
│  ┌─ Sidebar ───────────────────────────┐  ┌─ Content ───────┐│
│  │                                     │  │                 ││
│  │  SusIcon("sus-logo")                │  │  <SusRouteView> ││
│  │                                     │  │                 ││
│ │ ── Navigation ── │ │ Current screen ││
│ │ SusTabs (vertical, align-start) │ │ + its content ││
│  │   ● Dashboard                      │  │                 ││
│  │   ● Users                          │  │                 ││
│  │   ● Settings                       │  │                 ││
│  │   ● About                          │  │                 ││
│  │                                     │  │                 ││
│  │  ── Actions ──                      │  │                 ││
│  │  [Open Modal] SusButton            │  │                 ││
│  │                                     │  │                 ││
│  │  ── Theme ──                        │  │                 ││
│  │  [🌙 Dark / ☀️ Light] SusToggle    │  │                 ││
│  │                                     │  │                 ││
│  │  ── Info ──                         │  │                 ││
│  │  Current: /dashboard               │  │                 ││
│  │  Guards: 2  Routes: 8             │  │                 ││
│  │  [Back] [Forward]                  │  │                 ││
│  └─────────────────────────────────────┘  └─────────────────┘│
└───────────────────────────────────────────────────────────────┘
```### Sidebar (left, fixed)

| Element | Whale Component |
|---|---|
| Logo | `SusIcon("sus-logo", XLarge)` |
| Navigation | `SusTabs(Direction="vertical")` with 4 tabs (Dashboard, Users, Settings, About). `OnTabChanged` → `Router.Push(path)` |
| Modalka | `SusButton("Open Modal", Primary)` → `router.Modal()` |
| Topic | `SusToggle(Label="Dark")` → `SusThemeService.SetTheme(root, ...)` |
| History | `SusButton("◀")` / `SusButton("▶")` + `SusChip("Current: /xxx")` |
| Statistics | `SusChip("Routes: 8")` + `SusChip("Guards: 2")` |

### Screens (right, via SusRouteView)

| Screen | Kit components |
|---|---|
| `DashboardScreen` | `SusCard` × 3 (statistics) + `SusChip` (status) + `SusButton("View Users")` |
| `UsersScreen` | `SusTable` or `SusChip` list (Users) + `SusTextfield(Searchable)` + child `SusRouteView` |
| `UserDetailScreen` | `SusForm` (name, role, email) + `SusToggle(Active)` + `SusButton("Save")` |
| `SettingsScreen` | `SusForm` + `SusSelect(Theme)` + `SusToggle × 3` + `SusCheckbox × 2` |
| `AboutScreen` | `SusImg` + `SusLink` × 3 (links) + `SusChip` (version) |

### Key Points

- **Full lifecycle**: each screen logs `BeforeEnter` → `Entered` → `BeforeLeave` → `Left`
- **KeepAlive**: `DashboardScreen` and `UsersScreen` - KeepAlive
- **Guards**: beforeEach checks for "isLoggedIn"; CanLeave on UserDetailScreen (dirty form)
- **Modals**: SusModal "About app" on `/about`
- **Transitions**: Fade between all screens
- **Named routes**: `PushNamed("user-detail", new {id=42})`
- **Nested**: `/users` → `/users/42` - via child SusRouteView
- **Theme**: SusToggle toggles Dark/Light via SusThemeService

### Tasks

- [ ] **6.1** `FullDemoExample.cs` - MonoBehaviour with sidebar + content
- [ ] **6.2** Sidebar: logo, SusTabs(Direction="vertical")-navigation, SusButton-modal, SusToggle-theme, Back/Forward, statistics
- [ ] **6.3** `DashboardScreen` - SusCard × 3 + SusChip + button
- [ ] **6.4** `UsersScreen` - list of users + child SusRouteView
- [ ] **6.5** `UserDetailScreen` - SusForm with SusTextfield × 3 + SusToggle + SusCheckbox
- [ ] **6.6** `SettingsScreen` — SusSelect + SusToggle × 3 + SusCheckbox × 2
- [ ] **6.7** `AboutScreen` - SusImg + SusLink × 3 + SusChip
- [ ] **6.8** Integration of all features: KeepAlive, guards, modals, transitions, named, nested
- [ ] **6.9** `FullDemo.unity`

---

## 7. General tasks (for all examples)

### 7.1 Directory structure```
sus-router/
└── Samples~/
    ├── BasicRouting/
    │   ├── BasicRoutingExample.cs
    │   ├── BasicRouting.unity
    │   └── com.sharq-it.sus.router.examples.basic.asmdef
    ├── KeepAlive/
    │   ├── KeepAliveExample.cs
    │   ├── KeepAlive.unity
    │   └── com.sharq-it.sus.router.examples.keepalive.asmdef
    ├── Guards/
    │   ├── GuardsExample.cs
    │   ├── Guards.unity
    │   └── com.sharq-it.sus.router.examples.guards.asmdef
    ├── Modals/
    │   ├── ModalsExample.cs
    │   ├── Modals.unity
    │   └── com.sharq-it.sus.router.examples.modals.asmdef
    ├── NestedRouting/
    │   ├── NestedRoutingExample.cs
    │   ├── NestedRouting.unity
    │   └── com.sharq-it.sus.router.examples.nested.asmdef
    └── FullDemo/
        ├── FullDemoExample.cs
        ├── FullDemo.unity
        └── com.sharq-it.sus.router.examples.fulldemo.asmdef
```### 7.2 asmdef template```json
{
    "name": "com.sharq-it.sus.router.examples.basic",
    "references": [
        "com.sharq-it.sus.core",
        "com.sharq-it.sus.router",
        "com.sharq-it.sus.kit"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```### 7.3 Scene .unity template```
GameObject "ExampleName"
├── Transform
└── UIDocument (component)
└── PanelSettings: loaded from Resources/PanelSettings
or SerializeField (as in BasicKitExample)

PanelSettings asset:
    - Scale Mode: Scale With Screen Size
    - Reference Resolution: 1920×1080
    - renderMode: ScreenSpaceOverlay (default)
```### 7.4 package.json

Add 6 samples to `sus-router/package.json`:```json
"samples": [
    {
        "displayName": "Basic Routing",
"description": "Push / Replace / Back / Home - simple navigation",
        "path": "Samples~/BasicRouting"
    },
    {
        "displayName": "KeepAlive",
"description": "Screen caching - counters and forms are not reset",
        "path": "Samples~/KeepAlive"
    },
    {
        "displayName": "Guards",
"description": "BeforeEach, CanEnter, CanLeave, BeforeResolve - route protection",
        "path": "Samples~/Guards"
    },
    {
        "displayName": "Modals & Transitions",
"description": "SusModal from sus-kit + Fade/Slide animation",
        "path": "Samples~/Modals"
    },
    {
        "displayName": "Nested & Named Routes",
"description": "Child routes, PushNamed, URL parameters, redirect, alias",
        "path": "Samples~/NestedRouting"
    },
    {
        "displayName": "Full Demo",
"description": "ALL router features + maximum sus-kit components in one example",
        "path": "Samples~/FullDemo"
    }
]
```### Tasks

- [ ] **7.1** Create `.asmdef` for each example (6 pieces)
- [ ] **7.2** Create `.unity` scenes (6 pieces) - GameObject + UIDocument with PanelSettings
- [ ] **7.3** Update `sus-router/package.json` - add 6 samples
- [ ] **7.4** Delete old empty directories (if there is garbage)

---

## 8. Final table

| # | Example | Files | Features | Kit components |
|---|---|---|---|---|
| 1 | BasicRouting | 1 cs + 1 unity + 1 asmdef | Push, Replace, Back, Home | SusTabs, SusButton, SusChip, SusLink, SusImg, SusTextfield, SusToggle |
| 2 | KeepAlive | 1 cs + 1 unity + 1 asmdef | KeepAlive = true/false | SusTabs, SusButton, SusTextfield, SusToggle, SusChip |
| 3 | Guards | 1 cs + 1 unity + 1 asmdef | beforeEach, CanEnter, CanLeave, beforeResolve, redirect | SusTabs, SusButton, SusToggle, SusTextfield, SusChip, SusModal |
| 4 | Modals+Transitions | 1 cs + 1 unity + 1 asmdef | SusModal, Fade/Slide, NavigateWithTransition | SusTabs, SusButton, SusModal, SusIcon, SusTextfield, SusSelect |
| 5 | Nested+Named | 1 cs + 1 unity + 1 asmdef | Nested children, PushNamed, :id, ?q=, alias, redirect | SusTabs, SusChip, SusTextfield, SusToggle, SusForm, SusButton |
| 6 | FullDemo | 1 cs + 1 unity + 1 asmdef | EVERYTHING + theming via SusThemeService | SusTabs(vertical), SusCard, SusForm, SusSelect, SusCheckbox, SusLink, SusIcon, SusImg, SusButton, SusToggle, SusChip, SusTextfield, SusModal |

**Total: 6 examples, 6 scenes, 6 asmdef, ~20 screens, 18+ files**
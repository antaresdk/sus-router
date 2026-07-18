# 10. Running the samples (Samples~)

The package ships **7 standalone samples** under `Samples~/`. All samples use ` sus-kit`components (SusTabs, SusButton, SusChip, SusToggle, SusTextfield, SusModal, SusRouteLink).

## Requirements

- `sus-router` + ` sus-kit` + ` sus-core`installed in the project
- UPM Samples imported: Window → Package Manager → SusRouter → Samples → Import
- Scene with UIDocument (EventSystem required)
- Each sample: `[RequireComponent(typeof(UIDocument))]`

## Scene setup

1. Create GameObject → Add Component → `UIDocument`
2. Add Component → sample script (e.g. `BasicRoutingExample`)
3. GameObject → UI → Event System (if missing)
4. Play

## Overview

| # | Sample | Router features | sus-kit components |
|---|---|---|---|
| 1 | BasicRouting | Push, Replace, Back, Home, CurrentRoute | SusTabs, SusButton, SusChip, SusRouteLink, SusTextfield, SusToggle, SusImg |
| 2 | KeepAlive | KeepAlive=true/false, caching | SusTabs, SusButton, SusTextfield, SusToggle, SusChip |
| 3 | Guards | BeforeEach, CanEnter, BeforeResolve, redirect | SusTabs, SusButton, SusToggle, SusTextfield, SusChip |
| 4 | Modals+Transitions | SusRouterModal (InfoDialog, ConfirmDialog), Fade/Slide, NavigateWithTransition | SusTabs, SusButton, SusModal |
| 5 | Nested+Named | children, PushNamed, :id, ?q=, alias, redirect, lazy | SusTabs, SusChip, SusTextfield, SusToggle, SusButton |
| 6 | RouteLink | SusRouteLink, Bind(router), router-link-active/exact-active | SusRouteLink, Label |
| 7 | FullDemo | EVERYTHING: KeepAlive, guards, modals, transitions, nested, named, theming | SusTabs(vertical), SusButton, SusChip, SusToggle, SusTextfield, SusModal |

---

## Sample 1: BasicRouting

**Script:** `BasicRoutingExample.cs`

Demonstrates basic navigation: Push, Replace, Back, Home, CurrentRoute display.

### Navigation — SusTabs

4 tabs: Home, About, Contact, Settings. Each tab Pushes the matching path via SusTabs.OnTabChanged.

### Screens

- **HomeScreen** — greeting + SusRouteLink to About, SusToggle, SusImg
- **AboutScreen** — description + SusRouteLink to Contact
- **ContactScreen** — SusTextfield with Prop, submit SusButton
- **SettingsScreen** — SusChip with CurrentRoute

### Action buttons

- **Back** / **Forward** — SusButton, drives navigation
- **Log** — logs CurrentRoute.FullPath

### Key code

```csharp
// SusTabs navigation
void OnTabChanged(string path)
{
    Router.Push(path);
}

// SusRouteLink
var link = new SusRouteLink { To = "/about", Text = "About" };
link.Bind(Router); // enables router-link-active
```

---

## Sample 2: KeepAlive

**Script:** `KeepAliveExample.cs`

Shows the difference between KeepAlive=true (state preserved) and false (recreated).

### Navigation — SusTabs

3 tabs: Counter [K], Form [K], Settings. [K] = KeepAlive=true.

### Screens

- **CounterScreen (KeepAlive)** — Prop\<int\> counter, SusButton +/-, multiplier SusToggle. Count survives leave/return.
- **FormScreen (KeepAlive)** — SusTextfield with Prop\<string\>. Typed text survives tab switches.
- **SettingsScreen (NOT KeepAlive)** — recreated every time.

### Key code

```csharp
Router.Register("/counter", typeof(CounterScreen),
    new SusRouteConfig { KeepAlive = true });
Router.Register("/settings", typeof(SettingsScreen)); // KeepAlive=false
```

---

## Sample 3: Guards

**Script:** `GuardsExample.cs`

Demonstrates the guard pipeline.

### Navigation — SusTabs

4 tabs: Home (always), Dashboard (authenticated), Admin (admin only), About (always). Access to Admin/Dashboard via SusToggle "Login"/"Admin".

### Guards

- **BeforeEach** — checks auth for Meta["requiresAuth"]=true
- **AuthGuard** — ISusRouteGuard.CanEnter checks "admin" role
- **BeforeResolve** — redirects `/old-admin` → `/admin`

### Screens

- **HomeScreen** — greeting + auth status (SusChip)
- **DashboardScreen** — SusTextfield "Dashboard content"
- **AdminScreen** — admin panel (admins only)
- **AboutScreen** — guard info

### Key code

```csharp
Router.BeforeEach((from, to) =>
{
    if (to.Record?.Config?.Meta?.ContainsKey("requiresAuth") == true && !_isLoggedIn.Value)
        return false; // block
    return true;
});

Router.Register("/admin", typeof(AdminScreen), new SusRouteConfig
{
    Guard = new AuthGuard(),
    Meta = new() { ["requiresAuth"] = true, ["role"] = "admin" }
});
```

---

## Sample 4: Modals & Transitions

**Script:** `ModalExample.cs`

Demonstrates SusRouterModal, SusModalService, and transition animations.

### Navigation — SusTabs

3 tabs: Home, Dashboard, About. Navigation with Fade/Slide via NavigateWithTransition.

### Modals

- **InfoDialog** — info message with SusIcon + OK SusButton
- **ConfirmDialog** — confirmation with OK/Cancel SusButtons

### Action buttons

- **Info** — show InfoDialog
- **Confirm** — show ConfirmDialog
- **Close** — SusModalService.Close()
- **Toggle Dismiss** — toggle dismissOnClickOutside

### Key code

```csharp
// Show modal
Router.ModalService.Show(typeof(InfoDialog), new() {
    ["title"] = "Information",
    ["message"] = "Welcome!"
});

// Navigate with animation (slide)
Router.NavigateWithTransition("/dashboard",
    SusRouteTransitionType.SlideLeft, 0.4f);
```

---

## Sample 5: Nested & Named Routes

**Script:** `AdvancedRoutingExample.cs`

Demonstrates named routes, nested routes, alias, redirect, query params, lazy loading.

### Navigation — SusTabs

6 tabs: Main Menu (alias), Battle (:id), Settings (nested), Search (?q=), Lazy, Old Menu (redirect).

### Capabilities

- **Named route** — `/battle/:id`, PushNamed with pathParams
- **Alias** — `/menu` → `/main-menu`
- **Redirect** — `/old-menu` → `/main-menu`
- **Nested** — `/settings/profile`, `/settings/privacy` (SusTabs inside SettingsScreen)
- **Query** — `/search?q=sus-router`
- **Lazy** — `/lazy`, LazyFactory

### Key code

```csharp
Router.Register("/battle/:id", typeof(BattleScreen), new SusRouteConfig
{
    Name = "battle",
    Transition = SusRouteTransition.SlideLeft()
});

Router.PushNamed("battle", new() { ["id"] = "42" });

// Nested routes
Router.Register("/settings", typeof(SettingsScreen), new SusRouteConfig
{
    Children = new List<SusRouteRecord>
    {
        new SusRouteRecord("profile", typeof(ProfileScreen)),
        new SusRouteRecord("privacy", typeof(PrivacyScreen)),
    }
});
```

---

## Sample 6: RouteLink

**Script:** `RouteLinkExample.cs`

Demonstrates SusRouteLink with auto-highlighting.

### Navigation — SusRouteLink

Three SusRouteLink instances on screen: Home, Battle, Settings. Each automatically gets `router-link-active` / ` router-link-exact-active`classes.

### Key code

```csharp
var homeLink = new SusRouteLink { To = "/home", Text = "Home" };
homeLink.Bind(Router); // auto-highlight

var battleLink = new SusRouteLink { To = "/battle/42", Text = "Battle" };
battleLink.Bind(Router);
```

Exact match is also available:

```csharp
var exactLink = new SusRouteLink { To = "/home", Text = "Home", Exact = true };
// router-link-exact-active only on exact /home
```

---

## Sample 7: Full Demo

**Script:** `FullDemoExample.cs`

Comprehensive sample combining ALL router features + theming.

### Layout — sidebar + content

- **Sidebar** — vertical SusTabs: Dashboard, Users, Settings, About
- **Content** — SusRouteView with screens

### Features

- KeepAlive: DashboardScreen keeps its counter
- Guards: AboutScreen only for authenticated users (Login SusToggle)
- Modals: "Logout" SusButton → ConfirmDialog
- Transitions: Fade between screens
- Nested: SettingsScreen with Profile/Privacy sub-tabs (SusTabs)
- Named: /users/:id via PushNamed
- Theming: SusChip with current theme

### Key code

```csharp
// Sidebar — vertical SusTabs
SusTabs sidebar = ...;
sidebar.Direction.Value = "vertical";
sidebar.Items.Value = new List<TabItem>
{
    new() { Id = "dashboard", Label = "Dashboard" },
    new() { Id = "users", Label = "Users" },
    new() { Id = "settings", Label = "Settings" },
    new() { Id = "about", Label = "About" },
};
sidebar.OnTabChanged += (id) => Router.Push($"/{id}");

// Modal on Logout
Router.ModalService.Show(typeof(ConfirmDialog), new() {
    ["title"] = "Logout",
    ["message"] = "Are you sure?",
    ["onConfirm"] = (Action)(() => { _isLoggedIn.Value = false; })
});
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Nothing shows | UIDocument without PanelSettings | GetOrCreateUIDocument() sets it |
| SusTabs do nothing | OnTabChanged not wired | Bind Router.Push in the handler |
| Buttons ignore clicks | No EventSystem | Add Event System to the scene |
| PushNamed not found | No Name in SusRouteConfig | Set Name = "..." |
| KeepAlive does not cache | KeepAlive not true | Set KeepAlive = true |
| router-link-active missing | SusRouteLink without Bind | Call link.Bind(router) |
| Modal does not show | No OverlayHost | router.Init(overlayHost) |
| Redirect loop | Redirect points at itself | Check redirect chain |

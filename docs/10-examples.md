# 10. Running the samples (Samples~)

The package ships **7 standalone samples** under `Samples~/`. UI chrome uses **standard Unity UI Toolkit** controls (`Button`, `Label`, `TextField`, `Toggle`, `ScrollView`) — no downstream UI package is required.

## Requirements

- `sus-router` + `sus-core` installed in the project
- UPM Samples imported: Window → Package Manager → SusRouter → Samples → Import
- Scene with UIDocument (EventSystem required)
- Each sample: `[RequireComponent(typeof(UIDocument))]`

## Scene setup

1. Create GameObject → Add Component → `UIDocument`
2. Add Component → sample script (e.g. `BasicRoutingExample`)
3. GameObject → UI → Event System (if missing)
4. Play

## Overview

| # | Sample | Router features |
|---|---|---|
| 1 | BasicRouting | Push, Replace, Back, Home, CurrentRoute |
| 2 | KeepAlive | KeepAlive=true/false, caching |
| 3 | Guards | BeforeEach, CanEnter, BeforeResolve, redirect |
| 4 | Modals+Transitions | SusRouterModal (InfoDialog, ConfirmDialog), Fade/Slide, NavigateWithTransition |
| 5 | Nested+Named | children, PushNamed, :id, ?q=, alias, redirect, lazy |
| 6 | RouteLink | SusRouteLink, Bind(router), router-link-active/exact-active |
| 7 | FullDemo | EVERYTHING: KeepAlive, guards, modals, transitions, nested, named, theming |

---

## Sample 1: BasicRouting

**Script:** `BasicRoutingExample.cs`

Demonstrates basic navigation: Push, Replace, Back, Home, CurrentRoute display.

### Navigation — UITK tab bar

4 tab `Button`s: Home, About, Contact, Settings. Each tab Pushes the matching path.

### Screens

- **HomeScreen** — greeting + navigation buttons + route chip (`Label`)
- **AboutScreen** — description + `SusRouteLink` to Home + image placeholder
- **ContactScreen** — `TextField`s + submit `Button`
- **SettingsScreen** — `Toggle`s

### Action buttons

- **Back** — UITK `Button`, drives navigation
- Current route shown as a chip `Label`

### Key code

```csharp
// tab bar navigation
void OnTabChanged(string path)
{
    Router.Push(path);
}

// SusRouteLink
var link = new SusRouteLink { To = "/about", Exact = true };
link.Bind(Router); // enables router-link-active
```

---

## Sample 2: KeepAlive

**Script:** `KeepAliveExample.cs`

Shows the difference between KeepAlive=true (state preserved) and false (recreated).

### Navigation — UITK tab bar

3 tabs: Counter [K], Form [K], Settings. [K] = KeepAlive=true.

### Screens

- **CounterScreen (KeepAlive)** — counter + `Button` +/−. Count survives leave/return.
- **FormScreen (KeepAlive)** — `TextField`s. Typed text survives tab switches.
- **SettingsScreen (NOT KeepAlive)** — `Toggle`s; recreated every time.

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

### Navigation — UITK tab bar

4 tabs: Home, Admin, Profile, OldAdmin. Auth via a `Toggle` "Logged in".

### Guards

- **BeforeEach** — blocks non-home routes when not logged in
- **AdminGuard** — `ISusRouteGuard.CanLeave` when the admin form is dirty
- **Redirect** — `/old-admin` → `/admin` via `SusRouteConfig.Redirect`

### Screens

- **HomeScreen** — instructions + try-it buttons
- **AdminScreen** — `TextField` form + dirty CanLeave confirm overlay
- **ProfileScreen** — available only when logged in

### Key code

```csharp
Router.BeforeEach((from, to) =>
{
    if (to.FullPath == "/home") return true;
    if (!_isLoggedIn) return false; // block
    return true;
});

Router.Register("/admin", typeof(AdminScreen), new SusRouteConfig
{
    Guard = new AdminGuard()
});
```

---

## Sample 4: Modals & Transitions

**Script:** `ModalExample.cs`

Demonstrates SusRouterModal, modal service, and transition animations.

### Navigation — UITK tab bar

3 tabs: Page 1–3. Navigation with Fade via NavigateWithTransition (optional `Toggle`).

### Modals

- **InfoDialog** — info message + Close `Button`
- **ConfirmDialog** — confirmation with OK/Cancel `Button`s

### Action buttons

- **Open Info** / **Open Confirm** / **Stack 3**
- **Close Top** — `ModalService.Close()`

### Key code

```csharp
// Show modal
Router.ModalService.Show(typeof(InfoDialog), new() {
    ["title"] = "Information",
    ["message"] = "Welcome!"
});

// Navigate with animation
Router.NavigateWithTransition("/page-2", 0.3f);
```

---

## Sample 5: Nested & Named Routes

**Script:** `AdvancedRoutingExample.cs`

Demonstrates named routes, nested routes, alias, redirect, query params, lazy loading.

### Navigation — UITK tab bar

5 tabs: Main Menu (alias), Battle (:id), Settings (nested), Search (?q=), Lazy.

### Capabilities

- **Named route** — `/battle/:id`, PushNamed with pathParams
- **Alias** — `/menu` → `/main-menu`
- **Redirect** — `/old-menu` → `/main-menu`
- **Nested** — `/settings/profile`, `/settings/privacy` (tab bar inside SettingsScreen)
- **Query** — `/search?q=hello&page=1`
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
        new SusRouteRecord("profile", typeof(LabelScreen)),
        new SusRouteRecord("privacy", typeof(LabelScreen)),
    }
});
```

---

## Sample 6: RouteLink

**Script:** `RouteLinkExample.cs`

Demonstrates SusRouteLink with auto-highlighting.

### Navigation — SusRouteLink

Three SusRouteLink instances on screen: Home, Battle, Settings. Each automatically gets `router-link-active` / `router-link-exact-active` classes.

### Key code

```csharp
var homeLink = new SusRouteLink { To = "/home", Exact = true };
homeLink.Bind(Router); // auto-highlight

var battleLink = new SusRouteLink { To = "/battle/42" };
battleLink.Bind(Router);
```

Exact match is also available:

```csharp
var exactLink = new SusRouteLink { To = "/home", Exact = true };
// router-link-exact-active only on exact /home
```

---

## Sample 7: Full Demo

**Script:** `FullDemoExample.cs`

Comprehensive sample combining ALL router features + theming.

### Layout — sidebar + content

- **Sidebar** — vertical UITK tab `Button`s: Dashboard, Users, Settings, About
- **Content** — `ScrollView` + mounted route host

### Features

- KeepAlive: Dashboard / Users
- Guards: UserDetailGuard on nested `:id`
- Modals: "Open Modal" → AboutDialog (`SusRouterModal`)
- Transitions: Fade between screens
- Nested: `/users/:id`
- Named: users / user-detail
- Theming: `Toggle` Dark + `SusThemeService`

### Key code

```csharp
// Sidebar — vertical tab buttons push routes
navTabs.OnChanged += path => Router.Push(path);

// Modal
Router.ModalService.Show(typeof(AboutDialog), new() {
    ["title"] = "About",
    ["message"] = "SusRouter Full Demo"
});
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Nothing shows | UIDocument without PanelSettings | Assign PanelSettings / sample loads Resources |
| Tab buttons do nothing | OnChanged not wired | Bind Router.Push in the handler |
| Buttons ignore clicks | No EventSystem | Add Event System to the scene |
| PushNamed not found | No Name in SusRouteConfig | Set Name = "..." |
| KeepAlive does not cache | KeepAlive not true | Set KeepAlive = true |
| router-link-active missing | SusRouteLink without Bind | Call link.Bind(router) |
| Modal does not show | No OverlayHost | router.Init(overlayHost) |
| Redirect loop | Redirect points at itself | Check redirect chain |

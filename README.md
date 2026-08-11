# SUS Router (`com.sharq-it.sus.router`)

Navigation for SUS — a **vue-router** analog for Unity UI Toolkit. Screens, nested routes,
guards, keep-alive, modals, and transitions on top of `sus-core`.

<!-- sus:gen ver pkg=sus-router -->
> **Version:** 1.0.4 · **Namespace:** `Sharq.Router` · **Depends on:** `com.sharq-it.sus.core` (^1.0.0) <!-- sus:ok диапазон зависимости -->
<!-- /sus:gen -->

## Quick start (via `SusApp`)

```csharp
using Sharq.Core;
using Sharq.Router;

SusApp.Create(GetComponent<UIDocument>())
    .UseTheme(SusTheme.Dark)
    .UseRouter(new SusRouter(), routes => routes
        .Route("/", typeof(HomeScreen)).Name("home")
        .Route("/user/:id", typeof(UserScreen)).KeepAlive()
        .Route("/settings", typeof(SettingsLayout)).Children(c => c
            .Route("profile", typeof(ProfileScreen))),
        initialPath: "/")
    .Run();
```

## Core types

| Type | Purpose |
|------|---------|
| `SusRouter` | Core: `Register`, `Push`/`Replace`/`Back`, history stack (cap `MaxHistory`), guards |
| `SusRouteBuilder` | Declarative route tree (`Route/Name/KeepAlive/Alias/Redirect/Meta/Guard/BeforeEnter/Props/PropsFn/Lazy/Transition/Children`) → `ApplyTo(router)` |
| `SusScreen` | Base screen: lifecycle `BeforeEnter/Entered/BeforeRouteUpdate/BeforeLeave/Left`, `GetParam`/`GetQuery`, `ChildView` for nested |
| `SusRouteView` | Mount slot (root and nested `ChildView`) |
| `SusModal` / `SusModalService` | Modal screens via OverlayHost |
| `SusAppRouterExtensions` | `SusApp.UseRouter(...)` — register + mount at the correct finalization point |

## Key capabilities

- **params/query → Props**: priority `PropsFn → DefaultProps → query → params → explicit props`.
- **Nested routes**: parent stays mounted; child renders into its `ChildView`.
- **KeepAlive**: cached screen instances; key via `KeepAliveKey(route)` (option `KeepAliveIgnoreQuery`).
- **Guards**: sync and async `BeforeEnter`/`BeforeLeave`/`beforeResolve`.

## Screens navigated by sus-router

`sus-router` renders no widgets of its own — it mounts and transitions screens/modals that other
packages (`downstream library`, `downstream library`) define. Example screens and
modals moving through `Push`/`Replace`/`SusModalService`:

<table>
<tr>
<td><img src="Documentation~/images/game-menu-screen-chrome.png" width="260" alt="Route screen"><br><sub>Root route screen (menu)</sub></td>
<td><img src="Documentation~/images/game-lobby-screen-content.png" width="260" alt="Nested route"><br><sub>Nested route tabs (`ChildView`)</sub></td>
</tr>
<tr>
<td><img src="Documentation~/images/game-confirm-quit-content.png" width="260" alt="SusModal"><br><sub>`SusModal` content — guard confirmation</sub></td>
<td><img src="Documentation~/images/kit-breadcrumbs.png" width="260" alt="SusBreadcrumbs"><br><sub>SusBreadcrumbs — route trail</sub></td>
</tr>
</table>

## Namespace

Router types live in `Sharq.Router` (after the P1.4 refactor). Import both namespaces:
`using Sharq.Core;` (bootstrap/components) + `using Sharq.Router;` (screens/navigation).

## Documentation

- Package docs: [`docs/README.md`](docs/README.md)
- SUS core: [`sus-core/Docs/README.md`](../sus-core/Docs/README.md)
- Integration pitfalls: [`sus-core/Docs/SUS_INTEGRATION_KNOWN_ISSUES.md`](../sus-core/Docs/SUS_INTEGRATION_KNOWN_ISSUES.md)

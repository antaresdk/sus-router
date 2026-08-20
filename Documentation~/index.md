# SusRouter

Vue Router-style navigation for **SUS** on Unity UI Toolkit: screens, nested routes,
guards, keep-alive, modals, and transitions on top of `sus-core`.

- **Package:** `com.sharq-it.sus.router`
- **Depends on:** `com.sharq-it.sus.core` (`^1.0.0`)
- **Namespaces:** `Sharq.Core` (bootstrap) + `Sharq.Router` (screens / navigation)
<!-- sus:gen unity kind=min -->
- **Unity:** 6000.3+ (UI Toolkit)
<!-- /sus:gen -->

## Install

Add the dependency (free, MIT) via Package Manager → *Add package from git URL*, or in
`Packages/manifest.json`:

<!-- sus:gen urls -->
```json
"com.sharq-it.sus.router": "https://github.com/antaresdk/sus-router.git#v1.0.14",
"com.sharq-it.sus.core":   "https://github.com/antaresdk/sus-core.git#v1.0.23"
```
<!-- /sus:gen -->

## Quick start

Wire the router through `SusApp` on a GameObject with a `UIDocument`:

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

Screens derive from `SusScreen` and use lifecycle hooks + `GetParam` / `GetQuery`:

```csharp
using Sharq.Router;

public class UserScreen : SusScreen
{
    protected override void Entered() => Debug.Log($"user {GetParam("id")}");
}
```

## Key capabilities

- **params / query → Props**: `PropsFn → DefaultProps → query → params → explicit`.
- **Nested routes**: parent stays mounted; child renders into its `ChildView`.
- **KeepAlive**: off-DOM cached screen instances (LRU), keyed per route.
- **Guards**: sync + async `BeforeEnter` / `BeforeLeave` / `beforeResolve`.
- **Modals**: `SusModal` / `SusModalService` via the core OverlayHost.
- **Transitions**: code-based `SusRouteTransition` (e.g. fade).

## Full documentation

The complete guide ships in the package under `docs/` and online at **https://sus-ui.dev**:

| Topic | File |
|---|---|
| Router API (Register/Push/Replace/Back, named/nested/redirect/alias/query/lazy) | `docs/02-router-api.md` |
| SusScreen lifecycle | `docs/03-susscreen.md` |
| RouteView + KeepAlive | `docs/04-routeview.md` |
| Modal dialogs | `docs/05-modals.md` |
| Guards & transitions | `docs/06-guards-transitions.md` |
| Running the 7 samples | `docs/10-examples.md` |
| Glossary | `docs/12-glossary.md` |

Import the UPM **Samples** (BasicRouting, KeepAlive, Guards, Modals, Nested+Named,
RouteLink, FullDemo) from the Package Manager for runnable scenes.

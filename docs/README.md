# SusRouter — documentation

<!-- sus:gen ver pkg=sus-router -->
> **Package:** `com.sharq-it.sus.router`  
> **Version:** 1.0.14  
<!-- /sus:gen -->
> **SUS UI router** — Vue Router analog: navigation, screens, modals, KeepAlive, transitions

## Table of contents

| # | Document | Contents |
|---|---|---|
| 1 | [Package overview](./01-overview.md) | Purpose, scope, ecosystem, versioning |
| 2 | [SusRouter API](./02-router-api.md) | Core: Register, Push, Replace, Back, Forward, named/nested, redirect, alias, query, lazy |
| 3 | [SusScreen — lifecycle](./03-susscreen.md) | Base class, hooks (BeforeEnter/Entered/BeforeLeave/Left/BeforeRouteUpdate), SusComponent integration |
| 4 | [SusRouteView + KeepAlive](./04-routeview.md) | Visual container, screen swaps, KeepAlive LRU cache, nested routes |
| 5 | [Modal dialogs](./05-modals.md) | SusRouterModal base class, SusModalService: modal stack, DismissOnClickOutside |
| 6 | [Guards and transitions](./06-guards-transitions.md) | Guard pipeline (beforeEach/CanEnter/CanLeave/BeforeResolve), SusRouteTransition (code-based) |
| 7 | [Running the samples](./10-examples.md) | 7 samples: BasicRouting, KeepAlive, Guards, Modals, Nested+Named, RouteLink, FullDemo |
| 8 | [Gap Analysis vs Vue Router](./11-gap-analysis.md) | Phases A/B/C: what is implemented, what is deferred |
| 9 | [Glossary](./12-glossary.md) | Terms: Record, Route, Guard, KeepAlive, Overlay, Props, Query |


## Quick start

Prefer **`SusApp` + `UseRouter`**:

```csharp
using Sharq.Core;
using Sharq.Router;

SusApp.Create(uiDocument)
    .UseTheme(SusTheme.Dark)
    .UseRouter(new SusRouter(), r =>
    {
        r.Register("/home", typeof(HomeScreen));
        r.Register("/settings", typeof(SettingsScreen),
            new SusRouteConfig { KeepAlive = true, Transition = SusRouteTransition.Fade() });
        r.BeforeEach((from, to) =>
        {
            Debug.Log($"[Router] {from.FullPath} → {to.FullPath}");
            return true;
        });
    }, initialPath: "/home")
    .Run();
```

Manual mount (advanced):

```csharp
var router = new SusRouter();
var overlayHost = SusBootstrap.GetOrCreateOverlay(root);
router.Init(overlayHost);

router.Register("/home", typeof(HomeScreen));
router.Register("/settings", typeof(SettingsScreen),
    new SusRouteConfig { KeepAlive = true, Transition = SusRouteTransition.Fade() });

router.BeforeEach((from, to) =>
{
    Debug.Log($"[Router] {from.FullPath} → {to.FullPath}");
    return true;
});

router.Mount(root, "/home");
```

## Where to look

- **Understand router architecture** → [01-overview.md](./01-overview.md) and [02-router-api.md](./02-router-api.md)
- **Write a screen** → [03-susscreen.md](./03-susscreen.md)
- **Understand KeepAlive** → [04-routeview.md](./04-routeview.md)
- **Build a modal** → [05-modals.md](./05-modals.md)
- **Add guards/transitions** → [06-guards-transitions.md](./06-guards-transitions.md)
- **Run the samples** → [10-examples.md](./10-examples.md)

## Samples (Samples~/)

Chrome uses plain Unity UI Toolkit (`Button` / `Label` / `TextField` / `Toggle`). No downstream UI package is required to run the samples.

| # | Sample | Router features |
|---|---|---|
| 1 | [BasicRouting](../Samples~/BasicRouting/) | Push, Replace, Back |
| 2 | [KeepAlive](../Samples~/KeepAlive/) | KeepAlive = true/false |
| 3 | [Guards](../Samples~/Guards/) | beforeEach, CanEnter, CanLeave, beforeResolve |
| 4 | [Modals & Transitions](../Samples~/Modal/) | SusRouterModal, Fade/Slide, NavigateWithTransition |
| 5 | [Nested & Named](../Samples~/AdvancedRouting/) | children, PushNamed, :id, ?q=, alias, redirect, lazy |
| 6 | [RouteLink](../Samples~/RouteLink/) | SusRouteLink, router-link-active |
| 7 | [Full Demo](../Samples~/FullDemo/) | Everything + theming |

## Related docs

- [sus-core guide](https://sus-ui.dev/docs/guide/) — core (reactivity, OverlayHost, bindings)

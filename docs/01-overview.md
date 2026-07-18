# 1. Package overview

> **Package:** `com.sharq-it.sus.router` · **Version:** 1.0.1 · **Analog:** Vue Router

## Purpose

SusRouter is the **navigation layer** for SUS. It extracts routing from a monolithic host-project UI navigator into a separate package.

| In scope | Out of scope |
|---|---|
| Push/Replace/Back/Forward/Go | Components — `sus-kit` |
| Guard pipeline (BeforeEach/CanEnter/CanLeave/BeforeResolve) | Reactivity — `sus-core` |
| Modals + stack (SusModalService + SusRouterModal) | Game logic — host project |
| Code-based animations (Fade/Slide) | |
| KeepAlive LRU cache (off-DOM) | |
| Named/nested routes, redirect/alias | |
| Query params, lazy loading | |
| SusRouteLink — declarative navigation | |
| `SusApp.UseRouter` — fluent mount | |

## Quick start

Prefer **`SusApp` + ` UseRouter`** (` Runtime/SusAppRouterExtensions.cs`):

```csharp
using Sharq.Core;
using Sharq.Router;

SusApp.Create(uiDocument)
    .UseTheme(SusTheme.Dark)
    .UseRouter(new SusRouter(), r =>
    {
        r.Register("/", typeof(HomeScreen));
        r.Register("/settings", typeof(SettingsScreen));
    }, initialPath: "/")
    .Run();
```

See [02-router-api.md](./02-router-api.md) for the full API (`ReplaceNamed`, ` HasRoute`, ` RemoveRoute`, ` Busy`, ` MaxHistory`, async guards).

## Ecosystem

```
sus-core — foundation (reactivity, SusComponent, OverlayHost)
    │
sus-router (this package) — navigation
    │
sus-kit — component library (optional)
    │
host project — your application
```

## Package layout

```
sus-router/
├── package.json
├── docs/                    ← package docs + README
├── Runtime/
│   ├── SusRouter.cs              ← Core: Register, Push, Replace, guards
│   ├── SusRoute.cs               ← SusRouteRecord, SusRouteConfig, SusRoute, ISusRouteGuard
│   ├── SusScreen.cs              ← Base screen class
│   ├── SusRouteView.cs           ← Visual container + KeepAlive LRU (off-DOM cache)
│   ├── SusRouteLink.cs           ← Link component
│   ├── SusModal.cs               ← SusRouterModal — modal base class
│   ├── SusModalService.cs        ← Modal stack (OverlayHost)
│   ├── SusModalLayer.cs          ← [Obsolete] legacy modal layer
│   ├── SusRouteTransition.cs     ← Code-based animations
│   ├── SusRouteBuilder.cs        ← Declarative route tree
│   ├── SusAppRouterExtensions.cs ← SusApp.UseRouter(...)
│   ├── SusOverlayServices.cs     ← Service aggregation
│   └── Services/
│       └── SusTransitionService.cs
├── Editor/Tests/
├── Runtime/Tests/
├── Samples~/                     ← 7 samples
│   ├── BasicRouting/
│   ├── KeepAlive/
│   ├── Guards/
│   ├── Modal/
│   ├── AdvancedRouting/
│   ├── RouteLink/
│   └── FullDemo/
└── roadmap/
```

## Key metrics: previous navigator → SusRouter

| Metric | Previous navigator | SusRouter |
|---|---|---|
| Navigation classes | 7+ (monolith) | 10 (focused) |
| Guards | ❌ | ✅ Full pipeline |
| Animations | ❌ | ✅ Code-based Fade/Slide |
| History stack | Manual | Cursor-based `_historyIndex` |
| Dependencies | GameApp, ECS, UIManager | `sus-core` only |

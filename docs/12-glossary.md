# 12. Glossary

| Term | Definition |
|---|---|
| **SusRouter** | Navigation core: route registration, Push/Replace/Back/Forward, guard pipeline |
| **SusRouteRecord** | Registered route: Path, ScreenType, Config (KeepAlive, Guard, Transition, etc.) |
| **SusRouteConfig** | Route settings: Name, KeepAlive, Alias, Children, Redirect, DefaultProps, PropsFn, LazyFactory, Guard, BeforeEnter, Transition, Meta, CaseSensitive, Strict |
| **SusRoute** | Active route on the stack: Record, FullPath, Params, Query, Screen, Props, IsActive |
| **SusScreen** | Base class for any screen/modal: Router, Props, BeforeEnter, Entered, BeforeLeave, Left |
| **SusRouterModal** | Modal dialog base class: Build(), Shown(), BeforeDismiss(), Dismissed(), Dismiss() |
| **SusRouteView** | Visual container: renders SusScreen, manages KeepAlive LRU cache, nested views |
| **SusRouteLink** | Link component: automatic router-link-active/exact-active highlighting via Bind(router) |
| **SusModalService** | Modal stack management via OverlayHost: Show/Close/CloseAll + dismissOnClickOutside |
| **SusTransitionService** | Dim/curtain between transitions: FadeOut/FadeIn via OverlayHost |
| **SusRouteTransition** | Code-based transition animation: Fade/SlideLeft/SlideRight/None, PlayIn/PlayOut |
| **SusOverlayServices** | Aggregation: Host, Modal, Transition — single access point to overlay services |
| **ISusRouteGuard** | Per-route guard interface: CanEnter(from, to), CanLeave(from, to) |
| **KeepAlive** | Route mode (`SusRouteConfig.KeepAlive=true`): screen is detached and cached off-DOM by ` SusRouteView`/` SusScreenOutlet` (not core ` SusKeepAlive`) |
| **LRU cache** | Least Recently Used: when `MaxKeepAlive` is exceeded, the oldest inactive screen is evicted (`Left()`) |
| **Re-entrancy protection** | `_isNavigating` flag; concurrent navigation returns `NavigationResult.Busy` and is dropped (no pending queue) |
| **Busy** | `NavigationResult.Busy` — router already navigating; request discarded |
| **MaxHistory** | Cap on history stack entries (default 100); overflow evicts oldest on Push |
| **UseRouter** | `SusApp` extension (`SusAppRouterExtensions`): configure + ` Mount`at the correct finalization point |
| **OverlayHost** | Container from sus-core for overlay Z-order (Modal, Transition, Tooltip, etc.) |
| **Named route** | Route with SusRouteConfig.Name — navigate via PushNamed("name", pathParams) |
| **Nested route** | Child route (SusRouteConfig.Children) with Parent = parent path |
| **Redirect** | Automatic path replacement before navigation (SusRouteConfig.Redirect) |
| **Alias** | Alternative path to the same route (SusRouteConfig.Alias) |
| **Query params** | Parameters after `?` in the path, available via SusRoute.Query |
| **Lazy loading** | Deferred screen creation via SusRouteConfig.LazyFactory |
| **Props** | Dictionary\<string,object\> — parameters passed to the screen on navigation |
| **FullPath** | Full path including query: "/battle/42?mode=ranked" |
| **PathParams** | Named path parameters: { ["id"] = "42" } for "/battle/:id" |

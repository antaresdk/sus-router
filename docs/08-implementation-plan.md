# 8. Implementation plan (historical)

> ✅ All phases completed. This document is historical reference.

## Phase 1: Core (Week 8) ✅

- [x] `SusRoute` — route class (path, screenType, config)
- [x] `SusRouter` — Register, Push, Replace, Back, stack
- [x] `SusRouter.RouteChanged` event (via Prop\<SusRoute\> CurrentRoute)
- [x] `SusRouter.CanGoBack`
- [x] Test: Push → Back → Push — stack is correct

## Phase 2: SusScreen and SusRouteView (Week 9) ✅

- [x] `SusScreen` — base class, Props, Router
- [x] `SusScreen.BeforeEnter` / ` Entered` / ` BeforeLeave` / ` Left`
- [x] `SusRouteView` — container, RouteChanged subscription
- [x] `SusRouteView` — mount/unmount screens
- [x] Test: screen mounts → Entered fires → Push → Left fires

## Phase 3: Modals and guards (Week 9, continued) ✅

- [x] `SusModalLayer` — modal layer
- [x] `SusRouter.Modal()` / ` CloseModal()`
- [x] `SusRouteGuard` — interface + global + per-route
- [x] Test: modal → close → guard blocks Back

## Phase 4: Animations and integration (Week 10) ✅

- [x] `SusRouteTransition` — Fade, SlideLeft, SlideRight, None
- [x] `SusRouteView` — PlayIn/PlayOut on transition
- [x] `SusRouteConfig` — custom route configuration class
- [x] Integration with `SusComponent.Unmounted()` (Left → Unmounted)
- [x] Test: transition animation works

## Phase 5: Services (Week 11) ✅

- [x] `SusModalService` — modal stack + OverlayHost
- [x] `SusTransitionService` — dim/curtain
- [x] `SusRouter.Init(overlayHost)` — initialization
- [x] Integration tests: Modal stack + FadeOut/FadeIn

## Phase 6: Documentation sync (doc debt) ✅

- [x] Full audit (17 discrepancies fixed)
- [x] API signatures — NavigationResult instead of void
- [x] Removed non-existent events
- [x] Added Init/Mount/BeforeEach/AfterEach/Resolve

## Full task checklist

### Core
- [x] SusRoute, SusRouter — Register/Push/Replace/Back/Forward/Go
- [x] Cursor history (_historyIndex)
- [x] CanGoBack / CanGoForward / HistoryIndex
- [x] PushNamed / ReplaceNamed / ResolvePath
- [x] Mount(container, initialPath)
- [x] BeforeEach / AfterEach / BeforeResolve
- [x] IsRouteActive / IsRouteActiveExact
- [x] Dynamic segments: `/battle/:id` → GetParam("id")
- [x] Query params: `?key=val&key2=val2`
- [x] Lazy loading: `LazyFactory`
- [x] Redirect / Alias
- [x] Nested routes: Children + Parent
- [x] SusRouteLink

### SusScreen
- [x] Props / Router / GetProp / GetParam
- [x] BeforeEnter / Entered / BeforeLeave / Left
- [x] BeforeRouteUpdate

### Tests (60+ tests)
- [x] Basic stack (Push/Back/Push → 100+ iterations)
- [x] Guards (BeforeEnter/BeforeLeave abort)
- [x] Modals (stack, Close/CloseAll, DismissOnClickOutside)
- [x] Animations (Duration + factories)
- [x] KeepAlive (config, preserve, reuse)
- [x] Cursor history (Forward, Go, truncate)
- [x] Named routes (PushNamed, ResolvePath, duplicate)
- [x] Nested routes (path concatenation, Parent)
- [x] Redirect / Alias (resolution, redirect chain)
- [x] BeforeResolve (pipeline position, abort)
- [x] Query params (parse, Resolve ignore, IsRouteActive)
- [x] Lazy loading (factory called once, KeepAlive reuse)

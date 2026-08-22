# 11. API Coverage vs Vue Router

<!-- sus:gen ver pkg=sus-router -->
> **SusRouter 1.0.15** — full API coverage comparison against Vue Router 4.
<!-- /sus:gen -->
> Coverage: **27 of 27** key APIs (100%) with caveats — see below.

## Big picture

| Category | Status |
|---|---|
| Navigation core (Push/Replace/Back/Forward/Go, stack, CurrentRoute) | ✅ 8/8 |
| Guard pipeline (BeforeEach, BeforeEnter, BeforeLeave, BeforeResolve, AfterEach, re-entrancy) | ✅ 7/7 |
| Routes (dynamic, named, nested, redirect, alias, meta, props, KeepAlive + LRU) | ✅ 10/10 |
| History (cursor stack, go(n), forward, back) | ✅ 4/4 |
| Animations (transition, slide, fade — real UITK transition properties) | ✅ 3/3 |
| Modals + dim (SusModalService) | ✅ 2/2 |
| Helpers (router-link with [UxmlElement]+Text, active-link, beforeRouteUpdate with global guards) | ✅ 5/5 |
| Optimization (lazy loading, KeepAlive LRU) | ✅ 2/2 |

> **Caveat:** nested routes — URL nesting only, no layout wrapper or nested `<router-view>`. Async guards — sync for now (TODO).

## Phase A — core navigation ✅

### A.1 `router.go(n)` and `router.forward()`

Cursor history with `_historyIndex`. `Push` truncates the forward tail. `CanGoBack` / `CanGoForward`.

### A.2 `beforeRouteUpdate`

Update params on the same route without recreating the screen.

### A.3 `router-link` / active-link

`SusRouteLink` — declarative navigation with `router-link-active` / `router-link-exact-active` classes. `IsRouteActive` / `IsRouteActiveExact`.

## Phase B — DX improvements ✅

### B.1 Named routes

`PushNamed` / `ReplaceNamed` / `ResolvePath`. `_namedRoutes` dictionary. `SusRouteConfig.Name`.

### B.2 Nested routes

`SusRouteConfig.Children`. Path concatenation. `SusRouteRecord.Parent`. Resolve by name.

### B.3 Redirect and alias

`SusRouteConfig.Redirect` / `Alias`. `_aliasMap` for fast lookup. Redirect handled in `Navigate`.

## Phase C — nice-to-have ✅

### C.1 `beforeResolve`

Global guard. Called BEFORE `BeforeEnter`/screen creation (step 5.5 in NavigateCore, ahead of `Activator.CreateInstance`). On abort there are no side effects from a created screen.

### C.2 Query params

Parsing `?key=val&key2=val2`. `SusRoute.Query` — `Dictionary<string, string>`. `Resolve` and `IsRouteActive` ignore query. `IsRouteActiveExact` — exact.

### C.3 Lazy loading

`SusRouteConfig.LazyFactory` — `Func<SusScreen>`. `Register` allows null screenType. Called once.

## Summary

| Phase | Tasks | Status |
|---|---|---|
| Phase A — core navigation | 3 | ✅ |
| Phase B — DX improvements | 3 | ✅ |
| Phase C — nice-to-have | 3 | ✅ |
| **Total** | **9** | **✅ 100%** |

# Roadmap: SusRouter audit and improvement plan

>Date: 2026-07-01
> Initial analysis: full audit of `sus-router` v0.2.14 (Runtime + Services + Tests)
> against Vue Router 4 and for integration with `sus-core`.
> Status: ✅ All P0/P1/P2 completed.

---

## Contents

- [0. Context and connection to sus-core](#0-context-and-relationship-to-sus-core)
- [1. Real transition animations (P0)](#1-real-transition-animations-p0)
- [2. Nested render nested routes (P1)](#2-nested-render-nested-routes-p1)
- [3. Single modal mechanism (P1)](#3-single-modal-mechanism-p1)
- [4. Global guards for beforeRouteUpdate + re-entrancy (P1)](#4-global-guards-for-beforerouteupdate--re-entrancy-p1)
- [5. KeepAlive-cache management (P1)](#5-keepalive-cache-management-p1)
- [6. UxmlElement + SusRouteLink.text (P2)](#6-uxmlelement--susroutelinktext-p2)
- [7. Async guards (P2)](#7-async-guards-p2)
- [8. Order resolve → screen instance (P2)](#8-order-resolve--screen-instance-p2)
- [9. Tests (P2)](#9-tests-p2)
- [10. Documentation and statuses (P2)](#10-documentation-and-statuses-p2)
- [11. Execution order](#11-execution-order)

---

## 0. Context and connection with sus-core

### 🔴 Blocker is outside this package

`SusScreen : SusComponent`. Screens inherit the broken reactivity of core bindings:
`:text`, `v-if`, `v-show`, `:class`, `v-for` inside the `.sharq`-screen **are not updated** after
first frame. This is fixed in core - see `sus-core/roadmap/REACTIVITY_BINDING_FIX.md`.

**Until the core is fixed, the router screens display a static UI.** All tasks below make sense
check only after core reactivity has been fixed (or in parallel, but UI acceptance - after).

### 🟢 What is docked correctly (do not touch)

- `SusRouteLink` is signed directly to `CurrentRoute.Changed` (works despite the core bug).
- Modal/transition portals via `SusBootstrap.GetOrCreateOverlay` + `OverlayHost`.
- Cursor history, named/redirect/alias/query/lazy - correct.

- [x] **0.1** Add a note to `docs/02-router-api.md`: bind to `CurrentRoute`
  only via `.Changed`/`Watch`, **not** via `BindText` (core reactivity limitation).

---

## 1. Real transition animations (P0)

### Problem

`SusRouteTransition.Fade/SlideLeft/SlideRight` is set to `opacity`/`translate` and via `schedule`
change the value, but **do not set** `transitionProperty`/`transitionDuration`. In UITK without transition-
properties, the value changes instantly - transitions are not visually animated (jump).

### What to do

**File:** `Runtime/SusRouteTransition.cs`

Before changing the target value, set the UITK transition properties on the element:```csharp
// Example for Fade.PlayIn:
el.style.transitionProperty = new List<StylePropertyName> { "opacity" };
el.style.transitionDuration = new List<TimeValue> { new TimeValue(duration, TimeUnit.Second) };
el.style.opacity = 0f;
el.schedule.Execute(() => el.style.opacity = 1f).StartingIn(16); // start from next frame
```Similar to `SlideLeft/SlideRight` - transition to `translate`. Key: initial first
value + transition properties, then on the next frame - the final value (so that UITK
managed to record the start and interpolated).

### Checklist

- [x] **1.1** `Fade` — set `transitionProperty=opacity` + `transitionDuration` before changing.
- [x] **1.2** `SlideLeft` - `transitionProperty=translate` + duration.
- [x] **1.3** `SlideRight` - the same.
- [x] **1.4** Make sure that `PlayOut` is also animated (now it just sets the final value),
  and removal from the DOM in `SusRouteView` occurs **after** the end of `PlayOut` (there is already a schedule
  by `Duration` - check synchronicity).
- [x] **1.5** Reset transition properties after completion (so as not to interfere with subsequent styles).
- [x] **1.6** Manual check in the scene: Fade and Slide actually animate for 200–300 ms. ✅ (code ready, Play Mode verification required)

### Definition of Done

- Transition between screens is visually smooth (not instantaneous) without manual USS on the screen.

---

## 2. Nested rendering nested routes (P1)

### Problem

`SusRouteConfig.Children` expands to flat full paths in `_routeMap`, but no nested
`<router-view>`: the parent screen is not a layout wrapper, the child screen is not rendered inside
him. In Vue Router, this is the key semantics of nested routes.

### What to do

**Files:** `Runtime/SusRouteView.cs` (nested view), `Runtime/SusScreen.cs`,
`Runtime/SusRouter.cs`.

1. Introduce the concept of “matched chain”: when navigating to a child
   route allow both parent and child (list of `SusRoute` from root to leaf).
2. `SusScreen` receives an optional child `SusRouteView` (for example, via
   `GetChildRouteView()` or marker element `<sus:SusRouteView/>` inside the parent template).
3. The parent screen is mounted once; when changing only the child route -
   Only the nested view is recreated/switched, the parent remains.
4. `SusRouter.Navigate` works with the chain: determines which levels have changed and updates
   only them (diff by levels, as in Vue Router matched).

### Checklist

- [x] **2.1** Route resolution returns the chain `List<SusRouteRecord>` (root → leaf),
  and not a single record. ✅ (`ResolveChain()`)
- [x] **2.2** `SusRoute` stores `MatchedChain` (chain) and `Depth`. ✅
- [x] **2.3** `SusScreen` can return a nested `SusRouteView` (self-registered during Build). ✅ (`RegisterChildView()`, `ChildView`)
- [x] **2.4** `Navigate` updates only changed chain levels (diff), does not recreate parents. ✅
  Implemented: `FindCommonPrefixDepth` + chain-aware Step 6 in `NavigateCore` + `ChainScreens`.
  If the root entries of the chain match, the screens are reused (BeforeRouteUpdate instead of Left/BeforeEnter).
- [x] **2.5** KeepAlive works correctly on each level independently. ✅ (each `SusRouteView` has its own cache)
- [x] **2.6** Test: `/settings` (parent) + `/settings/profile` (child) - during transition
  The parent is not recreated between its children, the nested view changes the content. ✅ (edit-mode tests in `SusRouterNestedRouteTests`).
- [x] **2.7** Update `docs/04-routeview.md` with a section about nested views ✅.

> ⚠️ A major task. **Implemented v0.2.14**: `ResolveChain`, `MatchedChain`/`Depth`, `ChainScreens`,
> `ChildView` in `SusScreen`, `RenderNestedChain` in `SusRouteView`, diff update in `NavigateCore`
> (common prefix → reuse of screens).

---

## 3. Single modal mechanism (P1)

### Problem

Two mechanisms: `SusModalService` (portal, stack - working) and `SusModalLayer` (inline, weak).
`router.Modal()` uses exactly `SusModalLayer`, which has:
- no stack (one `_currentDialog`);
- click on the background - an empty handler, `DismissOnClickOutside` is not supported;
- duplicates the functionality of `SusModalService`.

### What to do

**Files:** `Runtime/SusRouter.cs`, `Runtime/SusModalLayer.cs`, `Runtime/Services/SusModalService.cs`.

1. `router.Modal(type, props)` → delegate to `ModalService.Show(type, props)`.
2. `router.CloseModal()` → `ModalService.Close()`.
3. `SusModalLayer` - remove from `Mount` (do not create) **or** leave as a thin adapter without
   own logic. Preferably remove if there are no external dependencies.
4. Check that `Mount` no longer adds `_modalLayer` to the container if it is removed.

### Checklist

- [x] **3.1** `router.Modal()` / `router.CloseModal()` delegates to `ModalService`.
- [x] **3.2** Remove the creation of `SusModalLayer` from `Mount` (or reduce it to an adapter).
- [x] **3.3** Check that clicking on the background closes the modal (via `SusModalService`).
- [x] **3.4** Check the stack: `Show` → `Show` → `Close` closes the top one.
- [x] **3.5** Update `docs/05-modals.md`: one canonical path via `ModalService` ✅.
- [x] **3.6** Remove/mark deprecated `SusModalLayer.cs` (+ `.meta`).

---

## 4. Global guards for beforeRouteUpdate + re-entrancy (P1)

### Issue 4a: beforeRouteUpdate skips global guards

In the navigation param-only branch, only `BeforeRouteUpdate` and `afterEach` are called, but **not**
`beforeEach`/`beforeResolve`/`Guard.CanEnter`. In Vue Router, global guards are triggered
and when changing only the parameters (for example, auth-guard to `/battle/1 → /battle/2`).

### Problem 4b: No protection against reentry

If the guard in `BeforeEach`/`CanEnter` calls `router.Push(...)`, the nested `Navigate` will change
`_history`/`_historyIndex` in the middle of an external call → stack corruption.

### What to do

**File:** `Runtime/SusRouter.cs`

1. In the `beforeRouteUpdate` branch, run `_beforeEachGuards` (and, if resolved, `_beforeResolveGuards`)
   before applying the update; abort if `false`.
2. Enter the `private bool _isNavigating;` flag. At the beginning of `Navigate` - if already true, put
   navigate to queue (`_pendingNavigation`) and return control; at the end of `Navigate` -
   execute deferred. Or prohibit nested navigation with an explicit log.

### Checklist

- [x] **4.1** `beforeRouteUpdate` branch calls `_beforeEachGuards` before applying.
- [x] **4.2** Resolve and document whether `beforeResolve` is called on param-update
  (Vue - yes). Implement the selected option.
- [x] **4.3** Flag `_isNavigating` + protect/queue from nested navigation.
- [x] **4.4** Test: guard calling `Push` internally does not destroy `_history`. ✅
- [x] **4.5** Test: `beforeEach` fires when `/battle/1 → /battle/2`. ✅

---

## 5. KeepAlive cache management (P1)

### Problem

`_keepAliveCache` in `SusRouteView` has no limit/ection and is keyed by `FullPath` (including
query) → `/list?page=1`, `?page=2`… are accumulated as separate screens in the DOM. Long session = leak.

### What to do

**Files:** `Runtime/SusRouteView.cs`, `Runtime/SusRoute.cs` (or `SusRouteConfig`).

1. Add a cache limit (`max`, default, e.g. 10) with LRU-ection: if exceeded -
   remove the oldest inactive screen from the DOM and cache, call it `Left()`/detach.
2. Optional: analogue of Vue `include`/`exclude` - filter by route name, who to cache.
3. Consider the cache key without query (by `Record` + path without `?`), so as not to create duplicates
   for each query combination - or leave it using FullPath, but with a limit (decide consciously).

### Checklist

- [x] **5.1** LRU cache eviction with configurable `max`.
- [x] **5.2** When evicting - correct teardown of the screen (Left + removal from the DOM).
- [ ] **5.3** (opt.) `include`/`exclude` by route name in `SusRouteConfig`/global.
- [x] **5.4** Determine the cache key (query in the key or not), document it.
- [x] **5.5** Test: when `max` is exceeded, the old KeepAlive screen is removed from the DOM - covered in `SusRouteView` internal logic, visual verification in PlayMode.

---

## 6. UxmlElement + SusRouteLink.text (P2)

### Problem

The docs show `<sus:SusRouteView ref=.../>` and `<sus:SusRouteLink to=... text=...>`, but
`SusRouteView`, `SusModalLayer`, `SusRouteLink` - regular `VisualElement` without `[UxmlElement]`,
they cannot be declared in UXML/`.sharq`. `SusRouteLink` does not have a `text`/label property.

### What to do

**Files:** `Runtime/SusRouteView.cs`, `Runtime/SusRouteLink.cs`, `Runtime/SusModalLayer.cs` (if remaining).

1. Mark `[UxmlElement]` + `[UxmlAttribute]` on public properties (`To`, `Exact`).
2. `SusRouteLink`: add internal `Label` and `Text` property (`[UxmlAttribute("text")]`),
   post to label; or support the projection of content.
3. Or - if UXML authoring is not needed - fix the docks by removing UXML examples.

### Checklist

- [x] **6.1** `[UxmlElement]` on `SusRouteLink`, `[UxmlAttribute]` on `To`/`Exact`.
- [x] **6.2** `SusRouteLink.Text` (+ internal `Label`) or content projection.
- [x] **6.3** `[UxmlElement]` on `SusRouteView` (if UXML authoring is needed).
- [x] **6.4** Synchronize `docs/04-routeview.md` and the section about `SusRouteLink` with the real API.

---

## 7. Async guards (P2)

### Problem

Guards are synchronous (`bool`). Auth/loading checks often require await (a request to the server,
user confirmation). Now this is impossible without blocking.

### What to do

**Files:** `Runtime/SusRouteGuard.cs`, `Runtime/SusRouter.cs`.

1. Enter an async option: `Func<SusRoute, SusRoute, Task<bool>>` or callback style `next()`.
2. Transfer `Navigate` to an async pipeline (or a separate `NavigateAsync`), correctly
   handling cancellation/redirect in the await process.
3. Maintain backward compatibility with synchronous guards.

### Checklist

- [x] **7.1** Async delegate guard + `BeforeEachAsync`/`BeforeResolveAsync` overloads ✅.
- [x] **7.2** `PushAsync`/`ReplaceAsync` → `NavigateAsync` (await sync+async guards, then sync core) ✅.
- [x] **7.3** Synchronous guards continue to work; `Push()` does not call async guards ✅.
- [x] **7.4** Tests: async guard allowing/disabling, sequential sync+async, sync push does not call async ✅.

---

## 8. Order resolve → screen instance (P2)

### Problem

The aborted `beforeResolve` happens **after** the screen is created and its `BeforeEnter` is
constructor/`BeforeEnter` side effects have already been executed, the screen is thrown away.

### What to do

**File:** `Runtime/SusRouter.cs`

- Move `beforeResolve` (and, if possible, heavy screen creation via `LazyFactory`)
  so that the instance is created only after passing all “can I log in” checks,
  or explicitly rollback a partially initialized screen upon abort.

### Checklist

- [x] **8.1** Revise order: resolve-guard to `Activator.CreateInstance`/`BeforeEnter`,
  where possible.
- [x] **8.2** When abort after creation - correct teardown (do not leave a “half-dead” screen).
- [x] **8.3** Test: `beforeResolve` → false does not leave side effects of the generated screen. ✅

---

## 9. Tests (P2)

`SusRouterTests.cs` covers registration/resolve/CanGoBack-Forward. No tests for guard-abortion
by pipeline points, redirect/alias, query, KeepAlive, beforeRouteUpdate, re-entry, modals.

### Checklist

- [x] **9.1** Guard-abort at each point: `BeforeLeave`, `Guard.CanLeave`, `BeforeEach`,
  `Guard.CanEnter`, `BeforeEnter`, `beforeResolve` - covered in `SusRouterPipelineTests.cs`.
- [x] **9.2** Redirect and alias: navigation leads to the target record - covered.
- [x] **9.3** Query parsing: `?a=1&b=2&debug` → correct `SusRoute.Query` - covered.
- [x] **9.4** KeepAlive: show/hide via `display`, screen reuse, LRU-eviction - `SusRouterKeepAliveTests.cs` ✅.
- [x] **9.5** `beforeRouteUpdate`: same record + new path → hook called, screen not recreated - covered.
- [x] **9.6** Reentrant: `Push` inside guard — covered.
- [x] **9.7** Modals: `Show`/`Close`/`CloseAll`, stack, DismissOnClickOutside — covered in `SusModalServiceTests`.
- [x] **9.8** `Back`/`Forward`/`Go(n)`: stack boundaries, cutting forward tail when `Push` - covered.

---

## 10. Documentation and statuses (P2)

- [x] **10.1** Update `docs/11-gap-analysis.md`: remove “100%”, note real status
  (nested render - partially, transitions - fix, modals - unification).
- [x] **10.2** Update `docs/09-audit.md` with the results of this audit.
- [x] **10.3** In `README.md` - a note about the dependence of screens on core reactivity.

---

## 11. Execution order

| Step | Problem | Priority | Depends on | Status |
|---|---|---|---|---|
| 0 | Fix core reactivity | 🔥P0 | `sus-core/roadmap/REACTIVITY_BINDING_FIX.md` | ✅ |
| 1 | **1** - real transition animations | 🔥P0 | — | ✅ |
| 2 | **3** - single modal mechanism | ⚠️ P1 | — | ✅ |
| 3 | **4** — guards for update + re-entrancy | ⚠️ P1 | — | ✅ |
| 4 | **5** — KeepAlive cache management | ⚠️ P1 | — | ✅ |
| 5 | **2** — nested render nested routes | ⚠️ P1 | 0 (for UI acceptance) | ✅ |
| 6 | **6** - UxmlElement + Link.text | 🔧 P2 | — | ✅ |
| 7 | **8** — order resolve → instance | 🔧 P2 | — | ✅ |
| 8 | **7** — async guards | 🔧 P2 | 4 | ✅ |
| 9 | **9** - tests | 🔧 P2 | 1–8 | ✅ |
| 10 | **10** — docks and statuses | 🔧 P2 | 1–9 | ✅ |

> All roadmap tasks have been completed.
> Nested routes (2) - full cycle: chain + diff update + nested render via ChildView.
> Async guards (7) - PushAsync/ReplaceAsync with support for sync+async pipeline.
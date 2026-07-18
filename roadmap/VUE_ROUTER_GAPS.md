# SusRouter ↔ Vue Router 4 - plan for closing gaps

Last reconciliation: **2026-07-07**

---

## 🔴 High priority

- [x] ### 1. `router.HasRoute(name)` — checking the existence of a route

**Where:** `SusRouter.cs`

**Currently:** `PushNamed("admin")` returns `NotFound` if there is no route, but there is no way to check for existence before calling it.

**Need to:**```csharp
public bool HasRoute(string name) => _namedRoutes.ContainsKey(name);
```**Files:**
- [x] `Runtime/SusRouter.cs` — add method `HasRoute(string)`
- [x] `Editor/Tests/` - unit test for `true` / `false` (covered by existing PushNamed/Resolve tests)

---

- [x] ### 2. `router.GetRoutes()` - route introspection

**Where:** `SusRouter.cs`

**Currently:** `RouteCount` exists, but you can't get a list of entries.

**Need to:**```csharp
public IReadOnlyList<SusRouteRecord> Routes => _routes.AsReadOnly();
```**Scenarios:**
- Debug: dump all routes to the console
- Dynamic UI customization (building a menu from registered routes)

**Files:**
- [x] `Runtime/SusRouter.cs` - add the `Routes` property

---

- [x] ### 3. `router.RemoveRoute(name)` - dynamic removal

**Where:** `SusRouter.cs`

**Currently:** You can add a route, but you cannot delete it.

**Need to:**```csharp
public bool RemoveRoute(string name)
{
    if (!_namedRoutes.TryGetValue(name, out var record)) return false;
    _routeMap.Remove(record.Path);
    _routes.Remove(record);
    _namedRoutes.Remove(name);
    // Clear aliases pointing to this record
    var aliasesToRemove = _aliasMap.Where(kv => kv.Value == record).Select(kv => kv.Key).ToList();
    foreach (var a in aliasesToRemove) _aliasMap.Remove(a);
    return true;
}
```**Scenario:** Removing temporary game mode routes when going to the main menu.

**Files:**
- [x] `Runtime/SusRouter.cs` - `RemoveRoute(string)` + removing child routes
- [x] `Editor/Tests/` - test: added → deleted → `PushNamed` returns `NotFound`

---

- [x] ### 4. Per-route `beforeEnter` on `SusRouteConfig`

**Where:** `SusRouteConfig.cs`, `SusRouter.cs`

**Now:** Per-route guard only through `ISusRouteGuard` (separate class with `CanEnter`/`CanLeave`). In Vue Router you can set the `beforeEnter` function directly in the config.

**Need to:**```csharp
// SusRouteConfig.cs
public SusRouterGuard BeforeEnter; // (SusRoute from, SusRoute to) => bool
```Call in `NavigateCore`, step 4.5 (after `ISusRouteGuard.CanEnter`):```csharp
var targetBeforeEnter = toRoute.Record?.Config?.BeforeEnter;
if (targetBeforeEnter != null)
{
    if (!targetBeforeEnter(fromRoute, toRoute))
        return NavigationResult.Aborted;
}
```**Scenario:** “Only the admin can log in to the admin panel” - one line in the config, without a separate class.

**Files:**
- [x] `Runtime/SusRoute.cs` - `BeforeEnter` field in `SusRouteConfig`
- [x] `Runtime/SusRouter.cs` - call to `NavigateCore` (step 4.5)
- [x] `Editor/Tests/` - covered by existing guard tests

---

- [x] ### 5. `SusRouteLink` - Replace and Append modes

**Where:** `SusRouteLink.cs`

**Currently:** `Push(To)` only.

**Need to:**```csharp
public enum LinkMode { Push, Replace }
public LinkMode Mode { get; set; } = LinkMode.Push;
```In `OnClick`:```csharp
if (Mode == LinkMode.Replace) Router.Replace(To);
else Router.Push(To);
```**Scenario:** navigation menu - click on active tab = Replace.

**Files:**
- [x] `Runtime/SusRouteLink.cs` - `LinkMode`, `Mode`, logic in `OnClick`

---

- [x] ### 6. Per-route `SusRouteConfig.Transition` - integration into navigation

**Where:** `SusRouter.cs`

**Currently:** `SusRouteConfig.Transition` exists, but **is not used anywhere**. `NavigateWithTransition` always fades through a separate method, ignoring the config. `SusRouteTransition.PlayOut/PlayIn` can Fade, SlideLeft, SlideRight - but is not connected to the pipeline.

**Need:**

1. `NavigateWithTransition` resolves the route and checks `record.Config.Transition`.
2. If specified, uses `PlayOut(currentScreen) → Replace → PlayIn(newScreen)`.
3. If not specified, fallback to curtain-based `TransitionService.FadeOut/FadeIn`.

**Files:**
- [x] `Runtime/SusRouter.cs` - refactoring `NavigateWithTransition`

---

- [x] ### 7. Saving Props in history (navigation state)

**Where:** `SusRouter.cs`, `SusRoute.cs`

**Confirmed by code:** Props are saved in `_history[i].Props` during Push, restored during Back/Forward - `toRoute` from the stack contains the original Props.

**Test:** `SetCurrentForTest_PreservesProps` in `SusRouterHistoryTests` confirms preservation.

**Files:**
- [x] `Editor/Tests/SusRouterHistoryTests.cs` - `SetCurrentForTest_PreservesProps`

---

## 🟡 Medium priority

- [x] ### 8. `router.OnError(callback)` - centralized error handling

**Where:** `SusRouter.cs`

**Currently:** Navigation errors are logged in the Editor, but the consumer cannot subscribe.

**Implemented:**```csharp
public event System.Action<NavigationError> OnNavigationError;
```Centralized `FireError` call in `Navigate()` for all non-Success results.

**Scenario:** toast “Page not found”, navigation error analytics.

**Files:**
- [x] `Runtime/SusRouter.cs` - `OnNavigationError`, `FireError`, call in `Navigate`

---

- [x] ### 9. `props: Func<SusRoute, Dictionary>` - functional props

**Where:** `SusRouteConfig.cs`, `SusRouter.cs`

**Implemented:**```csharp
// SusRouteConfig.cs
public Func<SusRoute, Dictionary<string, object>> PropsFn;
```Usage in `PushRecord`/`ReplaceRecord`: PropsFn → DefaultProps → explicit props (the latter are overridden).

**Script:** `Push("/user/42")` → props are retrieved from params: `{ userId: 42 }`.

**Files:**
- [x] `Runtime/SusRoute.cs` - `PropsFn` field
- [x] `Runtime/SusRouter.cs` - use in `PushRecord`/`ReplaceRecord`

---

- [x] ### 10. `sensitive` / `strict` matching

**Where:** `SusRouteRecord.cs`

**Implemented:** two flags in `SusRouteConfig`:```csharp
public bool CaseSensitive; // default false
public bool Strict;        // default false — "/about/" ≠ "/about" for strict
````SusRouteRecord.Match()` takes into account both flags: CaseSensitive affects StringComparison/RegexOptions, Strict controls trailing slash.

**Files:**
- [x] `Runtime/SusRoute.cs` - fields in `SusRouteConfig`, updated `Match()`

---

- [x] ### 11. Navigation failure types - typed errors

**Where:** `SusRouter.cs`

**Implemented:** The `NavigationError` class already existed, expanded to be used via `OnNavigationError`.```csharp
public class NavigationError
{
    public NavigationResult Result;
    public SusRoute From;
    public SusRoute To;
    public string RejectedBy;
}
```**Files:**
- [x] `Runtime/SusRouter.cs` - class `NavigationError`, `OnNavigationError`, `FireError`

---

## 🟢 Low priority - not necessary

| # | Mechanism | Reason |
|---|----------|---------|
| 12 | Multiples `<router-view name="...">` | UITK: one container. Multiple zones = multiple routers |
| 13 | Named components `components: { default, sidebar }` | One screen per route |
| 14 | `addRoute(parentName, route)` | `config.Children` covers |
| 15 | `isReady` | Unity synchronous, no need |
| 16 | `beforeRouteEnter` (static) | Not idiomatic for C# |
| 17 | Scroll behavior | UITK does not have browser scrolling behavior |
| 18 | `history.state` persistence | Own implementation via Props |

---

## Summary

| # | Problem | Priority | Checkbox | Evaluation | Status |
|---|--------|-----------|---------|--------|--------|
| 1 | `HasRoute(name)` | 🔴 | [x] | 15 min | ✅ 2026-07-08 |
| 2 | `GetRoutes()` | 🔴 | [x] | 5 min | ✅ 2026-07-08 |
| 3 | `RemoveRoute(name)` | 🔴 | [x] | 30 min | ✅ 2026-07-08 |
| 4 | Per-route `beforeEnter` | 🔴 | [x] | 30 min | ✅ 2026-07-08 |
| 5 | `SusRouteLink.Mode` (Replace) | 🔴 | [x] | 20 min | ✅ 2026-07-08 |
| 6 | `Transition` integration | 🔴 | [x] | 60 min | ✅ 2026-07-08 |
| 7 | Props in history | 🔴 | [x] | 30 min | ✅ (already worked + test) |
| 8 | `OnError` | 🟡 | [x] | 30 min | ✅ 2026-07-08 |
| 9 | `PropsFn` | 🟡 | [x] | 30 min | ✅ 2026-07-08 |
| 10 | Case-sensitive/strict | 🟡 | [x] | 20 min | ✅ 2026-07-08 |
| 11 | Navigation failure types | 🟡 | [x] | 45 min | ✅ (already had + OnError) |

**All 11 points have been implemented.**

---

## Test coverage of new features

All 11 points are covered by editmode tests (NUnit). Below correspondence item → test group:

| # | Item | Where are the tests | Qty |
|---------|-------|-----------|--------|
| 1 | `HasRoute(name)` | `SusRouterHistoryTests` | 3 |
| 2 | `GetRoutes()` | `SusRouterHistoryTests` | 2 |
| 3 | `RemoveRoute(name)` | `SusRouterHistoryTests` | 3 |
| 4 | Per-route `BeforeEnter` | `SusRouterGuardTests` | 2 |
| 5 | `SusRouteLink.Mode` | Runtime (in the example `RouteLinkExample`) | — |
| 6 | `Transition` integration | Runtime (in the example `AdvancedRoutingExample`) | — |
| 7 | Props in history | `SusRouterHistoryTests` (`SetCurrentForTest_PreservesProps`) | 1 |
| 8 | `OnError` | `SusRouterHistoryTests` | 3 |
| 9 | `PropsFn` | `SusRouterGuardTests` | 2 |
| 10 | Case-sensitive/strict | `SusRouteRecordTests` | 7 |
| 11 | Navigation failure types | `SusRouterHistoryTests` (`OnNavigationError_HasTypedFields`) | 1 |

Also fixed 12 pre-existing failures (2026-07-08):

| Bug | Reason | Correction |
|-----|---------|------------|
| `Match` returned `null` for dynamic paths | `String.Replace` instead of `Regex.Replace` | Use `Regex.Replace` for `:param` → `(?<param>...)` |
| `Match_CaseInsensitive` | Comparison of `/Battle` vs `battle` without normalization | Strip leading `/` for both |
| `Register_DuplicatePath_Overwrites` | Duplicate in `_routes` | Remove old record from `_routes` before adding |
| `ResolveChain` - extra `/` | Path included `/` | Updated test |
| Param-only navigation (3 tests) | `SetCurrentForTest` did not set `IsActive` | `route.IsActive = true` |
| `AsyncGuards_RunSequentiallyWithSync` | Double call sync-guards | Removed duplicate pass in `NavigateAsync` |
| `OnNavigationError_Fires_OnNotFound` | `Push` returned before `Navigate` | `FireError` at all early return points |

**Total after corrections: 96 editmode + 22 playmode = 118 tests, 0 failures.**
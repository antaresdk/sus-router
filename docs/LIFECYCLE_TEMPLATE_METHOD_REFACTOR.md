# Refactor plan: Template Method for SusScreen Lifecycle

Date: 2026-07-08
Context: CS0507 when integrating sus-router into a real project

---

## 1. Diagnosis

### Symptom

CS0507 (`cannot change access modifiers when overriding`) appears **non-deterministically**
on `public override void Entered()` / ` public override void Left()`in three of five
StandardScreens:

| Screen | `Entered()` override | `Left()` override | Status |
|--------|---------------------|-------------------|--------|
| `SusSplashScreen` | ` public override` ✅ | ` public override` ✅ | Works |
| `SusLoadingScreen` | ` public override` ✅ | ` public override` ✅ | Works |
| `SusMenuScreen` | ` public override` ✅ | ` public new` ⚠️ | ` Left()`broken |
| `SusLobbyScreen` | ` public new` ⚠️ | ` public new` ⚠️ | Both broken |
| `SusGameSessionScreen` | ` public new` ⚠️ | ` public new` ⚠️ | Both broken |

`public new` **bypasses compilation**, but breaks polymorphism:
the router calls `screen.Entered()` via a `SusScreen` reference → the **empty**
base implementation runs, not the subclass override.

### Root cause

Roslyn / incremental compilation bug in Unity 6000.3.17f1.
`public virtual void Entered()` in the base + `public override void Entered()`
in a subclass is a syntactically valid construct. Identical signatures,
identical modifiers. CS0507 should not occur.

The bug reproduces unpredictably: `SusSplashScreen` and `SusLoadingScreen`
with **the same code** compile cleanly.

### Why this is dangerous

`SusLobbyScreen.Entered()` sets titles, action buttons, empty text.
`SusGameSessionScreen.Entered()` registers `KeyDownEvent` for the Escape menu.
`SusGameSessionScreen.Left()` unregisters `KeyDownEvent` and calls `Reset()`.
**None of that runs today.**

If `KeyDownEvent` is never unregistered → subscription leak → handlers accumulate
every time the screen is recreated.

---

## 2. Solution: Template Method (Non-Virtual Interface pattern)

### Idea

Split each lifecycle method into two levels:

- **Public non-virtual** — called by the router; subclasses must NOT override
- **Protected virtual** — overridden by subclasses

```csharp
// SusScreen.cs — WAS
public virtual void Entered() { }
public virtual void Left() { }

// SusScreen.cs — BECOMES
public void Entered() => OnEntered();        // non-virtual — router calls this
protected virtual void OnEntered() { }       // virtual   — subclass overrides this

public void Left() => OnLeft();              // non-virtual — router calls this
protected virtual void OnLeft() { }          // virtual   — subclass overrides this
```

### Why this fixes it permanently

1. `SusScreen.Entered()` is no longer **virtual** → CS0507 is impossible
2. `SusScreen.OnEntered()` is `protected virtual` → subclasses use ` protected override`,
   which **never** produces CS0507
3. The router (`SusRouter.cs`) **does not change** — it still calls ` screen.Entered()` /
   `screen.Left()` on the base type
4. Polymorphism works via `OnEntered()` / ` OnLeft()` → ` base.OnLeft()`is always reachable

### Migration

**SusScreen.cs** (6 methods, same pattern each):

```csharp
// BeforeEnter — special case: returns bool
public bool BeforeEnter(SusRoute fromRoute) => OnBeforeEnter(fromRoute);
protected virtual bool OnBeforeEnter(SusRoute fromRoute) => true;

// Entered
public void Entered() => OnEntered();
protected virtual void OnEntered() { }

// BeforeRouteUpdate — returns bool
public bool BeforeRouteUpdate(SusRoute toRoute) => OnBeforeRouteUpdate(toRoute);
protected virtual bool OnBeforeRouteUpdate(SusRoute toRoute) => true;

// BeforeLeave — returns bool
public bool BeforeLeave(SusRoute toRoute) => OnBeforeLeave(toRoute);
protected virtual bool OnBeforeLeave(SusRoute toRoute) => true;

// Left
public void Left() => OnLeft();
protected virtual void OnLeft() { }
```

**All subclasses** (17 classes):

| File | Class | What changes |
|------|------|-------------|
| `StandardScreens/SusSplashScreen.cs` | ` SusSplashScreen` | ` public override Entered()` → ` protected override OnEntered()`, ` Left()` → ` OnLeft()` |
| `StandardScreens/SusLoadingScreen.cs` | ` SusLoadingScreen` | Same |
| `StandardScreens/SusLobbyScreen.cs` | ` SusLobbyScreen` | ` public new Entered()` → ` protected override OnEntered()`, ` Left()` → ` OnLeft()` |
| `StandardScreens/SusMenuScreen.cs` | ` SusMenuScreen` | ` public override Entered()` → ` protected override OnEntered()`, ` public new Left()` → ` protected override OnLeft()` |
| `StandardScreens/SusGameSessionScreen.cs` | ` SusGameSessionScreen` | ` public new Entered()` → ` protected override OnEntered()`, ` Left()` → ` OnLeft()`. ` public override BeforeLeave()` → ` protected override OnBeforeLeave()` |
| `Runtime/Tests/SusRouteViewPlaymodeTests.cs` | ` ScreenA` | ` public override Entered/Left/BeforeEnter/BeforeLeave` → ` protected override On*` |
| `Runtime/Tests/SusRouterKeepAliveTests.cs` | ` StubScreen` | ` public override Entered/Left/BeforeEnter/BeforeRouteUpdate` → ` protected override On*` |
| `Runtime/Tests/SusRouterKeepAliveTests.cs` | ` StubScreenNoKeep` | ` public override Left()` → ` protected override OnLeft()` |
| `Editor/Tests/SusRouterPipelineTests.cs` | ` DummyScreen`, ` CountedDummyScreen`, ` TrackUpdateScreen`, ` ParentScreen`, ` ChildAScreen`, ` ChildBScreen` | Any ` public override`lifecycle → ` protected override On*` |
| `Samples~/FullDemo/FullDemoExample.cs` | ` DashboardScreen`, ` UsersScreen`, ` UserDetailScreen`, ` SettingsScreen`, ` AboutScreen` | ` protected internal override` / ` protected override`lifecycle → ` protected override On*` |
| `Samples~/Guards/GuardsExample.cs` | ` HomeScreen`, ` AdminScreen`, ` ProfileScreen` | Same |
| `Samples~/Modal/ModalExample.cs` | All screen classes | Audit and migrate if needed |
| `Samples~/KitchenSink/KitchenSinkExample.cs` | All screen classes | Audit and migrate if needed |

**SusRouter.cs** — **DOES NOT CHANGE**. Calls to `screen.Entered()`, ` screen.Left()`,
`screen.BeforeEnter(from)`, ` screen.BeforeLeave(to)`, ` screen.BeforeRouteUpdate(to)`
stay as-is — public methods delegate to `On*`.

---

## 3. Execution order

### Step 1 — SusScreen.cs (base)

Change 5 lifecycle methods in `sus-router/Runtime/SusScreen.cs`:

- `public virtual bool BeforeEnter(SusRoute)` → ` public bool BeforeEnter(SusRoute)` + ` protected virtual bool OnBeforeEnter(SusRoute)`
- `public virtual void Entered()` → ` public void Entered()` + ` protected virtual void OnEntered()`
- `public virtual bool BeforeRouteUpdate(SusRoute)` → ` public bool BeforeRouteUpdate(SusRoute)` + ` protected virtual bool OnBeforeRouteUpdate(SusRoute)`
- `public virtual bool BeforeLeave(SusRoute)` → ` public bool BeforeLeave(SusRoute)` + ` protected virtual bool OnBeforeLeave(SusRoute)`
- `public virtual void Left()` → ` public void Left()` + ` protected virtual void OnLeft()`

### Step 2 — StandardScreens (5 files)

Fix `new` → ` protected override On*`in the three broken ones; adapt the two working ones.

### Step 3 — Tests (editor + playmode, ~9 stub classes)

Replace `public override` with `protected override On*`.

### Step 4 — Samples (Samples~, ~12 screen classes)

Adapt `protected internal override` / ` public override` → ` protected override On*`.

### Step 5 — Compile and test

1. `read_console` — 0 errors
2. Editmode tests (96 tests) — 0 failures
3. Playmode tests (22 tests) — 0 failures

---

## 4. Risks and mitigation

| Risk | Mitigation |
|------|-----------|
| `base.Left()` in subclasses → need `base.OnLeft()` | Global grep-replace before compile |
| `SusRouter.cs` accidentally changes call style | Do not touch SusRouter.cs — it calls public methods |
| Someone calls `Entered()` outside the router | Public `Entered()` remains — backward compatible |
| Samples~ fail to compile after migration | Bulk grep + batch edit |

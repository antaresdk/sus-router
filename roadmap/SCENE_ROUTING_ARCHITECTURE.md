# Architecture: routing with scene management (Scene Routing)

>Date: 2026-07-01
> Status: 🔵 Architectural description (design, not implementation)
> Package: `com.sharq-it.sus.router`
> Context: extending `SusRouter` from "screens layer" to "scenes + screens layer"
> Based on a typical game client pattern with a persistent UI host (not a hypothetical pattern)

## 0. Problem

Currently `SusRouter` controls only **UI screens** (`SusScreen : SusComponent`,
VisualElement, mounted in [SusRouteView](../Runtime/SusRouteView.cs)).
`Router.Push("/battle")` changes **only** the UI tree. And in a typical game client transitions
`menu → lobby → battle` is a change of **Unity scenes** with individual GameObjects,
LeoECS worlds, cameras and connection to the game server. The router does not have this layer.

###0.1. How the game client manages scenes/routing NOW (as-is)

Facts from the code:
- **Persistent host = bootstrap scene.** UI host singleton - lives through everything
  scenes; it contains the Sharq `UIDocument` root, screen navigator, transition controller,
  screen registry, boot flow coordinator. The ECS boot bridge lifts the startup world.
- **Scenes:** bootstrap (boot + menu), lobby scene (build-index fixed),
  fight scenes (index 3+, **dynamic index from matchmaking**).
- **Menu is NOT a scene.** Main menu / splash / black screen - Sharq screens inside
  bootstrap scenes. Changing boot→menu - without loading the scene (UI only).
- **Navigation - status graph.** Status Enum (`Init, Logo, MainMenu, Loading,
  LoadingFight, Lobby_SquadList, Lobby_UnitList, InGame, InGame_Modal, ...`);
  screen registry specifies **graph of allowed transitions**; `Navigate(Status)` →
  guard → transition controller (Fade / LoadingGate / ModalSlide / MenuReveal).
- **Loading scenes:** `SceneManager.LoadScene` (**synchronously**, `TransitionSceneSystem` by
  ECS event `ETransitionScene`), and when returning to the lobby - `LoadSceneAsync`.
  **Single mode, without additive.** There is no special loading scene - this is a UI overlay
  (`Status.Loading` + `UITransitionController` curtain).
- **ECS - LeoECS (not DOTS), stage worlds.** The combat world creates a startup in the combat
  stage (per-match), destroyed upon exit; `GameState` singleton; general event world
  bridges ECS↔UI. Connect to the game server at the entrance to the battle, disconnect at the exit.
- **Hybridity of transitions:** part is purely UI (Init→Logo→MainMenu; SquadList↔UnitList
  inside the lobby), partly with a scene change (MainMenu→Lobby, Lobby→Battle, Battle→Lobby).
- **sus-router is NOT integrated yet** - navigation to legacy navigator/status. The goal is to replace.

###0.2. Conclusion: which architecture is suitable NOW?

The client **in fact already implements a hybrid “persistent UI host + change of content scenes”**,
just spread across the UI host / navigator / scene transition system. So,
the router does not need an “ideal” abstract scheme - you need to formalize the same
hybrid** in a single API. Recommended architecture:

1. **Persistent host = bootstrap scene** (not a separate new bootstrap scene): it contains `UIDocument`
   + `SusRouter` + `SusRouteView` + loading overlay. It is experiencing a change in content scenes.
2. **Two classes of routes:** screen-only (within the current scene, like now) and
   scene-backed (requires a content scene). Menu/logo/lobby tabs—screen-only.
3. **Single mode of content scenes** (as now), but **via async with loading-gate**
   (get away from synchronous `LoadScene`, remove micro-freezes).
4. **Dynamic scene key** for battle (matchmaking gives build-index) - not a constant,
   and resolver.
5. **Scene hooks orchestrate the ECS world and network:** `SceneLoaded` → `EcsStartup`/connect,
   `BeforeSceneUnload` → world teardown/disconnect. The router does not know about ECS - it only provides hooks.
6. **`Status`-graph → paths + guards**, transition edges (Fade/LoadingGate/…)
   → `SusRouteTransition`. Legacy navigator/registry are replaced by the router in stages.

**Goal:** describe this hybrid as a single router API, while remaining testable without
strict dependence on SceneManager/Addressables in the core of the package.

---

## 1. Key idea - two levels of route

The route can be one of three types:

| Route type | What's changing | Example |
|---|---|---|
| **Screen-only** (as now) | only UI inside the active scene | `/settings`, modals, tabs |
| **Scene-backed** | Unity scene (world) + its HUD screen | `/battle`, `/main-menu`, `/limbo` |
| **Nested** | scene = world, child screen = layer on top | `/battle` → `/battle/inventory` |

Principle: **the stage gives the “world”, the screen gives the “interface”**. Same world (scene)
can show different screens without reloading the scene (nested/child routes).
Scene change is hard (async), screen change is easy (instant).```
┌──────────────────────────────────────────────────────────────┐
│ Bootstrap host = PERSISTENT (NEVER unloaded) │
│ — UI host singleton → UIDocument (panel), │
│ SusRouter, SusRouteView, loading-overlay, boot-ECS, network │
│  ┌────────────────────────────────────────────────────────┐  │
│ │ Content scene (Single, loaded instead of the previous one) │ │
│ │ Lobby.unity (idx 2) | Battle (idx 3+, dynamic) │ │
│ │ — GameObjects of the world, camera, LeoECS world (EcsStartup) │ │
│  └────────────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────┐  │
│ │ SusRouteView (UI) - HUD screen of the current route │ │
│ │ OverlayHost - modals/tooltypes/loading (above screens) │ │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```**Why the host scene is persistent:** UI panel (both loading overlay and router)
live in it → experience loading/unloading of content scenes. In a typical client, the persistent host is
bootstrap scene (UI host singleton), and not a separate second bootstrap scene.
**Menu is not a scene**, but Sharq-screens inside bootstrap (`Init→Logo→MainMenu` - screen-only).
Content scenes are now loaded into **Single** (`SceneManager.LoadScene`), additive is not used;
loading is a UI overlay (`Status.Loading`), so it does not blink when changing the scene. Agreed
with layer model ([OVERLAY-LAYERS.md](../../sus-core/roadmap/OVERLAY-LAYERS.md)): world-space UI —
in the content scene, the router panel in the persistent host (bootstrap scene).

---

## 2. Limits of responsibility (who does what)```
┌───────────────────────────────────────────────────────────────┐
│ SusRouter (orchestrator) │
│ — route resolution, history, guards, order of operations │
│ — decides whether a scene change is needed; coordinates scene ↔ screen │
├───────────────────────────────────────────────────────────────┤
│ SusSceneService (new layer) │
│ — life cycle of Unity scenes: load/unload/activate, progress │
│ — register of loaded scenes, keep-alive scenes │
│ - works through the ISceneLoader abstraction (NOT directly) │
├───────────────────────────────────────────────────────────────┤
│ ISceneLoader (abstraction, injected) │
│  — SceneManagerLoader (UnityEngine.SceneManagement)            │
│ — AddressablesSceneLoader (optional, separate adapter package) │
│ — FakeSceneLoader (for EditMode tests, without real scenes) │
├───────────────────────────────────────────────────────────────┤
│ SusRouteView + SusScreen (as now) │
│ — mounting the route HUD screen │
└───────────────────────────────────────────────────────────────┘
```**Critical:** the `sus-router` package itself **does not** depend on Addressables and does not cause
`SceneManager` directly in the kernel - only through `ISceneLoader`. This saves
testability (Fake-loader), purity of UPM dependencies and the ability to replace
loading backend (build-index / Addressables / custom).

---

## 3. Data model (extension of the existing one)

### 3.1. Scene descriptor in route config
Add the `Scene` field to [SusRouteConfig](../Runtime/SusRoute.cs):```csharp
public class SusSceneDescriptor
{
public string Key;                 // scene name / build-index / addressable-key
    public SceneLoadMode Mode;         // Single | Additive | Persistent
public bool KeepLoaded;            // keep-alive scenes (do not unload when leaving)
public bool SetActiveOnLoad = true; // make active (for Instantiate/lighting)
public object Payload;             // world initialization data (matchId, seed...)
}

public enum SceneLoadMode
{
Single, // unload the previous content scene, load this one
Additive, // load on top (leave the previous one)
Persistent, // load once, never unload (bootstrap-like)
}
````SusRouteConfig.Scene = new SusSceneDescriptor { Key = "Battle", Mode = Single }`.
If `Scene == null` → screen-only route (current behavior, no change).

### 3.2. Single history record
Current history - list `SusRoute`. Expand: recording stores **both stage and screen**,
so that `Back()`/`Forward()` can restore the scene (or reuse keep-alive):```
HistoryEntry { SusRoute route; SceneHandle sceneHandle; string screenPath; }
```### 3.3. Current state (source of truth)```
CurrentRoute : Prop<SusRoute> // as it is now
ActiveScene : Prop<SceneHandle> // new - active content scene
```---

## 4. Life cycle of transition to a scene-backed route

The sequence `Push("/battle", { matchId = 42 })`, where `/battle` has `Scene`:```
1. Resolve("/battle") → record.Scene != null
2. Guards: beforeEach / route.Guard / screen.BeforeEnter
- can be ASYNCHRONOUS (wait for verification/network). Cancel → Aborted.
3. Is a scene change necessary? (target.Scene.Key != ActiveScene.Key)
─ YES ───────────────────────────── ────────────────────────────┐
3.1 Show loading-transition (overlay in bootstrap panel) │
3.2 BeforeLeave the current screen; BeforeSceneUnload event │
3.3 Left() of the current screen; unmount HUD from SusRouteView │
   3.4  ISceneLoader.LoadAsync(target.Scene)                        │
— progress → loading UI update (0..1) │
— allowSceneActivation gate (wait for assets to be ready) │
3.5 Activate a new scene; unload previous content scene │
(if Mode=Single and the previous one is not KeepLoaded) │
3.6 SceneLoaded hook → boot world systems (ECS, camera, spawn) │
   ───────────────────────────────────────────────────────────────┘
─ NO → skip 3.1–3.6 (only screen change within the same scene)
4. Create/reuse HUD screen (LazyFactory / KeepAlive as now)
5. Inject Router + Props (matchId=42, Payload scenes)
6. SusRouteView.Add(screen) → Entered()
7. Hide loading-transition
8. Update history (route + sceneHandle) and ActiveScene/CurrentRoute
9. afterEach hooks
```Key invariants:
- **UI does not go dark:** loading overlay and panel - in persistent bootstrap scene.
- **Atomicity:** in case of loading error (step 3.4) - rollback to the previous route,
  the result is `NavigationResult.Aborted`, the old scene remains.
- **Order:** first the world is ready (SceneLoaded), then the HUD screen is mounted, otherwise
  the screen will turn to objects in the world that have not yet spawned.

---

## 5. Asynchrony and guards

Scene loading is asynchronous, so transitions become async. API options
(compatible with current synchronous kernel):

- `Task<NavigationResult> PushAsync(path, props)` - the main path for scene-backed.
- Synchronous `Push(...)` remains for screen-only routes (without `Scene`).
- Async-guard: `BeforeEnterAsync(from) : Task<bool>` next to the current synchronous one
  `BeforeEnter`. The router drives the coroutine/Task; blocks for loading time
  re-navigation (use existing `_isNavigating` + `_pendingNavigation`).

Re-entrant: if a new `Push` arrives while the scene is loading - either
queue (`_pendingNavigation`), or cancel the current download
(cancellation token in `ISceneLoader.LoadAsync`).

---

## 6. Keep-alive for scenes (analogous to KeepAlive screens)

Screens already have `KeepAlive` (hiding via `display:none` instead of deleting).
Symmetrical for scenes:

- `SceneDescriptor.KeepLoaded = true` → when leaving the scene **is not unloaded**, but
  deactivated (root objects `SetActive(false)` / system paused).
- When returning (`Back()` or repeated `Push`) - the scene is **reactivated** instantly,
  without async loading. Useful for heavy scenes (combat), where they often return.
- Memory budget: keep-alive scenes hold RAM/VRAM - this is a deliberate trade-off,
  restrict with an explicit flag on specific routes.

---

## 7. Displaying a typical `Status` graph → routes

| Old Status | Route | Scene (client) | Type |
|---|---|---|---|
| Init/Logo/MainMenu | `/boot`, `/logo`, `/main-menu` | bootstrap scene (same) | **screen-only** |
| Loading | transition (loading-gate) | — (UI overlay) | transition |
| Lobby_SquadList / Lobby_UnitList | `/lobby/squads`, `/lobby/units` | lobby scene (idx 2) | scene-backed + screen-only tabs |
| LoadingFight | transition (loading-gate) | — (UI-curtain) | transition |
| InGame/InGame_Modal | `/battle/:matchId` | combat (idx 3+, **dynamic**) | scene-backed (+ modal overlay) |

Notes on hybridity (as in the client now):
- `Init→Logo→MainMenu` and `SquadList↔UnitList` - **without scene change** (screen-only, HUD only).
- `MainMenu→Lobby`, `Lobby→Battle`, `Battle→Lobby` - **with scene change** (scene-backed).
- **Dynamic battle key:** the battle scene comes from matchmaking (`sceneBuildIndex` in
  `ScheduleBattleLaunchFromMatchmaking`), so the `Key` of the scene is not a constant, but a resolver
  (from payload/route parameter).
- **`Status`-graph of allowed transitions** (screen registry) → guards/nested structure
  routes; edge-animation transition controller (Fade / LoadingGate / ModalSlide /
  MenuReveal) → `SusRouteTransition`.

Boot: bootstrap scene is loaded once as a persistent host; first `Replace("/main-menu")` —
this is screen-only (menu in the same scene); The first scene-backed navigation is `Push("/lobby")`.

### 7.1. Orchestration of the ECS world and game server via scene hooks

The router **doesn't know** about ECS/network - it only provides scene lifecycle hooks,
to which the host project attaches its logic:
- `SceneLoaded(Battle)` → startup creates game state/ECS worlds + connect to the game server.
- `BeforeSceneUnload(Battle)` → teardown of the battle world + disconnect.
- Event bridge remains the ECS↔UI bridge; HUD screen `/battle`
  subscribes to it after `Entered()` (order “world → HUD”).

### 7.2. Migration with legacy navigator (step-by-step)

1. `SceneManager.LoadScene` (synchronously) → `PushAsync` + loading-gate (asynchronously).
2. `Status` enum + screen registry graph → paths + router guards.
3. Transition controller edges → `SusRouteTransition`.
4. `Navigate(Status)` → `Router.Push(path)`; UI host loses its role
   God-object-navigator (see [08-implementation-plan.md](../docs/08-implementation-plan.md)).

---

## 8. Public API (extension sketch)```csharp
// Registering a scene-backed route (battle is a dynamic key from matchmaking)
Router.Register("/battle/:matchId", typeof(BattleHudScreen), new SusRouteConfig {
    Scene = new SusSceneDescriptor {
        // Key is a resolver, not a constant: build-index comes from payload
        KeyResolver = ctx => ctx.GetProp<int>("sceneBuildIndex").ToString(),
        Mode = SceneLoadMode.Single,
    },
    Transition = SusRouteTransition.Fade(0.3f),
});

// Limbo - fixed build-index; tabs - screen-only children
Router.Register("/lobby", typeof(LobbyScreen), new SusRouteConfig {
    Scene = new SusSceneDescriptor { Key = "2", Mode = SceneLoadMode.Single },
Children = { /* /lobby/squads, /lobby/units - no scene change */ },
});

// Menu/logo - screen-only in the bootstrap scene (without Scene)
Router.Register("/main-menu", typeof(MainMenuScreen));

// Implementing a loading backend (in bootstrap/UI host)
Router.UseSceneLoader(new SceneManagerLoader());   // or AddressablesSceneLoader

// Navigation
await Router.PushAsync("/battle/42", new() { ["sceneBuildIndex"] = 3 });

// State
Router.ActiveScene.Value;          // current content scene
Router.SceneService.IsLoading;     // async loading in progress

// Scene hooks - the host project binds ECS/network (the router does not know about ECS)
Router.SceneService.BeforeSceneLoad += d => { /* asset preload */ };
Router.SceneService.SceneLoaded += h => { /* ECS startup + connect to game server */ };
Router.SceneService.BeforeSceneUnload += h => { /* world teardown + disconnect */ };
```---

## 9. Navigation state diagram```
        Push/Replace(scene-backed)
Idle ───────────────────────────────▶ Guarding
                                         │ guard=false → Aborted → Idle
                                         ▼ guard=true
Leaving (BeforeLeave, unmounting HUD)
                                         ▼
                                      SceneLoading (async, loading-overlay)
                                         │ error → Rollback → Idle
                                         ▼ progress=1
                                      SceneActivating (unload old / activate new)
                                         ▼
                                      Mounting (Entered HUD, hide overlay)
                                         ▼
Idle (history + ActiveScene updated)
```For a screen-only transition, the path is shorter: `Idle → Guarding → Leaving → Mounting → Idle`
(SceneLoading/Activating branches are skipped).

---

## 10. Design principles (summary)

1. **World/interface separation:** scene = world (async, heavy), screen = HUD (instant).
2. **Bootstrap scene is persistent:** UI panel + router + loading survive scene changes.
3. **The router orchestrates, does not load:** Unity scenes - via `ISceneLoader`, kernel without
   dependencies on SceneManager/Addressables → testable (Fake-loader).
4. **Single History:** recording stores stage AND screen; `Back()` restores the world.
5. **Atomicity and rollback:** loading error → rollback, old scene intact.
6. **Order “world → HUD”:** The screen is mounted only after the scene is ready.
7. **Keep-alive symmetry:** scenes, like screens, can be cached (deactivation).
8. **Async-first for scene-backed:** `PushAsync` + `BeforeEnterAsync`, synchronous
   the path remains for screen-only.
9. **Compliance with a typical client:** persistent host = bootstrap-scene; menu = screen-only; fight =
   scene-backed with a dynamic key from matchmaking.
10. **ECS/network - via hooks, not in the kernel:** `SceneLoaded`/`BeforeSceneUnload` orchestrate
   game world startup and connect/disconnect game server; The router does not know about ECS.

---

## 11. What to implement (high-level checklist)

- [ ] `SusSceneDescriptor` + `SceneLoadMode` + **`KeyResolver`** (dynamic build-index) in `SusRouteConfig`.
- [ ] `ISceneLoader` + `SceneManagerLoader` (build-index/name) (+ `FakeSceneLoader` for tests).
- [ ] `SusSceneService`: scene registry, load/unload/activate, progress, hooks, keep-alive.
- [ ] `SusRouter`: branching scene-backed vs screen-only in the navigation pipeline.
- [ ] `PushAsync`/`ReplaceAsync` + `BeforeEnterAsync`; blocking reentry.
- [ ] `ActiveScene : Prop<SceneHandle>`; expand history recording (scene+screen).
- [ ] Loading-transition, living in a persistent host (bootstrap), and not in the content scene.
- [ ] Scene hooks for ECS/network: `SceneLoaded`→startup+connect, `BeforeSceneUnload`→teardown+disconnect.
- [ ] Rollback on loading error → `NavigationResult.Aborted`.
- [ ] Mapping of the `Status` graph (screen registry) → paths/guards; transition edges → `SusRouteTransition`.
- [ ] Step-by-step replacement of legacy `Navigate(Status)` with `Router.Push(path)` in the host project.
- [ ] Tests (Fake-loader): scene switch, keep-alive, rollback, async-guard, Back with scene restoration.

---

## Related documents
- [SusRouter.cs](../Runtime/SusRouter.cs) - the current navigation core (screens).
- [SusRoute.cs](../Runtime/SusRoute.cs) - `SusRouteConfig` / `SusRouteRecord`.
- [SusScreen.cs](../Runtime/SusScreen.cs) - screen life cycle.
- [OVERLAY-LAYERS.md](../../sus-core/roadmap/OVERLAY-LAYERS.md) - UI layers (screen/world-space).
- [README.md](../README.md) — sus-router’s place in the ecosystem.
- [08-implementation-plan.md](../docs/08-implementation-plan.md) — scene routing implementation plan.
# 7. Comparison with previous navigator (UINavigator)

| | UINavigator (previous) | SusRouter |
|---|---|---|
| **Where** | Inside host project (monolith) | Separate sus-router package |
| **API** | Show\<TScreen\>() + events | Push / Replace / Back / Forward / Go + stack |
| **Guards** | None | Full pipeline (7 guard types) |
| **History** | Manual management | Cursor stack with `_historyIndex` |
| **Modals** | Separate ModalManager | SusModalService + SusRouterModal + stack |
| **Animations** | None | Code-based Fade/Slide |
| **Parameters** | Via DI / fields | Props + Query |
| **Named routes** | None | PushNamed / ReplaceNamed / ResolvePath |
| **Nested routes** | None | Children + Parent + ResolveChain |
| **Redirect / Alias** | None | Yes |
| **Lazy loading** | None | LazyFactory |
| **KeepAlive** | None | Off-DOM LRU cache (`SusScreenOutlet`) |
| **router-link** | None | SusRouteLink + router-link-active |
| **Dependencies** | GameApp, UIManager, ECS | sus-core only |
| **Re-entrancy protection** | None | `_isNavigating` → ` NavigationResult.Busy` (drop, no queue) |

## Key metrics

| Metric | Previous navigator | SusRouter |
|---|---|---|
| Navigation classes | 7+ (monolithic) | 10 (focused) |
| Total lines | ~2,500 | ~1,200 |
| Guards | None | 7 guard types |
| Animations | None | Code-based Fade/Slide |
| Screen parameters | Via DI / fields | Props + Query |

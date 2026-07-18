# 5. Modal dialogs and services

## SusRouterModal — base class

```csharp
public abstract class SusRouterModal : VisualElement
{
    public SusModalService ModalService { get; internal set; }

    // Build modal UI (must override)
    protected abstract void Build();

    // Lifecycle
    protected internal virtual void Shown() { }
    protected internal virtual void BeforeDismiss() { }
    protected internal virtual void Dismissed() { }

    // Close the modal
    public void Dismiss() => ModalService?.Close();
}
```

### Standard modals

```csharp
// GenericModal — dialog with title, text, and OK/Cancel buttons
public class GenericModal : SusRouterModal { ... }

// AuthModal — auth dialog
public class AuthModal : SusRouterModal { ... }
```

## SusModalService — modal stack

Manages the stack via `OverlayHost` from Core.

```csharp
public class SusModalService
{
    public OverlayHost OverlayHost { get; set; }
    public bool HasActiveModal { get; }
    public int StackCount { get; }

    public SusModalEntry Show(Type dialogType, Dictionary<string, object> props = null,
        bool dismissOnClickOutside = true);
    public void Close();
    public void CloseAll();
}

public class SusModalEntry
{
    public VisualElement Overlay;
    public SusRouterModal Dialog;
    public bool DismissOnClickOutside;
}
```

### Flow

```
SusModalService.Show():
    1. Activator.CreateInstance(dialogType)
    2. Create overlay (sus-modal-overlay) + container (sus-modal-container)
    3. Add dialog to container
    4. dialog.ModalService = this;
    5. If dismissOnClickOutside — ClickEvent on overlay → Close()
    6. OverlayHost.AddToOverlay(overlay, OverlayCategory.Modal)
    7. Push onto _stack, return SusModalEntry

SusModalService.Close():
    1. Pop top of _stack
    2. dialog.BeforeDismiss()
    3. OverlayHost.RemoveFromOverlay(overlay)
    4. dialog.Dismissed()
```

### Initialization

```csharp
// Via Init (automatic)
router.Init(overlayHost);

// Or via Mount (creates OverlayHost from SusBootstrap)
router.Mount(root, "/home");
```

### Usage

```csharp
// Direct service access
router.ModalService.Show(typeof(ConfirmDialog), new() {
    ["title"] = "Confirmation",
    ["message"] = "Are you sure?"
}, dismissOnClickOutside: false);

// Stack control
router.ModalService.Close();    // top
router.ModalService.CloseAll(); // all
```

### Stack and click-outside dismiss

- Show → Show → Close closes the **top** (stack semantics)
- `dismissOnClickOutside = true` (default) — click on the dimmed backdrop closes the modal
- `CloseAll()` — closes the entire stack

## SusTransitionService — curtain between screens

```csharp
public class SusTransitionService
{
    public OverlayHost OverlayHost { get; set; }
    public bool IsTransitioning { get; }

    public void FadeOut(float duration = 0.3f, Action onComplete = null);
    public void FadeIn(float duration = 0.3f);
    public void Cancel();
}
```

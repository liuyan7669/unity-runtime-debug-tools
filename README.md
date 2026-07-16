# Runtime Debug Tools

Runtime shortcuts for common Unity debugging workflows.

## Features

- `TimeScaleHotkeyController`: temporarily changes or toggles `Time.timeScale` with a hotkey.
- `UIRuntimeInspector`: finds UI roots marked with `IUIRuntimeInspectorRoot` and exposes their buttons, toggles, and custom shortcut targets at runtime.

## Usage

### Time-scale hotkey

1. Add `TimeScaleHotkeyController` to a GameObject.
2. Configure the default time scale, target time scale, hotkey, and trigger mode.
3. The hotkey is enabled in the Windows Editor by default. Enable `Build Enable` to use it in a player build.

### Runtime UI inspector

1. Implement the empty `IUIRuntimeInspectorRoot` interface on each UI root component without changing its existing base class.
2. Add `UIRuntimeInspector` to a Canvas or parent that contains those UI roots.
3. Enter Play Mode and press `F2` to open the inspector. Roots later in the hierarchy are treated as being in front.

```csharp
using Cowart.RuntimeDebugTools;

public class ExampleUI : MonoBehaviour, IUIRuntimeInspectorRoot
{
}
```

Custom components can implement `IUIRuntimeInspectorShortcutTarget` to appear in the inspector and respond to shortcut triggers.

## Requirements

- Unity 2021.3 or newer
- Unity UI (`com.unity.ugui`)

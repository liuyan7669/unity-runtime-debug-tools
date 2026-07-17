# Runtime Debug Tools

用于 Unity 常见调试流程的运行时快捷工具。

## 功能

- `TimeScaleHotkeyController`：通过快捷键临时修改或切换 `Time.timeScale`。
- `UIRuntimeInspector`：查找实现了 `IUIRuntimeInspectorRoot` 的 UI 根节点，并在运行时显示其中的按钮、开关和自定义快捷操作目标。

## 使用方法

### 时间缩放快捷键

1. 将 `TimeScaleHotkeyController` 添加到场景中的 GameObject。
2. 配置默认时间缩放、目标时间缩放、快捷键和触发模式。
3. 该快捷键默认仅在 Windows Editor 中启用；如需在 Player 构建中使用，请启用 `Build Enable`。

### 运行时 UI 检查器

1. 让每个 UI 根组件实现空接口 `IUIRuntimeInspectorRoot`，不需要修改其现有基类。
2. 将 `UIRuntimeInspector` 添加到包含这些 UI 根节点的 Canvas 或父节点。
3. 进入 Play Mode 后按 `F2` 打开检查器。层级中位置越靠后的 UI 根节点会被视为显示在更前方。

```csharp
using Cowart.RuntimeDebugTools;

public class ExampleUI : MonoBehaviour, IUIRuntimeInspectorRoot
{
}
```

自定义组件可以实现 `IUIRuntimeInspectorShortcutTarget`，从而显示在检查器中并响应快捷操作。

## 环境要求

- Unity 2021.3 或更高版本
- Unity UI（`com.unity.ugui`）

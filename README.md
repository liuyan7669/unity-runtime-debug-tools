# Runtime Debug Tools

用于 Unity 运行时调试和功能测试的快捷工具包，提供时间缩放快捷键和可直接触发 UI 交互的运行时检查器。

## 功能

### TimeScaleHotkeyController

- 通过快捷键修改 `Time.timeScale`
- 支持按住生效、松开恢复
- 支持每次按键在默认和目标速度之间切换
- 默认只在 Windows Editor 中监听
- 可选择在 Player 构建中启用
- 自动跨场景保留
- 自动销毁重复实例

### UIRuntimeInspector

- 使用快捷键打开运行时 IMGUI 检查面板
- 检查最前方 UI 或所有已打开 UI
- 查找 Button、Toggle 和自定义快捷目标
- 为交互组件自动分配键盘快捷键
- 从面板或快捷键直接触发交互
- 显示组件层级路径和交互状态
- 绘制交互区域边框和快捷键标识
- 闪烁定位指定 UI 组件
- 可过滤被前层 UI 遮挡的组件
- 可选择包含未激活组件
- 支持自动追踪界面打开与关闭

## 安装

### 通过 OpenUPM 安装

在 Unity 的 `Edit > Project Settings > Package Manager` 中添加：

```text
Name: package.openupm.com
URL: https://package.openupm.com
Scope: com.cowart.runtime-debug-tools
```

然后通过 `Add package by name` 安装：

```text
com.cowart.runtime-debug-tools
```

### 通过 Git URL 安装

```text
https://github.com/liuyan7669/unity-runtime-debug-tools.git#1.0.1
```

## 时间缩放快捷键

### 基本配置

1. 在场景中创建一个 GameObject。
2. 添加 `TimeScaleHotkeyController`。
3. 配置默认时间缩放、目标时间缩放、快捷键和触发模式。

默认配置：

```text
默认时间缩放：1
目标时间缩放：5
快捷键：U
触发模式：KeepPress
```

### 触发模式

| 模式 | 行为 |
| --- | --- |
| `KeepPress` | 按住快捷键使用目标时间缩放，松开后恢复 |
| `Press` | 每次按下快捷键时在默认值和目标值之间切换 |

组件默认只在 Windows Editor 中生效。需要在构建版本中使用时启用 `Build Enable`。

## 运行时 UI 检查器

### 标记 UI 根节点

让每个需要检查的 UI 根组件实现空接口 `IUIRuntimeInspectorRoot`，不需要修改已有基类：

```csharp
using Cowart.RuntimeDebugTools;
using UnityEngine;

public class ExampleUI : MonoBehaviour, IUIRuntimeInspectorRoot
{
}
```

### 添加检查器

1. 将 `UIRuntimeInspector` 添加到 Canvas 或包含 UI 根节点的父对象。
2. 进入 Play Mode。
3. 按 `F2` 打开检查面板。
4. 在面板中选择检查范围并触发 Button、Toggle 或自定义组件。

层级中位置越靠后的已打开 UI 根节点会被视为显示在更前方。

### 检查范围

- `仅最前界面`：只显示最前方已打开 UI 的交互组件。
- `所有打开界面`：显示全部已打开 UI 的交互组件。

### 面板选项

- 显示未激活组件
- 自动刷新界面列表
- 显示快捷键标识
- 显示交互区域边框
- 隐藏被前层遮挡的组件

面板还提供刷新、展开/折叠、关闭和组件闪烁定位操作。

### 接入自定义交互组件

非 Button、Toggle 组件可以实现 `IUIRuntimeInspectorShortcutTarget`：

```csharp
using Cowart.RuntimeDebugTools;
using UnityEngine;

public class CustomShortcutTarget : MonoBehaviour, IUIRuntimeInspectorShortcutTarget
{
    public bool CanRuntimeInspectorTrigger()
    {
        return isActiveAndEnabled;
    }

    public string GetRuntimeInspectorName()
    {
        return "执行自定义操作";
    }

    public void TriggerRuntimeInspector()
    {
        Debug.Log("自定义操作已触发");
    }
}
```

## 使用建议

- 工程中只保留一个 `TimeScaleHotkeyController` 实例。
- `UIRuntimeInspector` 面向开发与测试流程，不建议作为正式玩家 UI 使用。
- 自动刷新和遮挡检测会产生额外运行时开销，复杂界面中可按需关闭。
- 快捷键基于传统 `Input.GetKeyDown` 输入接口。
- 闪烁间隔使用不受 `Time.timeScale` 影响的时间。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `TimeScaleHotkeyTriggerMode` | 时间缩放快捷键触发模式 |
| `TimeScaleHotkeyController` | 全局时间缩放快捷键组件 |
| `IUIRuntimeInspectorRoot` | UI 检查边界标记接口 |
| `IUIRuntimeInspectorShortcutTarget` | 自定义运行时快捷目标接口 |
| `UIRuntimeInspector` | 运行时 UI 查找、显示和触发工具 |

## 环境要求

- Unity 2021.3 或更高版本
- Unity UI（`com.unity.ugui`）
- 使用传统 Input Manager 快捷键输入

## 许可证

本包使用 MIT License，详情参见仓库中的许可证文件。

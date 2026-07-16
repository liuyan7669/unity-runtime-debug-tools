using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cowart.RuntimeDebugTools
{
/// <summary>
/// 标记一个组件所在的 GameObject 为 UI 运行时检查边界。
/// </summary>
public interface IUIRuntimeInspectorRoot
{
}

/// <summary>
/// 为自定义 UI 组件提供运行时检查器的显示、状态判断和触发入口。
/// </summary>
public interface IUIRuntimeInspectorShortcutTarget
{
    /// <summary>
    /// 判断当前组件是否允许由运行时检查器触发。
    /// </summary>
    /// <returns>允许触发时返回 true，否则返回 false。</returns>
    bool CanRuntimeInspectorTrigger();

    /// <summary>
    /// 获取组件在运行时检查器中的显示名称。
    /// </summary>
    /// <returns>组件的显示名称；返回空字符串时使用组件类型名。</returns>
    string GetRuntimeInspectorName();

    /// <summary>
    /// 执行组件提供给运行时检查器的触发操作。
    /// </summary>
    void TriggerRuntimeInspector();
}

/// <summary>
/// 运行时 UI 调试检查器。
/// 挂载到 Canvas 或 UI 根节点后，可通过快捷键追踪实现 IUIRuntimeInspectorRoot 的 UI，
/// 在 OnGUI 面板中显示可交互组件，并为 Button、Toggle 或自定义快捷目标提供快捷触发、位置标识和闪烁提示。
/// </summary>
[DisallowMultipleComponent]
public class UIRuntimeInspector : MonoBehaviour
{
    /// <summary>
    /// 运行时检查器搜索已打开界面的范围。
    /// </summary>
    private enum InspectMode
    {
        /// <summary>
        /// 只检查层级中最靠前的已打开界面。
        /// </summary>
        FrontUIOnly,

        /// <summary>
        /// 检查层级中所有已打开的界面。
        /// </summary>
        AllOpenUI
    }

    /// <summary>
    /// 记录一个可由运行时检查器显示和触发的组件。
    /// </summary>
    private class InteractiveEntry
    {
        public Transform Owner;
        public Component Component;
        public Selectable Selectable;
        public IUIRuntimeInspectorShortcutTarget CustomTarget;
        public int SerialIndex;
    }

    private static readonly KeyCode[] ShortcutKeys =
    {
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6,
        KeyCode.Alpha7,
        KeyCode.Alpha8,
        KeyCode.Alpha9,
        KeyCode.Alpha0,
        KeyCode.Q,
        KeyCode.W,
        KeyCode.E,
        KeyCode.R,
        KeyCode.T,
        KeyCode.Y,
        KeyCode.U,
        KeyCode.I,
        KeyCode.O,
        KeyCode.P,
        KeyCode.A,
        KeyCode.S,
        KeyCode.D,
        KeyCode.F,
        KeyCode.G,
        KeyCode.H,
        KeyCode.J,
        KeyCode.K,
        KeyCode.L,
        KeyCode.Z,
        KeyCode.X,
        KeyCode.C,
        KeyCode.V,
        KeyCode.B,
        KeyCode.N,
        KeyCode.M
    };

    private static readonly string[] InspectModeNames =
    {
        "仅最前界面",
        "所有打开界面"
    };

    /// <summary>
    /// 打开运行时检查器面板的快捷键。
    /// </summary>
    [SerializeField] private KeyCode inspectKey = KeyCode.F2;

    /// <summary>
    /// 当前使用的界面检查范围。
    /// </summary>
    [SerializeField] private InspectMode inspectMode = InspectMode.AllOpenUI;

    /// <summary>
    /// 是否在组件搜索阶段包含未激活对象。
    /// </summary>
    [SerializeField] private bool showInactiveSelectables;

    /// <summary>
    /// 面板打开时是否持续刷新已打开的界面列表。
    /// </summary>
    [SerializeField] private bool autoTrackFrontUI = true;

    /// <summary>
    /// 是否在可交互组件上显示快捷键标识。
    /// </summary>
    [SerializeField] private bool showShortcutOverlay = true;

    /// <summary>
    /// 是否绘制可交互组件的屏幕区域边框。
    /// </summary>
    [SerializeField] private bool showSelectableBounds = true;

    /// <summary>
    /// 是否过滤被更高层 UI 遮挡的组件。
    /// </summary>
    [SerializeField] private bool hideCoveredComponents = true;

    /// <summary>
    /// 可交互组件的边框颜色。
    /// </summary>
    [SerializeField] private Color interactableBoundsColor = Color.yellow;

    /// <summary>
    /// 不可交互组件的边框颜色。
    /// </summary>
    [SerializeField] private Color disabledBoundsColor = Color.gray;

    /// <summary>
    /// 组件区域边框的绘制宽度。
    /// </summary>
    [SerializeField] private float selectableBoundsThickness = 2f;

    /// <summary>
    /// 定位组件时执行的闪烁次数。
    /// </summary>
    [SerializeField] private int selectFlashCount = 3;

    /// <summary>
    /// 定位闪烁每次明暗切换的间隔，使用不受时间缩放影响的秒数。
    /// </summary>
    [SerializeField] private float selectFlashInterval = 0.12f;

    /// <summary>
    /// 定位闪烁时覆盖组件区域的颜色。
    /// </summary>
    [SerializeField] private Color selectFlashPanelColor = new Color(1f, 0.85f, 0f, 0.28f);

    private readonly List<Transform> openUIRoots = new List<Transform>();
    private readonly List<Component> components = new List<Component>();
    private readonly List<Graphic> graphics = new List<Graphic>();
    private readonly List<Graphic> targetGraphics = new List<Graphic>();
    private readonly List<CanvasGroup> canvasGroups = new List<CanvasGroup>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private readonly List<InteractiveEntry> interactiveEntries = new List<InteractiveEntry>();
    private readonly Vector3[] rectCorners = new Vector3[4];
    private PointerEventData pointerEventData;
    private EventSystem pointerEventSystem;
    private Component flashingSelectComponent;
    private float selectFlashStartTime;
    private Vector2 scrollPosition;
    private Rect windowRect = new Rect(20f, 20f, 340f, 88f);
    private bool panelVisible;
    private bool panelCollapsed = true;
    private GUIStyle windowStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle toolbarButtonStyle;
    private GUIStyle modeButtonStyle;
    private GUIStyle modeButtonActiveStyle;
    private GUIStyle optionToggleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle ownerStyle;
    private GUIStyle entryStyle;
    private GUIStyle entryTitleStyle;
    private GUIStyle miniLabelStyle;
    private GUIStyle statusOnStyle;
    private GUIStyle statusOffStyle;
    private GUIStyle primaryButtonStyle;
    private GUIStyle secondaryButtonStyle;
    private GUIStyle scrollViewStyle;
    private GUIStyle horizontalScrollbarStyle;
    private GUIStyle verticalScrollbarStyle;
    private GUIStyle verticalScrollbarThumbStyle;
    private GUIStyle verticalScrollbarButtonStyle;

    private void Update()
    {
        if (Input.GetKeyDown(inspectKey))
        {
            panelVisible = true;
            RefreshInspectorData();
        }

        if (panelVisible)
        {
            if (autoTrackFrontUI)
            {
                RefreshOpenUIBases();
            }

            RefreshInteractiveEntryCache();
            TriggerShortcutKey();
        }
    }

    private void OnGUI()
    {
        if (!panelVisible)
        {
            return;
        }

        EnsureStyles();

        if (autoTrackFrontUI)
        {
            RefreshOpenUIBases();
        }

        RefreshInteractiveEntryCache();
        DrawShortcutOverlays();
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, GUIContent.none, windowStyle);
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label("UI运行时检查器", titleStyle);
        GUILayout.Label(GetInspectModeName() + "  |  界面 " + openUIRoots.Count + "  |  组件 " + interactiveEntries.Count, subtitleStyle);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("刷新", toolbarButtonStyle, GUILayout.Width(52f), GUILayout.Height(24f)))
        {
            RefreshInspectorData();
        }

        if (GUILayout.Button(panelCollapsed ? "展开" : "折叠", toolbarButtonStyle, GUILayout.Width(52f), GUILayout.Height(24f)))
        {
            panelCollapsed = !panelCollapsed;
            RefreshWindowSize();
        }

        if (GUILayout.Button("关闭", toolbarButtonStyle, GUILayout.Width(52f), GUILayout.Height(24f)))
        {
            panelVisible = false;
        }
        GUILayout.EndHorizontal();

        if (panelCollapsed)
        {
            GUI.DragWindow();
            return;
        }

        GUILayout.Space(6f);
        DrawInspectModeButtons();

        GUILayout.BeginVertical(sectionStyle);
        showInactiveSelectables = GUILayout.Toggle(showInactiveSelectables, "显示未激活组件", optionToggleStyle);
        autoTrackFrontUI = GUILayout.Toggle(autoTrackFrontUI, "自动刷新界面列表", optionToggleStyle);
        showShortcutOverlay = GUILayout.Toggle(showShortcutOverlay, "显示快捷键标识", optionToggleStyle);
        showSelectableBounds = GUILayout.Toggle(showSelectableBounds, "显示交互区域边框", optionToggleStyle);
        hideCoveredComponents = GUILayout.Toggle(hideCoveredComponents, "隐藏被前层遮挡的组件", optionToggleStyle);
        GUILayout.EndVertical();

        if (openUIRoots.Count == 0)
        {
            GUILayout.Label("未找到实现 IUIRuntimeInspectorRoot 的已打开界面。", subtitleStyle);
            GUI.DragWindow();
            return;
        }

        GUILayout.Label("已追踪界面: " + openUIRoots.Count + "    可操作组件: " + interactiveEntries.Count, subtitleStyle);

        GUIStyle oldHorizontalScrollbar = GUI.skin.horizontalScrollbar;
        GUIStyle oldVerticalScrollbar = GUI.skin.verticalScrollbar;
        GUIStyle oldVerticalScrollbarThumb = GUI.skin.verticalScrollbarThumb;
        GUIStyle oldVerticalScrollbarUpButton = GUI.skin.verticalScrollbarUpButton;
        GUIStyle oldVerticalScrollbarDownButton = GUI.skin.verticalScrollbarDownButton;
        GUI.skin.horizontalScrollbar = horizontalScrollbarStyle;
        GUI.skin.verticalScrollbar = verticalScrollbarStyle;
        GUI.skin.verticalScrollbarThumb = verticalScrollbarThumbStyle;
        GUI.skin.verticalScrollbarUpButton = verticalScrollbarButtonStyle;
        GUI.skin.verticalScrollbarDownButton = verticalScrollbarButtonStyle;

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, horizontalScrollbarStyle, verticalScrollbarStyle, scrollViewStyle);
        Transform lastOwner = null;
        for (int i = 0; i < interactiveEntries.Count; i++)
        {
            InteractiveEntry entry = interactiveEntries[i];
            if (entry == null || entry.Component == null)
            {
                continue;
            }

            if (entry.Owner != lastOwner)
            {
                lastOwner = entry.Owner;
                GUILayout.Label("界面: " + lastOwner.name + "  |  " + GetTransformPath(lastOwner), ownerStyle);
            }

            DrawInteractiveEntry(entry);
        }
        GUILayout.EndScrollView();

        GUI.skin.horizontalScrollbar = oldHorizontalScrollbar;
        GUI.skin.verticalScrollbar = oldVerticalScrollbar;
        GUI.skin.verticalScrollbarThumb = oldVerticalScrollbarThumb;
        GUI.skin.verticalScrollbarUpButton = oldVerticalScrollbarUpButton;
        GUI.skin.verticalScrollbarDownButton = oldVerticalScrollbarDownButton;

        GUI.DragWindow();
    }

    /// <summary>
    /// 立即刷新已打开界面、滚动位置和可交互组件缓存。
    /// </summary>
    private void RefreshInspectorData()
    {
        RefreshOpenUIBases();
        scrollPosition = Vector2.zero;
        RefreshInteractiveEntryCache();
    }

    private void RefreshWindowSize()
    {
        if (panelCollapsed)
        {
            windowRect.width = 340f;
            windowRect.height = 88f;
        }
        else
        {
            windowRect.width = 460f;
            windowRect.height = 560f;
        }
    }

    private void EnsureStyles()
    {
        if (windowStyle != null)
        {
            return;
        }

        Texture2D windowTexture = MakeTexture(new Color(0.05f, 0.06f, 0.06f, 0.86f));
        Texture2D sectionTexture = MakeTexture(new Color(0.12f, 0.13f, 0.12f, 0.78f));
        Texture2D entryTexture = MakeTexture(new Color(0.08f, 0.1f, 0.1f, 0.88f));
        Texture2D buttonTexture = MakeTexture(new Color(0.18f, 0.2f, 0.2f, 0.95f));
        Texture2D buttonHoverTexture = MakeTexture(new Color(0.26f, 0.29f, 0.29f, 0.95f));
        Texture2D activeTexture = MakeTexture(new Color(0.9f, 0.62f, 0.18f, 0.95f));
        Texture2D primaryTexture = MakeTexture(new Color(0.14f, 0.45f, 0.52f, 0.95f));
        Texture2D statusOnTexture = MakeTexture(new Color(0.22f, 0.56f, 0.36f, 0.95f));
        Texture2D statusOffTexture = MakeTexture(new Color(0.45f, 0.18f, 0.16f, 0.95f));
        Texture2D scrollTrackTexture = MakeVerticalStripeTexture(8, 2, new Color(0.04f, 0.05f, 0.05f, 0.32f));
        Texture2D scrollThumbTexture = MakeVerticalStripeTexture(8, 4, new Color(0.66f, 0.72f, 0.68f, 0.42f));
        Texture2D transparentTexture = MakeTexture(Color.clear);

        windowStyle = new GUIStyle(GUI.skin.box);
        windowStyle.normal.background = windowTexture;
        windowStyle.padding = new RectOffset(10, 10, 10, 10);
        windowStyle.margin = new RectOffset(0, 0, 0, 0);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 15;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(1f, 0.88f, 0.56f, 1f);
        titleStyle.margin = new RectOffset(0, 0, 0, 0);

        subtitleStyle = new GUIStyle(GUI.skin.label);
        subtitleStyle.fontSize = 12;
        subtitleStyle.normal.textColor = new Color(0.78f, 0.84f, 0.82f, 1f);
        subtitleStyle.margin = new RectOffset(0, 0, 0, 0);

        toolbarButtonStyle = CreateButtonStyle(buttonTexture, buttonHoverTexture, Color.white);
        toolbarButtonStyle.fontSize = 12;

        modeButtonStyle = CreateButtonStyle(buttonTexture, buttonHoverTexture, new Color(0.84f, 0.9f, 0.88f, 1f));
        modeButtonStyle.fontStyle = FontStyle.Bold;

        modeButtonActiveStyle = CreateButtonStyle(activeTexture, activeTexture, Color.white);
        modeButtonActiveStyle.fontStyle = FontStyle.Bold;

        optionToggleStyle = new GUIStyle(GUI.skin.toggle);
        optionToggleStyle.fontSize = 12;
        optionToggleStyle.normal.textColor = new Color(0.88f, 0.92f, 0.9f, 1f);
        optionToggleStyle.onNormal.textColor = Color.white;
        optionToggleStyle.hover.textColor = Color.white;
        optionToggleStyle.onHover.textColor = Color.white;
        optionToggleStyle.margin = new RectOffset(4, 4, 2, 2);

        sectionStyle = new GUIStyle(GUI.skin.box);
        sectionStyle.normal.background = sectionTexture;
        sectionStyle.padding = new RectOffset(8, 8, 6, 6);
        sectionStyle.margin = new RectOffset(0, 0, 6, 6);

        ownerStyle = new GUIStyle(GUI.skin.label);
        ownerStyle.fontSize = 12;
        ownerStyle.fontStyle = FontStyle.Bold;
        ownerStyle.normal.textColor = new Color(1f, 0.77f, 0.42f, 1f);
        ownerStyle.margin = new RectOffset(0, 0, 8, 2);

        entryStyle = new GUIStyle(GUI.skin.box);
        entryStyle.normal.background = entryTexture;
        entryStyle.padding = new RectOffset(8, 8, 6, 6);
        entryStyle.margin = new RectOffset(0, 0, 2, 6);

        entryTitleStyle = new GUIStyle(GUI.skin.label);
        entryTitleStyle.fontSize = 12;
        entryTitleStyle.fontStyle = FontStyle.Bold;
        entryTitleStyle.normal.textColor = Color.white;
        entryTitleStyle.alignment = TextAnchor.MiddleLeft;
        entryTitleStyle.clipping = TextClipping.Clip;

        miniLabelStyle = new GUIStyle(GUI.skin.label);
        miniLabelStyle.fontSize = 11;
        miniLabelStyle.normal.textColor = new Color(0.72f, 0.8f, 0.78f, 1f);
        miniLabelStyle.clipping = TextClipping.Clip;
        miniLabelStyle.margin = new RectOffset(0, 0, 0, 3);

        statusOnStyle = CreateStatusStyle(statusOnTexture);
        statusOffStyle = CreateStatusStyle(statusOffTexture);
        primaryButtonStyle = CreateButtonStyle(primaryTexture, primaryTexture, Color.white);
        primaryButtonStyle.fontStyle = FontStyle.Bold;
        secondaryButtonStyle = CreateButtonStyle(buttonTexture, buttonHoverTexture, Color.white);

        scrollViewStyle = new GUIStyle();
        scrollViewStyle.padding = new RectOffset(0, 4, 0, 0);

        horizontalScrollbarStyle = new GUIStyle(GUIStyle.none);
        horizontalScrollbarStyle.fixedHeight = 0f;

        verticalScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
        verticalScrollbarStyle.normal.background = scrollTrackTexture;
        verticalScrollbarStyle.hover.background = scrollTrackTexture;
        verticalScrollbarStyle.active.background = scrollTrackTexture;
        verticalScrollbarStyle.focused.background = scrollTrackTexture;
        verticalScrollbarStyle.fixedWidth = 8f;
        verticalScrollbarStyle.margin = new RectOffset(0, 0, 0, 0);
        verticalScrollbarStyle.padding = new RectOffset(0, 0, 2, 2);

        verticalScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
        verticalScrollbarThumbStyle.normal.background = scrollThumbTexture;
        verticalScrollbarThumbStyle.hover.background = scrollThumbTexture;
        verticalScrollbarThumbStyle.active.background = scrollThumbTexture;
        verticalScrollbarThumbStyle.focused.background = scrollThumbTexture;
        // 滑块与轨道共用 8 像素宽的布局区域，贴图中央只绘制 4 像素宽的条纹，
        // 避免直接将滑块样式设为 4 像素时出现 IMGUI 默认左对齐。
        verticalScrollbarThumbStyle.fixedWidth = 8f;
        verticalScrollbarThumbStyle.margin = new RectOffset(0, 0, 0, 0);
        verticalScrollbarThumbStyle.padding = new RectOffset(0, 0, 0, 0);

        verticalScrollbarButtonStyle = new GUIStyle(GUIStyle.none);
        verticalScrollbarButtonStyle.normal.background = transparentTexture;
        verticalScrollbarButtonStyle.hover.background = transparentTexture;
        verticalScrollbarButtonStyle.active.background = transparentTexture;
        verticalScrollbarButtonStyle.focused.background = transparentTexture;
        verticalScrollbarButtonStyle.fixedWidth = 8f;
        verticalScrollbarButtonStyle.fixedHeight = 0f;
        verticalScrollbarButtonStyle.margin = new RectOffset(0, 0, 0, 0);
        verticalScrollbarButtonStyle.padding = new RectOffset(0, 0, 0, 0);
    }

    private GUIStyle CreateButtonStyle(Texture2D normalTexture, Texture2D hoverTexture, Color textColor)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.normal.background = normalTexture;
        style.hover.background = hoverTexture;
        style.active.background = hoverTexture;
        style.focused.background = normalTexture;
        style.normal.textColor = textColor;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.padding = new RectOffset(6, 6, 2, 2);
        return style;
    }

    private GUIStyle CreateStatusStyle(Texture2D texture)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.normal.background = texture;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 11;
        style.fontStyle = FontStyle.Bold;
        style.padding = new RectOffset(4, 4, 1, 1);
        return style;
    }

    private Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private Texture2D MakeVerticalStripeTexture(int textureWidth, int stripeWidth, Color color)
    {
        Texture2D texture = new Texture2D(textureWidth, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        int stripeStart = (textureWidth - stripeWidth) / 2;
        int stripeEnd = stripeStart + stripeWidth;
        for (int x = 0; x < textureWidth; x++)
        {
            texture.SetPixel(x, 0, x >= stripeStart && x < stripeEnd ? color : Color.clear);
        }

        texture.Apply();
        return texture;
    }

    private void RefreshOpenUIBases()
    {
        openUIRoots.Clear();
        FindOpenUIRoots(transform, openUIRoots);
    }

    /// <summary>
    /// 根据当前筛选条件重建可交互组件列表。
    /// </summary>
    private void RefreshInteractiveEntryCache()
    {
        interactiveEntries.Clear();
        int serialIndex = 1;
        for (int i = 0; i < openUIRoots.Count; i++)
        {
            Transform uiRoot = openUIRoots[i];
            if (uiRoot == null)
            {
                continue;
            }

            components.Clear();
            uiRoot.GetComponentsInChildren<Component>(showInactiveSelectables, components);
            for (int j = 0; j < components.Count; j++)
            {
                Component component = components[j];
                InteractiveEntry entry;
                if (!TryCreateInteractiveEntry(uiRoot, component, serialIndex, out entry))
                {
                    continue;
                }

                if (!IsEntryInteractable(entry))
                {
                    continue;
                }

                if (hideCoveredComponents && IsBlockedByRuntimeRaycast(component))
                {
                    continue;
                }

                interactiveEntries.Add(entry);
                serialIndex++;
            }
        }
    }

    private bool TryCreateInteractiveEntry(Transform owner, Component component, int serialIndex, out InteractiveEntry entry)
    {
        entry = null;
        if (component == null || !CanUseShortcut(component) || (component.transform != owner && !component.transform.IsChildOf(owner)))
        {
            return false;
        }

        Selectable selectable = component as Selectable;
        IUIRuntimeInspectorShortcutTarget customTarget = component as IUIRuntimeInspectorShortcutTarget;
        if (!IsBuiltInShortcutTarget(selectable) && customTarget == null)
        {
            return false;
        }

        entry = new InteractiveEntry()
        {
            Owner = owner,
            Component = component,
            Selectable = selectable,
            CustomTarget = customTarget,
            SerialIndex = serialIndex
        };
        return true;
    }

    private bool IsBuiltInShortcutTarget(Selectable selectable)
    {
        return selectable is Button || selectable is Toggle;
    }

    private bool FindOpenUIRoots(Transform root, List<Transform> results)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.gameObject.activeInHierarchy && IsUIRuntimeInspectorRoot(child))
            {
                results.Add(child);
                if (inspectMode == InspectMode.FrontUIOnly)
                {
                    return true;
                }
            }

            if (FindOpenUIRoots(child, results) && inspectMode == InspectMode.FrontUIOnly)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsUIRuntimeInspectorRoot(Transform target)
    {
        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IUIRuntimeInspectorRoot)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 绘制一个可交互组件的状态和触发控件。
    /// </summary>
    /// <param name="entry">要绘制的可交互组件记录。</param>
    private void DrawInteractiveEntry(InteractiveEntry entry)
    {
        GUILayout.BeginVertical(entryStyle);
        bool entryInteractable = IsEntryInteractable(entry);
        string shortcutName = HasShortcut(entry) ? GetKeyName(ShortcutKeys[entry.SerialIndex - 1]) : "手动";

        GUILayout.BeginHorizontal();
        GUILayout.Label(entry.SerialIndex + " [" + shortcutName + "]  " + GetEntryDisplayName(entry), entryTitleStyle);

        Selectable selectable = entry.Selectable;
        if (selectable != null)
        {
            GUILayout.Label(entryInteractable ? "可交互" : "不可交互", entryInteractable ? statusOnStyle : statusOffStyle, GUILayout.Width(58f), GUILayout.Height(22f));

            if (GUILayout.Button("闪一下", secondaryButtonStyle, GUILayout.Width(66f), GUILayout.Height(22f)))
            {
                BeginSelectFlash(entry.Component);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Label(GetTransformPath(entry.Component.transform, entry.Owner.transform), miniLabelStyle);

        Button button = entry.Component as Button;
        if (button != null)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && entryInteractable;
            if (GUILayout.Button("点击", primaryButtonStyle, GUILayout.Height(24f)))
            {
                TriggerInteractiveEntry(entry);
            }
            GUI.enabled = oldEnabled;
            GUILayout.EndVertical();
            return;
        }

        Toggle toggle = entry.Component as Toggle;
        if (toggle != null)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && entryInteractable;
            GUILayout.BeginHorizontal();
            GUILayout.Label(toggle.isOn ? "当前: 已勾选" : "当前: 未勾选", miniLabelStyle);
            if (GUILayout.Button("触发", primaryButtonStyle, GUILayout.Width(96f), GUILayout.Height(24f)))
            {
                TriggerInteractiveEntry(entry);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = oldEnabled;
            GUILayout.EndVertical();
            return;
        }

        if (entry.CustomTarget != null)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && entryInteractable;
            if (GUILayout.Button("触发", primaryButtonStyle, GUILayout.Height(24f)))
            {
                TriggerInteractiveEntry(entry);
            }
            GUI.enabled = oldEnabled;
            GUILayout.EndVertical();
            return;
        }

        GUILayout.EndVertical();
    }

    private void DrawInspectModeButtons()
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < InspectModeNames.Length; i++)
        {
            GUIStyle style = i == (int)inspectMode ? modeButtonActiveStyle : modeButtonStyle;
            if (GUILayout.Button(InspectModeNames[i], style, GUILayout.Height(26f)))
            {
                if (i != (int)inspectMode)
                {
                    inspectMode = (InspectMode)i;
                    RefreshInspectorData();
                }
            }
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// 生成可交互组件及其层级路径的完整标题。
    /// </summary>
    /// <param name="entry">目标组件记录。</param>
    /// <returns>包含序号、快捷键、显示名称和层级路径的标题。</returns>
    private string GetInteractiveEntryTitle(InteractiveEntry entry)
    {
        string shortcutName = HasShortcut(entry) ? GetKeyName(ShortcutKeys[entry.SerialIndex - 1]) : "手动";
        string displayName = GetEntryDisplayName(entry);

        return entry.SerialIndex + " [" + shortcutName + "] | " + displayName + " - " + GetTransformPath(entry.Component.transform, entry.Owner.transform);
    }

    private void DrawShortcutOverlays()
    {
        bool hasSelectFlash = IsSelectFlashActive();
        if ((!showShortcutOverlay && !showSelectableBounds && !hasSelectFlash) || interactiveEntries.Count == 0)
        {
            return;
        }

        for (int i = 0; i < interactiveEntries.Count; i++)
        {
            InteractiveEntry entry = interactiveEntries[i];
            if (entry == null || !CanUseShortcut(entry.Component))
            {
                continue;
            }

            Rect targetRect;
            if (!TryGetGUIRect(entry.Component, out targetRect))
            {
                continue;
            }

            bool drawSelectFlash = IsSelectFlashVisible(entry.Component);
            if (showSelectableBounds || drawSelectFlash)
            {
                if (!IsSelectFlashHidden(entry.Component))
                {
                    float thickness = drawSelectFlash ? selectableBoundsThickness * 2f : selectableBoundsThickness;
                    if (drawSelectFlash)
                    {
                        DrawRectFill(targetRect, selectFlashPanelColor);
                    }

                    DrawRectOutline(targetRect, IsEntryInteractable(entry) ? interactableBoundsColor : disabledBoundsColor, thickness);
                }
            }

            if (showShortcutOverlay && HasShortcut(entry))
            {
                string keyName = GetShortcutLabel(entry);
                Rect labelRect = new Rect(
                    Mathf.Clamp(targetRect.xMin, 0f, Screen.width - 64f),
                    Mathf.Clamp(targetRect.yMin, 0f, Screen.height - 24f),
                    64f,
                    24f);

                bool oldEnabled = GUI.enabled;
                GUI.enabled = IsEntryInteractable(entry);
                GUI.Box(labelRect, keyName);
                GUI.enabled = oldEnabled;
            }
        }
    }

    private void BeginSelectFlash(Component component)
    {
        flashingSelectComponent = component;
        selectFlashStartTime = Time.unscaledTime;
    }

    private bool IsSelectFlashActive()
    {
        if (flashingSelectComponent == null)
        {
            return false;
        }

        if (selectFlashCount <= 0 || selectFlashInterval <= 0f)
        {
            flashingSelectComponent = null;
            return false;
        }

        float duration = selectFlashCount * selectFlashInterval * 2f;
        if (Time.unscaledTime - selectFlashStartTime > duration)
        {
            flashingSelectComponent = null;
            return false;
        }

        return true;
    }

    private bool IsSelectFlashVisible(Component component)
    {
        if (!IsSelectFlashActive() || component != flashingSelectComponent)
        {
            return false;
        }

        int phase = Mathf.FloorToInt((Time.unscaledTime - selectFlashStartTime) / selectFlashInterval);
        return phase % 2 == 0;
    }

    private bool IsSelectFlashHidden(Component component)
    {
        return IsSelectFlashActive() && component == flashingSelectComponent && !IsSelectFlashVisible(component);
    }

    private void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawRectFill(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void TriggerShortcutKey()
    {
        for (int i = 0; i < interactiveEntries.Count; i++)
        {
            InteractiveEntry entry = interactiveEntries[i];
            if (entry == null || !CanUseShortcut(entry.Component))
            {
                continue;
            }

            int shortcutIndex = entry.SerialIndex - 1;
            if (!HasShortcut(entry))
            {
                continue;
            }

            if (Input.GetKeyDown(ShortcutKeys[shortcutIndex]))
            {
                TriggerInteractiveEntry(entry);
                return;
            }
        }
    }

    private void TriggerInteractiveEntry(InteractiveEntry entry)
    {
        if (entry == null || !IsEntryInteractable(entry))
        {
            return;
        }

        if (entry.Selectable != null)
        {
            entry.Selectable.Select();
        }

        Button button = entry.Component as Button;
        if (button != null)
        {
            button.onClick.Invoke();
            return;
        }

        Toggle toggle = entry.Component as Toggle;
        if (toggle != null)
        {
            TriggerToggle(toggle);
            return;
        }

        if (entry.CustomTarget != null)
        {
            entry.CustomTarget.TriggerRuntimeInspector();
        }
    }

    private void TriggerToggle(Toggle toggle)
    {
        if (toggle.group != null && !toggle.isOn)
        {
            toggle.isOn = true;
            return;
        }

        toggle.isOn = !toggle.isOn;
    }

    private bool CanUseShortcut(Component component)
    {
        return component != null && component.gameObject.activeInHierarchy && component.transform is RectTransform;
    }

    private bool IsBlockedByRuntimeRaycast(Component component)
    {
        bool raycastChecked;
        bool canHitTarget = CanHitByRuntimeRaycast(component, out raycastChecked);
        if (raycastChecked)
        {
            return !canHitTarget;
        }

        return IsCoveredByVisualLayer(component);
    }

    private bool CanHitByRuntimeRaycast(Component component, out bool raycastChecked)
    {
        raycastChecked = false;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || component == null)
        {
            return true;
        }

        Rect targetRect;
        if (!TryGetGUIRect(component, out targetRect))
        {
            return true;
        }

        if (pointerEventData == null || pointerEventSystem != eventSystem)
        {
            pointerEventData = new PointerEventData(eventSystem);
            pointerEventSystem = eventSystem;
        }

        Vector2 center = targetRect.center;
        Vector2[] samplePoints =
        {
            center,
            new Vector2(Mathf.Lerp(targetRect.xMin, targetRect.xMax, 0.25f), center.y),
            new Vector2(Mathf.Lerp(targetRect.xMin, targetRect.xMax, 0.75f), center.y),
            new Vector2(center.x, Mathf.Lerp(targetRect.yMin, targetRect.yMax, 0.25f)),
            new Vector2(center.x, Mathf.Lerp(targetRect.yMin, targetRect.yMax, 0.75f))
        };

        for (int i = 0; i < samplePoints.Length; i++)
        {
            if (CanHitByRuntimeRaycast(component, samplePoints[i], eventSystem, out bool checkedPoint))
            {
                raycastChecked = true;
                return true;
            }

            raycastChecked |= checkedPoint;
        }

        return false;
    }

    private bool CanHitByRuntimeRaycast(Component component, Vector2 guiPoint, EventSystem eventSystem, out bool raycastChecked)
    {
        raycastChecked = false;
        raycastResults.Clear();
        pointerEventData.Reset();
        pointerEventData.position = new Vector2(guiPoint.x, Screen.height - guiPoint.y);
        eventSystem.RaycastAll(pointerEventData, raycastResults);
        raycastChecked = true;

        if (raycastResults.Count == 0)
        {
            return false;
        }

        Transform targetTransform = component.transform;
        for (int i = 0; i < raycastResults.Count; i++)
        {
            GameObject hitObject = raycastResults[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            Transform hitTransform = hitObject.transform;
            if (hitTransform == targetTransform || hitTransform.IsChildOf(targetTransform))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    private bool IsCoveredByVisualLayer(Component component)
    {
        if (component == null)
        {
            return false;
        }

        Rect targetRect;
        if (!TryGetGUIRect(component, out targetRect))
        {
            return false;
        }

        int targetDepth;
        if (!TryGetTargetGraphicDepth(component, out targetDepth))
        {
            return false;
        }

        Vector2 targetCenter = targetRect.center;
        for (int i = 0; i < openUIRoots.Count; i++)
        {
            Transform uiRoot = openUIRoots[i];
            if (uiRoot == null || !uiRoot.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (IsCoveredByHigherGraphic(component, uiRoot, targetCenter, targetDepth))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetTargetGraphicDepth(Component component, out int depth)
    {
        depth = int.MinValue;
        targetGraphics.Clear();
        component.GetComponentsInChildren(false, targetGraphics);
        for (int i = 0; i < targetGraphics.Count; i++)
        {
            Graphic graphic = targetGraphics[i];
            if (!IsVisibleGraphic(graphic))
            {
                continue;
            }

            depth = Mathf.Max(depth, graphic.depth);
        }

        return depth != int.MinValue;
    }

    private bool IsCoveredByHigherGraphic(Component component, Transform uiRoot, Vector2 targetCenter, int targetDepth)
    {
        graphics.Clear();
        uiRoot.GetComponentsInChildren(false, graphics);
        for (int i = 0; i < graphics.Count; i++)
        {
            Graphic graphic = graphics[i];
            if (!IsVisibleGraphic(graphic) || graphic.depth <= targetDepth || graphic.transform.IsChildOf(component.transform))
            {
                continue;
            }

            Rect graphicRect;
            if (!TryGetGUIRect(graphic, out graphicRect))
            {
                continue;
            }

            if (graphicRect.Contains(targetCenter))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsVisibleGraphic(Graphic graphic)
    {
        return graphic != null && graphic.enabled && graphic.gameObject.activeInHierarchy && graphic.color.a > 0.01f && graphic.canvasRenderer.GetAlpha() > 0.01f;
    }

    private bool HasShortcut(InteractiveEntry entry)
    {
        return entry != null && entry.SerialIndex > 0 && entry.SerialIndex <= ShortcutKeys.Length;
    }

    private bool IsEntryInteractable(InteractiveEntry entry)
    {
        if (entry == null || entry.Component == null || !entry.Component.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!IsComponentEnabled(entry.Component) || !CanInteractWithCanvasGroups(entry.Component))
        {
            return false;
        }

        bool raycastChecked;
        if (!CanHitByRuntimeRaycast(entry.Component, out raycastChecked) && raycastChecked)
        {
            return false;
        }

        if (entry.Selectable != null)
        {
            if (!entry.Selectable.IsActive() || !entry.Selectable.IsInteractable())
            {
                return false;
            }

            return true;
        }

        return entry.CustomTarget != null && entry.CustomTarget.CanRuntimeInspectorTrigger();
    }

    private bool IsComponentEnabled(Component component)
    {
        Behaviour behaviour = component as Behaviour;
        return behaviour == null || behaviour.enabled;
    }

    private bool CanInteractWithCanvasGroups(Component component)
    {
        canvasGroups.Clear();
        component.GetComponentsInParent(false, canvasGroups);
        for (int i = 0; i < canvasGroups.Count; i++)
        {
            CanvasGroup canvasGroup = canvasGroups[i];
            if (canvasGroup == null || !canvasGroup.enabled)
            {
                continue;
            }

            if (canvasGroup.alpha < 0.999f || !canvasGroup.interactable || !canvasGroup.blocksRaycasts)
            {
                return false;
            }

            if (canvasGroup.ignoreParentGroups)
            {
                break;
            }
        }

        return true;
    }

    private string GetEntryDisplayName(InteractiveEntry entry)
    {
        if (entry.CustomTarget != null)
        {
            string customName = entry.CustomTarget.GetRuntimeInspectorName();
            if (!string.IsNullOrEmpty(customName))
            {
                return customName;
            }
        }

        return entry.Component.GetType().Name;
    }

    private bool TryGetGUIRect(Component component, out Rect rect)
    {
        rect = Rect.zero;

        RectTransform rectTransform = component.transform as RectTransform;
        if (rectTransform == null)
        {
            return false;
        }

        Canvas canvas = component.GetComponentInParent<Canvas>();
        Camera camera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera;
        }

        rectTransform.GetWorldCorners(rectCorners);
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int i = 0; i < rectCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, rectCorners[i]);
            float guiY = Screen.height - screenPoint.y;
            minX = Mathf.Min(minX, screenPoint.x);
            minY = Mathf.Min(minY, guiY);
            maxX = Mathf.Max(maxX, screenPoint.x);
            maxY = Mathf.Max(maxY, guiY);
        }

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return rect.width > 0f && rect.height > 0f;
    }

    private string GetKeyName(KeyCode keyCode)
    {
        string keyName = keyCode.ToString();
        if (keyName.StartsWith("Alpha"))
        {
            return keyName.Substring(5);
        }

        return keyName;
    }

    private string GetInspectModeName()
    {
        int index = (int)inspectMode;
        if (index >= 0 && index < InspectModeNames.Length)
        {
            return InspectModeNames[index];
        }

        return inspectMode.ToString();
    }

    private string GetShortcutLabel(InteractiveEntry entry)
    {
        return entry.SerialIndex + "[" + GetKeyName(ShortcutKeys[entry.SerialIndex - 1]) + "]";
    }

    private string GetTransformPath(Transform target, Transform root = null)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
}

using UnityEngine;

namespace Cowart.RuntimeDebugTools
{
    /// <summary>
    /// 时间缩放快捷键的触发模式。
    /// </summary>
    public enum TimeScaleHotkeyTriggerMode
    {
        /// <summary>
        /// 按住快捷键时应用目标时间缩放，松开后恢复默认时间缩放。
        /// </summary>
        KeepPress,

        /// <summary>
        /// 每次按下快捷键时在目标时间缩放和默认时间缩放之间切换。
        /// </summary>
        Press
    }

    /// <summary>
    /// 全局时间缩放快捷键控制器。
    /// 请保持工程中只有一个实例；组件会在运行时跨场景保留。
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeScaleHotkeyController : MonoBehaviour
    {
        /// <summary>
        /// 未触发快捷键时使用的时间缩放值。
        /// </summary>
        [Header("默认时间缩放（关闭加速时恢复的 Time.timeScale）")]
        public float originTimeScale = 1f;

        /// <summary>
        /// 触发快捷键后使用的时间缩放值。
        /// </summary>
        [Header("目标时间缩放（触发快捷键后应用的 Time.timeScale）"), Range(0f, 10f)]
        public float changeTimeScale = 5f;

        /// <summary>
        /// 控制时间缩放的快捷键。
        /// </summary>
        [Header("触发快捷键")]
        public KeyCode triggerKey = KeyCode.U;

        /// <summary>
        /// 快捷键的触发模式。
        /// </summary>
        [Header("触发模式")]
        public TimeScaleHotkeyTriggerMode triggerType = TimeScaleHotkeyTriggerMode.KeepPress;

        /// <summary>
        /// 是否允许在非 Windows Editor 环境中监听快捷键。
        /// </summary>
        [Header("发布版本启用")]
        public bool buildEnable = false;

        private static GameObject instanceObject;
        private static TimeScaleHotkeyController instance;

        private int pressCount;

        /// <summary>
        /// 重置组件在编辑器中的默认位置和对象名称。
        /// </summary>
        private void Reset()
        {
            transform.position = Vector3.zero;
            gameObject.name = "[时间缩放快捷键控制器]";
        }

        /// <summary>
        /// 保留首个实例，并销毁后续重复实例。
        /// </summary>
        private void Awake()
        {
            if (instanceObject == null)
            {
                instanceObject = gameObject;
                DontDestroyOnLoad(instanceObject);
                instance = this;
            }
            else if (!instanceObject.Equals(gameObject))
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 根据当前运行环境决定是否监听时间缩放快捷键。
        /// </summary>
        private void Update()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                ListenForTimeScaleHotkey();
            }
            else if (buildEnable)
            {
                ListenForTimeScaleHotkey();
            }
        }

        /// <summary>
        /// 根据触发模式处理时间缩放快捷键输入。
        /// </summary>
        private void ListenForTimeScaleHotkey()
        {
            switch (triggerType)
            {
                case TimeScaleHotkeyTriggerMode.KeepPress:
                    if (Input.GetKeyDown(triggerKey))
                    {
                        Time.timeScale = changeTimeScale;
                    }

                    if (Input.GetKeyUp(triggerKey))
                    {
                        Time.timeScale = originTimeScale;
                    }

                    break;
                case TimeScaleHotkeyTriggerMode.Press:
                    if (Input.GetKeyDown(triggerKey))
                    {
                        pressCount++;
                        if (pressCount == 1)
                        {
                            Time.timeScale = changeTimeScale;
                        }
                        else if (pressCount > 1)
                        {
                            Time.timeScale = originTimeScale;
                            pressCount = 0;
                        }
                    }

                    break;
            }
        }
    }
}

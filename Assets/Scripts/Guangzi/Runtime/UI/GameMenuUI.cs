using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Guangzi.Runtime.UI
{
    /// <summary>
    /// ESC 菜单。三个状态：隐藏 → 主页面（设置 / 退出游戏）→ 设置页（两个拖拽反转开关）。
    ///
    /// **刻意不暂停游戏**（pauseGame 默认关）：第一周没有战斗、没有计时，
    /// Time.timeScale = 0 只会让相机停摆，反而看不出菜单是不是真在工作。
    /// 等时间系统接上再把 pauseGame 打开。
    ///
    /// 输入走新 Input System 的 Keyboard.current——工程 activeInputHandler = 1，
    /// 旧版 Input.GetKeyDown 会直接抛异常。
    /// </summary>
    public class GameMenuUI : MonoBehaviour
    {
        [Header("页面（留空会自动按名字在子级里找）")]
        [SerializeField] private GameObject menuOverlay;
        [SerializeField] private GameObject mainPage;
        [SerializeField] private GameObject settingsPage;
        [SerializeField] private Text titleText;

        [Header("页面标题")]
        [SerializeField] private string mainTitle = "已暂停";
        [SerializeField] private string settingsTitle = "设置";

        [Header("主页面按钮")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("设置页")]
        [SerializeField] private Button backButton;
        [SerializeField] private Toggle invertDragXToggle;
        [SerializeField] private Toggle invertDragYToggle;
        [SerializeField] private Slider dragSensitivitySlider;
        [SerializeField] private Text dragSensitivityValue;

        [Header("拖拽灵敏度范围（滑块映射到这个区间）")]
        [Tooltip("实际位移 = 灵敏度 × 相机距离 × 鼠标像素，distance=11 时：0.002≈2.2米/100px，0.02≈22米/100px")]
        [SerializeField] private float sensitivityMin = 0.002f;
        [SerializeField] private float sensitivityMax = 0.03f;
        [SerializeField] private float sensitivityDefault = 0.02f;

        [Header("目标相机（留空自动找）")]
        [SerializeField] private GridCamera targetCamera;

        [Header("行为")]
        [Tooltip("打开菜单时把 Time.timeScale 设 0。第一周建议关着")]
        [SerializeField] private bool pauseGame = false;

        [Tooltip("开关菜单的按键")]
        [SerializeField] private Key toggleKey = Key.Escape;

        [Header("设置存档键名（PlayerPrefs）")]
        [SerializeField] private string invertDragXPrefKey = "guangzi.invertDragX";
        [SerializeField] private string invertDragYPrefKey = "guangzi.invertDragY";
        [SerializeField] private string dragSensitivityPrefKey = "guangzi.dragSensitivity";

        private float _previousTimeScale = 1f;
        private bool _bound;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            EnsureEventSystem();
            ResolveReferences();
            BindCallbacks();
            LoadSettings();
            SetOverlayVisible(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var control = keyboard[toggleKey];
            if (control != null && control.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void OnDisable()
        {
            // 被外部 SetActive(false) 或场景卸载时，别把全局输入状态卡在 Menu 里
            Close();
        }

        // ---------- 开关 ----------

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;

            // 切到菜单模式：世界侧控制器（建造 / 相机拖拽 / 存档快捷键）全部停手
            InputModeService.Set(InputMode.Menu);

            _previousTimeScale = Time.timeScale;
            if (pauseGame) Time.timeScale = 0f;

            SetOverlayVisible(true);
            ShowMain();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;

            // 切回世界模式。放在关闭 UI 之前：避免这一帧的残留输入被世界侧吃掉
            InputModeService.Set(InputMode.World);

            if (pauseGame) Time.timeScale = _previousTimeScale;
            SetOverlayVisible(false);
        }

        public void ShowMain()
        {
            if (mainPage != null) mainPage.SetActive(true);
            if (settingsPage != null) settingsPage.SetActive(false);
            if (titleText != null) titleText.text = mainTitle;
        }

        public void ShowSettings()
        {
            if (mainPage != null) mainPage.SetActive(false);
            if (settingsPage != null) settingsPage.SetActive(true);
            if (titleText != null) titleText.text = settingsTitle;
        }

        // ---------- 退出 ----------

        public void QuitGame()
        {
            Close();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- 设置项 ----------

        private void OnInvertDragXChanged(bool value)
        {
            if (targetCamera != null) targetCamera.InvertDragX = value;
            PlayerPrefs.SetInt(invertDragXPrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnInvertDragYChanged(bool value)
        {
            if (targetCamera != null) targetCamera.InvertDragY = value;
            PlayerPrefs.SetInt(invertDragYPrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnDragSensitivityChanged(float value)
        {
            if (targetCamera != null) targetCamera.DragSensitivity = value;
            UpdateSensitivityLabel(value);
            PlayerPrefs.SetFloat(dragSensitivityPrefKey, value);
            PlayerPrefs.Save();
        }

        private void UpdateSensitivityLabel(float value)
        {
            if (dragSensitivityValue != null) dragSensitivityValue.text = value.ToString("F3");
        }

        private void LoadSettings()
        {
            bool invertX = PlayerPrefs.GetInt(invertDragXPrefKey, 0) == 1;
            bool invertY = PlayerPrefs.GetInt(invertDragYPrefKey, 0) == 1;

            if (targetCamera != null)
            {
                targetCamera.InvertDragX = invertX;
                targetCamera.InvertDragY = invertY;
            }

            // SetIsOnWithoutNotify：回填开关状态时不要触发回调，
            // 否则读档后会立刻把设置又写一遍
            if (invertDragXToggle != null) invertDragXToggle.SetIsOnWithoutNotify(invertX);
            if (invertDragYToggle != null) invertDragYToggle.SetIsOnWithoutNotify(invertY);

            float sensitivity = Mathf.Clamp(
                PlayerPrefs.GetFloat(dragSensitivityPrefKey, sensitivityDefault),
                sensitivityMin,
                sensitivityMax);

            if (targetCamera != null) targetCamera.DragSensitivity = sensitivity;

            if (dragSensitivitySlider != null)
            {
                dragSensitivitySlider.minValue = sensitivityMin;
                dragSensitivitySlider.maxValue = sensitivityMax;
                dragSensitivitySlider.SetValueWithoutNotify(sensitivity);
            }

            UpdateSensitivityLabel(sensitivity);
        }

        // ---------- 内部 ----------

        private void BindCallbacks()
        {
            if (_bound) return;
            _bound = true;

            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
            if (backButton != null) backButton.onClick.AddListener(ShowMain);

            if (invertDragXToggle != null) invertDragXToggle.onValueChanged.AddListener(OnInvertDragXChanged);
            if (invertDragYToggle != null) invertDragYToggle.onValueChanged.AddListener(OnInvertDragYChanged);
            if (dragSensitivitySlider != null) dragSensitivitySlider.onValueChanged.AddListener(OnDragSensitivityChanged);
        }

        private void ResolveReferences()
        {
            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();
            if (menuOverlay == null) menuOverlay = FindChild(transform, "MenuOverlay");
            if (mainPage == null) mainPage = FindChild(transform, "MainPage");
            if (settingsPage == null) settingsPage = FindChild(transform, "SettingsPage");

            if (titleText == null)
            {
                var titleGo = FindChild(transform, "Title");
                if (titleGo != null) titleText = titleGo.GetComponent<Text>();
            }

            if (dragSensitivitySlider == null)
            {
                var sliderGo = FindChild(transform, "Slider_DragSensitivity");
                if (sliderGo != null) dragSensitivitySlider = sliderGo.GetComponent<Slider>();
            }

            if (dragSensitivityValue == null)
            {
                var valueGo = FindChild(transform, "SensitivityValue");
                if (valueGo != null) dragSensitivityValue = valueGo.GetComponent<Text>();
            }
        }

        private void SetOverlayVisible(bool visible)
        {
            if (menuOverlay != null) menuOverlay.SetActive(visible);
        }

        private static GameObject FindChild(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i].gameObject;
            }
            return null;
        }

        /// <summary>
        /// 新 Input System 下 uGUI 必须用 InputSystemUIInputModule，用旧的 StandaloneInputModule 按钮点不动。
        /// 这是从 LudumDare60 抄来的唯一一段关键代码——踩过一次的人才会写这个兜底。
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}

using Guangzi.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 手写 3D 俯视相机（grid camera）。**刻意不用 Cinemachine**：
    /// 需求只有固定俯角 + 平移 + 缩放 + 跟随，Cinemachine 的混合/震动一个都用不上，
    /// 反而多一个包依赖和一层看不见的行为。
    ///
    /// 两套视角（按 R 切换），各自记一套构图，切回来还是你调好的那套：
    ///   God       —— 上帝视角：自由镜头，WASD 平移、拖拽、滚轮缩放
    ///   Character —— 角色视角（星露谷式）：镜头跟随角色，WASD 交给角色
    ///
    /// **WASD 的归属跟着视角走**，不是永久解绑：
    ///   上帝视角 → WASD 平移镜头
    ///   角色视角 → WASD 走角色
    /// 角色控制器通过 AllowsPlayerControl 判断自己该不该响应。
    ///
    /// 输入走新 Input System（工程 activeInputHandler = 1，仅新 Input System，
    /// 用旧版 Input.GetAxis 会直接抛异常，别改回去）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]
    public class GridCamera : MonoBehaviour
    {
        [Header("上帝视角")]
        [Tooltip("俯角：90 = 正俯视，0 = 平视。构图差别全靠这个")]
        [SerializeField] private float pitch = 60f;

        [SerializeField] private float yaw = 0f;

        [Tooltip("相机到注视点的距离，即缩放")]
        [SerializeField] private float distance = 22f;

        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 45f;

        [Header("角色视角（星露谷式，镜头跟随角色）")]
        [SerializeField] private float characterPitch = 55f;

        [SerializeField] private float characterDistance = 10f;

        [Tooltip("跟随平滑，越大跟得越紧。0 = 硬跟随（镜头一动不差）")]
        [SerializeField] private float followSmooth = 10f;

        [Header("视角切换")]
        [SerializeField] private Key toggleViewKey = Key.R;

        [SerializeField] private CameraViewMode startView = CameraViewMode.God;

        [Tooltip("跟随目标，通常把 Player 拖进来。留空会自动找 PlayerController")]
        [SerializeField] private Transform followTarget;

        [Header("平移 / 缩放手感")]
        [Tooltip("WASD 平移速度，单位 米/秒（仅上帝视角）")]
        [SerializeField] private float keyboardPanSpeed = 14f;

        [Tooltip("上帝视角能不能用 WASD 平移镜头。角色视角下这个开关不生效——WASD 一定归角色")]
        [SerializeField] private bool keyboardPanEnabled = true;

        [Tooltip("拖拽灵敏度，会再乘以当前距离，所以远近手感一致")]
        [SerializeField] private float dragSensitivity = 0.02f;

        [Header("拖拽方向反转（ESC 菜单里也能开关，这里勾选同样生效）")]
        [SerializeField] private bool invertDragX = false;
        [SerializeField] private bool invertDragY = false;

        [SerializeField] private float zoomStep = 2.5f;

        [Header("边界")]
        [SerializeField] private bool clampToMap = true;

        [Header("开局")]
        [SerializeField] private bool centerOnMapAtStart = true;

        private Vector3 _pivot;
        private Camera _camera;
        private CameraViewMode _viewMode;

        // 切到角色视角前，把上帝视角的构图记下来，切回来还原
        private float _savedGodPitch;
        private float _savedGodDistance;

        public CameraViewMode ViewMode => _viewMode;

        /// <summary>当前是不是角色视角。</summary>
        public bool IsCharacterView => _viewMode == CameraViewMode.Character;

        /// <summary>角色能不能被 WASD 操控——只有角色视角下能。角色控制器查这个。</summary>
        public bool AllowsPlayerControl => _viewMode == CameraViewMode.Character;

        public float Pitch
        {
            get => pitch;
            set => pitch = Mathf.Clamp(value, 15f, 90f);
        }

        public bool InvertDragX
        {
            get => invertDragX;
            set => invertDragX = value;
        }

        public bool InvertDragY
        {
            get => invertDragY;
            set => invertDragY = value;
        }

        /// <summary>当前相机到注视点的距离。DynamicPixelScale 靠它算像素密度。</summary>
        public float CameraDistance => distance;

        /// <summary>拖拽灵敏度。会再乘以当前 distance，所以远近手感一致。ESC 设置里的滑块改的就是它。</summary>
        public float DragSensitivity
        {
            get => dragSensitivity;
            set => dragSensitivity = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
            var world = GridWorld.Instance;
            if (centerOnMapAtStart && world != null)
            {
                _pivot = world.MapWorldCenter();
            }

            if (followTarget == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) followTarget = player.transform;
            }

            // _viewMode 默认就是 God(0)；只有开局设成角色视角时才需要走一次切换逻辑
            if (startView == CameraViewMode.Character && followTarget != null)
            {
                SetViewMode(CameraViewMode.Character);
            }

            Apply();
        }

        private void Update()
        {
            if (!InputModeService.IsWorld) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            var control = kb[toggleViewKey];
            if (control != null && control.wasPressedThisFrame)
            {
                ToggleViewMode();
            }
        }

        private void LateUpdate()
        {
            HandleInput();
            Apply();
        }

        // ---------- 视角切换 ----------

        public void ToggleViewMode()
        {
            SetViewMode(_viewMode == CameraViewMode.God ? CameraViewMode.Character : CameraViewMode.God);
        }

        public void SetViewMode(CameraViewMode mode)
        {
            if (_viewMode == mode) return;

            // 离开上帝视角前先记下构图
            if (_viewMode == CameraViewMode.God)
            {
                _savedGodPitch = pitch;
                _savedGodDistance = distance;
            }

            _viewMode = mode;

            if (mode == CameraViewMode.Character)
            {
                pitch = characterPitch;
                distance = Mathf.Clamp(characterDistance, minDistance, maxDistance);

                // 立刻吸附到角色，否则镜头会从旧位置飘过去一段
                if (followTarget != null)
                {
                    _pivot = new Vector3(followTarget.position.x, 0f, followTarget.position.z);
                }
            }
            else
            {
                if (_savedGodPitch > 0f) pitch = _savedGodPitch;
                if (_savedGodDistance > 0f) distance = _savedGodDistance;
            }

            Apply();
        }

        // ---------- 每帧输入 ----------

        private void HandleInput()
        {
            // 菜单打开时不要动镜头：否则在设置面板上拖鼠标，背景地图会跟着晃。
            if (!InputModeService.IsWorld) return;

            var world = GridWorld.Instance;
            float cell = world != null ? world.CellSize : 1f;

            // 滚轮缩放在两种视角下都能用——看远看近是通用需求
            HandleZoom();

            if (_viewMode == CameraViewMode.Character)
            {
                FollowTarget();
            }
            else
            {
                HandleKeyboardPan(cell);
                HandleDragPan(cell);
            }

            ClampPivot(world);
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) <= 0.01f) return;

            distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }

        private void HandleKeyboardPan(float cell)
        {
            if (!keyboardPanEnabled) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            float x = 0f;
            float z = 0f;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;

            if (Mathf.Abs(x) < 0.01f && Mathf.Abs(z) < 0.01f) return;

            var flatRot = Quaternion.Euler(0f, yaw, 0f);
            var forward = flatRot * Vector3.forward;
            var right = flatRot * Vector3.right;
            var dir = (forward * z + right * x).normalized;

            _pivot += dir * (keyboardPanSpeed * cell * Time.deltaTime);
        }

        private void HandleDragPan(float cell)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // 中键或右键拖拽（左键留给建造）
            bool dragging = mouse.middleButton.isPressed || mouse.rightButton.isPressed;
            if (!dragging) return;

            Vector2 delta = mouse.delta.ReadValue();
            if (invertDragX) delta.x = -delta.x;
            if (invertDragY) delta.y = -delta.y;

            float scale = dragSensitivity * distance * cell;
            var flatRot = Quaternion.Euler(0f, yaw, 0f);
            var forward = flatRot * Vector3.forward;
            var right = flatRot * Vector3.right;

            // 拖拽方向：水平（right 项）和垂直（forward 项）的符号**刻意不同**，别顺手统一。
            //   forward 项：鼠标往上拖 → 地图往上走（「抓住地图拖」的手感）
            //   right 项  ：鼠标往右拖 → 地图往左走（「推镜头」的手感）
            // 两边符号都是实机确认过的。哪边又觉得反了，只把对应那一项的 + / - 对调，不要动另一项。
            _pivot += (right * delta.x + forward * delta.y) * scale;
        }

        private void FollowTarget()
        {
            if (followTarget == null) return;

            Vector3 target = followTarget.position;
            var desired = new Vector3(target.x, 0f, target.z);

            if (followSmooth <= 0.001f)
            {
                _pivot = desired;
                return;
            }

            // 帧率无关的指数平滑，比固定 lerp 系数稳
            float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
            _pivot = Vector3.Lerp(_pivot, desired, t);
        }

        private void ClampPivot(GridWorld world)
        {
            if (!clampToMap || world == null) return;

            float w = world.Width * world.CellSize;
            float h = world.Height * world.CellSize;

            _pivot.x = Mathf.Clamp(_pivot.x, 0f, w);
            _pivot.z = Mathf.Clamp(_pivot.z, 0f, h);
            _pivot.y = 0f;
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var position = _pivot - rotation * Vector3.forward * distance;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>提供给工具的接口：把相机对准某个格。</summary>
        public void FocusOn(GridPos pos)
        {
            var world = GridWorld.Instance;
            if (world == null) return;

            _pivot = world.GridToWorldCenter(pos);
            Apply();
        }
    }
}

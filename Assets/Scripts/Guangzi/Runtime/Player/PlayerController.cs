using UnityEngine;
using UnityEngine.InputSystem;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 玩家角色控制器（player controller）。WASD 移动 + 空格跳跃。
    ///
    /// **为什么不用 CharacterController / Rigidbody**：
    /// 地面永远在 y = 0，阻挡信息全在 GridMap.passable 里。
    /// 挂物理组件只会多出一层要调的参数（mass / drag / 碰撞矩阵 / 台阶高度 / 斜坡角度），
    /// 而且和「网格是唯一真相源」这条架构约束打架——
    /// 一旦物理和格数据各说各话，就会出现「人贴着墙缝钻过去」这种查半天的问题。
    /// 这里用「格阻挡 + 自己算重力」，规则可预测、可单元测试。
    ///
    /// 输入遵守 InputModeService：ESC 菜单打开时整体停手。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        [Tooltip("水平移动速度，米/秒")]
        [SerializeField] private float moveSpeed = 4.5f;

        [Tooltip("转身平滑速度，越大转得越快")]
        [SerializeField] private float turnSpeed = 14f;

        [Tooltip("角色占地半径，用于判断能不能站到某格。0.35 ≈ 占七成格宽")]
        [SerializeField] private float bodyRadius = 0.35f;

        [Header("跳跃")]
        [Tooltip("能跳多高，米")]
        [SerializeField] private float jumpHeight = 1.1f;

        [Tooltip("重力加速度（负值）。越大下落越快、跳跃手感越「脆」")]
        [SerializeField] private float gravity = -26f;

        [Header("视图")]
        [Tooltip("负责旋转朝向的子物体，留空则旋转自身")]
        [SerializeField] private Transform visual;

        [Header("相机（留空自动找）")]
        [Tooltip("只有角色视角下 WASD 才控制角色；上帝视角下 WASD 归镜头")]
        [SerializeField] private GridCamera targetCamera;

        private GridWorld _world;
        private Vector3 _moveDirection = Vector3.forward;
        private float _verticalVelocity;
        private float _footY;
        private bool _grounded = true;

        public bool IsGrounded => _grounded;

        private void Awake()
        {
            if (visual == null) visual = transform;
        }

        private void Start()
        {
            if (_world == null) _world = GridWorld.Instance;

            if (_world == null)
            {
                Debug.LogError("[PlayerController] 场景里找不到 GridWorld，角色无法工作。");
                enabled = false;
                return;
            }

            // 出生在地图正中心
            Vector3 center = _world.MapWorldCenter();
            transform.position = new Vector3(center.x, 0f, center.z);
            _footY = 0f;

            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();
        }

        private void Update()
        {
            if (!InputModeService.IsWorld) return;

            // 上帝视角下 WASD 归镜头，角色不响应。
            // 这样两个控制器永远不会抢同一个键，也不用把 WASD 从镜头上永久解绑。
            if (targetCamera != null && !targetCamera.AllowsPlayerControl) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 input = ReadMoveInput(keyboard);
            TryJump(keyboard);
            UpdateVertical();
            bool moved = ApplyHorizontal(input);
            UpdateFacing(moved);
        }

        // ---------- 输入 ----------

        private static Vector2 ReadMoveInput(Keyboard keyboard)
        {
            float x = 0f;
            float y = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;

            var v = new Vector2(x, y);

            // 斜向不能比直线快
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        private void TryJump(Keyboard keyboard)
        {
            if (!_grounded) return;
            if (keyboard.spaceKey == null || !keyboard.spaceKey.wasPressedThisFrame) return;

            // 由目标高度反推初速度：v = sqrt(-2 * g * h)
            _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            _grounded = false;
        }

        // ---------- 移动 ----------

        private bool ApplyHorizontal(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return false;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            var cam = Camera.main;
            if (cam != null)
            {
                forward = cam.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

                right = cam.transform.right;
                right.y = 0f;
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
                right.Normalize();
            }

            // 移动方向相对相机——这样旋转视角后「按 W 还是往前走」
            Vector3 dir = forward * input.y + right * input.x;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();

            _moveDirection = dir;

            Vector3 step = dir * (moveSpeed * Time.deltaTime);
            Vector3 current = transform.position;

            if (CanOccupy(current + step))
            {
                SetPosition(current.x + step.x, current.z + step.z);
                return true;
            }

            // 贴墙滑：分轴各试一次，避免贴着墙整人卡死
            if (Mathf.Abs(step.x) > 0.00001f && CanOccupy(current + new Vector3(step.x, 0f, 0f)))
            {
                SetPosition(current.x + step.x, current.z);
                return true;
            }

            if (Mathf.Abs(step.z) > 0.00001f && CanOccupy(current + new Vector3(0f, 0f, step.z)))
            {
                SetPosition(current.x, current.z + step.z);
                return true;
            }

            return false;
        }

        private void SetPosition(float x, float z)
        {
            transform.position = new Vector3(x, _footY, z);
        }

        /// <summary>
        /// 能不能站在这个位置。用身体半径向四个方向采样，
        /// 避免「人一半嵌进墙里」——只看中心格会出现这种观感问题。
        /// </summary>
        private bool CanOccupy(Vector3 worldPos)
        {
            if (_world == null || _world.Map == null) return true;

            float r = bodyRadius;

            return PassableAt(worldPos.x + r, worldPos.z)
                && PassableAt(worldPos.x - r, worldPos.z)
                && PassableAt(worldPos.x, worldPos.z + r)
                && PassableAt(worldPos.x, worldPos.z - r);
        }

        private bool PassableAt(float x, float z)
        {
            return _world.Map.IsPassable(_world.WorldToGrid(new Vector3(x, 0f, z)));
        }

        // ---------- 跳跃 / 重力 ----------

        private void UpdateVertical()
        {
            if (_grounded && _verticalVelocity <= 0f) return;

            _verticalVelocity += gravity * Time.deltaTime;
            _footY += _verticalVelocity * Time.deltaTime;

            if (_footY <= 0f)
            {
                _footY = 0f;
                _verticalVelocity = 0f;
                _grounded = true;
            }

            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, _footY, p.z);
        }

        private void UpdateFacing(bool moved)
        {
            if (!moved || visual == null) return;

            Vector3 flat = new Vector3(_moveDirection.x, 0f, _moveDirection.z);
            if (flat.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(flat, Vector3.up);

            // 用 1 - exp(-k * dt) 做帧率无关的平滑，比固定 lerp 系数稳
            float t = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
            visual.rotation = Quaternion.Slerp(visual.rotation, target, t);
        }
    }
}

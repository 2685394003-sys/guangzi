using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 随从交互（follower interact）：玩家靠近一格时显示提示，右键打开随从面板。
    ///
    /// 距离用**格子曼哈顿距离**而不是世界坐标距离：
    /// 这个游戏的一切都按格子算，用格子距离才和「靠近一格」的直觉一致
    /// （斜对角算 2 格，不能隔着墙角开面板）。
    ///
    /// 右键在这里和「拖拽镜头」「取消手持」共用，靠 BuildController 的点击判定分流：
    /// 只有判定成「点击」才会走到这里，拖拽不会误触。
    /// </summary>
    public class FollowerInteract : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private FollowerController follower;
        [SerializeField] private FollowerPanelUI panel;

        [Tooltip("靠近时显示的 HUD 提示，留空则不显示")]
        [SerializeField] private GameObject hintRoot;

        [Header("范围")]
        [Tooltip("玩家和随从的格子距离在多少以内可以交互。1 = 上下左右相邻或同格")]
        [SerializeField] private int maxGridDistance = 1;

        [Header("提示标签")]
        [Tooltip("标签显示在随从头顶上方多高的地方（米）")]
        [SerializeField] private float hintHeightOffset = 1.55f;

        private GridWorld _world;
        private PlayerController _player;
        private Camera _rayCamera;

        public static FollowerInteract Instance { get; private set; }

        /// <summary>玩家当前是否在交互范围内（给提示和其他系统读）。</summary>
        public bool IsPlayerNear { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            IsPlayerNear = ComputeNear();
            UpdateHint();
        }

        private void UpdateHint()
        {
            if (hintRoot == null) return;

            bool visible = IsPlayerNear
                           && InputModeService.IsWorld
                           && panel != null
                           && !panel.IsOpen;

            if (!visible)
            {
                if (hintRoot.activeSelf) hintRoot.SetActive(false);
                return;
            }

            var cam = ResolveCamera();
            if (cam == null || follower == null)
            {
                if (hintRoot.activeSelf) hintRoot.SetActive(false);
                return;
            }

            // 投影到屏幕：标签跟着随从头顶走，而不是钉死在屏幕某个位置。
            // Canvas 是 Overlay，RectTransform.position 就是屏幕像素坐标。
            Vector3 world = follower.transform.position + Vector3.up * hintHeightOffset;
            Vector3 screen = cam.WorldToScreenPoint(world);

            // z <= 0 表示在相机背后，这时候不能显示，
            // 否则会把屏幕坐标镜像到反方向，出现一个"鬼影标签"
            if (screen.z <= 0f)
            {
                if (hintRoot.activeSelf) hintRoot.SetActive(false);
                return;
            }

            if (!hintRoot.activeSelf) hintRoot.SetActive(true);

            var rect = hintRoot.transform as RectTransform;
            if (rect != null) rect.position = screen;
        }

        private Camera ResolveCamera()
        {
            if (_rayCamera == null) _rayCamera = Camera.main;
            return _rayCamera;
        }

        /// <summary>
        /// 右键点一下时调用。在范围内就开面板（已开则关），返回是否消费了这次点击。
        /// 返回 true 时调用方不要再执行「清空手持」。
        /// </summary>
        public bool TryInteract()
        {
            if (panel == null) return false;
            if (!IsPlayerNear) return false;

            if (panel.IsOpen) panel.Close();
            else panel.Open();

            return true;
        }

        private bool ComputeNear()
        {
            if (follower == null || panel == null) return false;

            if (_world == null) _world = GridWorld.Instance;
            if (_world == null) return false;

            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null) return false;

            var playerCell = _world.WorldToGrid(_player.transform.position);
            var followerCell = _world.WorldToGrid(follower.transform.position);

            int distance = Mathf.Abs(playerCell.x - followerCell.x) + Mathf.Abs(playerCell.z - followerCell.z);
            return distance <= maxGridDistance;
        }
    }
}

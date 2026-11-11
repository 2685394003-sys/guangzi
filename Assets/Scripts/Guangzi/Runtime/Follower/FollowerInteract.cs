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
        private FollowerController _nearFollower;

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
            _nearFollower = FindNearest();
            IsPlayerNear = _nearFollower != null;
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
            if (cam == null || _nearFollower == null)
            {
                if (hintRoot.activeSelf) hintRoot.SetActive(false);
                return;
            }

            // 投影到屏幕：标签跟着随从头顶走，而不是钉死在屏幕某个位置。
            // Canvas 是 Overlay，RectTransform.position 就是屏幕像素坐标。
            Vector3 world = _nearFollower.transform.position + Vector3.up * hintHeightOffset;
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
        /// 右键点一下时调用。在范围内就打开**最近那只**随从的面板，返回是否消费了这次点击。
        ///
        /// 面板**已经开着**的时候只消费点击、不关面板——
        /// 因为右键在面板里另有用途（对物品弹操作菜单）。关面板走右上角的 X。
        /// </summary>
        public bool TryInteract()
        {
            if (panel == null) return false;
            if (_nearFollower == null) return false;

            if (panel.IsOpen) return true;

            panel.Open(_nearFollower);
            return true;
        }

        /// <summary>
        /// 找玩家身边最近的随从。
        ///
        /// 多随从之后不能再固定绑一只——玩家站在小二旁边却弹出小一的面板，
        /// 会以为「右键坏了」。这里按格子距离取最近，超出范围就返回 null。
        /// </summary>
        private FollowerController FindNearest()
        {
            if (panel == null) return null;

            if (_world == null) _world = GridWorld.Instance;
            if (_world == null) return null;

            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null) return null;

            var playerCell = _world.WorldToGrid(_player.transform.position);

            FollowerController best = null;
            int bestDistance = int.MaxValue;

            var all = FollowerController.All;

            // 正常情况走 All（运行时注册的）；编辑器里没注册就退回固定引用
            if (all.Count > 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var candidate = all[i];
                    if (candidate == null) continue;

                    var cell = _world.WorldToGrid(candidate.transform.position);
                    int distance = Mathf.Abs(playerCell.x - cell.x) + Mathf.Abs(playerCell.z - cell.z);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }
            }
            else if (follower != null)
            {
                var cell = _world.WorldToGrid(follower.transform.position);
                bestDistance = Mathf.Abs(playerCell.x - cell.x) + Mathf.Abs(playerCell.z - cell.z);
                best = follower;
            }

            return bestDistance <= maxGridDistance ? best : null;
        }
    }
}

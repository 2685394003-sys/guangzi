using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库交互（storage interact）：靠近仓库一格时显示提示，右键打开仓库菜单。
    ///
    /// 和 FollowerInteract 是一对：那边管随从，这边管仓库。
    /// 两者都会在 BuildController 的右键分流里被问一次，谁先命中谁消费。
    /// </summary>
    public class StorageInteract : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private StoragePoint storage;
        [SerializeField] private StorageMenu menu;

        [Tooltip("靠近时显示的 HUD 提示")]
        [SerializeField] private GameObject hintRoot;

        [Header("范围")]
        [Tooltip("玩家和仓库的格子距离在多少以内可以交互")]
        [SerializeField] private int maxGridDistance = 1;

        [Header("提示标签")]
        [SerializeField] private float hintHeightOffset = 1.6f;

        private GridWorld _world;
        private PlayerController _player;
        private Camera _rayCamera;

        public static StorageInteract Instance { get; private set; }

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
                           && menu != null
                           && !menu.IsOpen;

            if (!visible)
            {
                if (hintRoot.activeSelf) hintRoot.SetActive(false);
                return;
            }

            var cam = ResolveCamera();
            if (cam == null || storage == null)
            {
                if (hintRoot.activeSelf) hintRoot.SetActive(false);
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(storage.transform.position + Vector3.up * hintHeightOffset);
            if (screen.z <= 0f)
            {
                if (hintRoot.activeSelf) hintRoot.SetActive(false);
                return;
            }

            if (!hintRoot.activeSelf) hintRoot.SetActive(true);

            var rect = hintRoot.transform as RectTransform;
            if (rect != null) rect.position = screen;
        }

        /// <summary>右键点一下时调用，返回是否消费了这次点击。</summary>
        public bool TryInteract()
        {
            if (menu == null) return false;
            if (!IsPlayerNear) return false;

            if (menu.IsOpen) return true;

            menu.Show();
            return true;
        }

        private bool ComputeNear()
        {
            if (storage == null || menu == null) return false;

            if (_world == null) _world = GridWorld.Instance;
            if (_world == null) return false;

            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null) return false;

            var playerCell = _world.WorldToGrid(_player.transform.position);
            var storageCell = storage.Cell;

            int distance = Mathf.Abs(playerCell.x - storageCell.x) + Mathf.Abs(playerCell.z - storageCell.z);
            return distance <= maxGridDistance;
        }

        private Camera ResolveCamera()
        {
            if (_rayCamera == null) _rayCamera = Camera.main;
            return _rayCamera;
        }
    }
}

using System;
using System.Collections;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库物品的左键菜单（warehouse item menu）：获取 / 聚焦。
    ///
    /// 和随从面板那个右键菜单不同，这个是**左键**打开的，
    /// 因为仓库面板的格子左键原本没有用途（只做选中建造）。
    ///
    /// 聚焦的完整流程：
    ///   关掉仓库面板 → 镜头平滑飞到物品所在的容器 → 到达后打开那个容器的面板。
    /// 必须分段做，不能一步到位——玩家得看清东西在哪儿，不然镜头一闪就换了画面，很晕。
    /// </summary>
    public class WarehouseItemMenu : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform menuRect;
        [SerializeField] private GameObject blocker;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button takeButton;
        [SerializeField] private Button focusButton;
        [SerializeField] private Text focusLabel;

        [Header("面板")]
        [SerializeField] private WarehouseWindow warehouse;
        [SerializeField] private ChestWindow chestWindow;
        [SerializeField] private FollowerPanelUI followerPanel;
        [SerializeField] private GridCamera gridCamera;

        private string _itemId;
        private int _slotIndex;

        public static WarehouseItemMenu Instance { get; private set; }

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;

            if (root == null) root = gameObject;
            if (menuRect == null) menuRect = root.transform as RectTransform;

            if (selectButton != null) selectButton.onClick.AddListener(OnSelect);
            if (takeButton != null) takeButton.onClick.AddListener(OnTake);
            if (focusButton != null) focusButton.onClick.AddListener(OnFocus);

            if (blocker != null)
            {
                var blockBtn = blocker.GetComponent<Button>();
                if (blockBtn != null) blockBtn.onClick.AddListener(Hide);
            }

            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (selectButton != null) selectButton.onClick.RemoveListener(OnSelect);
            if (takeButton != null) takeButton.onClick.RemoveListener(OnTake);
            if (focusButton != null) focusButton.onClick.RemoveListener(OnFocus);
        }

        public void Show(string itemId, int slotIndex, Vector2 screenPosition)
        {
            if (string.IsNullOrEmpty(itemId) || root == null) return;

            _itemId = itemId;
            _slotIndex = slotIndex;

            // 只存在玩家身上时没地方可聚焦，按钮置灰并说明原因
            bool canFocus = TryFindSource(itemId, out _, out _);
            if (focusButton != null) focusButton.interactable = canFocus;
            if (focusLabel != null)
            {
                focusLabel.text = canFocus ? "聚焦" : "聚焦（在玩家身上）";
            }

            if (blocker != null) blocker.SetActive(true);

            // 提到 Canvas 最上层，否则会被仓库面板自己盖住
            transform.SetAsLastSibling();

            root.SetActive(true);

            PlaceAt(screenPosition);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
            _itemId = null;
        }

        private void PlaceAt(Vector2 screenPosition)
        {
            if (menuRect == null) return;

            float scale = 1f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) scale = canvas.transform.lossyScale.x;
            if (scale <= 0.0001f) scale = 1f;

            Vector2 size = menuRect.rect.size * scale;
            Vector2 target = screenPosition + new Vector2(12f, -12f);

            if (target.x + size.x > Screen.width - 8f) target.x = screenPosition.x - size.x - 12f;
            if (target.y + size.y > Screen.height - 8f) target.y = screenPosition.y + size.y + 12f;

            target.x = Mathf.Clamp(target.x, 8f, Mathf.Max(8f, Screen.width - size.x - 8f));
            target.y = Mathf.Clamp(target.y, 8f, Mathf.Max(8f, Screen.height - size.y - 8f));

            menuRect.position = target;
        }

        // ---------- 选中（保留原来的建造选物） ----------

        private void OnSelect()
        {
            if (warehouse != null) warehouse.SelectSlot(_slotIndex);
            Hide();
        }

        // ---------- 获取 ----------

        private void OnTake()
        {
            if (string.IsNullOrEmpty(_itemId)) { Hide(); return; }

            var database = ItemDatabase.Instance;
            var player = PlayerInventory.Instance;

            if (database == null || player == null || player.Inventory == null) { Hide(); return; }

            var def = database.Get(_itemId);
            if (def == null) { Hide(); return; }

            // 从「计入仓库」且有货的地方扣一份
            var source = FindInventoryWith(_itemId);
            if (source == null)
            {
                ToastUI.Show("仓库里已经没有 " + def.displayName);
                Hide();
                return;
            }

            int taken = source.Remove(_itemId, 1);
            if (taken <= 0) { Hide(); return; }

            int added = player.Inventory.Add(def, taken);
            if (added < taken)
            {
                // 背包满了，原路退回，不能凭空少一个
                source.Add(def, taken - added);
                ToastUI.Show("背包放不下 " + def.displayName);
            }

            Hide();
        }

        /// <summary>按箱子 → 随从 → 玩家背包的顺序找一份库存。</summary>
        private Inventory FindInventoryWith(string itemId)
        {
            for (int i = 0; i < Chest.All.Count; i++)
            {
                var chest = Chest.All[i];
                if (chest == null || !chest.IncludeInWarehouse) continue;
                if (chest.CountOf(itemId) > 0) return chest.Inventory;
            }

            var all = FollowerController.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f != null && f.Bag != null && f.Bag.CountOf(itemId) > 0) return f.Bag;
            }

            var player = PlayerInventory.Instance;
            if (player != null && player.Inventory != null && player.Inventory.CountOf(itemId) > 0)
            {
                return player.Inventory;
            }

            return null;
        }

        // ---------- 聚焦 ----------

        /// <summary>找出这件物品第一个有货的容器，返回它的世界位置和「打开它」的动作。</summary>
        private bool TryFindSource(string itemId, out Vector3 worldPosition, out Action openAction)
        {
            worldPosition = Vector3.zero;
            openAction = null;

            for (int i = 0; i < Chest.All.Count; i++)
            {
                var chest = Chest.All[i];
                if (chest == null || !chest.IncludeInWarehouse) continue;
                if (chest.CountOf(itemId) <= 0) continue;

                worldPosition = chest.transform.position;
                var target = chest;
                openAction = () => { if (chestWindow != null) chestWindow.Open(target); };
                return true;
            }

            var all = FollowerController.All;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == null || f.Bag == null || f.Bag.CountOf(itemId) <= 0) continue;

                worldPosition = f.transform.position;
                var target = f;
                openAction = () => { if (followerPanel != null) followerPanel.Open(target); };
                return true;
            }

            return false;
        }

        private void OnFocus()
        {
            if (string.IsNullOrEmpty(_itemId)) { Hide(); return; }
            if (gridCamera == null) gridCamera = FindFirstObjectByType<GridCamera>();
            if (gridCamera == null) { Hide(); return; }

            if (!TryFindSource(_itemId, out Vector3 worldPosition, out Action openAction))
            {
                Hide();
                return;
            }

            var world = GridWorld.Instance;
            if (world == null) { Hide(); return; }

            // 顺序很重要：先关自己的面板，玩家才看得到镜头移动
            Hide();
            if (warehouse != null) warehouse.Close();

            var cell = world.WorldToGrid(worldPosition);
            gridCamera.FocusOn(cell);

            if (openAction != null) StartCoroutine(OpenAfterArrive(openAction));
        }

        private IEnumerator OpenAfterArrive(Action openAction)
        {
            // 等镜头飞到位。加个上限，避免相机异常时永远不打开
            float deadline = Time.time + 3f;

            while (gridCamera != null && gridCamera.IsFocusing && Time.time < deadline)
            {
                yield return null;
            }

            openAction?.Invoke();
        }
    }
}

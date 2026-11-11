using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库窗口（warehouse window）。Windows 式弹窗：居中弹出、可拖、右上角关。
    ///
    /// 它在**上帝视角下就是快捷栏**：点格子选中物品，选中态和快捷栏一样高亮，
    /// 然后就能拿它去和地图交互（左键下单让随从去建）。所以这里刻意复用
    /// HotbarSlotView 和同一套 SetSelected 逻辑，两个界面的手感完全一致。
    ///
    /// 数据走 GetStacks() 单一出口，目前只拼玩家背包；以后加箱子、货架在这里继续拼，UI 不用改。
    /// </summary>
    public class WarehouseWindow : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private HotbarSlotView[] slots;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text emptyHint;
        [SerializeField] private Button closeButton;

        [Header("行为")]
        [SerializeField] private string title = "仓库";

        [Header("选中")]
        [Tooltip("当前选中的物品 id。按 id 记而不是槽位号——背包刷新后槽位会变")]
        [SerializeField] private string selectedItemId;

        private Inventory _boundInventory;

        public static WarehouseWindow Instance { get; private set; }

        public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

        public string SelectedItemId => selectedItemId;

        /// <summary>当前选中的物品堆。没选、或东西已经用光了，返回 null。</summary>
        public ItemStack SelectedStack
        {
            get
            {
                if (string.IsNullOrEmpty(selectedItemId)) return null;

                var inventory = GetInventory();
                if (inventory == null) return null;

                for (int i = 0; i < inventory.Capacity; i++)
                {
                    var stack = inventory.GetSlot(i);
                    if (stack == null || stack.IsEmpty) continue;
                    if (stack.Def.id == selectedItemId) return stack;
                }

                return null;
            }
        }

        private void Awake()
        {
            Instance = this;

            if (windowRoot == null) windowRoot = gameObject;
            if (windowRect == null) windowRect = windowRoot.transform as RectTransform;
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            // 开局默认关着。只在 Awake 关一次，不放在 Update 里——
            // 否则玩家点开的那一帧会被立刻关掉。
            if (windowRoot != null) windowRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            Unsubscribe();
        }

        private void Update()
        {
            // 背包可能比本组件晚就绪，这里补订阅
            EnsureBound();
        }

        private void EnsureBound()
        {
            var inventory = GetInventory();
            if (_boundInventory == inventory) return;

            Unsubscribe();

            _boundInventory = inventory;
            if (_boundInventory != null) _boundInventory.Changed += Refresh;
        }

        private void Unsubscribe()
        {
            if (_boundInventory != null) _boundInventory.Changed -= Refresh;
            _boundInventory = null;
        }

        private static Inventory GetInventory()
        {
            var player = PlayerInventory.Instance;
            return player != null ? player.Inventory : null;
        }

        public void Open()
        {
            if (windowRoot == null) return;

            windowRoot.SetActive(true);
            windowRoot.transform.SetAsLastSibling();

            Center();
            Refresh();
        }

        public void Close()
        {
            if (windowRoot == null) return;
            windowRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Center()
        {
            if (windowRect == null) return;
            windowRect.anchoredPosition = Vector2.zero;
        }

        /// <summary>点格子：选中它；再点同一个就取消选中。</summary>
        public void SelectSlot(int index)
        {
            // 背包可能刚就绪还没订阅上，这里补一次，否则第一次点击会静默失败
            EnsureBound();

            var inventory = GetInventory();
            if (inventory == null) return;

            var stack = inventory.GetSlot(index);
            if (stack == null || stack.IsEmpty)
            {
                ClearSelection();
                return;
            }

            selectedItemId = selectedItemId == stack.Def.id ? null : stack.Def.id;
            Refresh();
        }

        public void ClearSelection()
        {
            if (string.IsNullOrEmpty(selectedItemId)) return;

            selectedItemId = null;
            Refresh();
        }

        public void Refresh()
        {
            if (titleLabel != null) titleLabel.text = title;

            var stacks = GetStacks();
            int shown = 0;
            bool selectedStillExists = string.IsNullOrEmpty(selectedItemId);

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;

                    ItemStack stack = stacks != null && i < stacks.Length ? stacks[i] : null;
                    bool isSelected = stack != null && !stack.IsEmpty && stack.Def.id == selectedItemId;

                    slots[i].Refresh(stack);
                    slots[i].SetSelected(isSelected);

                    if (stack != null && !stack.IsEmpty)
                    {
                        shown++;
                        if (isSelected) selectedStillExists = true;
                    }
                }
            }

            // 选中的东西被用光了，选中态要跟着消失，
            // 否则会一直「拿着一个不存在的东西」，点地图还会拿它去下单
            if (!selectedStillExists) selectedItemId = null;

            if (emptyHint != null) emptyHint.gameObject.SetActive(shown == 0);
        }

        private ItemStack[] GetStacks()
        {
            var inventory = GetInventory();
            if (inventory == null) return null;

            var result = new ItemStack[inventory.Capacity];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = inventory.GetSlot(i);
            }

            return result;
        }
    }
}

using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 木箱窗口（chest window）：我的世界那种多格子的箱子。
    ///
    /// 工具条三件套：
    /// - 搜索：按显示名过滤
    /// - 排序：名称升序 / 降序（点按钮切换）
    /// - 分类：全部 / 方块 / 非方块（现在先这两类占位）
    ///
    /// 「计入仓库」开关：勾上后这个箱子的东西会汇总到仓库面板。
    /// 箱子被放下时默认就是勾上的——玩家把东西放进箱子，本来就期望它算进仓库。
    ///
    /// 注意：过滤和排序只影响**显示**，不改箱子里的真实顺序。
    /// 所以点击取物时要用「过滤后的列表」反查真实条目，不能用格子下标当槽位。
    /// </summary>
    public class ChestWindow : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text emptyHint;

        [Header("工具条")]
        [SerializeField] private InputField searchField;
        [SerializeField] private Button sortButton;
        [SerializeField] private Text sortLabel;
        [SerializeField] private Button categoryButton;
        [SerializeField] private Text categoryLabel;

        [Header("格子")]
        [SerializeField] private HotbarSlotView[] slots;

        [Header("计入仓库")]
        [SerializeField] private Toggle includeToggle;
        [SerializeField] private Text includeLabel;

        [Header("行为")]
        [SerializeField] private string title = "木箱";

        private Chest _chest;

        private string _query = string.Empty;
        private bool _descending;
        private int _category;   // 0 = 全部，1 = 方块，2 = 非方块

        private readonly List<ItemStack> _filtered = new List<ItemStack>();

        public static ChestWindow Instance { get; private set; }

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;

            if (root == null) root = gameObject;
            if (windowRect == null) windowRect = root.transform as RectTransform;

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (sortButton != null) sortButton.onClick.AddListener(ToggleSort);
            if (categoryButton != null) categoryButton.onClick.AddListener(CycleCategory);

            if (searchField != null) searchField.onValueChanged.AddListener(OnSearchChanged);
            if (includeToggle != null) includeToggle.onValueChanged.AddListener(OnIncludeChanged);

            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (sortButton != null) sortButton.onClick.RemoveListener(ToggleSort);
            if (categoryButton != null) categoryButton.onClick.RemoveListener(CycleCategory);
            if (searchField != null) searchField.onValueChanged.RemoveListener(OnSearchChanged);
            if (includeToggle != null) includeToggle.onValueChanged.RemoveListener(OnIncludeChanged);
        }

        private void Update()
        {
            if (!IsOpen) return;
            Refresh();
        }

        public void Open(Chest chest)
        {
            if (root == null) return;

            _chest = chest;

            root.SetActive(true);
            root.transform.SetAsLastSibling();
            if (windowRect != null) windowRect.anchoredPosition = Vector2.zero;

            if (titleLabel != null) titleLabel.text = title;

            // 勾选框反映这个箱子的当前状态；先解除监听再赋值，避免触发回调又写回去
            if (includeToggle != null)
            {
                includeToggle.onValueChanged.RemoveListener(OnIncludeChanged);
                includeToggle.isOn = chest != null && chest.IncludeInWarehouse;
                includeToggle.onValueChanged.AddListener(OnIncludeChanged);
            }

            Refresh();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        private void OnSearchChanged(string value)
        {
            _query = value ?? string.Empty;
        }

        private void ToggleSort()
        {
            _descending = !_descending;
        }

        private void CycleCategory()
        {
            _category = (_category + 1) % 3;
        }

        private void OnIncludeChanged(bool value)
        {
            if (_chest == null) return;
            _chest.IncludeInWarehouse = value;
        }

        private void Refresh()
        {
            if (_chest == null) return;

            if (sortLabel != null) sortLabel.text = _descending ? "降序" : "升序";
            if (categoryLabel != null) categoryLabel.text = CategoryName(_category);
            if (includeLabel != null) includeLabel.text = _chest.IncludeInWarehouse ? "计入仓库：开" : "计入仓库：关";

            Collect();
            Fill();
        }

        private static string CategoryName(int category)
        {
            switch (category)
            {
                case 1: return "方块";
                case 2: return "非方块";
                default: return "全部";
            }
        }

        private void Collect()
        {
            _filtered.Clear();

            var inventory = _chest.Inventory;
            if (inventory == null) return;

            string query = _query.Trim();

            for (int i = 0; i < inventory.Capacity; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack == null || stack.IsEmpty) continue;

                if (!MatchCategory(stack)) continue;
                if (query.Length > 0 && !stack.Def.displayName.Contains(query)) continue;

                _filtered.Add(stack);
            }

            _filtered.Sort(CompareStacks);
        }

        private bool MatchCategory(ItemStack stack)
        {
            if (_category == 0) return true;

            bool isBlock = stack.Def.category == ItemCategory.Buildable;
            return _category == 1 ? isBlock : !isBlock;
        }

        private int CompareStacks(ItemStack a, ItemStack b)
        {
            int result = string.CompareOrdinal(a.Def.displayName, b.Def.displayName);

            // 名字一样就按数量兜底，保证排序稳定（否则每帧顺序会跳）
            if (result == 0) result = a.Amount.CompareTo(b.Amount);

            return _descending ? -result : result;
        }

        private void Fill()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;

                ItemStack stack = i < _filtered.Count ? _filtered[i] : null;
                slots[i].Refresh(stack);
                slots[i].SetSelected(false);
            }

            if (emptyHint != null)
            {
                emptyHint.gameObject.SetActive(_filtered.Count == 0);
            }
        }
    }
}

using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库窗口（warehouse window）：库存总览 + 简单物品摆放。
    ///
    /// 数据是**汇总**的：玩家背包 + 所有勾了「计入仓库」的木箱。
    /// 同一件物品分散在几个箱子里会合并成一行，数量相加——所以显示数量可能超过单堆上限
    /// （面包 40 个但单堆上限 20），画格子时用 Refresh(def, amount) 绕开 ItemStack 的截断。
    ///
    /// 它在上帝视角下还是「快捷栏」：点格子选中物品，拿它去和地图交互下建造单。
    /// 所以格子不只是展示，还要能选。
    ///
    /// 工具条：搜索 / 升序降序 / 分类（全部、方块、非方块）。
    /// 过滤只影响显示顺序，选中态按物品 id 记，不受位置变化影响。
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

        [Header("工具条")]
        [SerializeField] private InputField searchField;
        [SerializeField] private Button sortButton;
        [SerializeField] private Text sortLabel;
        [SerializeField] private DropdownList categoryDropdown;
        [SerializeField] private Text categoryLabel;

        [Header("行为")]
        [SerializeField] private string title = "仓库";

        [Tooltip("当前选中的物品 id。按 id 记而不是槽位号——过滤/刷新后位置会变")]
        [SerializeField] private string selectedItemId;

        private Inventory _boundInventory;
        private FollowerController _follower;

        private string _query = string.Empty;
        private bool _descending;
        private int _category;   // 0 全部 / 1 方块 / 2 非方块

        // 汇总结果（每次 Refresh 重建）
        private readonly Dictionary<string, int> _totals = new Dictionary<string, int>();
        private readonly List<ItemDef> _defs = new List<ItemDef>();
        private readonly List<int> _amounts = new List<int>();

        public static WarehouseWindow Instance { get; private set; }

        public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

        public string SelectedItemId => selectedItemId;

        /// <summary>
        /// 当前选中的物品。汇总视图没有真实堆栈，这里合成一个只读的给下游取 Def 用。
        /// 数量会被单堆上限截断，但下游只关心「是什么东西」，不关心真实总量。
        /// </summary>
        public ItemStack SelectedStack
        {
            get
            {
                if (string.IsNullOrEmpty(selectedItemId)) return null;
                if (!_totals.TryGetValue(selectedItemId, out int amount) || amount <= 0) return null;

                var database = ItemDatabase.Instance;
                if (database == null) return null;

                var def = database.Get(selectedItemId);
                if (def == null) return null;

                return new ItemStack(def, Mathf.Min(amount, def.maxStack));
            }
        }

        private void Awake()
        {
            Instance = this;

            if (windowRoot == null) windowRoot = gameObject;
            if (windowRect == null) windowRect = windowRoot.transform as RectTransform;

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (sortButton != null) sortButton.onClick.AddListener(ToggleSort);
            if (categoryDropdown != null) categoryDropdown.OnValueChanged += OnCategoryChanged;
            if (searchField != null) searchField.onValueChanged.AddListener(OnSearchChanged);

            // 开局默认关着。只在 Awake 关一次，不放在 Update 里——
            // 否则玩家点开的那一帧会被立刻关掉。
            if (windowRoot != null) windowRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (sortButton != null) sortButton.onClick.RemoveListener(ToggleSort);
            if (categoryDropdown != null) categoryDropdown.OnValueChanged -= OnCategoryChanged;
            if (searchField != null) searchField.onValueChanged.RemoveListener(OnSearchChanged);

            Unsubscribe();
        }

        private void Update()
        {
            EnsureBound();

            // 箱子内容变化没有事件，靠开着的时候每帧刷新兜底
            if (IsOpen) Refresh();
        }

        private void EnsureBound()
        {
            var inventory = GetPlayerInventory();
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

        private static Inventory GetPlayerInventory()
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

        /// <summary>取某个格子（**过滤后**的下标）当前显示的物品 id。空格返回 null。</summary>
        public string GetItemIdAt(int index)
        {
            if (index < 0 || index >= _defs.Count) return null;
            return _defs[index].id;
        }

        /// <summary>点格子：选中它；再点同一个就取消。index 是**过滤后**的下标。</summary>
        public void SelectSlot(int index)
        {
            EnsureBound();

            if (index < 0 || index >= _defs.Count)
            {
                ClearSelection();
                return;
            }

            string id = _defs[index].id;
            selectedItemId = selectedItemId == id ? null : id;
            Refresh();
        }

        public void ClearSelection()
        {
            if (string.IsNullOrEmpty(selectedItemId)) return;

            selectedItemId = null;
            Refresh();
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

        private void OnCategoryChanged(int value)
        {
            _category = value;
        }

        public void Refresh()
        {
            if (titleLabel != null) titleLabel.text = title;
            if (sortLabel != null) sortLabel.text = _descending ? "降序" : "升序";

            BuildAggregate();
            Filter();
            FillSlots();
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

        /// <summary>
        /// 汇总三处来源：玩家背包 + 随从背包 + 所有计入仓库的木箱。
        /// 口径是「玩家的全部库存」——分散在谁身上不重要，打开仓库就是要看总共有多少。
        /// </summary>
        private void BuildAggregate()
        {
            _totals.Clear();

            CollectFrom(GetPlayerInventory());

            // 随从身上背着的也算库存。缓存一下，别每帧满场景找组件
            if (_follower == null) _follower = FindFirstObjectByType<FollowerController>();
            if (_follower != null) CollectFrom(_follower.Bag);

            for (int i = 0; i < Chest.All.Count; i++)
            {
                var chest = Chest.All[i];
                if (chest == null) continue;
                if (!chest.IncludeInWarehouse) continue;

                CollectFrom(chest.Inventory);
            }
        }

        private void CollectFrom(Inventory inventory)
        {
            if (inventory == null) return;

            for (int i = 0; i < inventory.Capacity; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack == null || stack.IsEmpty) continue;

                string id = stack.Def.id;
                if (_totals.TryGetValue(id, out int current)) _totals[id] = current + stack.Amount;
                else _totals[id] = stack.Amount;
            }
        }

        private void Filter()
        {
            _defs.Clear();
            _amounts.Clear();

            var database = ItemDatabase.Instance;
            if (database == null) return;

            string query = _query.Trim();

            foreach (var pair in _totals)
            {
                if (pair.Value <= 0) continue;

                var def = database.Get(pair.Key);
                if (def == null) continue;

                if (!MatchCategory(def)) continue;
                if (query.Length > 0 && !def.displayName.Contains(query)) continue;

                _defs.Add(def);
                _amounts.Add(pair.Value);
            }

            SortEntries();
        }

        private bool MatchCategory(ItemDef def)
        {
            if (_category == 0) return true;

            bool isBlock = def.category == ItemCategory.Buildable;
            return _category == 1 ? isBlock : !isBlock;
        }

        private void SortEntries()
        {
            // 简单插入排序：条目数很少（几十个），不值得引比较器分配
            for (int i = 1; i < _defs.Count; i++)
            {
                var def = _defs[i];
                int amount = _amounts[i];
                int j = i - 1;

                while (j >= 0 && Compare(def, _defs[j]) < 0)
                {
                    _defs[j + 1] = _defs[j];
                    _amounts[j + 1] = _amounts[j];
                    j--;
                }

                _defs[j + 1] = def;
                _amounts[j + 1] = amount;
            }
        }

        private int Compare(ItemDef a, ItemDef b)
        {
            int result = string.CompareOrdinal(a.displayName, b.displayName);
            if (result == 0) result = string.CompareOrdinal(a.id, b.id);

            return _descending ? -result : result;
        }

        private void FillSlots()
        {
            int shown = 0;
            bool selectedStillExists = string.IsNullOrEmpty(selectedItemId);

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;

                    if (i < _defs.Count)
                    {
                        var def = _defs[i];
                        int amount = _amounts[i];
                        bool isSelected = def.id == selectedItemId;

                        slots[i].Refresh(def, amount);
                        slots[i].SetSelected(isSelected);

                        shown++;
                        if (isSelected) selectedStillExists = true;
                    }
                    else
                    {
                        slots[i].Refresh(null, 0);
                        slots[i].SetSelected(false);
                    }
                }
            }

            // 选中的东西被用光了，选中态要跟着消失，
            // 否则会一直「拿着一个不存在的东西」，点地图还会拿它去下单
            if (!selectedStillExists) selectedItemId = null;

            if (emptyHint != null) emptyHint.gameObject.SetActive(shown == 0);
        }
    }
}

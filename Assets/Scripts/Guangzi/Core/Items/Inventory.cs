using System;
using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 背包（inventory）。**固定槽位数**，空格槽也占位——
    /// 因为 UI 要按索引画格子，用「可伸缩的 List」会导致格子数量随机变化。
    ///
    /// 堆叠规则：同种物品先填满已有未满的堆，再开新槽。
    /// 这是环世界/星露谷的共同做法，也让「堆叠上限」这件事对玩家可见。
    ///
    /// 同时实现 <see cref="IResourceStore"/>：建造校验器原本对接 ResourceLedger，
    /// 现在直接吃背包，**不需要中间适配层**——那个接口当初就是为这次替换留的。
    /// </summary>
    public sealed class Inventory : IResourceStore
    {
        private readonly ItemStack[] _slots;
        private readonly ItemCatalog _catalog;

        // 复用缓冲，避免每帧幽灵预览时反复 new 字典产生 GC
        private readonly Dictionary<string, int> _costBuffer = new Dictionary<string, int>();

        public int Capacity => _slots.Length;

        /// <summary>内容变化时触发。HUD 订阅它刷新，不用每帧轮询。</summary>
        public event Action Changed;

        public Inventory(int capacity, ItemCatalog catalog = null)
        {
            _slots = new ItemStack[capacity < 1 ? 1 : capacity];
            _catalog = catalog;
        }

        public ItemStack GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return null;
            return _slots[index];
        }

        public bool IsSlotEmpty(int index)
        {
            if (index < 0 || index >= _slots.Length) return true;
            return _slots[index] == null || _slots[index].IsEmpty;
        }

        /// <summary>
        /// 加物品，返回**实际装进去的数量**。
        /// 装不下的部分不会被静默吞掉——返回值告诉调用方还剩多少，由调用方决定提示还是丢弃。
        /// </summary>
        public int Add(ItemDef def, int count)
        {
            if (def == null || count <= 0) return 0;

            int remaining = count;

            // 1. 先填已有未满的同种堆
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                var stack = _slots[i];
                if (stack == null || stack.IsEmpty || stack.IsFull) continue;
                if (stack.Def.id != def.id) continue;

                remaining -= stack.Add(remaining);
            }

            // 2. 再开新槽
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i] != null && !_slots[i].IsEmpty) continue;

                var stack = new ItemStack(def, 0);
                remaining -= stack.Add(remaining);
                _slots[i] = stack;
            }

            int added = count - remaining;
            if (added > 0) RaiseChanged();
            return added;
        }

        /// <summary>移除物品，返回**实际移除的数量**。从后往前扣，保留前面完整的堆。</summary>
        public int Remove(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return 0;

            int remaining = count;

            for (int i = _slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                var stack = _slots[i];
                if (stack == null || stack.IsEmpty) continue;
                if (stack.Def.id != itemId) continue;

                remaining -= stack.Remove(remaining);

                if (stack.IsEmpty) _slots[i] = null;
            }

            int removed = count - remaining;
            if (removed > 0) RaiseChanged();
            return removed;
        }

        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;

            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var stack = _slots[i];
                if (stack == null || stack.IsEmpty) continue;
                if (stack.Def.id == itemId) total += stack.Amount;
            }
            return total;
        }

        public bool Has(string itemId, int count) => CountOf(itemId) >= count;

        public int FreeSlotCount
        {
            get
            {
                int free = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] == null || _slots[i].IsEmpty) free++;
                }
                return free;
            }
        }

        public void ClearSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return;
            if (_slots[index] == null) return;

            _slots[index] = null;
            RaiseChanged();
        }

        // ---------- IResourceStore ----------
        // 建造校验 / 拆除返还都走这里。id 就是物品 id，不再有「资源 id」这套平行命名。

        public int GetAmount(string resourceId) => CountOf(resourceId);

        public void Add(string resourceId, int amount)
        {
            if (_catalog == null) return;

            var def = _catalog.Get(resourceId);
            if (def == null) return;

            Add(def, amount);
        }

        public bool TryConsume(string resourceId, int amount)
        {
            return Remove(resourceId, amount) >= amount;
        }

        public bool CanAfford(IReadOnlyList<ResourceAmount> cost)
        {
            if (cost == null || cost.Count == 0) return true;

            // 同一种资源可能在一个配方里出现多次，先合计再比较，避免重复扣错
            _costBuffer.Clear();

            for (int i = 0; i < cost.Count; i++)
            {
                var item = cost[i];
                if (item.amount <= 0) continue;

                int current;
                _costBuffer.TryGetValue(item.resourceId, out current);
                _costBuffer[item.resourceId] = current + item.amount;
            }

            foreach (var pair in _costBuffer)
            {
                if (CountOf(pair.Key) < pair.Value) return false;
            }

            return true;
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}

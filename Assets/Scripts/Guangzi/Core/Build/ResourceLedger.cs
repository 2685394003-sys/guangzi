using System;
using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 资源台账（resource ledger）。第一周只做「够不够 / 扣多少 / 返多少」，
    /// 不含堆叠、存储区、搬运——那些属于 §2.2 的「物品 / 堆叠 / 存储区」系统，后面替换实现。
    /// </summary>
    public interface IResourceStore
    {
        int GetAmount(string resourceId);
        void Add(string resourceId, int amount);
        bool TryConsume(string resourceId, int amount);
        bool CanAfford(IReadOnlyList<ResourceAmount> cost);
    }

    /// <summary>最小可用实现：一个字典。等物品系统落地后换掉它，接口不变。</summary>
    public sealed class ResourceLedger : IResourceStore
    {
        private readonly Dictionary<string, int> _amounts = new Dictionary<string, int>();

        public ResourceLedger()
        {
        }

        public ResourceLedger(params ResourceAmount[] initial)
        {
            if (initial == null) return;
            for (int i = 0; i < initial.Length; i++)
            {
                Add(initial[i].resourceId, initial[i].amount);
            }
        }

        public int GetAmount(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return 0;
            return _amounts.TryGetValue(resourceId, out int amount) ? amount : 0;
        }

        public void Add(string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(resourceId) || amount == 0) return;
            _amounts[resourceId] = GetAmount(resourceId) + amount;
        }

        public bool TryConsume(string resourceId, int amount)
        {
            if (amount <= 0) return true;
            int have = GetAmount(resourceId);
            if (have < amount) return false;
            _amounts[resourceId] = have - amount;
            return true;
        }

        public bool CanAfford(IReadOnlyList<ResourceAmount> cost)
        {
            if (cost == null) return true;

            // 同一种资源可能在一个配方里出现多次，先合计再比较，避免重复扣错
            for (int i = 0; i < cost.Count; i++)
            {
                int total = 0;
                for (int j = 0; j < cost.Count; j++)
                {
                    if (cost[j].resourceId == cost[i].resourceId)
                    {
                        total += cost[j].amount;
                    }
                }

                if (GetAmount(cost[i].resourceId) < total) return false;
            }

            return true;
        }

        /// <summary>一次性扣掉整条配方。任意一项不足则整体失败，不会扣一半。</summary>
        public bool TryConsumeAll(IReadOnlyList<ResourceAmount> cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost == null) return true;

            for (int i = 0; i < cost.Count; i++)
            {
                TryConsume(cost[i].resourceId, cost[i].amount);
            }

            return true;
        }

        /// <summary>把返还清单加回台账。</summary>
        public void AddAll(IReadOnlyList<ResourceAmount> amounts)
        {
            if (amounts == null) return;
            for (int i = 0; i < amounts.Count; i++)
            {
                Add(amounts[i].resourceId, amounts[i].amount);
            }
        }

        /// <summary>导出快照给存档用（JsonUtility 不能直接序列化 Dictionary，必须转数组）。</summary>
        public ResourceAmount[] Snapshot()
        {
            var array = new ResourceAmount[_amounts.Count];
            int i = 0;
            foreach (var pair in _amounts)
            {
                array[i++] = new ResourceAmount(pair.Key, pair.Value);
            }
            return array;
        }

        public void Clear()
        {
            _amounts.Clear();
        }

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var pair in _amounts)
            {
                parts.Add(pair.Key + "=" + pair.Value);
            }
            return string.Join(", ", parts);
        }
    }
}

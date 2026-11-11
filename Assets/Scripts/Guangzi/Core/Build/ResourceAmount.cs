using System;

namespace Guangzi.Core
{
    /// <summary>
    /// 一条材料消耗：资源 id + 数量。资源 id 对应 14 种素材的配置键。
    /// 既是运行时结构，也是配方和返还清单的通用单位。
    /// </summary>
    [Serializable]
    public struct ResourceAmount
    {
        public string resourceId;
        public int amount;

        public ResourceAmount(string resourceId, int amount)
        {
            this.resourceId = resourceId;
            this.amount = amount;
        }

        public override string ToString() => resourceId + " x" + amount;
    }
}

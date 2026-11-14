namespace Guangzi.Core
{
    /// <summary>
    /// 物品定义（item definition）。纯数据，不含任何 Unity 类型。
    /// 对应配置表 items.json 里的一行。
    ///
    /// 刻意不塞「图标引用」——表现层用 colorHex 生成占位色块，
    /// 等美术给图集后再换成 sprite id。这样 Core 不会被资源系统绑死。
    /// </summary>
    public sealed class ItemDef
    {
        public readonly string id;
        public readonly string displayName;
        public readonly ItemCategory category;

        /// <summary>单个堆栈上限。飞书要求「物品堆叠上限」，这里是唯一来源。</summary>
        public readonly int maxStack;

        /// <summary>占位图标颜色，形如 "#9C7042"。没有正式图标时用它画色块。</summary>
        public readonly string colorHex;

        /// <summary>
        /// 图标资源名（Resources/Icons/Items 下的文件名，不带扩展）。
        /// 空表示还没出图，表现层退回 colorHex 色块。
        /// 存名字而不是 Sprite 引用，是为了让 Core 继续不依赖 Unity 资源系统。
        /// </summary>
        public readonly string icon;

        /// <summary>营养值。> 0 表示可食用，吃一个恢复这么多饥饿。</summary>
        public readonly int nutrition;

        public readonly int sellPrice;

        /// <summary>工具等级。0 = 不是工具；1 木 / 2 石 / 3 铁 / 4 钻石。方块按这个判断能不能挖。</summary>
        public readonly int toolTier;

        /// <summary>
        /// 开采速度**加成**。最终速度 = 基础速度(1) + 本值。
        /// 石镐填 5 → 实际 6，和我的世界「手 1 / 石镐 4」是同一套算法，只是数值不同。
        /// </summary>
        public readonly float miningBonus;

        public ItemDef(
            string id,
            string displayName,
            ItemCategory category,
            int maxStack,
            string colorHex,
            int nutrition = 0,
            int sellPrice = 0,
            int toolTier = 0,
            float miningBonus = 0f,
            string icon = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.category = category;
            this.maxStack = maxStack < 1 ? 1 : maxStack;
            this.colorHex = string.IsNullOrEmpty(colorHex) ? "#808080" : colorHex;
            this.nutrition = nutrition;
            this.sellPrice = sellPrice;
            this.toolTier = toolTier;
            this.miningBonus = miningBonus;
            this.icon = icon;
        }

        /// <summary>有没有正式图标。没有就得退回色块。</summary>
        public bool HasIcon => !string.IsNullOrEmpty(icon);

        public bool IsFood => nutrition > 0;

        public bool IsTool => toolTier > 0;

        public override string ToString() => id + "(" + displayName + ")";
    }
}

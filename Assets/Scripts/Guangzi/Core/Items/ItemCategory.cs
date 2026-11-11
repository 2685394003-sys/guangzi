namespace Guangzi.Core
{
    /// <summary>
    /// 物品分类。只影响 UI 分组和默认堆放策略，不影响逻辑。
    /// </summary>
    public enum ItemCategory
    {
        /// <summary>原材料：木材、石料、金属。</summary>
        Material = 0,

        /// <summary>食物：可吃，带营养值。</summary>
        Food = 1,

        /// <summary>建材：可以拿去盖东西。</summary>
        Buildable = 2,

        /// <summary>药品：血包、草药。</summary>
        Medicine = 3,

        /// <summary>杂项：暂时没归类的。</summary>
        Misc = 4,
    }
}

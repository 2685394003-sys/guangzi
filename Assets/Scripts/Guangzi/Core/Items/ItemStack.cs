using System;

namespace Guangzi.Core
{
    /// <summary>
    /// 一个物品堆栈（item stack）：同种物品 + 数量，数量不超过定义里的 maxStack。
    ///
    /// 用 class 而不是 struct：它需要被背包长期持有并原地增减，
    /// struct 放在数组里改会有「改副本」的坑，调试起来很费时。
    /// </summary>
    public sealed class ItemStack
    {
        public ItemDef Def { get; }

        public int Amount { get; private set; }

        public bool IsEmpty => Amount <= 0;

        /// <summary>
        /// 锁定标记（lock）。
        ///
        /// 锁住的堆不会被随从自动送走，会一直留在随从身上——
        /// 玩家用它来「钉住」某些东西：比如让随从背着食物随时吃，或者留一把工具不让他交出去。
        ///
        /// 放在物品堆上而不是槽位上：背包刷新、换格子、堆叠拆分之后，
        /// 「这件东西被锁了」这个状态应该跟着物品走，而不是跟着格子走。
        /// </summary>
        public bool Locked { get; set; }

        public bool IsFull => Amount >= Def.maxStack;

        public int Space => Def.maxStack - Amount;

        public ItemStack(ItemDef def, int amount = 0)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Amount = 0;
            if (amount > 0) Add(amount);
        }

        /// <summary>加物品，返回**实际加进去的数量**（可能因为堆满而少于请求值）。</summary>
        public int Add(int count)
        {
            if (count <= 0) return 0;

            int added = Math.Min(count, Space);
            Amount += added;
            return added;
        }

        /// <summary>减物品，返回**实际减掉的数量**（不会减成负数）。</summary>
        public int Remove(int count)
        {
            if (count <= 0) return 0;

            int removed = Math.Min(count, Amount);
            Amount -= removed;
            return removed;
        }

        public override string ToString() => Def.id + " x" + Amount + "/" + Def.maxStack;
    }
}

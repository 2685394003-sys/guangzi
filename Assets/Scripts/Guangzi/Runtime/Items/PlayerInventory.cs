using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 玩家背包持有者。挂在 Player 上，全局通过 Instance 访问。
    ///
    /// 初始配置写在这里而不是 JSON：这是「开局送什么」的关卡设计，
    /// 不是物品本身的属性，第一版先硬编码，等有开局配置表再挪。
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Header("背包")]
        [Tooltip("总槽位数。快捷栏只显示前 9 格，其余在背包面板里")]
        [SerializeField] private int capacity = 20;

        [Header("初始物品")]
        [SerializeField] private bool giveStarterItems = true;

        [Tooltip("石镐：开采速度 +5（基础 1 → 实际 6），可挖铁矿")]
        [SerializeField] private int starterPickaxeCount = 1;

        [Tooltip("石砖：用来测放置")]
        [SerializeField] private int starterStoneBrickCount = 30;

        [Tooltip("木块：开局送的 10 个")]
        [SerializeField] private int starterWoodBlockCount = 10;

        public Inventory Inventory { get; private set; }

        public static PlayerInventory Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            // ItemDatabase 有 DefaultExecutionOrder(-200)，这里能拿到它的 Catalog
            var database = ItemDatabase.Instance;
            Inventory = new Inventory(capacity, database != null ? database.Catalog : null);
        }

        private void Start()
        {
            if (giveStarterItems) GiveStarterItems();
        }

        private void GiveStarterItems()
        {
            var database = ItemDatabase.Instance;

            if (database == null)
            {
                Debug.LogError("[PlayerInventory] 场景里没有 ItemDatabase，无法发初始物品。");
                return;
            }

            AddIfFound(database, "stone_pickaxe", starterPickaxeCount);
            AddIfFound(database, "stone_brick", starterStoneBrickCount);
            AddIfFound(database, "wood_block", starterWoodBlockCount);
        }

        private void AddIfFound(ItemDatabase database, string itemId, int count)
        {
            if (count <= 0) return;

            var def = database.Get(itemId);
            if (def == null)
            {
                Debug.LogWarning("[PlayerInventory] 物品表里没有 " + itemId + "，跳过。");
                return;
            }

            int added = Inventory.Add(def, count);
            if (added < count)
            {
                Debug.LogWarning("[PlayerInventory] " + itemId + " 只装下 " + added + "/" + count + "，背包满了。");
            }
        }

        /// <summary>消耗物品，返回是否真的扣成功（用于吃东西、放方块）。</summary>
        public bool Consume(string itemId, int count)
        {
            if (Inventory == null) return false;
            return Inventory.Remove(itemId, count) >= count;
        }
    }
}

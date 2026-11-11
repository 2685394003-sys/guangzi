using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库（storage）：一个存放物资的点，玩家从这里「叫货」让随从来取。
    ///
    /// 现阶段还没有箱子建筑，先做成场景里一个固定点位（外观是一个方块）。
    /// 以后换成真正的箱子/货架时，只要它还提供 Inventory + 格子位置这两样东西，
    /// 随从的取货流程一行都不用改。
    /// </summary>
    public class StoragePoint : MonoBehaviour
    {
        [SerializeField] private int capacity = 20;

        [Header("初始物资")]
        [SerializeField] private string[] starterItemIds = { "bread", "stone_brick", "wood_block" };
        [SerializeField] private int[] starterAmounts = { 20, 20, 20 };

        [Header("外观")]
        [Tooltip("用来做占位视觉的方块，留空则不生成")]
        [SerializeField] private bool buildVisual = true;
        [SerializeField] private Color visualColor = new Color(0.46f, 0.33f, 0.20f, 1f);

        public static StoragePoint Instance { get; private set; }

        public Inventory Inventory { get; private set; }

        private void Awake()
        {
            Instance = this;
            EnsureStorage();
            BuildVisual();
        }

        private void EnsureStorage()
        {
            if (Inventory != null) return;

            var database = ItemDatabase.Instance;
            if (database == null) return;

            Inventory = new Inventory(capacity, database.Catalog);
            FillStarter();
        }

        private void FillStarter()
        {
            if (starterItemIds == null || starterAmounts == null) return;

            int count = Mathf.Min(starterItemIds.Length, starterAmounts.Length);

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(starterItemIds[i])) continue;

                var def = ItemDatabase.Instance.Get(starterItemIds[i]);
                if (def != null) Inventory.Add(def, starterAmounts[i]);
            }
        }

        private void BuildVisual()
        {
            if (!buildVisual) return;
            if (transform.Find("Visual") != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Visual";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            go.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                var mat = new Material(renderer.sharedMaterial);
                mat.color = visualColor;
                renderer.sharedMaterial = mat;
            }
        }

        public GridPos Cell
        {
            get
            {
                var world = GridWorld.Instance;
                if (world == null) return GridPos.Zero;
                return world.WorldToGrid(transform.position);
            }
        }

        /// <summary>仓库里现在有多少这个物品。</summary>
        public int CountOf(string itemId)
        {
            EnsureStorage();
            return Inventory != null ? Inventory.CountOf(itemId) : 0;
        }

        /// <summary>取出若干（给随从取货用），返回实际取到的数量。</summary>
        public int Take(string itemId, int amount)
        {
            EnsureStorage();
            if (Inventory == null || amount <= 0) return 0;

            int have = Inventory.CountOf(itemId);
            int take = Mathf.Min(amount, have);
            if (take <= 0) return 0;

            return Inventory.Remove(itemId, take);
        }
    }
}

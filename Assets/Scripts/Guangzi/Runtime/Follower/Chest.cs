using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 木箱（chest）：场景里的容器，玩家可以把东西存进去，也可以从这里叫随从取货。
    ///
    /// 「计入仓库」开关（includeInWarehouse）：勾上之后，这个箱子里的东西会**汇总**到
    /// 仓库面板里显示。箱子被放下时默认就是勾上的（玩家要的就是「存进去就等于进仓库」），
    /// 取消勾选就变成私房箱，只在打开它自己的时候看得到。
    ///
    /// 所有木箱都登记在 All 里，仓库面板靠它做汇总。
    /// </summary>
    public class Chest : MonoBehaviour
    {
        /// <summary>场景里所有木箱。仓库面板遍历它做汇总。</summary>
        public static readonly List<Chest> All = new List<Chest>();

        [SerializeField] private int capacity = 15;

        [Header("初始物资")]
        [SerializeField] private string[] starterItemIds = { "bread", "stone_brick", "wood_block" };
        [SerializeField] private int[] starterAmounts = { 20, 20, 20 };

        [Header("仓库登记")]
        [Tooltip("勾上后，箱子里的东西会统计到仓库面板里。放下箱子时默认打开")]
        [SerializeField] private bool includeInWarehouse = true;

        [Header("外观")]
        [SerializeField] private bool buildVisual = true;
        [SerializeField] private Color visualColor = new Color(0.46f, 0.33f, 0.20f, 1f);

        public Inventory Inventory { get; private set; }

        /// <summary>这个箱子的内容是否计入仓库。</summary>
        public bool IncludeInWarehouse
        {
            get => includeInWarehouse;
            set => includeInWarehouse = value;
        }

        private void Awake()
        {
            EnsureStorage();
            BuildVisual();
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
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

        /// <summary>找出落在某格上的木箱（随从取货按任务里的坐标找）。</summary>
        public static Chest FindAt(GridPos cell)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var chest = All[i];
                if (chest != null && chest.Cell.Equals(cell)) return chest;
            }

            return null;
        }

        public int CountOf(string itemId)
        {
            EnsureStorage();
            return Inventory != null ? Inventory.CountOf(itemId) : 0;
        }

        /// <summary>取出若干（随从取货用），返回实际取到的数量。</summary>
        public int Take(string itemId, int amount)
        {
            EnsureStorage();
            if (Inventory == null || amount <= 0) return 0;

            int have = Inventory.CountOf(itemId);
            int take = Mathf.Min(amount, have);
            if (take <= 0) return 0;

            return Inventory.Remove(itemId, take);
        }

        /// <summary>
        /// 汇总：这个箱子里某物品的数量（只有勾选「计入仓库」才有意义）。
        /// </summary>
        public int CountForWarehouse(string itemId)
        {
            return includeInWarehouse ? CountOf(itemId) : 0;
        }
    }
}


using System;
using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 网格宿主（grid host）：Core 规则层与 Unity 表现层之间**唯一的接缝**。
    /// 场景里只放一个。坐标换算、建造动作、存读档都从这里走。
    /// 它不做任何表现（不建 Mesh、不建 GameObject），只发事件；表现层订阅事件自己画。
    ///
    /// 为什么要有这一层：Core 不认识 Unity，表现层不该认识规则。
    /// 中间必须有人把两边接起来，而接缝越窄越好——就这一个类。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class GridWorld : MonoBehaviour
    {
        public static GridWorld Instance { get; private set; }

        [Header("网格尺寸（格）")]
        [SerializeField] private int width = 32;
        [SerializeField] private int height = 32;

        [Header("格尺寸（米 / 格）")]
        [SerializeField] private float cellSize = 1f;

        [Header("开局材料（调试用，等物品系统落地后删）")]
        [SerializeField] private int startWood = 500;
        [SerializeField] private int startStone = 500;

        public GridMap Map { get; private set; }
        public ResourceLedger Ledger { get; private set; }

        public float CellSize => cellSize;
        public int Width => Map != null ? Map.Width : width;
        public int Height => Map != null ? Map.Height : height;

        /// <summary>整张图被重建（读档）。表现层收到后应清空并重建全部视图。</summary>
        public event Action MapRebuilt;

        /// <summary>物块被放上去。</summary>
        public event Action<PlacedObject> ObjectPlaced;

        /// <summary>物块被拆掉。</summary>
        public event Action<PlacedObject> ObjectRemoved;

        /// <summary>
        /// 待办任务队列。上帝视角下的建造 / 挖掘都是往这里下单，
        /// 由随从（FollowerController）领取执行——玩家不再亲手干活。
        /// </summary>
        public JobQueue Jobs { get; private set; }

        private void Awake()
        {
            Instance = this;
            Jobs = new JobQueue();
            BuildEmptyMap();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void BuildEmptyMap()
        {
            Map = new GridMap(width, height);
            Ledger = new ResourceLedger(
                new ResourceAmount("wood", startWood),
                new ResourceAmount("stone", startStone));
        }

        // ---------- 坐标换算（世界坐标只是网格的投影，反向换算一律走这里） ----------

        /// <summary>格中心的世界坐标（y = 0）。</summary>
        public Vector3 GridToWorldCenter(GridPos p)
        {
            return new Vector3((p.x + 0.5f) * cellSize, 0f, (p.z + 0.5f) * cellSize);
        }

        /// <summary>格左下角的世界坐标（y = 0），多格物块的摆放基准。</summary>
        public Vector3 GridToWorldCorner(GridPos p)
        {
            return new Vector3(p.x * cellSize, 0f, p.z * cellSize);
        }

        /// <summary>世界坐标 → 格坐标。负数向下取整，保证边界不偏一格。</summary>
        public GridPos WorldToGrid(Vector3 world)
        {
            return new GridPos(
                Mathf.FloorToInt(world.x / cellSize),
                Mathf.FloorToInt(world.z / cellSize));
        }

        /// <summary>整张地图的世界中心，相机默认看向这里。</summary>
        public Vector3 MapWorldCenter()
        {
            return new Vector3(Width * cellSize * 0.5f, 0f, Height * cellSize * 0.5f);
        }

        // ---------- 建造动作（规则 + 材料在这里收口，表现层不要自己判断） ----------

        /// <summary>
        /// 建造用的资源来源。默认是旧的 ResourceLedger（测试和存档在用），
        /// 物品系统上线后由 BuildController 指向玩家背包，两套不会各扣各的。
        /// </summary>
        public IResourceStore ResourceStore { get; set; }

        private IResourceStore Store => ResourceStore != null ? ResourceStore : (IResourceStore)Ledger;

        /// <summary>尝试放置。失败会返回具体原因，调用方直接拿去显示。</summary>
        public BuildCheck TryPlace(BuildDef def, GridPos origin)
        {
            return TryPlace(def, origin, null);
        }

        /// <summary>
        /// 指定「谁付材料」。随从建造时传自己的背包——
        /// 材料必须由随从本人持有，不能直接从玩家仓库划走（那等于远程施工）。
        /// 传 null 就等于从玩家仓库扣，保持原来的行为。
        /// </summary>
        public BuildCheck TryPlace(BuildDef def, GridPos origin, IResourceStore payer)
        {
            if (Map == null) return BuildCheck.Fail(BuildInvalidReason.OutOfBounds, origin);

            var store = payer != null ? payer : Store;

            var check = BuildValidator.Check(Map, def, origin, store);
            if (!check.ok) return check;

            if (!ConsumeAll(store, def.cost))
            {
                return BuildCheck.Fail(BuildInvalidReason.NotEnoughResource, origin);
            }

            int id = Map.PlaceObject(def, origin);
            if (id == 0)
            {
                // 走到这里说明规则层和表现层判断不一致，把材料退回去，别让玩家白扣
                RefundAll(store, def.cost);
                return BuildCheck.Fail(BuildInvalidReason.Occupied, origin);
            }

            ObjectPlaced?.Invoke(Map.Objects[id]);
            return BuildCheck.Ok;
        }

        /// <summary>按格子拆除（点哪拆哪，多格物块拆整块）。产出的材料回到玩家仓库。</summary>
        public bool TryRemoveAt(GridPos pos)
        {
            return TryRemoveAt(pos, null);
        }

        /// <summary>
        /// 指定「产物归谁」。随从挖掘时传自己的背包——
        /// 挖到的东西先落在随从身上，由他负责搬运，不是凭空进玩家仓库。
        /// </summary>
        public bool TryRemoveAt(GridPos pos, IResourceStore receiver)
        {
            if (Map == null) return false;

            var obj = Map.GetObjectAt(pos);
            if (obj == null) return false;

            return TryRemoveById(obj.id, receiver);
        }

        public bool TryRemoveById(int id)
        {
            return TryRemoveById(id, null);
        }

        public bool TryRemoveById(int id, IResourceStore receiver)
        {
            if (Map == null) return false;

            if (!Map.RemoveObject(id, out var removed, out var refund)) return false;

            RefundAll(receiver != null ? receiver : Store, refund);
            ObjectRemoved?.Invoke(removed);
            return true;
        }

        /// <summary>按整条配方扣除。任意一项不足则整体失败，不会扣一半。</summary>
        private static bool ConsumeAll(IResourceStore store, IReadOnlyList<ResourceAmount> cost)
        {
            if (store == null) return true;
            if (cost == null || cost.Count == 0) return true;
            if (!store.CanAfford(cost)) return false;

            for (int i = 0; i < cost.Count; i++)
            {
                store.TryConsume(cost[i].resourceId, cost[i].amount);
            }
            return true;
        }

        private static void RefundAll(IResourceStore store, IReadOnlyList<ResourceAmount> amounts)
        {
            if (store == null || amounts == null) return;

            for (int i = 0; i < amounts.Count; i++)
            {
                store.Add(amounts[i].resourceId, amounts[i].amount);
            }
        }

        // ---------- 存 / 读档（直接产出 Core 的 DTO，序列化交给 Runtime 层） ----------

        public SaveData SaveToData()
        {
            var objects = new PlacedObjectSave[Map.Objects.Count];
            int i = 0;
            foreach (var pair in Map.Objects)
            {
                var obj = pair.Value;
                objects[i++] = new PlacedObjectSave
                {
                    id = obj.id,
                    defId = obj.def.id,
                    x = obj.origin.x,
                    z = obj.origin.z,
                    hp = obj.hp,
                };
            }

            // 存档 DTO 用 ResourceSave 而不是直接复用 ResourceAmount：
            // 运行时结构以后会改（加堆叠、品质、耐久），存档格式必须保持稳定。
            var snapshot = Ledger.Snapshot();
            var resources = new ResourceSave[snapshot.Length];
            for (int r = 0; r < snapshot.Length; r++)
            {
                resources[r] = new ResourceSave
                {
                    resourceId = snapshot[r].resourceId,
                    amount = snapshot[r].amount,
                };
            }

            return new SaveData
            {
                version = 1,
                width = Map.Width,
                height = Map.Height,
                objects = objects,
                resources = resources,
            };
        }

        public void LoadFromData(SaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[GridWorld] 读档数据为空，忽略。");
                return;
            }

            width = Mathf.Max(1, data.width);
            height = Mathf.Max(1, data.height);

            Map = new GridMap(width, height);
            Ledger = new ResourceLedger();

            if (data.resources != null)
            {
                for (int i = 0; i < data.resources.Length; i++)
                {
                    Ledger.Add(data.resources[i].resourceId, data.resources[i].amount);
                }
            }

            if (data.objects != null)
            {
                for (int i = 0; i < data.objects.Length; i++)
                {
                    var save = data.objects[i];
                    var def = BuildCatalog.Get(save.defId);
                    if (def == null)
                    {
                        Debug.LogWarning("[GridWorld] 存档里有未知物块 id：" + save.defId + "，跳过。");
                        continue;
                    }

                    if (!Map.LoadObject(save.id, def, new GridPos(save.x, save.z), save.hp))
                    {
                        Debug.LogWarning("[GridWorld] 物块 " + save.defId + " @(" + save.x + "," + save.z + ") 还原失败，跳过。");
                    }
                }
            }

            MapRebuilt?.Invoke();
        }

        /// <summary>清空整张图（调试用，不做材料返还）。</summary>
        public void ClearAll()
        {
            BuildEmptyMap();
            MapRebuilt?.Invoke();
        }
    }
}

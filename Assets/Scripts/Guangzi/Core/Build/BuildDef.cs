using System;
using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 物块定义（build definition）。纯数据，不含任何 Unity 类型。
    /// 对应策划表里的「物块」一行：size / hp / buildWork / cost / passable / deconstructRefund。
    /// 注意：不做材质系统，每种物块固定一种视觉，避免「材质 × 建筑」组合爆炸。
    /// </summary>
    public sealed class BuildDef
    {
        public readonly string id;
        public readonly string displayName;

        /// <summary>占地宽度（格）。1 表示单格。</summary>
        public readonly int width;

        /// <summary>占地深度（格）。</summary>
        public readonly int height;

        public readonly int hp;
        public readonly int buildWork;

        /// <summary>true = 可通行（地板、门、家具），false = 阻挡（墙）。寻路的唯一依据。</summary>
        public readonly bool passable;

        /// <summary>拆除返还比例，首版统一 50。</summary>
        public readonly int deconstructRefundPercent;

        public readonly IReadOnlyList<ResourceAmount> cost;

        /// <summary>渲染高度（米）。放 Core 是因为它属于物块定义本身，表现层只读取不判断。</summary>
        public readonly float visualHeight;

        public BuildDef(
            string id,
            string displayName,
            int width,
            int height,
            int hp,
            int buildWork,
            bool passable,
            int deconstructRefundPercent,
            float visualHeight,
            params ResourceAmount[] cost)
        {
            this.id = id;
            this.displayName = displayName;
            this.width = width;
            this.height = height;
            this.hp = hp;
            this.buildWork = buildWork;
            this.passable = passable;
            this.deconstructRefundPercent = deconstructRefundPercent;
            this.visualHeight = visualHeight;
            this.cost = cost ?? Array.Empty<ResourceAmount>();
        }

        /// <summary>以 origin 为左下角，枚举覆盖的所有格子。</summary>
        public IEnumerable<GridPos> Footprint(GridPos origin)
        {
            for (int dz = 0; dz < height; dz++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    yield return new GridPos(origin.x + dx, origin.z + dz);
                }
            }
        }

        /// <summary>按返还比例算出实际返还清单（向下取整，至少 0）。</summary>
        public List<ResourceAmount> Refund()
        {
            var list = new List<ResourceAmount>(cost.Count);
            for (int i = 0; i < cost.Count; i++)
            {
                int amount = cost[i].amount * deconstructRefundPercent / 100;
                if (amount > 0)
                {
                    list.Add(new ResourceAmount(cost[i].resourceId, amount));
                }
            }
            return list;
        }

        public override string ToString() => id + "(" + width + "x" + height + ")";
    }
}

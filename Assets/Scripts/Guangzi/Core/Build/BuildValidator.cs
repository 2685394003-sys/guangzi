using System;

namespace Guangzi.Core
{
    /// <summary>建造合法性失败的原因。表现层直接拿它决定红/白提示文案。</summary>
    public enum BuildInvalidReason
    {
        None = 0,
        UnknownDef = 1,
        OutOfBounds = 2,
        Occupied = 3,
        TerrainBlocked = 4,
        NotEnoughResource = 5,
    }

    /// <summary>一次建造检查的结果。用 struct 避免高频查询（每帧幽灵预览）产生 GC。</summary>
    public readonly struct BuildCheck
    {
        public readonly bool ok;
        public readonly BuildInvalidReason reason;

        /// <summary>导致失败的具体格子，用于高亮定位。</summary>
        public readonly GridPos blockingCell;

        public BuildCheck(bool ok, BuildInvalidReason reason, GridPos blockingCell)
        {
            this.ok = ok;
            this.reason = reason;
            this.blockingCell = blockingCell;
        }

        public static BuildCheck Ok => new BuildCheck(true, BuildInvalidReason.None, GridPos.Zero);

        public static BuildCheck Fail(BuildInvalidReason reason, GridPos cell)
        {
            return new BuildCheck(false, reason, cell);
        }

        public string Message
        {
            get
            {
                switch (reason)
                {
                    case BuildInvalidReason.None: return "可建造";
                    case BuildInvalidReason.UnknownDef: return "未知道具";
                    case BuildInvalidReason.OutOfBounds: return "超出地图边界";
                    case BuildInvalidReason.Occupied: return "该位置已被占用";
                    case BuildInvalidReason.TerrainBlocked: return "地形不可建造";
                    case BuildInvalidReason.NotEnoughResource: return "材料不足";
                    default: return "不可建造";
                }
            }
        }
    }

    /// <summary>
    /// 建造合法性检测。**纯规则，不含任何表现判断**。
    /// 表现层只调这里，不自己写 if，避免出现「预览说能放、实际放不下」。
    /// </summary>
    public static class BuildValidator
    {
        public static BuildCheck Check(GridMap map, BuildDef def, GridPos origin, IResourceStore store = null)
        {
            if (map == null) return BuildCheck.Fail(BuildInvalidReason.OutOfBounds, origin);
            if (def == null) return BuildCheck.Fail(BuildInvalidReason.UnknownDef, origin);

            foreach (var cellPos in def.Footprint(origin))
            {
                if (!map.InBounds(cellPos))
                {
                    return BuildCheck.Fail(BuildInvalidReason.OutOfBounds, cellPos);
                }

                var cell = map.GetCell(cellPos);

                if (cell.terrain == CellTerrain.Water)
                {
                    return BuildCheck.Fail(BuildInvalidReason.TerrainBlocked, cellPos);
                }

                if (cell.occupantId != 0)
                {
                    return BuildCheck.Fail(BuildInvalidReason.Occupied, cellPos);
                }
            }

            if (store != null && !store.CanAfford(def.cost))
            {
                return BuildCheck.Fail(BuildInvalidReason.NotEnoughResource, origin);
            }

            return BuildCheck.Ok;
        }
    }
}

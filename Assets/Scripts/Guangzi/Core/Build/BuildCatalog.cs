using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 首版物块目录。硬编码在代码里，等物块表（38 个）定稿后换成 ScriptableObject 数据驱动。
    /// 调用方只依赖 BuildDef 类型，换数据源时不用改任何调用点。
    /// </summary>
    public static class BuildCatalog
    {
        public const string WoodWall = "wall_wood";
        public const string StoneWall = "wall_stone";
        public const string StoneFloor = "floor_stone";

        /// <summary>石砖块：玩家的第一个可放置方块，cost 就是它自己（放一个扣一个，和我的世界一致）。</summary>
        public const string StoneBrick = "stone_brick";

        /// <summary>木块：开局送的 10 个，用来测放置和挖掘。</summary>
        public const string WoodBlock = "wood_block";

        private static readonly Dictionary<string, BuildDef> Defs = new Dictionary<string, BuildDef>
        {
            {
                WoodWall,
                new BuildDef(WoodWall, "木墙", 1, 1, 100, 30, false, 50, 1.0f,
                    new ResourceAmount("wood", 5))
            },
            {
                StoneBrick,
                new BuildDef(StoneBrick, "石砖块", 1, 1, 200, 60, false, 100, 1.0f,
                    new ResourceAmount(StoneBrick, 1))
            },
            {
                WoodBlock,
                new BuildDef(WoodBlock, "木块", 1, 1, 100, 30, false, 100, 1.0f,
                    new ResourceAmount(WoodBlock, 1))
            },
            {
                StoneWall,
                new BuildDef(StoneWall, "石墙", 1, 1, 200, 60, false, 50, 1.0f,
                    new ResourceAmount("stone", 5))
            },
            {
                StoneFloor,
                new BuildDef(StoneFloor, "石地板", 1, 1, 60, 15, true, 50, 0.1f,
                    new ResourceAmount("stone", 2))
            },
        };

        public static BuildDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Defs.TryGetValue(id, out var def);
            return def;
        }

        public static IEnumerable<BuildDef> All => Defs.Values;
    }
}

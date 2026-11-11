using System;

namespace Guangzi.Core
{
    /// <summary>地形类型。第一周只有 Soil，其余先占位。</summary>
    public enum CellTerrain
    {
        Soil = 0,
        Grass = 1,
        Stone = 2,
        Water = 3,
    }

    /// <summary>
    /// 单个格子的数据。刻意做成 struct + 平面数组，方便以后换 NativeArray。
    /// occupantId 为 0 表示空格；非 0 时是 PlacedObject 的 id。
    /// </summary>
    [Serializable]
    public struct Cell
    {
        public CellTerrain terrain;
        public int occupantId;

        public bool HasOccupant => occupantId != 0;

        public static Cell Empty => new Cell { terrain = CellTerrain.Soil, occupantId = 0 };
    }
}

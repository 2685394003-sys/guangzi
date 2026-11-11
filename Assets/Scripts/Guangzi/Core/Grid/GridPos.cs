using System;

namespace Guangzi.Core
{
    /// <summary>
    /// 网格坐标（grid position），等价于 int2 的零依赖版本。
    /// 约定：x 为横轴，z 为纵轴，与 Unity 世界坐标的 XZ 平面对应。
    /// 刻意不引用 UnityEngine，规则层可以在纯 .NET 环境下跑单元测试。
    /// </summary>
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        public int x;
        public int z;

        public GridPos(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public static readonly GridPos Zero = new GridPos(0, 0);

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.x + b.x, a.z + b.z);
        public static GridPos operator -(GridPos a, GridPos b) => new GridPos(a.x - b.x, a.z - b.z);
        public static bool operator ==(GridPos a, GridPos b) => a.x == b.x && a.z == b.z;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public bool Equals(GridPos other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        // 空间哈希常用的两个素数，保证 (x,z) 到 hash 的分布不至于大面积撞桶
        public override int GetHashCode() => unchecked((x * 73856093) ^ (z * 19349663));

        public override string ToString() => "(" + x + ", " + z + ")";
    }
}

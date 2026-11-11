using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 格子寻路（path finder）。四方向 BFS，不做权重、不做对角。
    ///
    /// 为什么用 BFS 而不是 A*：地图最大 32×32 = 1024 格，找一条路走不了几十个节点，
    /// A* 的启发式优化在这个规模上是纯开销。等地图大到几千格再换，接口不变。
    ///
    /// 用数组下标当 visited 而不是 Dictionary&lt;GridPos,...&gt;：
    /// 省掉 struct 的哈希/装箱开销，代码也更短。
    /// </summary>
    public static class PathFinder
    {
        private static readonly int[] OffsetX = { 0, 0, 1, -1 };
        private static readonly int[] OffsetZ = { 1, -1, 0, 0 };

        /// <summary>
        /// 找一条从 start 到 goal 的路径（含起点和终点）。
        /// 走不通返回 null。goal 本身不可通行时也返回 null——调用方该先去找「目标旁边」的格子。
        /// </summary>
        public static List<GridPos> FindPath(GridMap map, GridPos start, GridPos goal)
        {
            if (map == null) return null;
            if (!map.InBounds(start) || !map.InBounds(goal)) return null;
            if (!map.IsPassable(goal)) return null;

            if (start.Equals(goal)) return new List<GridPos> { start };

            int width = map.Width;
            int height = map.Height;
            int total = width * height;

            var cameFrom = new int[total];
            for (int i = 0; i < total; i++) cameFrom[i] = -1;

            var visited = new bool[total];
            var queue = new Queue<int>(64);

            int startIndex = start.x + start.z * width;
            int goalIndex = goal.x + goal.z * width;

            visited[startIndex] = true;
            queue.Enqueue(startIndex);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                if (current == goalIndex)
                {
                    return Reconstruct(map, cameFrom, startIndex, goalIndex);
                }

                int cx = current % width;
                int cz = current / width;

                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + OffsetX[d];
                    int nz = cz + OffsetZ[d];

                    if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;

                    int next = nx + nz * width;
                    if (visited[next]) continue;

                    var cell = new GridPos(nx, nz);
                    if (!map.IsPassable(cell)) continue;

                    visited[next] = true;
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            return null;
        }

        /// <summary>
        /// 找一个「能站到目标旁边」的位置。
        /// 随从不该站在目标格里干活——建造是把方块放进那格，挖掘是把那格挖空，
        /// 人都不能和那格重叠。返回第一个可通行的相邻格（按上下左右顺序）。
        /// </summary>
        public static bool TryFindWorkSpot(GridMap map, GridPos target, GridPos from, out GridPos spot)
        {
            spot = target;

            if (map == null) return false;

            // 优先挑离自己近的那一侧，少走两步
            int bestIndex = -1;
            int bestDistance = int.MaxValue;

            for (int d = 0; d < 4; d++)
            {
                var candidate = new GridPos(target.x + OffsetX[d], target.z + OffsetZ[d]);
                if (!map.InBounds(candidate) || !map.IsPassable(candidate)) continue;

                int distance = System.Math.Abs(candidate.x - from.x) + System.Math.Abs(candidate.z - from.z);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestIndex = d;
            }

            if (bestIndex < 0) return false;

            spot = new GridPos(target.x + OffsetX[bestIndex], target.z + OffsetZ[bestIndex]);
            return true;
        }

        private static List<GridPos> Reconstruct(GridMap map, int[] cameFrom, int startIndex, int goalIndex)
        {
            var path = new List<GridPos>(16);

            int current = goalIndex;
            int guard = 0;

            while (current != -1 && guard++ < 10000)
            {
                path.Add(new GridPos(current % map.Width, current / map.Width));
                if (current == startIndex) break;
                current = cameFrom[current];
            }

            path.Reverse();
            return path;
        }
    }
}

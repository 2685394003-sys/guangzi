using System;
using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 逻辑网格（grid map）。全项目坐标的唯一真相来源。
    /// 只管规则：占用、可通行、放置、移除。不知道 Unity 世界坐标的存在。
    /// </summary>
    public sealed class GridMap
    {
        public int Width { get; }
        public int Height { get; }

        private readonly Cell[] _cells;
        private readonly Dictionary<int, PlacedObject> _objects = new Dictionary<int, PlacedObject>();
        private int _nextId = 1;

        public GridMap(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "width 必须大于 0");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "height 必须大于 0");

            Width = width;
            Height = height;
            _cells = new Cell[width * height];
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = Cell.Empty;
            }
        }

        public int CellCount => _cells.Length;
        public IReadOnlyDictionary<int, PlacedObject> Objects => _objects;

        public int Index(GridPos p) => p.z * Width + p.x;

        public bool InBounds(GridPos p) => p.x >= 0 && p.x < Width && p.z >= 0 && p.z < Height;

        public Cell GetCell(GridPos p)
        {
            if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p + " 越界");
            return _cells[Index(p)];
        }

        public bool TryGetCell(GridPos p, out Cell cell)
        {
            if (!InBounds(p))
            {
                cell = Cell.Empty;
                return false;
            }
            cell = _cells[Index(p)];
            return true;
        }

        public bool IsEmpty(GridPos p)
        {
            return InBounds(p) && _cells[Index(p)].occupantId == 0;
        }

        /// <summary>寻路的唯一依据：地形可走 且 占用物块可走。</summary>
        public bool IsPassable(GridPos p)
        {
            if (!InBounds(p)) return false;

            var cell = _cells[Index(p)];
            if (cell.terrain == CellTerrain.Water) return false;
            if (cell.occupantId == 0) return true;

            return _objects.TryGetValue(cell.occupantId, out var obj) && obj.def.passable;
        }

        public bool SetTerrain(GridPos p, CellTerrain terrain)
        {
            if (!InBounds(p)) return false;
            _cells[Index(p)].terrain = terrain;
            return true;
        }

        /// <summary>放置物块。成功返回新 id，失败返回 0。</summary>
        public int PlaceObject(BuildDef def, GridPos origin)
        {
            if (def == null) return 0;

            var check = BuildValidator.Check(this, def, origin);
            if (!check.ok) return 0;

            int id = _nextId++;
            var obj = new PlacedObject
            {
                id = id,
                def = def,
                origin = origin,
                hp = def.hp,
            };

            _objects[id] = obj;

            foreach (var cellPos in def.Footprint(origin))
            {
                _cells[Index(cellPos)].occupantId = id;
            }

            return id;
        }

        /// <summary>
        /// 读档专用：按存档里的 id 原样还原，**不重新分配 id**，保证读档前后一致。
        /// 会推进内部计数器，之后新放的物块不会撞 id。
        /// </summary>
        public bool LoadObject(int id, BuildDef def, GridPos origin, int hp)
        {
            if (def == null || id <= 0) return false;
            if (_objects.ContainsKey(id)) return false;

            foreach (var cellPos in def.Footprint(origin))
            {
                if (!InBounds(cellPos)) return false;
                if (_cells[Index(cellPos)].occupantId != 0) return false;
            }

            var obj = new PlacedObject
            {
                id = id,
                def = def,
                origin = origin,
                hp = hp,
            };

            _objects[id] = obj;

            foreach (var cellPos in def.Footprint(origin))
            {
                _cells[Index(cellPos)].occupantId = id;
            }

            if (id >= _nextId)
            {
                _nextId = id + 1;
            }

            return true;
        }

        /// <summary>移除物块，并回填返还清单。失败返回 false。</summary>
        public bool RemoveObject(int id, out PlacedObject removed, out List<ResourceAmount> refund)
        {
            refund = null;

            if (!_objects.TryGetValue(id, out removed)) return false;

            foreach (var cellPos in removed.def.Footprint(removed.origin))
            {
                if (InBounds(cellPos))
                {
                    _cells[Index(cellPos)].occupantId = 0;
                }
            }

            _objects.Remove(id);
            refund = removed.def.Refund();
            return true;
        }

        /// <summary>对物块造成伤害，返回是否被打掉。第一周只给拆除用，不做战斗。</summary>
        public bool ApplyDamage(int id, int amount, out PlacedObject removed, out List<ResourceAmount> refund)
        {
            removed = null;
            refund = null;

            if (!_objects.TryGetValue(id, out var obj)) return false;

            obj.hp -= amount;
            if (!obj.IsDestroyed) return false;

            return RemoveObject(id, out removed, out refund);
        }

        public PlacedObject GetObjectAt(GridPos p)
        {
            if (!InBounds(p)) return null;
            int id = _cells[Index(p)].occupantId;
            if (id == 0) return null;
            _objects.TryGetValue(id, out var obj);
            return obj;
        }
    }
}

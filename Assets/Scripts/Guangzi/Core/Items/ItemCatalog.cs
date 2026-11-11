using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 物品总表（item catalog）。运行时的定义查询入口。
    ///
    /// 数据来源刻意不是 ScriptableObject——计划表里定过：
    /// 配置要出成 JSON 给天工引擎复用，Unity 这边只是个加载器。
    /// </summary>
    public sealed class ItemCatalog
    {
        private readonly Dictionary<string, ItemDef> _defs = new Dictionary<string, ItemDef>();

        public int Count => _defs.Count;

        public IEnumerable<ItemDef> All => _defs.Values;

        /// <summary>注册一个定义。重复 id 会被拒绝（配置错误要早暴露，别默默覆盖）。</summary>
        public bool Register(ItemDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return false;
            if (_defs.ContainsKey(def.id)) return false;

            _defs.Add(def.id, def);
            return true;
        }

        public ItemDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            ItemDef def;
            return _defs.TryGetValue(id, out def) ? def : null;
        }

        public bool TryGet(string id, out ItemDef def)
        {
            def = null;
            if (string.IsNullOrEmpty(id)) return false;
            return _defs.TryGetValue(id, out def);
        }
    }
}

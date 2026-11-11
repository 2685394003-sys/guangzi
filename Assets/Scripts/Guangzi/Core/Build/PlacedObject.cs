namespace Guangzi.Core
{
    /// <summary>
    /// 已经放在网格上的一个物块实例。只存运行时状态：身份 id、定义引用、原点、血量。
    /// 不存视觉信息——长什么样是表现层的事。
    /// </summary>
    public sealed class PlacedObject
    {
        public int id;
        public BuildDef def;
        public GridPos origin;
        public int hp;

        public bool IsDestroyed => hp <= 0;

        public override string ToString() => "#" + id + " " + def.id + " @" + origin;
    }
}

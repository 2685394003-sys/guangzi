using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 容器格子标记（container slot）：告诉拖拽系统「我这里能收东西，收进哪个容器」。
    ///
    /// 挂在木箱和随从背包的格子上。放在格子而不是面板上，
    /// 是因为拖拽落点判定是逐个 UI 元素命中的——必须能精确到「哪一格」。
    /// </summary>
    public class ContainerSlot : MonoBehaviour
    {
        public enum Kind
        {
            Chest = 0,
            Follower = 1,
        }

        [SerializeField] private Kind kind = Kind.Chest;
        [SerializeField] private Chest chest;
        [SerializeField] private FollowerController follower;

        public Kind SlotKind => kind;

        /// <summary>
        /// 切换绑定的随从。
        /// 面板可以在小一/小二之间切换，格子是同一批，必须跟着换——
        /// 否则打开小二的面板，拖进去的东西会跑进小一包里。
        /// </summary>
        public void SetFollower(FollowerController target)
        {
            if (kind != Kind.Follower) return;
            follower = target;
        }

        /// <summary>
        /// 尝试收下物品，返回**实际收下的数量**。
        /// 收不下（格子满了 / 不是同种）返回 0，调用方负责把剩下的留在地上。
        /// </summary>
        public int TryAccept(ItemDef def, int count)
        {
            if (def == null || count <= 0) return 0;

            Inventory inventory = null;

            if (kind == Kind.Chest)
            {
                if (chest == null) return 0;
                inventory = chest.Inventory;
            }
            else
            {
                if (follower == null) return 0;
                inventory = follower.Bag;
            }

            if (inventory == null) return 0;

            return inventory.Add(def, count);
        }
    }
}

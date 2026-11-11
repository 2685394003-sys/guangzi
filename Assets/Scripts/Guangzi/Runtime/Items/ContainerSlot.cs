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

            /// <summary>玩家快捷栏 = 玩家背包。拖到这里就是收到身上。</summary>
            Player = 2,
        }

        [SerializeField] private Kind kind = Kind.Chest;
        [SerializeField] private Chest chest;
        [SerializeField] private FollowerController follower;

        /// <summary>
        /// 这个格子**当前显示的是哪件物品**。
        ///
        /// 木箱和仓库会做搜索/排序/过滤，格子下标和容器真实槽位对不上，
        /// 所以拖拽不能按下标去取，必须按物品 id 反查。
        /// 由各个面板在填充格子时写入。
        /// </summary>
        [SerializeField] private string currentItemId;

        public Kind SlotKind => kind;

        public string CurrentItemId => currentItemId;

        public void SetCurrentItem(string itemId)
        {
            currentItemId = itemId;
        }

        /// <summary>
        /// 这个格子背后的容器。拖拽源靠它知道自己该从哪儿扣东西。
        /// </summary>
        public Inventory GetInventory()
        {
            switch (kind)
            {
                case Kind.Chest:
                    return chest != null ? chest.Inventory : null;

                case Kind.Follower:
                    return follower != null ? follower.Bag : null;

                case Kind.Player:
                    var player = PlayerInventory.Instance;
                    return player != null ? player.Inventory : null;

                default:
                    return null;
            }
        }

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

            var inventory = GetInventory();
            if (inventory == null) return 0;

            return inventory.Add(def, count);
        }
    }
}

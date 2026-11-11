using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 快捷栏里的一个格子。只负责「把数据画出来」，不管输入和选中逻辑——
    /// 那些归 HotbarUI，这样格子的显示逻辑可以单独复用（背包面板也是同样的格子）。
    /// </summary>
    public class HotbarSlotView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text countLabel;
        [SerializeField] private Image selectionFrame;

        /// <summary>把堆栈画到格子上。传 null 或空堆就清空。</summary>
        public void Refresh(ItemStack stack)
        {
            bool hasItem = stack != null && !stack.IsEmpty;
            Refresh(stack != null ? stack.Def : null, hasItem ? stack.Amount : 0);
        }

        /// <summary>
        /// 直接给定义 + 数量。
        ///
        /// 汇总视图（仓库）要用这个：同一件物品可能分散在多个箱子里，
        /// 合起来的总量会超过单堆上限（比如面包 40 个但上限 20），
        /// 用 ItemStack 装会被截断成 20，所以绕开它直接画。
        /// </summary>
        public void Refresh(ItemDef def, int amount)
        {
            bool hasItem = def != null && amount > 0;

            if (icon != null)
            {
                icon.enabled = hasItem;

                if (hasItem)
                {
                    Color color;
                    if (ColorUtility.TryParseHtmlString(def.colorHex, out color))
                    {
                        icon.color = color;
                    }
                }
            }

            if (countLabel != null)
            {
                // 数量为 1 时不显示数字，和星露谷/我的世界一致
                countLabel.text = hasItem && amount > 1 ? amount.ToString() : string.Empty;
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectionFrame != null) selectionFrame.enabled = selected;
        }
    }
}

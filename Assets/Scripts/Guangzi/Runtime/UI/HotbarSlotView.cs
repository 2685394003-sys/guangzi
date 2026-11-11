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

            if (icon != null)
            {
                icon.enabled = hasItem;

                if (hasItem)
                {
                    Color color;
                    if (ColorUtility.TryParseHtmlString(stack.Def.colorHex, out color))
                    {
                        icon.color = color;
                    }
                }
            }

            if (countLabel != null)
            {
                // 数量为 1 时不显示数字，和星露谷/我的世界一致
                countLabel.text = hasItem && stack.Amount > 1 ? stack.Amount.ToString() : string.Empty;
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectionFrame != null) selectionFrame.enabled = selected;
        }
    }
}

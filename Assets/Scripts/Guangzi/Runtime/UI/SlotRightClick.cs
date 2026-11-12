using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 格子的右键点击检测。
    ///
    /// Unity 的 Button 只认左键，右键完全不理。所以这里单独实现 IPointerClickHandler，
    /// 只把右键转发出去——左键还是走原来的 Button，两套互不干扰。
    /// </summary>
    public class SlotRightClick : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private int index;

        /// <summary>
        /// 右键回调：格子下标 + **点击那一刻的屏幕坐标**。
        ///
        /// 坐标必须从事件里拿，不能用 Mouse.current 去读当前位置——
        /// 两者不一定同帧，读出来的可能是上一个位置，菜单就会弹到别处。
        /// </summary>
        public event Action<int, Vector2> RightClicked;

        public int Index => index;

        public void SetIndex(int value)
        {
            index = value;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;

            RightClicked?.Invoke(index, eventData.position);
        }
    }
}

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

        public event Action<int> RightClicked;

        public int Index => index;

        public void SetIndex(int value)
        {
            index = value;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;

            RightClicked?.Invoke(index);
        }
    }
}

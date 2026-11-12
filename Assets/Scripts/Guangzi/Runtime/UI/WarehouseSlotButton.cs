using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库格子的点击处理器。
    ///
    /// 左键不再直接选中，而是弹出物品菜单（选中 / 获取 / 聚焦）——
    /// 仓库里的东西可能散在箱子或随从身上，玩家需要一个入口跳过去看。
    ///
    /// 位置必须从 PointerEventData 里拿，不能用 Button.onClick：
    /// Button 的回调不带坐标，只能事后去读 Mouse.current，
    /// 那一读可能已经是下一帧的位置，菜单就会弹到别处去。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class WarehouseSlotButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private WarehouseWindow warehouse;
        [SerializeField] private WarehouseItemMenu menu;
        [SerializeField] private int index;

        public void OnPointerClick(PointerEventData eventData)
        {
            // 只处理左键。右键留给别的用途（目前没有，先挡住）
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (warehouse == null) warehouse = FindFirstObjectByType<WarehouseWindow>();
            if (warehouse == null) return;

            string itemId = warehouse.GetItemIdAt(index);
            if (string.IsNullOrEmpty(itemId)) return;

            if (menu == null) menu = FindFirstObjectByType<WarehouseItemMenu>();

            if (menu != null)
            {
                menu.Show(itemId, index, eventData.position);
                return;
            }

            // 没配菜单就退回老行为（直接选中）
            warehouse.SelectSlot(index);
        }

        public void Initialize(WarehouseWindow owner, int slotIndex)
        {
            warehouse = owner;
            index = slotIndex;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库格子的点击处理器。
    ///
    /// 左键 = 选中（建造选物），和快捷栏保持一致；
    /// 右键 = 弹物品菜单（选中 / 获取 / 聚焦）。
    ///
    /// 全项目统一这套：左键干活，右键开菜单。
    /// 位置只能从 PointerEventData 里拿——Button.onClick 不带坐标，
    /// 事后读鼠标可能已经是下一帧的位置，菜单会弹到别处。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class WarehouseSlotButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private WarehouseWindow warehouse;
        [SerializeField] private WarehouseItemMenu menu;
        [SerializeField] private int index;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null) _button.onClick.AddListener(OnLeftClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnLeftClicked);
        }

        private void OnLeftClicked()
        {
            if (warehouse == null) warehouse = FindFirstObjectByType<WarehouseWindow>();
            if (warehouse == null) return;

            warehouse.SelectSlot(index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 只接右键。左键走 Button.onClick，两套互不干扰
            if (eventData.button != PointerEventData.InputButton.Right) return;

            if (warehouse == null) warehouse = FindFirstObjectByType<WarehouseWindow>();
            if (warehouse == null) return;

            string itemId = warehouse.GetItemIdAt(index);
            if (string.IsNullOrEmpty(itemId)) return;

            if (menu == null) menu = FindFirstObjectByType<WarehouseItemMenu>();

            // 没配菜单就没得弹，右键当作无操作
            if (menu == null) return;

            menu.Show(itemId, index, eventData.position);
        }

        public void Initialize(WarehouseWindow owner, int slotIndex)
        {
            warehouse = owner;
            index = slotIndex;
        }
    }
}

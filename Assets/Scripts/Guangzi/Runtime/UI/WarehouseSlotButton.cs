using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库格子的点击处理器。和 HotbarSlotButton 是一对——
    /// 快捷栏和仓库在这套设计里是同一个东西的两个位置：
    /// 角色视角用快捷栏选，上帝视角用仓库选，选中逻辑和手感必须一致。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class WarehouseSlotButton : MonoBehaviour
    {
        [SerializeField] private WarehouseWindow warehouse;
        [SerializeField] private int index;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            if (warehouse == null) warehouse = FindFirstObjectByType<WarehouseWindow>();
            if (warehouse != null) warehouse.SelectSlot(index);
        }

        public void Initialize(WarehouseWindow owner, int slotIndex)
        {
            warehouse = owner;
            index = slotIndex;
        }
    }
}

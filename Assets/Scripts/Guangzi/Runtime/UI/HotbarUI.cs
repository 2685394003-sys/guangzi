using Guangzi.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 快捷栏 HUD。9 格，显示背包前 9 格，数字键 1~9 切换选中。
    ///
    /// **只在角色视角显示**（上帝视角是规划模式，不需要手上的东西）。
    /// 注意：要显隐的那个对象必须是本组件的**子物体**，
    /// 不能是挂本组件的对象自己——否则一 SetActive(false)，Update 停掉就再也显示不回来了。
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("实际要显隐的容器，必须是子物体")]
        [SerializeField] private GameObject root;

        [Tooltip("留空会自动收集子物体上的 HotbarSlotView")]
        [SerializeField] private HotbarSlotView[] slots;

        [Tooltip("显示当前选中物品名，可以留空")]
        [SerializeField] private Text selectedNameLabel;

        [SerializeField] private GridCamera targetCamera;

        [Header("行为")]
        [Tooltip("只在角色视角显示")]
        [SerializeField] private bool onlyInCharacterView = true;

        private PlayerInventory _player;
        private int _selected;

        public int SelectedIndex => _selected;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();

            if (slots == null || slots.Length == 0)
            {
                slots = GetComponentsInChildren<HotbarSlotView>(true);
            }
        }

        private void Update()
        {
            EnsureBound();
            UpdateVisibility();
            HandleSelectionInput();
        }

        /// <summary>背包可能比本组件晚初始化，这里做懒绑定。</summary>
        private void EnsureBound()
        {
            if (_player == PlayerInventory.Instance) return;

            if (_player != null) _player.Inventory.Changed -= RefreshAll;

            _player = PlayerInventory.Instance;

            if (_player != null) _player.Inventory.Changed += RefreshAll;

            RefreshAll();
        }

        private void UpdateVisibility()
        {
            if (root == null) return;

            bool visible = InputModeService.IsWorld;

            if (visible && onlyInCharacterView)
            {
                visible = targetCamera != null && targetCamera.IsCharacterView;
            }

            if (root.activeSelf != visible) root.SetActive(visible);
        }

        private void HandleSelectionInput()
        {
            if (!InputModeService.IsWorld) return;
            if (slots == null || slots.Length == 0) return;

            // 上帝视角下快捷栏是「不在场」的：数字键不该改选中格，
            // 否则玩家在规划模式按 1~9 会悄悄改掉角色视角用的那套配置。
            if (onlyInCharacterView && targetCamera != null && !targetCamera.IsCharacterView) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 只用数字键。滚轮已经归相机缩放，不能再抢。
            int count = Mathf.Min(slots.Length, 9);
            for (int i = 0; i < count; i++)
            {
                var key = keyboard[(Key)((int)Key.Digit1 + i)];
                if (key != null && key.wasPressedThisFrame)
                {
                    Select(i);
                    return;
                }
            }
        }

        public void Select(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length) return;
            if (_selected == index) return;

            _selected = index;
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (slots == null) return;

            var inventory = _player != null ? _player.Inventory : null;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                slot.Refresh(inventory != null ? inventory.GetSlot(i) : null);
                slot.SetSelected(i == _selected);
            }

            UpdateNameLabel(inventory);
        }

        private void UpdateNameLabel(Inventory inventory)
        {
            if (selectedNameLabel == null) return;

            var stack = inventory != null ? inventory.GetSlot(_selected) : null;
            selectedNameLabel.text = stack != null && !stack.IsEmpty ? stack.Def.displayName : string.Empty;
        }
    }
}

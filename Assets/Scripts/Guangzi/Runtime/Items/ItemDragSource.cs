using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 物品拖拽源（item drag source）：把格子里的东西拖出去。
    ///
    /// 两种来源都走这一个组件：
    /// - 挂在**玩家快捷栏**格子：从玩家背包拖，按槽位取
    /// - 挂在**容器格子**（木箱 / 随从背包）：从那个容器拖，按物品 id 反查
    ///   （容器面板有搜索排序过滤，格子下标和真实槽位对不上，不能按位置取）
    ///
    /// 三种落点：
    /// 1. 容器格子 → 放进去（装不下掉地上）
    /// 2. 玩家快捷栏 → 收到身上
    /// 3. 地图 → 丢弃，生成带物理的掉落物
    /// </summary>
    public class ItemDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("引用")]
        [SerializeField] private int slotIndex;
        [SerializeField] private HotbarUI hotbar;

        [Tooltip("留空 = 从玩家背包拖。挂在容器格子上时，拖拽源就是那个容器")]
        [SerializeField] private ContainerSlot sourceContainer;

        [Header("拖拽视觉")]
        [Tooltip("跟随鼠标的拖拽图标，默认关；留空则运行时创建")]
        [SerializeField] private Image dragGhost;

        [Tooltip("图标大小（像素）")]
        [SerializeField] private float ghostSize = 56f;

        [Header("丢弃")]
        [Tooltip("扔到地上的散布半径（格）。半格左右，模仿手扔出去的感觉")]
        [SerializeField] private float scatterRadius = 0.5f;

        private Inventory _bag;
        private string _draggedId;
        private int _draggedAmount;
        private RectTransform _ghostRect;

        private void Awake()
        {
            if (hotbar == null) hotbar = GetComponentInParent<HotbarUI>();
            if (sourceContainer == null) sourceContainer = GetComponent<ContainerSlot>();

            EnsureGhost();
            if (_ghostRect != null) _ghostRect.gameObject.SetActive(false);
        }

        private void EnsureGhost()
        {
            if (dragGhost == null)
            {
                var go = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform.root, false);
                dragGhost = go.GetComponent<Image>();
                dragGhost.raycastTarget = false;
            }

            _ghostRect = dragGhost.transform as RectTransform;
            if (_ghostRect != null)
            {
                _ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
                _ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
                _ghostRect.pivot = new Vector2(0.5f, 0.5f);
                _ghostRect.sizeDelta = new Vector2(ghostSize, ghostSize);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            _bag = ResolveSourceInventory();
            if (_bag == null) return;

            var stack = ResolveDraggedStack(_bag);
            if (stack == null || stack.IsEmpty)
            {
                _bag = null;
                return;
            }

            _draggedId = stack.Def.id;
            _draggedAmount = stack.Amount;

            EnsureGhost();
            if (_ghostRect == null) return;

            _ghostRect.gameObject.SetActive(true);
            _ghostRect.SetAsLastSibling();

            Color color;
            if (ColorUtility.TryParseHtmlString(stack.Def.colorHex, out color)) dragGhost.color = color;

            MoveGhost(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_bag == null || string.IsNullOrEmpty(_draggedId)) return;
            MoveGhost(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghostRect != null) _ghostRect.gameObject.SetActive(false);

            if (_bag == null || string.IsNullOrEmpty(_draggedId))
            {
                ClearDrag();
                return;
            }

            var database = ItemDatabase.Instance;
            var def = database != null ? database.Get(_draggedId) : null;

            string id = _draggedId;
            int amount = _draggedAmount;

            if (def == null)
            {
                ClearDrag();
                return;
            }

            // 1. 落点是某个容器格子（含玩家快捷栏）
            var target = FindContainerSlot(eventData.position);
            if (target != null)
            {
                var targetInventory = target.GetInventory();

                // 拖回自己所在的容器：什么都不做
                if (targetInventory != null && targetInventory == _bag)
                {
                    ClearDrag();
                    return;
                }

                int accepted = target.TryAccept(def, amount);
                if (accepted > 0) _bag.Remove(id, accepted);

                int leftover = amount - accepted;
                if (leftover > 0)
                {
                    // 随从背包只有一组容量，塞不下别的东西。
                    // 这种情况**不能丢在地上**——玩家面板挡着看不到，会以为东西凭空没了。
                    // 改成退回玩家身上，并明确告诉他为什么放不进去。
                    if (target.SlotKind == ContainerSlot.Kind.Follower)
                    {
                        ToastUI.Show("随从只能携带一组同类型物品，放不下 " + def.displayName);
                        ReturnLeftoverToPlayer(def, leftover, eventData.position);
                    }
                    else
                    {
                        DropOnGround(def, leftover, eventData.position);
                    }
                }

                ClearDrag();
                return;
            }

            // 2. 鼠标落在别的 UI 上 → 不做
            if (IsPointerOverUI(eventData.position))
            {
                ClearDrag();
                return;
            }

            // 3. 落在空地上 → 整堆丢掉
            DropOnGround(def, amount, eventData.position);
            ClearDrag();
        }

        private void ClearDrag()
        {
            _bag = null;
            _draggedId = null;
            _draggedAmount = 0;
        }

        private Inventory ResolveSourceInventory()
        {
            if (sourceContainer != null)
            {
                var inventory = sourceContainer.GetInventory();
                if (inventory != null) return inventory;
            }

            var player = PlayerInventory.Instance;
            return player != null ? player.Inventory : null;
        }

        /// <summary>
        /// 取出要拖的那一堆。
        /// 容器格子按「当前显示的物品 id」反查——面板过滤后下标不可信；
        /// 快捷栏按槽位取即可（它不过滤）。
        /// </summary>
        private ItemStack ResolveDraggedStack(Inventory inventory)
        {
            string wantedId = sourceContainer != null ? sourceContainer.CurrentItemId : null;

            if (!string.IsNullOrEmpty(wantedId))
            {
                for (int i = 0; i < inventory.Capacity; i++)
                {
                    var slot = inventory.GetSlot(i);
                    if (slot == null || slot.IsEmpty) continue;
                    if (slot.Def.id == wantedId) return slot;
                }

                return null;
            }

            var byIndex = inventory.GetSlot(slotIndex);
            return byIndex != null && !byIndex.IsEmpty ? byIndex : null;
        }

        /// <summary>
        /// 把装不下的部分退回玩家身上。
        /// 来源本身是玩家背包时，先扣再退等于原样不动，玩家不会丢东西。
        /// 玩家背包也满了才落到地上兜底。
        /// </summary>
        private void ReturnLeftoverToPlayer(ItemDef def, int amount, Vector2 screenPosition)
        {
            if (def == null || amount <= 0) return;

            int taken = _bag.Remove(def.id, amount);
            if (taken <= 0) return;

            var player = PlayerInventory.Instance;
            if (player == null || player.Inventory == null)
            {
                var dropSystem = DropSystem.Instance;
                if (dropSystem != null) dropSystem.DropAt(def, taken, ResolveGroundPosition(screenPosition));
                return;
            }

            int added = player.Inventory.Add(def, taken);
            if (added < taken) DropOnGround(def, taken - added, screenPosition);
        }

        private void MoveGhost(Vector2 screenPosition)
        {
            if (_ghostRect == null) return;

            // Overlay Canvas 下 position 就是屏幕像素
            _ghostRect.position = screenPosition;
        }

        private ContainerSlot FindContainerSlot(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return null;

            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);

            for (int i = 0; i < results.Count; i++)
            {
                var slot = results[i].gameObject.GetComponentInParent<ContainerSlot>();
                if (slot != null) return slot;
            }

            return null;
        }

        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);

            return results.Count > 0;
        }

        /// <summary>
        /// 丢到鼠标所指的地面位置，并加一点随机散布。
        /// 全都精确落在同一个点会叠成一堆，看不出「扔了一把」。
        /// </summary>
        private void DropOnGround(ItemDef def, int amount, Vector2 screenPosition)
        {
            if (def == null || amount <= 0) return;

            var dropSystem = DropSystem.Instance;
            if (dropSystem == null) return;

            Vector3 position = ResolveGroundPosition(screenPosition);

            var offset = Random.insideUnitCircle * scatterRadius;
            position += new Vector3(offset.x, 0f, offset.y);

            // 先扣背包，扣掉了才生成掉落物——反过来的话生成失败就会凭空多出东西
            int consumed = _bag.Remove(def.id, amount);
            if (consumed <= 0) return;

            dropSystem.DropAt(def, consumed, position);
        }

        private Vector3 ResolveGroundPosition(Vector2 screenPosition)
        {
            var camera = Camera.main;

            if (camera != null)
            {
                var ray = camera.ScreenPointToRay(screenPosition);
                var plane = new Plane(Vector3.up, Vector3.zero);

                if (plane.Raycast(ray, out float enter)) return ray.GetPoint(enter);
            }

            var player = FindFirstObjectByType<PlayerController>();
            return player != null ? player.transform.position : Vector3.zero;
        }
    }
}

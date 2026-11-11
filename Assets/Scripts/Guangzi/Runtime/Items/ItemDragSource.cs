using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 快捷栏拖拽（item drag source）：把格子里的东西拖出去。
    ///
    /// 三种落点：
    /// 1. 容器格子（木箱 / 随从背包）→ 放进去
    /// 2. 地图上 → 丢弃，生成掉落物
    /// 3. 别处 → 什么都不做（原地不动）
    ///
    /// 拖出去的量是**整堆**。要做「拖一半」得加 Shift 分堆，那是另一套交互，先不做。
    /// </summary>
    public class ItemDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("引用")]
        [SerializeField] private int slotIndex;
        [SerializeField] private HotbarUI hotbar;

        [Header("拖拽视觉")]
        [Tooltip("跟随鼠标的拖拽图标，默认关；留空则运行时创建")]
        [SerializeField] private Image dragGhost;

        [Tooltip("图标大小（像素）")]
        [SerializeField] private float ghostSize = 56f;

        private ItemStack _dragged;
        private Inventory _bag;
        private RectTransform _ghostRect;
        private RectTransform _canvasRect;

        private void Awake()
        {
            if (hotbar == null) hotbar = GetComponentInParent<HotbarUI>();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) _canvasRect = canvas.transform as RectTransform;

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
            if (hotbar == null) return;

            var playerBag = PlayerInventory.Instance;
            if (playerBag == null || playerBag.Inventory == null) return;

            _bag = playerBag.Inventory;

            // 取当前格的物品（按 id 反查，避免槽位在拖拽途中被刷新改动）
            _dragged = _bag.GetSlot(slotIndex);
            if (_dragged == null || _dragged.IsEmpty)
            {
                _dragged = null;
                return;
            }

            EnsureGhost();
            if (_ghostRect == null) return;

            _ghostRect.gameObject.SetActive(true);
            _ghostRect.SetAsLastSibling();

            Color color;
            if (ColorUtility.TryParseHtmlString(_dragged.Def.colorHex, out color)) dragGhost.color = color;

            MoveGhost(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragged == null) return;
            MoveGhost(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghostRect != null) _ghostRect.gameObject.SetActive(false);

            if (_dragged == null || _bag == null)
            {
                _dragged = null;
                return;
            }

            var def = _dragged.Def;
            int amount = _dragged.Amount;
            _dragged = null;

            // 1. 先看落点是不是容器格子
            var slot = FindContainerSlot(eventData.position);
            if (slot != null)
            {
                int accepted = slot.TryAccept(def, amount);
                if (accepted > 0) _bag.TryConsume(def.id, accepted);

                // 容器装不下的部分丢在脚下，不能凭空消失
                if (accepted < amount) DropOnGround(def, amount - accepted, eventData.position);
                return;
            }

            // 2. 鼠标在 UI 上但不是容器 → 什么都不做
            if (IsPointerOverUI(eventData.position)) return;

            // 3. 地图上 → 全部丢弃
            DropOnGround(def, amount, eventData.position);
        }

        private void MoveGhost(Vector2 screenPosition)
        {
            if (_ghostRect == null) return;

            // Overlay Canvas 下 position 就是屏幕像素
            _ghostRect.position = screenPosition;
        }

        /// <summary>在鼠标位置找有没有容器格子。</summary>
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

        /// <summary>丢到鼠标所指的地面位置。</summary>
        private void DropOnGround(ItemDef def, int amount, Vector2 screenPosition)
        {
            if (def == null || amount <= 0) return;

            var dropSystem = DropSystem.Instance;
            if (dropSystem == null) return;

            var camera = Camera.main;
            Vector3 position;

            if (camera != null)
            {
                var ray = camera.ScreenPointToRay(screenPosition);
                var plane = new Plane(Vector3.up, Vector3.zero);

                if (plane.Raycast(ray, out float enter))
                {
                    position = ray.GetPoint(enter);
                }
                else
                {
                    var player = FindFirstObjectByType<PlayerController>();
                    position = player != null ? player.transform.position : Vector3.zero;
                }
            }
            else
            {
                var player = FindFirstObjectByType<PlayerController>();
                position = player != null ? player.transform.position : Vector3.zero;
            }

            // 先扣背包，扣掉了才生成掉落物——反过来的话生成失败就会凭空多出东西
            int consumed = _bag.Remove(def.id, amount);
            if (consumed <= 0) return;

            dropSystem.DropAt(def, consumed, position);
        }
    }
}

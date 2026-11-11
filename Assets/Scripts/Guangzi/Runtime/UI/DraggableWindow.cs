using UnityEngine;
using UnityEngine.EventSystems;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 可拖动窗口（draggable window）。挂在标题栏上，按住就能拖整个窗口。
    ///
    /// 拖拽量要除以 Canvas 的 scaleFactor：PointerEventData.delta 是**屏幕像素**，
    /// 而 anchoredPosition 是**UI 单位**。高分屏或 CanvasScaler 缩放过的时候
    /// 不除会明显「拖不动」或「拖飞」。
    /// </summary>
    public class DraggableWindow : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Tooltip("要拖动的窗口根节点。留空则拖自己的父级")]
        [SerializeField] private RectTransform target;

        [Tooltip("点一下就把窗口提到最上层，避免被别的窗口盖住")]
        [SerializeField] private bool bringToFrontOnClick = true;

        private Canvas _canvas;

        private void Awake()
        {
            if (target == null && transform.parent != null)
            {
                target = transform.parent as RectTransform;
            }

            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (bringToFrontOnClick && target != null)
            {
                target.SetAsLastSibling();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;

            float scale = _canvas != null && _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
            target.anchoredPosition += eventData.delta / scale;
        }
    }
}

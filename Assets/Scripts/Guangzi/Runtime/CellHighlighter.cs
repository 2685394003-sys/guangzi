using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 鼠标指向格的高亮（cell highlight）。
    /// 自己创建高亮片，不依赖场景预制体(prefab)——第一周少一个资产就少一处出错的地方。
    /// 用极扁的 Cube 而不是 Quad：Quad 有朝向问题（正面朝 +Z，要绕 X 转 -90° 才朝上），
    /// Cube 压扁没有朝向坑，视觉上一样。
    /// </summary>
    public class CellHighlighter : MonoBehaviour
    {
        [Header("颜色")]
        [SerializeField] private Color validColor = new Color(0.45f, 1f, 0.55f, 1f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.32f, 0.28f, 1f);
        [SerializeField] private Color removeColor = new Color(1f, 0.72f, 0.2f, 1f);

        [Header("尺寸（相对格）")]
        [Tooltip("1 = 铺满整格，0.9 = 四周留缝，能看出格子边界")]
        [SerializeField, Range(0.5f, 1f)] private float fill = 0.9f;

        [SerializeField] private float yOffset = 0.03f;
        [SerializeField] private float thickness = 0.02f;

        private GameObject _visual;
        private Material _material;

        public Color ValidColor => validColor;
        public Color InvalidColor => invalidColor;
        public Color RemoveColor => removeColor;

        private void Awake()
        {
            CreateVisual();
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_visual != null) Destroy(_visual);
        }

        private void CreateVisual()
        {
            _visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _visual.name = "CellHighlight";
            _visual.transform.SetParent(transform, false);

            // 必须去掉碰撞体：高亮片跟着鼠标走，万一以后改成物理射线，会自己挡住自己
            var collider = _visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            _material = new Material(shader) { name = "CellHighlightMat" };
            _visual.GetComponent<MeshRenderer>().sharedMaterial = _material;

            _visual.SetActive(false);
        }

        public void Show(GridPos pos, Color color)
        {
            var world = GridWorld.Instance;
            if (world == null) return;

            float cell = world.CellSize;
            float size = cell * fill;

            _visual.transform.position = world.GridToWorldCenter(pos) + Vector3.up * yOffset;
            _visual.transform.localScale = new Vector3(size, thickness, size);

            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", color);
            _material.color = color;

            if (!_visual.activeSelf) _visual.SetActive(true);
        }

        public void ShowValid(GridPos pos) => Show(pos, validColor);

        public void ShowInvalid(GridPos pos) => Show(pos, invalidColor);

        public void ShowRemove(GridPos pos) => Show(pos, removeColor);

        public void Hide()
        {
            if (_visual != null && _visual.activeSelf) _visual.SetActive(false);
        }
    }
}

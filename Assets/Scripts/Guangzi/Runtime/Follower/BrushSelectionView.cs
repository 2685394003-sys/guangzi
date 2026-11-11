using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 框选范围可视化（brush selection view）：用一个半透明的大方盒盖住要批量建造的区域。
    ///
    /// 为什么用一个大盒子而不是逐格高亮：
    /// 框选可能一次几十上百格，逐格建对象会瞬间生成一堆 GameObject，
    /// 拖鼠标时每帧重建更是灾难。一个盒子改 scale 是最省的表达方式。
    /// </summary>
    public class BrushSelectionView : MonoBehaviour
    {
        [Header("外观")]
        [Range(0.05f, 0.6f)]
        [SerializeField] private float alpha = 0.28f;

        [Tooltip("比格子略矮一点，避免和已有方块顶部打架")]
        [SerializeField] private float heightScale = 0.9f;

        private GameObject _box;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Material _material;
        private bool _visible;

        public bool IsVisible => _visible;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        /// <summary>用两个对角格圈出矩形区域。传进来前请保证 world 非空。</summary>
        public void Show(GridWorld world, GridPos a, GridPos b, Color color)
        {
            if (world == null) return;

            if (_box == null)
            {
                _box = CreateBox();
                if (_box == null) return;
            }

            if (!_box.activeSelf) _box.SetActive(true);
            _visible = true;

            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minZ = Mathf.Min(a.z, b.z);
            int maxZ = Mathf.Max(a.z, b.z);

            float cell = world.CellSize;

            float width = (maxX - minX + 1) * cell;
            float depth = (maxZ - minZ + 1) * cell;
            float height = cell * heightScale;

            // 矩形中心的格子坐标取两端中点的格心
            Vector3 cornerA = world.GridToWorldCenter(new GridPos(minX, minZ));
            Vector3 cornerB = world.GridToWorldCenter(new GridPos(maxX, maxZ));

            float centerX = (cornerA.x + cornerB.x) * 0.5f;
            float centerZ = (cornerA.z + cornerB.z) * 0.5f;

            _box.transform.position = new Vector3(centerX, height * 0.5f, centerZ);
            _box.transform.localScale = new Vector3(width, height, depth);

            color.a = alpha;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_block);
        }

        public void Hide()
        {
            _visible = false;
            if (_box != null && _box.activeSelf) _box.SetActive(false);
        }

        private GameObject CreateBox()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BrushSelection";

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _renderer = go.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = GetTransparentMaterial();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            go.transform.SetParent(transform, true);
            return go;
        }

        private Material GetTransparentMaterial()
        {
            if (_material != null) return _material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.name = "BrushSelection (runtime)";

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            _material = mat;
            return mat;
        }
    }
}

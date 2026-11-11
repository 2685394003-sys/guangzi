using Guangzi.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 鼠标尖端的手持预览（cursor item preview）。
    ///
    /// 上帝视角下仓库选中了物品时，鼠标指向哪一格，就在那一格显示半透明的方块，
    /// 告诉玩家「左键会把它放在这里」。
    ///
    /// 和幽灵方块（GhostBlockManager）的区别：
    /// 幽灵是**已经下过单的**，固定在某格等随从来做；
    /// 这里的是**还没下单**，跟着鼠标跑，一松手或者换物品就没了。
    ///
    /// 只在上帝视角出现——角色视角下手上已经有一个 3D 方块（HandItemView），
    /// 再叠一个鼠标预览会显得很乱。
    /// </summary>
    public class CursorItemPreview : MonoBehaviour
    {
        [Header("引用（留空自动查找）")]
        [SerializeField] private GridCamera targetCamera;
        [SerializeField] private WarehouseWindow warehouse;
        [SerializeField] private GridWorld world;
        [SerializeField] private Camera rayCamera;

        [Header("外观")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float alpha = 0.45f;

        [Tooltip("比真方块略小一点，避免和已建好的方块糊在一起")]
        [SerializeField] private float sizeScale = 0.94f;

        [Header("射线")]
        [SerializeField] private float groundHeight = 0f;
        [SerializeField] private float maxRayDistance = 300f;

        private GameObject _preview;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Material _material;
        private string _lastItemId;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();
            if (warehouse == null) warehouse = FindFirstObjectByType<WarehouseWindow>();
            if (world == null) world = GridWorld.Instance;
            if (rayCamera == null) rayCamera = Camera.main;

            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!ShouldShow())
            {
                Hide();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                Hide();
                return;
            }

            if (!TryGetCursorCell(mouse.position.ReadValue(), out GridPos cell))
            {
                Hide();
                return;
            }

            Show(cell, warehouse.SelectedStack);
        }

        private bool ShouldShow()
        {
            if (!InputModeService.IsWorld) return false;
            if (targetCamera == null || !targetCamera.IsGodView) return false;
            if (warehouse == null) return false;

            var stack = warehouse.SelectedStack;
            return stack != null && !stack.IsEmpty;
        }

        private bool TryGetCursorCell(Vector2 screenPosition, out GridPos cell)
        {
            cell = GridPos.Zero;

            var cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam == null || world == null || world.Map == null) return false;

            var ray = cam.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

            if (!plane.Raycast(ray, out float enter) || enter > maxRayDistance) return false;

            Vector3 hit = ray.GetPoint(enter);
            cell = world.WorldToGrid(hit);

            return world.Map.InBounds(cell);
        }

        private void Show(GridPos cell, ItemStack stack)
        {
            if (_preview == null)
            {
                _preview = CreatePreview();
                if (_preview == null) return;
            }

            if (!_preview.activeSelf) _preview.SetActive(true);

            Vector3 center = world.GridToWorldCenter(cell);
            float half = world.CellSize * 0.5f * sizeScale;
            _preview.transform.position = new Vector3(center.x, half, center.z);
            _preview.transform.localScale = Vector3.one * (world.CellSize * sizeScale);

            if (_lastItemId == stack.Def.id) return;
            _lastItemId = stack.Def.id;

            Color color;
            if (!ColorUtility.TryParseHtmlString(stack.Def.colorHex, out color)) return;

            color.a = alpha;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_block);
        }

        private void Hide()
        {
            if (_preview != null && _preview.activeSelf) _preview.SetActive(false);
            _lastItemId = null;
        }

        private GameObject CreatePreview()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CursorPreview";

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _renderer = go.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = GetTransparentMaterial();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            go.transform.SetParent(transform, true);
            return go;
        }

        /// <summary>和 GhostBlockManager 同一套透明材质设置：只改 alpha 会得到「半透明但写深度」。</summary>
        private Material GetTransparentMaterial()
        {
            if (_material != null) return _material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.name = "CursorPreview (runtime)";

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

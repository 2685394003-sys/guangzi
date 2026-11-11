using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 幽灵方块（ghost block）：上帝视角下玩家下的建造单，先在格子上放一个半透明方块当提示。
    ///
    /// 它**不是实体**——不占格、不阻挡、不参与寻路。只负责告诉玩家「这里我排了个活」。
    /// 随从真的走过去建好之后，这个对象就销毁，换成真正的方块。
    ///
    /// 全部幽灵共用一个材质实例，改颜色靠 MaterialPropertyBlock，避免每个方块一个材质。
    /// </summary>
    public class GhostBlockManager : MonoBehaviour
    {
        [Header("外观")]
        [Tooltip("半透明方块的透明度")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float alpha = 0.35f;

        [Tooltip("幽灵比真方块略大一点点，才不会被挡住看不清")]
        [SerializeField] private float sizeScale = 1.02f;

        [Header("引用（留空自动找）")]
        [SerializeField] private GridWorld world;

        private readonly Dictionary<int, GameObject> _ghosts = new Dictionary<int, GameObject>();
        private Material _sharedMaterial;
        private MaterialPropertyBlock _block;

        public static GhostBlockManager Instance { get; private set; }

        public int Count => _ghosts.Count;

        private void Awake()
        {
            Instance = this;
            _block = new MaterialPropertyBlock();
        }

        private void Start()
        {
            if (world == null) world = GridWorld.Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>给某条任务显示幽灵。同一个 jobId 重复调用会更新颜色，不会叠两个。</summary>
        public void Show(int jobId, GridPos cell, Color color)
        {
            if (world == null) world = GridWorld.Instance;
            if (world == null) return;

            GameObject ghost;
            if (!_ghosts.TryGetValue(jobId, out ghost) || ghost == null)
            {
                ghost = CreateGhost();
                _ghosts[jobId] = ghost;
            }

            Vector3 center = world.GridToWorldCenter(cell);
            float h = world.CellSize * 0.5f;
            ghost.transform.position = new Vector3(center.x, h, center.z);

            color.a = alpha;
            var renderer = ghost.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            renderer.SetPropertyBlock(_block);
        }

        public void Hide(int jobId)
        {
            GameObject ghost;
            if (!_ghosts.TryGetValue(jobId, out ghost)) return;

            _ghosts.Remove(jobId);
            if (ghost != null) Destroy(ghost);
        }

        public void HideAll()
        {
            foreach (var pair in _ghosts)
            {
                if (pair.Value != null) Destroy(pair.Value);
            }
            _ghosts.Clear();
        }

        private GameObject CreateGhost()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "GhostBlock";

            // 不要碰撞体：它只是提示，不能挡住任何东西
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            float cell = world != null ? world.CellSize : 1f;
            go.transform.localScale = Vector3.one * (cell * sizeScale);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetSharedMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.transform.SetParent(transform, true);
            return go;
        }

        private Material GetSharedMaterial()
        {
            if (_sharedMaterial != null) return _sharedMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.name = "GhostBlock (runtime)";

            // 切成透明混合。URP Lit 的透明不是改个 alpha 就行的，
            // 必须同时改 Surface/Blend 关键字、混合模式、ZWrite 和渲染队列，
            // 否则方块会变成"半透明但写深度"，透过它看到的东西会缺一块。
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            _sharedMaterial = mat;
            return mat;
        }
    }
}

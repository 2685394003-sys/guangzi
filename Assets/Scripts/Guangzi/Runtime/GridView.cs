using System.Collections.Generic;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 地面网格渲染（grid view）。分块合并网格（chunk mesh），不是一格一个 GameObject。
    ///
    /// 为什么不用 Tilemap：Unity 的 Tilemap 是 2D 专用，3D 用不了。
    /// 为什么不上一格一物体：32×32 就是 1024 个 draw call，跑起来就废了。
    /// 这里按 chunk 合并成少量 Mesh，格线靠棋盘色差表达，等美术进场再换图集 UV。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GridView : MonoBehaviour
    {
        [Header("分块大小（格）")]
        [SerializeField] private int chunkSize = 16;

        [Header("棋盘格两色")]
        [SerializeField] private Color colorA = new Color(0.38f, 0.48f, 0.34f, 1f);
        [SerializeField] private Color colorB = new Color(0.33f, 0.43f, 0.30f, 1f);

        [Header("起始时自动生成")]
        [SerializeField] private bool buildOnStart = true;

        private readonly List<GameObject> _chunks = new List<GameObject>();
        private Material _matA;
        private Material _matB;
        private bool _built;

        private void Start()
        {
            if (buildOnStart)
            {
                Rebuild();
            }
        }

        private void OnDestroy()
        {
            ClearChunks();

            if (_matA != null) Destroy(_matA);
            if (_matB != null) Destroy(_matB);
        }

        public void Rebuild()
        {
            var world = GridWorld.Instance;
            if (world == null)
            {
                Debug.LogError("[GridView] 场景里找不到 GridWorld，无法生成地面。");
                return;
            }

            ClearChunks();
            EnsureMaterials();

            int totalW = world.Width;
            int totalH = world.Height;
            float cell = world.CellSize;
            int step = Mathf.Max(1, chunkSize);

            for (int cz = 0; cz < totalH; cz += step)
            {
                for (int cx = 0; cx < totalW; cx += step)
                {
                    int w = Mathf.Min(step, totalW - cx);
                    int h = Mathf.Min(step, totalH - cz);

                    var chunkGo = new GameObject("Chunk_" + cx + "_" + cz);
                    chunkGo.transform.SetParent(transform, false);
                    chunkGo.transform.localPosition = new Vector3(cx * cell, 0f, cz * cell);

                    var mf = chunkGo.AddComponent<MeshFilter>();
                    var mr = chunkGo.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                    mr.sharedMaterials = new[] { _matA, _matB };

                    mf.sharedMesh = BuildChunkMesh(cx, cz, w, h, cell);
                    _chunks.Add(chunkGo);
                }
            }

            _built = true;
        }

        private Mesh BuildChunkMesh(int originX, int originZ, int w, int h, float cell)
        {
            var vertices = new List<Vector3>(w * h * 4);
            var uvs = new List<Vector2>(w * h * 4);
            var trisA = new List<int>(w * h * 6);
            var trisB = new List<int>(w * h * 6);

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 局部坐标：chunk 自己的原点在左下角
                    float x0 = x * cell;
                    float z0 = z * cell;
                    float x1 = x0 + cell;
                    float z1 = z0 + cell;

                    int baseIndex = vertices.Count;

                    vertices.Add(new Vector3(x0, 0f, z0));
                    vertices.Add(new Vector3(x1, 0f, z0));
                    vertices.Add(new Vector3(x1, 0f, z1));
                    vertices.Add(new Vector3(x0, 0f, z1));

                    uvs.Add(new Vector2(0f, 0f));
                    uvs.Add(new Vector2(1f, 0f));
                    uvs.Add(new Vector2(1f, 1f));
                    uvs.Add(new Vector2(0f, 1f));

                    // 绕序说明：从上方往下看要是正面，(0,2,1) 与 (0,3,2) 的法线才朝 +Y
                    var target = ((originX + x + originZ + z) & 1) == 0 ? trisA : trisB;
                    target.Add(baseIndex + 0);
                    target.Add(baseIndex + 2);
                    target.Add(baseIndex + 1);
                    target.Add(baseIndex + 0);
                    target.Add(baseIndex + 3);
                    target.Add(baseIndex + 2);
                }
            }

            var mesh = new Mesh { name = "GridChunk" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisA, 0);
            mesh.SetTriangles(trisB, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void EnsureMaterials()
        {
            if (_matA == null) _matA = CreateOpaqueMaterial(colorA, "GridMat_A");
            if (_matB == null) _matB = CreateOpaqueMaterial(colorB, "GridMat_B");
        }

        private static Material CreateOpaqueMaterial(Color color, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            mat.color = color;
            return mat;
        }

        private void ClearChunks()
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i] == null) continue;

                var mf = _chunks[i].GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Destroy(mf.sharedMesh);
                }

                Destroy(_chunks[i]);
            }

            _chunks.Clear();
            _built = false;
        }

        public bool IsBuilt => _built;
    }
}

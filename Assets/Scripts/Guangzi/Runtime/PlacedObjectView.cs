using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 已放置物块的可视化（placed object view）。
    /// 订阅 GridWorld 的放置/移除/重建事件，只负责「长什么样」，不参与任何规则判断。
    ///
    /// 第一周直接用 Cube primitive + 按 id 取色，**不做对象池**：
    /// 先做对再做快，等物块数量上来了再换池化和图集。
    /// </summary>
    public class PlacedObjectView : MonoBehaviour
    {
        [Header("占位配色（等美术资源进场后删）")]
        [SerializeField] private Color woodColor = new Color(0.62f, 0.44f, 0.26f, 1f);
        [SerializeField] private Color stoneColor = new Color(0.55f, 0.56f, 0.58f, 1f);
        [SerializeField] private Color floorColor = new Color(0.42f, 0.40f, 0.38f, 1f);

        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

        private GridWorld _world;

        private void Start()
        {
            _world = GridWorld.Instance;
            if (_world == null)
            {
                Debug.LogError("[PlacedObjectView] 场景里找不到 GridWorld。");
                return;
            }

            _world.ObjectPlaced += OnObjectPlaced;
            _world.ObjectRemoved += OnObjectRemoved;
            _world.MapRebuilt += OnMapRebuilt;

            RebuildAll();
        }

        private void OnDestroy()
        {
            if (_world != null)
            {
                _world.ObjectPlaced -= OnObjectPlaced;
                _world.ObjectRemoved -= OnObjectRemoved;
                _world.MapRebuilt -= OnMapRebuilt;
            }

            ClearAllViews();

            foreach (var pair in _materials)
            {
                if (pair.Value != null) Destroy(pair.Value);
            }
            _materials.Clear();
        }

        private void OnObjectPlaced(PlacedObject obj)
        {
            if (_views.ContainsKey(obj.id)) return;
            _views[obj.id] = CreateView(obj);
        }

        private void OnObjectRemoved(PlacedObject obj)
        {
            if (!_views.TryGetValue(obj.id, out var go)) return;
            _views.Remove(obj.id);
            if (go != null) Destroy(go);
        }

        private void OnMapRebuilt()
        {
            RebuildAll();
        }

        private void RebuildAll()
        {
            ClearAllViews();
            if (_world == null || _world.Map == null) return;

            foreach (var pair in _world.Map.Objects)
            {
                _views[pair.Key] = CreateView(pair.Value);
            }
        }

        private void ClearAllViews()
        {
            foreach (var pair in _views)
            {
                if (pair.Value != null) Destroy(pair.Value);
            }
            _views.Clear();
        }

        private GameObject CreateView(PlacedObject obj)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Obj_" + obj.def.id + "_" + obj.id;
            go.transform.SetParent(transform, false);

            // 射线检测走数学平面，不依赖物理，碰撞体一律去掉
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            float cell = _world.CellSize;
            float height = Mathf.Max(0.02f, obj.def.visualHeight);

            Vector3 corner = _world.GridToWorldCorner(obj.origin);
            Vector3 center = corner + new Vector3(
                obj.def.width * cell * 0.5f,
                0f,
                obj.def.height * cell * 0.5f);

            go.transform.position = center + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(
                obj.def.width * cell * 0.96f,
                height,
                obj.def.height * cell * 0.96f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(obj.def.id);
            return go;
        }

        private Material GetMaterial(string defId)
        {
            if (_materials.TryGetValue(defId, out var cached)) return cached;

            Color color;
            if (defId.StartsWith("floor"))
            {
                color = floorColor;
            }
            else if (defId.Contains("stone"))
            {
                color = stoneColor;
            }
            else
            {
                color = woodColor;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = "Mat_" + defId };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            mat.color = color;

            _materials[defId] = mat;
            return mat;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 像素密度跟随缩放（dynamic pixel density）。
    ///
    /// **解决什么问题**：URP 的 Render Scale 是固定值，相机一拉远，角色在屏幕上只剩 3~4 个像素，
    /// 无论如何调描边都糊成一坨——因为细节在**渲染阶段**就丢了，后处理救不回来。
    ///
    /// **思路**：把「1 个内部像素 = 固定屏幕面积」改成「1 个内部像素 = 固定世界尺寸」。
    /// 相机拉远 2 倍，内部分辨率就提高 2 倍，物体在屏幕上占的像素数保持不变。
    /// 这是像素艺术该有的行为：像素代表世界里的固定尺寸，而不是屏幕上的一块。
    ///
    /// **代价**：拉得越远，像素颗粒相对越细，块状感越弱。
    /// 这是物理必然——想在远处看清细节，就不能同时保留大色块。要"一直很块"就得限制最大缩放。
    ///
    /// 改的是全局 `UniversalRenderPipelineAsset.renderScale`（URP 17 没有逐相机分辨率），
    /// 所以做了节流：变化超过阈值才写入，避免每帧重建渲染目标。
    /// </summary>
    public class DynamicPixelScale : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("留空自动找主相机上的 GridCamera")]
        [SerializeField] private GridCamera targetCamera;

        [Header("像素密度")]
        [Tooltip("参考距离下的内部分辨率比例，就是原来固定的那个 Render Scale")]
        [SerializeField] private float baseRenderScale = 0.3f;

        [Tooltip("这个距离下用 baseRenderScale，拉远就按比例提高")]
        [SerializeField] private float referenceDistance = 11f;

        [SerializeField] private float minScale = 0.25f;
        [SerializeField] private float maxScale = 1f;

        [Header("性能")]
        [Tooltip("变化超过这个量才写入全局设置，避免频繁重建渲染目标")]
        [SerializeField] private float updateThreshold = 0.02f;

        private UniversalRenderPipelineAsset _asset;
        private float _applied = -1f;

        /// <summary>当前实际生效的渲染比例，给调试看。</summary>
        public float AppliedScale => _applied;

        private void Awake()
        {
            _asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

            if (_asset == null)
            {
                Debug.LogWarning("[DynamicPixelScale] 当前渲染管线不是 URP，本组件不生效。");
                enabled = false;
                return;
            }

            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();
        }

        private void LateUpdate()
        {
            if (_asset == null || targetCamera == null) return;

            float distance = Mathf.Max(0.01f, targetCamera.CameraDistance);
            float ratio = distance / Mathf.Max(0.01f, referenceDistance);
            float target = Mathf.Clamp(baseRenderScale * ratio, minScale, maxScale);

            if (Mathf.Abs(target - _applied) < updateThreshold) return;

            _applied = target;
            _asset.renderScale = target;
        }
    }
}

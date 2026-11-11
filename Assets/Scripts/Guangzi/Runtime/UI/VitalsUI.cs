using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 右上角的血量 / 体力 / 金币 HUD（星露谷式）。
    ///
    /// 条的填充用 RectTransform 的 anchorMax.x 直接控制宽度，
    /// 不用 Image.type = Filled——那个必须有 sprite 才生效，纯色块用不了。
    ///
    /// 显隐规则和快捷栏一致：只在角色视角 + 世界输入模式下出现。
    /// </summary>
    public class VitalsUI : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("要显隐的容器，必须是子物体")]
        [SerializeField] private GameObject root;

        [Tooltip("血条填充：anchorMin 固定 (0,0)，这里改 anchorMax.x 来表现百分比")]
        [SerializeField] private RectTransform healthFill;

        [SerializeField] private RectTransform staminaFill;
        [SerializeField] private Text goldLabel;
        [SerializeField] private GridCamera targetCamera;

        [Header("行为")]
        [SerializeField] private bool onlyInCharacterView = true;

        private PlayerVitalsHolder _holder;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();
        }

        private void Update()
        {
            EnsureBound();
            UpdateVisibility();
        }

        private void EnsureBound()
        {
            var current = PlayerVitalsHolder.Instance;
            if (_holder == current) return;

            if (_holder != null) _holder.Vitals.Changed -= Refresh;

            _holder = current;

            if (_holder != null) _holder.Vitals.Changed += Refresh;

            Refresh();
        }

        private void UpdateVisibility()
        {
            if (root == null) return;

            bool visible = InputModeService.IsWorld;

            if (visible && onlyInCharacterView)
            {
                visible = targetCamera != null && targetCamera.IsCharacterView;
            }

            if (root.activeSelf != visible) root.SetActive(visible);
        }

        private void Refresh()
        {
            if (_holder == null || _holder.Vitals == null) return;

            var vitals = _holder.Vitals;

            SetFill(healthFill, vitals.HealthPercent);
            SetFill(staminaFill, vitals.StaminaPercent);

            if (goldLabel != null) goldLabel.text = vitals.Gold.ToString();
        }

        private static void SetFill(RectTransform fill, float percent)
        {
            if (fill == null) return;

            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(percent);
            fill.anchorMax = max;
        }
    }
}

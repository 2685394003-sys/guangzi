using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 规划面板（planning panel）：上帝视角下常驻在屏幕左侧的木夹板。
    /// 它是「玩家不亲手干活时」的操作入口——仓库、随从、规划都从这里进。
    ///
    /// 和快捷栏一样只在上帝视角出现：角色视角下玩家自己动手，不需要指挥面板。
    /// </summary>
    public class PlanningPanel : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("要显隐的容器，必须是子物体")]
        [SerializeField] private GameObject root;

        [SerializeField] private GridCamera targetCamera;
        [SerializeField] private WarehouseWindow warehouse;

        [Header("按钮")]
        [SerializeField] private Button warehouseButton;
        [SerializeField] private Button followerButton;
        [SerializeField] private Button planButton;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();
        }

        private void OnEnable()
        {
            if (warehouseButton != null) warehouseButton.onClick.AddListener(OnWarehouseClicked);
            if (followerButton != null) followerButton.onClick.AddListener(OnFollowerClicked);
            if (planButton != null) planButton.onClick.AddListener(OnPlanClicked);
        }

        private void OnDisable()
        {
            if (warehouseButton != null) warehouseButton.onClick.RemoveListener(OnWarehouseClicked);
            if (followerButton != null) followerButton.onClick.RemoveListener(OnFollowerClicked);
            if (planButton != null) planButton.onClick.RemoveListener(OnPlanClicked);
        }

        private void Update()
        {
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (root == null) return;

            bool visible = InputModeService.IsWorld
                           && targetCamera != null
                           && !targetCamera.IsCharacterView;

            // 面板本身隐藏时，挂在上面的窗口也要收起来，否则下次切视角会留个孤零零的弹窗
            if (!visible && warehouse != null && warehouse.IsOpen) warehouse.Close();

            if (root.activeSelf != visible) root.SetActive(visible);
        }

        private void OnWarehouseClicked()
        {
            if (warehouse != null) warehouse.Toggle();
        }

        private void OnFollowerClicked()
        {
            Debug.Log("[PlanningPanel] 随从面板还没做。");
        }

        private void OnPlanClicked()
        {
            Debug.Log("[PlanningPanel] 规划面板还没做。");
        }
    }
}

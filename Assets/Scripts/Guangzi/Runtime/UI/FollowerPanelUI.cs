using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 随从面板（follower panel）：左栏数值、右栏背包。
    ///
    /// 打开期间每帧刷新——随从的状态、体力、背包内容都在实时变，
    /// 用事件订阅反而要挂一堆监听，这里数据量小，每帧刷更省心。
    /// 关闭后不再刷新。
    /// </summary>
    public class FollowerPanelUI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private Button closeButton;
        [SerializeField] private FollowerController follower;

        [Header("左栏：数值")]
        [SerializeField] private Text titleLabel;
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private RectTransform staminaFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Text staminaText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text jobText;

        [Header("右栏：背包")]
        [SerializeField] private HotbarSlotView[] bagSlots;
        [SerializeField] private Text emptyHint;

        public static FollowerPanelUI Instance { get; private set; }

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;

            if (root == null) root = gameObject;
            if (windowRect == null) windowRect = root.transform as RectTransform;
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (!IsOpen) return;
            Refresh();
        }

        public void Open()
        {
            if (root == null) return;

            root.SetActive(true);
            root.transform.SetAsLastSibling();

            // 和仓库窗口一致的定位：居中
            if (windowRect != null) windowRect.anchoredPosition = Vector2.zero;

            Refresh();
        }

        public void Close()
        {
            if (root == null) return;
            root.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Refresh()
        {
            if (follower == null) return;

            if (titleLabel != null) titleLabel.text = "随从";

            RefreshVitals();
            RefreshStatus();
            RefreshBag();
        }

        private void RefreshVitals()
        {
            var vitals = follower.Vitals;
            if (vitals == null) return;

            SetFill(healthFill, vitals.HealthPercent);
            SetFill(staminaFill, vitals.StaminaPercent);

            if (healthText != null) healthText.text = vitals.Health + " / " + vitals.MaxHealth;
            if (staminaText != null) staminaText.text = vitals.Stamina + " / " + vitals.MaxStamina;
        }

        private void RefreshStatus()
        {
            if (stateText != null) stateText.text = "状态：" + DescribeState(follower.StateName);

            if (jobText == null) return;

            if (!follower.HasJob)
            {
                jobText.text = "任务：无";
                return;
            }

            string type = follower.CurrentJobType == "build" ? "建造" : "挖掘";
            int percent = Mathf.RoundToInt(follower.CurrentProgress * 100f);
            jobText.text = "任务：" + type + " " + percent + "%";
        }

        private void RefreshBag()
        {
            var bag = follower.Bag;
            int shown = 0;

            if (bagSlots != null)
            {
                for (int i = 0; i < bagSlots.Length; i++)
                {
                    if (bagSlots[i] == null) continue;

                    ItemStack stack = bag != null && i < bag.Capacity ? bag.GetSlot(i) : null;
                    bagSlots[i].Refresh(stack);
                    bagSlots[i].SetSelected(false);

                    if (stack != null && !stack.IsEmpty) shown++;
                }
            }

            if (emptyHint != null) emptyHint.gameObject.SetActive(shown == 0);
        }

        private static void SetFill(RectTransform fill, float percent)
        {
            if (fill == null) return;

            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(percent);
            fill.anchorMax = max;
        }

        /// <summary>状态机内部名 → 玩家看得懂的中文。</summary>
        private static string DescribeState(string state)
        {
            switch (state)
            {
                case "Idle": return "待命";
                case "Moving": return "搬运中";
                case "Working": return "干活中";
                case "GoingToPlayer": return "去找玩家";
                default: return state;
            }
        }
    }
}

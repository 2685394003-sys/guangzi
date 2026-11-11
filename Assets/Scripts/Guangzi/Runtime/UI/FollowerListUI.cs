using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 随从列表（follower list）：点规划栏「随从」按钮弹出，
    /// 一行一个随从，点行直接打开那个随从的面板。
    ///
    /// 为什么要这个列表：随从多了以后，靠「走到谁旁边右键」太慢，
    /// 而且玩家不一定记得每个随从身上背了什么。列表一眼看完，点谁开谁。
    /// </summary>
    public class FollowerListUI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;

        [Header("列表")]
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private GameObject rowTemplate;
        [SerializeField] private Text emptyHint;

        [Header("面板")]
        [SerializeField] private FollowerPanelUI followerPanel;

        private readonly List<GameObject> _rows = new List<GameObject>();

        public static FollowerListUI Instance { get; private set; }

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;

            if (root == null) root = gameObject;
            if (windowRect == null) windowRect = root.transform as RectTransform;

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rowTemplate != null) rowTemplate.SetActive(false);

            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (root == null) return;

            root.SetActive(true);
            root.transform.SetAsLastSibling();
            if (windowRect != null) windowRect.anchoredPosition = Vector2.zero;

            if (titleLabel != null) titleLabel.text = "随从";

            Rebuild();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Rebuild()
        {
            ClearRows();

            int count = FollowerController.All.Count;

            for (int i = 0; i < count; i++)
            {
                var follower = FollowerController.All[i];
                if (follower == null) continue;
                CreateRow(follower);
            }

            if (emptyHint != null) emptyHint.gameObject.SetActive(count == 0);

            // 行是 Instantiate 出来的，当帧没有 Rect，点击会穿透——立刻重算布局
            if (rowContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rowContainer);
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) Destroy(_rows[i]);
            }

            _rows.Clear();
        }

        private void CreateRow(FollowerController follower)
        {
            if (rowTemplate == null || rowContainer == null) return;

            var row = Instantiate(rowTemplate, rowContainer);
            row.SetActive(true);
            _rows.Add(row);

            var label = row.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = follower.DisplayName + "    ·    " + DescribeState(follower.StateName);
            }

            var button = row.GetComponent<Button>();
            if (button == null) return;

            var target = follower;
            button.onClick.AddListener(() => OpenFollower(target));
        }

        private void OpenFollower(FollowerController follower)
        {
            if (follower == null) return;

            Close();

            if (followerPanel != null) followerPanel.Open(follower);
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

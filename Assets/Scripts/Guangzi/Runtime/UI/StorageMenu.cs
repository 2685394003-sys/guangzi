using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 仓库菜单（storage menu）：靠近仓库右键打开，列出里面的东西，
    /// 点哪一行就让随从去取一批送过来。
    ///
    /// 做成「列表」而不是格子面板：仓库的东西是「点单」用的，
    /// 玩家关心的是「有没有、有多少」，不是摆放位置。
    /// 以后要做箱子 UI 时再换成格子视图。
    /// </summary>
    public class StorageMenu : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform rowContainer;

        [Tooltip("行模板，必须默认隐藏；运行时按物品数量复制")]
        [SerializeField] private GameObject rowTemplate;

        [Tooltip("全屏透明遮罩，点它关闭")]
        [SerializeField] private GameObject blocker;

        [SerializeField] private Text titleLabel;
        [SerializeField] private Text emptyHint;

        [Header("取货")]
        [Tooltip("点一行让随从送多少个")]
        [SerializeField] private int batchSize = 5;

        [Tooltip("仓库面板相对屏幕中心的位置偏移")]
        [SerializeField] private Vector2 screenOffset = Vector2.zero;

        private readonly List<GameObject> _rows = new List<GameObject>();

        public static StorageMenu Instance { get; private set; }

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;

            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (root != null) root.SetActive(false);
            if (blocker != null) blocker.SetActive(false);

            if (blocker != null)
            {
                var btn = blocker.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(Hide);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show()
        {
            var storage = StoragePoint.Instance;
            if (storage == null) return;

            if (blocker != null) blocker.SetActive(true);
            if (root == null) return;

            root.SetActive(true);
            root.transform.SetAsLastSibling();

            Rebuild(storage);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
        }

        private void Rebuild(StoragePoint storage)
        {
            ClearRows();

            if (titleLabel != null) titleLabel.text = "仓库";

            var inventory = storage.Inventory;
            int shown = 0;

            if (inventory != null)
            {
                for (int i = 0; i < inventory.Capacity; i++)
                {
                    var stack = inventory.GetSlot(i);
                    if (stack == null || stack.IsEmpty) continue;

                    CreateRow(storage, stack);
                    shown++;
                }
            }

            if (emptyHint != null) emptyHint.gameObject.SetActive(shown == 0);

            // 必须立刻重算布局。
            // Instantiate 出来的行在这一帧还没有 Rect（高宽都是 0），
            // 如果玩家马上点，射线检测命中的是 0 尺寸的图形——直接穿透到遮罩上，
            // 表现为「点了行没反应，菜单却关了」。
            if (rowContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowContainer);
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) Destroy(_rows[i]);
            }

            _rows.Clear();
        }

        private void CreateRow(StoragePoint storage, ItemStack stack)
        {
            if (rowTemplate == null || rowContainer == null) return;

            var row = Instantiate(rowTemplate, rowContainer);
            row.SetActive(true);
            _rows.Add(row);

            var label = row.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = stack.Def.displayName + "  ×" + stack.Amount;
            }

            var button = row.GetComponent<Button>();
            if (button == null) return;

            // 闭包捕获物品 id，不要捕获 stack（仓库内容会变，stack 可能失效）
            string itemId = stack.Def.id;
            button.onClick.AddListener(() => RequestItem(storage, itemId));
        }

        private void RequestItem(StoragePoint storage, string itemId)
        {
            var world = GridWorld.Instance;
            if (world != null && world.Jobs != null)
            {
                world.Jobs.EnqueueFetch(storage.Cell, itemId, batchSize);
            }

            Hide();
        }
    }
}

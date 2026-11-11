using Guangzi.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 物品右键菜单（item context menu）：在鼠标位置弹出，对某一件物品做操作。
    ///
    /// 现在两项：
    /// - 锁定 / 解锁：锁住的物品不会被随从自动送走，留在身上
    /// - 食用：只对食物出现，吃一份恢复体力
    ///
    /// 设计上让「可选项」跟着物品自己长出来（食物才有食用），
    /// 以后加「装备」「丢弃」只需要往菜单里塞按钮，不用改调用方。
    /// </summary>
    public class ItemContextMenu : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform menuRect;

        [Tooltip("全屏透明遮罩，点它关闭菜单")]
        [SerializeField] private GameObject blocker;

        [SerializeField] private Button takeButton;
        [SerializeField] private Text takeLabel;

        [SerializeField] private Button lockButton;
        [SerializeField] private Text lockLabel;

        [SerializeField] private Button eatButton;
        [SerializeField] private Text eatLabel;

        [Header("布局")]
        [Tooltip("菜单相对鼠标的偏移，避免正好盖住指针")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(8f, -8f);

        [Tooltip("离屏幕边缘至少留这么多，防止菜单被裁掉")]
        [SerializeField] private float screenMargin = 12f;

        private ItemStack _target;
        private FollowerController _follower;
        private RectTransform _canvasRect;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) _canvasRect = canvas.transform as RectTransform;

            if (takeButton != null) takeButton.onClick.AddListener(OnTakeClicked);
            if (lockButton != null) lockButton.onClick.AddListener(OnLockClicked);
            if (eatButton != null) eatButton.onClick.AddListener(OnEatClicked);

            // 遮罩本身就是个按钮，点它关菜单
            if (blocker != null)
            {
                var blockerButton = blocker.GetComponent<Button>();
                if (blockerButton != null) blockerButton.onClick.AddListener(OnBlockerClicked);
            }

            if (root != null) root.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
        }

        private void OnDestroy()
        {
            if (takeButton != null) takeButton.onClick.RemoveListener(OnTakeClicked);
            if (lockButton != null) lockButton.onClick.RemoveListener(OnLockClicked);
            if (eatButton != null) eatButton.onClick.RemoveListener(OnEatClicked);
        }

        public void Show(ItemStack stack, FollowerController follower, Vector2 screenPosition)
        {
            if (stack == null || stack.IsEmpty) return;

            _target = stack;
            _follower = follower;

            if (blocker != null) blocker.SetActive(true);
            if (root == null) return;

            root.SetActive(true);
            root.transform.SetAsLastSibling();

            UpdateItems();
            PlaceAt(screenPosition);
        }

        public void Hide()
        {
            _target = null;

            if (root != null) root.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
        }

        /// <summary>点遮罩关闭（也可以在 blocker 的 Button 上挂这个）。</summary>
        public void OnBlockerClicked()
        {
            Hide();
        }

        private void UpdateItems()
        {
            if (_target == null) return;

            if (takeLabel != null) takeLabel.text = "获得（" + _target.Amount + "）";
            if (lockLabel != null) lockLabel.text = _target.Locked ? "解除锁定" : "锁定";

            bool isFood = _target.Def.IsFood;
            if (eatButton != null) eatButton.gameObject.SetActive(isFood);

            if (isFood && eatLabel != null)
            {
                eatLabel.text = "食用（+" + _target.Def.nutrition + " 体力）";
            }
        }

        /// <summary>把菜单放到鼠标位置，但不出屏幕。</summary>
        private void PlaceAt(Vector2 screenPosition)
        {
            if (menuRect == null) return;

            // 单位要对齐：CanvasScaler 会缩放 UI，
            // 所以 rect.size 是「UI 单位」而鼠标坐标和 position 是「屏幕像素」。
            // 不乘 lossyScale 就直接和屏幕边界比，Clamp 会算错，菜单会被推出屏幕。
            float scale = _canvasRect != null ? _canvasRect.lossyScale.x : 1f;
            if (scale <= 0.0001f) scale = 1f;

            Vector2 size = menuRect.rect.size * scale;
            float width = Screen.width;
            float height = Screen.height;

            Vector2 target = screenPosition + cursorOffset;

            // 靠右下角时翻到指针另一侧，别让菜单长到屏幕外面
            if (target.x + size.x > width - screenMargin) target.x = screenPosition.x - size.x - cursorOffset.x;
            if (target.y + size.y > height - screenMargin) target.y = screenPosition.y + size.y + Mathf.Abs(cursorOffset.y);

            target.x = Mathf.Clamp(target.x, screenMargin, Mathf.Max(screenMargin, width - size.x - screenMargin));
            target.y = Mathf.Clamp(target.y, screenMargin, Mathf.Max(screenMargin, height - size.y - screenMargin));

            menuRect.position = target;
        }

        private void OnTakeClicked()
        {
            if (_target != null && _follower != null) _follower.GiveItemToPlayer(_target);
            Hide();
        }

        private void OnLockClicked()
        {
            if (_target != null) _target.Locked = !_target.Locked;
            Hide();
        }

        private void OnEatClicked()
        {
            if (_target != null && _follower != null) _follower.EatItem(_target);
            Hide();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (_target != null && _target.IsEmpty) Hide();
        }
    }
}

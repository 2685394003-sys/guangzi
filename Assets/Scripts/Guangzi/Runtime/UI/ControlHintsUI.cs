using System.Text;
using Guangzi.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 左上角操作提示。内容不是写死的字符串，而是**跟着当前状态算出来的**：
    /// 视角不同、手上拿的东西不同，提示就不同。
    ///
    /// 为什么要有它：这个游戏两套视角的操作差异很大（WASD 归谁、左键干什么都不一样的），
    /// 光靠记是记不住的。让提示跟着当前能做的事实时变，玩家不用背键位。
    ///
    /// ESC 菜单打开时整块消失（InputModeService 非 World）。
    /// </summary>
    public class ControlHintsUI : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("要显隐的容器，必须是子物体")]
        [SerializeField] private GameObject root;

        [SerializeField] private Text hintText;
        [SerializeField] private GridCamera targetCamera;
        [SerializeField] private HotbarUI hotbar;
        [SerializeField] private WarehouseWindow warehouse;

        private string _lastText;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = FindFirstObjectByType<GridCamera>();
            if (hotbar == null) hotbar = FindFirstObjectByType<HotbarUI>();
            if (warehouse == null) warehouse = FindFirstObjectByType<WarehouseWindow>();
        }

        private void Update()
        {
            UpdateVisibility();

            if (root == null || !root.activeSelf) return;
            UpdateText();
        }

        private void UpdateVisibility()
        {
            if (root == null) return;

            bool visible = InputModeService.IsWorld;
            if (root.activeSelf != visible) root.SetActive(visible);
        }

        private void UpdateText()
        {
            if (hintText == null || targetCamera == null) return;

            string text = targetCamera.IsCharacterView ? BuildCharacterHint() : BuildGodHint();

            // 只有内容真变了才写 Text，避免每帧触发 UI 重建
            if (text == _lastText) return;

            _lastText = text;
            hintText.text = text;
        }

        private string BuildCharacterHint()
        {
            var sb = new StringBuilder(192);

            sb.Append("角色视角\n");
            sb.Append("WASD 移动   ·   空格 跳跃   ·   滚轮 缩放\n");
            sb.Append(DescribeHandAction(GetHotbarStack()));
            sb.Append("\n1~9 选格   ·   R 切换视角");

            return sb.ToString();
        }

        private string BuildGodHint()
        {
            var sb = new StringBuilder(256);

            sb.Append("上帝视角\n");
            sb.Append("WASD 平移   ·   滚轮 缩放\n");

            // 右键在这个视角有两个用途（点一下取消手持、按住拖拽移镜头），
            // 所以不能笼统写「右键拖拽」——得把两种手势分清楚，否则玩家一拖镜头就把手上的东西丢了。
            var stack = warehouse != null ? warehouse.SelectedStack : null;

            if (stack == null || stack.IsEmpty)
            {
                sb.Append("左键 无操作（先在仓库选物品）\n");
                sb.Append("右键 按住拖拽移镜头\n");
            }
            else if (stack.Def.category == ItemCategory.Buildable)
            {
                sb.Append("左键 派随从建造 ").Append(stack.Def.displayName).Append('\n');
                sb.Append("右键 点一下取消手持   ·   按住拖拽移镜头\n");
            }
            else if (stack.Def.IsTool)
            {
                sb.Append("左键 派随从挖掘（").Append(stack.Def.displayName).Append("）\n");
                sb.Append("右键 点一下取消手持   ·   按住拖拽移镜头\n");
            }
            else
            {
                sb.Append("左键 无操作（").Append(stack.Def.displayName).Append(" 用不了）\n");
                sb.Append("右键 点一下取消手持   ·   按住拖拽移镜头\n");
            }

            sb.Append("R 切换视角");

            return sb.ToString();
        }

        /// <summary>角色视角下左键会做什么，和 BuildController.CurrentAction 同一套判断。</summary>
        private static string DescribeHandAction(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty) return "左键 挖掘（空手）";

            if (stack.Def.category == ItemCategory.Buildable)
            {
                return "左键 放置 " + stack.Def.displayName;
            }

            if (stack.Def.IsTool)
            {
                return "左键 挖掘（" + stack.Def.displayName + "）";
            }

            return "左键 无操作（" + stack.Def.displayName + " 用不了）";
        }

        private ItemStack GetHotbarStack()
        {
            if (hotbar == null) return null;

            var player = PlayerInventory.Instance;
            if (player == null || player.Inventory == null) return null;

            return player.Inventory.GetSlot(hotbar.SelectedIndex);
        }
    }
}

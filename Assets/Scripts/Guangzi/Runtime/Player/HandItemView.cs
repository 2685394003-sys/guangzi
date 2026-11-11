using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 手上拿的物品（hand item view）。模仿我的世界：
    /// 用当前选中方块的缩小版本挂在角色手边，换物品就换颜色。
    ///
    /// 为什么挂成 Player 的子物体而不是 Visual 的子物体：
    /// Visual 是胶囊体、带非等比缩放（0.7, 0.9, 0.7），挂进去手上的方块会被压扁。
    /// 这里改成每帧跟 Visual 的位置和朝向，但用自己的干净缩放。
    /// </summary>
    public class HandItemView : MonoBehaviour
    {
        [Header("跟随目标")]
        [Tooltip("通常是角色的 Visual 子物体，留空会自动找父级下名为 Visual 的节点")]
        [SerializeField] private Transform follow;

        [Tooltip("相对 Visual 的偏移。朝着角色右手前方")]
        [SerializeField] private Vector3 localOffset = new Vector3(0.42f, 1.0f, 0.32f);

        [Tooltip("手上物品的世界尺寸（格 = 米）。地上的方块是 1 米，这里缩到 1/3")]
        [SerializeField] private float itemScale = 0.32f;

        [Header("引用")]
        [SerializeField] private HotbarUI hotbar;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private string _lastItemId;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();

            if (follow == null && transform.parent != null)
            {
                follow = transform.parent.Find("Visual");
            }
        }

        private void LateUpdate()
        {
            FollowHand();
            UpdateVisual();
        }

        private void FollowHand()
        {
            if (follow == null) return;

            // Visual 带动画的缩放，用 TransformPoint 让偏移跟着它的比例走
            transform.position = follow.TransformPoint(localOffset);
            transform.rotation = follow.rotation;
        }

        private void UpdateVisual()
        {
            if (_renderer == null) return;

            var stack = GetSelectedStack();
            bool show = stack != null && !stack.IsEmpty;

            if (_renderer.enabled != show) _renderer.enabled = show;
            if (!show)
            {
                _lastItemId = null;
                return;
            }

            // 尺寸每帧写一次，改 Inspector 能立刻看到效果
            transform.localScale = Vector3.one * itemScale;

            if (_lastItemId == stack.Def.id) return;
            _lastItemId = stack.Def.id;

            Color color;
            if (!ColorUtility.TryParseHtmlString(stack.Def.colorHex, out color)) return;

            _renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_block);
        }

        private ItemStack GetSelectedStack()
        {
            if (hotbar == null) hotbar = FindFirstObjectByType<HotbarUI>();
            if (hotbar == null) return null;

            var player = PlayerInventory.Instance;
            if (player == null || player.Inventory == null) return null;

            return player.Inventory.GetSlot(hotbar.SelectedIndex);
        }
    }
}

using Guangzi.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 建造交互：鼠标指向 → 合法性预览 → 左键放置 / Shift+左键挖掉。
    ///
    /// **放什么由快捷栏选中项决定**（和我的世界一致）：
    ///   手上是空 / 拿的不是可放置物 → 完全没反应，不会凭空刷方块。
    ///   手上是可放置物 → 落的就是它，落一个扣一个。
    ///
    /// 材料走玩家背包（Inventory 实现了 IResourceStore），
    /// 不再碰那份平行的 ResourceLedger——之前「放了墙但数量不减」就是两套账各记各的。
    ///
    /// 关键纪律：合法性判断一律调 BuildValidator，不在这里写 if。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class BuildController : MonoBehaviour
    {
        [Header("鼠标射线（走数学平面，不依赖碰撞体）")]
        [SerializeField] private float groundHeight = 0f;
        [SerializeField] private float maxRayDistance = 300f;

        [Header("手能伸多远（只在角色视角生效）")]
        [Tooltip("能操作离玩家多远的格子。1 = 上下左右四格（不含脚下）")]
        [SerializeField] private int handReach = 1;

        [Tooltip("关掉则不限制距离，用来对比手感")]
        [SerializeField] private bool limitInCharacterView = true;

        [Header("体力消耗")]
        [Tooltip("挖一个方块花多少体力。0 = 不消耗")]
        [SerializeField] private int staminaPerMine = 2;

        [Header("引用（留空自动查找）")]
        [SerializeField] private CellHighlighter highlighter;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private HotbarUI hotbar;
        [SerializeField] private PlayerController player;
        [SerializeField] private GridCamera viewCamera;

        [Header("调试")]
        [SerializeField] private bool logBuildResults = false;

        private GridWorld _world;
        private bool _hasHover;
        private GridPos _hoverPos;
        private BuildCheck _lastCheck;

        public GridPos HoverPos => _hoverPos;
        public bool HasHover => _hasHover;
        public BuildCheck LastCheck => _lastCheck;

        /// <summary>左键当前会做什么。由手上拿的东西决定，和我的世界一致，不用组合键。</summary>
        private enum HandAction
        {
            /// <summary>手上拿的是用不了的东西（食物等），左键无反应。</summary>
            None = 0,

            /// <summary>手上有可放置方块，左键放置。此时**不能挖掘**。</summary>
            Place = 1,

            /// <summary>空手或拿工具，左键挖掘。</summary>
            Mine = 2,
        }

        private HandAction CurrentAction
        {
            get
            {
                var stack = ActiveStack;

                // 空手 = 挖掘（我的世界的规则：空手也能敲东西，只是慢）
                if (stack == null || stack.IsEmpty) return HandAction.Mine;

                if (stack.Def.category == ItemCategory.Buildable) return HandAction.Place;
                if (stack.Def.IsTool) return HandAction.Mine;

                return HandAction.None;
            }
        }

        /// <summary>上帝视角 = 指挥模式：左键不是亲手干活，而是给随从下单。</summary>
        private bool IsGodView
        {
            get { return viewCamera != null && !viewCamera.IsCharacterView; }
        }

        /// <summary>
        /// 当前「手上」的物品。
        /// 角色视角取快捷栏；上帝视角取仓库——仓库就是上帝视角的快捷栏，
        /// 两边都是「选中一个东西，然后左键作用于地图」。
        /// </summary>
        private ItemStack ActiveStack
        {
            get
            {
                if (IsGodView)
                {
                    var warehouse = WarehouseWindow.Instance;
                    return warehouse != null ? warehouse.SelectedStack : null;
                }

                return SelectedStack;
            }
        }

        /// <summary>当前要放的东西。手上没有可放置物时返回 null（此时点地面不会有任何反应）。</summary>
        public BuildDef CurrentDef
        {
            get
            {
                var stack = ActiveStack;
                if (stack == null || stack.IsEmpty) return null;
                if (stack.Def.category != ItemCategory.Buildable) return null;

                return BuildCatalog.Get(stack.Def.id);
            }
        }

        private ItemStack SelectedStack
        {
            get
            {
                if (hotbar == null) return null;

                var player = PlayerInventory.Instance;
                if (player == null || player.Inventory == null) return null;

                return player.Inventory.GetSlot(hotbar.SelectedIndex);
            }
        }

        private Inventory PlayerBag
        {
            get
            {
                var player = PlayerInventory.Instance;
                return player != null ? player.Inventory : null;
            }
        }

        private void Start()
        {
            if (_world == null) _world = GridWorld.Instance;
            if (highlighter == null) highlighter = FindFirstObjectByType<CellHighlighter>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (hotbar == null) hotbar = FindFirstObjectByType<HotbarUI>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (viewCamera == null) viewCamera = FindFirstObjectByType<GridCamera>();

            // 建造吃玩家背包，替换掉旧的平行台账
            if (_world != null) _world.ResourceStore = PlayerBag;

            if (_world == null) Debug.LogError("[BuildController] 场景里找不到 GridWorld。");
            if (highlighter == null) Debug.LogError("[BuildController] 场景里找不到 CellHighlighter。");
            if (targetCamera == null) Debug.LogError("[BuildController] 找不到主相机（Camera.main），请确认相机 Tag 是 MainCamera。");
        }

        private void Update()
        {
            // ESC 菜单一开就整体停手：不查输入、不刷新高亮、不落方块。
            if (!InputModeService.IsWorld)
            {
                HideHighlight();
                return;
            }

            if (_world == null) _world = GridWorld.Instance;
            if (_world == null || _world.Map == null) return;

            // 背包可能比本组件晚就绪，这里补一次
            if (_world.ResourceStore == null) _world.ResourceStore = PlayerBag;

            UpdateHover();
            HandlePointer();
        }

        private void UpdateHover()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                HideHighlight();
                return;
            }

            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || highlighter == null)
            {
                HideHighlight();
                return;
            }

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

            if (!plane.Raycast(ray, out float enter) || enter > maxRayDistance)
            {
                HideHighlight();
                return;
            }

            Vector3 hit = ray.GetPoint(enter);
            var pos = _world.WorldToGrid(hit);

            if (!_world.Map.InBounds(pos))
            {
                HideHighlight();
                return;
            }

            // 手够不着就不亮：玩家只会看到身边四格有反馈，自然就懂「手有长度」
            if (!InHandReach(pos))
            {
                HideHighlight();
                return;
            }

            _hasHover = true;
            _hoverPos = pos;

            var action = CurrentAction;

            // 挖掘模式：指向已有方块才亮
            if (action == HandAction.Mine)
            {
                if (_world.Map.GetObjectAt(pos) != null) highlighter.ShowRemove(pos);
                else highlighter.Hide();
                return;
            }

            if (action != HandAction.Place)
            {
                highlighter.Hide();
                return;
            }

            var def = CurrentDef;
            if (def == null)
            {
                highlighter.Hide();
                return;
            }

            _lastCheck = BuildValidator.Check(_world.Map, def, pos, PlayerBag);

            if (_lastCheck.ok) highlighter.ShowValid(pos);
            else highlighter.ShowInvalid(pos);
        }

        private void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null || !_hasHover) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;

            // 点到 UI（快捷栏、面板）时不能穿透到世界里——否则点个格子会顺手在地上盖一块
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return;

            // 左键行为完全由手上拿的东西决定，不再用 Shift 组合键
            switch (CurrentAction)
            {
                case HandAction.Place:
                    PlaceAt(_hoverPos);
                    break;

                case HandAction.Mine:
                    MineAt(_hoverPos);
                    break;
            }
        }

        private void PlaceAt(GridPos pos)
        {
            if (!InHandReach(pos)) return;

            var def = CurrentDef;
            if (def == null) return;

            // 上帝视角：排一条建造任务，由随从过去建
            if (IsGodView)
            {
                EnqueueBuildJob(def, pos);
                return;
            }

            // TryPlace 内部会走 BuildValidator + 从 ResourceStore（玩家背包）扣材料
            var result = _world.TryPlace(def, pos);

            if (!result.ok)
            {
                if (logBuildResults)
                {
                    Debug.Log("[Build] 放置失败：" + result.Message + " @" + result.blockingCell);
                }
                return;
            }

            if (logBuildResults) Debug.Log("[Build] 已放置 " + def.id + " @" + pos);
        }

        /// <summary>
        /// 挖掉一个方块。拆解返还比例是 100% 的方块会原物回到背包，
        /// 所以石砖/木块挖回来还是石砖/木块——这就是「破坏方块得到方块」。
        /// </summary>
        private void MineAt(GridPos pos)
        {
            if (!InHandReach(pos)) return;
            if (_world.Map.GetObjectAt(pos) == null) return;

            // 上帝视角：派随从去挖
            if (IsGodView)
            {
                EnqueueMineJob(pos);
                return;
            }

            if (!_world.TryRemoveAt(pos))
            {
                if (logBuildResults) Debug.Log("[Mine] 拆除失败 @" + pos);
                return;
            }

            // 挖掉了才扣体力：失败不扣，避免玩家白掉血
            var vitals = PlayerVitalsHolder.Instance;
            if (vitals != null && staminaPerMine > 0) vitals.SpendStamina(staminaPerMine);

            if (logBuildResults) Debug.Log("[Mine] 已挖掉 @" + pos);
        }

        /// <summary>
        /// 手能不能碰到这一格。
        ///
        /// 只在**角色视角**生效：玩家亲手建造，手只能伸到身边四格（曼哈顿距离 1）。
        /// **上帝视角不限制**——那里是规划用的远程镜头，后续会换成「在计划表上排建造任务，
        /// 由小人跑过去执行」，如果现在给上帝视角也套距离限制，以后还得拆。
        ///
        /// 距离 0（脚下那格）暂时不开放，避免把方块放到自己站的位置里穿模。
        /// </summary>
        private bool InHandReach(GridPos target)
        {
            if (!limitInCharacterView) return true;
            if (viewCamera == null || !viewCamera.IsCharacterView) return true;
            if (player == null || _world == null) return true;

            var playerCell = _world.WorldToGrid(player.transform.position);

            int dx = Mathf.Abs(target.x - playerCell.x);
            int dz = Mathf.Abs(target.z - playerCell.z);
            int distance = dx + dz;

            return distance >= 1 && distance <= handReach;
        }

        /// <summary>
        /// 上帝视角下的建造：不亲手放，而是排一条任务给随从。
        /// 材料先在这里检查——不够就根本不下单，省得随从白跑一趟。
        /// </summary>
        private void EnqueueBuildJob(BuildDef def, GridPos pos)
        {
            if (_world.Jobs.HasActiveJobAt(pos)) return;

            var bag = PlayerBag;
            if (bag != null && !bag.CanAfford(def.cost))
            {
                if (logBuildResults) Debug.Log("[Order] 材料不足，不下单：" + def.id);
                return;
            }

            var job = _world.Jobs.Enqueue(Job.TypeBuild, pos, 100, def, 1.5f);

            if (GhostBlockManager.Instance != null)
            {
                GhostBlockManager.Instance.Show(job.id, pos, ResolveItemColor(def.id, new Color(0.7f, 0.7f, 0.7f)));
            }

            if (logBuildResults) Debug.Log("[Order] 建造单 " + def.id + " @" + pos);
        }

        /// <summary>上帝视角下的挖掘：派随从去挖。标记用红色，和建造单的灰色区分开。</summary>
        private void EnqueueMineJob(GridPos pos)
        {
            if (_world.Jobs.HasActiveJobAt(pos)) return;

            var job = _world.Jobs.Enqueue(Job.TypeMine, pos, 100, null, 1.0f);

            if (GhostBlockManager.Instance != null)
            {
                GhostBlockManager.Instance.Show(job.id, pos, new Color(0.9f, 0.25f, 0.2f));
            }

            if (logBuildResults) Debug.Log("[Order] 挖掘单 @" + pos);
        }

        private static Color ResolveItemColor(string itemId, Color fallback)
        {
            var database = ItemDatabase.Instance;
            if (database == null) return fallback;

            var itemDef = database.Get(itemId);
            if (itemDef == null) return fallback;

            Color color;
            return ColorUtility.TryParseHtmlString(itemDef.colorHex, out color) ? color : fallback;
        }

        private void HideHighlight()
        {
            _hasHover = false;
            if (highlighter != null) highlighter.Hide();
        }
    }
}

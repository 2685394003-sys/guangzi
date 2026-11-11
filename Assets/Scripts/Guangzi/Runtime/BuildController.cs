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

        [Header("右键清空手持")]
        [Tooltip("右键按下后鼠标位移超过这个像素数，就当成拖拽相机，不清空物品")]
        [SerializeField] private float rightClickMaxDrag = 8f;

        [Tooltip("右键按住超过这个秒数，就当成拖拽相机，不清空物品")]
        [SerializeField] private float rightClickMaxSeconds = 0.35f;

        [Header("框选建造（仅上帝视角）")]
        [Tooltip("框选范围的可视化，留空会运行时找")]
        [SerializeField] private BrushSelectionView brushView;

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

        // 右键「点击 vs 拖拽」的判定现场
        private bool _rightPressed;
        private Vector2 _rightDownScreenPos;
        private float _rightDownTime;

        // 左键拖动框选
        private DragState _dragState = DragState.None;
        private GridPos _dragStart;

        /// <summary>正在框选中（给 HUD 提示用）。</summary>
        public bool IsBrushing => _dragState == DragState.Selecting;

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

        /// <summary>左键拖动的三个阶段：按下 → 拖出范围 → 松开确认。</summary>
        private enum DragState
        {
            None = 0,

            /// <summary>按下了但还没拖出起点格，此时松开等于单格放置。</summary>
            Pressed = 1,

            /// <summary>已经拖出范围，松开就是批量确认。</summary>
            Selecting = 2,
        }

        private HandAction CurrentAction
        {
            get
            {
                var stack = ActiveStack;

                if (stack == null || stack.IsEmpty)
                {
                    // 上帝视角是「指挥模式」，空手点地图什么都不做。
                    // 那里视野大、格子密，误触代价高——随手一点就可能把不相干的方块挖了。
                    // 角色视角保持空手可挖（我的世界的规则，人贴得近，误触风险小）。
                    return IsGodView ? HandAction.None : HandAction.Mine;
                }

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
            HandleRightClickClear();
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
            if (mouse == null) return;

            // 框选途中按右键 = 取消（此时不判点击/拖拽，按下就取消）
            if (_dragState != DragState.None && mouse.rightButton.wasPressedThisFrame)
            {
                CancelBrush();
                return;
            }

            // 松开左键 = 确认。拖出去过就批量，没拖就是单格
            if (_dragState != DragState.None && mouse.leftButton.wasReleasedThisFrame)
            {
                ConfirmBrush(IsPointerOverUI());
                return;
            }

            // 拖动中：只更新框选范围，不放置
            if (_dragState != DragState.None)
            {
                UpdateBrush();
                return;
            }

            if (!_hasHover) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (IsPointerOverUI()) return;

            var action = CurrentAction;

            if (action == HandAction.Mine)
            {
                MineAt(_hoverPos);
                return;
            }

            if (action != HandAction.Place) return;

            // 上帝视角：按下先别放，等松开时才知道是单格还是框选
            if (IsGodView)
            {
                _dragState = DragState.Pressed;
                _dragStart = _hoverPos;
                return;
            }

            // 角色视角手只有一格，框选没有意义，按下即放
            PlaceAt(_hoverPos);
        }

        private static bool IsPointerOverUI()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        // ---------- 框选 ----------

        private void UpdateBrush()
        {
            if (_dragState != DragState.Pressed) return;

            // 还停在起点格上，说明只是点了一下，不算框选
            if (_hoverPos.Equals(_dragStart)) return;

            _dragState = DragState.Selecting;
            RefreshBrushView();
        }

        private void RefreshBrushView()
        {
            if (brushView == null) return;

            var def = CurrentDef;
            if (def == null) return;

            brushView.Show(_world, _dragStart, _hoverPos,
                ResolveItemColor(def.id, new Color(0.75f, 0.9f, 0.65f)));
        }

        /// <summary>松开左键：批量下单。点在 UI 上松开视为放弃。</summary>
        private void ConfirmBrush(bool overUI)
        {
            bool wasSelecting = _dragState == DragState.Selecting;
            GridPos start = _dragStart;
            GridPos end = _hoverPos;

            HideBrush();

            if (overUI) return;

            var def = CurrentDef;
            if (def == null) return;

            if (!wasSelecting)
            {
                PlaceAt(start);
                return;
            }

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minZ = Mathf.Min(start.z, end.z);
            int maxZ = Mathf.Max(start.z, end.z);

            int queued = 0;
            int skipped = 0;

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    // 材料不够 / 已有任务 / 位置被占，都会被跳过，不会中断整批
                    if (EnqueueBuildJob(def, new GridPos(x, z))) queued++;
                    else skipped++;
                }
            }

            if (logBuildResults)
            {
                Debug.Log("[Brush] 框选 " + (maxX - minX + 1) + "x" + (maxZ - minZ + 1) +
                          "，下单 " + queued + "，跳过 " + skipped);
            }
        }

        /// <summary>右键中途取消：一个都不下。</summary>
        private void CancelBrush()
        {
            HideBrush();

            if (logBuildResults) Debug.Log("[Brush] 已取消");
        }

        private void HideBrush()
        {
            _dragState = DragState.None;
            if (brushView != null) brushView.Hide();
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
        /// 返回是否真的下了单（框选批量时用来统计跳过数）。
        /// </summary>
        private bool EnqueueBuildJob(BuildDef def, GridPos pos)
        {
            if (_world.Jobs.HasActiveJobAt(pos)) return false;

            var bag = PlayerBag;
            if (bag != null && !bag.CanAfford(def.cost)) return false;

            var job = _world.Jobs.Enqueue(Job.TypeBuild, pos, 100, def, 1.5f);

            if (GhostBlockManager.Instance != null)
            {
                GhostBlockManager.Instance.Show(job.id, pos, ResolveItemColor(def.id, new Color(0.7f, 0.7f, 0.7f)));
            }

            return true;
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

        /// <summary>
        /// 右键清空手上拿的仓库物品。
        ///
        /// 难点：右键同时是相机拖拽键。所以必须区分「点一下」和「拖一下」：
        /// 用位移和按住时长两个条件——只要其中一个超标，就认为玩家在拖相机，不清空。
        /// 只判位移不够：手抖或者帧率低的时候拖拽可能只有两三像素。
        /// </summary>
        private void HandleRightClickClear()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightPressed = true;
                _rightDownScreenPos = mouse.position.ReadValue();
                _rightDownTime = Time.time;
                return;
            }

            if (!_rightPressed) return;
            if (!mouse.rightButton.wasReleasedThisFrame) return;

            _rightPressed = false;

            float drag = (mouse.position.ReadValue() - _rightDownScreenPos).magnitude;
            float held = Time.time - _rightDownTime;

            if (drag > rightClickMaxDrag || held > rightClickMaxSeconds) return;

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return;

            // 靠近随从时，右键优先当作「打开随从面板」，不清空手持
            var interact = FollowerInteract.Instance;
            if (interact != null && interact.TryInteract()) return;

            ClearHeldItem();
        }

        private void ClearHeldItem()
        {
            // 目前只有上帝视角存在「手上拿着仓库物品」这个概念
            if (!IsGodView) return;

            var warehouse = WarehouseWindow.Instance;
            if (warehouse == null) return;
            if (string.IsNullOrEmpty(warehouse.SelectedItemId)) return;

            warehouse.ClearSelection();

            if (logBuildResults) Debug.Log("[Order] 右键清空手持物品");
        }

        private void HideHighlight()
        {
            _hasHover = false;
            if (highlighter != null) highlighter.Hide();
        }
    }
}

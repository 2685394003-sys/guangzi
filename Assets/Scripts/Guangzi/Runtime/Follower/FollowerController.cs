using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 随从（follower）：领任务、搬运材料、干活、把产出送回玩家。
    ///
    /// 这套系统的四条骨架规则：
    /// 1. 随从背包默认只有一格（一种物品），装满了就带不了别的。
    /// 2. 建造材料**必须由随从自己持有**——不够就走回玩家身边取。
    ///    不能直接从玩家仓库划走，否则等于远程施工，搬运这个行为就没意义了。
    /// 3. 挖到的东西先进随从背包，不凭空进玩家仓库。
    /// 4. 只有「闲置」或「需要腾格子」时才把背包里的东西送回玩家。
    ///
    /// 状态机：Idle / Moving / Working / GoingToPlayer。
    /// 后两个状态（GoingToPlayer）是「去玩家那儿办事」，取材料和送东西共用一条路径。
    ///
    /// 不受 InputModeService 影响：ESC 菜单打开时玩家停手，随从该继续干活。
    /// </summary>
    public class FollowerController : MonoBehaviour
    {
        private enum State
        {
            Idle = 0,
            Moving = 1,
            Working = 2,
            GoingToPlayer = 3,
        }

        /// <summary>走到玩家身边之后要办的事。</summary>
        private enum PlayerTask
        {
            None = 0,
            FetchBuildMaterials = 1,
            DeliverItems = 2,
        }

        [Header("移动")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float turnSpeed = 12f;

        [Header("干活")]
        [Tooltip("建造一个方块要多久（秒）")]
        [SerializeField] private float buildSeconds = 1.5f;

        [Tooltip("挖掉一个方块要多久（秒）")]
        [SerializeField] private float mineSeconds = 1.0f;

        [Tooltip("待命时多久扫一次任务队列，避免每帧遍历")]
        [SerializeField] private float idleScanInterval = 0.3f;

        [Header("背包")]
        [Tooltip("能带几种物品。1 = 只能带一组同类型（当前设定）")]
        [SerializeField] private int bagSlots = 1;

        [Header("数值")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int maxStamina = 100;

        [Tooltip("建造一次消耗多少体力")]
        [SerializeField] private int buildStaminaCost = 8;

        [Tooltip("挖掘一次消耗多少体力")]
        [SerializeField] private int mineStaminaCost = 5;

        [Tooltip("待命时每秒恢复多少体力")]
        [SerializeField] private float staminaRegenPerSecond = 3f;

        [Header("视图")]
        [SerializeField] private Transform visual;

        private GridWorld _world;
        private Inventory _bag;
        private PlayerController _player;
        private float _staminaAccumulator;

        private State _state = State.Idle;
        private Job _job;
        private List<GridPos> _path;
        private int _pathIndex;
        private float _idleTimer;

        private PlayerTask _playerTask = PlayerTask.None;
        private Job _pendingJob;

        public Inventory Bag => _bag;
        public bool HasJob => _job != null;
        public string CurrentJobType => _job != null ? _job.type : string.Empty;
        public float CurrentProgress => _job != null ? _job.WorkProgress : 0f;
        public string StateName => _state.ToString();
        public bool IsBagEmpty => _bag == null || BagTotal() == 0;

        /// <summary>随从的血量 / 体力。和玩家共用同一个 PlayerVitals 数据类。</summary>
        public PlayerVitals Vitals { get; private set; }

        private void Awake()
        {
            if (visual == null) visual = transform;
            Vitals = new PlayerVitals(maxHealth, maxStamina);
            EnsureBag();
        }

        /// <summary>
        /// 建随从背包。
        /// **必须传 ItemCatalog**：Inventory.Add(string, int) 在 catalog 为空时会直接 return，
        /// 物品会静默消失——挖到的东西既没进背包也没进玩家仓库，看起来就是「凭空蒸发」。
        /// 物品库可能比本组件晚就绪，所以这里做成可重试的。
        /// </summary>
        private void EnsureBag()
        {
            if (_bag != null) return;

            var database = ItemDatabase.Instance;
            if (database == null) return;

            _bag = new Inventory(Mathf.Max(1, bagSlots), database.Catalog);
        }

        private void Update()
        {
            EnsureBag();
            if (_bag == null) return;

            if (_world == null) _world = GridWorld.Instance;
            if (_world == null || _world.Jobs == null) return;

            switch (_state)
            {
                case State.Idle:
                    TickIdle();
                    break;

                case State.Moving:
                    TickMoving();
                    break;

                case State.Working:
                    TickWorking();
                    break;

                case State.GoingToPlayer:
                    TickGoingToPlayer();
                    break;
            }
        }

        // ---------- 待命 ----------

        private void TickIdle()
        {
            // 待命时回体力。和玩家那套一样的思路：只有闲下来才回，干活时不动
            if (Vitals != null && Vitals.Stamina < Vitals.MaxStamina && staminaRegenPerSecond > 0f)
            {
                _staminaAccumulator += staminaRegenPerSecond * Time.deltaTime;
                if (_staminaAccumulator >= 1f)
                {
                    int whole = Mathf.FloorToInt(_staminaAccumulator);
                    _staminaAccumulator -= whole;
                    Vitals.RestoreStamina(whole);
                }
            }

            _idleTimer -= Time.deltaTime;
            if (_idleTimer > 0f) return;

            _idleTimer = idleScanInterval;

            // 闲置时背包还有东西 → 先送回去。
            // 这是「闲下来才清理」的落点：正忙的时候不折腾背包。
            if (BagTotal() > 0)
            {
                BeginDeliver();
                return;
            }

            Job job;
            if (!_world.Jobs.TryClaimNext(1, out job)) return;

            if (!BeginJob(job)) FailJob(job);
        }

        private bool BeginJob(Job job)
        {
            if (job.type == Job.TypeBuild) return BeginBuildJob(job);
            if (job.type == Job.TypeMine) return BeginMineJob(job);

            return false;
        }

        /// <summary>建造：先看背包够不够，不够就去玩家那儿取。</summary>
        private bool BeginBuildJob(Job job)
        {
            var def = job.buildDef;
            if (def == null) return false;

            if (!_bag.CanAfford(def.cost))
            {
                var playerBag = GetPlayerInventory();
                if (playerBag == null) return false;
                if (!playerBag.CanAfford(def.cost)) return false;

                _pendingJob = job;
                _playerTask = PlayerTask.FetchBuildMaterials;

                if (BeginGoToPlayer()) return true;

                _pendingJob = null;
                _playerTask = PlayerTask.None;
                return false;
            }

            _job = job;
            job.workDone = 0f;
            job.workRequired = buildSeconds;
            return BeginMoveToTarget(job.target);
        }

        /// <summary>挖掘：先看背包放不放得下产物，放不下就先回去腾格子。</summary>
        private bool BeginMineJob(Job job)
        {
            var obj = _world.Map.GetObjectAt(job.target);
            if (obj == null) return false;

            var def = ResolveItem(obj.def.id);
            if (def == null) return false;

            if (!BagCanAccept(def, 1))
            {
                // 这单做不了，标记失败；先把背包送回去，腾出格子再说
                _world.Jobs.Fail(job.id);
                if (GhostBlockManager.Instance != null) GhostBlockManager.Instance.Hide(job.id);

                BeginDeliver();
                return true;
            }

            _job = job;
            job.workDone = 0f;
            job.workRequired = mineSeconds;
            return BeginMoveToTarget(job.target);
        }

        // ---------- 移动 ----------

        private bool BeginMoveToTarget(GridPos target)
        {
            var selfCell = _world.WorldToGrid(transform.position);

            GridPos spot;
            if (!PathFinder.TryFindWorkSpot(_world.Map, target, selfCell, out spot)) return false;

            var path = PathFinder.FindPath(_world.Map, selfCell, spot);
            if (path == null) return false;

            _path = path;
            _pathIndex = 0;
            _state = State.Moving;
            return true;
        }

        private bool BeginGoToPlayer()
        {
            var player = ResolvePlayer();
            if (player == null) return false;

            var selfCell = _world.WorldToGrid(transform.position);
            var playerCell = _world.WorldToGrid(player.transform.position);

            GridPos spot;
            if (!PathFinder.TryFindWorkSpot(_world.Map, playerCell, selfCell, out spot)) return false;

            var path = PathFinder.FindPath(_world.Map, selfCell, spot);
            if (path == null) return false;

            _path = path;
            _pathIndex = 0;
            _state = State.GoingToPlayer;
            return true;
        }

        private void BeginDeliver()
        {
            _playerTask = PlayerTask.DeliverItems;

            if (BeginGoToPlayer()) return;

            _playerTask = PlayerTask.None;
            _state = State.Idle;
            _idleTimer = idleScanInterval;
        }

        private void TickMoving()
        {
            if (_job == null)
            {
                _state = State.Idle;
                return;
            }

            if (StepAlongPath()) return;

            _state = State.Working;
            _job.workDone = 0f;
        }

        private void TickGoingToPlayer()
        {
            if (StepAlongPath()) return;

            var task = _playerTask;
            _playerTask = PlayerTask.None;

            if (task == PlayerTask.DeliverItems) DoDeliver();
            else if (task == PlayerTask.FetchBuildMaterials) DoFetchMaterials();
            else _state = State.Idle;
        }

        /// <summary>沿路径走一步。返回 true = 还在路上，false = 到终点了。</summary>
        private bool StepAlongPath()
        {
            if (_path == null || _pathIndex >= _path.Count) return false;

            Vector3 target = _world.GridToWorldCenter(_path[_pathIndex]);
            var flatTarget = new Vector3(target.x, 0f, target.z);

            Vector3 pos = transform.position;
            var flatPos = new Vector3(pos.x, 0f, pos.z);

            Vector3 delta = flatTarget - flatPos;
            float distance = delta.magnitude;
            float step = moveSpeed * Time.deltaTime;

            if (distance <= step)
            {
                transform.position = flatTarget;
                _pathIndex++;
                return _pathIndex < _path.Count;
            }

            Vector3 dir = delta / distance;
            transform.position = flatPos + dir * step;
            FaceDirection(dir);
            return true;
        }

        private void FaceDirection(Vector3 dir)
        {
            if (visual == null) return;
            if (dir.sqrMagnitude < 0.0001f) return;

            var look = Quaternion.LookRotation(dir, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, look, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        // ---------- 和玩家交接 ----------

        /// <summary>从玩家背包取材料塞进自己包里，然后继续去做那个建造单。</summary>
        private void DoFetchMaterials()
        {
            var job = _pendingJob;
            _pendingJob = null;

            if (job == null || job.buildDef == null)
            {
                _state = State.Idle;
                return;
            }

            var def = job.buildDef;
            var playerBag = GetPlayerInventory();

            if (playerBag == null)
            {
                FailJob(job);
                return;
            }

            var cost = def.cost;
            for (int i = 0; i < cost.Count; i++)
            {
                var need = cost[i];
                int available = playerBag.CountOf(need.resourceId);
                int take = Mathf.Min(need.amount, available);
                if (take <= 0) continue;

                if (!playerBag.TryConsume(need.resourceId, take)) continue;

                var itemDef = ResolveItem(need.resourceId);
                if (itemDef != null) _bag.Add(itemDef, take);
            }

            // 拿完之后再确认一遍：玩家身上其实不够就把单退掉
            if (!_bag.CanAfford(cost))
            {
                FailJob(job);
                return;
            }

            _job = job;
            job.workDone = 0f;
            job.workRequired = buildSeconds;

            if (!BeginMoveToTarget(job.target)) FailJob(job);
        }

        /// <summary>把背包里的东西倒回玩家背包。</summary>
        private void DoDeliver()
        {
            var playerBag = GetPlayerInventory();

            if (playerBag != null)
            {
                for (int i = 0; i < _bag.Capacity; i++)
                {
                    var stack = _bag.GetSlot(i);
                    if (stack == null || stack.IsEmpty) continue;

                    var def = stack.Def;
                    int amount = stack.Amount;
                    if (amount <= 0) continue;

                    int added = playerBag.Add(def, amount);
                    if (added > 0) _bag.Remove(def.id, added);
                }
            }

            _state = State.Idle;
            _idleTimer = idleScanInterval;
        }

        // ---------- 干活 ----------

        private void TickWorking()
        {
            if (_job == null)
            {
                _state = State.Idle;
                return;
            }

            if (IsJobObsolete())
            {
                FinishJob(false);
                return;
            }

            _job.workDone += Time.deltaTime;
            if (_job.WorkProgress < 1f) return;

            ExecuteJob();
        }

        private bool IsJobObsolete()
        {
            bool occupied = _world.Map.GetObjectAt(_job.target) != null;

            if (_job.type == Job.TypeBuild) return occupied;
            if (_job.type == Job.TypeMine) return !occupied;

            return true;
        }

        private void ExecuteJob()
        {
            bool success = false;

            if (_job.type == Job.TypeBuild)
            {
                // 材料从随身背包扣，不从玩家仓库扣
                var check = _world.TryPlace(_job.buildDef, _job.target, _bag);
                success = check.ok;
            }
            else if (_job.type == Job.TypeMine)
            {
                // 挖出来的东西进随身背包，不进玩家仓库
                success = _world.TryRemoveAt(_job.target, _bag);
            }

            // 干完活才扣体力，失败不扣
            if (success && Vitals != null)
            {
                int cost = _job.type == Job.TypeBuild ? buildStaminaCost : mineStaminaCost;
                Vitals.DrainStamina(cost);
                _staminaAccumulator = 0f;
            }

            FinishJob(success);
        }

        private void FinishJob(bool success)
        {
            if (_job != null)
            {
                if (success) _world.Jobs.Complete(_job.id);
                else _world.Jobs.Fail(_job.id);

                if (GhostBlockManager.Instance != null) GhostBlockManager.Instance.Hide(_job.id);
            }

            _job = null;
            _path = null;
            _pathIndex = 0;
            _state = State.Idle;
            _idleTimer = idleScanInterval;
        }

        private void FailJob(Job job)
        {
            if (job == null)
            {
                _state = State.Idle;
                return;
            }

            _world.Jobs.Fail(job.id);
            if (GhostBlockManager.Instance != null) GhostBlockManager.Instance.Hide(job.id);

            _job = null;
            _path = null;
            _pathIndex = 0;
            _state = State.Idle;
            _idleTimer = idleScanInterval;
        }

        // ---------- 小工具 ----------

        private int BagTotal()
        {
            int total = 0;

            for (int i = 0; i < _bag.Capacity; i++)
            {
                var stack = _bag.GetSlot(i);
                if (stack != null && !stack.IsEmpty) total += stack.Amount;
            }

            return total;
        }

        /// <summary>背包还能不能装下这种物品（有空位，或同种没满）。</summary>
        private bool BagCanAccept(ItemDef def, int count)
        {
            if (def == null) return false;

            for (int i = 0; i < _bag.Capacity; i++)
            {
                var stack = _bag.GetSlot(i);
                if (stack == null || stack.IsEmpty) return true;
                if (stack.Def.id == def.id && !stack.IsFull) return true;
            }

            return false;
        }

        /// <summary>玩家背包在 PlayerInventory 组件上，不在 PlayerController 上。</summary>
        private Inventory GetPlayerInventory()
        {
            var holder = PlayerInventory.Instance;
            return holder != null ? holder.Inventory : null;
        }

        private PlayerController ResolvePlayer()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            return _player;
        }

        private static ItemDef ResolveItem(string itemId)
        {
            var database = ItemDatabase.Instance;
            return database != null ? database.Get(itemId) : null;
        }
    }
}

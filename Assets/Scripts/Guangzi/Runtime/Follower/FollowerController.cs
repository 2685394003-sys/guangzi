using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 随从（follower）：领取任务、走过去、干活。
    ///
    /// 状态机只有三态：待命 → 移动 → 干活，失败就回到待命。
    /// 刻意不做「排队等待」「边走边想」这类中间态——单随从、任务量小，
    /// 加了只会多出难调的边界情况。
    ///
    /// 关键：随从**不受 InputModeService 影响**。ESC 菜单打开时玩家停手，
    /// 但随从该继续干活——它不是玩家输入的一部分。
    /// </summary>
    public class FollowerController : MonoBehaviour
    {
        private enum State
        {
            Idle = 0,
            Moving = 1,
            Working = 2,
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

        [Header("视图")]
        [SerializeField] private Transform visual;

        private GridWorld _world;
        private State _state = State.Idle;
        private Job _job;
        private List<GridPos> _path;
        private int _pathIndex;
        private float _idleTimer;

        public bool HasJob => _job != null;
        public string CurrentJobType => _job != null ? _job.type : string.Empty;
        public float CurrentProgress => _job != null ? _job.WorkProgress : 0f;

        private void Awake()
        {
            if (visual == null) visual = transform;
        }

        private void Update()
        {
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
            }
        }

        // ---------- 待命 ----------

        private void TickIdle()
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer > 0f) return;

            _idleTimer = idleScanInterval;

            Job job;
            if (!_world.Jobs.TryClaimNext(1, out job)) return;

            if (!BeginJob(job))
            {
                _world.Jobs.Fail(job.id);
                if (GhostBlockManager.Instance != null) GhostBlockManager.Instance.Hide(job.id);
            }
        }

        /// <summary>接单：算出「站哪」和「怎么走过去」。任何一步失败就放弃这条任务。</summary>
        private bool BeginJob(Job job)
        {
            var selfCell = _world.WorldToGrid(transform.position);

            GridPos workSpot;
            if (!PathFinder.TryFindWorkSpot(_world.Map, job.target, selfCell, out workSpot)) return false;

            var path = PathFinder.FindPath(_world.Map, selfCell, workSpot);
            if (path == null) return false;

            _job = job;
            _path = path;
            _pathIndex = 0;
            job.workDone = 0f;
            job.workRequired = job.type == Job.TypeBuild ? buildSeconds : mineSeconds;

            _state = State.Moving;
            return true;
        }

        // ---------- 移动 ----------

        private void TickMoving()
        {
            if (_job == null || _path == null || _pathIndex >= _path.Count)
            {
                _state = State.Working;
                if (_job != null) _job.workDone = 0f;
                return;
            }

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
                return;
            }

            Vector3 dir = delta / distance;
            transform.position = flatPos + dir * step;
            FaceDirection(dir);
        }

        private void FaceDirection(Vector3 dir)
        {
            if (visual == null) return;
            if (dir.sqrMagnitude < 0.0001f) return;

            var look = Quaternion.LookRotation(dir, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, look, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        // ---------- 干活 ----------

        private void TickWorking()
        {
            if (_job == null)
            {
                _state = State.Idle;
                return;
            }

            // 目标格已经被别人动过了（玩家亲手建/拆），这条任务就没意义了
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
                var result = _world.TryPlace(_job.buildDef, _job.target);
                success = result.ok;
            }
            else if (_job.type == Job.TypeMine)
            {
                success = _world.TryRemoveAt(_job.target);
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
    }
}

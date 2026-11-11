using System;
using System.Collections.Generic;

namespace Guangzi.Core
{
    /// <summary>
    /// 任务状态。首版只有一条线：Pending → Claimed → Done / Failed。
    /// 没有「排队中」「等待材料」这类中间态——等真正需要了再加，别提前设计。
    /// </summary>
    public enum JobState
    {
        Pending = 0,
        Claimed = 1,
        Done = 2,
        Failed = 3,
    }

    /// <summary>一条待办任务。</summary>
    public sealed class Job
    {
        /// <summary>建造任务：把 buildDef 放进 target 格。</summary>
        public const string TypeBuild = "build";

        /// <summary>挖掘任务：把 target 格的方块挖掉。</summary>
        public const string TypeMine = "mine";

        public int id;
        public string type;
        public GridPos target;
        public int priority;
        public JobState state;
        public int assignedWorkerId;

        /// <summary>build 任务要放的东西；mine 任务为 null。</summary>
        public BuildDef buildDef;

        /// <summary>要干多少秒（由方块硬度和工具速度算出来）。</summary>
        public float workRequired;

        /// <summary>已经干了多少秒。</summary>
        public float workDone;

        public Job(int id, string type, GridPos target, int priority, BuildDef buildDef = null, float workRequired = 0f)
        {
            this.id = id;
            this.type = type;
            this.target = target;
            this.priority = priority;
            this.buildDef = buildDef;
            this.workRequired = workRequired < 0f ? 0f : workRequired;
            state = JobState.Pending;
            assignedWorkerId = 0;
        }

        /// <summary>0 ~ 1 的完成度，给进度显示用。</summary>
        public float WorkProgress
        {
            get
            {
                if (workRequired <= 0f) return 1f;

                float p = workDone / workRequired;
                return p < 0f ? 0f : (p > 1f ? 1f : p);
            }
        }

        public override string ToString() => "#" + id + " " + type + " @" + target;
    }

    /// <summary>
    /// 任务队列（job queue）。**首版就是线性轮询**：每次取 Pending 里 priority 最小的。
    /// 角色规模 &lt; 20、任务量小，堆和 DOTS 都只会增加复杂度，不增加帧率。
    /// 真到瓶颈再换，接口不变。
    /// </summary>
    public sealed class JobQueue
    {
        private readonly List<Job> _jobs = new List<Job>();
        private int _nextId = 1;

        public IReadOnlyList<Job> All => _jobs;

        public int PendingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _jobs.Count; i++)
                {
                    if (_jobs[i].state == JobState.Pending) count++;
                }
                return count;
            }
        }

        public Job Enqueue(string type, GridPos target, int priority = 100, BuildDef buildDef = null, float workRequired = 0f)
        {
            var job = new Job(_nextId++, type, target, priority, buildDef, workRequired);
            _jobs.Add(job);
            return job;
        }

        /// <summary>这条格子是不是已经有待办任务了（避免重复下单）。</summary>
        public bool HasActiveJobAt(GridPos target)
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                var job = _jobs[i];
                if (job.state == JobState.Done || job.state == JobState.Failed) continue;
                if (job.target.Equals(target)) return true;
            }
            return false;
        }

        /// <summary>取消一条还没被领取的任务（玩家反悔时用）。</summary>
        public bool Cancel(int jobId)
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                var job = _jobs[i];
                if (job.id != jobId) continue;
                if (job.state != JobState.Pending) return false;

                job.state = JobState.Failed;
                return true;
            }
            return false;
        }

        /// <summary>轮询领取：线性扫描所有 Pending，取 priority 最小（同优先级取先入队）的一条。</summary>
        public bool TryClaimNext(int workerId, out Job job)
        {
            job = null;
            for (int i = 0; i < _jobs.Count; i++)
            {
                var candidate = _jobs[i];
                if (candidate.state != JobState.Pending) continue;
                if (job == null || candidate.priority < job.priority) job = candidate;
            }

            if (job == null) return false;

            job.state = JobState.Claimed;
            job.assignedWorkerId = workerId;
            return true;
        }

        public bool Complete(int jobId) => SetState(jobId, JobState.Done);

        public bool Fail(int jobId) => SetState(jobId, JobState.Failed);

        private bool SetState(int jobId, JobState state)
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                if (_jobs[i].id != jobId) continue;
                _jobs[i].state = state;
                return true;
            }
            return false;
        }

        /// <summary>清掉已完成/已失败的任务，避免列表无限增长。轮询前调用即可。</summary>
        public int PruneFinished()
        {
            int removed = _jobs.RemoveAll(j => j.state == JobState.Done || j.state == JobState.Failed);
            return removed;
        }
    }
}

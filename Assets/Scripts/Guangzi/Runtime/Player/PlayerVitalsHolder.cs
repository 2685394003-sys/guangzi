using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 玩家数值持有者。挂在 Player 上，把 Core 的 PlayerVitals 接进 Unity 生命周期。
    ///
    /// 体力恢复做成「停止消耗后等一会儿再回」而不是持续回血，
    /// 否则边挖边回、体力条一直满格，玩家感觉不到消耗。
    /// </summary>
    public class PlayerVitalsHolder : MonoBehaviour
    {
        [Header("初始数值")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int maxStamina = 100;
        [SerializeField] private int startGold = 0;

        [Header("体力恢复")]
        [Tooltip("每秒恢复多少体力。0 = 不恢复")]
        [SerializeField] private float staminaRegenPerSecond = 2.5f;

        [Tooltip("停止消耗后多久开始恢复（秒），避免刚挖完立刻回满")]
        [SerializeField] private float regenDelay = 1.2f;

        public PlayerVitals Vitals { get; private set; }

        public static PlayerVitalsHolder Instance { get; private set; }

        private float _sinceLastSpend;
        private float _regenAccumulator;

        private void Awake()
        {
            Instance = this;
            Vitals = new PlayerVitals(maxHealth, maxStamina, startGold);
            _sinceLastSpend = regenDelay;
        }

        private void Update()
        {
            if (!InputModeService.IsWorld) return;
            if (Vitals == null) return;
            if (staminaRegenPerSecond <= 0f) return;
            if (Vitals.Stamina >= Vitals.MaxStamina) return;

            _sinceLastSpend += Time.deltaTime;
            if (_sinceLastSpend < regenDelay) return;

            // 累成整数再回，避免每帧调用 RestoreStamina 反复触发事件
            _regenAccumulator += staminaRegenPerSecond * Time.deltaTime;
            if (_regenAccumulator < 1f) return;

            int whole = Mathf.FloorToInt(_regenAccumulator);
            _regenAccumulator -= whole;
            Vitals.RestoreStamina(whole);
        }

        /// <summary>扣体力并重置恢复计时，返回实际扣掉的量。</summary>
        public int SpendStamina(int amount)
        {
            if (Vitals == null) return 0;

            _sinceLastSpend = 0f;
            return Vitals.DrainStamina(amount);
        }
    }
}

using System;

namespace Guangzi.Core
{
    /// <summary>
    /// 玩家的三条基础数值：血量、体力、金币。纯数据 + 变更事件，不含任何表现。
    ///
    /// 为什么单独抽出来：HUD 只是它的一个观察者。以后存档、AI、事件系统都要读它，
    /// 数值绝不能挂在 UI 组件上——那样一换界面就得重写一遍规则。
    /// </summary>
    public sealed class PlayerVitals
    {
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public int MaxStamina { get; }
        public int Stamina { get; private set; }
        public int Gold { get; private set; }

        /// <summary>任意一条数值变化时触发。HUD 订阅它刷新，不轮询。</summary>
        public event Action Changed;

        public float HealthPercent => MaxHealth > 0 ? (float)Health / MaxHealth : 0f;

        public float StaminaPercent => MaxStamina > 0 ? (float)Stamina / MaxStamina : 0f;

        public bool IsDead => Health <= 0;

        public PlayerVitals(int maxHealth, int maxStamina, int startGold = 0)
        {
            MaxHealth = maxHealth < 1 ? 1 : maxHealth;
            MaxStamina = maxStamina < 1 ? 1 : maxStamina;
            Health = MaxHealth;
            Stamina = MaxStamina;
            Gold = startGold < 0 ? 0 : startGold;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return;

            Health = Math.Max(0, Health - amount);
            RaiseChanged();
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;

            int before = Health;
            Health = Math.Min(MaxHealth, Health + amount);

            if (Health != before) RaiseChanged();
        }

        /// <summary>扣体力。不够就一点都不扣，返回 false——让调用方决定是提示还是阻止动作。</summary>
        public bool TrySpendStamina(int amount)
        {
            if (amount <= 0) return true;
            if (Stamina < amount) return false;

            Stamina -= amount;
            RaiseChanged();
            return true;
        }

        /// <summary>能扣多少扣多少，返回实际扣掉的量。用于「体力见底也能勉强干活」的场合。</summary>
        public int DrainStamina(int amount)
        {
            if (amount <= 0) return 0;

            int drained = Math.Min(amount, Stamina);
            if (drained <= 0) return 0;

            Stamina -= drained;
            RaiseChanged();
            return drained;
        }

        public void RestoreStamina(int amount)
        {
            if (amount <= 0) return;

            int before = Stamina;
            Stamina = Math.Min(MaxStamina, Stamina + amount);

            if (Stamina != before) RaiseChanged();
        }

        public void AddGold(int amount)
        {
            if (amount == 0) return;

            Gold = Math.Max(0, Gold + amount);
            RaiseChanged();
        }

        public bool TrySpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (Gold < amount) return false;

            Gold -= amount;
            RaiseChanged();
            return true;
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}

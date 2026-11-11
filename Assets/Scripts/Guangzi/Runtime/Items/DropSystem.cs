using System.Collections.Generic;
using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 掉落物管理（drop system）：登记场上所有掉落物，让玩家能自动捡起冷却结束的东西。
    ///
    /// 玩家站得够近就自动进包——不做「按键捡」，因为掉落物通常是操作失手掉出来的，
    /// 还要再按一次键去捡很烦。冷却没到的自动跳过，不会把手滑丢的东西立刻收回去。
    /// </summary>
    public class DropSystem : MonoBehaviour
    {
        [Tooltip("玩家离掉落物多近会自动捡（格）")]
        [SerializeField] private float pickupRadius = 1.2f;

        [Tooltip("多久扫一次地上的掉落物（秒）")]
        [SerializeField] private float scanInterval = 0.25f;

        private readonly List<DroppedItem> _drops = new List<DroppedItem>();
        private GridWorld _world;
        private PlayerController _player;
        private float _scanTimer;

        public static DropSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(DroppedItem drop)
        {
            if (drop != null && !_drops.Contains(drop)) _drops.Add(drop);
        }

        private void Update()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = scanInterval;

            if (_world == null) _world = GridWorld.Instance;
            if (_world == null) return;

            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player == null) return;

            var playerBag = PlayerInventory.Instance;
            if (playerBag == null || playerBag.Inventory == null) return;

            Vector3 playerPos = _player.transform.position;

            // 倒序删，避免边遍历边移除
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                var drop = _drops[i];

                if (drop == null)
                {
                    _drops.RemoveAt(i);
                    continue;
                }

                // 丢弃者 3 秒冷却，没到就跳过
                if (!drop.CanOwnerPickup) continue;

                float distance = Vector2.Distance(
                    new Vector2(playerPos.x, playerPos.z),
                    new Vector2(drop.transform.position.x, drop.transform.position.z));

                if (distance > pickupRadius) continue;

                var def = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(drop.ItemId) : null;
                if (def == null) continue;

                int added = playerBag.Inventory.Add(def, drop.Amount);
                if (added <= 0) continue;

                _drops.RemoveAt(i);

                // 没装下的部分留着（背包满时只能捡走一部分）
                if (added < drop.Amount)
                {
                    drop.TrimAmount(added);
                    continue;
                }

                drop.Consume(out _, out _);
            }
        }

        /// <summary>拖到地图上时调用：在指定世界坐标生成一个掉落物。</summary>
        public DroppedItem DropAt(ItemDef def, int count, Vector3 worldPosition)
        {
            var drop = DroppedItem.Spawn(def, count, worldPosition);
            Register(drop);
            return drop;
        }
    }
}

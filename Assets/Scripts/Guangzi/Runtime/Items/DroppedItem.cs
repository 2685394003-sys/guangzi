using Guangzi.Core;
using UnityEngine;

namespace Guangzi.Runtime
{
    /// <summary>
    /// 掉在地上的物品（dropped item）。
    ///
    /// 冷却规则来自设计：
    /// - 丢弃者本人 **3 秒**内不能捡回来（防手滑，也防「丢出去立刻捡」刷东西）
    /// - 其他人 / NPC **1 秒**后就能捡
    ///
    /// 两个时间是分开的，不能合成一个：丢弃者等得最久，别人等得短。
    /// </summary>
    public class DroppedItem : MonoBehaviour
    {
        [SerializeField] private string itemId;
        [SerializeField] private int amount = 1;
        [SerializeField] private float ownerLockSeconds = 3f;
        [SerializeField] private float otherPickupSeconds = 1f;

        private float _spawnTime;
        private float _spinSpeed = 90f;

        public string ItemId => itemId;
        public int Amount => amount;

        /// <summary>丢弃者（玩家）现在能不能捡。</summary>
        public bool CanOwnerPickup => Time.time >= _spawnTime + ownerLockSeconds;

        /// <summary>其他人 / NPC 现在能不能捡。</summary>
        public bool CanOtherPickup => Time.time >= _spawnTime + otherPickupSeconds;

        /// <summary>冷却还剩多久（给 UI 提示用）。</summary>
        public float OwnerLockRemaining => Mathf.Max(0f, _spawnTime + ownerLockSeconds - Time.time);

        public static DroppedItem Spawn(ItemDef def, int count, Vector3 worldPosition)
        {
            if (def == null || count <= 0) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Drop_" + def.id;
            go.transform.position = worldPosition + Vector3.up * 0.25f;
            go.transform.localScale = Vector3.one * 0.4f;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Color color;
                if (ColorUtility.TryParseHtmlString(def.colorHex, out color))
                {
                    var mat = new Material(renderer.sharedMaterial);
                    mat.color = color;
                    renderer.sharedMaterial = mat;
                }
            }

            var drop = go.AddComponent<DroppedItem>();
            drop.itemId = def.id;
            drop.amount = count;
            drop._spawnTime = Time.time;

            return drop;
        }

        private void Update()
        {
            // 慢慢转，和地上的方块区分开
            transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
        }

        /// <summary>
        /// 被部分捡走：背包只装得下一部分时用它把剩余量改小。
        /// amount 是**已经捡走**的数量。
        /// </summary>
        public void TrimAmount(int taken)
        {
            if (taken <= 0) return;

            amount -= taken;
            if (amount <= 0) Destroy(gameObject);
        }

        /// <summary>被捡走时调用。返回物品 id 和数量，并销毁自己。</summary>
        public void Consume(out string id, out int count)
        {
            id = itemId;
            count = amount;
            Destroy(gameObject);
        }
    }
}

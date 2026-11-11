using System.Collections.Generic;
using Guangzi.Core;
using NUnit.Framework;

namespace Guangzi.Tests
{
    /// <summary>
    /// Core 层单元测试。不依赖 Unity 场景、不需要进 Play 模式，改规则就靠这层兜底。
    /// 只测规则，不测渲染——渲染测试又慢又脆。
    /// </summary>
    public class GridMapTests
    {
        private static BuildDef Wall => BuildCatalog.Get(BuildCatalog.WoodWall);
        private static BuildDef Floor => BuildCatalog.Get(BuildCatalog.StoneFloor);

        [Test]
        public void NewMap_AllCellsPassable()
        {
            var map = new GridMap(8, 8);

            Assert.IsTrue(map.IsPassable(new GridPos(0, 0)));
            Assert.IsTrue(map.IsPassable(new GridPos(7, 7)));
            Assert.IsFalse(map.InBounds(new GridPos(8, 0)));
            Assert.IsFalse(map.IsPassable(new GridPos(-1, 0)));
        }

        [Test]
        public void PlaceWall_BlocksPassable_RemovalRestoresAndRefundsHalf()
        {
            var map = new GridMap(8, 8);
            var pos = new GridPos(3, 4);

            int id = map.PlaceObject(Wall, pos);
            Assert.AreNotEqual(0, id);
            Assert.IsFalse(map.IsPassable(pos));
            Assert.AreEqual(id, map.GetCell(pos).occupantId);

            bool removed = map.RemoveObject(id, out var removedObj, out var refund);
            Assert.IsTrue(removed);
            Assert.AreEqual(id, removedObj.id);
            Assert.IsTrue(map.IsPassable(pos));

            // 木墙造价 5 木材，返还 50% 向下取整 = 2
            Assert.AreEqual(1, refund.Count);
            Assert.AreEqual("wood", refund[0].resourceId);
            Assert.AreEqual(2, refund[0].amount);
        }

        [Test]
        public void PlaceOnOccupiedCell_ReturnsZero()
        {
            var map = new GridMap(8, 8);
            var pos = new GridPos(1, 1);

            Assert.AreNotEqual(0, map.PlaceObject(Wall, pos));
            Assert.AreEqual(0, map.PlaceObject(Wall, pos));
        }

        [Test]
        public void PlaceOutOfBounds_ReturnsZero()
        {
            var map = new GridMap(4, 4);

            Assert.AreEqual(0, map.PlaceObject(Wall, new GridPos(-1, 0)));
            Assert.AreEqual(0, map.PlaceObject(Wall, new GridPos(4, 0)));
            Assert.AreEqual(0, map.PlaceObject(Wall, new GridPos(0, 4)));
        }

        [Test]
        public void MultiCellFootprint_OccupiesEveryCell()
        {
            var map = new GridMap(8, 8);
            var big = new BuildDef("test_2x2", "测试 2x2", 2, 2, 100, 10, false, 50, 1f,
                new ResourceAmount("wood", 4));

            var origin = new GridPos(2, 2);
            int id = map.PlaceObject(big, origin);
            Assert.AreNotEqual(0, id);

            for (int dz = 0; dz < 2; dz++)
            {
                for (int dx = 0; dx < 2; dx++)
                {
                    Assert.AreEqual(id, map.GetCell(new GridPos(2 + dx, 2 + dz)).occupantId);
                }
            }

            Assert.IsTrue(map.IsPassable(new GridPos(4, 2)));
            Assert.AreEqual(0, map.PlaceObject(Wall, new GridPos(3, 3)));
        }

        [Test]
        public void FloorIsPassable_ButStillOccupiesCell()
        {
            var map = new GridMap(8, 8);
            var pos = new GridPos(5, 5);

            Assert.AreNotEqual(0, map.PlaceObject(Floor, pos));
            Assert.IsTrue(map.IsPassable(pos));
            Assert.IsFalse(map.IsEmpty(pos));
        }

        [Test]
        public void Validator_ReportsOccupiedWithBlockingCell()
        {
            var map = new GridMap(8, 8);
            var pos = new GridPos(2, 2);
            map.PlaceObject(Wall, pos);

            var check = BuildValidator.Check(map, Wall, pos);

            Assert.IsFalse(check.ok);
            Assert.AreEqual(BuildInvalidReason.Occupied, check.reason);
            Assert.AreEqual(pos, check.blockingCell);
        }

        [Test]
        public void Validator_WaterBlocksBuilding()
        {
            var map = new GridMap(8, 8);
            var pos = new GridPos(1, 1);
            map.SetTerrain(pos, CellTerrain.Water);

            var check = BuildValidator.Check(map, Wall, pos);

            Assert.IsFalse(check.ok);
            Assert.AreEqual(BuildInvalidReason.TerrainBlocked, check.reason);
        }

        [Test]
        public void LoadObject_KeepsOriginalId_AndAdvancesCounter()
        {
            var map = new GridMap(8, 8);

            Assert.IsTrue(map.LoadObject(42, Wall, new GridPos(1, 1), 33));
            Assert.AreEqual(42, map.GetCell(new GridPos(1, 1)).occupantId);
            Assert.AreEqual(33, map.Objects[42].hp);

            // 读档后新放置的 id 必须大于 42，不能撞车
            int newId = map.PlaceObject(Wall, new GridPos(2, 2));
            Assert.Greater(newId, 42);
        }

        [Test]
        public void Ledger_RequiresFullCost()
        {
            var ledger = new ResourceLedger(
                new ResourceAmount("wood", 4),
                new ResourceAmount("stone", 10));

            var cheap = new BuildDef("t1", "t1", 1, 1, 1, 1, false, 50, 1f, new ResourceAmount("wood", 4));
            var pricey = new BuildDef("t2", "t2", 1, 1, 1, 1, false, 50, 1f, new ResourceAmount("wood", 5));

            Assert.IsTrue(ledger.CanAfford(cheap.cost));
            Assert.IsFalse(ledger.CanAfford(pricey.cost));

            Assert.IsTrue(ledger.TryConsumeAll(cheap.cost));
            Assert.AreEqual(0, ledger.GetAmount("wood"));
            Assert.IsFalse(ledger.TryConsumeAll(cheap.cost));

            ledger.AddAll(cheap.Refund());
            Assert.AreEqual(2, ledger.GetAmount("wood"));
        }

        [Test]
        public void Ledger_SumsDuplicateResourceEntries()
        {
            var ledger = new ResourceLedger(new ResourceAmount("wood", 5));
            var cost = new List<ResourceAmount>
            {
                new ResourceAmount("wood", 3),
                new ResourceAmount("wood", 3),
            };

            // 合计需要 6，只有 5 → 负担不起；如果按单条比较会误判为可以
            Assert.IsFalse(ledger.CanAfford(cost));
        }

        [Test]
        public void JobQueue_ClaimsLowestPriorityFirst_AndPrunes()
        {
            var queue = new JobQueue();
            queue.Enqueue("Haul", new GridPos(1, 1), 100);
            var urgent = queue.Enqueue("Construct", new GridPos(2, 2), 10);
            queue.Enqueue("Clean", new GridPos(3, 3), 200);

            Assert.IsTrue(queue.TryClaimNext(7, out var claimed));
            Assert.AreEqual(urgent.id, claimed.id);
            Assert.AreEqual(7, claimed.assignedWorkerId);
            Assert.AreEqual(JobState.Claimed, claimed.state);

            Assert.IsTrue(queue.Complete(claimed.id));
            Assert.AreEqual(1, queue.PruneFinished());
            Assert.AreEqual(2, queue.PendingCount);
        }
    }
}

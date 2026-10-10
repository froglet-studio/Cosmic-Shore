using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-vessel anti-spam table the vessel debuff and overtake effects share. Pins the two
    /// halves of its contract: the cooldown (a second application inside the window is refused, one
    /// after it is admitted) and the prune (a destroyed vessel's entry leaves the table the next time
    /// a new vessel is seen, so a static table cannot grow by dead vessels across matches).
    /// </summary>
    public class VesselEffectCooldownsTests
    {
        readonly System.Collections.Generic.List<GameObject> _objects = new();

        ResourceSystem NewVessel(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<ResourceSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) if (go) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void SecondApplicationInsideTheWindowIsRefused_AfterItIsAdmitted()
        {
            var table = new VesselEffectCooldowns();
            var a = NewVessel("a");

            Assert.IsTrue(table.TryBegin(a, now: 10f, cooldown: 1f), "first sight is admitted");
            Assert.IsFalse(table.TryBegin(a, now: 10.5f, cooldown: 1f), "inside the window");
            Assert.IsTrue(table.TryBegin(a, now: 11.0f, cooldown: 1f), "the window has elapsed");
            Assert.AreEqual(1, table.Count);
        }

        [Test]
        public void VesselsAreIndependent()
        {
            var table = new VesselEffectCooldowns();
            var a = NewVessel("a");
            var b = NewVessel("b");

            Assert.IsTrue(table.TryBegin(a, 10f, 1f));
            Assert.IsTrue(table.TryBegin(b, 10f, 1f), "b's first sight is not gated by a's cooldown");
            Assert.AreEqual(2, table.Count);
        }

        [Test]
        public void DestroyedVesselsLeaveTheTable_WhenANewVesselIsSeen()
        {
            var table = new VesselEffectCooldowns();
            var dead1 = NewVessel("dead1");
            var dead2 = NewVessel("dead2");
            var live = NewVessel("live");
            Assert.IsTrue(table.TryBegin(dead1, 1f, 1f));
            Assert.IsTrue(table.TryBegin(dead2, 1f, 1f));
            Assert.IsTrue(table.TryBegin(live, 1f, 1f));
            Assert.AreEqual(3, table.Count);

            Object.DestroyImmediate(dead1.gameObject);
            Object.DestroyImmediate(dead2.gameObject);
            Assert.AreEqual(3, table.Count, "a known vessel's repeat does not prune");
            Assert.IsTrue(table.TryBegin(live, 5f, 1f));
            Assert.AreEqual(3, table.Count);

            var next = NewVessel("next");
            Assert.IsTrue(table.TryBegin(next, 6f, 1f), "a vessel seen for the first time is admitted");
            Assert.AreEqual(2, table.Count, "the two destroyed vessels were pruned; live and next remain");
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var table = new VesselEffectCooldowns();
            var a = NewVessel("a");
            table.TryBegin(a, 10f, 1f);
            table.Clear();
            Assert.AreEqual(0, table.Count);
            Assert.IsTrue(table.TryBegin(a, 10f, 1f), "no cooldown survives a Clear");
        }
    }
}

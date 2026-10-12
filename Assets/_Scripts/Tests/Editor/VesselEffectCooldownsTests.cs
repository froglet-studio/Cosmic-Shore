using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-object anti-spam table the vessel debuff and overtake effects share. Pins the two
    /// halves of its contract: the cooldown (a second application inside the window is refused, one
    /// after it is admitted) and the prune (a destroyed key's entry leaves the table the next time
    /// a new key is seen, so a static table cannot grow by dead vessels across matches).
    ///
    /// Exercised through the generic <see cref="ObjectCooldowns{TKey}"/> with plain GameObject keys.
    /// <see cref="VesselEffectCooldowns"/> is an empty subclass keyed by <see cref="ResourceSystem"/>,
    /// and a ResourceSystem cannot be made on a bare GameObject in the Editor: its
    /// [RequireComponent(typeof(IVesselStatus))] names an interface Unity cannot add, so AddComponent
    /// logs an error and returns null (.claude/skills/vessel/references/CONTRACT.md §1). Every line of
    /// the table's logic is in the generic, so a GameObject key tests exactly the code the vessel
    /// effects run, and Unity's destroyed-object null (the prune's test) is the same for any Object.
    /// </summary>
    public class VesselEffectCooldownsTests
    {
        readonly System.Collections.Generic.List<GameObject> _objects = new();

        GameObject NewKey(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) if (go) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void VesselTableIsTheGenericKeyedByResourceSystem()
        {
            Assert.IsTrue(typeof(ObjectCooldowns<ResourceSystem>).IsAssignableFrom(typeof(VesselEffectCooldowns)),
                "the vessel effects' table must stay the generic these tests exercise");
        }

        [Test]
        public void SecondApplicationInsideTheWindowIsRefused_AfterItIsAdmitted()
        {
            var table = new ObjectCooldowns<GameObject>();
            var a = NewKey("a");

            Assert.IsTrue(table.TryBegin(a, now: 10f, cooldown: 1f), "first sight is admitted");
            Assert.IsFalse(table.TryBegin(a, now: 10.5f, cooldown: 1f), "inside the window");
            Assert.IsTrue(table.TryBegin(a, now: 11.0f, cooldown: 1f), "the window has elapsed");
            Assert.AreEqual(1, table.Count);
        }

        [Test]
        public void KeysAreIndependent()
        {
            var table = new ObjectCooldowns<GameObject>();
            var a = NewKey("a");
            var b = NewKey("b");

            Assert.IsTrue(table.TryBegin(a, 10f, 1f));
            Assert.IsTrue(table.TryBegin(b, 10f, 1f), "b's first sight is not gated by a's cooldown");
            Assert.AreEqual(2, table.Count);
        }

        [Test]
        public void DestroyedKeysLeaveTheTable_WhenANewKeyIsSeen()
        {
            var table = new ObjectCooldowns<GameObject>();
            var dead1 = NewKey("dead1");
            var dead2 = NewKey("dead2");
            var live = NewKey("live");
            Assert.IsTrue(table.TryBegin(dead1, 1f, 1f));
            Assert.IsTrue(table.TryBegin(dead2, 1f, 1f));
            Assert.IsTrue(table.TryBegin(live, 1f, 1f));
            Assert.AreEqual(3, table.Count);

            Object.DestroyImmediate(dead1);
            Object.DestroyImmediate(dead2);
            Assert.AreEqual(3, table.Count, "destroying a key does not prune by itself");
            Assert.IsTrue(table.TryBegin(live, 5f, 1f));
            Assert.AreEqual(3, table.Count, "a known key's repeat does not prune");

            var next = NewKey("next");
            Assert.IsTrue(table.TryBegin(next, 6f, 1f), "a key seen for the first time is admitted");
            Assert.AreEqual(2, table.Count, "the two destroyed keys were pruned; live and next remain");
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var table = new ObjectCooldowns<GameObject>();
            var a = NewKey("a");
            table.TryBegin(a, 10f, 1f);
            table.Clear();
            Assert.AreEqual(0, table.Count);
            Assert.IsTrue(table.TryBegin(a, 10f, 1f), "no cooldown survives a Clear");
        }
    }
}

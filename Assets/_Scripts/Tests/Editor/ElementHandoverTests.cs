using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A hull's element levels travel with it when it changes machines (<see cref="ElementHandover"/>, 2026-10-27): the
    /// rules that decide WHEN the new owner adopts the published base levels and WHEN the owner republishes them.
    /// </summary>
    public class ElementHandoverTests
    {
        [Test]
        public void GainingAHullAfterItSpawned_IsAHandover()
        {
            Assert.IsTrue(ElementHandover.IsHandover(spawned: true, wasOwner: false));
        }

        [Test]
        public void OwningAHullFromItsSpawn_IsNotAHandover_SoItsSeededLevelsStand()
        {
            Assert.IsFalse(ElementHandover.IsHandover(spawned: false, wasOwner: false), "ownership before the spawn is the seed");
            Assert.IsFalse(ElementHandover.IsHandover(spawned: true, wasOwner: true), "already this machine's");
        }

        [Test]
        public void TheFirstPublish_AlwaysGoesOut_EvenAtZero()
        {
            Assert.IsTrue(ElementHandover.ShouldPublish(Vector4.zero, Vector4.zero, everPublished: false));
        }

        [Test]
        public void APetalChange_IsPublished()
        {
            var published = ElementHandover.Pack(0.3f, 0.5f, 0f, 1f);
            Assert.IsTrue(ElementHandover.ShouldPublish(published, ElementHandover.Pack(0.4f, 0.5f, 0f, 1f), true), "Charge +1 petal");
            Assert.IsTrue(ElementHandover.ShouldPublish(published, ElementHandover.Pack(0.3f, 0.5f, 0f, 0.9f), true), "Time -1 petal");
        }

        [Test]
        public void DriftUnderAHundredthOfAPetal_IsNotPublished()
        {
            var published = ElementHandover.Pack(1.2f, 0.5f, -0.2f, 1f);
            Assert.IsFalse(ElementHandover.ShouldPublish(published, ElementHandover.Pack(1.2005f, 0.5f, -0.2f, 1f), true));
            Assert.IsTrue(ElementHandover.ShouldPublish(published, ElementHandover.Pack(1.2015f, 0.5f, -0.2f, 1f), true));
        }

        [Test]
        public void TheComponentsAreTheReplicatedNibblesOrder()
        {
            var v = ElementHandover.Pack(0.1f, 0.2f, 0.3f, 0.4f);
            Assert.AreEqual(0.1f, ElementHandover.Get(v, Element.Charge));
            Assert.AreEqual(0.2f, ElementHandover.Get(v, Element.Mass));
            Assert.AreEqual(0.3f, ElementHandover.Get(v, Element.Space));
            Assert.AreEqual(0.4f, ElementHandover.Get(v, Element.Time));
            Assert.AreEqual(0f, ElementHandover.Get(v, Element.Omni));
        }
    }
}

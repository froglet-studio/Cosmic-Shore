using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Serpent collects crystals (QA-P2-SERPENT-SKIMMER, fixed 2026-10-31 by
    /// Tools/Build/fix_serpent_crystal_pickup.py). An elemental crystal is taken only by an
    /// initialised skimmer's contact, and the Serpent's one skimmer had been an inactive shell
    /// with no impactor, so it flew through every crystal. These hold the wiring: a live trigger
    /// catcher in the far-field slot, and a container that hands each crystal on to the hull so the
    /// Serpent's own crystal chain (the seed-wall re-shield) still runs.
    /// </summary>
    public class SerpentCrystalPickupTests
    {
        const string PrefabPath = "Assets/_Prefabs/Spacevessels/Serpent.prefab";

        static Skimmer Catcher()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(root, PrefabPath);
            Assert.IsTrue(root.TryGetComponent(out VesselStatus status), "no VesselStatus on the Serpent");
            Assert.IsNotNull(status.FarFieldSkimmer, "the Serpent has no far-field skimmer");
            return status.FarFieldSkimmer;
        }

        [Test]
        public void Catcher_IsActiveWithAnEnabledTrigger()
        {
            var skimmer = Catcher();
            for (var t = skimmer.transform; t != null; t = t.parent)
                Assert.IsTrue(t.gameObject.activeSelf, $"'{t.name}' is inactive");
            Assert.IsTrue(skimmer.TryGetComponent(out SphereCollider sphere), "no trigger sphere");
            Assert.IsTrue(sphere.enabled && sphere.isTrigger, "the sphere is not an enabled trigger");
            Assert.IsTrue(skimmer.TryGetComponent(out Rigidbody body) && body.isKinematic, "no kinematic Rigidbody");
        }

        [Test]
        public void Catcher_HasAnImpactorTheCrystalCanResolve()
        {
            var skimmer = Catcher();
            Assert.IsTrue(skimmer.TryGetComponent(out SkimmerImpactor impactor), "no SkimmerImpactor");
            Assert.AreSame(skimmer, impactor.Skimmer);
            Assert.IsTrue(skimmer.TryGetComponent(out ImpactCollider collider), "no ImpactCollider");
            Assert.AreSame(impactor, collider.Impactor as SkimmerImpactor);
        }

        [Test]
        public void Catcher_HandsEveryCrystalToTheHull()
        {
            Assert.IsTrue(Catcher().TryGetComponent(out SkimmerImpactor impactor));
            var effects = impactor.EffectContainer ? impactor.EffectContainer.SkimmerCrystalEffects : null;
            Assert.IsNotNull(effects, "no effect container");
            bool found = false;
            foreach (var e in effects)
                if (e is SkimmerHandsCrystalToHullEffectSO hand)
                {
                    found = true;
                    Assert.IsTrue(hand.AlsoAppliesToOmniCrystals, "omni crystals must reach the hull too");
                }
            Assert.IsTrue(found, "the container does not hand crystals to the hull");
        }
    }
}

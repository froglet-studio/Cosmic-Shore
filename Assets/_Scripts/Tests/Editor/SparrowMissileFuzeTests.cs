#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The asset-side gate for the Sparrow's PROXIMITY FUZE and its WARHEAD blast — the two
    /// numbers that decide whether a missile goes off near a target and whether the resulting
    /// blast can reach what set it off.
    ///
    /// <para>Both are authored as multiples of the round's OWN hit radius, which is itself a
    /// function of MASS (the missile swells 14x-38x in flight). So neither number can be checked
    /// by reading it: what matters is the RELATIONSHIP between them, and that is what these
    /// tests pin. Absolute figures are asserted only where a real physical coupling exists — the
    /// blast prefab's collider radius, which <c>ProjectileDetonatorSO</c> assumes when it doubles
    /// a radius into a <c>MaxScale</c> diameter.</para>
    ///
    /// <para>Lives under an Editor/ folder per CLAUDE.md — a test anywhere else compiles into
    /// the player and breaks the Windows build at the IL2CPP linker.</para>
    /// </summary>
    public class SparrowMissileFuzeTests
    {
        const string MissilePrefab = "Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab";
        const string WarheadPrefab = "Assets/_Prefabs/Projectile/AOEMissileWarhead.prefab";
        const string SparrowVessel = "Assets/_Prefabs/Spacevessels/Sparrow.prefab";
        const string SparrowContainer =
            "Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/SparrowImpactorDataContainer.asset";

        // The missile's measured half-extents at growth 1, root-local (see SparrowRoundGrowthTests).
        static readonly Vector3 ModelExtents = new Vector3(0.019053f, 0.019053f, 0.082950f);
        const float RootScale = 10f;          // ProjectileScale on SkyBurstGunAction.asset

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"missing asset: {path}");
            return File.ReadAllText(path);
        }

        static float Field(string yaml, string key)
        {
            var m = Regex.Match(yaml, @"^\s*" + Regex.Escape(key) + @":\s*(-?[0-9.eE+]+)\s*$",
                                RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"'{key}' is not authored");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        static float FuzeMultiplier() => Field(Read(MissilePrefab), "proximityFuzeRadiusMultiplier");
        static float WarheadMultiplier() => Field(Read(MissilePrefab), "warheadBlastRadiusMultiplier");

        /// <summary>World hit radius at a given MASS level, from the shipped growth pair.</summary>
        static float HitRadiusWorld(int massLevel)
        {
            float growth = ElementalScaling.RoundGrowthFactorForLevel(massLevel, 20f, 32f);
            return Projectile.ModelHitRadius(ModelExtents, growth) * RootScale;
        }

        [Test]
        public void TheFuzeIsArmedOnTheShippedMissile()
        {
            // 0 is the platform default and means "no fuze" — every other round in the game.
            // The skyburst is the one that opts in, so a zero here is the feature switched off.
            Assert.That(FuzeMultiplier(), Is.GreaterThan(0f));
            Assert.That(WarheadMultiplier(), Is.GreaterThan(0f));
        }

        [Test]
        public void TheWarheadReachesAtLeastAsFarAsTheFuzeTripsIt()
        {
            // THE load-bearing relationship. The fuze detonates the round when something comes
            // within its radius; if the warhead were the smaller of the two, a proximity
            // detonation would routinely fail to touch the very thing that set it off, and the
            // whole mechanic would read as missiles going off for no reason. Asserted as an
            // ORDERING rather than as two numbers, so a retune of either stays free.
            Assert.That(WarheadMultiplier(), Is.GreaterThanOrEqualTo(FuzeMultiplier()),
                "the warhead blast must not be smaller than the fuze that triggers it");
        }

        [Test]
        public void BothRadiiScaleWithMassAndStayOrdered()
        {
            // Both are multiples of the same live measurement, so the ordering has to survive
            // the whole MASS band rather than holding only at the resting level.
            float fuze = FuzeMultiplier(), warhead = WarheadMultiplier();
            float previous = 0f;

            foreach (int level in new[] { -5, 0, 5, 10, 15 })
            {
                float r = HitRadiusWorld(level);
                Assert.That(r, Is.GreaterThan(previous), $"hit radius must grow with MASS (level {level})");
                previous = r;

                Assert.That(r * warhead, Is.GreaterThanOrEqualTo(r * fuze), $"level {level}");
            }
        }

        [Test]
        public void TheFuzeIsBarelyArmedAtLaunchAndFullSizeOnceTheMissileHasGrown()
        {
            // The arming delay nobody authored: the fuze is a multiple of the round's CURRENT
            // size, and the round leaves the bay at 1/20th of it. So a rocket cannot detonate on
            // something it brushes past while still clearing the hull, and it reaches full
            // sensitivity exactly when the model finishes swelling.
            float atLaunch = Projectile.ModelHitRadius(ModelExtents, 1f) * RootScale * FuzeMultiplier();
            float grown = HitRadiusWorld(0) * FuzeMultiplier();

            Assert.That(atLaunch, Is.LessThan(grown * 0.1f),
                "a round at launch size must be far less sensitive than a grown one");
        }

        [Test]
        public void TheWarheadPrefabIsWiredAndDoesNotTouchPrisms()
        {
            var missile = Read(MissilePrefab);
            Assert.IsTrue(Regex.IsMatch(missile, @"warheadBlast: \{fileID: \d+, guid: \w+"),
                "the missile must reference a warhead blast prefab");

            var warhead = Read(WarheadPrefab);

            // The blast aimed at LIVING things must leave the arena to the other explosion in the
            // same detonation. Note this is NOT the same as clearing Destructive: a
            // non-destructive blast still reaches every prism it engulfs and ARMOURS it.
            Assert.AreEqual(0f, Field(warhead, "affectsPrisms"),
                "the warhead must not run the prism pass");

            // Belt to that brace: the trigger itself excludes TrailBlocks (layer 11 -> bit 2048),
            // so even the Physics fallback path cannot reach a prism.
            Assert.IsTrue(Regex.IsMatch(warhead, @"m_ExcludeLayers:\s*\n\s*serializedVersion: 2\s*\n\s*m_Bits: 2048"),
                "the warhead trigger must exclude the TrailBlocks layer");
        }

        [Test]
        public void TheWarheadColliderMatchesTheRadiusTheDetonatorAssumes()
        {
            // ProjectileDetonatorSO sizes the warhead as MaxScale = radius * 2, which is only
            // correct while the prefab's own trigger is the unit sphere's 0.5. Change the
            // collider and the blast silently becomes the wrong size.
            var warhead = Read(WarheadPrefab);
            var m = Regex.Match(warhead, @"SphereCollider:.*?m_Radius: ([0-9.]+)", RegexOptions.Singleline);
            Assert.IsTrue(m.Success, "the warhead prefab must carry a SphereCollider");
            Assert.AreEqual(0.5f, float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1e-4f);
        }

        [Test]
        public void TheSparrowRearmsFromPrismsAndWardsFromCrystals()
        {
            var vessel = Read(SparrowVessel);

            // The rearm needs BOTH references: the channel it counts on and the weapon whose
            // ammo index it single-sources. Either unassigned and the missile tank never fills.
            Assert.IsTrue(Regex.IsMatch(vessel, @"onPrismDestroyed: \{fileID: \d+, guid: \w+"),
                "the rearm component must be wired to the prism-destroyed channel");
            Assert.IsTrue(Regex.IsMatch(vessel, @"weaponAction: \{fileID: \d+, guid: \w+"),
                "the rearm component must name the weapon it refills");
            Assert.That(Field(vessel, "ammoPerPrism"), Is.GreaterThan(0f));

            // The crystal's job moved: it grants a ward, so the vessel needs one to grant.
            Assert.IsTrue(Regex.IsMatch(vessel, @"wardedSources: -?\d+"),
                "the Sparrow must carry a timed elemental ward for the crystal to grant");
        }

        [Test]
        public void TheOmniCrystalNoLongerRefillsTheMissileTank()
        {
            // The swap is the whole change: the crystal used to set the missile tank full, and
            // the effect that did it is gone. A container still holding a resource-change effect
            // here would leave both economies live at once.
            var container = Read(SparrowContainer);
            var crystalBlock = Regex.Match(container,
                @"vesselCrystalEffects:(.*?)\n  vessel", RegexOptions.Singleline);
            Assert.IsTrue(crystalBlock.Success, "the Sparrow container must author crystal effects");

            foreach (Match g in Regex.Matches(crystalBlock.Groups[1].Value, @"guid: (\w+)"))
            {
                string meta = $"Assets/_SO_Assets/Effects/Vessel Crystal Effects/" +
                              "SparrowVesselChangeResourceByCrystalEffect.asset.meta";
                Assert.IsFalse(File.Exists(meta),
                    "the retired crystal-refill effect must not be resurrected");
                Assert.IsNotNull(g.Groups[1].Value);
            }
        }

        [Test]
        public void OnlyHostileMassPaysForARearm()
        {
            // The rearm's gate is StatsManager's own environment-friendliness rule, so "which
            // mass is worth something to me" has one answer platform-wide. Without it a pilot
            // could park and reload off their own ribbon.
            Assert.IsTrue(StatsManager.IsFriendlyEnvironmentPrism(Domains.Jade, Domains.Jade),
                "your own domain's mass is friendly");
            Assert.IsFalse(StatsManager.IsFriendlyEnvironmentPrism(Domains.Jade, Domains.Ruby),
                "another domain's mass is hostile");
            Assert.IsFalse(StatsManager.IsFriendlyEnvironmentPrism(Domains.Jade, Domains.Blue),
                "neutral mass is hostile to everyone — Blue is the no-team sentinel");
        }
    }
}
#endif

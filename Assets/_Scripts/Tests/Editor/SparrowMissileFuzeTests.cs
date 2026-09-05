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
            //
            // NOTE this is REACH, not capture - see TheWarheadExpandsFastEnoughToCatchOrdinaryFlight
            // for the half of the claim that this ordering does NOT establish.
            Assert.That(WarheadMultiplier(), Is.GreaterThanOrEqualTo(FuzeMultiplier()),
                "the warhead blast must not be smaller than the fuze that triggers it");
        }

        [Test]
        public void TheWarheadExpandsFastEnoughToCatchOrdinaryFlight()
        {
            // REACH IS NOT CAPTURE, and the radius ordering above silently implies that it is.
            // The blast is not a sphere that exists at full size: AOEExplosion grows it as
            // radius(t) = R * sin(t/D * PI/2) over its ExplosionDuration D, and a vessel only
            // takes the debuff when the sphere CONTAINS it (OnTriggerEnter is an overlap, not a
            // surface crossing). So against a target that was already moving away when the fuze
            // tripped, the sphere has to close the margin (warhead - fuze) x hitRadius before it
            // finishes expanding. At the original 0.5 s that margin bought only ~40 u/s - below
            // every vessel's cruise - so a rocket could detonate beside a pilot and reach nobody
            // while this file's radius test passed.
            //
            // Asserted as a SPEED the geometry must cover, not as a duration, so the duration and
            // the two multipliers can each be retuned as long as the mechanic still works.
            const float MustCatchRecedingSpeed = 120f;   // ~ the skyburst's own flight speed

            float r = HitRadiusWorld(0);                       // resting MASS
            float tripDistance = r * FuzeMultiplier();
            float blastRadius = r * WarheadMultiplier();
            float duration = Field(Read(WarheadPrefab), "ExplosionDuration");

            Assert.That(duration, Is.GreaterThan(0f), "the warhead must author an ExplosionDuration");

            bool caught = false;
            const int Steps = 2000;
            for (int i = 0; i <= Steps && !caught; i++)
            {
                float t = duration * i / Steps;
                float radius = blastRadius * Mathf.Sin(t / duration * Mathf.PI * 0.5f);
                if (radius >= tripDistance + MustCatchRecedingSpeed * t) caught = true;
            }

            Assert.IsTrue(caught,
                $"the warhead expands too slowly: over {duration}s it never contains a target " +
                $"that was {tripDistance:F1}u away and receding at {MustCatchRecedingSpeed} u/s. " +
                "Shorten ExplosionDuration on the warhead prefab, or widen the warhead/fuze margin.");
        }

        [Test]
        public void TheWarheadNeverDebuffsItsOwnDomain()
        {
            // A 95-unit sphere centred at most a fuze-radius away puts the SHOOTER inside its own
            // blast at exactly the close range the fuze exists to encourage. The warhead's whole
            // payload is an elemental debuff on vessels, and there is no level at which a pilot
            // should debuff themselves or a wingman - domains ARE the sides in all three modes
            // this weapon flies in. The detonator must therefore NOT hand the warhead the CHARGE-5
            // 'Domain-Safe Skybursts' snapshot it hands the prism blasts: that flag is true BELOW
            // the upgrade, which is precisely when it would self-debuff.
            string detonator = Read(
                "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/ProjectileDetonatorSO.cs");

            int warheadAt = detonator.IndexOf("proj.WarheadBlast", System.StringComparison.Ordinal);
            Assert.That(warheadAt, Is.GreaterThan(0), "the detonator must still spawn the warhead");

            string warheadBlock = detonator.Substring(warheadAt);
            int selfAt = warheadBlock.IndexOf("AffectSelfOverride", System.StringComparison.Ordinal);
            Assert.That(selfAt, Is.GreaterThan(0), "the warhead spawn must set AffectSelfOverride");

            string line = warheadBlock.Substring(selfAt, Mathf.Min(80, warheadBlock.Length - selfAt));
            Assert.IsTrue(line.Contains("false"),
                "the warhead must pass AffectSelfOverride = false; passing the CHARGE-5 snapshot " +
                "(!proj.SpareOwnDomain) makes a Sparrow debuff itself on every close-range kill");
        }

        [Test]
        public void WildlifeIsQuarryWhateverColourItWears()
        {
            // The creature kill must NOT be gated on the blast's friendly-fire flag. Fauna spawn
            // in exactly ONE colour - the cell's controlling domain - so borrowing that flag let
            // an upgrade about PRISMS switch off wildlife kills entirely for a pilot who happened
            // to share the swarm's colour, in the one mode scored on LifeformsKilled. It also
            // disagreed with the mode's primary kill: shooting a creature's body prisms has
            // always worked regardless of colour.
            string effect = Read(
                "Assets/_SO_Assets/Effects/Explosion Crystal Effects/MissileWarheadWitherLifeformEffect.asset");

            Assert.AreEqual(0f, Field(effect, "sparesOwnDomain"),
                "the warhead must kill wildlife of any domain");

            string src = Read("Assets/_Scripts/Controller/ImpactEffects/EffectsSO/" +
                              "Explosion Crystal Effects/ExplosionWitherLifeformByCrystalEffectSO.cs");
            Assert.IsFalse(src.Contains("AffectsOwnDomain"),
                "the creature kill must own its friendly-fire decision, not read the blast's");
        }

        [Test]
        public void ACorpseIsNotATarget()
        {
            // IsEmbedded does NOT mean alive: a creature with a progressive wither re-homes its
            // heart onto the cell at the top of its death and leaves it embedded for the whole
            // animation, so a corpse's heart keeps matching for seconds. Jousting one re-runs the
            // sealed death - a second LifeformsKilled credit for one creature, and the heart
            // freed while the wither is still eating inward (Docs/ECOSYSTEM.md §26 requires it to
            // be the LAST thing standing). Three places have to agree, so all three are pinned.
            string predated = Read(
                "Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs");
            int at = predated.IndexOf("public virtual bool Predated(string predatorName, Transform",
                                      System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "Fauna.Predated must still exist");
            string body = predated.Substring(at, Mathf.Min(2400, predated.Length - at));
            Assert.IsTrue(body.Contains("_diedThisLife"),
                "Fauna.Predated must decline a creature that has already died - _consumedAsPrey " +
                "is only set by Predated itself, so a starvation or prism-loss death slips past it");

            string fuze = Read("Assets/_Scripts/Controller/Projectiles/Projectile.cs");
            Assert.IsTrue(fuze.Contains("fauna.IsDying"),
                "the proximity fuze must not arm on a corpse's heart");

            string sweepEffect = Read("Assets/_Scripts/Controller/ImpactEffects/EffectsSO/" +
                                      "Explosion Crystal Effects/ExplosionWitherLifeformByCrystalEffectSO.cs");
            Assert.IsTrue(sweepEffect.Contains("IsDying"),
                "the warhead's creature kill must decline a corpse");
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
        public void TheWarheadIsSilent()
        {
            // A single skyburst already spawns TWO authored blasts (the cone and the sphere), each
            // of which plays the shared Explosion one-shot. The warhead goes off at the same point
            // on the same frame, so leaving it audible makes three identical one-shots sum and
            // phase rather than read as a bigger explosion. If it should ever have a voice, the
            // house rule is its own EventReference shipped empty - not a third consumer of a
            // shared category.
            Assert.AreEqual(0f, Field(Read(WarheadPrefab), "playsDetonationSfx"),
                "the warhead must not add a third coincident Explosion one-shot");
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

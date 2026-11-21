#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The asset-side gate for the Sparrow's TWO missiles — the cheap BASE rocket fired on the
    /// wing and the HEAVY one fired from the turret stance.
    ///
    /// <para>They come out of one bay, one weapon asset, one projectile prefab and one pool; what
    /// separates them is a per-shot payload (<see cref="ProjectilePayload"/>) resolved at press
    /// time from <c>IVesselStatus.IsTranslationRestricted</c>. So there is no second prefab to
    /// read and the whole design lives in ONE asset's field pairs — which is exactly what makes
    /// it worth pinning: a single edit that switched <c>armWarhead</c> back on for the base shot
    /// would delete the distinction with nothing on screen to say so.</para>
    ///
    /// <para>The two RELATIONSHIPS the design is stated in ("half the energy", "twice as fast")
    /// are asserted as relationships rather than as four numbers, so a retune of the heavy
    /// rocket carries the base one with it instead of silently drifting apart.</para>
    ///
    /// <para>Lives under an Editor/ folder per CLAUDE.md — a test anywhere else compiles into
    /// the player and breaks the Windows build at the IL2CPP linker.</para>
    /// </summary>
    public class SparrowMissileVariantTests
    {
        const string Weapon = "Assets/_SO_Assets/VesselActions/Sparrow/SkyBurstGunAction.asset";
        const string MissilePrefab = "Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab";

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

        [Test]
        public void TheWeaponFiresTwoVariants()
        {
            // Without this flag the stationary block is inert and the Sparrow fires ONE rocket -
            // the base one, which carries no warhead at all. The whole feature hangs off it.
            Assert.That(Field(Read(Weapon), "hasStationaryVariant"), Is.EqualTo(1f));
        }

        [Test]
        public void TheBaseRocketCostsHalfTheHeavyOne()
        {
            var w = Read(Weapon);

            // "half the energy", asserted as the RATIO. The bay is a 0..1 tank, so this is also
            // the statement that it holds four cheap rockets or two heavy ones.
            Assert.That(Field(w, "ammoCost"),
                Is.EqualTo(Field(w, "stationaryAmmoCost") * 0.5f).Within(1e-4f));
        }

        [Test]
        public void TheHeavyRocketFliesTwiceAsFast()
        {
            var w = Read(Weapon);

            // "twice as fast", as the RATIO. It is not only a feel change: the heavy rocket is
            // fired from a STANDSTILL, so unlike the base one it inherits no vessel velocity -
            // doubling its own speed is what keeps it from being the slower round in the world
            // frame.
            Assert.That(Field(w, "stationarySpeed"),
                Is.EqualTo(Field(w, "speed") * 2f).Within(1e-4f));
        }

        [Test]
        public void OnlyTheHeavyRocketCarriesTheFuzeTheCairnAndTheTrail()
        {
            var w = Read(Weapon);

            // The base rocket has to be AIMED: no proximity fuze means no early detonation on a
            // pilot or a creature, and no warhead shockwave means the blast that debuffs and
            // jousts never spawns. It still detonates on contact and still blows its hole - the
            // DESTRUCTIVE half of the detonation is untouched by any of these three flags.
            Assert.That(Field(w, "armWarhead"), Is.EqualTo(0f));
            Assert.That(Field(w, "createMassOnDetonation"), Is.EqualTo(0f));
            Assert.That(Field(w, "layPrismTrail"), Is.EqualTo(0f));

            Assert.That(Field(w, "stationaryArmWarhead"), Is.EqualTo(1f));
            Assert.That(Field(w, "stationaryCreateMassOnDetonation"), Is.EqualTo(1f));
            Assert.That(Field(w, "stationaryLayPrismTrail"), Is.EqualTo(1f));
        }

        [Test]
        public void ThePrefabStillAuthorsTheFuzeAndWarheadTheHeavyRocketSwitchesOn()
        {
            // The payload flags are a per-SHOT gate; WHAT is carried and HOW BIG stay the
            // prefab's business. A zero here would disarm BOTH rockets, and the variant flags
            // would go on reading as though the heavy one were armed.
            var p = Read(MissilePrefab);
            Assert.That(Field(p, "proximityFuzeRadiusMultiplier"), Is.GreaterThan(0f));
            Assert.That(Field(p, "warheadBlastRadiusMultiplier"), Is.GreaterThan(0f));
        }

        [Test]
        public void ThePrismTrailIsWiredAndItsCapCoversTheHeavyRocketsRange()
        {
            var p = Read(MissilePrefab);
            var w = Read(Weapon);

            // Without the channel the trail is a no-op however the shot is authored - the one
            // reference that turns the feature off silently.
            Assert.IsTrue(Regex.IsMatch(p, @"prismTrailChannel: \{fileID: \d+, guid: \w+"),
                "the missile must be wired to the pooled-prism spawn channel");

            float spacing = Field(p, "prismTrailSpacing");
            int cap = (int)Field(p, "prismTrailMaxPrisms");
            Assert.That(spacing, Is.GreaterThan(0f));
            Assert.That(cap, Is.GreaterThan(0));

            // RANGE is not speed x lifetime: Projectile.MoveProjectileAsync advances by
            // Velocity * dt * cos(pi*t / 2T), which integrates to speed * 2T/pi (~64%). A cap
            // BELOW the range/spacing count would truncate the ribbon part-way down the flight,
            // which reads as the trail stopping for no reason.
            float range = Field(w, "stationarySpeed") * 2f * Field(w, "projectileTime") / Mathf.PI;
            Assert.That(cap, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(range / spacing)),
                "the cap must cover the heavy rocket's whole flight at the authored spacing");
        }

        [Test]
        public void ADefaultPayloadIsTheFleetsOldBehaviour()
        {
            // Every other gun in the game passes no payload, so this value is what they get -
            // the prefab's own authoring and no flight trail. A change here is a change to every
            // projectile in the project.
            var d = ProjectilePayload.Default;
            Assert.IsTrue(d.ArmWarhead);
            Assert.IsTrue(d.CreateMassOnDetonation);
            Assert.IsFalse(d.LayPrismTrail);
        }

        [Test]
        public void TheIconLadderCountsRocketsRatherThanTankFraction()
        {
            // The ladder has three rungs (0/1/2 rockets). At the OLD one-cost weapon it spread
            // the tank across them and ROUNDED, which drew TWO rockets on a 0.75 tank holding
            // one - wrong before this change and much wronger after it, since the bay now holds
            // four base rockets.
            Assert.That(SparrowHUDView.LadderState(0.75f, 0.5f, 3), Is.EqualTo(1));
            Assert.That(SparrowHUDView.LadderState(0.5f, 0.5f, 3), Is.EqualTo(1));
            Assert.That(SparrowHUDView.LadderState(0.49f, 0.5f, 3), Is.EqualTo(0));
            Assert.That(SparrowHUDView.LadderState(1f, 0.5f, 3), Is.EqualTo(2));

            // Four rockets, three rungs: it CLAMPS. That under-reports rather than lying about
            // which rocket is next, and closing it is an art task (five sprites), not a code one.
            Assert.That(SparrowHUDView.LadderState(1f, 0.25f, 3), Is.EqualTo(2));
            Assert.That(SparrowHUDView.LadderState(0.25f, 0.25f, 3), Is.EqualTo(1));
            Assert.That(SparrowHUDView.LadderState(0f, 0.25f, 3), Is.EqualTo(0));

            // Unknown cost degenerates to the tank fraction it used to show, rather than to zero.
            Assert.That(SparrowHUDView.LadderState(1f, 0f, 3), Is.EqualTo(2));
        }
    }
}
#endif

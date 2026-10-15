using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Shrike (<c>R_VesselActions/SHRIKE.md</c>): the pure rules ported from the Shrike Studio
    /// (<c>Docs/Studios/VesselStudio/shrike.html</c>), each held to golden numbers produced by running the page's OWN
    /// function in node on the same inputs (the page's (x, y, z) is Unity's (x, y, −z): three.js noses down −Z), and the
    /// hull form's shape. Pure: <c>Tools/Build/shrike_harness/run.sh</c> compiles and runs this file outside Unity. The
    /// config's elemental ranges are <see cref="ShrikeConfigTests"/> (they need a ScriptableObject).
    /// </summary>
    [TestFixture]
    public class ShrikeTests
    {
        const float Eps = 1e-3f;

        static void Near(Vector3 want, Vector3 have, string what)
        {
            Assert.AreEqual(want.x, have.x, Eps, what + ".x");
            Assert.AreEqual(want.y, have.y, Eps, what + ".y");
            Assert.AreEqual(want.z, have.z, Eps, what + ".z");
        }

        // ------------------------------------------------------------------ the nest (page buildNest)

        [Test]
        public void Nest_TwelveBlocks_MatchThePage()
        {
            Near(new Vector3(2.86667f, 0f, -8.53125f), ShrikeRules.NestLocal(0, 12, false), "nest0");
            Near(new Vector3(6.86633f, -4.36796f, -3.84375f), ShrikeRules.NestLocal(5, 12, false), "nest5");
            Near(new Vector3(2.64057f, 8.41751f, 1.78125f), ShrikeRules.NestLocal(11, 12, false), "nest11");
        }

        [Test]
        public void Nest_ThirtyTwoBlocks_MatchThePage()
        {
            Near(new Vector3(1.77007f, 0f, -8.82422f), ShrikeRules.NestLocal(0, 32, false), "nest0");
            Near(new Vector3(6.43266f, 5.42088f, -3.19922f), ShrikeRules.NestLocal(16, 32, false), "nest16");
            Near(new Vector3(4.73577f, -7.36683f, 2.07422f), ShrikeRules.NestLocal(31, 32, false), "nest31");
        }

        [Test]
        public void Den_FortyBlocks_MatchThePage()
        {
            Near(new Vector3(2.38797f, 0f, -11.76f), ShrikeRules.NestLocal(0, 40, true), "den0");
            Near(new Vector3(-3.95001f, -7.60509f, -8.4f), ShrikeRules.NestLocal(7, 40, true), "den7");
            Near(new Vector3(7.78594f, -5.9108f, 6.96f), ShrikeRules.NestLocal(39, 40, true), "den39");
        }

        [Test]
        public void Nest_IsACupBehindTheHull_TheNoseOpen()
        {
            for (int i = 0; i < 32; i++)
            {
                var p = ShrikeRules.NestLocal(i, 32, false);
                Assert.AreEqual(9f, p.magnitude, Eps, "every block sits on the nest's sphere");
                Assert.Less(p.z, 9f * 0.25f + Eps, "nothing past zMin in front of the hull: the nose's way out is open");
            }
            Assert.Less(ShrikeRules.NestLocal(0, 12, false).z, 0f, "the first block is straight behind");
        }

        [Test]
        public void Turret_SitsOnTopBehind()
        {
            Near(new Vector3(0f, 11f, -4.05f), ShrikeRules.TurretLocal(false), "nest turret");
            Near(new Vector3(0f, 14f, -5.4f), ShrikeRules.TurretLocal(true), "den turret");
            Near(new Vector3(0f, 20f, -8.1f), ShrikeRules.TurretLocal(18f), "a grown nest's turret");
        }

        [Test]
        public void NestBlockCount_RoundsHalvesUp_LikeJavaScript()
        {
            Assert.AreEqual(12, ShrikeRules.NestBlockCount(12f));
            Assert.AreEqual(19, ShrikeRules.NestBlockCount(18.5f), "Math.round(18.5) = 19 (banker's rounding would say 18)");
            Assert.AreEqual(32, ShrikeRules.NestBlockCount(32f));
        }

        // ------------------------------------------------------------------ the rules

        [Test]
        public void CooldownRate_IsTheDockBonus()
        {
            Assert.AreEqual(1f, ShrikeRules.CooldownRate(false, true, 1.6f, 2.5f));
            Assert.AreEqual(1.6f, ShrikeRules.CooldownRate(true, false, 1.6f, 2.5f));
            Assert.AreEqual(2.5f, ShrikeRules.CooldownRate(true, true, 1.6f, 2.5f));
        }

        [Test]
        public void Sling_CoversItsDistanceInSlingSeconds()
        {
            float dist = ShrikeRules.SlingDistance(Vector3.zero, new Vector3(0f, 0f, 803f), 3f);
            Assert.AreEqual(800f, dist, Eps);
            Assert.AreEqual(800f, ShrikeRules.SlingSpeed(dist, 1f), Eps);
            Assert.AreEqual(1600f, ShrikeRules.SlingSpeed(dist, 0.5f), Eps);
        }

        [Test]
        public void BoxDistance_MatchesThePage()
        {
            var half = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(5f, ShrikeRules.BoxDistance(new Vector3(4f, 6f, 0f), half), Eps);
            Assert.AreEqual(0f, ShrikeRules.BoxDistance(new Vector3(0.5f, 0f, 0f), half), Eps);
            Assert.AreEqual(2.23607f, ShrikeRules.BoxDistance(new Vector3(2f, 0f, 5f), half), Eps);
            Near(new Vector3(1f, 2f, 0f), ShrikeRules.BoxClosestLocal(new Vector3(4f, 6f, 0f), half), "closest");
        }

        [Test]
        public void RayBox_MatchesThePage()
        {
            var half = new Vector3(1f, 2f, 3f);
            Assert.IsTrue(ShrikeRules.TryRayBox(new Vector3(-10f, 0f, 0f), Vector3.right, half, 0f, 100f, out float t1));
            Assert.AreEqual(9f, t1, Eps);
            Assert.IsFalse(ShrikeRules.TryRayBox(new Vector3(-10f, 2.5f, 0f), Vector3.right, half, 0f, 100f, out _), "passes over");
            Assert.IsTrue(ShrikeRules.TryRayBox(new Vector3(-10f, 2.5f, 0f), Vector3.right, half, 1f, 100f, out float t2), "the pad thickens the ray");
            Assert.AreEqual(8f, t2, Eps);
            float s = Mathf.Sqrt(0.5f);
            Assert.IsTrue(ShrikeRules.TryRayBox(new Vector3(-5f, -5f, 0f), new Vector3(s, s, 0f), Vector3.one, 0.2f, 100f, out float t3));
            Assert.AreEqual(5.37401f, t3, Eps);
            Assert.IsFalse(ShrikeRules.TryRayBox(new Vector3(-5f, 0f, 0f), Vector3.right, Vector3.one, 0f, 3f, out _), "out of reach");
        }

        [Test]
        public void TurnToward_IsCappedByTheTurnRate()
        {
            var turned = ShrikeRules.TurnToward(Vector3.forward, Vector3.right, 10f * Mathf.Deg2Rad, Vector3.up);
            Assert.AreEqual(10f, Vector3.Angle(Vector3.forward, turned), 1e-2f);
            var arrived = ShrikeRules.TurnToward(Vector3.forward, Vector3.right, Mathf.PI, Vector3.up);
            Near(Vector3.right, arrived, "a turn with room to spare lands on the target");
        }

        [Test]
        public void Flora_GrowsMissiles_FasterDocked_UpToItsMax()
        {
            int ready = 1;
            float growth = 0f;
            ShrikeRules.GrowTick(ref ready, ref growth, 3f, docked: true, growSeconds: 6f, max: 3);
            Assert.AreEqual(2, ready, "twice as fast docked in its nest: 3 s grows a missile");
            ShrikeRules.GrowTick(ref ready, ref growth, 5f, docked: false, growSeconds: 6f, max: 3);
            Assert.AreEqual(2, ready);
            Assert.AreEqual(5f, growth, 1e-4f, "the growing missile is 5/6 grown");
            ShrikeRules.GrowTick(ref ready, ref growth, 1f, docked: false, growSeconds: 6f, max: 3);
            Assert.AreEqual(3, ready);
            ShrikeRules.GrowTick(ref ready, ref growth, 60f, docked: false, growSeconds: 6f, max: 3);
            Assert.AreEqual(3, ready, "a full flora grows no more");
            Assert.AreEqual(0f, growth, "and shows no growing missile");
            Assert.IsTrue(ShrikeRules.FloraMayLaunch(1, 0f));
            Assert.IsFalse(ShrikeRules.FloraMayLaunch(0, 0f), "no ready missile");
            Assert.IsFalse(ShrikeRules.FloraMayLaunch(2, 0.1f), "its clock");
        }

        [Test]
        public void Nest_NoTwoBlocksClip_AtAnyCount()
        {
            const float size = 4.5f;
            for (int count = 4; count <= 40; count++)
            {
                float r = ShrikeRules.NestRadiusFor(count, false, size);
                Assert.GreaterOrEqual(r, 9f, "never smaller than the page's nest");
                // Centres further apart than a cube's bounding diameter cannot intersect, whatever their orientation.
                for (int i = 0; i < count; i++)
                    for (int j = i + 1; j < count; j++)
                        Assert.Greater((ShrikeRules.NestLocal(i, count, false, r) - ShrikeRules.NestLocal(j, count, false, r)).magnitude,
                            size * Mathf.Sqrt(3f), $"{count} blocks: {i} and {j} clip");
            }
            Assert.Greater(ShrikeRules.NestRadiusFor(32, false, size), ShrikeRules.NestRadiusFor(12, false, size), "Mass grows the nest");
        }

        [Test]
        public void Lock_Priority_IsVesselsThenFaunaThenFloraThenMass()
        {
            Assert.IsTrue(ShrikeRules.BetterLock(ShrikeRules.LockClass.Vessel, 0.2f, ShrikeRules.LockClass.Fauna, 0.01f), "a vessel beats nearer fauna");
            Assert.IsTrue(ShrikeRules.BetterLock(ShrikeRules.LockClass.Fauna, 0.2f, ShrikeRules.LockClass.Flora, 0.01f));
            Assert.IsTrue(ShrikeRules.BetterLock(ShrikeRules.LockClass.Flora, 0.2f, ShrikeRules.LockClass.Mass, 0.01f));
            Assert.IsFalse(ShrikeRules.BetterLock(ShrikeRules.LockClass.Mass, 0.01f, ShrikeRules.LockClass.Flora, 0.2f));
            Assert.IsTrue(ShrikeRules.BetterLock(ShrikeRules.LockClass.Fauna, 0.1f, ShrikeRules.LockClass.Fauna, 0.2f), "within a class, nearer the aim");
            Assert.IsTrue(ShrikeRules.BetterLock(ShrikeRules.LockClass.Mass, 0.5f, ShrikeRules.LockClass.None, 0f), "anything beats nothing");
        }

        [Test]
        public void Lance_BreaksSuperShieldedOnlyWhenUpgraded()
        {
            Assert.IsTrue(ShrikeRules.LanceBreaks(superShielded: false, upgraded: false));
            Assert.IsFalse(ShrikeRules.LanceBreaks(superShielded: true, upgraded: false));
            Assert.IsTrue(ShrikeRules.LanceBreaks(superShielded: true, upgraded: true));
        }

        // ------------------------------------------------------------------ the hull form

        [Test]
        public void Hull_HasPlatingAndAccents()
        {
            var m = ShrikeHullForm.Hull(ShrikeHullForm.Defaults);
            Assert.Greater(m.Body.Count, 0, "plating (submesh 0)");
            Assert.Greater(m.Accent.Count, 0, "accents (submesh 1: ShipHelper paints the domain onto slot 1)");
            Assert.AreEqual(1, ShrikeHullForm.AccentSubmesh);
            Assert.AreEqual(m.Verts.Count, m.Normals.Count);
            Assert.Greater(ShrikeHullForm.SignedVolume(ShrikeHullForm.Flame(ShrikeHullForm.Defaults)), 0f, "the flame winds outward");
        }

        [Test]
        public void Primitives_WindOutward_TheirVolumesExact()
        {
            // A closed mesh's signed volume is its volume when every face winds outward, and negative when inverted.
            var box = new ShrikeHullForm.MeshData();
            ShrikeHullForm.Box(box, new Vector3(3f, -1f, 2f), new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 40f, 70f), accent: false);
            Assert.AreEqual(48f, ShrikeHullForm.SignedVolume(box), 1e-2f, "box 2 x 4 x 6");

            float hexArea = 1.5f * Mathf.Sqrt(3f);   // a unit-circumradius hexagon
            var prism = new ShrikeHullForm.MeshData();
            ShrikeHullForm.Frustum(prism, new Vector3(1f, 2f, 0f), -1f, 1f, 2f, 1f, 6, accent: false);
            Assert.AreEqual(hexArea * 3f, ShrikeHullForm.SignedVolume(prism), 1e-2f, "hexagonal prism");

            var nose = new ShrikeHullForm.MeshData();
            ShrikeHullForm.Frustum(nose, Vector3.zero, 0f, 1f, 3f, 0f, 6, accent: false);
            Assert.AreEqual(hexArea, ShrikeHullForm.SignedVolume(nose), 1e-2f, "pyramid pointing forward (r1 = 0)");

            var tail = new ShrikeHullForm.MeshData();
            ShrikeHullForm.Frustum(tail, Vector3.zero, 0f, 0f, 3f, 1f, 6, accent: true);
            Assert.AreEqual(hexArea, ShrikeHullForm.SignedVolume(tail), 1e-2f, "pyramid pointing back (r0 = 0)");
        }

        [Test]
        public void Hull_NosesDownPlusZ_TheMuzzleAhead()
        {
            var s = ShrikeHullForm.Defaults;
            var muzzle = ShrikeHullForm.Muzzle(s);
            Assert.Greater(muzzle.z, s.BodyLength * 0.5f, "the barrel reaches past the nose");
            Assert.Less(ShrikeHullForm.FlameRoot(s).z, 0f, "the flame roots at the tail");
        }

        [Test]
        public void Hull_Morphs_FollowTheElementMap()
        {
            var s = ShrikeHullForm.Defaults;
            var charged = ShrikeHullForm.Morph(s, 1f, 0f, 0f, 0f);
            Assert.Greater(charged.BarrelRadius, s.BarrelRadius, "Charge: a wider lance");
            Assert.Greater(ShrikeHullForm.Morph(s, 0f, 1f, 0f, 0f).BodyRadius, s.BodyRadius, "Mass: a fuller hull");
            Assert.Greater(ShrikeHullForm.Morph(s, 0f, 0f, 1f, 0f).BarrelLength, s.BarrelLength, "Space: a longer reach");
            Assert.Greater(ShrikeHullForm.Morph(s, 0f, 0f, 0f, 1f).WingSpan, s.WingSpan, "Time: wider wings");
            var rest = ShrikeHullForm.Morph(s, 0f, 0f, 0f, 0f);
            Assert.AreEqual(s.BarrelRadius, rest.BarrelRadius);
            Assert.AreEqual(s.WingSpan, rest.WingSpan);
        }
    }
}

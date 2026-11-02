using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Stoat's slingshot (<c>R_VesselActions/STOAT.md</c>): the pure arithmetic of a squeeze,
    /// and the shipped registration set read as text — the prefab, its bindings, the map and the
    /// two spawn lists — so a drift in any of them fails here rather than as a hull that spawns
    /// nothing. The triggers carry the field dipole since 2026-10-09 (<c>STOAT_DIPOLE.md</c>); the
    /// sling's math and assets stay, unbound. The offline twin is
    /// <c>Tools/Build/author_stoat_assets.py --check</c>.
    /// </summary>
    [TestFixture]
    public class StoatSlingTests
    {
        const string Prefab = "Assets/_Prefabs/Spacevessels/Stoat.prefab";
        const string Map = "Assets/Resources/ElementalAbilityMaps/Stoat.asset";
        const string Container = "Assets/_SO_Assets/Vessel Prefab Container.asset";
        const string NetworkPrefabs = "Assets/DefaultNetworkPrefabs.asset";
        const string PrefabGuid = "599b396ff054448db5bccc9307dfb4c9";
        const string RootTransform = "5928382658856804067";
        static string StoatClass => ((int)VesselClassType.Stoat).ToString();

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        // ------------------------------------------------------------------ the squeeze

        [Test]
        public void Hold01_IsTheTriggersTravelOnAGamepad()
        {
            Assert.AreEqual(0.35f, StoatSlingMath.Hold01(0.35f, 10f, hasAnalogTriggers: true, rampSeconds: 1f, autopilot: false, autopilotHold01: 0.5f), 1e-6f);
            Assert.AreEqual(1f, StoatSlingMath.Hold01(7f, 0f, true, 1f, false, 0.5f), 1e-6f, "clamped to 1");
        }

        [Test]
        public void Hold01_RampsWithHoldTimeWithoutAnalogTriggers()
        {
            // A keyboard writes a binary 1 into the analog field; the ramp must ignore it.
            Assert.AreEqual(0.5f, StoatSlingMath.Hold01(1f, 0.6f, false, 1.2f, false, 0.5f), 1e-6f);
            Assert.AreEqual(1f, StoatSlingMath.Hold01(1f, 5f, false, 1.2f, false, 0.5f), 1e-6f, "clamped at the ramp");
            Assert.AreEqual(1f, StoatSlingMath.Hold01(0f, 0f, false, 0f, false, 0.5f), 1e-6f, "no ramp = full squeeze");
        }

        // ------------------------------------------------------------------ the orbit

        [Test]
        public void OrbitRadius_SqueezeHarderTurnsTighter()
        {
            Assert.AreEqual(150f, StoatSlingMath.OrbitRadius(0f, 150f, 40f, 1.5f), 1e-4f, "a touch is the wide orbit");
            Assert.AreEqual(40f, StoatSlingMath.OrbitRadius(1f, 150f, 40f, 1.5f), 1e-4f, "buried is the tight one");
            float half = StoatSlingMath.OrbitRadius(0.5f, 150f, 40f, 1.5f);
            Assert.Less(half, 150f); Assert.Greater(half, 40f);
            Assert.AreEqual(150f - 110f * Mathf.Pow(0.5f, 1.5f), half, 1e-3f, "along hold^exponent");
        }

        [Test]
        public void OrbitHeading_TurnsTheTangentTowardTheCentre()
        {
            var forward = new Vector3(0f, 0f, 1f);
            var toCentre = new Vector3(-1f, 0f, 0f);           // the attractor to the LEFT
            var heading = StoatSlingMath.OrbitHeading(forward, toCentre, 10f * Mathf.Deg2Rad);
            Assert.AreEqual(1f, heading.magnitude, 1e-4f);
            Assert.AreEqual(10f, Vector3.Angle(forward, heading), 1e-3f, "turned by the step");
            Assert.Less(heading.x, 0f, "turned toward the centre (left)");
            // A nose with a radial part is first laid back onto the tangent.
            var skewed = StoatSlingMath.OrbitHeading(new Vector3(-0.3f, 0f, 1f).normalized, toCentre, 0f);
            Assert.AreEqual(0f, Vector3.Dot(skewed, toCentre), 1e-4f, "zero step = the tangent itself");
            Assert.AreEqual(toCentre, StoatSlingMath.OrbitHeading(toCentre, toCentre, 0.2f), "no tangent: the nose comes back unchanged");
        }

        [Test]
        public void Slingshot_TheDeeperSqueezeThrowsHarder()
        {
            Assert.AreEqual(15f, StoatSlingMath.SlingBoost(60f, 0f, 0.25f, 0.9f), 1e-4f);
            Assert.AreEqual(54f, StoatSlingMath.SlingBoost(60f, 1f, 0.25f, 0.9f), 1e-4f);
        }

        // ------------------------------------------------------------------ Time mode: the catch (STOAT_DIPOLE.md §1b)

        [Test]
        public void CatchPair_LaysTheAttractorAheadOnThePressedSide_TheRepulsorMirrored()
        {
            StoatSlingMath.CatchPair(Vector3.zero, Vector3.forward, Vector3.right, attractorLeft: true, 150f, 45f,
                out var att, out var rep);
            Assert.AreEqual(new Vector3(-45f, 0f, 150f), att, "left trigger: ahead and to the left");
            Assert.AreEqual(new Vector3(45f, 0f, 150f), rep, "the repulsor mirrored");
            StoatSlingMath.CatchPair(Vector3.zero, Vector3.forward, Vector3.right, attractorLeft: false, 150f, 45f, out att, out _);
            Assert.AreEqual(new Vector3(45f, 0f, 150f), att, "right trigger: to the right");
            // In front of the chase camera, never square beside the hull (the 2026-10-13 cut's invisible pole).
            Assert.Less(Vector3.Angle(Vector3.forward, att), 30f);
            // The registry's own placement agrees: the attractor sits at -axis × halfGap from the midpoint.
            var axis = StoatSlingMath.PairAxis(true, Vector3.right);
            Assert.AreEqual(new Vector3(-45f, 0f, 150f), new Vector3(0f, 0f, 150f) - axis * 45f);
        }

        [Test]
        public void Catches_AsYouPassIt_OrInsideTheOrbit_NeverWhileStillDeadAhead()
        {
            var att = new Vector3(-45f, 0f, 150f);
            Assert.IsFalse(StoatSlingMath.Catches(Vector3.zero, Vector3.forward, att, 40f, 60f, 50f), "far and ahead: not yet");
            Assert.IsTrue(StoatSlingMath.Catches(new Vector3(0f, 0f, 113f), Vector3.forward, att, 40f, 60f, 50f),
                "within the detection radius and 50° off the nose");
            Assert.IsFalse(StoatSlingMath.Catches(new Vector3(0f, 0f, 112f), Vector3.forward, new Vector3(-10f, 0f, 160f), 40f, 60f, 50f),
                "within reach but still ahead: it waits for the pass");
            Assert.IsTrue(StoatSlingMath.Catches(new Vector3(0f, 0f, 120f), Vector3.forward, new Vector3(-10f, 0f, 160f), 50f, 75f, 50f),
                "inside the orbit radius it always catches");
        }

        [Test]
        public void Catches_AStraightPassAlways_AtEveryShippedSqueeze()
        {
            // The user's rule (2026-10-14): Time mode "always catches them". Fly straight past the shipped pair: the
            // tightest orbit (40 u) with the 1.5× detection radius still reaches the 45 u pass by the time the
            // attractor is 50° off the nose (45 / sin 50° = 58.7 u ≤ 60 u).
            var att = new Vector3(-45f, 0f, 150f);
            foreach (float r in new[] { 40f, 60f, 100f, 150f })
            {
                bool caught = false;
                for (float z = 0f; z <= 300f && !caught; z += 0.5f)
                    caught = StoatSlingMath.Catches(new Vector3(0f, 0f, z), Vector3.forward, att, r, 1.5f * r, 50f);
                Assert.IsTrue(caught, $"orbit {r} u: a straight pass is caught");
            }
        }

        [Test]
        public void Ring_IsACircleOfTheRadius_InThePlaneOfThePass()
        {
            var points = new Vector3[96];
            var centre = new Vector3(-45f, 0f, 150f);
            var normal = StoatSlingMath.RingNormal(Vector3.zero, Vector3.forward, centre, Vector3.up);
            Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(normal, Vector3.up)), 1e-4f, "a level pass draws a level ring");
            int n = StoatSlingMath.Ring(centre, normal, 80f, 96, points);
            Assert.AreEqual(96, n);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(80f, Vector3.Distance(points[i], centre), 1e-3f, "every point on the ring");
                Assert.AreEqual(0f, Vector3.Dot(points[i] - centre, normal), 1e-3f, "in the ring's plane");
            }
            Assert.AreEqual(Vector3.up, StoatSlingMath.RingNormal(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 50f), Vector3.up),
                "flying straight at the centre: the fallback plane");
            Assert.AreEqual(0, StoatSlingMath.Ring(centre, normal, 0f, 96, points), "no radius, no ring");
        }

        [Test]
        public void SettleHeading_EasesOntoTheTangent_NeverSlowerThanTheOrbitsOwnTurn()
        {
            var tangent = Quaternion.Euler(0f, -60f, 0f) * Vector3.forward;
            var h = StoatSlingMath.SettleHeading(Vector3.forward, tangent, 2f, 0.1f);
            Assert.AreEqual(6f, Vector3.Angle(Vector3.forward, h), 1e-3f, "a tenth of the 60° miss");
            h = StoatSlingMath.SettleHeading(Vector3.forward, tangent, 10f, 0.1f);
            Assert.AreEqual(10f, Vector3.Angle(Vector3.forward, h), 1e-3f, "at least the orbit's own step");
            h = StoatSlingMath.SettleHeading(Vector3.forward, Quaternion.Euler(0f, -1f, 0f) * Vector3.forward, 2f, 0.1f);
            Assert.AreEqual(1f, Vector3.Angle(Vector3.forward, h), 1e-3f, "never past the tangent");
        }

        [Test]
        public void Peak_KeepsTheDeepestSqueezeThroughTheLetGo()
        {
            // A real trigger let go from full travel: the samples sweep back down before the release
            // edge (deadzone 0.05). The pair must be slung at the full squeeze, not the last sample.
            float peak = 0f;
            foreach (float sample in new[] { 0.2f, 0.7f, 1f, 1f, 0.8f, 0.45f, 0.15f, 0.06f })
                peak = StoatSlingMath.Peak(peak, sample);
            Assert.AreEqual(1f, peak, 1e-6f);
            Assert.AreEqual(0.4f, StoatSlingMath.Peak(0.4f, 0.1f), 1e-6f, "a lighter sample never shrinks the hold");
            Assert.AreEqual(1f, StoatSlingMath.Peak(0f, 3f), 1e-6f, "clamped to 1");
        }

        [Test]
        public void Hold01_AutopilotTakesItsFixedSqueeze()
        {
            Assert.AreEqual(0.5f, StoatSlingMath.Hold01(1f, 99f, true, 1f, autopilot: true, autopilotHold01: 0.5f), 1e-6f);
        }

        [Test]
        public void Strength_RunsMinToMaxAlongTheExponent()
        {
            Assert.AreEqual(2f, StoatSlingMath.Strength(0f, 2f, 12f, 1.5f), 1e-5f, "a touch is the minimum");
            Assert.AreEqual(12f, StoatSlingMath.Strength(1f, 2f, 12f, 1.5f), 1e-5f, "buried is the maximum");
            float half = StoatSlingMath.Strength(0.5f, 2f, 12f, 1.5f);
            Assert.Less(half, 7f, "exponent > 1 keeps a half squeeze under the linear midpoint");
            Assert.AreEqual(2f + 10f * Mathf.Pow(0.5f, 1.5f), half, 1e-5f);
            Assert.AreEqual(7f, StoatSlingMath.Strength(0.5f, 2f, 12f, 1f), 1e-5f, "exponent 1 is linear");
            Assert.AreEqual(5f, StoatSlingMath.Strength(1f, 5f, 3f, 1f), 1e-5f, "max below min collapses to min");
        }

        [Test]
        public void PairAxis_PutsTheBlackHoleOnThePressedSide()
        {
            // BlackHolePairMath.Positions lays the black hole at −axis and the white hole at +axis.
            var right = Vector3.right;
            var leftAxis = StoatSlingMath.PairAxis(blackOnLeft: true, right);
            BlackHolePairMath.Positions(Vector3.zero, leftAxis, 10f, out var black, out var white);
            Assert.Less(Vector3.Dot(black, right), 0f, "left trigger: black on the left");
            Assert.Greater(Vector3.Dot(white, right), 0f, "left trigger: white on the right");

            var rightAxis = StoatSlingMath.PairAxis(blackOnLeft: false, right);
            BlackHolePairMath.Positions(Vector3.zero, rightAxis, 10f, out black, out white);
            Assert.Greater(Vector3.Dot(black, right), 0f, "right trigger: black on the right");
            Assert.Less(Vector3.Dot(white, right), 0f, "right trigger: white on the left");
            Assert.AreEqual(0f, black.y, 1e-6f, "the pair lies on the hull's horizontal");
        }

        [Test]
        public void Midpoint_IsAheadInHorizonRadii()
        {
            var mid = StoatSlingMath.Midpoint(new Vector3(1f, 2f, 3f), Vector3.forward, 5f, 2f);
            Assert.AreEqual(new Vector3(1f, 2f, 13f), mid);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), StoatSlingMath.Midpoint(new Vector3(1f, 2f, 3f), Vector3.forward, 5f, -1f), "never behind");
        }

        // ------------------------------------------------------------------ the registration set

        [Test]
        public void Enum_StoatIsFifteenAndOnTheToyRoster()
        {
            Assert.AreEqual(15, (int)VesselClassType.Stoat);
            CollectionAssert.Contains(ToyVesselRoster.Default, VesselClassType.Stoat);
        }

        [Test]
        public void Prefab_ClaimsTheStoatClassWithItsOwnNetcodeIdentity()
        {
            string text = Read(Prefab);
            var types = Regex.Matches(text, @"  vesselType: (-?\d+)\n");
            Assert.AreEqual(1, types.Count, "exactly one VesselStatus");
            Assert.AreEqual(StoatClass, types[0].Groups[1].Value, "vesselType is the prefab's ADDRESS (CONTRACT.md §1)");
            StringAssert.Contains("  m_Name: Stoat\n", text);
            StringAssert.DoesNotContain("  _name: Squirrel\n", text);

            string squirrel = Read("Assets/_Prefabs/Spacevessels/Squirrel.prefab");
            string Hash(string t) => Regex.Match(t, @"  GlobalObjectIdHash: (\d+)\n").Groups[1].Value;
            Assert.AreNotEqual(Hash(squirrel), Hash(text), "a disk copy keeps the donor's GlobalObjectIdHash and Netcode keys both prefabs on one entry");
            StringAssert.Contains("  InScenePlacedSourceGlobalObjectIdHash: 0\n", text);
        }

        [Test]
        public void Prefab_BindsEachTriggerToItsOrbit_XToTheHold_AndNothingToB()
        {
            string text = Read(Prefab);
            string block = Regex.Match(text, @"  _gamepadActionOverrides:\n(?:.*\n)*?(?=  _onButtonPressed:)").Value;
            Assert.IsNotEmpty(block, "gamepad override block");
            // 2026-11-02: the field dipole and the B mode switch were removed from the kit; each trigger is its orbit alone.
            StringAssert.Contains("  - InputEvent: 2\n    ShipActions:\n    - {fileID: 11400000, guid: 5c382e0def5b45d7b1230d75e8be082b, type: 2}\n" +
                                  "  - InputEvent: 1\n", block, "LT → the left orbit alone");
            StringAssert.Contains("  - InputEvent: 1\n    ShipActions:\n    - {fileID: 11400000, guid: 012d65baf31c49c496d38975eba8c0be, type: 2}\n" +
                                  "  - InputEvent: 6\n", block, "RT → the right orbit alone");
            StringAssert.Contains("  - InputEvent: 6\n    ShipActions:\n    - {fileID: 11400000, guid: 9b50ea11259b4b1aa750eea7f1d7f781, type: 2}\n", block, "X → StoatHoldAction");
            foreach (var guid in new[] { "9eacf723b796418caecfe237336b9985", "62ce60a32bef4c70b42fd1f7cd9896ff", "f05e7ded817a4670bb78832c9c879566" })
                StringAssert.DoesNotContain($"guid: {guid}, type: 2}}\n", block, $"{guid} (a dipole or the mode switch) is still bound");
            StringAssert.DoesNotContain("  - InputEvent: 7\n", block, "B is bound");
            // On a phone the same three (the owner, 2026-10-21: "the stoat should join the mobile fleet"; STOAT.md "On a phone").
            string touch = Regex.Match(text, @"  _touchActionOverrides:\n(?:.*\n)*?(?=  _gamepadActionOverrides:)").Value;
            Assert.AreEqual(block.Substring(block.IndexOf('\n') + 1), touch.Substring(touch.IndexOf('\n') + 1),
                "the touch overrides carry exactly the gamepad's entries");
            // No DriftActionSO is bound, so the trigger analog the transformer still reads never drifts.
            StringAssert.DoesNotContain("guid: aa095c07c8cd0374698884d1a44dbdec", text, "the Squirrel's drift action must not ride the Stoat's triggers");
        }

        [Test]
        public void Prefab_CarriesBothExecutorsInTheRegistry()
        {
            string text = Read(Prefab);
            StringAssert.Contains("m_Script: {fileID: 11500000, guid: cf82f131a8df416cb205ab8055108b82, type: 3}", text, "StoatSlingExecutor");
            StringAssert.Contains("  config: {fileID: 11400000, guid: e79a09360967428cb727002bd123383a, type: 2}\n", text, "the executor's config");
            string registry = Regex.Match(text, @"--- !u!114 &9105512684531825410\n(?:.*\n)*?  _executors:\n((?:  - \{fileID: -?\d+\}\n)+)").Groups[1].Value;
            StringAssert.Contains("  - {fileID: 5137264980012345601}\n", registry, "sling executor registered");
            StringAssert.Contains("  - {fileID: 5137264980012345602}\n", registry, "stop executor registered");
            // The stop is the Sparrow's: its own prism controller, the fleet's shared channels.
            StringAssert.Contains("  stationaryModeChanged: {fileID: 11400000, guid: 0b48e834efdbe654ca3c7df60370ea3f, type: 2}\n", text);
            StringAssert.Contains("  OnMiniGameTurnEnd: {fileID: 11400000, guid: 498a06d44bde9184f985c938c803b2a1, type: 2}\n", text);
            // Stage 6: the mode switch and the ripple.
            StringAssert.Contains("  - {fileID: 5137264980012345605}\n", registry, "mode executor registered");
            StringAssert.Contains("  - {fileID: 5137264980012345606}\n", registry, "ripple executor registered");
            StringAssert.Contains("  config: {fileID: 11400000, guid: f05e7ded817a4670bb78832c9c879566, type: 2}\n", text, "the mode executor's config");
            StringAssert.Contains("  config: {fileID: 11400000, guid: da077e9619f845c388ad5b7baf8da948, type: 2}\n", text, "the ripple executor's config");
        }

        [Test]
        public void Prefab_IsItsOwnVessel_NothingOfTheSquirrelLeft()
        {
            string text = Read(Prefab);
            StringAssert.DoesNotContain("Squirrel", text, "a Squirrel name survives in the prefab");
            foreach (var (guid, what) in new[]
                     {
                         ("5db69771d02b4c24ca00998adf318137", "the Squirrel model"),
                         ("c1572db06ad4244469ad3f25d86940b8", "SquirrelHUDVariant"),
                         ("002a4e51f3b840e3b397dac7b84f1a9a", "SquirrelHUDController"),
                         ("2b0d487de1554acfad93a158ddac3874", "SquirrelVesselTelemetry"),
                         ("3c3fc507fffc4f5e86ab043ee7543cd2", "the Squirrel's camera settings"),
                         ("769c65db9958c064485be49a2183f263", "the Squirrel's vessel container"),
                         ("643af834f1bcd9946903ac2c71d6d8ff", "the Squirrel's skimmer container"),
                     })
                StringAssert.DoesNotContain(guid, text, $"still references {what}");
            StringAssert.Contains("guid: 1f9b41e6fa58494599674753191b495b", text, "the HUD is StoatHUDVariant");
            StringAssert.Contains("  settings: {fileID: 11400000, guid: 2fb2a0bb0eaf4bd1863fdf55490fa869, type: 2}\n", text, "StoatCameraSettingsSO");
            StringAssert.Contains("  m_Name: StoatBody\n", text, "the Stoat's own hit box");
            StringAssert.Contains("  m_Name: StoatHitbox\n", text, "the Stoat's own trigger volume");
        }

        [Test]
        public void Map_DeclaresAllFourAbilitiesAndTheirLevelFives()
        {
            string text = Read(Map);
            StringAssert.Contains($"  vesselClass: {StoatClass}\n", text);
            string Entry(int element) => Regex.Match(text, $@"  - Element: {element}\n(?:    .*\n)*?    UpgradeDescription: .*\n").Value;
            string charge = Entry(1), mass = Entry(2), space = Entry(3), time = Entry(4);
            StringAssert.StartsWith("  - Element: 3\n    AbilityLabel: Catch Orbit\n", space);
            StringAssert.Contains("    Input: 2\n", space, "the catch is on the triggers (LT; RT mirrors)");
            StringAssert.Contains("    UpgradeLabel: Accretion\n", space);
            StringAssert.StartsWith("  - Element: 4\n    AbilityLabel: Timed Slingshot\n", time);
            StringAssert.Contains("    Input: 0\n", time, "the warp is passive; X's hold sits outside the four abilities");
            StringAssert.Contains("    UpgradeLabel: Foresight\n", time);
            StringAssert.StartsWith("  - Element: 1\n    AbilityLabel: Ripple\n", charge);
            StringAssert.Contains("    UpgradeLabel: Siphon\n", charge);
            StringAssert.StartsWith("  - Element: 2\n    AbilityLabel: Heavy Launch\n", mass);
            StringAssert.Contains("    Input: 0\n", mass, "passive: nothing on B since Mass mode was removed");
            StringAssert.Contains("    UpgradeLabel: Armoured Wake\n", mass);
            Assert.AreEqual(4, Regex.Matches(text, @"  - Element: \d\n").Count, "one entry per element");
            StringAssert.DoesNotContain("open design slot", text, "every slot is designed");
        }

        [Test]
        public void Prefab_IsOnBothSpawnLists()
        {
            // _shipPrefabs is a Transform[]: it names the root TRANSFORM, as every hull's entry does.
            StringAssert.Contains($"  - {{fileID: {RootTransform}, guid: {PrefabGuid}, type: 3}}\n", Read(Container), "Vessel Prefab Container");
            StringAssert.Contains($"    Prefab: {{fileID: 6417075533431866457, guid: {PrefabGuid}, type: 3}}\n", Read(NetworkPrefabs), "DefaultNetworkPrefabs");
            StringAssert.Contains($"guid: {PrefabGuid}", Read(Prefab + ".meta"));
        }

        // ------------------------------------------------------------------ the catch's pace (studio dpWarp, 2026-10-14)

        [Test]
        public void CatchWarp_IsTheFieldsBoostScaledByTheShare()
        {
            var config = ScriptableObject.CreateInstance<StoatSlingConfigSO>();
            try
            {
                // the shipped share, 1.15 (the studio's dpWarp): the field's ×3 becomes 1 + 2 × 1.15 = 3.3
                Assert.AreEqual(3.3f, config.CatchWarpTarget(3f), 1e-4f);
                Assert.AreEqual(1f, config.CatchWarpTarget(1f), 1e-6f, "no field boost, no catch warp");
                Assert.AreEqual(1f, config.CatchWarpTarget(0.5f), 1e-6f, "a boost below 1 never slows the clock");
                Assert.IsTrue(config.CatchWarpFromPress, "the warp starts at the press (studio dpWarpHeld 1)");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void CatchWarp_RisesWhileHeldAndFadesAfter_TheFieldsOwnStep()
        {
            // StoatDipoleMath.StepBoost is the one rule both modes run: rise toward the target while warped, fade after.
            float b = 1f;
            for (int i = 0; i < 120; i++) b = StoatDipoleMath.StepBoost(b, true, 3.3f, 20f, 0.6f, 1f / 60f);
            Assert.AreEqual(3.3f, b, 0.01f, "two seconds held reaches the catch's target");
            for (int i = 0; i < 36; i++) b = StoatDipoleMath.StepBoost(b, false, 3.3f, 20f, 0.6f, 1f / 60f);
            Assert.AreEqual(1f + 2.3f * Mathf.Exp(-1f), b, 0.02f, "one fade time after the release, 1/e of the warp is left");
        }

        [Test]
        public void Asset_ShipsTheStudiosPaceAndTiming()
        {
            var text = Read("Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset");
            StringAssert.Contains("  catchWarpShare: 1.15\n", text, "studio dpWarp");
            StringAssert.Contains("  catchWarpFromPress: 1\n", text, "studio dpWarpHeld");
            StringAssert.Contains("  sweetSpotDegrees: 90\n", text, "studio dpSweetDeg");
            StringAssert.Contains("  missKickShare: 0.2\n", text, "studio dpMissShare");
        }

        [Test]
        public void Keyboard_BIsT_TheStudiosKeyToo()
        {
            // R is the Vessel Studio's reset; the Stoat's mode switch (Button2Action, pad B) is T in both (2026-10-14).
            foreach (var path in new[] { "Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs", "Assets/_Scripts/Controller/IO/SingleStickMouseInputStrategy.cs" })
            {
                var src = Read(path);
                StringAssert.DoesNotContain("rKey", src, $"{path}: Button2 left R");
                StringAssert.Contains("tKey", src, $"{path}: Button2 is T");
            }
            StringAssert.Contains("  - binding: 3\n    padGlyph: {fileID: 21300000, guid: b611b9e51b8b5f14b9d9dc8dd392fc60, type: 3}\n    padGlyphHeld: {fileID: 0}\n    padLabel: B\n    keyboardLabel: T\n",
                Read("Assets/Resources/ControlGlyphSet.asset"), "the B chip reads T");
        }

        // ------------------------------------------------------------------ one mode (2026-11-02: Mass and Gate removed)

        [Test]
        public void TheStoatFliesTimeModeOnly()
        {
            var go = new GameObject("StoatModeProbe");
            try
            {
                var mode = go.AddComponent<StoatModeExecutor>();
                Assert.AreEqual(StoatModeExecutor.Mode.Time, mode.Current);
                Assert.IsTrue(mode.UsesOrbit);
                Assert.IsFalse(mode.IsMassMode);
                Assert.AreEqual(1, (int)StoatModeExecutor.Mode.Time, "recorded in flights: never renumber");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hud_TheModeCardShowsAnIconPerMode()
        {
            var text = Read("Assets/_Prefabs/UI Elements/VesselHUD/StoatHUDVariant.prefab");
            foreach (var (field, guid) in new[]
                     {
                         ("massModeIcon", "707ac0afea37451ef31b5be8eb09cdb2"),
                         ("timeModeIcon", "fb6ff66c748d7560d236e89894823447"),
                     })
            {
                StringAssert.Contains($"  {field}: {{fileID: 21300000, guid: {guid}, type: 3}}\n", text, field);
                Assert.IsTrue(File.Exists(AssetDatabasePathOf(guid)), $"{field}'s sprite is missing");
            }
        }

        static string AssetDatabasePathOf(string guid)
        {
            foreach (var meta in Directory.GetFiles("Assets/_Graphics/Icons/AbilityIcons/Stoat", "*.png.meta"))
                if (File.ReadAllText(meta).Contains($"guid: {guid}")) return meta.Substring(0, meta.Length - ".meta".Length);
            return "";
        }

        // ------------------------------------------------------------------ the orbit (2026-11-02 play test)

        [Test]
        public void Spin_RightIsClockwise_LeftAntiClockwise()
        {
            // Frame dragging moves matter along cross(spin, offset): +up drags what is ahead of the hole to the right.
            var right = StoatSlingMath.SpinAxis(false, Vector3.up);
            Assert.Greater(Vector3.Cross(right, Vector3.forward).x, 0f, "the right orbit turns clockwise seen from above");
            Assert.AreEqual(-right, StoatSlingMath.SpinAxis(true, Vector3.up), "the left one the other way");
        }

        [Test]
        public void Catch_OnlyOnTheOrbitsOwnSide()
        {
            Assert.IsTrue(StoatSlingMath.OnSpinSide(Vector3.zero, Vector3.right, new Vector3(30f, 0f, 50f), attractorLeft: false));
            Assert.IsFalse(StoatSlingMath.OnSpinSide(Vector3.zero, Vector3.right, new Vector3(-30f, 0f, 50f), attractorLeft: false),
                "flown past on the wrong side: not the clockwise orbit");
            Assert.IsTrue(StoatSlingMath.OnSpinSide(Vector3.zero, Vector3.right, new Vector3(-30f, 0f, 50f), attractorLeft: true));
        }

        [Test]
        public void LaunchAim_FollowsTheStick_WithinTheCone_AlongTheTangentAtRest()
        {
            var fwd = Vector3.forward;
            Assert.AreEqual(fwd, StoatSlingMath.LaunchAim(fwd, Vector3.right, Vector3.up, 0.1f, 0f, 0.3f, 60f), "a resting stick: the tangent");
            var full = StoatSlingMath.LaunchAim(fwd, Vector3.right, Vector3.up, 1f, 0f, 0.3f, 60f);
            Assert.AreEqual(60f, Vector3.Angle(fwd, full), 0.01f, "a full stick: the whole cone");
            Assert.Greater(full.x, 0f, "toward the stick's turn (yaw right)");
            var half = StoatSlingMath.LaunchAim(fwd, Vector3.right, Vector3.up, 0.5f, 0f, 0.3f, 60f);
            Assert.AreEqual(30f, Vector3.Angle(fwd, half), 0.01f, "in proportion to the stick");
            var up = StoatSlingMath.LaunchAim(fwd, Vector3.right, Vector3.up, 0f, -1f, 0.3f, 60f);
            Assert.Greater(up.y, 0f, "pull back: the launch climbs");
            Assert.AreEqual(fwd, StoatSlingMath.LaunchAim(fwd, Vector3.right, Vector3.up, 1f, 0f, 0.3f, 0f), "a 0 cone: always the tangent");
        }

        [Test]
        public void Flick_AFastSwingFromRest_NotASlowPush()
        {
            Assert.IsTrue(StoatSlingMath.IsFlick(0.9f, 0.1f, 0.8f, 0.15f), "rest to 0.9 in 0.1 s");
            Assert.IsFalse(StoatSlingMath.IsFlick(0.9f, 0.4f, 0.8f, 0.15f), "the same swing over 0.4 s is steering");
            Assert.IsFalse(StoatSlingMath.IsFlick(0.6f, 0.05f, 0.8f, 0.15f), "not far enough over");
        }

        [Test]
        public void Mass_IsTheLaunchsPower_AndArmouredWakeShieldsIt()
        {
            var text = Read("Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset");
            var power = Regex.Match(text, @"  launchPower:\n(?:    .*\n)+").Value;
            StringAssert.Contains("    Min: 1\n", power);
            StringAssert.Contains("    Max: 2.5\n", power);
            StringAssert.Contains("    element: 2\n", power, "Mass");
            StringAssert.Contains("  massUpgradeShieldsLaunchTrail: 1\n", text);
        }

        [Test]
        public void Earned_AnInstantReleasePaysNothing_AndTheKickNeverPassesTheCap()
        {
            Assert.AreEqual(0f, StoatSlingMath.CappedKick(50f, 160f, 1.6f, 10f, 30f), "released before 30 degrees: nothing");
            Assert.AreEqual(50f, StoatSlingMath.CappedKick(50f, 160f, 1.6f, 90f, 30f), 1e-4f, "under the cap: all of it");
            Assert.AreEqual(96f, StoatSlingMath.CappedKick(500f, 160f, 1.6f, 90f, 30f), 1e-3f, "never past 1.6 x the release speed");
        }

        [Test]
        public void Accretion_FourPercentAPrism_CappedAtForty()
        {
            Assert.AreEqual(0f, StoatSlingMath.AccretionBonus(0, 0.04f, 0.4f), 1e-6f);
            Assert.AreEqual(0.12f, StoatSlingMath.AccretionBonus(3, 0.04f, 0.4f), 1e-5f);
            Assert.AreEqual(0.4f, StoatSlingMath.AccretionBonus(10, 0.04f, 0.4f), 1e-5f);
            Assert.AreEqual(0.4f, StoatSlingMath.AccretionBonus(50, 0.04f, 0.4f), 1e-5f, "never past +40%");
            Assert.AreEqual(0f, StoatSlingMath.AccretionBonus(-3, 0.04f, 0.4f), 1e-6f);
        }

        [Test]
        public void SpaceScaling_WidensTheGap_AndTheClearCap_ButNotTheRipple()
        {
            Assert.AreEqual(1f, StoatSlingMath.GapScale(0.5f), 1e-6f, "never shrinks");
            Assert.AreEqual(1.4f, StoatSlingMath.GapScale(1.4f), 1e-6f);
            // catchSide 45, whiteClear 0.8: the gap 90 → 126, the clear cap 72 → ~101.
            float wide = 150f * 1.4f;
            Assert.AreEqual(72f, StoatSlingMath.ClearOfRepulsor(150f, 2f * 45f, 0.8f), 1e-3f);
            Assert.AreEqual(100.8f, StoatSlingMath.ClearOfRepulsor(wide, 2f * 45f * 1.4f, 0.8f), 1e-3f);
            Assert.AreEqual(90f, StoatSlingMath.RippleSeparation(126f, 1.4f), 1e-3f, "Space does not leak into Charge's reach");
            Assert.AreEqual(0f, StoatSlingMath.RippleSeparation(-5f, 1.4f), 1e-6f);
        }

        [Test]
        public void Momentum_IsTheOrbitsWarpByTheTiming_FadingToNothing()
        {
            Assert.AreEqual(3f, StoatSlingMath.MomentumWarp(3f, 1f, 0f, 2.5f), 1e-5f, "a perfect release keeps the orbit's warp");
            Assert.AreEqual(1f, StoatSlingMath.MomentumWarp(3f, 0f, 0f, 2.5f), 1e-5f, "an untimed one keeps none");
            Assert.AreEqual(2f, StoatSlingMath.MomentumWarp(3f, 1f, 1.25f, 2.5f), 1e-5f, "half way through the fade, half of it");
            Assert.AreEqual(1f, StoatSlingMath.MomentumWarp(3f, 1f, 2.5f, 2.5f), 1e-5f, "gone");
        }

        [Test]
        public void SlingConfig_ShipsTheEarnedSpeed_AndAOneSecondPhoneRamp()
        {
            var text = Read("Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset");
            foreach (var row in new[] { "pressWarpShare: 0.5\n", "minSweepDegrees: 30\n", "momentumSeconds: 6\n", "speedCapShare: 1.6\n",
                         "holdRampSeconds: 1\n" })
                StringAssert.Contains("  " + row, text);
        }

        [Test]
        public void SlingConfig_ShipsTheInnerOrbit()
        {
            var text = Read("Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset");
            foreach (var row in new[] { "innerOrbitShare: 0.6\n", "innerPullRate: 1.5\n", "orbitSpeedBonus: 0.15\n", "launchConeDegrees: 60\n",
                         "flickStick: 0.8\n", "flickSeconds: 0.15\n" })
                StringAssert.Contains("  " + row, text);
        }

        [Test]
        public void WantsWarp_ALongLegAtAnyBearing_NeverAShortOneOrWithTheRuleOff()
        {
            Assert.IsTrue(StoatSlingMath.WantsWarp(new Vector3(0f, 0f, 280f), 280f), "dead ahead, far enough");
            Assert.IsFalse(StoatSlingMath.WantsWarp(new Vector3(0f, 0f, 279f), 280f), "too near the ring");
            Assert.IsFalse(StoatSlingMath.WantsWarp(new Vector3(0f, 0f, 9000f), 0f), "0 turns the rule off");
            Assert.IsFalse(StoatSlingMath.TryAutopilotSide(new Vector3(0f, 0f, 9000f), 7.84f, 120f, out _),
                "dead ahead the turn press never fires: the hole the warp press closes");
        }

        [Test]
        public void EveryAIStoat_EarnsItsSpeed_NoWarpOnlyPresses_HoldsForTheWindow()
        {
            // Since 2026-11-02 speed is earned by timed launches: an AI that pressed for the warp alone spammed ~20-28 presses a
            // minute and timed almost none (Tools/Build/stoat_orbit_sim.py --earned); holding for the window it times most.
            foreach (var path in new[] { "Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset",
                         "Assets/Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset" })
            {
                StringAssert.Contains("  aiWarpMinDistance: 0\n", Read(path), path);
                StringAssert.Contains("  aiHoldForWindowDegrees: 120\n", Read(path), path);
            }
        }
    }
}

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The envelope every Cleave arena is built to - ONE PER INTENSITY - and the ordering that
    /// makes it load-bearing.
    ///
    /// These are not tidiness checks. Three systems are sized against an arena's radius (the AI's
    /// stations, the player spawn ring, the cell membrane), so an arena that quietly grew past its
    /// own rung's envelope would break the AI and the player spawn without failing anything at
    /// edit time or at run time.
    ///
    /// The prism COUNTS are proved elsewhere and on purpose: three of the four arenas cull with
    /// value noise, so they are measured by compiling and RUNNING the shipped generators
    /// (<c>Tools/Build/cleave_arena_harness</c>), and <c>Tools/Build/cleave_budget.py</c> asserts
    /// the envelope against that measurement with the prism's far CORNER rather than its lay point.
    /// What is left for this suite is the part a C# test can own: that the constants themselves are
    /// sane, that the TABLE is self-consistent, and that the offline model has not drifted from it.
    /// </summary>
    public class SliceArenaGeometryTests
    {
        /// <summary>Per-intensity spawn ring, restated from <c>cleave_budget.SPAWN_RING</c> and
        /// authored onto the scene's
        /// <c>ServerPlayerVesselInitializer.spawnRingRadiusFloorByIntensity</c>.</summary>
        static readonly float[] SpawnRing = { 3150f, 3150f, 1050f, 1050f };

        /// <summary>Per-intensity membrane radius, from each Cleave cell config's MembranePrefab -
        /// <c>CleaveMembrane.prefab</c> (3600) on rungs 1 and 2, the standard
        /// <c>CapsuleMembrane.prefab</c> (1200) on 3 and 4.</summary>
        static readonly float[] MembraneRadius = { 3600f, 3600f, 1200f, 1200f };

        /// <summary>A standard nucleus in world units: <c>Node2.fbx</c>'s half-extent 0.9798 x
        /// <c>Nucleus.prefab</c>'s scale 400. Cleave's cell authors NO nucleus, so this is a
        /// yardstick rather than a thing in the arena - see <see cref="EveryArenaIsBiggerThanANucleus"/>.</summary>
        const float StandardNucleusRadius = 392f;

        const int Intensities = 4;

        [Test]
        public void AiStationSitsOutsideTheArena()
        {
            // AIPilot has no arrive-and-stop behaviour - it steers at its target forever and flies
            // through on arrival - so a station at or inside the mass is a point the AI orbits from
            // WITHIN. That defect shipped twice before the standoff was introduced.
            Assert.Greater(SliceArenaGeometry.AiStationStandoff, 1f,
                "AiStationStandoff must exceed 1 or every AI parks inside the arena.");
        }

        /// <summary>
        /// The ordering holds PER RUNG, which is the whole reason the envelope became a table.
        ///
        /// One shared radius was fine while the four arenas were one size. Intensities 1 and 2 are
        /// now 2,160 against 720 for 3 and 4, and a single spawn ring across that spread either
        /// spawns a pilot inside the big arenas or parks them 3,000 units from a speck - so each
        /// rung carries its own station, ring and membrane, and each has to be checked.
        /// </summary>
        [Test]
        public void EnvelopeOrderingHoldsForEveryIntensity()
        {
            for (int i = 1; i <= Intensities; i++)
            {
                float arena = SliceArenaGeometry.OuterRadiusFor(i);
                float station = SliceArenaGeometry.AiStationRadiusFor(i);

                Assert.Less(arena, station,
                    $"intensity {i}: the AI's stations must sit outside the arena.");
                Assert.Less(station, SpawnRing[i - 1],
                    $"intensity {i}: players must spawn outside the AI's stations, with the whole "
                    + "arena ahead of them.");
                Assert.Less(SpawnRing[i - 1], MembraneRadius[i - 1],
                    $"intensity {i}: the spawn ring must sit inside the cell membrane.");
            }
        }

        /// <summary>
        /// The arena is a PLACE, not an ornament in the middle of an empty cell.
        ///
        /// Cleave first shipped at radius 360 - smaller than a standard nucleus - and read exactly
        /// that way: four hand-built arenas, all of them a small ball parked at the centre of a
        /// 1200-radius membrane, with a boosted Rhino crossing the whole thing in 0.6 s. It is
        /// asserted rather than documented because the failure is a FEELING - nothing breaks,
        /// nothing logs, the arena is simply small - so nothing else would catch a future edit that
        /// quietly pulled an envelope back in.
        /// </summary>
        [Test]
        public void EveryArenaIsBiggerThanANucleus()
        {
            for (int i = 1; i <= Intensities; i++)
                Assert.Greater(SliceArenaGeometry.OuterRadiusFor(i), StandardNucleusRadius,
                    $"intensity {i}'s arena must be larger than a standard nucleus, or it reads as "
                    + "a little ball in the middle of the cell rather than as the place the match "
                    + "happens.");
        }

        /// <summary>
        /// A rung's radius IS its authored radius times its own length scale - no rung may carry a
        /// radius that is not a similarity of the geometry the four generators were tuned against.
        ///
        /// This is the invariant the whole scaling approach rests on: every count in the four
        /// generators is a ratio of two lengths that both carry the scale, so a similarity moves no
        /// prism count and therefore no collider budget. A hand-written radius would break that
        /// silently - the arena would still build, just at a size its spacings were never fitted
        /// for.
        /// </summary>
        [Test]
        public void EveryRadiusIsItsOwnScaleTimesTheAuthoredRadius()
        {
            for (int i = 1; i <= Intensities; i++)
            {
                Assert.Greater(SliceArenaGeometry.LengthScaleFor(i), 0f, $"intensity {i}");
                Assert.AreEqual(
                    SliceArenaGeometry.AuthoredRadius * SliceArenaGeometry.LengthScaleFor(i),
                    SliceArenaGeometry.OuterRadiusFor(i), 1e-3f,
                    $"intensity {i}'s OuterRadius is not AuthoredRadius x its LengthScale.");
            }
        }

        /// <summary>
        /// A gap scale is a MULTIPLIER on an across-grain step, so 1 is "no extra spacing" and
        /// anything under 1 would crowd an arena tighter than the geometry was fitted for.
        ///
        /// It is the one dial that moves a prism count (down, by G), which is also why it may not
        /// go below 1 without a fresh collider decision rather than a re-run.
        /// </summary>
        [Test]
        public void GapScalesNeverCrowdAnArena()
        {
            for (int i = 1; i <= Intensities; i++)
                Assert.GreaterOrEqual(SliceArenaGeometry.GapScaleFor(i), 1f,
                    $"intensity {i}'s GapScale is below 1, which packs its ribs tighter than they "
                    + "were fitted for and RAISES the prism count.");
        }

        /// <summary>
        /// Intensity is clamped at both ends rather than throwing or reading off the end.
        ///
        /// <c>GameDataSO.SelectedIntensity</c> is an int a mode can in principle be launched with
        /// out of range (a Maelstrom draw, a hand-set config, a 0 before the server has published
        /// one), and every consumer here runs during the spawn chain. Falling back to a real rung
        /// is the difference between "the first arena" and an exception inside cell bring-up.
        /// </summary>
        [Test]
        public void OutOfRangeIntensityClampsToARealRung()
        {
            Assert.AreEqual(SliceArenaGeometry.OuterRadiusI1, SliceArenaGeometry.OuterRadiusFor(0));
            Assert.AreEqual(SliceArenaGeometry.OuterRadiusI1, SliceArenaGeometry.OuterRadiusFor(-3));
            Assert.AreEqual(SliceArenaGeometry.OuterRadiusI4, SliceArenaGeometry.OuterRadiusFor(9));
            Assert.AreEqual(SliceArenaGeometry.GapScaleI1, SliceArenaGeometry.GapScaleFor(0));
            Assert.AreEqual(SliceArenaGeometry.GapScaleI4, SliceArenaGeometry.GapScaleFor(9));
            Assert.AreEqual(SliceArenaGeometry.LengthScaleI1, SliceArenaGeometry.LengthScaleFor(0));
            Assert.AreEqual(SliceArenaGeometry.LengthScaleI4, SliceArenaGeometry.LengthScaleFor(9));
            Assert.AreEqual(SliceArenaGeometry.PrismScaleI1, SliceArenaGeometry.PrismScaleFor(0));
            Assert.AreEqual(SliceArenaGeometry.PrismScaleI4, SliceArenaGeometry.PrismScaleFor(9));
        }

        /// <summary>The widest rung, for anything that has to bound every intensity at once.</summary>
        [Test]
        public void MaxOuterRadiusBoundsEveryRung()
        {
            for (int i = 1; i <= Intensities; i++)
                Assert.LessOrEqual(SliceArenaGeometry.OuterRadiusFor(i),
                    SliceArenaGeometry.MaxOuterRadius, $"intensity {i}");
        }

        /// <summary>
        /// The offline harness COMPILES these constants, so the model can never disagree with them.
        /// What can still rot is the prose: CLEAVE.md, this suite and the generators all quote
        /// 2160, 720, 360, 6, 3 and 1.3 as readable numbers. This asserts they remain simple
        /// literals AND that the literal a human reads is the value the code uses - so a move to a
        /// computed expression fails here rather than quietly making every stated figure a guess.
        ///
        /// The per-rung radii are deliberately NOT in this set: they are authored as
        /// <c>AuthoredRadius * LengthScaleIn</c>, which is the expression
        /// <see cref="EveryRadiusIsItsOwnScaleTimesTheAuthoredRadius"/> exists to hold, and
        /// re-stating them as literals is exactly the drift surface that removes.
        /// </summary>
        [Test]
        public void ConstantsAreParseableByTheOfflineModel()
        {
            string path = Path.Combine(Application.dataPath,
                "_Scripts/Controller/Environment/MiniGameObjects/SliceArenaGeometry.cs");
            Assert.IsTrue(File.Exists(path), $"SliceArenaGeometry.cs not found at {path}");

            string src = File.ReadAllText(path);

            AssertLiteral(src, "AuthoredRadius", SliceArenaGeometry.AuthoredRadius);
            AssertLiteral(src, "AiStationStandoff", SliceArenaGeometry.AiStationStandoff);
            for (int i = 1; i <= Intensities; i++)
            {
                AssertLiteral(src, $"LengthScaleI{i}", SliceArenaGeometry.LengthScaleFor(i));
                AssertLiteral(src, $"GapScaleI{i}", SliceArenaGeometry.GapScaleFor(i));
                AssertLiteral(src, $"PrismScaleI{i}", SliceArenaGeometry.PrismScaleFor(i));
            }
        }

        /// <summary>
        /// Prism size is NOT part of the similarity, and the whole ladder states one number for it.
        /// A rung that quietly drifted off 2 would re-cut that arena into a different number of
        /// pieces - which re-prices its destruction target, since a target is a fraction of the
        /// arena - so the fleet-wide value is asserted rather than left to four independent
        /// literals. Raising it on ONE rung is a deliberate act that should fail here first.
        /// </summary>
        [Test]
        public void EveryRungIsBuiltFromTheSameSmallPrisms()
        {
            for (int i = 1; i <= Intensities; i++)
                Assert.AreEqual(SliceArenaGeometry.PrismScaleI1, SliceArenaGeometry.PrismScaleFor(i),
                    1e-4f, $"intensity {i}");
        }

        static void AssertLiteral(string src, string name, float expected)
        {
            var m = Regex.Match(src, $@"public const float {name}\s*=\s*([0-9.]+)f\s*;");
            Assert.IsTrue(m.Success,
                $"{name} is no longer a literal `public const float ... = N f;`.");
            Assert.AreEqual(expected,
                float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 1e-4f, name);
        }
    }
}

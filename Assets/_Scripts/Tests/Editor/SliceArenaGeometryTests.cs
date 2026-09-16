using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The one envelope every Cleave arena is built to, and the ordering that makes it load-bearing.
    ///
    /// These are not tidiness checks. Three systems are sized against
    /// <see cref="SliceArenaGeometry.OuterRadius"/> and none of them can be told which of the four
    /// arenas is running, so an arena that quietly grew past it would break the AI and the player
    /// spawn without failing anything at edit time or at run time.
    ///
    /// The prism COUNTS are proved elsewhere and on purpose: three of the four arenas cull with
    /// value noise, so they are measured by compiling and RUNNING the shipped generators
    /// (<c>Tools/Build/cleave_arena_harness</c>), and <c>Tools/Build/cleave_budget.py</c> asserts
    /// the envelope against that measurement with the prism's far CORNER rather than its lay point.
    /// What is left for this suite is the part a C# test can own: that the constants themselves are
    /// sane, and that the offline model has not drifted from them.
    /// </summary>
    public class SliceArenaGeometryTests
    {
        /// <summary>The spawn ring the scene authors, restated from <c>cleave_budget.SPAWN_RING</c>.</summary>
        const float SpawnRing = 1050f;
        /// <summary>The cell's membrane, from the Cleave cell configs' MembranePrefab
        /// (<c>CapsuleMembrane.prefab</c>, radius 1200).</summary>
        const float MembraneRadius = 1200f;
        /// <summary>A standard nucleus in world units: <c>Node2.fbx</c>'s half-extent 0.9798 x
        /// <c>Nucleus.prefab</c>'s scale 400. Cleave's cell authors NO nucleus, so this is a
        /// yardstick rather than a thing in the arena - see <see cref="ArenaIsBiggerThanANucleus"/>.</summary>
        const float StandardNucleusRadius = 392f;

        [Test]
        public void AiStationSitsOutsideTheArena()
        {
            // AIPilot has no arrive-and-stop behaviour - it steers at its target forever and flies
            // through on arrival - so a station at or inside the mass is a point the AI orbits from
            // WITHIN. That defect shipped twice before the standoff was introduced.
            Assert.Greater(SliceArenaGeometry.AiStationStandoff, 1f,
                "AiStationStandoff must exceed 1 or every AI parks inside the arena.");
        }

        [Test]
        public void EnvelopeOrderingHolds()
        {
            float station = SliceArenaGeometry.OuterRadius * SliceArenaGeometry.AiStationStandoff;

            Assert.Less(SliceArenaGeometry.OuterRadius, station,
                "The AI's stations must sit outside the arena.");
            Assert.Less(station, SpawnRing,
                "Players must spawn outside the AI's stations, with the whole arena ahead of them.");
            Assert.Less(SpawnRing, MembraneRadius,
                "The spawn ring must sit inside the cell membrane.");
        }

        /// <summary>
        /// The arena is a PLACE, not an ornament in the middle of an empty cell.
        ///
        /// Cleave first shipped at radius 360 - smaller than a standard nucleus - and read exactly
        /// that way: four hand-built arenas, all of them a small ball parked at the centre of a
        /// 1200-radius membrane, with a boosted Rhino crossing the whole thing in 0.6 s. The fix
        /// was a uniform 2x similarity of the whole family
        /// (<see cref="SliceArenaGeometry.LengthScale"/>), and this is the promise it was made to
        /// keep. It is asserted rather than documented because the failure is a FEELING - nothing
        /// breaks, nothing logs, the arena is simply small - so nothing else would catch a future
        /// edit that quietly pulled the envelope back in.
        /// </summary>
        [Test]
        public void ArenaIsBiggerThanANucleus()
        {
            Assert.Greater(SliceArenaGeometry.OuterRadius, StandardNucleusRadius,
                "The Cleave arena must be larger than a standard nucleus, or it reads as a little "
                + "ball in the middle of the cell rather than as the place the match happens.");
        }

        /// <summary>
        /// The scale is a SIMILARITY of geometry that was tuned at
        /// <see cref="SliceArenaGeometry.AuthoredRadius"/>, and every count in the four generators
        /// is a ratio of two lengths that both carry it - so prism counts, and therefore the
        /// collider budget, do not move. Keeping it an exact power of two is what makes that
        /// bit-exact rather than approximately true: no <c>floor</c> boundary and no noise sample
        /// can land on the other side of itself. (Proved empirically too - the harness re-measured
        /// identical counts and exactly 8x volumes after the 2x.)
        /// </summary>
        [Test]
        public void LengthScaleIsAnExactPowerOfTwo()
        {
            Assert.AreEqual(SliceArenaGeometry.OuterRadius / SliceArenaGeometry.AuthoredRadius,
                SliceArenaGeometry.LengthScale, 0f);
            Assert.Greater(SliceArenaGeometry.LengthScale, 0f);

            float log2 = Mathf.Log(SliceArenaGeometry.LengthScale, 2f);
            Assert.AreEqual(Mathf.Round(log2), log2, 1e-5f,
                "LengthScale must stay an exact power of two - see SliceArenaGeometry.");
        }

        [Test]
        public void OuterRadiusIsPositiveAndInsideTheMembrane()
        {
            Assert.Greater(SliceArenaGeometry.OuterRadius, 0f);
            Assert.Less(SliceArenaGeometry.OuterRadius, MembraneRadius);
        }

        /// <summary>
        /// The offline harness COMPILES these constants, so the model can never disagree with them.
        /// What can still rot is the prose: CLEAVE.md, this suite and the generator all quote 720,
        /// 360 and 1.3 as readable numbers. This asserts they remain simple literals AND that the
        /// literal a human reads is the value the code uses - so a move to a computed expression
        /// fails here rather than quietly making every stated figure a guess.
        /// </summary>
        [Test]
        public void ConstantsAreParseableByTheOfflineModel()
        {
            string path = Path.Combine(Application.dataPath,
                "_Scripts/Controller/Environment/MiniGameObjects/SliceArenaGeometry.cs");
            Assert.IsTrue(File.Exists(path), $"SliceArenaGeometry.cs not found at {path}");

            string src = File.ReadAllText(path);

            var outer = Regex.Match(src, @"public const float OuterRadius\s*=\s*([0-9.]+)f\s*;");
            Assert.IsTrue(outer.Success,
                "OuterRadius is no longer a literal `public const float ... = N f;`.");
            Assert.AreEqual(SliceArenaGeometry.OuterRadius, float.Parse(outer.Groups[1].Value), 1e-4f);

            var authored = Regex.Match(src, @"public const float AuthoredRadius\s*=\s*([0-9.]+)f\s*;");
            Assert.IsTrue(authored.Success,
                "AuthoredRadius is no longer a literal `public const float ... = N f;`.");
            Assert.AreEqual(SliceArenaGeometry.AuthoredRadius,
                float.Parse(authored.Groups[1].Value), 1e-4f);

            var standoff = Regex.Match(src, @"public const float AiStationStandoff\s*=\s*([0-9.]+)f\s*;");
            Assert.IsTrue(standoff.Success,
                "AiStationStandoff is no longer a literal `public const float ... = N f;`.");
            Assert.AreEqual(SliceArenaGeometry.AiStationStandoff,
                float.Parse(standoff.Groups[1].Value), 1e-4f);
        }
    }
}

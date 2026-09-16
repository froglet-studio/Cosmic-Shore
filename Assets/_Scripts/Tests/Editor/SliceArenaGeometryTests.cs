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
        const float SpawnRing = 576f;
        /// <summary>The cell's membrane, from the Cleave cell configs' MembranePrefab.</summary>
        const float MembraneRadius = 1200f;

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

        [Test]
        public void OuterRadiusIsPositiveAndInsideTheMembrane()
        {
            Assert.Greater(SliceArenaGeometry.OuterRadius, 0f);
            Assert.Less(SliceArenaGeometry.OuterRadius, MembraneRadius);
        }

        /// <summary>
        /// The offline harness COMPILES these constants, so the model can never disagree with them.
        /// What can still rot is the prose: CLEAVE.md, this suite and the generator all quote 360
        /// and 1.3 as readable numbers. This asserts they remain simple literals AND that the
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

            var standoff = Regex.Match(src, @"public const float AiStationStandoff\s*=\s*([0-9.]+)f\s*;");
            Assert.IsTrue(standoff.Success,
                "AiStationStandoff is no longer a literal `public const float ... = N f;`.");
            Assert.AreEqual(SliceArenaGeometry.AiStationStandoff,
                float.Parse(standoff.Groups[1].Value), 1e-4f);
        }
    }
}

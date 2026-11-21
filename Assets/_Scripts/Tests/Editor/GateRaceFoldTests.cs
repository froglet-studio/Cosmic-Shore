#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>The gate-race fold, proven rather than argued.</b>
    ///
    /// <para><see cref="GateRaceController"/> is shared by three modes, and Breakwater generalised
    /// its fold to admit a LEAD-IN gate — a start gate the laps do not come back to. A change like
    /// that is safe for the two modes that do not use one only if it is byte-identical at
    /// <c>leadIn = 0</c>, and "the algebra collapses" is exactly the kind of claim that reads as
    /// true and ships wrong. So the pre-lead-in formulas are written out here as an ORACLE and the
    /// shipped ones are compared against them over the whole parameter space.</para>
    /// </summary>
    public class GateRaceFoldTests
    {
        // The formulas as they stood before the lead-in landed.
        static int OracleRaceLength(int rings, int laps) => rings * Mathf.Max(1, laps);

        static int OracleRingIndex(int threaded, int rings, int laps) =>
            rings == 0 ? -1 : (laps > 1 ? threaded % rings : threaded);

        [Test]
        public void AtZeroLeadInTheFoldIsExactlyWhatItWasBefore()
        {
            for (int rings = 0; rings <= 40; rings++)
            for (int laps = 1; laps <= 6; laps++)
            {
                Assert.AreEqual(OracleRaceLength(rings, laps),
                                GateRaceController.RaceLengthFor(rings, 0, laps),
                                $"RaceLength drifted at rings={rings}, laps={laps}.");

                for (int threaded = 0; threaded <= OracleRaceLength(rings, laps); threaded++)
                    Assert.AreEqual(OracleRingIndex(threaded, rings, laps),
                                    GateRaceController.RingIndexFor(threaded, rings, 0, laps),
                                    $"RingIndexFor drifted at threaded={threaded}, rings={rings}, laps={laps}.");
            }
        }

        /// <summary>Breakwater's shape: a start gate plus a fourteen-station circuit, two laps.</summary>
        [Test]
        public void ABreakwaterCourseThreadsTheStartGateOnceAndTheCircuitEveryLap()
        {
            const int rings = 15, leadIn = 1, laps = 2;

            Assert.AreEqual(29, GateRaceController.RaceLengthFor(rings, leadIn, laps),
                            "a start gate plus fourteen stations flown twice is 29 crossings.");

            var visited = new System.Collections.Generic.List<int>();
            for (int t = 0; t < 29; t++) visited.Add(GateRaceController.RingIndexFor(t, rings, leadIn, laps));

            Assert.AreEqual(0, visited[0], "crossing 0 is the start gate.");
            Assert.AreEqual(1, visited.FindAll(v => v == 0).Count,
                            "the start gate must be threaded exactly ONCE - re-offering it is the " +
                            "'sent me backward through the rings I came' defect in a new costume.");

            for (int ring = 1; ring < rings; ring++)
                Assert.AreEqual(laps, visited.FindAll(v => v == ring).Count,
                                $"ring {ring} should be threaded once per lap.");

            for (int i = 1; i < visited.Count; i++)
                Assert.AreNotEqual(visited[i - 1], visited[i],
                                   $"crossings {i - 1} and {i} name the same ring.");

            Assert.AreEqual(1, visited[1], "the lap starts at the first circuit station.");
            Assert.AreEqual(1, visited[15], "lap two re-enters at the first circuit station, not the gate.");
        }

        [Test]
        public void TheFoldNeverIndexesOutsideTheRingSet()
        {
            for (int rings = 1; rings <= 20; rings++)
            for (int leadIn = 0; leadIn <= rings + 2; leadIn++)
            for (int laps = 1; laps <= 4; laps++)
            for (int t = 0; t <= GateRaceController.RaceLengthFor(rings, leadIn, laps) + 3; t++)
            {
                int idx = GateRaceController.RingIndexFor(t, rings, leadIn, laps);
                bool past = t >= GateRaceController.RaceLengthFor(rings, leadIn, laps);
                if (past && leadIn == 0 && laps <= 1) continue;   // open chain: caller stops at the target
                Assert.GreaterOrEqual(idx, 0, $"rings={rings} leadIn={leadIn} laps={laps} t={t}");
                Assert.Less(idx, rings, $"rings={rings} leadIn={leadIn} laps={laps} t={t}");
            }
        }
    }
}
#endif

using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// How dangerous one rung of the sea is (Docs/Voyages/MILESTONE_2.md §3, the Voyage Codex's ladder). Every voyage type
    /// reads the same four rungs; nothing here touches a vessel.
    /// </summary>
    [Serializable]
    public struct VoyageSeaRung
    {
        /// <summary>Share of the leg's cells held against the squad (0 none, 1 every cell).</summary>
        public float HostileShare;
        /// <summary>Multiplier on each cell's life (its fauna and plants).</summary>
        public float Life;
        /// <summary>The breach line: the Ark falls below this share of her hull (0: no line).</summary>
        public float Breach;
        /// <summary>Raider waves in open water on the way out.</summary>
        public bool RaidersOut;
        /// <summary>Raider waves in open water on the way home.</summary>
        public bool RaidersHome;
        /// <summary>Kaiju crossings in one leg: 0 none, 1 the first crossing only, 2 or more every crossing.</summary>
        public int KaijuCrossings;

        /// <summary>The Codex's ladder: I1 calm, I2 choppy, I3 hostile, I4 abyssal.</summary>
        public static VoyageSeaRung[] Defaults() => new[]
        {
            new VoyageSeaRung { HostileShare = 0f, Life = 0.6f, Breach = 0f, RaidersOut = false, RaidersHome = false, KaijuCrossings = 0 },
            new VoyageSeaRung { HostileShare = 1f / 3f, Life = 1f, Breach = 0.1f, RaidersOut = false, RaidersHome = true, KaijuCrossings = 0 },
            new VoyageSeaRung { HostileShare = 2f / 3f, Life = 1.3f, Breach = 0.3f, RaidersOut = true, RaidersHome = true, KaijuCrossings = 1 },
            new VoyageSeaRung { HostileShare = 1f, Life = 1.6f, Breach = 0.45f, RaidersOut = true, RaidersHome = true, KaijuCrossings = 2 },
        };
    }

    /// <summary>
    /// The shared base of the Milestone 2 voyages (Docs/Voyages/MILESTONE_2.md, Shombith 2026-11-24): a run flies its
    /// lane's cells (1 to 6), its difficulty is the sea's danger (rung I1-I4, never a timer), and a cell takes about 50
    /// seconds so a run lands at 3 to 5 minutes. Pure C#: the board, the run and the tests read it.
    /// </summary>
    public static class VoyageSea
    {
        public const int MinCells = 1, MaxCells = 6;
        public const int MinRung = 1, MaxRung = 4;

        /// <summary><paramref name="cells"/> kept inside 1-6.</summary>
        public static int RunCells(int cells) => Math.Max(MinCells, Math.Min(MaxCells, cells));

        /// <summary><paramref name="rung"/> kept inside I1-I4.</summary>
        public static int Rung(int rung) => Math.Max(MinRung, Math.Min(MaxRung, rung));

        /// <summary>The rung <paramref name="rung"/> from <paramref name="table"/> (I1 first), the Codex's when the table
        /// is short.</summary>
        public static VoyageSeaRung RungOf(IReadOnlyList<VoyageSeaRung> table, int rung)
        {
            int i = Rung(rung) - 1;
            if (table != null && i < table.Count) return table[i];
            return VoyageSeaRung.Defaults()[i];
        }

        /// <summary>How many of <paramref name="cells"/> cells are held against the squad at <paramref name="share"/>
        /// (rounded half away from zero: one cell in three at I2, two in three at I3).</summary>
        public static int HostileCount(int cells, float share)
        {
            if (cells <= 0 || share <= 0f) return 0;
            if (share >= 1f) return cells;
            return Math.Max(0, Math.Min(cells, (int)Math.Round(cells * share, MidpointRounding.AwayFromZero)));
        }

        /// <summary>
        /// Which cells of a leg start held against the squad: the <see cref="HostileCount"/> most dangerous ones by
        /// <paramref name="danger"/> (the map's danger along the course, one per cell), a tie going to the later cell so
        /// the sea roughens toward the stop. Without dangers the last cells are held.
        /// </summary>
        public static List<bool> HostilePattern(int cells, float share, IReadOnlyList<float> danger)
        {
            var pattern = new List<bool>(new bool[Math.Max(0, cells)]);
            int n = HostileCount(cells, share);
            if (n <= 0) return pattern;
            var order = new List<int>(cells);
            for (int i = 0; i < cells; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                float da = danger != null && a < danger.Count ? danger[a] : 0f;
                float db = danger != null && b < danger.Count ? danger[b] : 0f;
                int c = db.CompareTo(da);
                return c != 0 ? c : b.CompareTo(a);
            });
            for (int k = 0; k < n; k++) pattern[order[k]] = true;
            return pattern;
        }

        /// <summary>
        /// The map's danger along a course, one value per run cell: <paramref name="steps"/> (each real HyperSea cell's
        /// danger, in order) cut into <paramref name="cells"/> even runs, each run's worst. Empty steps give zeros.
        /// </summary>
        public static List<float> DangerPerCell(IReadOnlyList<double> steps, int cells)
        {
            var out0 = new List<float>(Math.Max(0, cells));
            for (int c = 0; c < cells; c++)
            {
                if (steps == null || steps.Count == 0) { out0.Add(0f); continue; }
                int a = (int)Math.Floor((double)c * steps.Count / cells);
                int b = Math.Max(a + 1, (int)Math.Floor((double)(c + 1) * steps.Count / cells));
                double worst = 0;
                for (int i = a; i < b && i < steps.Count; i++) worst = Math.Max(worst, steps[i]);
                out0.Add((float)worst);
            }
            return out0;
        }

        /// <summary>The danger of every real HyperSea cell a route crosses, in order, from the content's chart. Empty when
        /// the places are not charted.</summary>
        public static List<double> CourseDanger(VoyageContent content, IReadOnlyList<string> route)
        {
            var dangers = new List<double>();
            var chart = content != null ? VoyageChart.For(content) : null;
            if (chart == null || route == null) return dangers;
            for (int i = 1; i < route.Count; i++)
            {
                var leg = chart.LegBetween(route[i - 1], route[i]);
                if (leg?.Course == null) continue;
                foreach (var s in leg.Course.Steps) dangers.Add(s.Danger);
            }
            return dangers;
        }

        /// <summary>
        /// Lay a named voyage's sea onto <paramref name="job"/>: its hostile cells (the most dangerous by
        /// <paramref name="danger"/>), its breach line and each cell's life, all from <paramref name="rung"/>. The job's
        /// cell count is kept inside 1-6. Unnamed work is left as it is.
        /// </summary>
        public static void Lay(VoyageJob job, in VoyageSeaRung rung, IReadOnlyList<float> danger)
        {
            if (job == null || !job.IsNamed) return;
            job.Cells = RunCells(job.Cells);
            job.HostileCells = HostilePattern(job.Cells, rung.HostileShare, danger);
            job.BreachHull = Math.Max(0f, rung.Breach);
            var life = new List<float>(job.Cells);
            for (int i = 0; i < job.Cells; i++)
            {
                float authored = job.CellLife != null && job.CellLife.Count > 0 ? job.CellLife[Math.Min(i, job.CellLife.Count - 1)] : 1f;
                life.Add(Math.Max(0.1f, authored * Math.Max(0.1f, rung.Life)));
            }
            job.CellLife = life;
        }

        /// <summary>True when a kaiju may wait on crossing <paramref name="crossing"/> (0-based) of a leg at
        /// <paramref name="rung"/>.</summary>
        public static bool KaijuOn(in VoyageSeaRung rung, int crossing) =>
            rung.KaijuCrossings >= 2 || rung.KaijuCrossings == 1 && crossing <= 0;

        // ── The cell clock ──────────────────────────────────────────────────

        /// <summary>
        /// Seconds an Ark takes from one cell's core to the next under the Arkway's two-speed profile
        /// (<c>Ark.SetSpeedProfile(approach, cruise, slowRadius)</c>): <paramref name="cruise"/> in open water, easing by a
        /// smoothstep to <paramref name="approach"/> at the core over <paramref name="slowRadius"/>.
        /// </summary>
        public static double CrossingSeconds(double spacing, double slowRadius, double approach, double cruise)
        {
            approach = Math.Max(1e-3, approach);
            cruise = Math.Max(approach, cruise);
            double r = Math.Max(0, Math.Min(spacing, slowRadius));
            double open = (spacing - r) / cruise;
            const int N = 128;
            double inside = 0;
            for (int i = 0; i < N; i++)
            {
                double t = (i + 0.5) / N;
                double s = t * t * (3 - 2 * t);
                inside += 1.0 / (approach + (cruise - approach) * s);
            }
            return open + r * inside / N;
        }

        /// <summary>
        /// The core speed that crosses a cell in <paramref name="seconds"/> (cruise is <paramref name="cruiseFactor"/>
        /// times it), never above <paramref name="ceiling"/>: crossing time is inversely proportional to the speed.
        /// </summary>
        public static float ArkSpeedFor(double spacing, double slowRadius, double cruiseFactor, double seconds, float ceiling)
        {
            double unit = CrossingSeconds(spacing, slowRadius, 1, Math.Max(1, cruiseFactor));
            double speed = unit / Math.Max(1, seconds);
            if (ceiling > 0f) speed = Math.Min(ceiling, speed);
            return (float)Math.Max(1, speed);
        }
    }
}

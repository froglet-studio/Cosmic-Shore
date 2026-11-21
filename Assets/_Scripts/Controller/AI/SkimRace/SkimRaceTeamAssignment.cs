using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shares a Skim Race team's crystals out between its AI pilots: every pilot flies a DIFFERENT
    /// crystal, and the plan is the one with the least total straight-line distance. Pure - positions
    /// in, crystal indices out - so the game's team plan (<see cref="SkimRaceTeamPlan"/>) and the
    /// offline simulator run the same code.
    ///
    /// <para>Why: Skim Race counts a team's crystals together and gives the team one crystal per
    /// player. Two AI on one team that each fly at the nearest crystal start on the SAME one; the
    /// loser of that race is left aiming at a crystal that has just jumped to the next anchor, and the
    /// two fly through each other's trails. In the simulator a 2-AI team that splits finishes 28-51%
    /// sooner than one that does not (Docs/SKIM_RACE_AI.md section 13).</para>
    ///
    /// <para>The nearest pair is not always the best plan. Pilot A sitting between two crystals and
    /// pilot B beside only one of them: "A takes its nearest" sends B the long way round, while the
    /// cheapest plan gives B its neighbour and A the other. So the plan is searched, not built greedily
    /// - exactly up to <see cref="MaxExact"/> pilots or crystals (6! orderings at most), greedily
    /// (cheapest remaining pair first) beyond that.</para>
    /// </summary>
    public static class SkimRaceTeamAssignment
    {
        /// <summary>The largest team or crystal count the exact search takes.</summary>
        public const int MaxExact = 6;

        // Scratch, reused: the plan is rebuilt every frame and must not allocate. Main thread only.
        static readonly float[] s_cost = new float[MaxExact * MaxExact];
        static readonly int[] s_perm = new int[MaxExact];
        static readonly int[] s_best = new int[MaxExact];
        static readonly bool[] s_usedColumn = new bool[MaxExact];
        static bool[] s_taken = new bool[16];
        static float s_bestCost;

        /// <summary>
        /// Writes the plan into <paramref name="result"/>: <c>result[i]</c> is the index into
        /// <paramref name="crystals"/> pilot i should fly, or -1 when no crystal is left for it (more
        /// pilots than crystals). <paramref name="previous"/>[i] is what pilot i flew on the last plan
        /// (-1 = nothing, null = no last plan). The last plan is KEPT while it is complete - every pilot
        /// that can have a crystal has one, none twice - and the best plan is not cheaper than
        /// <paramref name="keepRatio"/> x its cost, so a near-tie cannot flip the team's aim every frame.
        /// <paramref name="result"/> must hold at least one slot per pilot. Returns the total distance of
        /// the plan written.
        /// </summary>
        public static float Assign(IReadOnlyList<Vector3> pilots, IReadOnlyList<Vector3> crystals,
            IReadOnlyList<int> previous, int[] result, float keepRatio)
        {
            int p = pilots.Count, c = crystals.Count;
            for (int i = 0; i < p; i++) result[i] = -1;
            if (p == 0 || c == 0) return 0f;

            float best = p <= MaxExact && c <= MaxExact
                ? Exact(pilots, crystals, result)
                : Greedy(pilots, crystals, result);

            float kept = PreviousCost(pilots, crystals, previous, Mathf.Min(p, c));
            if (kept < float.MaxValue && !(best < kept * keepRatio))
            {
                for (int i = 0; i < p; i++) result[i] = previous[i];
                return kept;
            }
            return best;
        }

        /// <summary>The cheapest plan, searched. Rows are the smaller set (pilots, or crystals when there
        /// are more pilots), so every row is matched; ties go to the first plan found in row order.</summary>
        static float Exact(IReadOnlyList<Vector3> pilots, IReadOnlyList<Vector3> crystals, int[] result)
        {
            int p = pilots.Count, c = crystals.Count;
            bool pilotRows = p <= c;
            int rows = pilotRows ? p : c, columns = pilotRows ? c : p;
            for (int r = 0; r < rows; r++)
            for (int k = 0; k < columns; k++)
                s_cost[r * MaxExact + k] = pilotRows
                    ? (crystals[k] - pilots[r]).magnitude
                    : (crystals[r] - pilots[k]).magnitude;

            for (int k = 0; k < columns; k++) s_usedColumn[k] = false;
            s_bestCost = float.MaxValue;
            Search(0, rows, columns, 0f);

            for (int r = 0; r < rows; r++)
            {
                if (pilotRows) result[r] = s_best[r];
                else result[s_best[r]] = r;
            }
            return s_bestCost;
        }

        static void Search(int row, int rows, int columns, float cost)
        {
            if (cost >= s_bestCost) return;
            if (row == rows)
            {
                s_bestCost = cost;
                System.Array.Copy(s_perm, s_best, rows);
                return;
            }
            for (int k = 0; k < columns; k++)
            {
                if (s_usedColumn[k]) continue;
                s_usedColumn[k] = true;
                s_perm[row] = k;
                Search(row + 1, rows, columns, cost + s_cost[row * MaxExact + k]);
                s_usedColumn[k] = false;
            }
        }

        /// <summary>Beyond <see cref="MaxExact"/>: the cheapest remaining pilot-crystal pair, repeatedly.</summary>
        static float Greedy(IReadOnlyList<Vector3> pilots, IReadOnlyList<Vector3> crystals, int[] result)
        {
            int p = pilots.Count, c = crystals.Count;
            ClearTaken(c);
            float total = 0f;
            for (int n = Mathf.Min(p, c); n > 0; n--)
            {
                float bestSqr = float.MaxValue;
                int bestPilot = -1, bestCrystal = -1;
                for (int i = 0; i < p; i++)
                {
                    if (result[i] >= 0) continue;
                    for (int j = 0; j < c; j++)
                    {
                        if (s_taken[j]) continue;
                        float d = (crystals[j] - pilots[i]).sqrMagnitude;
                        if (d < bestSqr) { bestSqr = d; bestPilot = i; bestCrystal = j; }
                    }
                }
                result[bestPilot] = bestCrystal;
                s_taken[bestCrystal] = true;
                total += Mathf.Sqrt(bestSqr);
            }
            return total;
        }

        /// <summary>The last plan's cost on today's positions, or float.MaxValue when it cannot be kept:
        /// missing, a crystal index out of range, a crystal flown twice, or fewer pilots matched than the
        /// best plan matches (a pilot without a crystal while one is free).</summary>
        static float PreviousCost(IReadOnlyList<Vector3> pilots, IReadOnlyList<Vector3> crystals,
            IReadOnlyList<int> previous, int matched)
        {
            if (previous == null || previous.Count < pilots.Count) return float.MaxValue;
            int c = crystals.Count;
            ClearTaken(c);
            int assigned = 0;
            float total = 0f;
            for (int i = 0; i < pilots.Count; i++)
            {
                int j = previous[i];
                if (j < 0) continue;
                if (j >= c || s_taken[j]) return float.MaxValue;
                s_taken[j] = true;
                assigned++;
                total += (crystals[j] - pilots[i]).magnitude;
            }
            return assigned == matched ? total : float.MaxValue;
        }

        static void ClearTaken(int count)
        {
            if (s_taken.Length < count) s_taken = new bool[Mathf.NextPowerOfTwo(count)];
            for (int j = 0; j < count; j++) s_taken[j] = false;
        }
    }
}

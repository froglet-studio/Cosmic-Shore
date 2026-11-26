using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// Which asked seat each squad vessel fills (Frogma Rev 146 A, "Seat asks", picked by Shombith 2026-11-26;
    /// Docs/MENU_REDESIGN_B.md section 236): the same best assignment <see cref="VesselArchetypes.Fit"/> totals, kept per
    /// seat so the quay can say "Squirrel, half" beside Harraser and Your Ark can stand "Fills Sapper" over a hull. Ties
    /// keep the squad's own order (seat 1 before seat 2), so the answer never flickers between equal assignments.
    /// </summary>
    public static class VoyageSeatFill
    {
        /// <summary>One asked seat: the archetype, the squad vessel that fills it (-1 for none) and how well (0, 0.5, 1).</summary>
        public readonly struct Filled
        {
            public readonly VesselArchetype Archetype;
            public readonly int Vessel;
            public readonly float Meets;

            public Filled(VesselArchetype archetype, int vessel, float meets)
            {
                Archetype = archetype;
                Vessel = vessel;
                Meets = meets;
            }

            /// <summary>Which of the vessel's archetypes meets the seat: 0 primary, 1 second, 2 third (-1 when unmet).</summary>
            public int Rank(IReadOnlyList<IReadOnlyList<VesselArchetype>> vessels)
            {
                if (Vessel < 0 || vessels == null || Vessel >= vessels.Count || vessels[Vessel] == null) return -1;
                for (int i = 0; i < vessels[Vessel].Count; i++)
                    if (vessels[Vessel][i] == Archetype) return i;
                return -1;
            }
        }

        /// <summary>Each seat of <paramref name="seats"/> in order, with the vessel (an index into
        /// <paramref name="vessels"/>) that fills it in the best assignment; the met values add up to
        /// <see cref="VesselArchetypes.Fit"/>.</summary>
        public static Filled[] Assign(IReadOnlyList<VesselArchetype> seats, IReadOnlyList<IReadOnlyList<VesselArchetype>> vessels)
        {
            int n = seats?.Count ?? 0;
            var result = new Filled[n];
            if (n == 0) return result;
            int count = vessels?.Count ?? 0;
            var pick = new int[n];
            var best = new int[n];
            for (int i = 0; i < n; i++) best[i] = -1;
            float bestTotal = -1f;
            var used = new bool[count];
            Search(seats, vessels, 0, 0f, pick, used, best, ref bestTotal);
            for (int i = 0; i < n; i++)
            {
                int v = best[i];
                result[i] = new Filled(seats[i], v, v < 0 ? 0f : VesselArchetypes.Meets(vessels[v], seats[i]));
            }
            return result;
        }

        /// <summary>The seat index <paramref name="vessel"/> fills in <paramref name="filled"/>, or -1.</summary>
        public static int SeatOf(Filled[] filled, int vessel)
        {
            if (filled == null) return -1;
            for (int i = 0; i < filled.Length; i++)
                if (filled[i].Vessel == vessel && filled[i].Meets > 0f) return i;
            return -1;
        }

        // Seats are three and squads three, so every assignment is tried; a later one wins only when strictly better.
        static void Search(IReadOnlyList<VesselArchetype> seats, IReadOnlyList<IReadOnlyList<VesselArchetype>> vessels, int seat,
            float total, int[] pick, bool[] used, int[] best, ref float bestTotal)
        {
            if (seat >= seats.Count)
            {
                if (total > bestTotal + 0.001f)
                {
                    bestTotal = total;
                    System.Array.Copy(pick, best, pick.Length);
                }
                return;
            }
            for (int v = 0; v < used.Length; v++)
            {
                if (used[v]) continue;
                float meets = VesselArchetypes.Meets(vessels[v], seats[seat]);
                if (meets <= 0f) continue;
                used[v] = true;
                pick[seat] = v;
                Search(seats, vessels, seat + 1, total + meets, pick, used, best, ref bestTotal);
                used[v] = false;
            }
            pick[seat] = -1;
            Search(seats, vessels, seat + 1, total, pick, used, best, ref bestTotal);
        }
    }
}

using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The retention economy's arithmetic, pure so it is tested without a scene (RetentionPayTests). Each rule names the
    /// Loop Atlas hole it fills (Docs/LoopAtlas/model/loops/06-arcs.yaml, 05-voyages.yaml, 02-toybox.yaml) and the
    /// persona it was found on (Docs/LoopAtlas/personas/journeys.yaml, Docs/LoopAtlas/sim/REPORT.md).
    /// </summary>
    public static class RetentionPay
    {
        /// <summary>
        /// The place a party member is paid by (arc.social hole: "a party match pays every member by the party's best
        /// placing"): of the domains this player's party flew for in the match, the one placed best. A domain no party
        /// member flew for is never chosen, so a player who is not in a party (or whose party is not in this match) is
        /// paid by their own domain, as before. Solo losing still pays nothing.
        /// </summary>
        /// <param name="local">The local player's own domain.</param>
        /// <param name="placement">Domains best first (the scoreboard's placement order).</param>
        /// <param name="partyDomains">The domains the local player's party members flew for in this match.</param>
        public static Domains PayDomain(Domains local, IReadOnlyList<Domains> placement, IEnumerable<Domains> partyDomains)
        {
            if (placement == null || partyDomains == null) return local;
            int best = IndexOf(placement, local);
            var pick = local;
            foreach (var d in partyDomains)
            {
                int i = IndexOf(placement, d);
                if (i < 0) continue;
                if (best < 0 || i < best)
                {
                    best = i;
                    pick = d;
                }
            }
            return pick;
        }

        /// <summary>
        /// Wallet crystals a docking banks (P10, one currency): <paramref name="perJob"/> for every job finished since the
        /// last dock, never more than <paramref name="cap"/>. A voyage that failed its job or lost the Ark finished none.
        /// </summary>
        public static int VoyageWalletCrystals(int jobsDone, int perJob, int cap)
        {
            if (jobsDone <= 0 || perJob <= 0) return 0;
            long pay = (long)jobsDone * perJob;
            return cap > 0 ? (int)Math.Min(pay, cap) : (int)Math.Min(pay, int.MaxValue);
        }

        /// <summary>
        /// What a finished painting pays (P16, making pays): <paramref name="perPainting"/> while fewer than
        /// <paramref name="perDay"/> have been paid today, then nothing (a painter is paid for making, not for farming).
        /// </summary>
        public static int PaintingCrystals(int paidToday, int perPainting, int perDay) =>
            perPainting > 0 && paidToday < Math.Max(0, perDay) ? perPainting : 0;

        static int IndexOf(IReadOnlyList<Domains> list, Domains d)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == d) return i;
            return -1;
        }
    }
}

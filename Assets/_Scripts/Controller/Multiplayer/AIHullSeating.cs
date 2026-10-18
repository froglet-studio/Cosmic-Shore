using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Which hull an AI seat flies, as pure functions - the one answer the launch panel's AI
    /// chips, the server's AI backfill and the arena hull backstop all read, so the lobby can
    /// never show a hull the spawner will not field.
    ///
    /// <para><b>Two kinds of AI seat.</b> An <b>opponent</b> seat is an AI on a domain no human
    /// flies; when the card pins an opponent hull (<c>SO_ArcadeGame.OpponentAIVessel</c> -
    /// Regatta's Squirrel) every opponent flies it, and those hulls sit outside arena seating's
    /// one-pilot-per-hull rule. An <b>ally</b> seat is an AI on a domain a human flies; it flies
    /// the hull a teammate picked for it on the launch panel, or the card's ordinary draw when
    /// nobody picked (<see cref="VesselClassType.Random"/>).</para>
    ///
    /// <para><b>The host picks for every seat</b> (2026-10-14, "test 5 vessels on Regatta"): the
    /// host may step ANY AI chip, an opponent's included, so one person can put a whole mixed grid
    /// on the course; a pick on an opponent seat replaces the pinned hull for that seat only. A
    /// guest still picks only for their own team's allies.</para>
    /// </summary>
    public static class AIHullSeating
    {
        /// <summary>True for a real hull, false for the Any / Random "no pick" sentinels.</summary>
        public static bool IsConcrete(VesselClassType hull) =>
            hull != VesselClassType.Any && hull != VesselClassType.Random;

        /// <summary>
        /// Whether an AI on <paramref name="seatDomain"/> is an OPPONENT seat that flies the
        /// card's pinned <paramref name="opponentHull"/>. False whenever the card pins nothing,
        /// so a card without the field keeps its ordinary draw for every seat.
        /// </summary>
        public static bool IsOpponentSeat(VesselClassType opponentHull, Domains seatDomain,
                                          ICollection<Domains> humanDomains) =>
            IsConcrete(opponentHull) && (humanDomains == null || !humanDomains.Contains(seatDomain));

        /// <summary>
        /// Whether a pilot may pick AI seat's hull: the host for every seat, anyone else only for
        /// an ally on their own team (<paramref name="requesterDomain"/> == <paramref name="seatDomain"/>).
        /// </summary>
        public static bool MayPick(bool requesterIsHost, Domains requesterDomain, Domains seatDomain) =>
            requesterIsHost || requesterDomain == seatDomain;

        /// <summary>
        /// The hull an AI seat flies before the card's draw: a concrete <paramref name="picked"/>
        /// hull wins (an opponent seat included); otherwise an opponent seat flies the card's pinned
        /// <paramref name="opponentHull"/>; otherwise <see cref="VesselClassType.Random"/> (the draw).
        /// The spawner still checks a pick is legal, built and free before it flies it.
        /// </summary>
        public static VesselClassType SeatHull(VesselClassType opponentHull, Domains seatDomain,
                                               ICollection<Domains> humanDomains, VesselClassType picked) =>
            IsConcrete(picked) ? picked
            : IsOpponentSeat(opponentHull, seatDomain, humanDomains) ? opponentHull
            : VesselClassType.Random;

        /// <summary>
        /// Step an ally seat's pick through <c>Random</c> (auto) followed by
        /// <paramref name="roster"/> in the card's order, in <paramref name="direction"/>
        /// (+1 / -1), skipping hulls in <paramref name="taken"/> - another pilot's claim under
        /// arena seating. Auto is never taken. Returns false when there is nothing to step to.
        /// </summary>
        public static bool TryCycle(IList<VesselClassType> roster, VesselClassType current, int direction,
                                    ICollection<VesselClassType> taken, out VesselClassType next)
        {
            next = current;
            if (roster == null || roster.Count == 0 || direction == 0) return false;

            // Position 0 is auto; positions 1..N are the roster.
            int n = roster.Count + 1;
            int at = IsConcrete(current) ? roster.IndexOf(current) + 1 : 0;
            int step = direction > 0 ? 1 : -1;

            for (int i = 1; i < n; i++)
            {
                int pos = ((at + step * i) % n + n) % n;
                var candidate = pos == 0 ? VesselClassType.Random : roster[pos - 1];
                if (IsConcrete(candidate) && taken != null && taken.Contains(candidate)) continue;
                if (candidate == current) continue;
                next = candidate;
                return true;
            }
            return false;
        }
    }
}

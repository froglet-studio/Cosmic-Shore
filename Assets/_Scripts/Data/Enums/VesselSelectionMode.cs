using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    // Always assign static numeric values: the mode is replicated as an int in the lobby snapshot.
    //
    // How the seats of an ARENA card (SO_ArcadeGame.ArenaRules) get their hulls - the row that replaced the arena
    // cards' intensity (Garrett, 2026-11-06). The host picks it on the launch panel; every guest sees it.
    //
    // 0 is not a member on purpose: it is what an unwritten field reads as, and every reader resolves it through
    // VesselSelectionRules.Resolve, which turns it into Pick (the behaviour every arena card had before the row).
    public enum VesselSelectionMode
    {
        /// <summary>Every human claims any free hull; teammates pick for their own team's AI. The behaviour before the row.</summary>
        Pick = 1,
        /// <summary>Every seat, human and AI, is dealt a random free hull.</summary>
        Random = 2,
        /// <summary>Seats take turns in snake order; a human picks for their own seat and for their team's AI, an all-AI team picks at random.</summary>
        Draft = 3,
        /// <summary>A human chooses one of their saved squads and is dealt one of its vessels at random; AI are dealt at random.</summary>
        Squad = 4,
    }

    /// <summary>
    /// The pure rules of <see cref="VesselSelectionMode"/>: resolving a replicated value, the snake draft order and the
    /// random deals. No engine dependency, so they run in an edit-mode test; the launch modal and the server call them.
    /// </summary>
    public static class VesselSelectionRules
    {
        /// <summary>What an arena card opens on, and what any unwritten value means.</summary>
        public const VesselSelectionMode Default = VesselSelectionMode.Pick;

        /// <summary><paramref name="value"/> when it is a real mode, otherwise <see cref="Default"/>.</summary>
        public static VesselSelectionMode Resolve(int value) =>
            value is >= (int)VesselSelectionMode.Pick and <= (int)VesselSelectionMode.Squad
                ? (VesselSelectionMode)value
                : Default;

        /// <summary>Whether a human may claim a hull of their own choosing right now.</summary>
        public static bool HumanPicksFreely(VesselSelectionMode mode) => mode == VesselSelectionMode.Pick;

        /// <summary>Whether the server deals each human their hull (Random, and Squad from the chosen squad).</summary>
        public static bool HumanIsDealt(VesselSelectionMode mode) =>
            mode is VesselSelectionMode.Random or VesselSelectionMode.Squad;

        /// <summary>Whether every AI seat is dealt a random hull by the host (every mode but Pick and Draft).</summary>
        public static bool AiIsDealt(VesselSelectionMode mode) =>
            mode is VesselSelectionMode.Random or VesselSelectionMode.Squad;

        /// <summary>One turn of a draft: which team picks, and which of its seats (in the team's seat order).</summary>
        public readonly struct DraftTurn : IEquatable<DraftTurn>
        {
            public readonly int Team;
            public readonly int Seat;

            public DraftTurn(int team, int seat)
            {
                Team = team;
                Seat = seat;
            }

            public bool Equals(DraftTurn o) => Team == o.Team && Seat == o.Seat;
            public override bool Equals(object obj) => obj is DraftTurn o && Equals(o);
            public override int GetHashCode() => Team * 397 ^ Seat;
            public override string ToString() => $"team {Team} seat {Seat}";
        }

        /// <summary>
        /// The snake draft order (Garrett, 2026-11-06): rounds go A, B, C then C, B, A, and so on, each team's turn
        /// filling its next unfilled seat. A team with no seat left is skipped, so teams of different sizes finish
        /// their extra seats at the end. <paramref name="seatsPerTeam"/> is the number of drafting seats each team
        /// has, in the order teams pick in the first round.
        /// </summary>
        public static List<DraftTurn> SnakeOrder(IReadOnlyList<int> seatsPerTeam)
        {
            var order = new List<DraftTurn>();
            if (seatsPerTeam == null || seatsPerTeam.Count == 0) return order;

            int teams = seatsPerTeam.Count;
            var filled = new int[teams];
            int total = 0;
            for (int t = 0; t < teams; t++) total += Math.Max(0, seatsPerTeam[t]);

            for (int round = 0; order.Count < total; round++)
            {
                bool forward = round % 2 == 0;
                for (int k = 0; k < teams; k++)
                {
                    int t = forward ? k : teams - 1 - k;
                    if (filled[t] >= seatsPerTeam[t]) continue;
                    order.Add(new DraftTurn(t, filled[t]));
                    filled[t]++;
                }
            }
            return order;
        }

        /// <summary>
        /// A random hull from <paramref name="candidates"/> that is not in <paramref name="taken"/>, or false when every
        /// candidate is taken. Order-independent: a duplicate candidate does not weigh its hull more.
        /// </summary>
        public static bool TryDealRandom(IEnumerable<VesselClassType> candidates, ICollection<VesselClassType> taken,
                                         Random rng, out VesselClassType hull)
        {
            hull = VesselClassType.Random;
            if (candidates == null) return false;

            var free = new List<VesselClassType>();
            foreach (var c in candidates)
            {
                if (c is VesselClassType.Random or VesselClassType.Any) continue;
                if (taken != null && taken.Contains(c)) continue;
                if (!free.Contains(c)) free.Add(c);
            }
            if (free.Count == 0) return false;

            hull = free[(rng ?? new Random()).Next(free.Count)];
            return true;
        }

        /// <summary>
        /// The Squad deal: a random free vessel from the chosen squad's hulls (<paramref name="squadHulls"/>, the
        /// card's hulls only), and when every one of those is taken or none is on the card, a random free hull from
        /// <paramref name="cardHulls"/>. False only when the card has no free hull at all.
        /// </summary>
        public static bool TryDealFromSquad(IEnumerable<VesselClassType> squadHulls, IReadOnlyCollection<VesselClassType> cardHulls,
                                            ICollection<VesselClassType> taken, Random rng, out VesselClassType hull)
        {
            var onCard = new List<VesselClassType>();
            if (squadHulls != null && cardHulls != null)
                foreach (var h in squadHulls)
                    if (Contains(cardHulls, h)) onCard.Add(h);

            return TryDealRandom(onCard, taken, rng, out hull) || TryDealRandom(cardHulls, taken, rng, out hull);
        }

        static bool Contains(IReadOnlyCollection<VesselClassType> set, VesselClassType hull)
        {
            foreach (var h in set) if (h == hull) return true;
            return false;
        }

        /// <summary>
        /// The platform autopilot's skill (0..1) on an arena card, which used to be intensity x 0.25: now the AI
        /// difficulty the host picked (Easy 0.25 .. Legendary 1), so removing the intensity row does not flatten it.
        /// </summary>
        public static float ArenaAutopilotSkill(AIDifficulty difficulty) =>
            (int)AIDifficultyRules.Resolve(difficulty) * 0.25f;
    }
}

using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Which of its domain's rings a Skim Race pilot flies when it shares the domain with other pilots:
    /// ONE ring per pilot, the same one for the whole race, so each pilot takes one ring at a spawn point
    /// and flies on to the next spawn point - where the ring it just took has reappeared - instead of
    /// turning back for its teammate's.
    ///
    /// <para><b>What the game does</b> (read from <c>CrystalManager</c>, not assumed): Skim Race lays one
    /// ring per player, wearing that player's domain (<c>PlayerCountPlusExtra</c> with no extra,
    /// <c>spawnCrystalWithPlayerDomain</c>), all at the first anchor, and each ring walks the anchors on
    /// its own: a collected ring moves to ITS next anchor. So a domain with N pilots has N rings at a
    /// spawn point, and a pilot that takes one finds it again one spawn point on while the others still
    /// stand behind it. Under the nearest-ring rule (<see cref="SkimRaceTargetTracker"/>) that pilot
    /// turned round for the ring behind it, and its teammate flew at the same one.</para>
    ///
    /// <para><b>The rule.</b> The domain's pilots are ranked, and so are its rings; pilot k flies ring k.
    /// Both ranks come from facts every machine holds identically - the player's network id, the ring's
    /// crystal id - and never from positions, which two machines see a few units apart. That matters:
    /// the pilots of one domain may run on different machines (a host's hull and a guest's, each flown
    /// by its own pilot) with no memory in common, and two machines that split the rings by distance
    /// could each decide the OTHER pilot has the ring nobody is flying. A rule made of ranks cannot
    /// disagree. It also needs no memory: the choice is a function of the roster and the rings.</para>
    ///
    /// <para><b>Who counts as a pilot.</b> Every player of the domain, whoever flies the hull. Whether a
    /// teammate's hull on another machine is flown by a person or by an AI is not replicated, so the rule
    /// cannot tell them apart; a person on the team is therefore left their ring, like an AI teammate. A
    /// pilot alone on its domain is not sharing anything and keeps the nearest-ring rule exactly.</para>
    ///
    /// <para>Pure and read-only: facts in, an index out. AI pilots that all run on ONE machine are still
    /// planned together first (<see cref="SkimRaceTeamPlan"/>); this is the rule for every pilot that
    /// plan has nothing for.</para>
    /// </summary>
    public static class SkimRaceCoopShare
    {
        /// <summary>True when there is something to share: two or more pilots and two or more rings.</summary>
        public static bool Applies(int pilotCount, int ringCount) => pilotCount >= 2 && ringCount >= 2;

        /// <summary>
        /// Whether pilot <paramref name="pilotRank"/> of <paramref name="pilotCount"/> flies ring
        /// <paramref name="ringRank"/> of <paramref name="ringCount"/>. One ring per pilot when the counts
        /// match (the game's rule). More rings than pilots (a teammate left and its ring stayed): the
        /// spare rings are dealt round, so none is nobody's. Fewer rings than pilots: the pilots are
        /// dealt round the rings, so none is left without one.
        /// </summary>
        public static bool IsMine(int ringRank, int ringCount, int pilotRank, int pilotCount)
        {
            if (!Applies(pilotCount, ringCount)) return true;
            pilotRank = Mathf.Clamp(pilotRank, 0, pilotCount - 1);
            return ringCount >= pilotCount
                ? ringRank % pilotCount == pilotRank
                : pilotRank % ringCount == ringRank;
        }

        /// <summary>
        /// The order the domain's pilots are ranked in: network id first (the same number on every
        /// machine), then name, then roster position - the last two only separate players that have no
        /// network id (a session that is not networked, which is one machine).
        /// </summary>
        public static bool RanksBefore(ulong idA, string nameA, int indexA, ulong idB, string nameB, int indexB)
        {
            if (idA != idB) return idA < idB;
            int byName = string.CompareOrdinal(nameA, nameB);
            return byName != 0 ? byName < 0 : indexA < indexB;
        }

        static bool IsRing(in SkimRaceTargetTracker.Candidate c, Domains domain) =>
            c.Alive && !c.Embedded && c.Domain == domain;

        /// <summary>The rank of candidate <paramref name="i"/> among the domain's rings, by crystal id.
        /// A ring mid-collection keeps its rank: the ranks must not shift while one is being taken.</summary>
        static int RingRank(IReadOnlyList<SkimRaceTargetTracker.Candidate> candidates, int i, Domains domain)
        {
            int id = candidates[i].Id, rank = 0;
            for (int j = 0; j < candidates.Count; j++)
            {
                if (j == i) continue;
                var other = candidates[j];
                if (!IsRing(other, domain)) continue;
                if (other.Id < id || (other.Id == id && j < i)) rank++;
            }
            return rank;
        }

        /// <summary>
        /// The ring to fly at: index into <paramref name="candidates"/>, or -1 (a normal, short state:
        /// this pilot's ring was taken this frame and has not moved yet - the caller follows the track).
        /// <paramref name="pilotRank"/> is this pilot's rank among the <paramref name="pilotCount"/>
        /// pilots of its domain (<see cref="RanksBefore"/>). A pilot alone on its domain, or a domain
        /// with one ring, gets <see cref="SkimRaceTargetTracker.SelectIndex"/> unchanged. Otherwise the
        /// answer is this pilot's own ring - the nearest of them, with the tracker's hysteresis, when it
        /// has more than one - and NEVER a teammate's, however near.
        /// </summary>
        public static int SelectIndex(IReadOnlyList<SkimRaceTargetTracker.Candidate> candidates, Domains domain,
            Vector3 pilotPosition, int currentIndex, int pilotRank, int pilotCount)
        {
            int rings = 0;
            for (int i = 0; i < candidates.Count; i++)
                if (IsRing(candidates[i], domain)) rings++;
            if (!Applies(pilotCount, rings))
                return SkimRaceTargetTracker.SelectIndex(candidates, domain, pilotPosition, currentIndex);

            int best = -1;
            float bestSqr = float.MaxValue, currentSqr = -1f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!IsRing(c, domain) || !SkimRaceTargetTracker.IsValid(c, domain, pilotPosition)) continue;
                if (!IsMine(RingRank(candidates, i, domain), rings, pilotRank, pilotCount)) continue;
                float d = (c.Position - pilotPosition).sqrMagnitude;
                if (i == currentIndex) currentSqr = d;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }

            float keep = SkimRaceTargetTracker.Hysteresis * SkimRaceTargetTracker.Hysteresis;
            if (best >= 0 && currentSqr >= 0f && currentIndex != best && bestSqr > currentSqr * keep)
                return currentIndex;
            return best;
        }

        static readonly List<SkimRaceTargetTracker.Candidate> s_candidates = new(8);

        /// <summary>Applies <see cref="SelectIndex"/> to the live crystal registry. A pilot alone on its
        /// domain goes straight to <see cref="SkimRaceTargetTracker.Select"/>.</summary>
        public static Crystal Select(Domains domain, Vector3 pilotPosition, Crystal current, int pilotRank, int pilotCount)
        {
            if (pilotCount < 2) return SkimRaceTargetTracker.Select(domain, pilotPosition, current);

            var list = Crystal.Active;
            s_candidates.Clear();
            int currentIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                bool alive = c != null && c.isActiveAndEnabled;
                s_candidates.Add(new SkimRaceTargetTracker.Candidate
                {
                    Alive = alive,
                    Embedded = alive && c.IsEmbedded,
                    Exploding = alive && c.IsExploding,
                    Domain = alive ? c.ownDomain : Domains.Blue,
                    Position = alive ? c.transform.position : Vector3.zero,
                    Id = alive ? c.Id : 0,
                });
                if (alive && c == current) currentIndex = i;
            }

            int idx = SelectIndex(s_candidates, domain, pilotPosition, currentIndex, pilotRank, pilotCount);
            return idx >= 0 ? list[idx] : null;
        }

        /// <summary>
        /// This player's rank among the players of its domain, and how many they are, read from the
        /// roster (<see cref="RanksBefore"/>). Count 1, rank 0 when the roster does not hold the player.
        /// </summary>
        public static void RankInDomain(IReadOnlyList<IPlayer> players, IPlayer me, out int rank, out int count)
        {
            rank = 0;
            count = 1;
            if (players == null || me == null) return;
            int mine = -1;
            for (int i = 0; i < players.Count; i++)
                if (ReferenceEquals(players[i], me)) { mine = i; break; }
            if (mine < 0) return;

            var domain = me.Domain;
            ulong myId = NetId(me);
            string myName = me.Name;
            count = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.Domain != domain) continue;
                count++;
                if (i != mine && RanksBefore(NetId(p), p.Name, i, myId, myName, mine)) rank++;
            }
        }

        static ulong NetId(IPlayer p) => p is Player { IsSpawned: true } player ? player.PlayerNetId : 0UL;
    }
}

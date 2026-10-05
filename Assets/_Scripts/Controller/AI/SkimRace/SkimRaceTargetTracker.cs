using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Picks the crystal a Skim Race pilot should fly at, from the AUTHORITATIVE live registry
    /// (<see cref="Crystal.Active"/>) — never from a private copy of the course's anchor list.
    ///
    /// Skim Race spawns one crystal per player, wearing that player's domain
    /// (<c>CrystalManager.spawnCrystalWithPlayerDomain</c>), and the crystal is a persistent
    /// object the manager MOVES to its next anchor on every collection. So "the next crystal"
    /// is simply: a live, collectable crystal of this vessel's domain. Rules:
    /// <list type="bullet">
    /// <item>Wrong-domain crystals are ignored: <c>TeamCrystalImpactor</c> refuses them, so
    /// flying at one is wasted time.</item>
    /// <item>Embedded crystals (a lifeform's heart) are ignored — not a race pickup.</item>
    /// <item>A crystal mid-collection (<see cref="Crystal.IsExploding"/>, a 0.5 s latch) is still
    /// a valid AIM once it has moved away from the pilot — that is the next crystal's new
    /// position — but it is skipped while it still sits on top of the pilot, which is the frame
    /// it was collected and has not moved yet.</item>
    /// <item>With several valid crystals (teammates share a domain) the nearest wins, with a
    /// small hysteresis so a near-tie cannot flip the pilot's aim every frame.</item>
    /// </list>
    /// A missing target is a normal, transient state (between collection and respawn); callers
    /// handle it by following the track.
    /// </summary>
    public static class SkimRaceTargetTracker
    {
        public const float OnTopOfPilot = 25f;
        public const float Hysteresis = 0.85f;

        /// <summary>The facts about one crystal the selection rule reads. Pure data.</summary>
        public struct Candidate
        {
            public bool Alive;
            public bool Embedded;
            public bool Exploding;
            public Domains Domain;
            public Vector3 Position;
        }

        public static bool IsValid(in Candidate c, Domains domain, Vector3 pilotPosition)
        {
            if (!c.Alive || c.Embedded || c.Domain != domain) return false;
            if (c.Exploding && (c.Position - pilotPosition).sqrMagnitude < OnTopOfPilot * OnTopOfPilot)
                return false;
            return true;
        }

        /// <summary>
        /// The selection rule over plain candidates: index of the crystal to fly at, or -1.
        /// <paramref name="currentIndex"/> is the crystal flown at last tick (-1 = none).
        /// </summary>
        public static int SelectIndex(System.Collections.Generic.IReadOnlyList<Candidate> candidates,
            Domains domain, Vector3 pilotPosition, int currentIndex)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!IsValid(c, domain, pilotPosition)) continue;
                float d = (c.Position - pilotPosition).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }

            if (best >= 0 && currentIndex >= 0 && currentIndex < candidates.Count && currentIndex != best
                && IsValid(candidates[currentIndex], domain, pilotPosition))
            {
                float dc = (candidates[currentIndex].Position - pilotPosition).sqrMagnitude;
                if (bestSqr > dc * Hysteresis * Hysteresis) return currentIndex;
            }
            return best;
        }

        static readonly System.Collections.Generic.List<Candidate> s_candidates = new(8);

        /// <summary>Applies <see cref="SelectIndex"/> to the live crystal registry.</summary>
        public static Crystal Select(Domains domain, Vector3 pilotPosition, Crystal current)
        {
            var list = Crystal.Active;
            s_candidates.Clear();
            int currentIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                bool alive = c != null && c.isActiveAndEnabled;
                s_candidates.Add(new Candidate
                {
                    Alive = alive,
                    Embedded = alive && c.IsEmbedded,
                    Exploding = alive && c.IsExploding,
                    Domain = alive ? c.ownDomain : Domains.Blue,
                    Position = alive ? c.transform.position : Vector3.zero,
                });
                if (alive && c == current) currentIndex = i;
            }

            int idx = SelectIndex(s_candidates, domain, pilotPosition, currentIndex);
            return idx >= 0 ? list[idx] : null;
        }
    }
}

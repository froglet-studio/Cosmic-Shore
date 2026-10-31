using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>What a course message says (Docs/Voyages/SLICE.md §4 step 5).</summary>
    public enum VoyageCourseKind
    {
        /// <summary>The host set out: every friend deploys their own seat and follows.</summary>
        Embark = 0,
        /// <summary>The host took a job: the galaxy as the host knows it, the job and the leg's worlds.</summary>
        Leg = 1,
        /// <summary>The host's voyage ended away from the way home: every friend's voyage ends too.</summary>
        End = 2,
        /// <summary>The host set sail on an Ark escort: every friend sails the same escort (difficulty, seed, home pose)
        /// in the seat the host gave them; AI fills the seats nobody took.</summary>
        EscortSail = 3,
        /// <summary>The host's escort ended: every friend's escort ends too.</summary>
        EscortEnd = 4,
    }

    /// <summary>
    /// The host's voyage, sent to the party so every friend sails the same course (SLICE.md §5: each machine runs its
    /// own Ark, fauna and finds; the course, the jobs and the news are the host's). Plain fields only, for JsonUtility.
    /// </summary>
    [Serializable]
    public class VoyageCourse
    {
        public VoyageCourseKind Kind;
        public VoyageState State;
        public VoyageJob Job;
        public List<int> Route = new();

        // ── Ark escort (EscortSail) ──
        /// <summary>The escort's difficulty (1-9).</summary>
        public int Difficulty;
        /// <summary>The escort's seed: the corridor's headings and each world's hostile roll come from it, so every peer
        /// stands the same worlds in the same places.</summary>
        public int Seed;
        /// <summary>The host's pose when it set sail (the corridor's origin and heading on every peer).</summary>
        public float HomeX, HomeY, HomeZ, HomeQx, HomeQy, HomeQz, HomeQw;
        /// <summary>Client ids of the party's humans in seat order (seat 0 first, -1 = an AI seat).</summary>
        public List<long> SeatClients = new();
    }
}

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
    }
}

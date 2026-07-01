using System;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists First-Time-User-Experience completion + resume state to UGS Cloud Save,
    /// so the FTUE runs once per account and never repeats across devices/reinstalls.
    ///
    /// JSON example:
    /// {
    ///   "Completed": true,
    ///   "CurrentPhase": "Phase2_GameplayTimer",
    ///   "LastNodeId": "a1b2c3...",
    ///   "Version": 1
    /// }
    /// </summary>
    [Serializable]
    public class FTUECloudData
    {
        /// <summary>True once the player has finished the FTUE flow.</summary>
        public bool Completed;

        /// <summary>Name of the last <c>TutorialPhase</c> reached (resume/analytics aid).</summary>
        public string CurrentPhase;

        /// <summary>Stable node id of the last node reached (resume/analytics aid).</summary>
        public string LastNodeId;

        /// <summary>Schema version for forward-compatible migrations.</summary>
        public int Version = 1;
    }
}

using System;
using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persists the microgame drill's account facts to UGS Cloud Save
    /// (Docs/ModePreview/TRAINING_PLAN.md §3, §4.5). Deliberately NOT the quest cursor: the
    /// drill runs on every preview entry, long after any quest has finished.
    ///
    /// Every field only ever GROWS (a key turns true, a tip becomes seen, a lap gets faster), so
    /// the cloud copy and the local mirror merge without conflict: OR, union, min.
    ///
    /// JSON example:
    /// {
    ///   "CompletedAnyLesson": true,
    ///   "CompletedTwoThumbLesson": false,
    ///   "SeenTipIds": ["ability:Sparrow:Charge", "race-inside-line"],
    ///   "BestLaps": [ { "Mode": 45, "Vessel": 2, "Seconds": 41.3 } ],
    ///   "Version": 1
    /// }
    /// </summary>
    [Serializable]
    public class DrillProgressCloudData
    {
        /// <summary>The account has finished a Lesson, on any ship (D7).</summary>
        public bool CompletedAnyLesson;

        /// <summary>The account has finished a Lesson on a two-thumb ship (D7).</summary>
        public bool CompletedTwoThumbLesson;

        /// <summary>Mentor tips this account has been shown; they move to the end of the playlist.</summary>
        public List<string> SeenTipIds = new();

        /// <summary>Best practice lap per (mode, hull).</summary>
        public List<DrillBestLap> BestLaps = new();

        /// <summary>Schema version for forward-compatible migrations.</summary>
        public int Version = 1;
    }

    [Serializable]
    public class DrillBestLap
    {
        public GameModes Mode;
        public VesselClassType Vessel;
        public float Seconds;
    }
}

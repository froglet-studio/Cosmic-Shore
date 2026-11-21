using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where each (card, hull) microgame visit stopped, so re-entering resumes there instead of
    /// starting the Lesson over (Docs/ModePreview/TRAINING_PLAN.md §4.4: "re-entering resumes
    /// where it left off").
    ///
    /// <para><b>Session memory, on purpose.</b> What persists across launches is already in
    /// <see cref="DrillProgressStore"/>: which Lessons are finished (so the Lesson is skippable)
    /// and which tips were said (so they go to the back of the playlist). This only remembers the
    /// PLACE inside one app session - a tap out to read the card, then straight back in. It is
    /// cleared when the domain reloads.</para>
    ///
    /// <para>Keyed on mode AND hull: the Lesson is the hull's and the playlist is the card's, so
    /// either one changing is a different visit.</para>
    /// </summary>
    public static class DrillResume
    {
        public struct Mark
        {
            public DrillPhase Phase;
            /// <summary>The Lesson step the visit was on.</summary>
            public int StepIndex;
            /// <summary>The Mentor tip to say next. Null = the top of the playlist.</summary>
            public string NextTipId;
        }

        static readonly Dictionary<(GameModes, VesselClassType), Mark> Marks = new();

        public static bool TryGet((GameModes, VesselClassType) key, out Mark mark) =>
            Marks.TryGetValue(key, out mark);

        public static void Set((GameModes, VesselClassType) key, Mark mark) => Marks[key] = mark;

        /// <summary>Forget every visit.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Marks.Clear();
    }
}

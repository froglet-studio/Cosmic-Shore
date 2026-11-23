using System;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Where the squad sails beside the Ark, and how it gets there at the start of a voyage (Shombith, 2026-11-17: "the
    /// vessel should always spawn side by side with the ark ... two besides the ark and one on top"). Seat 1 flies off
    /// the Ark's port side, seat 2 off its starboard side and seat 3 over its back, each this far clear of the hull's own
    /// extent. The intro launches them from the hangar's three pads to those slots (<c>ArkSortie</c>); with no intro
    /// they are set down there. Read by <c>ArkFormation</c>.
    /// </summary>
    [Serializable]
    public sealed class ArkFormationSettings
    {
        [Header("The three slots")]
        [SerializeField, Min(0f), Tooltip("Units of open water between the Ark's side (its widest part) and a side slot.")]
        float abeamMargin = 30f;

        [SerializeField, Min(0f), Tooltip("Units of open water between the Ark's back (its highest part) and the top slot.")]
        float aboveMargin = 25f;

        [SerializeField, Tooltip("How far forward of the hull's middle the slots sit, as a share of its length (0 = level " +
                                 "with the middle, 0.5 = level with the nose, negative = toward the jets).")]
        float alongShare = 0.1f;

        [Header("The launch from the hangar")]
        [SerializeField, Min(0f), Tooltip("Units a vessel rides above its hangar pad while it waits to launch.")]
        float padLift = 2f;

        [SerializeField, Min(0f), Tooltip("Units ahead of the hangar's doors a vessel flies straight out before it turns " +
                                          "for its slot (clear of the jaws).")]
        float exitClear = 40f;

        [SerializeField, Min(0f), Tooltip("Seconds between one vessel's launch and the next's.")]
        float launchStagger = 0.45f;

        [SerializeField, Min(0.3f), Tooltip("Seconds a vessel takes from its pad to its slot.")]
        float launchSeconds = 2.6f;

        [SerializeField, Range(0.1f, 0.6f), Tooltip("Share of the launch spent flying straight out of the hangar (the " +
                                                    "rest is the turn out and back into the slot).")]
        float exitShare = 0.3f;

        public float AbeamMargin => abeamMargin;
        public float AboveMargin => aboveMargin;
        public float AlongShare => alongShare;
        public float PadLift => padLift;
        public float ExitClear => exitClear;
        public float LaunchStagger => launchStagger;
        public float LaunchSeconds => launchSeconds;
        public float ExitShare => exitShare;

        /// <summary>Seconds from the first launch until the last of <paramref name="vessels"/> is in its slot.</summary>
        public float LaunchDuration(int vessels) => vessels <= 0 ? 0f : launchStagger * (vessels - 1) + launchSeconds;
    }
}

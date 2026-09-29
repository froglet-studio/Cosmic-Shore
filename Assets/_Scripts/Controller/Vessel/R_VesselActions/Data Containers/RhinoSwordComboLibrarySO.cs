using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The four channels the Rhino's sword pose is written in. The analog drive fills them
    /// from the trigger reparameterization (difference → yaw + roll, sum → pitch); a combo
    /// flourish fills them from an authored path. Because both live in the SAME space, a
    /// flourish blends in from — and back out to — the finger's pose with a plain lerp.
    /// Angles are degrees (sign conventions: +yaw = right, +roll = counterclockwise from the
    /// pilot's seat, +pitch = chop down / forward); thrust is a fraction of the blade's own
    /// hilt-anchor length, pushed out along the blade (a lunge), 0 = hilt at the mount.
    /// </summary>
    [Serializable]
    public struct SwordPoseChannels
    {
        public float yaw;
        public float roll;
        public float pitch;
        public float thrust;

        public SwordPoseChannels(float yaw, float roll, float pitch, float thrust)
        {
            this.yaw = yaw;
            this.roll = roll;
            this.pitch = pitch;
            this.thrust = thrust;
        }

        public static SwordPoseChannels Lerp(SwordPoseChannels a, SwordPoseChannels b, float t) => new(
            Mathf.LerpUnclamped(a.yaw, b.yaw, t),
            Mathf.LerpUnclamped(a.roll, b.roll, t),
            Mathf.LerpUnclamped(a.pitch, b.pitch, t),
            Mathf.LerpUnclamped(a.thrust, b.thrust, t));

        /// <summary>
        /// Each angle wrapped into (-180, 180]. The pose is Y(yaw)·R(roll)·P(pitch), and a
        /// 360° turn about any one axis is the identity ROTATION, so wrapping each channel
        /// independently never changes what is drawn — it only stops a blend from unwinding
        /// a full revolution the flourish already paid for.
        /// </summary>
        public SwordPoseChannels Wrapped() => new(WrapDegrees(yaw), WrapDegrees(roll), WrapDegrees(pitch), thrust);

        public static float WrapDegrees(float degrees)
        {
            float w = Mathf.Repeat(degrees + 180f, 360f) - 180f;
            return w <= -180f ? 180f : w;
        }
    }

    /// <summary>One authored point on a combo path. <c>time</c> is normalized (0..1) over the path's duration.</summary>
    [Serializable]
    public struct RhinoSwordComboKeyframe
    {
        [Range(0f, 1f)] public float time;
        public float yaw;
        public float roll;
        public float pitch;
        public float thrust;

        public SwordPoseChannels Channels => new(yaw, roll, pitch, thrust);
    }

    /// <summary>
    /// One flourish: the trigger sequence that calls it (e.g. "RLR"), whether it is the
    /// ENERGIZED variant, and its authored path through pose-channel space. The path is sampled
    /// with a piecewise cubic Hermite through the keys (finite-difference tangents, zero at the
    /// two ends), so it arrives at and leaves every beat smoothly and eases in and out of the
    /// whole flourish.
    /// </summary>
    [Serializable]
    public class RhinoSwordComboPath
    {
        [Tooltip("Trigger sequence, oldest first: R = right trigger, L = left. Two or three letters.")]
        [SerializeField] string sequence = "RR";
        [Tooltip("True for the upgraded flourish played while the blade is ENERGIZED (RHINO_ENERGY_SWORD.md).")]
        [SerializeField] bool energized;
        [Tooltip("Player-facing name of the flourish (documentation / future HUD callout).")]
        [SerializeField] string displayName = "";
        [Tooltip("Seconds the authored path takes, before the blend back to the triggers.")]
        [SerializeField, Min(0.05f)] float durationSeconds = 0.7f;
        [Tooltip("Keys in increasing normalized time; first at 0, last at 1.")]
        [SerializeField] List<RhinoSwordComboKeyframe> keys = new();

        [Header("Audio")]
        [Tooltip("FMOD event played when this flourish starts. Leave empty for silence.")]
        [SerializeField] EventReference sound;

        public string Sequence => sequence;
        public bool Energized => energized;
        public string DisplayName => displayName;
        public float DurationSeconds => Mathf.Max(0.05f, durationSeconds);
        public IReadOnlyList<RhinoSwordComboKeyframe> Keys => keys;
        public EventReference Sound => sound;
        public bool IsValid => keys is { Count: >= 2 };

        /// <summary>Pose at normalized time <paramref name="t01"/> (clamped to 0..1).</summary>
        public SwordPoseChannels Sample(float t01) => SamplePath(keys, t01);

        public SwordPoseChannels EndPose => IsValid ? keys[keys.Count - 1].Channels : default;

        /// <summary>
        /// Piecewise cubic Hermite through <paramref name="keys"/>. Interior tangents are the
        /// finite difference across the neighbouring keys (non-uniform Catmull–Rom), end tangents
        /// are zero. Pure — shared by the runtime and the edit-mode tests, and mirrored exactly by
        /// Tools/Build/author_rhino_sword_combos.py.
        /// </summary>
        public static SwordPoseChannels SamplePath(IReadOnlyList<RhinoSwordComboKeyframe> keys, float t01)
        {
            if (keys == null || keys.Count == 0) return default;
            if (keys.Count == 1) return keys[0].Channels;

            float t = Mathf.Clamp01(t01);
            int last = keys.Count - 1;
            if (t <= keys[0].time) return keys[0].Channels;
            if (t >= keys[last].time) return keys[last].Channels;

            int i = 0;
            while (i < last - 1 && t > keys[i + 1].time) i++;

            var k0 = keys[i];
            var k1 = keys[i + 1];
            float span = Mathf.Max(1e-5f, k1.time - k0.time);
            float u = (t - k0.time) / span;

            SwordPoseChannels m0 = Tangent(keys, i, span);
            SwordPoseChannels m1 = Tangent(keys, i + 1, span);

            float u2 = u * u, u3 = u2 * u;
            float h00 = 2f * u3 - 3f * u2 + 1f;
            float h10 = u3 - 2f * u2 + u;
            float h01 = -2f * u3 + 3f * u2;
            float h11 = u3 - u2;

            return new SwordPoseChannels(
                h00 * k0.yaw + h10 * m0.yaw + h01 * k1.yaw + h11 * m1.yaw,
                h00 * k0.roll + h10 * m0.roll + h01 * k1.roll + h11 * m1.roll,
                h00 * k0.pitch + h10 * m0.pitch + h01 * k1.pitch + h11 * m1.pitch,
                h00 * k0.thrust + h10 * m0.thrust + h01 * k1.thrust + h11 * m1.thrust);
        }

        // Tangent at key j, expressed per unit of the SEGMENT's local parameter (scaled by span).
        static SwordPoseChannels Tangent(IReadOnlyList<RhinoSwordComboKeyframe> keys, int j, float span)
        {
            if (j <= 0 || j >= keys.Count - 1) return default;
            var a = keys[j - 1];
            var b = keys[j + 1];
            float dt = Mathf.Max(1e-5f, b.time - a.time);
            float s = span / dt;
            return new SwordPoseChannels(
                (b.yaw - a.yaw) * s,
                (b.roll - a.roll) * s,
                (b.pitch - a.pitch) * s,
                (b.thrust - a.thrust) * s);
        }
    }

    /// <summary>
    /// The Rhino sword's COMBO library: every two- and three-press trigger sequence
    /// (RR, LL, RL, LR, RRR … LRR) maps to its own authored flourish, with an upgraded set
    /// for an ENERGIZED blade. Combos are an OVERLAY on the analog swordsmanship — holding a
    /// trigger still positions the sword exactly as before; only a rapid string of taps calls
    /// a flourish, which plays its path and then hands the pose back to the fingers.
    /// Authored by Tools/Build/author_rhino_sword_combos.py (--check); see RHINO_SWORD_COMBOS.md.
    /// </summary>
    [CreateAssetMenu(fileName = "RhinoSwordComboLibrary", menuName = "ScriptableObjects/Vessel Actions/RhinoSwordComboLibrarySO")]
    public class RhinoSwordComboLibrarySO : ScriptableObject
    {
        [Header("Detection (read from the replicated trigger mirrors, so every peer agrees)")]
        [Tooltip("Trigger value (0..1, deadzone-renormalized) a pull must reach to count as a PRESS.")]
        [SerializeField, Range(0.05f, 1f)] float pressThreshold = 0.5f;
        [Tooltip("Trigger value a pressed trigger must fall below to count as RELEASED. Below pressThreshold (hysteresis).")]
        [SerializeField, Range(0f, 1f)] float releaseThreshold = 0.2f;
        [Tooltip("Max seconds between one press and the next for the two to link into a combo.")]
        [SerializeField, Min(0.05f)] float comboWindowSeconds = 0.35f;
        [Tooltip("A press held longer than this is a HOLD (positioning the sword), not a tap, and breaks the chain. " +
                 "This is what keeps the analog swordsmanship free of accidental combos.")]
        [SerializeField, Min(0.05f)] float tapMaxHoldSeconds = 0.3f;
        [Tooltip("Two presses closer together than this, with both triggers down, are a CHORD — the energize " +
                 "stance — never a two-letter combo. The chain clears and stays clear until both release.")]
        [SerializeField, Min(0f)] float chordWindowSeconds = 0.08f;
        [Tooltip("Fraction of a three-press FINISHER during which new presses are ignored, so a finisher " +
                 "plays out instead of being cancelled by the fingers still coming off the triggers.")]
        [SerializeField, Range(0f, 1f)] float finisherLockoutFraction = 0.7f;

        [Header("Playback")]
        [Tooltip("Seconds to blend from the current pose onto the flourish's path.")]
        [SerializeField, Min(0.01f)] float blendInSeconds = 0.06f;
        [Tooltip("Seconds to blend from the flourish's end back to the live trigger pose.")]
        [SerializeField, Min(0.01f)] float blendOutSeconds = 0.18f;

        [Header("Flourishes")]
        [SerializeField] List<RhinoSwordComboPath> combos = new();

        public float PressThreshold => pressThreshold;
        public float ReleaseThreshold => Mathf.Min(releaseThreshold, pressThreshold);
        public float ComboWindowSeconds => comboWindowSeconds;
        public float TapMaxHoldSeconds => tapMaxHoldSeconds;
        public float ChordWindowSeconds => chordWindowSeconds;
        public float FinisherLockoutFraction => Mathf.Clamp01(finisherLockoutFraction);
        public float BlendInSeconds => Mathf.Max(0.01f, blendInSeconds);
        public float BlendOutSeconds => Mathf.Max(0.01f, blendOutSeconds);
        public IReadOnlyList<RhinoSwordComboPath> Combos => combos;

        public RhinoSwordComboDetector.Settings DetectorSettings => new()
        {
            PressThreshold = PressThreshold,
            ReleaseThreshold = ReleaseThreshold,
            ComboWindowSeconds = ComboWindowSeconds,
            TapMaxHoldSeconds = TapMaxHoldSeconds,
            ChordWindowSeconds = ChordWindowSeconds,
        };

        /// <summary>
        /// The flourish for <paramref name="sequence"/>. An energized blade gets the energized
        /// variant, falling back to the base one if none is authored — a missing upgrade must
        /// never make a combo vanish exactly when the player has earned the better one.
        /// </summary>
        public bool TryGet(string sequence, bool energized, out RhinoSwordComboPath path)
        {
            path = null;
            RhinoSwordComboPath fallback = null;
            for (int i = 0; i < combos.Count; i++)
            {
                var c = combos[i];
                if (c == null || !c.IsValid || !string.Equals(c.Sequence, sequence, StringComparison.Ordinal)) continue;
                if (c.Energized == energized) { path = c; return true; }
                if (!c.Energized) fallback = c;
            }
            path = fallback;
            return path != null;
        }
    }
}

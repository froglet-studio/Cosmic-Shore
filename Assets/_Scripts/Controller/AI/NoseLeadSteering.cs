using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure steering math of <see cref="AIPilot"/>'s stick and its NOSE LEAD (<see cref="AIPilot.NoseLeadGain"/>),
    /// lifted out of the MonoBehaviour so the EditMode tests (and the headless harness that compiles them against the
    /// Unity shim, <c>Tools/Build/nose_lead_harness/run.sh</c>) can run it alone. The offline models port the same math:
    /// <c>Tools/Build/ai_nose_lead.py</c> (<c>slerp_beyond</c>, <c>roll_stick</c>) and <c>manta_redline_sim.stick</c>.
    /// </summary>
    public static class NoseLeadSteering
    {
        /// <summary>The nose lead never turns the command further than this from the nose, degrees.</summary>
        public const float MaxTurnDegrees = 170f;

        /// <summary>
        /// Where the nose lead puts the command: <paramref name="nose"/> turned toward <paramref name="desired"/> by
        /// <paramref name="gain"/> times the angle between them along the same great circle (past
        /// <paramref name="desired"/> when the gain is over 1), but never more than <paramref name="maxExtraDegrees"/> past
        /// it nor <see cref="MaxTurnDegrees"/> from the nose. <paramref name="desired"/> itself when the two are
        /// (anti)parallel or the gain is 0 (<c>slerp_beyond</c> offline).
        /// </summary>
        public static Vector3 Direction(Vector3 nose, Vector3 desired, float gain, float maxExtraDegrees)
        {
            Vector3 n = nose.normalized, d = desired.normalized;
            if (gain <= 0f || n.sqrMagnitude < 1e-8f || d.sqrMagnitude < 1e-8f) return d;
            Vector3 perp = d - n * Vector3.Dot(n, d);
            if (perp.sqrMagnitude < 1e-8f) return d;
            perp.Normalize();
            float error = Vector3.Angle(n, d);
            float turn = Mathf.Min(gain * error, error + Mathf.Max(0f, maxExtraDegrees), MaxTurnDegrees) * Mathf.Deg2Rad;
            return n * Mathf.Cos(turn) + perp * Mathf.Sin(turn);
        }

        /// <summary>
        /// When the objective is behind the judged forward (<paramref name="forwardDot"/> under 0), turn hard on the
        /// cross product's own axis (full stick, 90 deg) instead of letting a vanishing cross product drift.
        /// </summary>
        public static void TurnHardIfBehind(float forwardDot, ref Vector3 localCross, ref float angleDegrees)
        {
            if (forwardDot >= 0f) return;
            localCross = localCross.sqrMagnitude > 1e-8f ? localCross.normalized : Vector3.up;
            angleDegrees = 90f;
        }

        /// <summary>
        /// AIPilot's stick for one error, (XSum, YSum), from the cross product of the judged forward and the steer
        /// direction already turned into the NOSE's frame (<paramref name="localCross"/>) and their dot
        /// (<paramref name="forwardDot"/>): scaled by <c>asin(|cross|^2 x aggressiveness / min(d^2, maxDistance))</c>
        /// degrees, full and on its own axis when the aim is behind (<see cref="TurnHardIfBehind"/>), each axis clamped
        /// to [-1, 1]. A single stick flies (x, -y).
        /// </summary>
        public static Vector2 ErrorStick(Vector3 localCross, float forwardDot, float sqrDistance, float maxDistance,
                                         float aggressiveness)
        {
            float angle = Mathf.Asin(Mathf.Clamp(localCross.sqrMagnitude * aggressiveness / Mathf.Min(sqrDistance, maxDistance), -1f, 1f)) * Mathf.Rad2Deg;
            TurnHardIfBehind(forwardDot, ref localCross, ref angle);
            return new Vector2(Mathf.Clamp(angle * localCross.y, -1, 1), Mathf.Clamp(angle * localCross.x, -1, 1));
        }

        /// <summary>
        /// The ROLL stick (<c>YDiff</c>) of a dual-stick hull with no belly target: the yaw stick
        /// (<paramref name="yawStick"/>, <c>XSum</c>) as AIPilot has always flown it, and NONE while the nose lead steers
        /// (<paramref name="leading"/>).
        /// <para>Why none. <c>VesselTransformer.Roll</c> turns the COMMAND about the NOSE's forward. Judged from the nose,
        /// with the command close behind it, that only banks the hull. The lead holds the command far from the nose by
        /// design (gain x the error, up to <see cref="AIPilot.NoseLeadMaxExtraDegrees"/> past the aim), so any roll stick
        /// swings that command round the nose: the led yaw stick, pinned full, corkscrews the hull (offline, 2026-11-07:
        /// the Manta finishes 0 of 36 Redline races from I2 up, the Urchin no I2-I4 Skein race, PRs #1829 and #1831),
        /// and even the unled one (the yaw stick it flies with the lead off) still whirls it, because a lagging nose is
        /// off the aim for most of every turn (the Manta at 2.5: 39 of 48 finished, I2-I4 medians 620-835 s against
        /// 180-540 s with the lead off). With no roll stick every race finishes (48 of 48). The lead steers yaw and pitch
        /// alone; the hull's bank is cosmetic to its path. A single stick cannot take this: its roll IS its yaw axis
        /// (<c>SingleStickVesselTransformer</c>), which is why the Serpent's lead wants a lower gain instead.</para>
        /// </summary>
        public static float RollStick(bool leading, float yawStick) => leading ? 0f : yawStick;
    }
}

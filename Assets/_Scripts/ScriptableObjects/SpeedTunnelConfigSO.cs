using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the speed-tunnel quasi dolly zoom (<c>VesselSpeedTunnel</c>,
    /// Docs/SPEED_TUNNEL.md).
    ///
    /// The law is ABSOLUTE, and that is the whole design: the effect is one global function
    /// of measured speed, so the SAME SPEED ON ANY VESSEL PRODUCES THE SAME VISUAL. A vessel
    /// that cruises faster sits deeper in the tunnel than one that crawls, because it *is*
    /// going faster. Do NOT add per-vessel windows, per-vessel scalars, or a normalization
    /// that makes every vessel reach full effect at its own top speed — that is a different
    /// design, it was considered and rejected, and it would break the one property the law
    /// exists to guarantee.
    ///
    /// There is consequently nothing to author per vessel and nothing to author per mode:
    /// this asset is the ONLY tuning surface for the entire fleet.
    ///
    /// Place the asset at <c>Resources/SpeedTunnelConfig</c>. With no asset the defaults
    /// below apply, so the law holds with zero authoring (the PrismOcclusionConfigSO
    /// precedent).
    /// </summary>
    [CreateAssetMenu(fileName = "SpeedTunnelConfig", menuName = "ScriptableObjects/Rendering/Speed Tunnel Config")]
    public class SpeedTunnelConfigSO : ScriptableObject
    {
        [Header("Law")]
        [Tooltip("Master switch, for A/B-ing the whole law in one place. This is a global debug " +
                 "switch, NOT an authoring surface — there is deliberately no way to turn the " +
                 "tunnel off for one vessel, one scene, or one game mode.")]
        [SerializeField] bool enabled = true;

        [Header("Speed Window (absolute, world units per second — fleet-wide)")]
        [Tooltip("Speed at which the tunnel begins to appear. Below this the effect is exactly " +
                 "zero and costs nothing. Absolute across the fleet: a vessel whose ordinary " +
                 "cruise is above this floor shows some tunnel all the time, which is correct — " +
                 "it is genuinely moving that fast.")]
        [Min(0f)]
        [SerializeField] float minEffectSpeed = 70f;

        [Tooltip("Speed at which the tunnel reaches full strength. Speeds above this are clamped, " +
                 "so a very fast vessel saturates rather than distorting further.")]
        [Min(0f)]
        [SerializeField] float maxEffectSpeed = 280f;

        [Header("Quasi Dolly Zoom")]
        [Tooltip("Degrees REMOVED from the camera's home field of view at full effect — the " +
                 "narrowing that magnifies the frame centre into the tunnel.")]
        [Min(0f)]
        [SerializeField] float fovDrop = 25f;

        [Tooltip("How far the Panini projection distance falls below the profile's own baseline " +
                 "at full effect. The gameplay profile ships 0.7; the tunnel relaxes it toward " +
                 "rectilinear as the FOV narrows.")]
        [Range(0f, 1f)]
        [SerializeField] float paniniDrop = 0.5f;

        [Header("Response")]
        [Tooltip("Per-second exponential tracking of the visual toward the speed-derived value. " +
                 "Speed is already continuous, so this only rounds off discontinuities " +
                 "(teleports, spawns, resets); keep it high so the visual matches the speed.")]
        [Min(0.01f)]
        [SerializeField] float responsiveness = 12f;

        public bool Enabled => enabled;
        public float MinEffectSpeed => minEffectSpeed;

        /// <summary>Clamped above the floor so a mis-authored asset can never invert the window.</summary>
        public float MaxEffectSpeed => Mathf.Max(maxEffectSpeed, minEffectSpeed + MinWindowWidth);

        public float FovDrop => fovDrop;
        public float PaniniDrop => paniniDrop;
        public float Responsiveness => Mathf.Max(0.01f, responsiveness);

        /// <summary>Narrowest legal window, so <see cref="Effect01"/> can never divide by zero.</summary>
        public const float MinWindowWidth = 0.01f;

        /// <summary>Lowest field of view the tunnel may ever leave a camera at.</summary>
        public const float MinFieldOfView = 1f;

        /// <summary>
        /// The law itself: measured speed → effect strength in [0, 1]. Pure, absolute, and
        /// identical for every vessel — this is the single expression that makes "the same
        /// speed on any vessel looks the same" true.
        /// </summary>
        public float Effect01(float speed)
        {
            if (!enabled) return 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(MinEffectSpeed, MaxEffectSpeed, speed));
        }

        /// <summary>Field of view for a camera whose home FOV is <paramref name="homeFov"/>.</summary>
        public float FovFor(float homeFov, float effect01) =>
            Mathf.Max(MinFieldOfView, homeFov - fovDrop * Mathf.Clamp01(effect01));

        /// <summary>Signed Panini offset from the profile's own baseline (always ≤ 0).</summary>
        public float PaniniOffsetFor(float effect01) => -paniniDrop * Mathf.Clamp01(effect01);

        /// <summary>
        /// One rule, three gates: the runtime, the FrogletTools validator and the edit-mode test
        /// all ask THIS method whether an authored asset is usable, so an asset that passes the
        /// audit cannot fail at runtime (the PrismOcclusionDiagnostics.IsCorridorCapable pattern).
        /// </summary>
        public bool IsSane(out string reason)
        {
            if (maxEffectSpeed <= minEffectSpeed)
            {
                reason = $"maxEffectSpeed ({maxEffectSpeed}) must exceed minEffectSpeed ({minEffectSpeed}) — " +
                         "an inverted window makes every vessel saturate instantly.";
                return false;
            }

            if (fovDrop <= 0f && paniniDrop <= 0f)
            {
                reason = "both fovDrop and paniniDrop are zero — the law is authored to do nothing.";
                return false;
            }

            if (fovDrop >= MinFieldOfView + 60f)
            {
                reason = $"fovDrop ({fovDrop}°) exceeds any sane home FOV, so the tunnel would " +
                         "bottom out at the minimum field of view for most of its range.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}

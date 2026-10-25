using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number of the CAMERA FEEL: the layer that makes the gameplay camera a little bit alive instead of
    /// nailing the hull to the centre of the screen (<c>Docs/CAMERA_FEEL.md</c>). Six parts, all subtle, all
    /// added on top of the settled follow pose by <see cref="CustomCameraController"/> and taken off again
    /// before the next frame's pose, so no follow, zoom, rear view or portal math ever sees them:
    /// <list type="bullet">
    /// <item><b>Inertia</b>: the camera lags the vessel's ACCELERATION (a boost pulls the hull ahead, a turn
    /// swings it into the turn), never its speed, so a steady cruise sits exactly where it always did.</item>
    /// <item><b>Kicks</b>: one damped spring every one-off event nudges: a skim pushes away from the prism, a
    /// hit shoves and tilts, a shot recoils, a crystal lifts.</item>
    /// <item><b>Shake</b>: trauma (0..1, felt squared) for hits and blasts.</item>
    /// <item><b>Rumble</b>: a fine, linear wobble while skimming or drifting.</item>
    /// <item><b>Drift lean</b>: the camera banks and looks a little along the slide.</item>
    /// <item><b>Breathing</b>: a very slow drift that is always on, so the view never reads as frozen.</item>
    /// </list>
    /// Distances are FRACTIONS OF THE FOLLOW DISTANCE, so the same number reads the same on screen on a
    /// Squirrel at 17 u and a Serpent at 250 u. Angles are degrees. The player's camera-shake setting
    /// (<c>AccessibilitySettings.CameraShakeIntensity</c>) scales all of it. Loaded from
    /// <c>Resources/CameraFeelConfig</c>; a missing asset falls back to these defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraFeelConfig", menuName = "ScriptableObjects/Camera/Camera Feel Config")]
    public class CameraFeelConfigSO : ScriptableObject
    {
        [Header("Master")]
        [Tooltip("Turns the whole camera feel on or off. Off leaves the camera exactly as it was before the feel " +
                 "existed (the older per-ability Shake calls still play).")]
        [SerializeField] bool feelEnabled = true;

        [Tooltip("Scales every part of the feel at once. 1 = as authored; the player's camera-shake setting " +
                 "multiplies on top of this.")]
        [Range(0f, 2f)]
        [SerializeField] float strength = 1f;

        [Header("Inertia: the camera lags the vessel's acceleration")]
        [Tooltip("How quickly the lag settles, Hz. Lower = a heavier, floatier camera. A vessel's inertia " +
                 "(below) divides this by its square root.")]
        [Min(0.05f)]
        [SerializeField] float inertiaFrequency = 1.3f;

        [Tooltip("Damping of the lag (1 = no overshoot, lower = it swings past and settles back).")]
        [Range(0.1f, 2f)]
        [SerializeField] float inertiaDamping = 0.85f;

        [Tooltip("The acceleration that reaches the full lag, in follow distances per second squared (so it reads " +
                 "the same at every camera distance). Lower = the camera lags more for the same boost or turn.")]
        [Min(0.01f)]
        [SerializeField] float fullLagAcceleration = 15f;

        [Tooltip("The furthest the camera lags, as a fraction of the follow distance. A vessel's inertia (below) " +
                 "multiplies it.")]
        [Range(0f, 0.3f)]
        [SerializeField] float maxLag = 0.06f;

        [Tooltip("How much of the lag shows along the view (falling back on a boost, closing in on a stop). " +
                 "1 = as much as sideways; lower keeps the hull's size on screen steadier.")]
        [Range(0f, 1f)]
        [SerializeField] float forwardLagShare = 0.7f;

        [Header("Kicks: the spring every one-off nudge rides")]
        [Tooltip("How fast a kick swings and settles, Hz.")]
        [Min(0.1f)]
        [SerializeField] float kickFrequency = 4.5f;

        [Tooltip("Damping of a kick (lower = more bounce before it settles).")]
        [Range(0.05f, 1.5f)]
        [SerializeField] float kickDamping = 0.42f;

        [Tooltip("The furthest kicks may push the camera together, as a fraction of the follow distance.")]
        [Range(0f, 0.3f)]
        [SerializeField] float maxKick = 0.08f;

        [Tooltip("The furthest kicks may tilt the camera together, degrees.")]
        [Range(0f, 15f)]
        [SerializeField] float maxKickAngle = 3f;

        [Header("Skim: a nudge away from each skimmed prism, and a little rumble")]
        [Tooltip("Peak nudge per skimmed prism, as a fraction of the follow distance, for a dead-centre skim.")]
        [Range(0f, 0.05f)]
        [SerializeField] float skimNudge = 0.004f;

        [Tooltip("Peak tilt per skimmed prism, degrees.")]
        [Range(0f, 2f)]
        [SerializeField] float skimTilt = 0.12f;

        [Tooltip("Share of the skim feel an edge-of-sphere skim still gets (1 at the skimmer's centre).")]
        [Range(0f, 1f)]
        [SerializeField] float skimEdgeFloor = 0.3f;

        [Tooltip("Shortest gap between two skim nudges, seconds. A dense skim still rumbles in between.")]
        [Min(0f)]
        [SerializeField] float skimMinInterval = 0.025f;

        [Tooltip("Rumble each skimmed prism adds (the rumble level runs 0..1).")]
        [Range(0f, 1f)]
        [SerializeField] float skimRumblePerSkim = 0.15f;

        [Tooltip("How fast the skim rumble fades once skimming stops, per second.")]
        [Min(0f)]
        [SerializeField] float skimRumbleDecay = 2.5f;

        [Header("Impact: the hull hits a prism, a creature's heart or another vessel's skimmer")]
        [Tooltip("Peak shove of a hit of weight 1, as a fraction of the follow distance.")]
        [Range(0f, 0.2f)]
        [SerializeField] float impactNudge = 0.035f;

        [Tooltip("Share of the shove that throws the camera FORWARD at the hull (the hull stops, the camera's " +
                 "momentum carries on); the rest pushes it away from what was hit.")]
        [Range(0f, 1f)]
        [SerializeField] float impactLurch = 0.45f;

        [Tooltip("Peak tilt of a hit of weight 1, degrees.")]
        [Range(0f, 10f)]
        [SerializeField] float impactTilt = 1.2f;

        [Tooltip("Shake trauma a hit of weight 1 adds (0..1; shake is felt as trauma squared).")]
        [Range(0f, 1f)]
        [SerializeField] float impactTrauma = 0.5f;

        [Tooltip("Shortest gap between two felt hits, seconds (a hull with two colliders reports one hit twice).")]
        [Min(0f)]
        [SerializeField] float impactMinInterval = 0.12f;

        [Header("What was hit: different objects land with different weight")]
        [Tooltip("The prism volume that lands with weight 1. Weight grows with the cube root of the volume, so a " +
                 "prism twice as long lands a little harder, not twice as hard.")]
        [Min(0.001f)]
        [SerializeField] float referencePrismVolume = 60f;

        [Tooltip("The lightest and heaviest a prism's size can make a hit.")]
        [SerializeField] Vector2 prismWeightRange = new(0.6f, 2f);

        [Tooltip("Weight multiplier for a shielded prism.")]
        [Min(0f)]
        [SerializeField] float shieldedWeight = 1.25f;

        [Tooltip("Weight multiplier for a super-shielded prism.")]
        [Min(0f)]
        [SerializeField] float superShieldedWeight = 1.6f;

        [Tooltip("Weight of the hull passing through another vessel's skimmer (a joust or a near miss), and of " +
                 "ramming a creature's heart.")]
        [Min(0f)]
        [SerializeField] float vesselBumpWeight = 1f;

        [Header("Hits taken: blasts and projectiles")]
        [Tooltip("Shake trauma a blast that catches the vessel adds.")]
        [Range(0f, 1f)]
        [SerializeField] float blastTrauma = 0.6f;

        [Tooltip("Peak shove of a blast, away from its centre, as a fraction of the follow distance.")]
        [Range(0f, 0.2f)]
        [SerializeField] float blastNudge = 0.04f;

        [Tooltip("Peak tilt of a blast, degrees.")]
        [Range(0f, 10f)]
        [SerializeField] float blastTilt = 1.5f;

        [Tooltip("Shake trauma a projectile striking the vessel adds.")]
        [Range(0f, 1f)]
        [SerializeField] float struckTrauma = 0.2f;

        [Tooltip("Peak shove of a projectile strike, away from the round, as a fraction of the follow distance.")]
        [Range(0f, 0.2f)]
        [SerializeField] float struckNudge = 0.015f;

        [Tooltip("Peak tilt of a projectile strike, degrees.")]
        [Range(0f, 10f)]
        [SerializeField] float struckTilt = 0.6f;

        [Tooltip("Shortest gap between two felt projectile strikes, seconds, so a stream of rounds rattles the " +
                 "camera without pinning the shake at full.")]
        [Min(0f)]
        [SerializeField] float struckMinInterval = 0.15f;

        [Header("Shots: recoil when the vessel fires")]
        [Tooltip("Peak push back per shot, as a fraction of the follow distance.")]
        [Range(0f, 0.05f)]
        [SerializeField] float shotKick = 0.006f;

        [Tooltip("Peak nose-up tilt per shot, degrees.")]
        [Range(0f, 3f)]
        [SerializeField] float shotPitch = 0.3f;

        [Tooltip("Shake trauma per shot. Tiny on purpose: a held trigger fires dozens a second.")]
        [Range(0f, 0.3f)]
        [SerializeField] float shotTrauma = 0.03f;

        [Tooltip("Shortest gap between two felt shots, seconds.")]
        [Min(0f)]
        [SerializeField] float shotMinInterval = 0.035f;

        [Header("Crystal pickup: a small lift")]
        [Tooltip("Peak lift when the vessel collects a crystal, as a fraction of the follow distance.")]
        [Range(0f, 0.05f)]
        [SerializeField] float pickupLift = 0.012f;

        [Tooltip("Peak tilt when the vessel collects a crystal, degrees.")]
        [Range(0f, 3f)]
        [SerializeField] float pickupTilt = 0.4f;

        [Header("Shake: trauma from hits and blasts")]
        [Tooltip("How fast trauma drains, per second (trauma 1 is gone in 1/this seconds).")]
        [Min(0.01f)]
        [SerializeField] float traumaDecay = 1.5f;

        [Tooltip("Pitch and yaw at full trauma, degrees.")]
        [Range(0f, 15f)]
        [SerializeField] float traumaAngle = 2.5f;

        [Tooltip("Roll at full trauma, degrees.")]
        [Range(0f, 15f)]
        [SerializeField] float traumaRoll = 3f;

        [Tooltip("Positional shake at full trauma, as a fraction of the follow distance.")]
        [Range(0f, 0.1f)]
        [SerializeField] float traumaOffset = 0.012f;

        [Tooltip("Shake frequency, Hz. About 9 reads as a weighty thud; 20 and above reads as a buzz.")]
        [Min(0.1f)]
        [SerializeField] float traumaFrequency = 9f;

        [Header("Rumble: the fine wobble of skimming and drifting")]
        [Tooltip("Wobble at full rumble, degrees. It is felt linearly, not squared, so it stays gentle.")]
        [Range(0f, 3f)]
        [SerializeField] float rumbleAngle = 0.3f;

        [Tooltip("Rumble frequency, Hz.")]
        [Min(0.1f)]
        [SerializeField] float rumbleFrequency = 6.5f;

        [Header("Drift: lean into the slide")]
        [Tooltip("The slip angle (between where the hull points and where it travels) that counts as a full " +
                 "drift, degrees.")]
        [Range(1f, 90f)]
        [SerializeField] float driftFullSlipDegrees = 35f;

        [Tooltip("Bank at a full drift, degrees: the camera leans into the turn.")]
        [Range(-10f, 10f)]
        [SerializeField] float driftRoll = 2f;

        [Tooltip("Look along the slide at a full drift, degrees.")]
        [Range(-10f, 10f)]
        [SerializeField] float driftYaw = 1.5f;

        [Tooltip("Swing out toward the side the hull is sliding from at a full drift, as a fraction of the " +
                 "follow distance.")]
        [Range(0f, 0.2f)]
        [SerializeField] float driftOffset = 0.02f;

        [Tooltip("Rumble level at a full drift (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] float driftRumble = 0.5f;

        [Tooltip("How fast the lean eases in and out, per second.")]
        [Min(0.01f)]
        [SerializeField] float driftEase = 5f;

        [Header("Breathing: always on, very slow")]
        [Tooltip("Slow drift of the view, degrees. 0 turns it off.")]
        [Range(0f, 2f)]
        [SerializeField] float breathAngle = 0.1f;

        [Tooltip("Breathing frequency, Hz.")]
        [Min(0.01f)]
        [SerializeField] float breathFrequency = 0.15f;

        [Header("Per vessel: each hull carries its own inertia and weight")]
        [Tooltip("Hulls not listed use inertia 1 and weight 1. Inertia > 1 is a heavier camera: it lags further " +
                 "and settles slower. Weight scales how hard the hull's hits, shots and skims land on the camera.")]
        [SerializeField] VesselFeel[] vessels =
        {
            new() { vessel = VesselClassType.Squirrel, inertia = 0.8f, weight = 0.9f },
            new() { vessel = VesselClassType.Dolphin, inertia = 0.85f, weight = 0.9f },
            new() { vessel = VesselClassType.Urchin, inertia = 0.75f, weight = 0.9f },
            new() { vessel = VesselClassType.Sparrow, inertia = 0.9f, weight = 1f },
            new() { vessel = VesselClassType.Manta, inertia = 0.9f, weight = 1f },
            new() { vessel = VesselClassType.Rhino, inertia = 1.35f, weight = 1.25f },
            new() { vessel = VesselClassType.Grizzly, inertia = 1.3f, weight = 1.25f },
            new() { vessel = VesselClassType.Serpent, inertia = 1.15f, weight = 1.1f },
            new() { vessel = VesselClassType.Orca, inertia = 1.4f, weight = 1.3f },
            new() { vessel = VesselClassType.Butterfly, inertia = 1.1f, weight = 0.9f },
            new() { vessel = VesselClassType.Starling, inertia = 0.4f, weight = 0.5f },
        };

        [Serializable]
        public struct VesselFeel
        {
            public VesselClassType vessel;
            [Min(0f)] public float inertia;
            [Min(0f)] public float weight;
        }

        public bool Enabled => feelEnabled;
        public float Strength => Mathf.Max(0f, strength);

        public float InertiaFrequency => Mathf.Max(0.05f, inertiaFrequency);
        public float InertiaDamping => Mathf.Clamp(inertiaDamping, 0.1f, 2f);
        public float FullLagAcceleration => Mathf.Max(0.01f, fullLagAcceleration);
        public float MaxLag => Mathf.Max(0f, maxLag);
        public float ForwardLagShare => Mathf.Clamp01(forwardLagShare);

        public float KickFrequency => Mathf.Max(0.1f, kickFrequency);
        public float KickDamping => Mathf.Clamp(kickDamping, 0.05f, 1.5f);
        public float MaxKick => Mathf.Max(0f, maxKick);
        public float MaxKickAngle => Mathf.Max(0f, maxKickAngle);

        public float SkimNudge => Mathf.Max(0f, skimNudge);
        public float SkimTilt => Mathf.Max(0f, skimTilt);
        public float SkimEdgeFloor => Mathf.Clamp01(skimEdgeFloor);
        public float SkimMinInterval => Mathf.Max(0f, skimMinInterval);
        public float SkimRumblePerSkim => Mathf.Clamp01(skimRumblePerSkim);
        public float SkimRumbleDecay => Mathf.Max(0f, skimRumbleDecay);

        public float ImpactNudge => Mathf.Max(0f, impactNudge);
        public float ImpactLurch => Mathf.Clamp01(impactLurch);
        public float ImpactTilt => Mathf.Max(0f, impactTilt);
        public float ImpactTrauma => Mathf.Clamp01(impactTrauma);
        public float ImpactMinInterval => Mathf.Max(0f, impactMinInterval);

        public float VesselBumpWeight => Mathf.Max(0f, vesselBumpWeight);

        public float BlastTrauma => Mathf.Clamp01(blastTrauma);
        public float BlastNudge => Mathf.Max(0f, blastNudge);
        public float BlastTilt => Mathf.Max(0f, blastTilt);
        public float StruckTrauma => Mathf.Clamp01(struckTrauma);
        public float StruckNudge => Mathf.Max(0f, struckNudge);
        public float StruckTilt => Mathf.Max(0f, struckTilt);
        public float StruckMinInterval => Mathf.Max(0f, struckMinInterval);

        public float ShotKick => Mathf.Max(0f, shotKick);
        public float ShotPitch => Mathf.Max(0f, shotPitch);
        public float ShotTrauma => Mathf.Clamp01(shotTrauma);
        public float ShotMinInterval => Mathf.Max(0f, shotMinInterval);

        public float PickupLift => Mathf.Max(0f, pickupLift);
        public float PickupTilt => Mathf.Max(0f, pickupTilt);

        public float TraumaDecay => Mathf.Max(0.01f, traumaDecay);
        public float TraumaAngle => Mathf.Max(0f, traumaAngle);
        public float TraumaRoll => Mathf.Max(0f, traumaRoll);
        public float TraumaOffset => Mathf.Max(0f, traumaOffset);
        public float TraumaFrequency => Mathf.Max(0.1f, traumaFrequency);

        public float RumbleAngle => Mathf.Max(0f, rumbleAngle);
        public float RumbleFrequency => Mathf.Max(0.1f, rumbleFrequency);

        public float DriftFullSlipDegrees => Mathf.Clamp(driftFullSlipDegrees, 1f, 90f);
        public float DriftRoll => driftRoll;
        public float DriftYaw => driftYaw;
        public float DriftOffset => Mathf.Max(0f, driftOffset);
        public float DriftRumble => Mathf.Clamp01(driftRumble);
        public float DriftEase => Mathf.Max(0.01f, driftEase);

        public float BreathAngle => Mathf.Max(0f, breathAngle);
        public float BreathFrequency => Mathf.Max(0.01f, breathFrequency);

        /// <summary>
        /// How heavy a prism lands: its size (cube root of volume against <see cref="referencePrismVolume"/>,
        /// held inside <see cref="prismWeightRange"/>) times its shield tier. Pure, so it is tested directly.
        /// </summary>
        public float PrismWeight(float volume, bool shielded, bool superShielded)
        {
            float lo = Mathf.Max(0f, Mathf.Min(prismWeightRange.x, prismWeightRange.y));
            float hi = Mathf.Max(lo, Mathf.Max(prismWeightRange.x, prismWeightRange.y));
            float size = Mathf.Clamp(Mathf.Pow(Mathf.Max(0f, volume) / Mathf.Max(0.001f, referencePrismVolume), 1f / 3f), lo, hi);
            float tier = superShielded ? Mathf.Max(0f, superShieldedWeight) : shielded ? Mathf.Max(0f, shieldedWeight) : 1f;
            return size * tier;
        }

        /// <summary>This hull's inertia and weight; 1 and 1 for a hull the table does not list.</summary>
        public (float inertia, float weight) ProfileFor(VesselClassType vessel)
        {
            if (vessels != null)
                for (int i = 0; i < vessels.Length; i++)
                    if (vessels[i].vessel == vessel)
                        return (Mathf.Max(0f, vessels[i].inertia), Mathf.Max(0f, vessels[i].weight));
            return (1f, 1f);
        }

        // ------------------------------------------------------------------
        // Instance

        const string ResourcePath = "CameraFeelConfig";
        static CameraFeelConfigSO s_instance;
        static bool s_loadAttempted;

        // If s_instance ever goes null after the first attempt, the latch would otherwise skip
        // Resources.Load forever and silently serve CreateInstance code defaults.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_loadAttempted = false;
        }

        /// <summary>
        /// The fleet's one config. Falls back to an in-memory instance carrying the authored defaults above,
        /// so the feel is never silently off just because the asset is missing.
        /// </summary>
        public static CameraFeelConfigSO Instance
        {
            get
            {
                if (s_instance) return s_instance;
                if (!s_loadAttempted)
                {
                    s_loadAttempted = true;
                    s_instance = Resources.Load<CameraFeelConfigSO>(ResourcePath);
                }
                if (!s_instance)
                    s_instance = CreateInstance<CameraFeelConfigSO>();
                return s_instance;
            }
        }
    }
}

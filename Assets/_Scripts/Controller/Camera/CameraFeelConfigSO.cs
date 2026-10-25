using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number of the CAMERA FEEL: the layer that makes the gameplay camera a little bit alive instead of
    /// nailing the hull to the centre of the screen (<c>Docs/CAMERA_FEEL.md</c>). Five parts, all subtle, all
    /// added on top of the settled follow pose by <see cref="CustomCameraController"/> and taken off again
    /// before the next frame's pose, so no follow, zoom, rear view or portal math ever sees them:
    /// <list type="bullet">
    /// <item><b>G-force</b>: the camera lags the vessel's ACCELERATION (a boost pulls the hull ahead, a turn
    /// swings it into the turn) and a drift's SLIDE (the hull slides out across the frame), never its speed, so a
    /// steady cruise sits exactly where it always did; the same G tilts the camera like the pilot's neck. A bigger
    /// hull's pilot feels less of it, and more slowly.</item>
    /// <item><b>Kicks</b>: one damped spring every one-off event nudges: a skim pushes away from the prism, a
    /// hit shoves and tilts, a shot recoils, a crystal lifts.</item>
    /// <item><b>Shake</b>: trauma (0..1, felt squared) for hits and blasts.</item>
    /// <item><b>Rumble</b>: a fine, linear wobble while skimming or drifting.</item>
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

        [Header("G-force: the camera lags what the hull's motion pushes on the pilot")]
        [Tooltip("How quickly the G builds and settles, Hz. Lower = a heavier, floatier camera. A bigger hull " +
                 "responds slower (Hull size, below).")]
        [Min(0.05f)]
        [SerializeField] float inertiaFrequency = 1.3f;

        [Tooltip("Damping of the G (1 = no overshoot, lower = it swings a little past and settles back, the way a " +
                 "head does). 0.7 builds with an accelerating start and one small overshoot.")]
        [Range(0.1f, 2f)]
        [SerializeField] float inertiaDamping = 0.7f;

        [Tooltip("The acceleration that reaches the full lag, in follow distances per second squared (so it reads " +
                 "the same at every camera distance). Lower = the camera lags more for the same boost or turn.")]
        [Min(0.01f)]
        [SerializeField] float fullLagAcceleration = 15f;

        [Tooltip("The lag a full acceleration gives, as a fraction of the follow distance. Scaled by the hull's G " +
                 "(Hull size and the table, below).")]
        [Range(0f, 0.3f)]
        [SerializeField] float maxLag = 0.06f;

        [Tooltip("The furthest the G (lag and drift slide together) moves the camera, as a fraction of the follow " +
                 "distance; it rounds off toward this rather than stopping at it. Scaled by the hull's G.")]
        [Range(0f, 0.4f)]
        [SerializeField] float maxGOffset = 0.14f;

        [Tooltip("How much of the lag shows along the view (falling back on a boost, closing in on a stop). " +
                 "1 = as much as sideways; lower keeps the hull's size on screen steadier.")]
        [Range(0f, 1f)]
        [SerializeField] float forwardLagShare = 0.7f;

        [Tooltip("The pilot's neck: nose-up tilt at the full forward G (thrown back by a boost; a stop tips it " +
                 "forward), degrees.")]
        [Range(0f, 10f)]
        [SerializeField] float gPitch = 1.5f;

        [Tooltip("The pilot's neck: lean outward at the full sideways G (a turn or a drift), degrees.")]
        [Range(0f, 15f)]
        [SerializeField] float gRoll = 3.5f;

        [Header("Hull size: a bigger hull's pilot feels less G, and feels it more slowly")]
        [Tooltip("The hull size (the mean of its model's length, width and height, world units, measured when the " +
                 "camera starts following it) that feels exactly the numbers above.")]
        [Min(0.01f)]
        [SerializeField] float referenceHullSize = 5f;

        [Tooltip("How strongly size changes the G: G x (reference / size) ^ this. 0 = every hull the same.")]
        [Range(0f, 1.5f)]
        [SerializeField] float sizeGExponent = 0.35f;

        [Tooltip("The least and most G size alone can give a hull.")]
        [SerializeField] Vector2 sizeGRange = new(0.55f, 1.4f);

        [Tooltip("How strongly size slows the G's response: frequency x (reference / size) ^ this. 0 = every hull " +
                 "responds alike.")]
        [Range(0f, 1f)]
        [SerializeField] float sizeResponseExponent = 0.25f;

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

        [Header("Drift: the hull slides out across the frame")]
        [Tooltip("The slip angle (between where the hull points and where it travels) that counts as a full " +
                 "drift, degrees.")]
        [Range(1f, 90f)]
        [SerializeField] float driftFullSlipDegrees = 35f;

        [Tooltip("How far the hull slides off the centre at a full drift, as a fraction of the follow distance. It " +
                 "rides the G spring (an accelerating start, a small overshoot, a settle), leans the camera through " +
                 "the neck roll above, and is scaled by the hull's G.")]
        [Range(0f, 0.3f)]
        [SerializeField] float driftSlide = 0.08f;

        [Tooltip("The speed at which a drift slides in full, in follow distances per second; a slower slide slides " +
                 "less, so a drift at a crawl stays gentle.")]
        [Min(0.01f)]
        [SerializeField] float driftFullSpeed = 4f;

        [Tooltip("Rumble level at a full drift (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] float driftRumble = 0.5f;

        [Header("Breathing: always on, very slow")]
        [Tooltip("Slow drift of the view, degrees. 0 turns it off.")]
        [Range(0f, 2f)]
        [SerializeField] float breathAngle = 0.1f;

        [Tooltip("Breathing frequency, Hz.")]
        [Min(0.01f)]
        [SerializeField] float breathFrequency = 0.15f;

        [Header("Per vessel: each hull's character on top of its size")]
        [Tooltip("Hulls not listed use G 1 and weight 1. G multiplies what the hull's size already gives (agile " +
                 "drifters above 1, lumbering hulls below); how hard it maneuvers is measured from its motion, so this " +
                 "is character, not agility. Weight scales how hard its hits, shots and skims land on the camera.")]
        [SerializeField] VesselFeel[] vessels =
        {
            new() { vessel = VesselClassType.Squirrel, gForce = 1.15f, weight = 0.9f },
            new() { vessel = VesselClassType.Dolphin, gForce = 1.1f, weight = 0.9f },
            new() { vessel = VesselClassType.Manta, gForce = 1.1f, weight = 1f },
            new() { vessel = VesselClassType.Urchin, gForce = 1f, weight = 0.9f },
            new() { vessel = VesselClassType.Sparrow, gForce = 1f, weight = 1f },
            new() { vessel = VesselClassType.Rhino, gForce = 0.85f, weight = 1.25f },
            new() { vessel = VesselClassType.Grizzly, gForce = 0.85f, weight = 1.25f },
            new() { vessel = VesselClassType.Serpent, gForce = 0.9f, weight = 1.1f },
            new() { vessel = VesselClassType.Orca, gForce = 0.9f, weight = 1.3f },
            new() { vessel = VesselClassType.Butterfly, gForce = 0.9f, weight = 0.9f },
            new() { vessel = VesselClassType.Starling, gForce = 0.4f, weight = 0.5f },
        };

        [Serializable]
        public struct VesselFeel
        {
            public VesselClassType vessel;
            [Min(0f)] public float gForce;
            [Min(0f)] public float weight;
        }

        public bool Enabled => feelEnabled;
        public float Strength => Mathf.Max(0f, strength);

        public float InertiaFrequency => Mathf.Max(0.05f, inertiaFrequency);
        public float InertiaDamping => Mathf.Clamp(inertiaDamping, 0.1f, 2f);
        public float FullLagAcceleration => Mathf.Max(0.01f, fullLagAcceleration);
        public float MaxLag => Mathf.Max(0f, maxLag);
        public float MaxGOffset => Mathf.Max(0f, maxGOffset);
        public float GPitch => Mathf.Max(0f, gPitch);
        public float GRoll => Mathf.Max(0f, gRoll);
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
        public float DriftSlide => Mathf.Max(0f, driftSlide);
        public float DriftFullSpeed => Mathf.Max(0.01f, driftFullSpeed);
        public float DriftRumble => Mathf.Clamp01(driftRumble);

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

        /// <summary>This hull's table row: its G character and its weight; 1 and 1 for a hull the table does not list.</summary>
        public (float gForce, float weight) ProfileFor(VesselClassType vessel)
        {
            if (vessels != null)
                for (int i = 0; i < vessels.Length; i++)
                    if (vessels[i].vessel == vessel)
                        return (Mathf.Max(0f, vessels[i].gForce), Mathf.Max(0f, vessels[i].weight));
            return (1f, 1f);
        }

        /// <summary>
        /// What a hull's SIZE alone does to its G: a bigger hull's pilot feels less of it (<c>gForce</c>, the ratio
        /// to <see cref="referenceHullSize"/> to the <see cref="sizeGExponent"/>, held in <see cref="sizeGRange"/>)
        /// and feels it build more slowly (<c>response</c>, the same ratio to <see cref="sizeResponseExponent"/>).
        /// A size of 0 (not measured) is the reference hull. Pure, so it is tested directly.
        /// </summary>
        public (float gForce, float response) SizeFeel(float hullSize)
        {
            if (!(hullSize > 0f)) return (1f, 1f);
            float ratio = Mathf.Max(0.001f, referenceHullSize) / hullSize;
            float lo = Mathf.Max(0f, Mathf.Min(sizeGRange.x, sizeGRange.y));
            float hi = Mathf.Max(lo, Mathf.Max(sizeGRange.x, sizeGRange.y));
            float g = Mathf.Clamp(Mathf.Pow(ratio, Mathf.Max(0f, sizeGExponent)), lo, hi);
            float response = Mathf.Clamp(Mathf.Pow(ratio, Mathf.Max(0f, sizeResponseExponent)), 0.4f, 2f);
            return (g, response);
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

using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's SLINGSHOT (<c>R_VesselActions/STOAT.md</c>): every number behind a trigger that lays
    /// an attractor–repulsor wormhole pair beside the hull. The pair itself is the black hole's
    /// (<c>Docs/BLACK_HOLE.md</c> §11) — this file only decides where, how big and how long.
    ///
    /// <para><b>The orbit</b> (the drift pair — the playtest's design, 2026-10-09). PRESS lays the
    /// attractor to the trigger's side, at the radius the squeeze asks for, and the repulsor mirrored on
    /// the other side at the same distance and size. HOLD and the hull ORBITS the attractor: the squeeze
    /// sets the radius live (harder = tighter, <see cref="OrbitRadiusWide"/> → <see cref="OrbitRadiusTight"/>),
    /// the hole's horizon is <see cref="OrbitHorizons"/> times smaller than the radius, and its strength is
    /// the one that makes that radius a circular orbit at the hull's speed. RELEASE and the hull slingshots
    /// out along the tangent with a boost; the pair falls together at <see cref="DriftSpeed"/> and
    /// annihilates where the horizons touch.</para>
    ///
    /// <para><b>The crystal style</b> (<c>BlackHoleConfig.crystalPairs</c>, §13) still lays its pair on
    /// RELEASE, sized by the squeeze between <see cref="MinStrength"/> and <see cref="MaxStrength"/>, at
    /// <see cref="AheadHorizons"/> / <see cref="HalfGapHorizons"/>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatSlingConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Sling Config")]
    public sealed class StoatSlingConfigSO : ScriptableObject
    {
        [Header("Squeeze")]
        [Tooltip("Squeeze curve exponent: 1 = linear, >1 keeps a light squeeze wide and makes the last bit of travel count.")]
        [SerializeField, Range(0.5f, 4f)] float holdExponent = 1.5f;

        [Tooltip("Keyboard / mouse / touch have no analog trigger: seconds of hold that count as a full squeeze.")]
        [SerializeField, Range(0.1f, 5f)] float holdRampSeconds = 3f;

        [Tooltip("The autopilot writes no trigger; its every sling is this fraction of a full squeeze.")]
        [SerializeField, Range(0f, 1f)] float autopilotHold01 = 0.5f;

        [Header("Orbit (drift pair: press lays it, hold orbits, release slings)")]
        [Tooltip("Orbit radius at the lightest squeeze, world units: a wide, gentle turn.")]
        [SerializeField, Min(1f)] float orbitRadiusWide = 150f;

        [Tooltip("Orbit radius fully squeezed, world units: the tightest turn.")]
        [SerializeField, Min(1f)] float orbitRadiusTight = 40f;

        [Tooltip("Space is reach: a multiplier on both orbit radii — more Space, the pair is laid further out and the turn is wider.")]
        [SerializeField] ElementalFloat orbitReach = ElementalFloat.Multiplier(1f, 1.4f, Element.Space, 1f);

        [Tooltip("How many of its own horizon radii the orbit sits out from the attractor (the repulsor matches it). " +
                 "Floored at 3, where the Paczyński–Wiita law stops allowing a stable circular orbit.")]
        [SerializeField, Range(3f, 20f)] float orbitHorizons = 6f;

        [Tooltip("How fast the orbit radius (and the hole's size) follows a changing squeeze, per second.")]
        [SerializeField, Range(0.1f, 20f)] float radiusFollowRate = 3f;

        [Tooltip("How hard the hull is held on the orbit's radius, per second (drift from steering and frame steps).")]
        [SerializeField, Range(0.1f, 20f)] float radialCorrectionRate = 5f;

        [Header("Slingshot (on release)")]
        [Tooltip("Boost on release at the lightest squeeze, as a fraction of the orbit speed.")]
        [SerializeField, Range(0f, 3f)] float slingBoostMin = 0.25f;

        [Tooltip("Boost on release at the deepest squeeze of the hold, as a fraction of the orbit speed.")]
        [SerializeField, Range(0f, 3f)] float slingBoostMax = 0.9f;

        [Tooltip("Seconds the boost takes to fade.")]
        [SerializeField, Range(0.1f, 5f)] float slingBoostSeconds = 1.5f;

        [Header("Life")]
        [Tooltip("How fast the two holes CLOSE on each other once let go (u/s); they annihilate where the horizons touch.")]
        [SerializeField, Range(0f, 200f)] float driftSpeed = 40f;

        [Tooltip("The longest a pair may be held, seconds; past it the pair lets go on its own and the hull slings.")]
        [SerializeField, Range(0.5f, 30f)] float lifetime = 12f;

        [Header("Crystal style (BlackHoleConfig.crystalPairs on — laid on release)")]
        [Tooltip("Strength of the slung crystal pair at the lightest press. Throat = strength × BlackHoleConfig.horizonPerStrength.")]
        [SerializeField] float minStrength = 2f;

        [Tooltip("Strength of the slung crystal pair at a fully buried trigger.")]
        [SerializeField] float maxStrength = 12f;

        [Tooltip("How far ahead of the hull the crystal pair's midpoint is laid, in throat radii.")]
        [SerializeField, Range(0f, 20f)] float aheadHorizons = 4f;

        [Tooltip("How far to either side each crystal pole sits, in throat radii. Space scales it. Floored so the two can never overlap.")]
        [SerializeField] ElementalFloat halfGapHorizons = ElementalFloat.Multiplier(6f, 9f, Element.Space, 1.5f);

        [Header("Autopilot sling")]
        [Tooltip("An autopilot slings when its target is at least this many degrees off the nose, to the side it wants to turn. 0 disables the AI sling.")]
        [SerializeField, Range(0f, 180f)] float aiSlingMinTurnDegrees = 30f;

        [Tooltip("Seconds between an autopilot's slings — one pair at a time, so roughly its lifetime.")]
        [SerializeField, Range(0.5f, 20f)] float aiSlingIntervalSeconds = 3f;

        [Tooltip("An autopilot does not sling at a target closer than this (world units) — a turn that short is the stick's.")]
        [SerializeField, Min(0f)] float aiSlingMinDistance = 120f;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played once when a squeeze begins (the wind-up). Empty = silent.")]
        [SerializeField] EventReference holdStartEvent;

        [Tooltip("Played once when the pair is slung. Empty = silent.")]
        [SerializeField] EventReference slingEvent;

        public float MinStrength => Mathf.Max(0.01f, Mathf.Min(minStrength, maxStrength));
        public float MaxStrength => Mathf.Max(MinStrength, maxStrength);
        public float HoldExponent => Mathf.Clamp(holdExponent, 0.5f, 4f);
        public float HoldRampSeconds => Mathf.Clamp(holdRampSeconds, 0.1f, 5f);
        public float AutopilotHold01 => Mathf.Clamp01(autopilotHold01);
        public float OrbitRadiusWide => Mathf.Max(1f, orbitRadiusWide);
        public float OrbitRadiusTight => Mathf.Clamp(orbitRadiusTight, 1f, OrbitRadiusWide);
        public ElementalFloat OrbitReach => orbitReach;
        public float OrbitHorizons => Mathf.Clamp(orbitHorizons, 3f, 20f);
        public float RadiusFollowRate => Mathf.Clamp(radiusFollowRate, 0.1f, 20f);
        public float RadialCorrectionRate => Mathf.Clamp(radialCorrectionRate, 0.1f, 20f);
        public float SlingBoostMin => Mathf.Clamp(slingBoostMin, 0f, 3f);
        public float SlingBoostMax => Mathf.Clamp(slingBoostMax, 0f, 3f);
        public float SlingBoostSeconds => Mathf.Clamp(slingBoostSeconds, 0.1f, 5f);
        public float AheadHorizons => Mathf.Clamp(aheadHorizons, 0f, 20f);
        public ElementalFloat HalfGapHorizons => halfGapHorizons;
        public float DriftSpeed => Mathf.Clamp(driftSpeed, 0f, 200f);
        public float Lifetime => Mathf.Clamp(lifetime, 0.5f, 30f);
        public float AiSlingMinTurnDegrees => Mathf.Clamp(aiSlingMinTurnDegrees, 0f, 180f);
        public float AiSlingIntervalSeconds => Mathf.Clamp(aiSlingIntervalSeconds, 0.5f, 20f);
        public float AiSlingMinDistance => Mathf.Max(0f, aiSlingMinDistance);
        public EventReference HoldStartEvent => holdStartEvent;
        public EventReference SlingEvent => slingEvent;
    }
}

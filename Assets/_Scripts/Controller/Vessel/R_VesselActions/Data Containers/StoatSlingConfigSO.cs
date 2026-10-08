using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's SLINGSHOT (<c>R_VesselActions/STOAT.md</c>): every number behind a trigger
    /// squeeze that lays a attractor–repulsor wormhole pair across the hull. The pair itself is the black
    /// hole's (<c>Docs/BLACK_HOLE.md</c> §11) — this file only decides how big, how far, how long.
    ///
    /// <para><b>Hold → size.</b> The trigger's depth (its hold time on a device without analog
    /// triggers) is the hole's STRENGTH between <see cref="MinStrength"/> and
    /// <see cref="MaxStrength"/>, raised to <see cref="HoldExponent"/> so a feathered tap stays a
    /// nudge and only a buried trigger buys the full slingshot. Strength is the attractor's one
    /// number: its horizon radius and its pull both follow from it.</para>
    ///
    /// <para><b>Geometry in horizon radii.</b> The midpoint sits <see cref="AheadHorizons"/>
    /// horizon radii ahead of the hull and the holes <see cref="HalfGapHorizons"/> either side on
    /// the hull's own horizontal — so a bigger hole is laid further out and wider, and the hull
    /// never spawns inside a horizon. The half-gap is the Space-scaled number: Space is reach.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatSlingConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Sling Config")]
    public sealed class StoatSlingConfigSO : ScriptableObject
    {
        [Header("Hold → size")]
        [Tooltip("Strength of the slung pair at the lightest press (a nudge). Horizon radius = strength × BlackHoleConfig.horizonPerStrength.")]
        [SerializeField] float minStrength = 2f;

        [Tooltip("Strength of the slung pair at a fully buried trigger (the ~90° slingshot).")]
        [SerializeField] float maxStrength = 12f;

        [Tooltip("Hold curve exponent: 1 = linear, >1 keeps a light squeeze small and makes the last bit of travel count.")]
        [SerializeField, Range(0.5f, 4f)] float holdExponent = 1.5f;

        [Tooltip("Keyboard / mouse / touch have no analog trigger: seconds of hold that count as a full squeeze.")]
        [SerializeField, Range(0.1f, 5f)] float holdRampSeconds = 1.2f;

        [Tooltip("The autopilot writes no trigger; its every sling is this fraction of a full squeeze.")]
        [SerializeField, Range(0f, 1f)] float autopilotHold01 = 0.5f;

        [Header("Geometry (in horizon radii of the slung hole)")]
        [Tooltip("How far ahead of the hull the pair's midpoint is laid, in horizon radii.")]
        [SerializeField, Range(0f, 20f)] float aheadHorizons = 2f;

        [Tooltip("How far to either side each hole sits, in horizon radii. Space scales it: more Space, wider sling. Floored so the two can never overlap.")]
        [SerializeField] ElementalFloat halfGapHorizons = ElementalFloat.Multiplier(4f, 8f, Element.Space, 1.5f);

        [Header("Life")]
        [Tooltip("How fast the two holes drift apart at birth (u/s); they stop at half the lifetime and fall back.")]
        [SerializeField, Range(0f, 200f)] float driftSpeed = 20f;

        [Tooltip("Seconds from birth to annihilation.")]
        [SerializeField, Range(0.5f, 30f)] float lifetime = 4f;

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
        public float AheadHorizons => Mathf.Clamp(aheadHorizons, 0f, 20f);
        public ElementalFloat HalfGapHorizons => halfGapHorizons;
        public float DriftSpeed => Mathf.Clamp(driftSpeed, 0f, 200f);
        public float Lifetime => Mathf.Clamp(lifetime, 0.5f, 30f);
        public EventReference HoldStartEvent => holdStartEvent;
        public EventReference SlingEvent => slingEvent;
    }
}

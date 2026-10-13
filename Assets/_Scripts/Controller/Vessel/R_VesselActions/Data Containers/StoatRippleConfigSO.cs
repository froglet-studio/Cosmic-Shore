using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's ANNIHILATION RIPPLE — its Charge ability, passive (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1c).
    /// Whenever this hull's pair annihilates — a dipole let go, an orbit's pair closing after its slingshot — the
    /// meeting point throws out a SPACE-TIME RIPPLE: a spherical wave that bends the scene as it passes
    /// (<see cref="StoatRipple"/>, drawn through the black-hole lens pass) and, where its front sweeps over them:
    /// <list type="bullet">
    /// <item><b>creatures</b> are killed (the platform's predation death, heart released — on the fauna's simulating
    /// machine);</item>
    /// <item><b>rival vessels</b> are STRIPPED: <see cref="PetalsPerElement"/> whole petals of every element they
    /// hold, shed as crystals around them (<see cref="ResourceSystem.AccrueElementalLoss"/>, classed
    /// <see cref="ElementalDebuffSources.Explosion"/> — a weapon another pilot aimed, so a ward against blasts
    /// stops it). With <b>Charge 5 — Siphon</b> the petals come to the Stoat instead.</item>
    /// </list>
    /// Nothing else: no prism is touched (Charge is anti-vessel / anti-fauna on this hull — the design brief,
    /// 2026-10-13), teammates and the owner are never stripped.
    ///
    /// <para><b>The dipole moment is the threat.</b> The ripple's radius is the separation the poles had when they
    /// were let go × <see cref="RadiusPerSeparation"/>, clamped, × Charge (<see cref="Reach"/>, read at the
    /// REPLICATED level — every peer sweeps the same sphere).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatRippleConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Ripple Config")]
    public sealed class StoatRippleConfigSO : ScriptableObject
    {
        [Header("Reach (Charge)")]
        [Tooltip("The ripple's radius per world unit of pole separation at the let-go: a pair held wide throws a wide ripple.")]
        [SerializeField, Range(0f, 4f)] float radiusPerSeparation = 0.6f;

        [Tooltip("The smallest ripple, world units (a pair let go with its poles together still throws one).")]
        [SerializeField, Min(0f)] float minRadius = 30f;

        [Tooltip("The largest ripple before Charge, world units.")]
        [SerializeField, Min(1f)] float maxRadius = 160f;

        [Tooltip("CHARGE's one dial: a multiplier on the ripple's radius — ×1 at rest, ×2 at level 10, floored at 1. Read at " +
                 "the REPLICATED level, so every peer sweeps the same sphere.")]
        [SerializeField] ElementalFloat reach = ElementalFloat.Multiplier(1f, 2f, Element.Charge, 1f);

        [Header("The wave")]
        [Tooltip("Seconds the front takes to reach the ripple's full radius (it decelerates — fast at birth).")]
        [SerializeField, Range(0.1f, 5f)] float expandSeconds = 0.9f;

        [Tooltip("Seconds the drawn wave lingers past full radius while it fades.")]
        [SerializeField, Range(0f, 3f)] float fadeSeconds = 0.5f;

        [Tooltip("The ring's half-thickness on screen, as a share of the radius (the band the scene is bent in).")]
        [SerializeField, Range(0.02f, 0.5f)] float bandWidth = 0.12f;

        [Tooltip("How far the wave bends the scene behind it, as a share of the screen at birth (fades to 0).")]
        [SerializeField, Range(0f, 0.1f)] float bend = 0.025f;

        [Tooltip("The red / blue split of the bend at the wavefront (0 = none): the ripple's colour fringe.")]
        [SerializeField, Range(0f, 0.5f)] float chromaticSplit = 0.12f;

        [Tooltip("How bright the wavefront's rim glows in the owner's domain colour (HDR, 0 = no rim).")]
        [SerializeField, Range(0f, 8f)] float rimGlow = 1.6f;

        [Header("What it does")]
        [Tooltip("Whole petals stripped from EVERY element a rival vessel holds when the front sweeps over it.")]
        [SerializeField, Range(0, 10)] int petalsPerElement = 1;

        [Tooltip("How fast a stripped petal is thrown off the rival, u/s (it settles as a crystal around them).")]
        [SerializeField, Range(0f, 60f)] float shedSpeed = 8f;

        [Tooltip("CHARGE level 5 — Siphon: the stripped petals come straight to the Stoat instead of being shed. Gated on " +
                 "the replicated unlock bit.")]
        [SerializeField] bool siphonOnChargeUpgrade = true;

        [Tooltip("Creatures inside the front are killed (on the fauna's simulating machine).")]
        [SerializeField] bool killsFauna = true;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played at the meeting point when the ripple is born. Empty = silent.")]
        [SerializeField] EventReference rippleEvent;

        [Tooltip("Played at a rival when the front strips it. Empty = silent.")]
        [SerializeField] EventReference stripEvent;

        public float ExpandSeconds => Mathf.Max(0.1f, expandSeconds);
        public float FadeSeconds => Mathf.Max(0f, fadeSeconds);
        public float BandWidth => Mathf.Clamp(bandWidth, 0.02f, 0.5f);
        public float Bend => Mathf.Clamp(bend, 0f, 0.1f);
        public float ChromaticSplit => Mathf.Clamp(chromaticSplit, 0f, 0.5f);
        public float RimGlow => Mathf.Max(0f, rimGlow);
        public int PetalsPerElement => Mathf.Max(0, petalsPerElement);
        public float ShedSpeed => Mathf.Max(0f, shedSpeed);
        public bool KillsFauna => killsFauna;
        public ElementalFloat Reach => reach;
        public EventReference RippleEvent => rippleEvent;
        public EventReference StripEvent => stripEvent;

        /// <summary>The ripple's full radius for a pair let go at <paramref name="separation"/>, before Charge.</summary>
        public float BaseRadius(float separation) =>
            StoatRippleMath.Radius(separation, radiusPerSeparation, minRadius, Mathf.Max(minRadius, maxRadius), 1f);

        /// <summary>The ripple's full radius for <paramref name="status"/>'s pair (Charge at the replicated level).</summary>
        public float Radius(float separation, IVesselStatus status) =>
            StoatRippleMath.Radius(separation, radiusPerSeparation, minRadius, Mathf.Max(minRadius, maxRadius),
                Mathf.Max(1f, reach.EvaluateReplicated(status)));

        /// <summary>Whether the Charge-5 Siphon is live for <paramref name="status"/> (the replicated bit).</summary>
        public bool Siphons(IVesselStatus status)
        {
            if (!siphonOnChargeUpgrade) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(Element.Charge);
        }
    }
}

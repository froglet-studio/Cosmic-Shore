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
    /// (<see cref="StoatRipple"/>, drawn by <see cref="StoatRipplePass"/>, no colour) and, where its front sweeps over them:
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
        [SerializeField] ElementalFloat reach = ElementalFloat.Multiplier(1f, 2f, Element.Charge, 0.5f);

        [Header("The wave (drawn: a space-time ripple with no colour, StoatRipple)")]
        // Subtle by design (2026-10-14, play: the first cut was "far too big and bright"). 2026-11-21 (Yash: "a
        // near-realistic space-time ripple wave, no domain color, just a wave of disturbance ... diminishing to
        // distance"): the scene behind the wave is bent and nothing is painted on it; the domain-colour ring is gone.
        [Tooltip("Seconds the front takes to reach the ripple's full radius (it decelerates — fast at birth). The front " +
                 "that strips rivals runs on the same clock.")]
        [SerializeField, Range(0.1f, 5f)] float expandSeconds = 0.9f;

        [Tooltip("Seconds the drawn wave lingers past full radius while it fades.")]
        [SerializeField, Range(0f, 3f)] float fadeSeconds = 0.5f;

        [Tooltip("The wave's wavelength, world units: the spacing of the front and the crests ringing down behind it.")]
        [SerializeField, Range(1f, 60f)] float wavelength = 9f;

        [Tooltip("How far the wave bends light at its front while it is young, radians (0.01 is about a hundredth of the " +
                 "screen's height). Nothing else is drawn: 0 = an invisible ripple.")]
        [SerializeField, Range(0f, 0.1f)] float bend = 0.012f;

        [Tooltip("Within this radius, world units, the wave keeps its full bend; past it the bend falls as this / the front's " +
                 "radius - the wave thins as it spreads, as a real one does (1/r).")]
        [SerializeField, Range(1f, 200f)] float nearRadius = 22f;

        [Tooltip("How much the wave brightens the scene on a crest and darkens it in a trough, a share of the scene's own " +
                 "light at full bend (no colour is added). It is what shows the wave over a plain background.")]
        [SerializeField, Range(0f, 0.3f)] float shimmer = 0.05f;

        [Tooltip("When the wave runs through the camera itself the whole view swells and settles: its share of the front's " +
                 "bend. 0 = the wave passes you unseen.")]
        [SerializeField, Range(0f, 1f)] float passShare = 0.35f;

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

        [Header("The scored hit (SILENCE.md: every scored hit and every petal theft are one event)")]
        [Tooltip("Every rival a front strips is ONE scored Debuff-class combat hit, admitted through " +
                 "CombatHitDrain.TryAdmitPetals on the machine that owns this Stoat: it silences the rival for the petals " +
                 "the ripple takes, and a mode's rule may pay it (Shockfront does). A ripple strikes each victim once " +
                 "anyway; this is the shared latch window between two ripples on the same rival, seconds.")]
        [SerializeField, Range(0f, 5f)] float hitCooldownSeconds = 0.5f;

        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager scores combat hits on. Fail-loud.")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played at the meeting point when the ripple is born. Empty = silent.")]
        [SerializeField] EventReference rippleEvent;

        [Tooltip("Played at a rival when the front strips it. Empty = silent.")]
        [SerializeField] EventReference stripEvent;

        public float ExpandSeconds => Mathf.Max(0.1f, expandSeconds);
        public float FadeSeconds => Mathf.Max(0f, fadeSeconds);
        public float Wavelength => Mathf.Max(1f, wavelength);
        public float Bend => Mathf.Clamp(bend, 0f, 0.1f);
        public float NearRadius => Mathf.Max(1f, nearRadius);
        public float Shimmer => Mathf.Clamp(shimmer, 0f, 0.3f);
        public float PassShare => Mathf.Clamp01(passShare);
        public int PetalsPerElement => Mathf.Max(0, petalsPerElement);
        public float ShedSpeed => Mathf.Max(0f, shedSpeed);
        public bool KillsFauna => killsFauna;
        public float HitCooldownSeconds => Mathf.Max(0f, hitCooldownSeconds);
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public ElementalFloat Reach => reach;
        public EventReference RippleEvent => rippleEvent;
        public EventReference StripEvent => stripEvent;

        /// <summary>The ripple's full radius for a pair let go at <paramref name="separation"/>, before Charge.</summary>
        public float BaseRadius(float separation) =>
            StoatRippleMath.Radius(separation, radiusPerSeparation, minRadius, Mathf.Max(minRadius, maxRadius), 1f);

        /// <summary>The ripple's full radius for <paramref name="status"/>'s pair (Charge at the replicated level).</summary>
        public float Radius(float separation, IVesselStatus status) =>
            StoatRippleMath.Radius(separation, radiusPerSeparation, minRadius, Mathf.Max(minRadius, maxRadius),
                reach.EvaluateReplicated(status));

        /// <summary>Whether the Charge-5 Siphon is live for <paramref name="status"/> (the replicated bit).</summary>
        public bool Siphons(IVesselStatus status)
        {
            if (!siphonOnChargeUpgrade) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(Element.Charge);
        }
    }
}

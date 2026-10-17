using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly third weapon (Mass 5): the burning plasma-claw flamethrower.
    /// Costs NO energy (per the class doc). Sets enemy prisms — and enemy trails,
    /// which are prisms — on fire with a spreading burn (PrismBurnManager).
    /// With Mass 5 active the burn STEALS on burnout instead of destroying.
    ///
    /// On the triggers (design ask, 2026-10-16) LT sprays from the LEFT front of the hull and RT
    /// from the RIGHT front, each forward, and the trigger's live PRESSURE sizes its claw between
    /// the Min and Max values below. The Max range must stay the Grizzly's shortest reach - the
    /// claw is always its closest-range weapon.
    ///
    /// <para><b>The look</b> (2026-10-16): four stiff RED whip cords per trigger
    /// (<see cref="GrizzlyClawWhips"/>), the Brittle Star's Charge Mode whips (<see cref="BrittleStarCord"/>) held
    /// near-rigid, rooted at one point and fanned slightly like a claw. Since 2026-10-17 the fingers ARE the weapon:
    /// what they touch burns, and there is no cone.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyFlamethrowerAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Flamethrower")]
    public class GrizzlyFlamethrowerActionSO : ShipActionSO
    {
        [Header("Claw (what burns: the fingers themselves - there is no cone)")]
        [SerializeField, Tooltip("Length of the claw's fingers at FULL pressure (and on devices with no analog trigger). Keep it the Grizzly's shortest-range weapon.")]
        float range = 55f;
        [SerializeField, Tooltip("Length of the fingers at the LIGHTEST pull.")]
        float minRange = 22f;
        [SerializeField, Tooltip("Outer fingers' spread at the LIGHTEST pull, as a fraction of Whip Spread Degrees (full pressure = the full spread).")]
        [Range(0f, 1f)] float minSpreadFraction = 0.6f;
        [SerializeField, Tooltip("How close to a finger a prism must be to catch fire, world units (plus half the prism's own size). The Brittle Star cords' cut, made a burn.")]
        float clawBurnRadius = 2f;
        [SerializeField, Tooltip("Ignites per second while held.")]
        float igniteTicksPerSecond = 4f;
        [SerializeField, Tooltip("Max prisms ignited per tick, per claw.")]
        int ignitesPerTick = 3;

        [Header("Muzzles (one per trigger)")]
        [SerializeField, Tooltip("How far each claw's root sits to its own side of the hull's centre line, in the hull's local units (LT left, RT right).")]
        float sideOffset = 6f;
        [SerializeField, Tooltip("How far ahead of the hull's pivot each claw's root sits, in the hull's local units.")]
        float noseOffset = 4f;
        [SerializeField, Tooltip("Degrees each claw is turned OUTWARD from straight ahead (LT left, RT right), so the two claws spread apart. 0 = both straight forward.")]
        float splayDegrees = 6f;

        [Header("Energy cell (one per claw)")]
        [SerializeField, Tooltip("Seconds a FULL cell lasts spraying the SMALLEST claw. Each trigger's claw has its own cell.")]
        float cellSeconds = 3f;
        [SerializeField, Tooltip("How much faster a cell empties at FULL pressure than at the lightest pull (design ask, 2026-10-16: 1.5x for larger flames). Scales linearly with pressure in between.")]
        float largeFlameDrainMultiplier = 1.5f;
        [SerializeField, Tooltip("Seconds an EMPTY cell takes to refill completely while its trigger is not spraying.")]
        float cellRechargeSeconds = 2f;
        [SerializeField, Tooltip("Seconds after a claw stops spraying before its cell starts refilling.")]
        float cellRechargeDelay = 0.25f;

        [Header("Whips (the claw's fingers - four per trigger)")]
        [SerializeField, Tooltip("Claw fingers (whip cords) drawn per trigger.")]
        int whipsPerSide = 4;
        [SerializeField, Tooltip("Degrees the OUTER fingers open to each side of the claw's aim. Every finger starts at one point and they fan flat across the hull's width, so a few degrees reads as a claw (design ask, 2026-10-17: 'one convergence point and they spread out slightly'). What the fingers touch burns, so this is also the claw's width.")]
        [Range(0f, 30f)] float whipSpreadDegrees = 7f;
        [SerializeField, Tooltip("How far each finger's tip hooks DOWN, as a fraction of the claw's length (0 = straight spikes). The hook grows toward the tip, so the fingers read as talons. Cosmetic only.")]
        [Range(0f, 0.5f)] float whipHook = 0.12f;
        [SerializeField, Tooltip("0 = the Brittle Star's loose whip, 1 = a straight saber. High on purpose: the claw's whips are 'more rigid' (design ask, 2026-10-16), so they lag a turn and shiver but stay nearly straight.")]
        [Range(0f, 1f)] float whipRigidity = 0.8f;
        [SerializeField, Tooltip("Joints per whip. More = smoother bends, more cost.")]
        int whipLinks = 10;
        [SerializeField, Tooltip("1/s^2: wave stiffness at rigidity 0 (the Brittle Star's 180).")]
        float whipLooseWave = 180f;
        [SerializeField, Tooltip("1/s^2: wave stiffness at rigidity 1 (the Brittle Star's 2500).")]
        float whipRigidWave = 2500f;
        [SerializeField, Tooltip("1/s: wave damping at rigidity 0 (the Brittle Star's 0.4).")]
        float whipLooseDamping = 0.4f;
        [SerializeField, Tooltip("1/s: wave damping at rigidity 1 (the Brittle Star's 14).")]
        float whipRigidDamping = 14f;
        [SerializeField, Tooltip("1/s^2: pull onto the straight line at full rigidity, x rigidity^2 (the Brittle Star's 1600).")]
        float whipPose = 1600f;
        [SerializeField, Tooltip("Smoothing points drawn between whip joints.")]
        int whipRenderSubdivisions = 4;
        [SerializeField, Tooltip("Whip width at the root, world units; it tapers to a fifth at the tip.")]
        float whipWidth = 2f;
        [SerializeField, Tooltip("Fresnel tube material (the Thresher chain's). Empty = an unlit runtime fallback.")]
        Material whipMaterial;
        [SerializeField, Tooltip("The whip's dark core colour.")]
        Color whipBodyColor = new Color(0.25f, 0.02f, 0.02f, 1f);
        [SerializeField, Tooltip("The whip's glowing rim colour at a full energy cell.")]
        Color whipRimColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField, Tooltip("Rim brightness left at an EMPTY cell, as a fraction of full, so a drained claw still shows its reach.")]
        [Range(0f, 1f)] float whipEmptyBrightness = 0.25f;

        public float Range => range;
        public float ClawBurnRadius => clawBurnRadius;
        public float IgniteTicksPerSecond => igniteTicksPerSecond;
        public int IgnitesPerTick => ignitesPerTick;
        public float SideOffset => sideOffset;
        public float NoseOffset => noseOffset;
        public float SplayDegrees => splayDegrees;
        public float CellRechargeDelay => cellRechargeDelay;
        public int WhipsPerSide => whipsPerSide;
        public float WhipSpreadDegrees => whipSpreadDegrees;
        public float WhipHook => whipHook;
        public float WhipRigidity => whipRigidity;
        public int WhipLinks => whipLinks;
        public float WhipLooseWave => whipLooseWave;
        public float WhipRigidWave => whipRigidWave;
        public float WhipLooseDamping => whipLooseDamping;
        public float WhipRigidDamping => whipRigidDamping;
        public float WhipPose => whipPose;
        public int WhipRenderSubdivisions => whipRenderSubdivisions;
        public float WhipWidth => whipWidth;
        public Material WhipMaterial => whipMaterial;
        public Color WhipBodyColor => whipBodyColor;
        public Color WhipRimColor => whipRimColor;
        public float WhipEmptyBrightness => whipEmptyBrightness;

        /// <summary>Cell fraction spent per second spraying at pressure 0..1: the lightest pull
        /// empties a full cell in <c>cellSeconds</c>, the largest <c>largeFlameDrainMultiplier</c>
        /// times faster.</summary>
        public float CellDrainPerSecond(float pressure01) =>
            Mathf.Lerp(1f, Mathf.Max(1f, largeFlameDrainMultiplier), Mathf.Clamp01(pressure01)) /
            Mathf.Max(0.05f, cellSeconds);

        /// <summary>Cell fraction refilled per second while the claw is idle.</summary>
        public float CellRechargePerSecond => 1f / Mathf.Max(0.05f, cellRechargeSeconds);

        /// <summary>Finger length for a trigger pressure 0..1 (1 = full).</summary>
        public float RangeForPressure(float pressure01) =>
            Mathf.Lerp(Mathf.Min(minRange, range), range, Mathf.Clamp01(pressure01));

        /// <summary>Outer fingers' spread, degrees, for a trigger pressure 0..1 (1 = full).</summary>
        public float SpreadForPressure(float pressure01) =>
            whipSpreadDegrees * Mathf.Lerp(Mathf.Clamp01(minSpreadFraction), 1f, Mathf.Clamp01(pressure01));

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.BeginSpray(this, vesselStatus, null);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.EndSpray(null);
    }
}

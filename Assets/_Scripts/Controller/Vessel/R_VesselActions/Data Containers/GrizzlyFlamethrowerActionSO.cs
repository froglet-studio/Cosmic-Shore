using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly third weapon: the burning plasma-claw flamethrower, the MASS weapon.
    /// Costs NO energy (per the class doc). Sets enemy prisms — and enemy trails,
    /// which are prisms — on fire with a spreading burn (PrismBurnManager).
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
    ///
    /// <para><b>Mass is what the claw FEEDS you</b> (2026-11-18, the element-placeholder pass). Every enemy prism the
    /// claw lights refills the Grizzly's energy (<see cref="energyPerIgnite"/>, 0.01 at rest -> 0.025 at level 10), the
    /// energy the cannon, the sniper and the Rush spend. Mass only grows what helps your own team (Garrett,
    /// 2026-11-16), so the claw's reach and bite stay fixed (that was the 2026-11-16 try, reverted) and the trail no
    /// longer grows with Mass (the placeholder before it). Read live at each tick, on the machine that runs the spray.</para>
    /// <para>Mass 5, Claimed Fire: the burn STEALS on burnout instead of destroying.</para>
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

        [Header("Slice (2026-10-17: the claw slices prisms as well as starting the burn)")]
        [SerializeField, Tooltip("On: every enemy prism a finger touches is SLICED (the Brittle Star cord's cut) and the prisms around the cut are ignited. Off: the fingers only ignite what they touch.")]
        bool sliceEnabled = true;
        [SerializeField, Tooltip("Most prisms one claw may slice in a frame (perf cap).")]
        int slicesPerFrame = 6;
        [SerializeField, Tooltip("Prisms within this many world units of a cut are set burning (up to Ignites Per Tick per claw per tick). The sliced prism itself is gone, so its neighbours carry the fire.")]
        float sliceIgniteRadius = 12f;
        [SerializeField, Tooltip("Debris speed as a fraction of the finger's own speed where it cut (the Brittle Star's 0.5).")]
        float sliceDebrisRestitution = 0.5f;
        [SerializeField, Tooltip("Cap on that debris speed, u/s (0 = none). A sweeping fingertip moves fast.")]
        float sliceDebrisSpeedLimit = 200f;
        [SerializeField, Tooltip("FMOD event played at each cut. Leave empty for silence (an unwired slot, never a placeholder).")]
        EventReference sliceEvent;

        [Header("Swipe on release (2026-11-23: each claw is one stroke of an X - up and out, then down and across)")]
        [SerializeField, Tooltip("On: letting go of a trigger SWIPES its claw instead of just vanishing: it rises up and OUT, then slashes down and ACROSS the hull's centre line, slicing everything the fingers pass through. LT's stroke runs upper-left to lower-right and RT's upper-right to lower-left, so the two strokes are the two sides of an X. Off: release ends the claw as before.")]
        bool swipeEnabled = true;
        [SerializeField, Tooltip("Seconds one swipe takes, rise and slash together. Short on purpose: a slash, not a sweep.")]
        float swipeSeconds = 0.3f;
        [SerializeField, Tooltip("Degrees of the whole stroke, top to bottom. It is centred on where the claw was held: half of it above and outside, half below and across.")]
        float swipeArcDegrees = 90f;
        [SerializeField, Tooltip("Share of Swipe Seconds spent RISING from where the claw was held up to the top of the stroke. The rest is the slash down.")]
        [Range(0.05f, 0.6f)] float swipeWindupFraction = 0.25f;
        [SerializeField, Tooltip("Direction of the swipe: 0 = straight down, 90 = straight across toward the centre line. 45 = the diagonal.")]
        [Range(0f, 90f)] float swipeDiagonalDegrees = 45f;
        [SerializeField, Tooltip("Energy cell spent by each swipe, as a fraction of a full cell. A claw whose cell is empty does not swipe.")]
        [Range(0f, 1f)] float swipeCellCost = 0.1f;
        [SerializeField, Tooltip("The swipe is as big as the HARDEST pull within this many seconds before release, so easing off the trigger to let go does not shrink the swipe.")]
        float swipeSizeMemorySeconds = 0.25f;

        [Header("Fuel (the Mass card)")]
        [SerializeField, Tooltip("Index of the vessel ResourceSystem resource the claw refills (the Grizzly's energy).")]
        int energyIndex = 0;
        [SerializeField, Tooltip("Energy each enemy prism the claw lights gives back. MASS scales it: 0.01 at rest, 0.025 at " +
                                 "level 10, floored at half (the fleet floor rule). A Rush costs 0.25, so a few seconds of " +
                                 "claw on enemy mass buys the next ram. Super-shielded prisms it cannot light give nothing.")]
        ElementalFloat energyPerIgnite = ElementalFloat.Multiplier(0.01f, 0.025f, Element.Mass, 0.005f);

        public int EnergyIndex => energyIndex;
        /// <summary>Energy one lit prism refills at this vessel's live Mass level.</summary>
        public float EnergyPerIgniteFor(IVesselStatus status) => Mathf.Max(0f, energyPerIgnite.EvaluateLive(status));

        [Header("Muzzles (one per trigger)")]
        [SerializeField, Tooltip("How far each claw's root sits to its own side of the hull's centre line, in the hull's local units (LT left, RT right).")]
        float sideOffset = 6f;
        [SerializeField, Tooltip("How far ahead of the hull's pivot each claw's root sits, in the hull's local units.")]
        float noseOffset = 4f;
        [SerializeField, Tooltip("Degrees each claw is turned OUTWARD from straight ahead (LT left, RT right), so the two claws spread apart. 0 = both straight forward.")]
        float splayDegrees = 6f;
        [SerializeField, Tooltip("Degrees each claw is tilted UP from the hull's forward, so the two claws do not sit across each other from the chase camera (design ask, 2026-10-17: 'tilt them up just a touch'). The fingers are what burns, so this tilts the reach too.")]
        [Range(0f, 30f)] float tiltUpDegrees = 5f;

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
        public bool SliceEnabled => sliceEnabled;
        public int SlicesPerFrame => slicesPerFrame;
        public float SliceIgniteRadius => sliceIgniteRadius;
        public float SliceDebrisRestitution => sliceDebrisRestitution;
        public float SliceDebrisSpeedLimit => sliceDebrisSpeedLimit;
        public EventReference SliceEvent => sliceEvent;
        public float IgniteTicksPerSecond => igniteTicksPerSecond;
        public int IgnitesPerTick => ignitesPerTick;
        public float SideOffset => sideOffset;
        public float NoseOffset => noseOffset;
        public float SplayDegrees => splayDegrees;
        public float TiltUpDegrees => tiltUpDegrees;
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
        public bool SwipeEnabled => swipeEnabled;
        public float SwipeSeconds => swipeSeconds;
        public float SwipeArcDegrees => swipeArcDegrees;
        public float SwipeDiagonalDegrees => swipeDiagonalDegrees;
        public float SwipeCellCost => swipeCellCost;
        public float SwipeSizeMemorySeconds => swipeSizeMemorySeconds;

        public float SwipeWindupFraction => swipeWindupFraction;

        /// <summary>
        /// The swipe's turn at <paramref name="progress01"/> (0 = where the claw was held, 1 = the end of the slash),
        /// in the CLAW's own frame: apply it after the claw's aim (<c>aim * SwipeRotation(...)</c>).
        /// <para>Each claw draws ONE STROKE OF AN X (design ask, 2026-11-23: "I want each claw to be one side of an X
        /// when it swipes, starting up and going down and across"). The stroke runs along one diagonal: down, and
        /// toward the hull's centre line - +x for LT's claw on the left, -x for RT's on the right - mixed by
        /// <c>swipeDiagonalDegrees</c>. For the first <c>swipeWindupFraction</c> the claw RISES back along that line,
        /// up and out, to half the arc above where it was held; then it SLASHES the full arc down and across, ending
        /// half the arc past it. Both legs ease out, so the slash is fastest as it leaves the top.</para>
        /// </summary>
        public Quaternion SwipeRotation(bool leftClaw, float progress01)
        {
            float p = Mathf.Clamp01(progress01);
            float w = Mathf.Clamp(swipeWindupFraction, 0.01f, 0.99f);
            float half = 0.5f * swipeArcDegrees;
            float angle = p < w
                ? -half * EaseOut(p / w)                                  // rise: held -> top of the stroke
                : Mathf.Lerp(-half, half, EaseOut((p - w) / (1f - w)));   // slash: top -> bottom, across
            float diag = swipeDiagonalDegrees * Mathf.Deg2Rad;
            var motion = new Vector3((leftClaw ? 1f : -1f) * Mathf.Sin(diag), -Mathf.Cos(diag), 0f);
            // Rotating forward about cross(forward, motion) by a positive angle carries it toward motion.
            return Quaternion.AngleAxis(angle, Vector3.Cross(Vector3.forward, motion));
        }

        static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>
        /// Remembers the hardest pull within the last <c>swipeSizeMemorySeconds</c> (call once a frame with the live
        /// pressure). A trigger eases through every lighter pressure on its way out, so sizing the swipe by the
        /// pressure AT release would always make the smallest one.
        /// </summary>
        public float TrackSwipePressure(ref float peak, ref float peakAge, float live01, float dt)
        {
            live01 = Mathf.Clamp01(live01);
            peakAge += dt;
            if (live01 >= peak || peakAge > swipeSizeMemorySeconds)
            {
                peak = live01;
                peakAge = 0f;
            }
            return peak;
        }

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

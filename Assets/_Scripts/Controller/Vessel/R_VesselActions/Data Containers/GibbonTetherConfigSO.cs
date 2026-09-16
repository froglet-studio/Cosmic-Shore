using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number the Gibbon's two tether lines are made of, in ONE asset — the
    /// config-separation rule applied to a mechanic that has a lot of dials and exactly one feel.
    /// Both arms read the same asset, because a left line that behaves differently from a right
    /// line is a bug rather than a feature.
    /// </summary>
    [CreateAssetMenu(fileName = "GibbonTetherConfig",
                     menuName = "ScriptableObjects/Vessel Actions/Gibbon Tether Config")]
    public class GibbonTetherConfigSO : ScriptableObject
    {
        [Header("Beam — charge and reach")]
        [Tooltip("Beam length at a feather-touch on the trigger. The floor exists so a twitchy " +
                 "press still plants something usable rather than an anchor inside the hull.")]
        public float MinBeamLength = 45f;

        [Tooltip("Beam length at a fully-buried trigger. This is also the vessel's reach: it is " +
                 "how far ahead the pilot can commit to a piece of empty space.")]
        public float MaxBeamLength = 320f;

        [Tooltip("How fast the beam travels out to its endpoint (u/s), purely for the visual and " +
                 "for when the anchor becomes real. Fast enough to read as a beam, slow enough " +
                 "that a long shot is visibly a longer commitment.")]
        public float BeamTravelSpeed = 1400f;

        [Tooltip("Radius of the cut the beam makes along its path, and of the live line's cut.")]
        public float BeamCutRadius = 4.5f;

        [Header("Line — the spring")]
        [Tooltip("Spring constant on the stretch past the rest length (1/s^2). Higher reads as a " +
                 "steel cable, lower as bungee. The acceleration this can produce is bounded by " +
                 "Stiffness x MaxStretch, so raising it without lowering MaxStretch raises the " +
                 "hardest possible yank.")]
        public float Stiffness = 9f;

        [Tooltip("Damping on the RADIAL velocity only (1/s), applied as an exact exponential so " +
                 "it is frame-rate identical. The swing (tangential) component is never damped — " +
                 "damping that is what makes a tether feel like mud.")]
        public float RadialDamping = 3.2f;

        [Tooltip("Stretch at which the spring stops getting stronger. Caps the force a line can " +
                 "ever apply, which is what keeps an explicit spring stable across a hitch frame.")]
        public float MaxStretch = 26f;

        [Tooltip("Stretch at which the line SNAPS and releases itself. Both a game rule — " +
                 "overload it and you lose it — and the bound that stops the solver from ever " +
                 "being arbitrarily far from equilibrium.")]
        public float BreakStretch = 70f;

        [Header("Winch — the vessel's only accelerator")]
        [Tooltip("How fast a live line shortens (u/s). THE speed dial: all of this vessel's " +
                 "acceleration is angular momentum conserved as the radius falls.")]
        public float ReelRate = 34f;

        [Tooltip("Terminal velocity. The winch's efficiency falls as 1-(v/cap)^2 and reaches zero " +
                 "here, so the cap stops the vessel EARNING speed rather than braking a pilot who " +
                 "already has it. Note this is usually NOT the binding limit — see MaxSwingRate.")]
        public float SpeedCap = 260f;

        [Tooltip("Above the cap, bleed speed at this rate (u/s^2) so nothing (a boost, a blast, " +
                 "another pilot) can park the vessel above its terminal velocity indefinitely.")]
        public float OverspeedDrag = 26f;

        [Header("Winch — the floor")]
        [Tooltip("Hardest orbit the vessel may be reeled into (rad/s). This is the REAL speed " +
                 "ceiling in most swings: angular momentum L is fixed once you are on the line, " +
                 "and the reel stops at r = v/w, so the swing tops out near sqrt(L*w). A longer " +
                 "beam fired at speed banks more L and therefore pays out more speed — which is " +
                 "what makes the analog charge a strategic choice and not just a range dial.")]
        public float MaxSwingRate = 3.2f;

        [Tooltip("Hull half-extent used for the geometric part of the rest-length floor.")]
        public float HullRadius = 6f;

        [Tooltip("Anchor prism half-extent used for the geometric part of the floor.")]
        public float AnchorRadius = 3f;

        [Tooltip("Extra clearance kept between hull and anchor at the tightest legal orbit.")]
        public float Clearance = 4f;

        [Header("Handling while tethered")]
        [Tooltip("How strongly momentum still rotates onto the nose while a line is taut (1/s). " +
                 "0 is a pure frozen swing; high values fight the tether. The point of a non-zero " +
                 "value is that the two sticks stay CONNECTED mid-swing — you can lean the arc.")]
        public float TetheredNoseConvergence = 1.4f;

        [Header("Cutting")]
        [Tooltip("The live line slices hostile prisms it sweeps through. Seconds between sweeps: " +
                 "the line is long and moving, so this is a SAMPLE of a swept quad, not a test of " +
                 "one. Lower costs more segment queries; higher lets mass slip through.")]
        public float LiveCutInterval = 0.05f;

        [Tooltip("Switch the live line's cutting off entirely and leave only the firing sweep.")]
        public bool LiveLineCuts = true;

        [Header("Anchor")]
        [Tooltip("Scale of the prism the beam plants at its endpoint.")]
        public Vector3 AnchorScale = new Vector3(6f, 6f, 6f);
    }
}

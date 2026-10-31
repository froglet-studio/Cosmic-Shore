using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Serpent's <b>scope</b> — hold the left trigger and a magnified cockpit view
    /// opens in a window beside the flight view, and the right trigger becomes the sniper shot
    /// (<see cref="SniperShotActionSO"/>). Release and the window closes.
    ///
    /// <para><b>The pilot's own view never moves.</b> The scope is a second, magnified picture;
    /// the ship keeps flying exactly as it did. That is not a presentation preference — the first
    /// cut took the whole screen into the cockpit and magnified it, and it read as nauseating
    /// (<c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 4), because a magnified view
    /// multiplies every motion the pilot did not ask for as faithfully as the one they did.</para>
    ///
    /// <para><b>The scope has NO element parameter</b> (Garrett, 2026-10-30: "lets not have the
    /// scope have its own parameters. lets just have a good scope to begin with"). It is the
    /// Serpent's CHARGE card, and Charge's number is the sniper's recovery
    /// (<see cref="SniperShotActionSO"/>); the zoom is one authored magnification at every level.
    /// Its level-5 upgrade is <b>Hunter's Sight</b>: while scoped, every rival in range leaves
    /// short-lived AFTERIMAGES, and a round through an afterimage strips the real pilot
    /// (<see cref="ScopeAfterimages"/>). Space 5's old Deep Focus zoom step is retired with
    /// the Space scaling.</para>
    ///
    /// <para><b>The zoom is a pure function of the trigger's own depth and NOTHING ELSE</b> —
    /// not the stick, not the vessel's speed, not the camera the pilot is flying with. An earlier
    /// version bled the magnification off as the pilot turned (a "Steady Eye" gate the upgrade
    /// removed) and let the speed tunnel go on narrowing the scoped value; both are retired. The
    /// general rule they leave behind: <i>a magnified view is a lever on every input that reaches
    /// it, so a zoom that is a function of anything but the zoom control amplifies motion the
    /// pilot never asked for.</i> <see cref="ZoomResponse"/> is the one remaining term and it is
    /// a function of the trigger's own history, not of the world: it exists so a BINARY trigger
    /// (mouse, keyboard — 0 or 1) ramps instead of snapping.</para>
    ///
    /// <para>It touches no mass and no speed. The whole ability is a window, a field of view
    /// and a flag the sniper reads.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SniperScopeAction", menuName = "ScriptableObjects/Vessel Actions/Sniper Scope")]
    public class SniperScopeActionSO : ShipActionSO
    {
        [Header("Magnification")]
        [Tooltip("Field of view in degrees with the scope RAISED but not yet zoomed - the wide " +
                 "end of the dial. A constant rather than a read of the live gameplay camera, " +
                 "deliberately: that camera is narrowed by the speed tunnel, so reading it back " +
                 "would make the scope's magnification a function of how fast the pilot is going.")]
        [SerializeField, Range(20f, 110f)] private float unscopedFieldOfView = 60f;

        [Tooltip("Field of view in degrees at FULL zoom - the scope's one magnification, the same at " +
                 "every element level. Lower is more magnified. 12 sits between the old resting 22 " +
                 "and the old Space 10 + Deep Focus 6.9, which is the reach the upgrades used to buy.")]
        [SerializeField, Range(1f, 80f)] private float fieldOfViewAtFullZoom = 12f;

        [Header("Feel")]
        [Tooltip("How fast the applied zoom chases the trigger, in units of zoom per second. " +
                 "Its ONLY job is to make a BINARY trigger (mouse/keyboard, which report 0 or 1) " +
                 "ramp instead of snapping. On an analog pad it must be high enough to be a " +
                 "pass-through: a low value makes the zoom lag the thumb, which reads as the " +
                 "scope moving on its own — the exact complaint the pure-trigger rule answers.")]
        [SerializeField, Min(0.1f)] private float zoomResponse = 12f;

        [Tooltip("Trigger depth below which the scope reports NO zoom at all, so the very top of " +
                 "the trigger's travel is dead and a resting pad cannot creep the view in.")]
        [SerializeField, Range(0f, 0.5f)] private float zoomDeadzone = 0.08f;

        [Header("Hunter's Sight (Charge 5)")]
        [Tooltip("Seconds between afterimages of each rival while the scope is up. Each one is the " +
                 "rival's hull frozen where it stood.")]
        [SerializeField, Min(0.05f)] private float afterimageInterval = 0.25f;

        [Tooltip("Seconds an afterimage stands before it fades. A round through it inside this " +
                 "window strips the pilot it was taken from.")]
        [SerializeField, Min(0.05f)] private float afterimageLifetime = 1f;

        [Tooltip("Rivals farther than this are not traced. Matches the sniper's range by default, " +
                 "so every afterimage can be shot.")]
        [SerializeField, Min(1f)] private float afterimageRange = 3000f;

        [Tooltip("The afterimage's look: CloakGhost.mat (CosmicShore/CloakGhost, an additive Fresnel " +
                 "outline), tinted with the RIVAL's domain colour at runtime. Empty = afterimages are " +
                 "still recorded and shootable, but not drawn.")]
        [SerializeField] private Material afterimageMaterial;

        public float UnscopedFieldOfView => unscopedFieldOfView;
        public float FieldOfViewAtFullZoom => fieldOfViewAtFullZoom;
        public float ZoomResponse => zoomResponse;
        public float ZoomDeadzone => zoomDeadzone;
        public float AfterimageInterval => afterimageInterval;
        public float AfterimageLifetime => afterimageLifetime;
        public float AfterimageRange => afterimageRange;
        public Material AfterimageMaterial => afterimageMaterial;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SniperScopeActionExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SniperScopeActionExecutor>()?.Release(this, vesselStatus);
    }
}

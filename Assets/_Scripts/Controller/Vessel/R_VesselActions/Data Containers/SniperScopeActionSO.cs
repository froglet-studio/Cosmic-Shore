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
    /// <para>Element → parameter: <b>SPACE</b> owns this ability, and the parameter is the
    /// MAGNIFICATION — how far down-range the scope can see. Space is reach/presence fleet-wide,
    /// and a scope's whole claim is reach, so the element and the mechanic are the same
    /// statement. Its level-5 upgrade, <b>Deep Focus</b>, is one more magnification step:
    /// <see cref="UpgradeZoomDepthMultiplier"/> multiplies the depth and divides the floor by the
    /// same number, so the whole zoom range moves together rather than running into a wall the
    /// upgrade cannot pass.</para>
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

        [Tooltip("Field of view in degrees at FULL zoom, at the resting Space level. Lower is " +
                 "more magnified. The unscoped end is the player's own FOV setting, read live, " +
                 "so the scope narrows from wherever they play rather than from a constant.")]
        [SerializeField, Range(1f, 80f)] private float fieldOfViewAtFullZoom = 22f;

        [Tooltip("Multiplier on the zoom DEPTH at Space level 10. Above 1 the scope reaches " +
                 "further (a NARROWER field of view at full zoom); at the resting level the " +
                 "authored value above is used exactly, per the anchored-at-1 elemental contract.")]
        [SerializeField, Min(1f)] private float zoomDepthAtFullSpace = 2f;

        [Tooltip("Floor on the magnified field of view, in degrees, whatever Space says. A scope " +
                 "narrower than this stops being an aiming aid and becomes a soda straw the " +
                 "pilot cannot read. Deep Focus divides it by the upgrade multiplier below, so " +
                 "the upgrade moves the ceiling as well as the dial.")]
        [SerializeField, Range(1f, 40f)] private float minFieldOfView = 8f;

        [Tooltip("Space 5 \"Deep Focus\": one more magnification step. Multiplies the zoom depth " +
                 "AND divides the floor by the same number, so the extra reach is actually " +
                 "reachable. 1 disables the upgrade's effect.")]
        [SerializeField, Min(1f)] private float upgradeZoomDepthMultiplier = 1.6f;

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

        public float UnscopedFieldOfView => unscopedFieldOfView;
        public float FieldOfViewAtFullZoom => fieldOfViewAtFullZoom;
        public float ZoomDepthAtFullSpace => zoomDepthAtFullSpace;
        public float MinFieldOfView => minFieldOfView;
        public float UpgradeZoomDepthMultiplier => upgradeZoomDepthMultiplier;
        public float ZoomResponse => zoomResponse;
        public float ZoomDeadzone => zoomDeadzone;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SniperScopeActionExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SniperScopeActionExecutor>()?.Release(this, vesselStatus);
    }
}

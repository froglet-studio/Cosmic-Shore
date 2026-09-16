using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Serpent's <b>scope</b> — hold the left trigger and the view drops into the
    /// cockpit and magnifies, and the right trigger becomes the sniper shot
    /// (<see cref="SniperShotActionSO"/>). Release and the view returns to the chase camera.
    ///
    /// <para>Element → parameter: <b>SPACE</b> owns this ability, and the parameter is the
    /// MAGNIFICATION — how far down-range the scope can see. Space is reach/presence fleet-wide,
    /// and a scope's whole claim is reach, so the element and the mechanic are the same
    /// statement. Its level-5 upgrade extends the scope to hold its zoom while the pilot turns
    /// (<see cref="SniperScopeActionExecutor"/>'s steady gate).</para>
    ///
    /// <para><b>The zoom is analog</b>: the pilot's own trigger depth sets it, so easing the
    /// trigger is a continuous magnification dial rather than a two-state toggle. On a device
    /// whose trigger is binary (mouse, keyboard) the same number arrives as 0 or 1 and
    /// <see cref="ZoomResponse"/> ramps it, so every device gets a smooth zoom and only the pad
    /// gets a proportional one — see the executor.</para>
    ///
    /// <para>It touches no mass and no speed. The whole ability is a camera pose, a field of view
    /// and a flag the sniper reads.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SniperScopeAction", menuName = "ScriptableObjects/Vessel Actions/Sniper Scope")]
    public class SniperScopeActionSO : ShipActionSO
    {
        [Header("Magnification")]
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
                 "pilot cannot fly with.")]
        [SerializeField, Range(1f, 40f)] private float minFieldOfView = 8f;

        [Header("Feel")]
        [Tooltip("How fast the applied zoom chases the trigger, in units of zoom per second. " +
                 "This is what makes a BINARY trigger (mouse/keyboard, which report 0 or 1) ramp " +
                 "smoothly instead of snapping, and it costs an analog pad almost nothing " +
                 "because its own value is already continuous.")]
        [SerializeField, Min(0.1f)] private float zoomResponse = 6f;

        [Tooltip("Trigger depth below which the scope reports NO zoom at all, so the very top of " +
                 "the trigger's travel is dead and a resting pad cannot creep the view in.")]
        [SerializeField, Range(0f, 0.5f)] private float zoomDeadzone = 0.08f;

        public float FieldOfViewAtFullZoom => fieldOfViewAtFullZoom;
        public float ZoomDepthAtFullSpace => zoomDepthAtFullSpace;
        public float MinFieldOfView => minFieldOfView;
        public float ZoomResponse => zoomResponse;
        public float ZoomDeadzone => zoomDeadzone;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SniperScopeActionExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SniperScopeActionExecutor>()?.Release(this, vesselStatus);
    }
}

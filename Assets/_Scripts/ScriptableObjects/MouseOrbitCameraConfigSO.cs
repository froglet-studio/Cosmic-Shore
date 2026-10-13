using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning and bindings for <c>MouseOrbitCamera</c> — the strategy-game mouse camera used by the
    /// test scenes (pan / orbit / zoom about a pivot, Transport Fever style; Docs/BLACK_HOLE.md §7).
    ///
    /// Bindings live here rather than in code so a different hand (left-drag pan, right-drag
    /// orbit — Transport Fever's own default) is an asset edit. Place the asset at
    /// <c>Resources/MouseOrbitCameraConfig</c>; with no asset the defaults below apply.
    /// </summary>
    [CreateAssetMenu(fileName = "MouseOrbitCameraConfig", menuName = "ScriptableObjects/Camera/Mouse Orbit Camera Config")]
    public class MouseOrbitCameraConfigSO : ScriptableObject
    {
        /// <summary>A mouse button a gesture is bound to. Values are explicit (serialization).</summary>
        public enum MouseButtonBinding
        {
            Left = 0,
            Right = 1,
            Middle = 2,
        }

        [Header("Bindings")]
        [Tooltip("Hold and drag to PAN — the world moves with the cursor, as if you grabbed it.")]
        [SerializeField] MouseButtonBinding panButton = MouseButtonBinding.Right;

        [Tooltip("Hold and drag to ORBIT (rotate the view around the pivot). Only starts when the press " +
                 "is not on UI, so buttons still click.")]
        [SerializeField] MouseButtonBinding orbitButton = MouseButtonBinding.Left;

        [Tooltip("Hold and drag up/down to ZOOM (dolly toward / away from the pivot). The wheel zooms too.")]
        [SerializeField] MouseButtonBinding zoomDragButton = MouseButtonBinding.Middle;

        [Tooltip("Holding Alt while dragging the PAN button orbits instead — one-handed rotate.")]
        [SerializeField] bool altTurnsPanIntoOrbit = true;

        [Tooltip("Hide and lock the cursor while orbiting, so a long drag never runs out of screen.")]
        [SerializeField] bool lockCursorWhileOrbiting = true;

        [Header("Pan")]
        [Tooltip("1 = the point under the cursor stays under the cursor (exact at the pivot's depth). " +
                 "Higher pans faster than the cursor moves.")]
        [Min(0.01f)]
        [SerializeField] float panSpeed = 1f;

        [Tooltip("WASD pan speed, as a fraction of the current view distance per second — so it feels " +
                 "the same close up and far away.")]
        [Min(0f)]
        [SerializeField] float keyboardPanSpeed = 0.9f;

        [Header("Orbit")]
        [Tooltip("Degrees the view turns per pixel of drag.")]
        [Min(0.001f)]
        [SerializeField] float orbitDegreesPerPixel = 0.25f;

        [Tooltip("Q/E orbit speed, degrees per second.")]
        [Min(0f)]
        [SerializeField] float keyboardOrbitDegreesPerSecond = 90f;

        [Tooltip("Pitch limits, degrees. Kept short of ±90 so the view never flips over the pole.")]
        [Range(-89f, 0f)]
        [SerializeField] float minPitch = -85f;

        [Range(0f, 89f)]
        [SerializeField] float maxPitch = 85f;

        [Header("Zoom")]
        [Tooltip("Fraction of the view distance one wheel notch zooms by.")]
        [Range(0.01f, 0.9f)]
        [SerializeField] float wheelZoomStep = 0.15f;

        [Tooltip("Zoom toward the point under the cursor (on) or toward the pivot (off).")]
        [SerializeField] bool zoomTowardCursor = true;

        [Tooltip("Fraction of the view distance per pixel of zoom-button drag.")]
        [Min(0f)]
        [SerializeField] float dragZoomPerPixel = 0.006f;

        [Tooltip("Closest the camera may get to its pivot, world units.")]
        [Min(0.1f)]
        [SerializeField] float minDistance = 5f;

        [Tooltip("Farthest the camera may get from its pivot, world units.")]
        [Min(1f)]
        [SerializeField] float maxDistance = 20000f;

        [Header("Feel")]
        [Tooltip("How quickly the camera catches up with its target, 1/s. 0 = instant (no smoothing).")]
        [Min(0f)]
        [SerializeField] float smoothing = 14f;

        [Tooltip("Shift multiplies every keyboard and wheel speed by this.")]
        [Min(1f)]
        [SerializeField] float fastMultiplier = 3f;

        [Tooltip("The far clip plane is kept at least this far beyond the pivot, so zooming out never " +
                 "clips the thing you are looking at.")]
        [Min(0f)]
        [SerializeField] float farClipMargin = 5000f;

        public MouseButtonBinding PanButton => panButton;
        public MouseButtonBinding OrbitButton => orbitButton;
        public MouseButtonBinding ZoomDragButton => zoomDragButton;
        public bool AltTurnsPanIntoOrbit => altTurnsPanIntoOrbit;
        public bool LockCursorWhileOrbiting => lockCursorWhileOrbiting;
        public float PanSpeed => Mathf.Max(0.01f, panSpeed);
        public float KeyboardPanSpeed => Mathf.Max(0f, keyboardPanSpeed);
        public float OrbitDegreesPerPixel => Mathf.Max(0.001f, orbitDegreesPerPixel);
        public float KeyboardOrbitDegreesPerSecond => Mathf.Max(0f, keyboardOrbitDegreesPerSecond);
        public float MinPitch => Mathf.Clamp(minPitch, -89f, 0f);
        public float MaxPitch => Mathf.Clamp(maxPitch, 0f, 89f);
        public float WheelZoomStep => Mathf.Clamp(wheelZoomStep, 0.01f, 0.9f);
        public bool ZoomTowardCursor => zoomTowardCursor;
        public float DragZoomPerPixel => Mathf.Max(0f, dragZoomPerPixel);
        public float MinDistance => Mathf.Max(0.1f, minDistance);
        public float MaxDistance => Mathf.Max(MinDistance + 1f, maxDistance);
        public float Smoothing => Mathf.Max(0f, smoothing);
        public float FastMultiplier => Mathf.Max(1f, fastMultiplier);
        public float FarClipMargin => Mathf.Max(0f, farClipMargin);

        /// <summary>
        /// Two gestures on one button would make one of them unreachable. The zoom-drag button may
        /// not be either of the others; pan and orbit may share only through the Alt modifier.
        /// </summary>
        public bool IsSane =>
            panButton != orbitButton && zoomDragButton != panButton && zoomDragButton != orbitButton &&
            MinDistance < MaxDistance && MinPitch < MaxPitch;
    }
}

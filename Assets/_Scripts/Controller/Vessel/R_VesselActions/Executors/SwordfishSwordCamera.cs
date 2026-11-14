using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Swordfish's camera keeps its sword in the middle of the frame (Garrett, 2026-11-08): "in the home position
    /// the camera should have no y-offset or x-offset ... when a trigger is pulled the sword should go to the side and
    /// the camera's x offset should center the sword. when both triggers are pulled the sword should be vertical and
    /// the camera should only have a y offset to again keep the sword centered."
    ///
    /// <para>The pan is the sword's MIDPOINT moved off its rest, in the hull's frame: at rest the bill lies along the
    /// nose, so the pan is zero; swung out to the side its midpoint is half a blade to that side (an x pan); raised
    /// straight up it is half a blade overhead (a y pan). Only the across-the-view part (x, y) is used - the camera
    /// already pulls back with the blade's length (<see cref="SwordfishSwordLengthCamera"/>). Written to
    /// <see cref="CustomCameraController.FramingPan"/>, which pans the camera and its look point together, for the
    /// local human pilot only; an AI or a remote copy never moves anyone's camera.</para>
    ///
    /// <para>Owned by <see cref="ShieldSwipeActionExecutor"/> and ticked after every pose it writes. Plain C#,
    /// allocation-free per frame.</para>
    /// </summary>
    public sealed class SwordfishSwordCamera
    {
        Vector3 _pan;
        bool _written;
        CustomCameraController _camera;

        /// <summary>
        /// The pan that centres a sword whose midpoint moved <paramref name="midpointOffsetInHull"/> off its rest, in
        /// the hull's frame: its x and y, scaled by <paramref name="centring"/>; never a z. Pure, so it is tested.
        /// </summary>
        public static Vector3 PanFor(Vector3 midpointOffsetInHull, float centring)
        {
            float c = Mathf.Clamp01(centring);
            return new Vector3(midpointOffsetInHull.x * c, midpointOffsetInHull.y * c, 0f);
        }

        public void Tick(IVesselStatus status, Vector3 midpointOffsetInHull, SwordfishShieldSwipeConfigSO config, float dt)
        {
            bool pilot = status?.Player != null && status.IsLocalUser && !status.IsInitializedAsAI
                         && (status.AIPilot == null || !status.AIPilot.AutoPilotEnabled);
            if (!pilot || !config)
            {
                Release();
                return;
            }

            Vector3 target = PanFor(midpointOffsetInHull, config.BillCameraCentring);
            float seconds = config.BillCameraPanSeconds;
            _pan = seconds <= 0f ? target : Vector3.Lerp(_pan, target, 1f - Mathf.Exp(-dt / seconds));
            if ((_pan - target).sqrMagnitude < 1e-6f) _pan = target;
            if (_pan == Vector3.zero && !_written) return;

            var cam = Resolve();
            if (!cam) return;
            cam.FramingPan = _pan;
            _written = _pan != Vector3.zero;
        }

        public void Release()
        {
            _pan = Vector3.zero;
            if (!_written) return;
            _written = false;
            var cam = _camera ? _camera : Resolve();
            if (cam) cam.FramingPan = Vector3.zero;
        }

        CustomCameraController Resolve()
        {
            if (_camera && _camera.isActiveAndEnabled) return _camera;
            _camera = CameraManager.Instance != null ? CameraManager.Instance.GetActiveController() as CustomCameraController : null;
            return _camera;
        }
    }
}

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino sword's two holds on the pilot's camera, for the LOCAL HUMAN pilot only (an AI, a
    /// remote copy or the lava lamp's autopilot never moves anyone's camera). Owned and ticked by
    /// <see cref="ShieldSkimmerScaleDriver"/>; both are point-of-use multipliers on
    /// <see cref="CustomCameraController"/>, so adaptive zoom and settings re-applies keep working
    /// underneath, and both reset with the follow target.
    /// <list type="bullet">
    /// <item><b>Reach</b> (<see cref="CustomCameraController.VesselDistanceScale"/>): the camera pulls back
    /// as Space lengthens the blade (Garrett, 2026-10-26: "Space alone can grow the sword, but move the
    /// camera accordingly") - <see cref="ReachDistanceScale"/>.</item>
    /// <item><b>Surge</b> (<see cref="CustomCameraController.SurroundFraming01"/>): during an omni-crystal
    /// surge the camera eases straight back and re-centres on the hull so the sword ring is on screen.</item>
    /// </list>
    /// </summary>
    public sealed class RhinoSwordCamera
    {
        float _surround01;
        bool _written;
        CustomCameraController _camera;

        /// <summary>
        /// The camera distance multiplier for a blade <paramref name="lengthRatio"/> times its resting
        /// (Space 0) length: 1 at rest, and <paramref name="follow"/> of the blade's growth on top -
        /// 1 keeps the blade the same size on screen, 0 leaves the camera where it was. Never nearer
        /// than authored. Pure.
        /// </summary>
        public static float ReachDistanceScale(float lengthRatio, float follow) =>
            Mathf.Max(1f, 1f + Mathf.Max(0f, follow) * (lengthRatio - 1f));

        public void Tick(IVesselStatus status, bool surging, float lengthRatio, ShieldSkimmerScaleConfigSO config, float dt)
        {
            bool pilot = status?.Player != null && status.IsLocalUser && !status.IsInitializedAsAI
                         && (status.AIPilot == null || !status.AIPilot.AutoPilotEnabled);
            if (!pilot || config == null)
            {
                Release();
                return;
            }

            _surround01 = Mathf.MoveTowards(_surround01, surging ? 1f : 0f, dt / config.SurgeCameraBlendSeconds);
            float reach = ReachDistanceScale(lengthRatio, config.CameraFollowsBladeLength);
            if (_surround01 <= 0f && Mathf.Approximately(reach, 1f) && !_written) return;

            var cam = Resolve();
            if (!cam) return;
            cam.SurroundFraming01 = _surround01 * _surround01 * (3f - 2f * _surround01);
            cam.SurroundDistanceScale = config.SurgeCameraDistanceScale;
            cam.VesselDistanceScale = reach;
            _written = _surround01 > 0f || !Mathf.Approximately(reach, 1f);
        }

        /// <summary>Hand the camera back at once (pilot change, disable, destroy).</summary>
        public void Release()
        {
            _surround01 = 0f;
            if (!_written) return;
            _written = false;
            var cam = _camera ? _camera : Resolve();
            if (!cam) return;
            cam.SurroundFraming01 = 0f;
            cam.SurroundDistanceScale = 1f;
            cam.VesselDistanceScale = 1f;
        }

        CustomCameraController Resolve()
        {
            if (_camera && _camera.isActiveAndEnabled) return _camera;
            _camera = CameraManager.Instance != null ? CameraManager.Instance.GetActiveController() as CustomCameraController : null;
            return _camera;
        }
    }
}

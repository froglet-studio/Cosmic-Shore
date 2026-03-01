using Unity.Cinemachine;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cinemachine extension that orients the camera to look AT the follow target
    /// from the computed camera position. Matches the rotation that
    /// <see cref="CustomCameraController.SnapToTarget"/> computes via SafeLookRotation:
    ///   <c>Quaternion.LookRotation(target.position - camera.position, target.up)</c>
    ///
    /// Operates at the Aim pipeline stage: CinemachineFollow handles position (Body),
    /// then this extension computes orientation from the resulting camera position.
    /// During CinemachineBrain blends, the Brain interpolates between vCam CameraStates
    /// so transitions remain smooth — and because the look direction shifts naturally
    /// as position interpolates, rotation and position transition simultaneously.
    /// </summary>
    [AddComponentMenu("")] // Added programmatically by MainMenuCameraController
    public class CinemachineMatchTargetOrientation : CinemachineExtension
    {
        [Tooltip("Rotation damping in seconds. 0 = snap to target orientation instantly.")]
        public float Damping;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Aim) return;

            var target = vcam.Follow;
            if (target == null) return;

            // LookAt rotation from the computed camera position toward the follow target.
            // This matches CustomCameraController.SnapToTarget()'s SafeLookRotation so the
            // bridge→PlayerCam handoff has zero rotation discontinuity.
            var dir = target.position - state.GetFinalPosition();
            if (dir.sqrMagnitude < 0.001f) return;

            var targetRot = Quaternion.LookRotation(dir, target.up);

            if (Damping > 0.001f && deltaTime >= 0f)
            {
                float t = 1f - Mathf.Exp(-deltaTime / Mathf.Max(Damping, 0.0001f));
                state.RawOrientation = Quaternion.Slerp(state.RawOrientation, targetRot, t);
            }
            else
            {
                state.RawOrientation = targetRot;
            }
        }
    }
}

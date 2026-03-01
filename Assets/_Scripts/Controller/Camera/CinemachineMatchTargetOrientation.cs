using Unity.Cinemachine;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cinemachine extension that makes the camera face the same direction as the
    /// follow target rather than aiming at it. Replaces CinemachineRotationComposer
    /// for third-person cameras where the camera should share the vessel's orientation
    /// (forward, up, roll) — critical for space games with full 6DOF movement.
    ///
    /// Operates at the Aim pipeline stage: CinemachineFollow handles position (Body),
    /// then this extension overrides orientation to match the tracking target's rotation.
    /// During CinemachineBrain blends, the Brain interpolates between vCam CameraStates
    /// so transitions remain smooth.
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

            var targetRot = target.rotation;

            if (Damping > 0.001f && deltaTime >= 0f)
            {
                // Exponential decay matches CinemachineFollow's smoothing behavior
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

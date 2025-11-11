using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// Attach to the VICTIM (vessel) impact list for Vessel–Skimmer collisions.
    [CreateAssetMenu(
        fileName = "VesselSpinBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselSpinBySkimmerEffectSO")]
    public sealed class VesselSpinBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Spin")]
        [SerializeField, Tooltip("Yaw in degrees applied to the victim. Sign decided via attacker vs victim facing.")]
        private float yawDegrees = 15f;

        [SerializeField, Tooltip("Duration (seconds) for SpinShip.")]
        private float spinDuration = 0.6f;

        [Header("Optional lateral shove (0 disables)")]
        [SerializeField] private float lateralSpeed = 0f;
        [SerializeField] private float accelScale   = 15f;

        [Header("Axis Options")]
        [SerializeField]
        private bool useAttackerUpAxis = false;

        public override void Execute(VesselImpactor vesselImpactor, SkimmerImpactor skimmerImpactee)
        {
            var victimStatus = vesselImpactor?.Vessel?.VesselStatus;
            if (victimStatus == null) return;
            var skimmer = skimmerImpactee?.Skimmer;
            if (skimmer == null) return;
    
            var victimTf = victimStatus.ShipTransform;
            var transformer = victimStatus.VesselTransformer;

            // Inputs
            Vector3 victimFwd = victimTf.forward;
            Vector3 attackerFwd = skimmer.transform.forward;
            Vector3 yawAxis = useAttackerUpAxis ? skimmer.transform.up : victimTf.up;

            Vector3 cross = Vector3.Cross(victimFwd, attackerFwd);
            float sign = Mathf.Sign(Vector3.Dot(cross, yawAxis));
            float signedYaw = yawDegrees * sign;

            Vector3 newForward = Quaternion.AngleAxis(signedYaw, yawAxis) * victimFwd;
 
            newForward.Normalize();

            transformer.SpinShip(newForward);

            if (!(lateralSpeed > 0f)) return;
            Vector3 lateralDir = (sign >= 0f ? victimTf.right : -victimTf.right).normalized;
            transformer.ModifyVelocity(lateralDir * lateralSpeed, Time.deltaTime * accelScale);
        }
    }
}

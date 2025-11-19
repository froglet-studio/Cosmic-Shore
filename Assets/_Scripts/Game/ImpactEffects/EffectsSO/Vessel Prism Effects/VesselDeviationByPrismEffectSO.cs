using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselDeviationByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselDeviationByPrismEffectSO")]
    public class VesselDeviationByPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Lateral Bounce")]
        [SerializeField] private float lateralSpeed = 5f;   
        [SerializeField] private float accelScale  = 15f;    
        [SerializeField, Tooltip("If true, choose left or right randomly each hit.")]
        private bool randomizeLeftRight = true;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = vesselImpactor?.Vessel?.VesselStatus;
            if (vesselStatus == null || vesselStatus.IsTranslationRestricted) return;

            var shipTransform = vesselStatus.ShipTransform;
            if (shipTransform == null) return;

            var prismTf = prismImpactee.Prism.prismProperties.prism.transform;
            var cross   = Vector3.Cross(shipTransform.forward, prismTf.forward);
            var normal  = Quaternion.AngleAxis(15f, cross) * prismTf.forward;
            
            var reflectRight = Vector3.Reflect(shipTransform.right, normal).normalized;
            var reflectUp    = Vector3.Reflect(shipTransform.up,    normal).normalized;

            var lateralDir = reflectRight;
            if (randomizeLeftRight && Random.value < 0.5f)
                lateralDir = -lateralDir;

            var newForward = Vector3.Cross(reflectUp, lateralDir).normalized;

            vesselStatus.VesselTransformer.GentleSpinShip(newForward, reflectUp, 1f);
            vesselStatus.VesselTransformer.ModifyVelocity(lateralDir * lateralSpeed, Time.deltaTime * accelScale);
        }
    }
}

using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselDeviationByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselDeviationByPrismEffectSO")]
    public class VesselDeviationByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float deviationAngle = 15f; // default: 45°

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor?.Ship?.ShipStatus;
            if (shipStatus == null) return;

            if(shipStatus.IsStationary) return;
            
            var t = shipStatus.ShipTransform;
            if (t == null) return;

            var sign = Random.value < 0.5f ? -1f : 1f;
            
            t.rotation = Quaternion.AngleAxis(sign * deviationAngle, t.up) * t.rotation;
            Debug.Log("GOt deviated");
        }
    }
}
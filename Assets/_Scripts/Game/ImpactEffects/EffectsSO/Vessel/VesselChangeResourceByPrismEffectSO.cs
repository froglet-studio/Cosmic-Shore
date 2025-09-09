using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselChangeResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/VesselChangeResourceByPrismEffectSO")]
    public class VesselChangeResourceByPrismEffectSO : VesselPrismEffectSO   // <- use the correct base for Prism
    {
        [SerializeField] ResourceChangeSpec _change;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var rs = vesselImpactor.Ship.ShipStatus.ResourceSystem;
            _change.ApplyTo(rs, this);
        }
    }
}
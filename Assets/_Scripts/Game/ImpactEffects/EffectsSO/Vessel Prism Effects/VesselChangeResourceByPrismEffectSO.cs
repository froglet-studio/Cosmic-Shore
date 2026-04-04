using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselChangeResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselChangeResourceByPrismEffectSO")]
    public class VesselChangeResourceByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] ResourceChangeSpec _change;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var rs = vesselImpactor?.Vessel?.VesselStatus?.ResourceSystem;
            if (rs == null) return;

            _change.ApplyTo(rs, this);
        }
    }
}
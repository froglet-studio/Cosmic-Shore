using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ChangeResourceImpactEffect", menuName = "ScriptableObjects/Impact Effects/ChangeResourceImpactEffectSO")]
    public class ShipChangeResourceEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField]
        int _resourceIndex;

        [SerializeField]
        float _resourceAmount;
        
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(_resourceIndex, _resourceAmount);
        }
    }
}

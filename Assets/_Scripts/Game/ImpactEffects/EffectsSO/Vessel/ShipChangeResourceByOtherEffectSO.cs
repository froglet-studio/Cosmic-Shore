using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeResourceByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeResourceByOtherEffectSO")]
    public class ShipChangeResourceByOtherEffectSO : ShipOtherEffectSO
    {
        [SerializeField]
        int _resourceIndex;

        [SerializeField]
        float _resourceAmount;
        
        public override void Execute(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(_resourceIndex, _resourceAmount);
        }
    }
}

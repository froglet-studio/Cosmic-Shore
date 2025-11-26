using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeResourceByOtherEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeResourceByOtherEffectSO")]
    public class ShipChangeResourceByOtherEffectSO : ImpactEffectSO<ShipImpactor, ImpactorBase>
    {
        [SerializeField] int _resourceIndex;

        [SerializeField] float _resourceAmount;

        protected override void ExecuteTyped(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(_resourceIndex, _resourceAmount);
        }
    }
}
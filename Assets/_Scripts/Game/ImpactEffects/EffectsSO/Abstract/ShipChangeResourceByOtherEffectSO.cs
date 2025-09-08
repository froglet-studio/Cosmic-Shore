using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeResourceByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeResourceByOtherEffectSO")]
    public abstract class ShipChangeResourceByOtherEffectSO : ImpactEffectSO
    {
        [SerializeField]
        int _resourceIndex;

        [SerializeField, Range(0, 1)]
        float _resourceAmount;

        [SerializeField, Tooltip("FillUp must be false. " +
                                 "If allow Override true, the current resource amount will be overriden with the serialized amount value." +
                                 "If allow Override false, the serialized amount value will be added to the curent resource amount.")]
        bool allowOverride = false;
        
        public void Execute(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            var resourceSystem = shipImpactor.Ship.ShipStatus.ResourceSystem;
            
            if (allowOverride)
                resourceSystem.SetResourceAmount(_resourceIndex, _resourceAmount);  
            else
                resourceSystem.ChangeResourceAmount(_resourceIndex, _resourceAmount);
        }
    }
}

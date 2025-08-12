using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipStunEffect", menuName = "ScriptableObjects/Impact Effects/ShipStunEffectSO")]
    public class ShipStunEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase impactee)
        {
            impactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(.6f, 5);
        }
    }
}

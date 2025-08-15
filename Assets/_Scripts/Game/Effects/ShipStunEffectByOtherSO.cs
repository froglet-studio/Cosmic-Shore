using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipStunEffectByOther", menuName = "ScriptableObjects/Impact Effects/ShipStunEffectByOtherSO")]
    public class ShipStunEffectByOtherSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase impactee)
        {
            impactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(.6f, 5);
        }
    }
}

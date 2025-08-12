using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipKnockbackEffect", menuName = "ScriptableObjects/Impact Effects/ShipKnockbackEffectSO")]
    public class ShipKnockbackEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            /*var shipStatus = shipImpactor.Ship.ShipStatus;
            
            if (shipStatus.Team == context.ImpactedShipStatus.Team)
                shipStatus.ShipTransformer.ModifyThrottle(10, 6); // TODO: the magic number here needs tuning after switch to additive
            else
                shipStatus.ShipTransformer.ModifyVelocity(context.ImpactVector * 100, 3);*/
        }
    }
}
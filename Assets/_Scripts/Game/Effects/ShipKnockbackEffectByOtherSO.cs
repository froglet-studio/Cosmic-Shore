using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipKnockbackEffectByOther", menuName = "ScriptableObjects/Impact Effects/ShipKnockbackEffectByOtherSO")]
    public class ShipKnockbackEffectByOtherSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            Vector3 impactVector = (impactee.transform.position - shipImpactor.transform.position).normalized;
            var impactorShipStatus = shipImpactor.Ship.ShipStatus;

            if (impactee is R_ShipImpactor shipImpactee)
            {
                // if both impactor ship and impactee ship teams are same
                if (impactorShipStatus.Team == shipImpactee.Ship.ShipStatus.Team)
                    impactorShipStatus.ShipTransformer.ModifyThrottle(10, 6); // TODO: the magic number here needs tuning after switch to additive    
            }
            else
                impactorShipStatus.ShipTransformer.ModifyVelocity(impactVector * 100, 3);
        }
    }
}
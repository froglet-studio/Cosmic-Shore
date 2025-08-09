using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "KnockbackImpactEffect", menuName = "ScriptableObjects/Impact Effects/KnockbackImpactEffectSO")]
    public class KnockbackEffectSO : ImpactEffectSO
    {
        /*public void Execute(ImpactEffectData context)
        {
            var shipStatus = context.ThisShipStatus;

            if (shipStatus.Team == context.ImpactedShipStatus.Team)
            {

                shipStatus.ShipTransformer.ModifyThrottle(10, 6); // TODO: the magic number here needs tuning after switch to additive
            }
            else
                shipStatus.ShipTransformer.ModifyVelocity(context.ImpactVector * 100, 3);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "KnockbackImpactEffect", menuName = "ScriptableObjects/Impact Effects/KnockbackImpactEffectSO")]
    public class KnockbackEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            var shipStatus = context.ShipStatus;

            if (shipStatus.Team == context.OwnTeam)
            {
                
                shipStatus.ShipTransformer.ModifyThrottle(10, 6); // TODO: the magic number here needs tuning after switch to additive
            }
            else 
                shipStatus.ShipTransformer.ModifyVelocity(context.ImpactVector * 100, 3);
        }
    }
}
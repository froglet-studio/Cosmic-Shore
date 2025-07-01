using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "StunImpactEffect", menuName = "ScriptableObjects/Impact Effects/StunImpactEffectSO")]
    public class StunEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.Ship.ShipStatus.ShipTransformer.ModifyThrottle(.6f, 5);
        }
    }
}

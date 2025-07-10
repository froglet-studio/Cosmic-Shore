using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "StunImpactEffect", menuName = "ScriptableObjects/Impact Effects/StunImpactEffectSO")]
    public class StunEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ShipTransformer.ModifyThrottle(.6f, 5);
        }
    }
}

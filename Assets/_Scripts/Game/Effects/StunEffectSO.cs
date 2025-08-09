using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "StunImpactEffect", menuName = "ScriptableObjects/Impact Effects/StunImpactEffectSO")]
    public class StunEffectSO : ImpactEffectSO
    {
        /*public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ShipTransformer.ModifyThrottle(.6f, 5);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}

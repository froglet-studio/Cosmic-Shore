using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SpinAroundImpactEffect", menuName = "ScriptableObjects/Impact Effects/SpinAroundImpactEffectSO")]
    public class SpinAroundEffectSO : ImpactEffectSO
    {
        /*public void Execute(ImpactEffectData context)
        {
            context.ThisShipStatus.ShipTransformer.SpinShip(context.ImpactVector);
        }*/

        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}

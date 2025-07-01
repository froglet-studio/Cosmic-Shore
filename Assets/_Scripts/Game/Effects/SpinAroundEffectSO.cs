using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SpinAroundImpactEffect", menuName = "ScriptableObjects/Impact Effects/SpinAroundImpactEffectSO")]
    public class SpinAroundEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.Ship.ShipStatus.ShipTransformer.SpinShip(context.ImpactVector);
        }
    }
}

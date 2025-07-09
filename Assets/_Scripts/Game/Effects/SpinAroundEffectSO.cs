using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SpinAroundImpactEffect", menuName = "ScriptableObjects/Impact Effects/SpinAroundImpactEffectSO")]
    public class SpinAroundEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        public void Execute(ImpactEffectData context)
        {
            context.ThisShipStatus.ShipTransformer.SpinShip(context.ImpactVector);
        }
    }
}

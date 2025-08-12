using System.Net.NetworkInformation;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipSpinAroundEffect", menuName = "ScriptableObjects/Impact Effects/ShipSpinAroundEffectSO")]
    public class ShipSpinAroundEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        /*public void Execute(ImpactEffectData context)
        {
            context.ThisShipStatus.ShipTransformer.SpinShip(context.ImpactVector);
        }*/

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            throw new System.NotImplementedException();
        }
    }
}

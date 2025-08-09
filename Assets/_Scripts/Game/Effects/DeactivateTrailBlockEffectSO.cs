using CosmicShore.Core;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DeactivateTrailBlockImpactEffect", menuName = "ScriptableObjects/Impact Effects/DeactivateTrailBlockImpactEffectSO")]
    public class DeactivateTrailBlockEffectSO : ImpactEffectSO
    {
        /*public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            trailBlockProperties.trailBlock.Damage(
                data.ThisShipStatus.Course * data.ThisShipStatus.Speed * data.ThisShipStatus.GetInertia, 
                data.ThisShipStatus.Team, data.ThisShipStatus.PlayerName);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}

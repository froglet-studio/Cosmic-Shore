using CosmicShore.Core;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "StealImpactEffect", menuName = "ScriptableObjects/Impact Effects/StealImpactEffectSO")]
    public class StealEffectSO : ImpactEffectSO
    {
        /*public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            if (data == null || trailBlockProperties == null)
            {
                Debug.LogError("ImpactEffectData or TrailBlockProperties is null.");
                return;
            }

            trailBlockProperties.trailBlock.Steal(data.ThisShipStatus.PlayerName, data.ThisShipStatus.Team);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}

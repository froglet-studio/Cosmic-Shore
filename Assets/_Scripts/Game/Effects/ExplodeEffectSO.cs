using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ExplodeImpactEffect", menuName = "ScriptableObjects/Impact Effects/ExplodeImpactEffectSO")]
    public class ExplodeEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        float _inertia;

        public override void Execute(ImpactContext context)
        {
            var shipStatus = context.ShipStatus;

            context.TrailBlockProperties.trailBlock.Damage(context.ShipStatus.Course * context.ShipStatus.Speed * _inertia,
                                shipStatus.Team, shipStatus.Player.PlayerName);
        }
    }
}

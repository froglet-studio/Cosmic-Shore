using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    // TODO: Figure out a way to separate the IExplodableImpactEffect, and ITrailBlockImpactEffect

    [CreateAssetMenu(fileName = "ShipExplodePrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipExplodePrismEffectSO")]
    public class ShipExplodePrismEffectSO : ImpactEffectSO<R_ShipImpactor, R_PrismImpactor>
    {
        [SerializeField]
        float _inertia;

        [SerializeField]
        GameObject[] _aoePrefabs;

        [SerializeField]
        float _minExplosionScale;

        [SerializeField]
        float _maxExplosionScale;

        [SerializeField]
        float _charge;
        
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_PrismImpactor prismImpactee)
        {
            var shipStatus = shipImpactor.Ship.ShipStatus;
            
            prismImpactee.TrailBlock.Damage(_inertia * shipStatus.Speed * shipStatus.Course,
                shipStatus.Team, shipStatus.PlayerName);
            
            foreach (var AOE in _aoePrefabs)
            {
                var aoeExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam = shipStatus.Team,
                    Ship = shipStatus.Ship,
                    MaxScale = Mathf.Lerp(_minExplosionScale, _maxExplosionScale, _charge),
                    OverrideMaterial = shipStatus.AOEExplosionMaterial,
                    AnnonymousExplosion = false
                });

                Transform shipTransform = shipStatus.ShipTransform;
                aoeExplosion.SetPositionAndRotation(shipTransform.position, shipTransform.rotation);
                aoeExplosion.Detonate();
            }
        }
    }
}

using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    // TODO - Separate Damage Effect and Explode Effect by asking Garrett
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
            Transform shipTransform = shipStatus.ShipTransform;
            
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
                    AnnonymousExplosion = false,
                    SpawnPosition = shipTransform.position,
                    SpawnRotation = shipTransform.rotation,
                });
                
                aoeExplosion.Detonate();
            }
        }
    }
}

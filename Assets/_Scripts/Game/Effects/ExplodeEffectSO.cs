using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    // TODO: Figure out a way to separate the IExplodableImpactEffect, and ITrailBlockImpactEffect

    [CreateAssetMenu(fileName = "ExplodeImpactEffect", menuName = "ScriptableObjects/Impact Effects/ExplodeImpactEffectSO")]
    public class ExplodeEffectSO : ImpactEffectSO, IExplodableImpactEffect, ITrailBlockImpactEffect
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

        public void Execute(IExplodable explodable)
        {
            if (explodable == null)
            {
                Debug.LogWarning("Explodable is null, cannot execute ExplodeEffectSO.");
                return;
            }

            explodable.Explode();
            // _poolManager.ReturnToPool(gameObject, gameObject.tag);

            // TODO -> Check out for Explodable Projectile detonate mechanics. and figure out how to access poolmanager
        }

        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            var shipStatus = data.ThisShipStatus;

            trailBlockProperties.trailBlock.Damage(_inertia * shipStatus.Speed * shipStatus.Course,
                                shipStatus.Team, shipStatus.PlayerName);

            foreach (var AOE in _aoePrefabs)
            {
                var AOEExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                AOEExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam = data.ThisShipStatus.Team,
                    Ship = data.ThisShipStatus.Ship,
                    MaxScale = Mathf.Lerp(_minExplosionScale, _maxExplosionScale, _charge),
                    OverrideMaterial = data.ThisShipStatus.AOEExplosionMaterial,
                    AnnonymousExplosion = false
                });

                Transform shipTransform = shipStatus.ShipTransform;
                AOEExplosion.SetPositionAndRotation(shipTransform.position, shipTransform.rotation);
                AOEExplosion.Detonate();
            }
        }
    }
}

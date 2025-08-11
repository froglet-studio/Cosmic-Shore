using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    // TODO: Figure out a way to separate the IExplodableImpactEffect, and ITrailBlockImpactEffect

    [CreateAssetMenu(fileName = "ShipExplodePrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipExplodePrismEffectSO")]
    public class ShipExplodePrismEffectSO : ImpactEffectSO
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

        /*public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            var shipStatus = data.ThisShipStatus;

            trailBlockProperties.trailBlock.Damage(_inertia * shipStatus.Speed * shipStatus.Course,
                                shipStatus.Team, shipStatus.PlayerName);

            foreach (var AOE in _aoePrefabs)
            {
                var aoeExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam = data.ThisShipStatus.Team,
                    Ship = data.ThisShipStatus.Ship,
                    MaxScale = Mathf.Lerp(_minExplosionScale, _maxExplosionScale, _charge),
                    OverrideMaterial = data.ThisShipStatus.AOEExplosionMaterial,
                    AnnonymousExplosion = false
                });

                Transform shipTransform = shipStatus.ShipTransform;
                aoeExplosion.SetPositionAndRotation(shipTransform.position, shipTransform.rotation);
                aoeExplosion.Detonate();
            }
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            if (impactor is not R_IShipImpactor shipImpactor)
            {
                Debug.LogError("No IShipImpactor found to run this effect!");
                return;
            }

            if (impactee is not R_IPrismImpactor prismImpactor)
            {
                Debug.LogError("No IPrismImpactor found to run this effect!");
                return;
            }

            var shipStatus = shipImpactor.Ship.ShipStatus;
            prismImpactor.TrailBlock.Damage(_inertia * shipStatus.Speed * shipStatus.Course,
                shipStatus.Team, shipStatus.PlayerName);
        }
    }
}

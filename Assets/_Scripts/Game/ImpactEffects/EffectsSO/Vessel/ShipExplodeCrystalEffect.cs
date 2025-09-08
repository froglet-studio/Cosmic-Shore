using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipExplodeCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipExplodeCrystalEffect")]
    public class ShipExplodeCrystalEffect : ImpactEffectSO<ShipImpactor, CrystalImpactor>
    {
        [Header("Damage")]
        [SerializeField] float _inertia;

        [Header("Explosion")]
        [SerializeField] GameObject[] _aoePrefabs;

        [SerializeField] float _minExplosionScale = 0.75f;
        [SerializeField] float _maxExplosionScale = 1.5f;

        [SerializeField] float _charge = 1f;

        protected override void ExecuteTyped(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            var shipStatus = shipImpactor.Ship.ShipStatus;

            var spawnPos     = crystalImpactee.transform.position;
            var spawnRot     = crystalImpactee.transform.rotation;

            var maxScale = Mathf.Lerp(_minExplosionScale, _maxExplosionScale, _charge);

            foreach (var prefab in _aoePrefabs)
            {
                if (!prefab) continue;

                var instance = Instantiate(prefab, spawnPos, spawnRot);

                if (!instance.TryGetComponent<AOEExplosion>(out var aoeExplosion))
                    continue;

                aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam              = shipStatus.Team,
                    Ship                 = shipStatus.Ship,
                    MaxScale             = maxScale,
                    OverrideMaterial     = shipStatus.AOEExplosionMaterial,
                    AnnonymousExplosion  = false,
                    SpawnPosition        = spawnPos,
                    SpawnRotation        = spawnRot,
                });

                aoeExplosion.Detonate();
            }
        }
    }
}
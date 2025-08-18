using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipAreaOfEffectExplosionByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipAreaOfEffectExplosionByOtherEffectSO")]
    public class ShipAreaOfEffectExplosionByOtherEffectSO : ImpactEffectSO<ShipImpactor, ImpactorBase>
    {
        [SerializeField]
        AOEExplosion _prefabGO;

        [SerializeField]
        int _ammoResourceIndex;

        [SerializeField]
        float _minExplosionScale;

        [SerializeField]
        float _maxExplosionScale;

        [SerializeField]
        Material _aoeExplosionMaterial;
        
        protected override void ExecuteTyped(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            IShipStatus shipStatus = shipImpactor.Ship.ShipStatus;
            Transform shipTransform = shipStatus.ShipTransform;
            var aoeExplosion = Instantiate(_prefabGO).GetComponent<AOEExplosion>();
            aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnTeam = shipStatus.Team,
                Ship = shipStatus.Ship,
                OverrideMaterial = _aoeExplosionMaterial,

                MaxScale = shipStatus.ResourceSystem.Resources.Count > _ammoResourceIndex
                    ? Mathf.Lerp(_minExplosionScale, _maxExplosionScale, shipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount)
                    : _maxExplosionScale,

                AnnonymousExplosion = true,
                SpawnPosition = shipTransform.position,
                SpawnRotation = shipTransform.rotation,
            });
            
            aoeExplosion.Detonate();
        }
    }
}

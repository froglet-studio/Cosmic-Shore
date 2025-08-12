using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipAreaOfEffectExplosionEffect", menuName = "ScriptableObjects/Impact Effects/ShipAreaOfEffectExplosionEffectSO")]
    public class ShipAreaOfEffectExplosionEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
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
        
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            IShipStatus shipStatus = shipImpactor.Ship.ShipStatus;
            
            var aoeExplosion = Instantiate(_prefabGO).GetComponent<AOEExplosion>();
            aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnTeam = shipStatus.Team,
                Ship = shipStatus.Ship,
                OverrideMaterial = _aoeExplosionMaterial,

                MaxScale = shipStatus.ResourceSystem.Resources.Count > _ammoResourceIndex
                    ? Mathf.Lerp(_minExplosionScale, _maxExplosionScale, shipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount)
                    : _maxExplosionScale,

                AnnonymousExplosion = true
            });
            Transform shipTransform = shipStatus.ShipTransform;
            aoeExplosion.SetPositionAndRotation(shipTransform.position, shipTransform.rotation);
            aoeExplosion.Detonate();
        }
    }
}

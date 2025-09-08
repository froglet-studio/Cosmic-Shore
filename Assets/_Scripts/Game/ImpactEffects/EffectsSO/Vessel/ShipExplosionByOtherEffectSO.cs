using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipExplosionByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipExplosionByOtherEffectSO")]
    public abstract class ShipExplosionByOtherEffectSO : ImpactEffectSO
    {
        [SerializeField]
        AOEExplosion[] _aoePrefabs;

        [SerializeField]
        float _minExplosionScale;

        [SerializeField]
        float _maxExplosionScale;

        [SerializeField]
        int _resourceIndex;

        [SerializeField]
        Material _aoeExplosionMaterial;
        
        /*public override void Execute(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            IShipStatus shipStatus = shipImpactor.Ship.ShipStatus;
            Transform shipTransform = shipStatus.ShipTransform;
            var aoeExplosion = Instantiate(_prefabGO).GetComponent<AOEExplosion>();
            aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnTeam = shipStatus.Team,
                Ship = shipStatus.Ship,
                OverrideMaterial = _aoeExplosionMaterial,

                MaxScale = shipStatus.ResourceSystem.Resources.Count > _resourceIndex
                    ? Mathf.Lerp(_minExplosionScale, _maxExplosionScale, shipStatus.ResourceSystem.Resources[_resourceIndex].CurrentAmount)
                    : _maxExplosionScale,

                AnnonymousExplosion = true,
                SpawnPosition = shipTransform.position,
                SpawnRotation = shipTransform.rotation,
            });
            
            aoeExplosion.Detonate();
        }*/

        public void Execute(ShipImpactor impactor, ImpactorBase impactee)
        {
            ExplosionHelper.CreateExplosion(
                _aoePrefabs,
                impactor,
                _minExplosionScale,
                _maxExplosionScale,
                _aoeExplosionMaterial,
                _resourceIndex);
        }
    }
}

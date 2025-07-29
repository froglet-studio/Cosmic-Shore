using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AreaOfEffectExplosionImpactEffect", menuName = "ScriptableObjects/Impact Effects/AreaOfEffectExplosionImpactEffectSO")]
    public class AreaOfEffectExplosionEffectSO : ImpactEffectSO, IBaseImpactEffect
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

        public void Execute(ImpactEffectData data)
        {
            var aoeExplosion = Instantiate(_prefabGO).GetComponent<AOEExplosion>();
            aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnTeam = data.ThisShipStatus.Team,
                Ship = data.ThisShipStatus.Ship,
                OverrideMaterial = _aoeExplosionMaterial,

                MaxScale = data.ThisShipStatus.ResourceSystem.Resources.Count > _ammoResourceIndex
                ? Mathf.Lerp(_minExplosionScale, _maxExplosionScale, data.ThisShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount)
                : _maxExplosionScale,

                AnnonymousExplosion = true
            });
            Transform shipTransform = data.ThisShipStatus.ShipTransform;
            aoeExplosion.SetPositionAndRotation(shipTransform.position, shipTransform.rotation);
            aoeExplosion.Detonate();
        }
    }
}

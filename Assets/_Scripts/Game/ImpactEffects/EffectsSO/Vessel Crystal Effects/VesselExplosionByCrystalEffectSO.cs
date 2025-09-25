using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselExplosionByOmniCrystal", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselExplosionByCrystalEffectSO")]
    public class VesselExplosionByCrystalEffectSO : VesselCrystalEffectSO
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
        [SerializeField] 
        Vector3 _spawnOffset = new Vector3(0, 0, -5f); 
        
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            ExplosionHelper.CreateExplosion(
                _aoePrefabs,
                vesselImpactor,
                _minExplosionScale,
                _maxExplosionScale,
                _aoeExplosionMaterial,
                _resourceIndex, 
                _spawnOffset);
        }
        
        /*public override void Execute(VesselImpactor shipImpactor, ImpactorBase impactee)
        {
            IVesselStatus vesselStatus = shipImpactor.Vessel.VesselStatus;
            Transform shipTransform = vesselStatus.ShipTransform;
            var aoeExplosion = Instantiate(_prefabGO).GetComponent<AOEExplosion>();
            aoeExplosion.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnTeam = vesselStatus.Team,
                Vessel = vesselStatus.Vessel,
                OverrideMaterial = _aoeExplosionMaterial,

                MaxScale = vesselStatus.ResourceSystem.Resources.Count > _resourceIndex
                    ? Mathf.Lerp(_minExplosionScale, _maxExplosionScale, vesselStatus.ResourceSystem.Resources[_resourceIndex].CurrentAmount)
                    : _maxExplosionScale,

                AnnonymousExplosion = true,
                SpawnPosition = shipTransform.position,
                SpawnRotation = shipTransform.rotation,
            });

            aoeExplosion.Detonate();
        }*/
    }
}
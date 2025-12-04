using System;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselExplosionByOmniCrystal", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselExplosionByCrystalEffectSO")]
    public class VesselExplosionByCrystalEffectSO : VesselCrystalEffectSO
    {
        public static event Action<VesselImpactor> OnCrystalExplosionTriggered;
        public static event Action<VesselImpactor> OnMantaFlowerExplosion;

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
        
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            ExplosionHelper.CreateExplosion(
                _aoePrefabs,
                vesselImpactor,
                _minExplosionScale,
                _maxExplosionScale,
                _aoeExplosionMaterial,
                _resourceIndex, 
                _spawnOffset);

            if (vesselImpactor.Vessel.VesselStatus.VesselType == VesselClassType.Rhino)
            {
                OnCrystalExplosionTriggered?.Invoke(vesselImpactor);
            }
            if (vesselImpactor.Vessel.VesselStatus.VesselType == VesselClassType.Manta)
            {
                OnMantaFlowerExplosion?.Invoke(vesselImpactor);
            }
        }
    }
}
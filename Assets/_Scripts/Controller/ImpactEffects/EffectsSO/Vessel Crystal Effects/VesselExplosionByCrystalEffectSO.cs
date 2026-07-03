using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "VesselExplosionByOmniCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselExplosionByCrystalEffectSO")]
    public class VesselExplosionByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Events")]
        [SerializeField] private ScriptableEventVesselImpactor rhinoCrystalExplosionEvent;
        [SerializeField] private ScriptableEventVesselImpactor squirrelCrystalExplosionEvent;

        public static event Action<VesselImpactor> OnMantaFlowerExplosion;
        
      


        [Header("Explosion Settings")]
        [SerializeField] private AOEExplosion[] _aoePrefabs;
        [SerializeField] private float _minExplosionScale;
        [SerializeField] private float _maxExplosionScale;
        [SerializeField] private int _resourceIndex;
        [SerializeField] private Material _aoeExplosionMaterial;
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0, 0, -5f);

        [Header("Anti-Spam")]
        [Tooltip("Minimum time between explosions from the same vessel hitting a crystal.")]
        [SerializeField] private float _explosionCooldown = 0.15f;

        // Keyed by instance ID (not the impactor reference) so destroyed vessel
        // impactors are never retained by this static dictionary across scene loads.
        private static readonly Dictionary<int, float> _lastExplosionTimeByImpactor
            = new ();

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (vesselImpactor == null || vesselImpactor.Vessel == null)
                return;

            var now = Time.time;
            int impactorId = vesselImpactor.GetInstanceID();

            if (_lastExplosionTimeByImpactor.TryGetValue(impactorId, out var lastTime))
            {
                if (now - lastTime < _explosionCooldown)
                    return;
            }

            _lastExplosionTimeByImpactor[impactorId] = now;

            ExplosionHelper.CreateExplosion(
                _aoePrefabs,
                vesselImpactor,
                _minExplosionScale,
                _maxExplosionScale,
                _aoeExplosionMaterial,
                _resourceIndex,
                _spawnOffset);

            switch (vesselImpactor.Vessel.VesselStatus.VesselType)
            {
                case VesselClassType.Rhino:
                    rhinoCrystalExplosionEvent?.Raise(vesselImpactor);
                    break;
                case VesselClassType.Manta:
                    OnMantaFlowerExplosion?.Invoke(vesselImpactor);
                    break;
                case VesselClassType.Any:
                    break;
                case VesselClassType.Random:
                    break;
                case VesselClassType.Dolphin:
                    break;
                case VesselClassType.Urchin:
                    break;
                case VesselClassType.Grizzly:
                    break;
                case VesselClassType.Squirrel:
                    squirrelCrystalExplosionEvent?.Raise(vesselImpactor);
                    break;
                case VesselClassType.Serpent:
                    break;
                case VesselClassType.Termite:
                    break;
                case VesselClassType.Falcon:
                    break;
                case VesselClassType.Shrike:
                    break;
                case VesselClassType.Sparrow:
                    break;
            }
        }
    }
}
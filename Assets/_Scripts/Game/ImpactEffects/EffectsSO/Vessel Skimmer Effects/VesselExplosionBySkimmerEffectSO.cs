using System;
using System.Collections.Generic;
using CosmicShore.Game.Ship;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Game.UI;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Projectiles;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(
        fileName = "VesselExplosionBySkimmer",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselExplosionBySkimmerEffectSO")]
    public class VesselExplosionBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [SerializeField]
        ScriptableEventString OnJoustCollision;

        [Header("Explosion Settings")]
        [SerializeField] private AOEExplosion[] _aoePrefabs;
        [SerializeField] private float _minExplosionScale;
        [SerializeField] private float _maxExplosionScale;
        [SerializeField] private int _resourceIndex;
        [SerializeField] private Material _aoeExplosionMaterial;
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0, 0, -5f);

        [Header("Anti-Spam")]
        [Tooltip("Minimum time between explosions from the same vessel hitting a skimmer.")]
        [SerializeField] private float _explosionCooldown = 0.15f;

        private static readonly Dictionary<VesselImpactor, float> _lastExplosionTimeByImpactor
            = new();

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (impactor == null || impactor.Vessel == null)
                return;

            if (impactee == null || impactee.Skimmer.VesselStatus.Vessel == null)
                return;

            var impactorVessel = impactor.Vessel;
            var impacteeVessel = impactee.Skimmer.VesselStatus.Vessel;

            // Only trigger if the skimmer's vessel is faster than the impactor
            if (impacteeVessel.VesselStatus.Speed <= impactorVessel.VesselStatus.Speed)
                return;

            var impacteeVesselImpactor = impacteeVessel.Transform.GetComponent<VesselImpactor>();
            if (impacteeVesselImpactor == null)
                return;

            var now = Time.time;
            if (_lastExplosionTimeByImpactor.TryGetValue(impacteeVesselImpactor, out var lastTime))
            {
                if (now - lastTime < _explosionCooldown)
                    return;
            }

            _lastExplosionTimeByImpactor[impacteeVesselImpactor] = now;

            ExplosionHelper.CreateExplosion(
                _aoePrefabs,
                impacteeVesselImpactor,
                _minExplosionScale,
                _maxExplosionScale,
                _aoeExplosionMaterial,
                _resourceIndex,
                _spawnOffset);

            OnJoustCollision.Raise(impacteeVessel.VesselStatus.PlayerName);

            // Post two-tone joust notification to the game feed
            GameFeedAPI.PostJoust(
                impacteeVessel.VesselStatus.PlayerName,
                impacteeVessel.VesselStatus.Domain,
                impactorVessel.VesselStatus.PlayerName,
                impactorVessel.VesselStatus.Domain);
        }
    }
}
using System.Collections.Generic;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.UI;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "DogFightHitByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/DogFightHitByExplosionEffectSO")]
    public class DogFightHitByExplosionEffectSO : VesselExplosionEffectSO
    {
        [Header("Dog Fight")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] ScriptableEventString OnDogFightHit;

        [Header("Anti-Spam")]
        [Tooltip("Minimum time between hits from the same explosion on the same vessel.")]
        [SerializeField] float hitCooldown = 0.5f;

        // Dedup: (explosionInstanceId, vesselInstanceId) → last hit time
        static readonly Dictionary<(int, int), float> _hitTracker = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _hitTracker.Clear();

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            if (impactor == null || impactee == null)
                return;

            var explosion = impactee.GetComponent<AOEExplosion>();
            if (explosion == null || explosion.AnonymousExplosion)
                return;

            var shooterVessel = explosion.Vessel;
            if (shooterVessel?.VesselStatus == null)
                return;

            var shooterName = shooterVessel.VesselStatus.PlayerName;
            if (string.IsNullOrEmpty(shooterName))
                return;

            var victimVessel = impactor.Vessel;
            if (victimVessel?.VesselStatus == null)
                return;

            // Don't count self-hits
            if (shooterName == victimVessel.VesselStatus.PlayerName)
                return;

            // 1 hit per explosion per vessel
            var key = (impactee.GetInstanceID(), impactor.GetInstanceID());
            var now = Time.time;
            if (_hitTracker.TryGetValue(key, out var lastHit) && now - lastHit < hitCooldown)
                return;
            _hitTracker[key] = now;

            // Directly increment DogFightHits on the shooter's stats
            if (gameData && gameData.TryGetRoundStats(shooterName, out var roundStats))
                roundStats.DogFightHits++;

            if (OnDogFightHit)
                OnDogFightHit.Raise(shooterName);

            GameFeedAPI.PostDogFightHit(
                shooterName,
                shooterVessel.VesselStatus.Player.Domain,
                victimVessel.VesselStatus.PlayerName,
                victimVessel.VesselStatus.Player.Domain);
        }
    }
}

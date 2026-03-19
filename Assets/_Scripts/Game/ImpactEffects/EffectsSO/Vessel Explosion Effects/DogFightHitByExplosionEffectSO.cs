using System.Collections.Generic;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.UI;
using CosmicShore.Utility;
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
            {
                CSDebug.Log("[DogFightExplosion] SKIP: impactor or impactee is null");
                return;
            }

            var explosion = impactee.GetComponent<AOEExplosion>();
            if (explosion == null)
            {
                CSDebug.Log("[DogFightExplosion] SKIP: no AOEExplosion component on impactee");
                return;
            }
            if (explosion.AnonymousExplosion)
            {
                CSDebug.Log("[DogFightExplosion] SKIP: anonymous explosion");
                return;
            }

            var shooterVessel = explosion.Vessel;
            if (shooterVessel?.VesselStatus == null)
            {
                CSDebug.Log("[DogFightExplosion] SKIP: shooter vessel or status is null");
                return;
            }

            var shooterName = shooterVessel.VesselStatus.PlayerName;
            if (string.IsNullOrEmpty(shooterName))
            {
                CSDebug.Log("[DogFightExplosion] SKIP: shooter name is empty");
                return;
            }

            var victimVessel = impactor.Vessel;
            if (victimVessel?.VesselStatus == null)
            {
                CSDebug.Log($"[DogFightExplosion] SKIP: victim vessel or status is null (shooter={shooterName})");
                return;
            }

            // Don't count self-hits
            if (shooterName == victimVessel.VesselStatus.PlayerName)
            {
                CSDebug.Log($"[DogFightExplosion] SKIP: self-hit ({shooterName})");
                return;
            }

            // 1 hit per explosion per vessel
            var key = (impactee.GetInstanceID(), impactor.GetInstanceID());
            var now = Time.time;
            if (_hitTracker.TryGetValue(key, out var lastHit) && now - lastHit < hitCooldown)
            {
                CSDebug.Log($"[DogFightExplosion] SKIP: cooldown ({shooterName} → {victimVessel.VesselStatus.PlayerName}, " +
                          $"elapsed={now - lastHit:F2}s < {hitCooldown:F2}s)");
                return;
            }
            _hitTracker[key] = now;

            CSDebug.Log($"[DogFightExplosion] HIT! {shooterName} → {victimVessel.VesselStatus.PlayerName} " +
                      $"(explosion={impactee.GetInstanceID()}, vessel={impactor.GetInstanceID()})");

            OnDogFightHit.Raise(shooterName);

            GameFeedAPI.PostDogFightHit(
                shooterName,
                shooterVessel.VesselStatus.Player.Domain,
                victimVessel.VesselStatus.PlayerName,
                victimVessel.VesselStatus.Player.Domain);
        }
    }
}

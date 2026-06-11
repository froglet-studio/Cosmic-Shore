using CosmicShore.Game.UI;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "DogFightHitByProjectileEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/DogFightHitByProjectileEffectSO")]
    public class DogFightHitByProjectileEffectSO : VesselProjectileEffectSO
    {
        [Header("Dog Fight")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] ScriptableEventString OnDogFightHit;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            if (impactor == null || impactee?.Projectile == null)
                return;

            var shooterStatus = impactee.Projectile.VesselStatus;
            if (shooterStatus == null)
                return;

            var shooterName = shooterStatus.PlayerName;
            if (string.IsNullOrEmpty(shooterName))
                return;

            var victimVessel = impactor.Vessel;
            if (victimVessel?.VesselStatus == null)
                return;

            // Don't count self-hits
            if (shooterStatus.PlayerName == victimVessel.VesselStatus.PlayerName)
                return;

            // Directly increment DogFightHits on the shooter's stats
            if (gameData && gameData.TryGetRoundStats(shooterName, out var roundStats))
                roundStats.DogFightHits++;

            if (OnDogFightHit)
                OnDogFightHit.Raise(shooterName);

            GameFeedAPI.PostDogFightHit(
                shooterName,
                shooterStatus.Player.Domain,
                victimVessel.VesselStatus.PlayerName,
                victimVessel.VesselStatus.Player.Domain);
        }
    }
}

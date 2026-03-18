using CosmicShore.Game.Projectiles;
using CosmicShore.Game.UI;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselExplosiveJoustByExplosion",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselExplosiveJoustByExplosionEffectSO")]
    public class VesselExplosiveJoustByExplosionEffectSO : VesselExplosionEffectSO
    {
        [Header("Events")]
        [SerializeField] private ScriptableEventString onExplosiveJoustCollision;

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            var victimVessel = impactor?.Vessel;
            if (victimVessel == null) return;

            var explosion = impactee != null ? impactee.GetComponent<AOEExplosion>() : null;
            if (explosion == null || explosion.Vessel == null) return;

            // Skip self-hits
            if (victimVessel == explosion.Vessel) return;

            // Skip same-domain hits
            if (victimVessel.VesselStatus.Domain == explosion.Domain) return;

            // Score goes to the Manta that created the explosion
            var attackerName = explosion.Vessel.VesselStatus.PlayerName;
            onExplosiveJoustCollision?.Raise(attackerName);

            GameFeedAPI.PostJoust(
                attackerName,
                explosion.Domain,
                victimVessel.VesselStatus.PlayerName,
                victimVessel.VesselStatus.Domain);
        }
    }
}

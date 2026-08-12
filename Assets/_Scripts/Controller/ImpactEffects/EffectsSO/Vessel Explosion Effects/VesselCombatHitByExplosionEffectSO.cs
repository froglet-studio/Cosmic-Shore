using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Records an opposing vessel being CAUGHT IN A BLAST as a scoreable combat hit - the half
    /// of missile scoring that a direct-hit effect cannot see, and in a dogfight the half that
    /// matters most: a rocket that detonates near a jinking pilot still counts.
    ///
    /// Deliberately the same weight as a direct strike. "Hit by the missile OR caught in its
    /// blast radius" is one event to the scoreboard, which is why both this and
    /// <see cref="VesselCombatHitByProjectileEffectSO"/> claim the SAME
    /// <see cref="VesselCombatHitLatch"/> window: a skyburst detonates on its own direct hit,
    /// so for a clean centre-punch both effects fire for one rocket and exactly one of them
    /// scores.
    ///
    /// <b>Authority.</b> The blast is instantiated by the machine that owned the projectile, so
    /// like the direct-hit effect this runs on the shooter's machine alone and raises
    /// unconditionally; <c>StatsManager.CombatHitLanded</c> arbitrates server-vs-client
    /// attribution.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselCombatHitByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselCombatHitByExplosionEffectSO")]
    public class VesselCombatHitByExplosionEffectSO : VesselExplosionEffectSO
    {
        [Header("Scoring")]
        [Tooltip("Which weapon class this blast counts as. Missile for the Sparrow's skyburst; " +
                 "the field exists so a future non-rocket blast can be scored differently.")]
        [SerializeField] CombatHitClass hitClass = CombatHitClass.Missile;

        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager listens on. " +
                 "Fail-loud: a missing reference throws rather than silently un-scoring the mode.")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Tooltip("Seconds before the same shooter can score again on the same victim with this " +
                 "weapon class. MUST match the direct-hit effect's window - they share one latch, " +
                 "and that shared window is what stops a rocket scoring twice (direct strike + " +
                 "its own blast) on a clean hit.")]
        [SerializeField, Min(0f)] float sameVictimCooldownSeconds = 0.5f;

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            // As in the projectile effect, ExplosionImpactor.AcceptImpactee passes the VESSEL
            // as `impactor`: here that is the victim, and `impactee` is the blast.
            var victimStatus = impactor?.Vessel?.VesselStatus;
            var shooterStatus = impactee?.SourceVessel?.VesselStatus;

            // An anonymous blast has no pilot to credit - SourceVessel is null and it silently
            // scores for nobody, which is correct: nobody fired it.
            if (victimStatus == null || shooterStatus == null) return;

            // Never score a pilot for their own blast, and never for a teammate's. The
            // ExplosionImpactor already skips own-domain vessels unless the blast is running
            // friendly fire (the CHARGE-5 'Domain-Safe Skybursts' gate flips exactly that), so
            // without this check a pilot below that upgrade would be paid for splashing their
            // own wingman - and for splashing themselves.
            if (victimStatus.Domain == shooterStatus.Domain) return;
            if (ReferenceEquals(victimStatus, shooterStatus)) return;

            string shooterName = shooterStatus.PlayerName;
            string victimName = victimStatus.PlayerName;

            if (!VesselCombatHitLatch.TryAdmit(shooterName, victimName, hitClass, sameVictimCooldownSeconds))
                return;

            onCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = shooterName,
                VictimName = victimName,
                HitClass = hitClass,
            });
        }
    }
}

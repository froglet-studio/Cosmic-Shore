using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Records a DIRECT projectile hit on an opposing vessel as a scoreable combat hit.
    ///
    /// This is the gunnery counterpart of <c>VesselExplosionBySkimmerEffectSO</c>'s joust
    /// point: an impact effect that carries no gameplay consequence of its own (the spin, the
    /// debuff, the detonation are separate effects in the same container) and exists only to
    /// publish the fact that a shot connected. Keeping it separate is what lets a mode score
    /// gunnery without any vessel or weapon knowing which mode it is in.
    ///
    /// <b>Authority.</b> Projectiles are local objects with no NetworkObject, so this runs on
    /// exactly one machine: whichever fired the shot. It raises unconditionally and lets
    /// <c>StatsManager.CombatHitLanded</c> arbitrate - the server credits directly (its own
    /// guns and every AI's, since AI players are server-owned), a client forwards only its own
    /// shot through the Player object it owns, and an AI's gun that happens to also fire on a
    /// client is dropped there on the name check. That is the same arrangement the fauna kill
    /// path uses, and for the same reason.
    ///
    /// <b>The hit class is authored, not inferred.</b> One script serves both weapons: drop
    /// this asset into the full-auto container marked <see cref="CombatHitClass.Bullet"/> and
    /// into the skyburst container marked <see cref="CombatHitClass.Missile"/>. Nothing here
    /// inspects a prefab name or a projectile type to guess.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselCombatHitByProjectileEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselCombatHitByProjectileEffectSO")]
    public class VesselCombatHitByProjectileEffectSO : VesselProjectileEffectSO
    {
        [Header("Scoring")]
        [Tooltip("Which weapon class this container's shots count as. Authored per container - " +
                 "the same asset script sits in the Sparrow's full-auto container as Bullet and " +
                 "in its skyburst container as Missile.")]
        [SerializeField] CombatHitClass hitClass = CombatHitClass.Bullet;

        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager listens on. " +
                 "Fail-loud: a missing reference throws rather than silently un-scoring the mode.")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Tooltip("Seconds before the same shooter can score again on the same victim with this " +
                 "weapon class. A missile MUST use a non-zero window: a skyburst detonates on " +
                 "its own direct hit, so this effect and the blast effect both fire for one " +
                 "rocket. Also collapses the duplicate contacts a multi-collider hull generates. " +
                 "0 disables the latch.")]
        [SerializeField, Min(0f)] float sameVictimCooldownSeconds = 0.5f;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            // NOTE the argument order, which is inverted from most effects in this family:
            // ProjectileImpactor.AcceptImpactee passes the VESSEL as the impactor and itself as
            // the impactee, so here `impactor` is the victim and `impactee` carries the shot.
            var victimStatus = impactor?.Vessel?.VesselStatus;
            var projectile = impactee?.Projectile;
            var shooterStatus = projectile?.VesselStatus;
            if (victimStatus == null || shooterStatus == null) return;

            // A vessel class filter is available on the base for weapons that should only score
            // against particular hulls; empty (the default) means "any opponent".
            if (!IsVesselAllowedToImpact(victimStatus.VesselType, vesselTypesToImpact)) return;

            // Own-domain contact never scores. The projectile path already refuses it
            // (Projectile.DisallowImpactOnVessel), so this is unreachable today - it is here so
            // that a future weapon which CAN hit its own domain cannot start paying teammates.
            if (victimStatus.Domain == shooterStatus.Domain) return;

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

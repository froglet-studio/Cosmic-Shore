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
    /// <b>Authority.</b> Projectiles are local objects with no NetworkObject, but a human's press is
    /// replicated (owner, then server, then every peer through
    /// <c>R_VesselActionHandler.SendButtonPressed_ClientRpc</c>), so every peer fires its OWN copy
    /// of the round, the host included. Before Oct 2026 this effect raised on every copy and let
    /// <c>StatsManager.CombatHitLanded</c> arbitrate. That arbitration filters a client's replays
    /// by name, but the SERVER credits whatever it sees, so the host's replay of a client's round
    /// was credited directly and the client's own copy was credited again through
    /// <c>Player.ReportCombatHit_ServerRpc</c>: one hit, two scores (and one score for a hit only
    /// the host's lagged copy landed). So the effect now runs only on the machine that OWNS the
    /// shooter (<see cref="ElementalTransfer.IsDecidedHere"/>): the shooter's own client, or the
    /// server for the host and every AI. That is the same machine <see cref="CombatHitDrain"/>
    /// settles the petals from, so the score and the drain cannot disagree. Offline (an unspawned
    /// hull) every hit is decided here, as before.
    ///
    /// <b>The hit class is authored, not inferred.</b> One script serves both weapons: drop
    /// this asset into the full-auto container marked <see cref="CombatHitClass.Bullet"/> and
    /// into the skyburst container marked <see cref="CombatHitClass.MissileDirect"/>. Nothing here
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

            // Only the SHOOTER's owner reports, scores and drains this hit (see the class doc).
            // A human's press replicates, so this round also exists on every other peer, the host
            // included. Without this gate the host's replay of a client's shot was credited here
            // AND the client's own copy was credited again through ReportCombatHit_ServerRpc.
            if (!ElementalTransfer.IsDecidedHere(shooterStatus)) return;

            // A vessel class filter is available on the base for weapons that should only score
            // against particular hulls; empty (the default) means "any opponent".
            if (!IsVesselAllowedToImpact(victimStatus.VesselType, vesselTypesToImpact)) return;

            // Own-domain contact never scores. The projectile path already refuses it
            // (Projectile.DisallowImpactOnVessel), so this is unreachable today - it is here so
            // that a future weapon which CAN hit its own domain cannot start paying teammates.
            if (victimStatus.Domain == shooterStatus.Domain) return;

            string shooterName = shooterStatus.PlayerName;
            string victimName = victimStatus.PlayerName;

            if (!VesselCombatHitLatch.TryAdmit(shooterName, victimName, hitClass,
                                               sameVictimCooldownSeconds, out int supersededRank))
                return;

            // The round's own bite, priced off the same list its points come from - ten points
            // to the petal (so ten bullets cost a victim one). Netted against a superseded
            // missile tier, because a direct strike admits over its own blast.
            // The round's OWN velocity throws whatever it knocks loose - the same quantity that
            // throws prism debris, so a petal shot out of a pilot scatters exactly as far as a
            // prism struck at that speed would have. A ranged verb EJECTS, so the shooter is not
            // handed the petals; they have to come back through the crystals.
            CombatHitDrain.Apply(victimStatus, shooterStatus, hitClass, supersededRank,
                                 projectile.Velocity, ElementalDebuffSources.Other);

            onCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = shooterName,
                VictimName = victimName,
                HitClass = hitClass,
                SupersededRank = supersededRank,
            });
        }
    }
}

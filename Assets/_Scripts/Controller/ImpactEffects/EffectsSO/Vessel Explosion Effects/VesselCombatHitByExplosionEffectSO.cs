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
    /// <b>Authority.</b> The blast is instantiated by whichever machine flew the projectile, and a
    /// human's press is replicated, so every peer detonates its own copy. Like the direct-hit
    /// effect, this therefore reports only on the machine that OWNS the shooter
    /// (<see cref="ElementalTransfer.IsDecidedHere"/>). Before Oct 2026 it raised on every copy,
    /// and the server credited its replay of a client's rocket on top of the client's own
    /// <c>ReportCombatHit_ServerRpc</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselCombatHitByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselCombatHitByExplosionEffectSO")]
    public class VesselCombatHitByExplosionEffectSO : VesselExplosionEffectSO
    {
        [Header("Scoring")]
        [Tooltip("Which weapon class this blast counts as. A rocket lands in THREE ranked " +
                 "classes and an explosion can be two of them: MissileBlast for the skyburst's " +
                 "prism detonation, MissileShockwave for the warhead's outer blast. They share " +
                 "one latch window per victim, so the closest one a rocket achieves is what " +
                 "pays - see VesselCombatHitLatch. The field exists so a future non-rocket " +
                 "blast can be scored differently.")]
        [SerializeField] CombatHitClass hitClass = CombatHitClass.MissileBlast;

        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager listens on. " +
                 "Fail-loud: a missing reference throws rather than silently un-scoring the mode.")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Tooltip("Seconds before the same shooter can score again on the same victim with this " +
                 "weapon class. MUST match the direct-hit effect's window - they share one latch, " +
                 "and that shared window is what stops a rocket scoring twice (direct strike + " +
                 "its own blast) on a clean hit.")]
        [SerializeField, Min(0f)] float sameVictimCooldownSeconds = 0.5f;

        [Tooltip("Only report the hit on the machine that OWNS the shooting vessel. Off for a " +
                 "weapon whose blast exists on exactly one machine - a projectile is a pooled " +
                 "local object, so the machine that spawned it is the only one that can raise " +
                 "anything. ON for a blast that is REPLAYED onto more than one machine: the " +
                 "Dolphin's crystal blast is, because a crystal collection resolves server-side " +
                 "and NetworkCrystalManager.ReplayVesselCrystalEffects then re-runs the vessel " +
                 "effects on the owning client, so a client's one blast exists on BOTH the " +
                 "server and that client. StatsManager would then credit it twice - once from " +
                 "the server's own copy, once from the client's forwarded RPC - and the " +
                 "per-machine VesselCombatHitLatch cannot see across machines to stop it. " +
                 "IsNetworkOwner (not IsLocalUser) is the test, because an AI's vessel is " +
                 "server-owned and its hits must still be recorded. Since Oct 2026 every blast " +
                 "is gated on the shooter's owner regardless (ElementalTransfer.IsDecidedHere), " +
                 "because a replicated press puts EVERY blast on more than one machine; this " +
                 "field is kept because the mode generators author it, and in a spawned match it " +
                 "adds nothing to that gate.")]
        [SerializeField] bool requireOwningMachine = false;

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            // As in the projectile effect, ExplosionImpactor.AcceptImpactee passes the VESSEL
            // as `impactor`: here that is the victim, and `impactee` is the blast.
            var victimStatus = impactor?.Vessel?.VesselStatus;
            var shooterStatus = impactee?.SourceVessel?.VesselStatus;

            // An anonymous blast has no pilot to credit - SourceVessel is null and it silently
            // scores for nobody, which is correct: nobody fired it.
            if (victimStatus == null || shooterStatus == null) return;

            // Exactly one machine may report a blast that exists on several: the shooter's owner,
            // the machine CombatHitDrain settles from (see the class doc). The field below is the
            // older, opt-in form of the same rule and is kept because the mode generators author it.
            if (!ElementalTransfer.IsDecidedHere(shooterStatus)) return;
            if (requireOwningMachine && shooterStatus.Player is { IsNetworkOwner: false }) return;

            // Never score a pilot for their own blast, and never for a teammate's. The
            // ExplosionImpactor already skips own-domain vessels unless the blast is running
            // friendly fire (the CHARGE-5 'Domain-Safe Skybursts' gate flips exactly that), so
            // without this check a pilot below that upgrade would be paid for splashing their
            // own wingman - and for splashing themselves.
            if (victimStatus.Domain == shooterStatus.Domain) return;
            if (ReferenceEquals(victimStatus, shooterStatus)) return;

            // One gate for the score and the petals: a warded victim is neither scored on nor
            // robbed (CombatHitDrain.TryAdmit). Asked about THIS blast's own class, Explosion, the
            // same one the drain below and the sibling debuff effect are warded by. This used to be
            // an opt-in flag (requireDebuffableVictim) that rockets left off, so a rocket scored
            // through a ward its victim's petals were safe behind.
            if (!CombatHitDrain.TryAdmit(victimStatus, shooterStatus, hitClass, sameVictimCooldownSeconds,
                                         ElementalDebuffSources.Explosion, out int supersededRank))
                return;

            // A hit bites in proportion to what it is worth - ten points to the petal, netted
            // against whatever tier this admission supersedes, so one rocket drains its BEST
            // tier and never the sum of the three it can land in. See CombatHitDrain: the
            // Debuff class is absent from that table because its drain is authored per weapon
            // (this same container carries it), so adding this call cannot double it.
            // BlastImpactVector is the blast's own answer for "which way, how hard, at this
            // point" - radial for a sphere, the sweep axis for a plate - so an ejected petal
            // leaves the way the blast was travelling rather than along some invented normal.
            // This is the same accessor the crystal->ball forge had to adopt for the same reason.
            Vector3 blastVelocity = impactee.BlastImpactVector(impactor.transform.position);

            CombatHitDrain.Apply(victimStatus, shooterStatus, hitClass, supersededRank,
                                 blastVelocity, ElementalDebuffSources.Explosion);

            onCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = shooterStatus.PlayerName,
                VictimName = victimStatus.PlayerName,
                HitClass = hitClass,
                SupersededRank = supersededRank,
            });
        }
    }
}

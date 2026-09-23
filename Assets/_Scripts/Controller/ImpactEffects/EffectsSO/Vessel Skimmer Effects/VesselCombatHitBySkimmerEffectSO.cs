using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Records a CONTACT hit on an opposing vessel as a scoreable combat hit - the skimmer
    /// sibling of <see cref="VesselCombatHitByProjectileEffectSO"/> and
    /// <c>VesselCombatHitByExplosionEffectSO</c>.
    ///
    /// Like both of those it carries no gameplay consequence of its own: the damage, the spin
    /// and the joust explosion are separate effects already sitting in the same container, and
    /// this one exists only to publish the fact that the contact happened. That separation is
    /// what lets a mode score a blade without the Rhino, the Squirrel or their skimmers knowing
    /// which mode they are in - the two hulls it arms were landing real, felt hits long before
    /// anything counted them.
    ///
    /// <b>AUTHORITY IS THE WHOLE DIFFICULTY, AND IT IS NOT THE PROJECTILE'S.</b> A projectile is
    /// a pooled LOCAL object, so its effect may raise unconditionally and let
    /// <c>StatsManager.CombatHitLanded</c> arbitrate - the shot exists on exactly one machine. A
    /// skimmer is not: both vessels are replicated, so PhysX raises this overlap on EVERY peer.
    /// Raising unconditionally would have the server credit its own observation AND the
    /// shooter's client forward the same contact, and the latch cannot catch it because the
    /// latch is per-machine. That is the double-credit The Bends recorded for a replayed blast,
    /// reached from the other direction, so <see cref="requireOwningMachine"/> gates on the
    /// machine that OWNS the shooter and defaults to on.
    ///
    /// <c>IsNetworkOwner</c> rather than <c>IsLocalUser</c> is the test, for the reason the
    /// explosion effect already records: an AI's vessel is server-owned and never "local", so a
    /// local-user gate would silently un-score every bot in the mode.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselCombatHitBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselCombatHitBySkimmerEffectSO")]
    public class VesselCombatHitBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Scoring")]
        [Tooltip("Which weapon class this container's contacts count as. Authored per container " +
                 "like every other combat-hit effect - Strike for the Rhino's sword and the " +
                 "Squirrel's joust. Nothing here inspects a prefab to guess.")]
        [SerializeField] CombatHitClass hitClass = CombatHitClass.Strike;

        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager listens on. " +
                 "Fail-loud: a missing reference throws rather than silently un-scoring the mode.")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Tooltip("Seconds before the same shooter can score again on the same victim with this " +
                 "class. A skimmer sphere overlaps a hull for many frames and a hull is several " +
                 "colliders, so a non-zero window is effectively mandatory here. 0 disables it.")]
        [SerializeField, Min(0f)] float sameVictimCooldownSeconds = 1f;

        [Header("Authority")]
        [Tooltip("Only the machine that OWNS the striking vessel reports the hit. Leave ON: a " +
                 "skimmer overlap is observed by every peer, so an unconditional raise double-" +
                 "credits (the server records its own view and the shooter's client forwards " +
                 "the same contact). Off is for an unspawned/offline container only.")]
        [SerializeField] bool requireOwningMachine = true;

        [Header("Contact rule")]
        [Tooltip("Require the STRIKING vessel to be moving faster than its victim - i.e. score " +
                 "only a genuine overtake. ON for the Squirrel, whose contact IS a joust and " +
                 "whose whole kit is speed; OFF for the Rhino, whose sword is swung and connects " +
                 "on its own terms regardless of who is travelling faster.")]
        [SerializeField] bool requireFasterThanVictim;

        /// <summary>
        /// NOTE the argument order, shared with every other skimmer effect and inverted from
        /// what the names suggest: <c>impactor</c> is the vessel that was SWEPT (the victim) and
        /// <c>impactee</c> is the skimmer doing the sweeping (the shooter).
        /// </summary>
        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            var victimStatus = impactor?.Vessel?.VesselStatus;
            var shooterStatus = impactee?.Skimmer?.VesselStatus;
            if (victimStatus == null || shooterStatus == null) return;
            if (shooterStatus.Vessel == null) return;

            // Own-domain contact never scores - a teammate sweep is a buff
            // (VesselOvertakeBySkimmerEffectSO), never a hit.
            if (victimStatus.Domain == shooterStatus.Domain) return;

            // See the class doc: a replicated contact is observed everywhere, so exactly one
            // machine may speak for it.
            if (requireOwningMachine && shooterStatus.Player is { IsNetworkOwner: false }) return;

            if (requireFasterThanVictim && shooterStatus.Speed <= victimStatus.Speed) return;

            string shooterName = shooterStatus.PlayerName;
            string victimName = victimStatus.PlayerName;

            if (!VesselCombatHitLatch.TryAdmit(shooterName, victimName, hitClass,
                                               sameVictimCooldownSeconds, out int supersededRank))
                return;

            // Routed through the same seam as every other reported hit, so the rule "a hit bites
            // what it is priced at" is structural rather than remembered. It is a NO-OP for the
            // Strike class shipped here - a contact strike's drain is authored per weapon (the
            // Squirrel's overtake mirrors it as an ally buff), so CombatHitDrain declines it.
            CombatHitDrain.Apply(victimStatus, hitClass, supersededRank,
                                 ElementalDebuffSources.VesselContact);

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

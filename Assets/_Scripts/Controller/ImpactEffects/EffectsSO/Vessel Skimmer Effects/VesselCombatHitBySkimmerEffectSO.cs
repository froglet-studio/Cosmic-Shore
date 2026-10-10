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
    /// It is the ONE place a contact hit is decided: it admits the hit
    /// (<see cref="CombatHitDrain.TryAdmit"/>), takes its petals through the weapon's own
    /// <see cref="IContactPetalTake"/> sibling in the same container, and publishes the score.
    /// A scored hit and a petal theft are one event (Garrett, 2026-10-10). That separation is
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
            // machine may speak for it. IsDecidedHere is the same predicate the projectile and
            // blast reporters and every authoritative petal transfer use (hull ownership), so the
            // overtake's steal and this score are decided on one machine.
            if (!ElementalTransfer.IsDecidedHere(shooterStatus)) return;
            if (requireOwningMachine && shooterStatus.Player is { IsNetworkOwner: false }) return;

            if (requireFasterThanVictim && shooterStatus.Speed <= victimStatus.Speed) return;

            // One gate for the score and the petals: a warded victim is neither scored on nor
            // robbed (CombatHitDrain.TryAdmit). Contact is the VesselContact class, the same one
            // the overtake's steal is warded by.
            if (!CombatHitDrain.TryAdmit(victimStatus, shooterStatus, hitClass, sameVictimCooldownSeconds,
                                         ElementalDebuffSources.VesselContact, out int supersededRank))
                return;

            // THE TAKE. A contact weapon's petals are authored per weapon, on a sibling in this same
            // container that implements IContactPetalTake (the overtake steal on the Squirrel and
            // the Rhino, the dust on the Butterfly). It runs HERE and only here, for the hit just
            // admitted, so a scored contact and a petal theft are one event: same gate, same
            // cooldown, same machine. A Strike with no authored take falls back to its fleet
            // price (CombatHitDrain.ApplyPriced), so no container can score a contact that takes
            // nothing.
            if (!TakeThroughSiblings(victimStatus, shooterStatus, impactor, impactee))
                CombatHitDrain.ApplyPriced(victimStatus, shooterStatus, hitClass, supersededRank,
                                           shooterStatus.Course * shooterStatus.Speed,
                                           ElementalDebuffSources.VesselContact);

            onCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = shooterStatus.PlayerName,
                VictimName = victimStatus.PlayerName,
                HitClass = hitClass,
                SupersededRank = supersededRank,
            });
        }

        bool TakeThroughSiblings(IVesselStatus victim, IVesselStatus attacker,
                                 VesselImpactor impactor, SkimmerImpactor impactee)
        {
            var container = impactee ? impactee.EffectContainer : null;
            var siblings = container ? container.VesselSkimmerEffects : null;
            if (siblings == null) return false;

            bool took = false;
            for (int i = 0; i < siblings.Length; i++)
            {
                if (siblings[i] is not IContactPetalTake take) continue;
                take.TakeFrom(victim, attacker, impactor, impactee);
                took = true;
            }
            return took;
        }
    }
}

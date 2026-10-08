using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE ELEMENTAL ECONOMY'S ONLY SINK. Every other elemental debuff in the game MOVES petals -
    /// a joust steals them, a blast knocks them loose as crystals - so without exactly one force
    /// that DESTROYS them the match is a closed pot that fills to the ceiling and stops being
    /// worth fighting over. A hostile danger prism is that force, and nothing else is.
    ///
    /// <para><b>Both domains are still punished; only the SEVERITY differs, and that distinction
    /// is load-bearing.</b> CLAUDE.md's danger-prism rule is LOCKED - <i>danger prisms are not
    /// safe to their own domain, and a danger-prism effect must not GATE on domain</i> - because
    /// a danger trail that spared its owner would make laying one free. Nothing here gates:
    /// ramming your own danger trail costs you exactly what it always did, a temporary decaying
    /// debuff. What the prism's domain now decides is whether the loss is PERMANENT:
    /// <list type="bullet">
    /// <item><b>Opposing-domain danger</b> - somebody else's trap, or a creature's danger rods -
    /// <b>BURNS</b> the petals. They are gone from the match.</item>
    /// <item><b>Own-domain danger</b> - your own trail - keeps the temporary debuff. You are
    /// punished for flying into your own hazard without the arena eating your crystals for it.</item>
    /// </list>
    /// The reason it falls this way round is that burning is an act of the WORLD against a pilot,
    /// and a pilot's own trail is not the world. A self-inflicted permanent burn would also make
    /// the Squirrel's Live Wire upgrade - which pays 10x for skimming danger - a trap that
    /// destroys the very levels it is paying out.</para>
    ///
    /// <para><b>What it does to danger-prism fauna, stated carefully.</b> Fauna spawn in the
    /// cell's CONTROLLING colour, so a creature's danger rods burn the pilots who do NOT control
    /// that cell and merely sting the ones who do. That asymmetry is emergent rather than
    /// designed and it is worth watching in playtest: holding a cell now also shelters you from
    /// its wildlife's permanence, which is either a satisfying reward for territory or a
    /// rich-get-richer problem, and only play will say which. What is unambiguous is the
    /// risk/reward it puts on the Squirrel's Live Wire (Charge 5), which pays 10x energy for
    /// SKIMMING danger mass: the skim still pays, and a ram into hostile danger now costs
    /// something no crystal hands back.</para>
    ///
    /// <para>Both branches are classed <see cref="ElementalDebuffSources.DangerPrism"/>, so a ward
    /// held against the ARENA alone (the Dolphin's Drift Ward) stops both while leaving that vessel
    /// fully debuffable by another pilot's weapon. Note that ward therefore now wards the SINK,
    /// which is a real and deliberate strengthening of it.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselElementalDebuffByDangerPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselElementalDebuffByDangerPrismEffectSO")]
    public class VesselElementalDebuffByDangerPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Debuff Settings")]
        [Tooltip("Signed level change applied to every element (negative = debuff). On an " +
                 "OPPOSING-domain danger prism this much is BURNED permanently; on your own " +
                 "trail it is a temporary debuff of the same size.")]
        [SerializeField] private float debuffMagnitude = -0.5f;

        [Tooltip("The TUNED size (PetalBurnRule.Tuned), used instead of debuffMagnitude in any cell " +
                 "whose CellConfigDataSO.PetalBurnRule is Tuned. -0.1 = one petal per element per " +
                 "contact - the Living Ecology lab's recommendation (Docs/ELEMENTAL_ECONOMY.md §4.1). " +
                 "Same coupling as the shipped size: it is both the hostile burn and the own-domain " +
                 "temporary debuff.")]
        [SerializeField] private float tunedDebuffMagnitude = -0.1f;

        [Tooltip("Seconds over which the OWN-DOMAIN temporary debuff decays back to baseline. " +
                 "The opposing-domain burn is permanent and has nothing to decay.")]
        [SerializeField] private float debuffDuration = 4f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between danger prism debuffs on the same vessel")]
        [SerializeField] private float cooldown = 1f;

        [Header("Spawn Grace")]
        [Tooltip("Seconds after a vessel spawns (ResetForPlay, or the go of StartVessel) during which a danger " +
                 "contact does nothing - neither the hostile burn nor the own-domain sting. A pilot dropped onto a " +
                 "trap's teeth has had no chance to read it. 1 s is the Living Ecology lab's fair-burns fix " +
                 "(Tools/Ecology/DISCOVERIES.md, Fair burns 2026-10-05: the one unread burn left was a spawn on the " +
                 "teeth at t = 0.1 s). Works the same under either PetalBurnRule. 0 = off.")]
        [SerializeField] private float spawnGraceSeconds = 1f;

        [Header("Stakes Switch")]
        [Tooltip("The live cell. Its Config.PetalBurnRule picks debuffMagnitude (Shipped) or " +
                 "tunedDebuffMagnitude (Tuned). Unassigned, or no LIVE cell, plays Shipped.")]
        [SerializeField] private CellRuntimeDataSO cellData;

        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Per-vessel anti-spam: last time a danger prism debuff was applied to a vessel.
        private static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();

        // No prune path — destroyed ResourceSystem keys accumulate for the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastEffectTime.Clear();

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            if (!vesselImpactor || !prismImpactee || vesselImpactor.Vessel == null)
                return;

            if (!prismImpactee.Prism.prismProperties.IsDangerous)
                return;

            ApplyContact(vesselImpactor.Vessel.VesselStatus, prismImpactee.Prism.Domain,
                         prismImpactee.Prism.prismProperties.DangerWeight);
        }

        /// <summary>
        /// One danger CONTACT on <paramref name="victim"/> from a prism of <paramref name="prismDomain"/>, at the
        /// burn rules' <paramref name="weight"/> (a bite 1, a drain 0.25; 0 or less does nothing). The prism collision
        /// path (<see cref="Execute"/>) and contacts that have no collision - a leech sipping from the hull it rides
        /// (SubstrateFauna) - share this, the per-vessel cooldown included. Returns whether it landed.
        /// </summary>
        public bool ApplyContact(IVesselStatus victim, Domains prismDomain, float weight)
        {
            if (!(weight > 0f)) return false;
            var rs = victim?.ResourceSystem;
            if (rs == null) return false;

            // Spawn grace: nothing lands in the first spawnGraceSeconds of a life, and the cooldown is not started
            var now = Time.time;
            if (InSpawnGrace(now, rs.SpawnedAt, spawnGraceSeconds)) return false;

            // Cooldown check - anti-spam per debuffed vessel
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return false;
            _lastEffectTime[rs] = now;

            // NOT A GATE. Both branches run for every vessel that touches the prism - the locked
            // friendly-fire rule is intact - and the prism's domain only chooses HOW the loss
            // lands. Domains.Blue (unrostered environment mass) is hostile to everyone, exactly as
            // PrismStats treats it, so a neutral danger rod burns.
            bool hostile = prismDomain != victim.Domain;

            // THE STAKES SWITCH. The cell picks the size; both branches below use it, so the
            // own-domain sting stays the same size as the hostile burn under either rule. The
            // contact's weight scales it (burn rules: a drain is a quarter of a bite).
            // Read through the LIVE cell, never CellRuntimeDataSO.Config alone: that field survives a scene load and
            // is only cleared when a Cell enables, so after the Swarm cell was picked a scene with no Cell would
            // otherwise keep playing its Tuned burn. No live cell = Shipped.
            var liveCell = cellData ? cellData.Cell : null;
            var liveConfig = liveCell ? liveCell.Config : null;
            var rule = liveConfig ? liveConfig.PetalBurnRule : PetalBurnRule.Shipped;
            float magnitude = PetalBurnRules.Magnitude(rule, debuffMagnitude, tunedDebuffMagnitude) * weight;

            // Classed DangerPrism either way, which is what a narrow ward can be held against: the
            // Dolphin's Time-5 Drift Ward wards THIS and nothing else (ElementalDebuffSources).
            for (int i = 0; i < AllElements.Length; i++)
            {
                if (hostile)
                    // THE SINK. The only call in the game that destroys a petal instead of
                    // moving it. Authored negative because it reads as a debuff; a transfer
                    // takes a positive amount.
                    ElementalTransfer.Burn(victim, AllElements[i], -magnitude,
                                           ElementalDebuffSources.DangerPrism);
                else
                    rs.ApplyElementalEffect(AllElements[i], magnitude, debuffDuration,
                                            ElementalDebuffSources.DangerPrism);
            }
            return true;
        }

        /// <summary>Is a contact at <paramref name="now"/> inside the spawn grace of a vessel that spawned at
        /// <paramref name="spawnedAt"/>? A grace of 0 or less is off; a vessel never stamped is never in grace.</summary>
        public static bool InSpawnGrace(float now, float spawnedAt, float graceSeconds) =>
            graceSeconds > 0f && now >= spawnedAt && now - spawnedAt < graceSeconds;
    }
}

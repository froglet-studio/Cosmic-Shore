using System.Collections.Generic;
using CosmicShore.Data;
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

        [Tooltip("Seconds over which the OWN-DOMAIN temporary debuff decays back to baseline. " +
                 "The opposing-domain burn is permanent and has nothing to decay.")]
        [SerializeField] private float debuffDuration = 4f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between danger prism debuffs on the same vessel")]
        [SerializeField] private float cooldown = 1f;

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

            var victim = vesselImpactor.Vessel.VesselStatus;
            var rs = victim?.ResourceSystem;
            if (rs == null) return;

            // Cooldown check - anti-spam per debuffed vessel
            var now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return;
            _lastEffectTime[rs] = now;

            // NOT A GATE. Both branches run for every vessel that touches the prism - the locked
            // friendly-fire rule is intact - and the prism's domain only chooses HOW the loss
            // lands. Domains.Blue (unrostered environment mass) is hostile to everyone, exactly as
            // PrismStats treats it, so a neutral danger rod burns.
            bool hostile = prismImpactee.Prism.Domain != victim.Domain;

            // Classed DangerPrism either way, which is what a narrow ward can be held against: the
            // Dolphin's Time-5 Drift Ward wards THIS and nothing else (ElementalDebuffSources).
            for (int i = 0; i < AllElements.Length; i++)
            {
                if (hostile)
                    // THE SINK. The only call in the game that destroys a petal instead of
                    // moving it. Authored negative because it reads as a debuff; a transfer
                    // takes a positive amount.
                    ElementalTransfer.Burn(victim, AllElements[i], -debuffMagnitude,
                                           ElementalDebuffSources.DangerPrism);
                else
                    rs.ApplyElementalEffect(AllElements[i], debuffMagnitude, debuffDuration,
                                            ElementalDebuffSources.DangerPrism);
            }
        }
    }
}

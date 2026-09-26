using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE FLEET'S CONTACT ELEMENTAL TRANSFER - the skimmer verb that takes petals off a pilot you
    /// flew into. Two hulls wear it, through two assets of this one type:
    /// <list type="bullet">
    /// <item>the <b>Squirrel's joust</b>, which must OVERTAKE to land it (<see cref="requireOvertake"/>
    /// on) - the manoeuvre is the ability;</item>
    /// <item>the <b>Rhino's sword</b>, which lands it on any contact (<see cref="requireOvertake"/>
    /// off) - a blade connects on its own terms, and the sword's own
    /// <c>VesselCombatHitBySkimmerEffectSO</c> already prices being faster.</item>
    /// </list>
    ///
    /// <para><b>The opponent branch is now a permanent STEAL, not a decaying debuff.</b> A contact
    /// verb is the one kind of hit where the attacker is physically there to take what they knocked
    /// loose, so the petals move straight onto the overtaker's own levels
    /// (<see cref="ElementalTransfer.Steal"/>): the victim is permanently poorer and the jouster
    /// permanently richer until somebody takes it off THEM. Nothing decays, so a match is a running
    /// ledger rather than a series of four-second inconveniences.</para>
    ///
    /// <para><b>The ally branch stays a TEMPORARY buff, deliberately.</b> A buff is not a transfer -
    /// there is no victim to take it from - so making it permanent would mint petals out of nothing
    /// and break the rule that lifeforms are the only source. As a transient it never touches the
    /// base level, so it is felt and then gone, and the economy is untouched. That asymmetry is the
    /// design: jousting an enemy MOVES material, jousting a friend only encourages them.</para>
    ///
    /// <para>Nothing happens to the faster (overtaking) vessel beyond being paid.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselOvertakeBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselOvertakeBySkimmerEffectSO")]
    public class VesselOvertakeBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Effect")]
        [Tooltip("Signed level change TAKEN from an overtaken opponent and handed to the " +
                 "overtaker (negative = taken). Permanent: the petals change owner. DERIVED " +
                 "from the price Broadside puts on a Strike, not authored — edit it with " +
                 "Tools/Build/author_combat_debuff_magnitudes.py, whose --check FAILS on a " +
                 "hand-edit.")]
        [SerializeField] private float debuffMagnitude = -0.5f;

        [Tooltip("Signed level change applied to an overtaken ally (positive = buff). MIRRORS " +
                 "the debuff exactly: this is one mechanic with two branches, and a buff that " +
                 "outweighed the debuff would make a friendly overtake worth more than an " +
                 "enemy one costs. Derived by the same script.")]
        [SerializeField] private float buffMagnitude = 0.5f;

        [Tooltip("Seconds over which the ally BUFF decays back to baseline. It no longer " +
                 "applies to the opponent branch, which is a permanent transfer with nothing " +
                 "to decay; the authoring tool still reads it to price sustained pressure.")]
        [SerializeField] private float effectDuration = 3f;

        [Header("When it lands")]
        [Tooltip("ON (the Squirrel's joust): only the SLOWER vessel is affected, so the effect " +
                 "is earned by overtaking. OFF (the Rhino's sword): any contact lands it, " +
                 "because a swung blade connects on its own terms rather than by out-running " +
                 "anyone. Defaults ON so every asset authored before this field existed " +
                 "deserializes to exactly its old behaviour.")]
        [SerializeField] private bool requireOvertake = true;

        [Header("Haptics")]
        [SerializeField] private float hapticAmplitude = 0.8f;
        [SerializeField] private float hapticFrequency = 0.7f;
        [SerializeField] private float hapticDuration = 0.25f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between overtake effects on the same vessel")]
        [SerializeField] private float cooldown = 1f;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        // Per-vessel anti-spam: last time an overtake effect was applied to a vessel.
        private static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();

        // No prune path — destroyed ResourceSystem keys accumulate for the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastEffectTime.Clear();

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (impactor == null || impactor.Vessel == null) return;
            if (impactee == null || impactee.Skimmer?.VesselStatus?.Vessel == null) return;

            var impactorVessel = impactor.Vessel;
            var impacteeVessel = impactee.Skimmer.VesselStatus.Vessel;

            // Don't trigger on self-collision
            if (impactorVessel == impacteeVessel) return;

            // Only the slower vessel - the one being overtaken - is affected, WHERE THE ASSET
            // ASKS FOR IT. The Squirrel's joust does (the manoeuvre is the ability); the Rhino's
            // sword does not (a blade connects on its own terms, and its own combat-hit reporter
            // already prices being faster).
            if (requireOvertake &&
                impactorVessel.VesselStatus.Speed >= impacteeVessel.VesselStatus.Speed) return;

            var overtakenStatus = impactorVessel.VesselStatus;
            var rs = overtakenStatus.ResourceSystem;
            if (rs == null) return;

            // Cooldown check - anti-spam per overtaken vessel
            var now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return;
            _lastEffectTime[rs] = now;

            // Haptic feedback
            HapticController.PlayConstant(hapticAmplitude, hapticFrequency, hapticDuration);

            bool isAlly = overtakenStatus.Domain == impacteeVessel.VesselStatus.Domain;

            if (isAlly)
            {
                // A BUFF IS NOT A TRANSFER - there is no victim to take it from - so it stays a
                // temporary, decaying effect. Making it permanent would mint petals out of
                // nothing and break the rule that lifeforms are the economy's only source.
                for (int i = 0; i < AllElements.Length; i++)
                    rs.ApplyElementalEffect(AllElements[i], buffMagnitude, effectDuration,
                                            ElementalDebuffSources.VesselContact);
            }
            else
            {
                // A CONTACT VERB STEALS: the petals leave the victim's levels permanently and
                // land on the overtaker's. Classed VesselContact, which is what decides which
                // wards stop it - and a warded pilot yields nothing, so the thief is paid
                // exactly what the victim actually lost and never more
                // (ResourceSystem.AccrueElementalLoss is the authority, not this call site).
                // The magnitude is authored negative because it reads as a debuff; a transfer
                // takes a positive amount, since how much moves has no sign.
                var thief = impacteeVessel.VesselStatus;
                for (int i = 0; i < AllElements.Length; i++)
                    ElementalTransfer.Steal(overtakenStatus, thief, AllElements[i],
                                            -debuffMagnitude, ElementalDebuffSources.VesselContact);
            }

            // Friendly buff audio: all four elements are buffed at once, so play a
            // single representative element's buff SFX (chosen at random for variety)
            // rather than stacking a four-sound chord. Only the local ally being
            // buffed hears it.
            if (isAlly && overtakenStatus.IsLocalUser)
            {
                var element = AllElements[Random.Range(0, AllElements.Length)];
                AudioSystem.Instance?.PlayGameplaySFX(JoustBuffCategoryForElement(element));
            }
        }

        static GameplaySFXCategory JoustBuffCategoryForElement(Element element) => element switch
        {
            Element.Charge => GameplaySFXCategory.JoustBuffCharge,
            Element.Mass   => GameplaySFXCategory.JoustBuffMass,
            Element.Space  => GameplaySFXCategory.JoustBuffSpace,
            Element.Time   => GameplaySFXCategory.JoustBuffTime,
            _              => GameplaySFXCategory.JoustBuffCharge,
        };
    }
}

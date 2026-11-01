using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tiger Moth's <b>Scale Dust</b>, pilot half: CHARGE. The dust SHREDS: while an opposing pilot
    /// stays inside it, one petal of one random element they hold is STOLEN off them every
    /// <see cref="shredInterval"/> seconds, 1 s at Charge 0 down to 0.1 s at Charge 10 (Garrett,
    /// 2026-10-31: "have the charge ability shred a random element petal every second and decrease the
    /// cooldown to .1 seconds at charge 10"). Charge is the RATE of the shred; the size of one shred is
    /// always one petal.
    ///
    /// <para><b>The take runs only through the reporter</b>, as every contact take does
    /// (<see cref="IContactPetalTake"/>): the container's <see cref="VesselCombatHitBySkimmerEffectSO"/>
    /// admits the contact and calls <see cref="TakeFrom"/>. This take is PACED
    /// (<see cref="IPacedContactPetalTake"/>), so the reporter uses this asset's Charge-scaled window,
    /// silences the victim for one petal, and scores one Strike per 3.2 petals (a Strike's fleet price),
    /// so score and silence stay proportional to the petals taken. The reporter runs every frame the
    /// rival stays in the dust (its <c>repeatWhileInside</c>, driven by <c>ButterflyDustField</c>), and
    /// the window paces it. <c>Execute</c> only plays the bite's sound, on entry.</para>
    ///
    /// <para><b>The element is picked among those the victim HOLDS</b> (their replicated level above
    /// 0), so a shred never lands on an empty element while another still has petals. A victim who
    /// holds nothing is not admitted at all: no score, no silence. The pick is an integer hash of a
    /// running serial, never <c>UnityEngine.Random</c> (vessel rule 18); only the deciding machine
    /// picks, and the victim's owner settles the one element it names.</para>
    ///
    /// <para><b>Classed <see cref="ElementalDebuffSources.VesselContact"/></b>, not DangerPrism: a ward
    /// earned against the ARENA must not cancel a weapon another pilot aimed.</para>
    ///
    /// <para><b>Own-domain contact is declined here</b>: a skimmer has no domain gate for vessels
    /// (<c>Skimmer.affectSelf</c> only gates skim bookkeeping), so without this test the dust would
    /// bite the teammate flying beside it. Allies are the cleanse's
    /// (<c>VesselCleanseAllyBySkimmerEffectSO</c>).</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselElementalDebuffBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselElementalDebuffBySkimmerEffectSO")]
    public class VesselElementalDebuffBySkimmerEffectSO : VesselSkimmerEffectsSO, IPacedContactPetalTake
    {
        [Header("Shred")]
        [Tooltip("Which elements a shred can take from. Each shred picks ONE of these at random, among " +
                 "the ones the victim holds petals on.")]
        [SerializeField] Element[] elements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        [Tooltip("Petals one shred steals off the one element it picks. Also what it silences for and " +
                 "what its score is priced on (one Strike per 3.2 petals).")]
        [SerializeField, Min(1)] int petalsPerShred = 1;

        [Tooltip("Seconds between shreds on the same victim, scaled by the ATTACKING pilot's CHARGE: " +
                 "1 s at rest, 0.1 s at Charge 10, floored at 0.1 s above it. Read through the pilot's " +
                 "REPLICATED integer level, on the machine that decides the hit.")]
        [SerializeField] ElementalFloat shredInterval = ElementalFloat.Multiplier(1f, 0.1f, Element.Charge, 0.1f);

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between dust BITE SOUNDS on the same vessel. The shred itself is " +
                 "paced by shredInterval, through the reporter's latch.")]
        [SerializeField, Min(0f)] float cooldown = 1f;

        // Per-vessel anti-spam, keyed on the victim's ResourceSystem — the same shape the
        // explosion and danger-prism debuffs use, so the three share a mental model.
        static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();

        // The running serial the element pick hashes. Only the deciding machine draws from it.
        static uint _shredSerial;

        // No prune path — destroyed ResourceSystem keys accumulate for the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _lastEffectTime.Clear();
            _shredSerial = 0;
        }

        /// <summary>
        /// NOTE the argument order, shared with every other skimmer effect and inverted from what
        /// the names suggest: <paramref name="impactor"/> is the vessel that was SWEPT (the victim) and
        /// <paramref name="impactee"/> is the skimmer doing the sweeping.
        /// </summary>
        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (!impactor || impactor.Vessel == null) return;

            var victim = impactor.Vessel.VesselStatus;
            var pilot = impactee != null && impactee.Skimmer != null
                ? impactee.Skimmer.VesselStatus : null;
            if (victim == null || pilot == null) return;

            // A teammate sweep is not an attack — see the class note.
            if (victim.Domain == pilot.Domain) return;

            var rs = victim.ResourceSystem;
            if (rs == null) return;

            float now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return;
            _lastEffectTime[rs] = now;

            // The PETALS are not taken here: see TakeFrom, which the reporter calls for an
            // admitted shred. This keeps only the sound every peer should hear.

            // The bite's voice. Its slot lives on the Tiger Moth's dust capsule
            // (ButterflyDustField), beside the SkimmerImpactor doing the sweeping; any other
            // adopter of this effect has no such component and stays silent here.
            if (impactee.TryGetComponent(out ButterflyDustField dust))
                dust.PlayScaleDustBite(impactor.transform.position);
        }

        public float SameVictimCooldownSeconds(IVesselStatus attacker) =>
            Mathf.Max(0.01f, shredInterval.EvaluateReplicated(attacker));

        public float PetalsPerTake => Mathf.Max(1, petalsPerShred);

        public bool CanTakeFrom(IVesselStatus victim) => HeldCount(victim) > 0;

        /// <summary>
        /// Steal one shred off <paramref name="victim"/>: <see cref="petalsPerShred"/> petals of ONE
        /// element, picked among those they hold. Called only by
        /// <see cref="VesselCombatHitBySkimmerEffectSO"/>, on the pilot's owner, for an admitted shred;
        /// the victim's owner settles it (ward, clamp, whole petals).
        /// </summary>
        public void TakeFrom(IVesselStatus victim, IVesselStatus attacker, VesselImpactor impactor,
                             SkimmerImpactor impactee)
        {
            if (victim == null || attacker == null) return;
            int held = HeldCount(victim);
            if (held <= 0) return;

            var element = HeldElementAt(victim, PickIndex(++_shredSerial, held));
            if (element == Element.None) return;

            ElementalTransfer.ApplyAuthoritative(ElementalTransferForm.Steal, victim, attacker,
                                                 ElementalTransfer.MaskOf(element),
                                                 PetalsPerTake * CombatHitDrain.NormalizedPerLevel,
                                                 Vector3.zero, ElementalDebuffSources.VesselContact);
        }

        /// <summary>The index in [0, <paramref name="count"/>) a shred's serial picks: an integer
        /// hash, so the pick is spread evenly and draws on no global random state.</summary>
        public static int PickIndex(uint serial, int count)
        {
            if (count <= 1) return 0;
            uint h = serial * 0x9E3779B9u;
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (int)(h % (uint)count);
        }

        int HeldCount(IVesselStatus victim)
        {
            if (elements == null) return 0;
            int n = 0;
            for (int i = 0; i < elements.Length; i++)
                if (Holds(victim, elements[i])) n++;
            return n;
        }

        Element HeldElementAt(IVesselStatus victim, int index)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (!Holds(victim, elements[i])) continue;
                if (index-- == 0) return elements[i];
            }
            return Element.None;
        }

        // The victim's REPLICATED level, the number every peer agrees on (an element level never
        // replicates otherwise). Off-network it is the victim's own level.
        static bool Holds(IVesselStatus victim, Element element)
        {
            if (element == Element.None) return false;
            var abilities = victim?.ElementalAbilityHandler;
            return abilities != null && abilities.ReplicatedLevel(element) > 0;
        }
    }
}

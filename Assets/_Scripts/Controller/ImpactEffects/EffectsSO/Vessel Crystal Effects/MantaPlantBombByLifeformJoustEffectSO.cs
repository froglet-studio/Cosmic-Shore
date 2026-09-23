using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's answer to the Crystal Joust: hull hits a living lifeform's embedded crystal
    /// (its heart) and <b>plants a bomb on it instead of killing it</b>.
    ///
    /// <para>Same surface as the Squirrel's <see cref="VesselWitherLifeformByCrystalEffectSO"/>
    /// — a vessel's <c>VesselLifeformCrystalEffects</c>, dispatched by
    /// <see cref="VesselImpactor"/> — and a deliberately opposite outcome. The Squirrel takes
    /// the heart and leaves a skeleton; the Manta leaves the creature standing and walks away
    /// having armed it. That is the whole shape of the vessel: it does not kill things it
    /// touches, it <i>tags</i> them, and cashes the whole board later.</para>
    ///
    /// <para><b>Why it is a separate effect rather than a flag on the Squirrel's.</b> The two
    /// share only their trigger. This one has no speed gate beyond the bay's own rules, no
    /// ally branch, no heart award, and it spends a resource the other does not have. Wiring
    /// is per-vessel (the rule-22 container question), so a vessel gets exactly the joust
    /// behaviour its own container names — and the Manta's container deliberately does NOT
    /// carry the withering one, or a graze would kill the target the bomb is riding.</para>
    ///
    /// <para>Flora were the case that motivated it: they are rooted (<c>CurrentSpeed</c> 0), so
    /// they are trivially joustable, and the reef is what a Bloomrush pilot is flying through.
    /// It works on fauna identically — the skimmer plants on creatures it grazes, and this
    /// plants on anything the hull reaches through its heart.</para>
    ///
    /// Stateless: every read is live, per impact.
    /// </summary>
    [CreateAssetMenu(fileName = "MantaPlantBombByLifeformJoustEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Lifeform Crystal/MantaPlantBombByLifeformJoustEffectSO")]
    public class MantaPlantBombByLifeformJoustEffectSO : VesselLifeformCrystalEffectSO
    {
        [Tooltip("Plant on OWN-domain lifeforms too. Off by default: a bomb is a weapon, and " +
                 "the bay is scarce enough that spending one on your own reef is never what " +
                 "the pilot meant.")]
        [SerializeField] bool plantOnOwnDomain = false;

        public override void Execute(VesselImpactor vesselImpactor, Crystal embeddedCrystal)
        {
            var status = vesselImpactor?.Vessel?.VesselStatus;
            if (status == null || embeddedCrystal == null || !embeddedCrystal.IsEmbedded) return;

            var lifeform = embeddedCrystal.EmbeddedIn;
            if (lifeform == null) return;
            if (!plantOnOwnDomain && lifeform.Domain == status.Domain) return;

            // The bay is the authority on whether this lands: it holds the charge, the
            // one-bomb-per-target rule and the simulation-authority gate. A failed plant is
            // silent and free — the pilot simply flew through a plant with an empty bay.
            if (!MantaStingActionExecutor.TryGetFor(status, out var sting)) return;
            sting.TryPlantOnLifeform(lifeform);
        }
    }
}

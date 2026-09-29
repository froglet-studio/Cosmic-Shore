using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's dust on MASS. Every prism the dust capsule passes through gets ONE
    /// outcome, rolled per contact (design record: <c>R_VesselActions/BUTTERFLY.md</c> §3.1):
    ///
    /// <list type="bullet">
    /// <item><b>Own domain</b> — the dust tends the garden: the prism GROWS along a rolled axis,
    /// turns DANGEROUS, or is SHIELDED. With the SPACE level-5 upgrade a rare roll raises it to
    /// SUPER-SHIELDED instead.</item>
    /// <item><b>Opposing domain</b> (anything not the pilot's colour, <c>Domains.Blue</c> included)
    /// — the dust is a blight: the prism is DESTROYED, SHRUNK, or STOLEN.</item>
    /// </list>
    ///
    /// <para><b>Only PLAIN prisms change tier.</b> The tiers are mutually exclusive in the state
    /// machine — <c>MakeDangerous</c> clears both shields, <c>ActivateSuperShield</c> clears danger —
    /// so a tier roll on an already-tiered prism would downgrade it (a super-shield turned into
    /// danger by the pilot's own dust). A tiered own-domain prism can only GROW. An opposing
    /// super-shielded prism is invulnerable, so every roll becomes the visible deflection the
    /// platform already draws for an unbreaking hit (<see cref="Prism.AbsorbSuperShieldHit"/>).</para>
    ///
    /// <para><b>The roll is a HASH, never <c>UnityEngine.Random</c>.</b> A skimmer overlap is
    /// observed on EVERY peer, and this effect edits conserved mass, so every peer has to reach the
    /// same outcome for the same contact. The inputs are what the peers already agree on: the
    /// prism's position and target size, quantized, and its domain. The size term is what makes a
    /// second pass over the same prism roll again — the first outcome changed it — while staying a
    /// function of shared state. (It also keeps a gun-rate hot path off the global RNG that
    /// deterministic systems seed; the vessel contract's rule 18.)</para>
    ///
    /// <para>The asset is shared and stateless.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SkimmerScaleDustPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleDustPrismEffectSO")]
    public class SkimmerScaleDustPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Own domain — tend")]
        [Tooltip("Relative weight of GROW among own-domain outcomes.")]
        [SerializeField, Min(0f)] float growWeight = 0.4f;
        [Tooltip("Relative weight of DANGEROUS among own-domain outcomes.")]
        [SerializeField, Min(0f)] float dangerWeight = 0.3f;
        [Tooltip("Relative weight of SHIELDED among own-domain outcomes.")]
        [SerializeField, Min(0f)] float shieldWeight = 0.3f;

        [Tooltip("A grow adds this fraction of the prism's current size along ONE rolled axis. " +
                 "Bounded by the prism's own scale window, so repeated passes cannot run away.")]
        [SerializeField, Range(0.05f, 2f)] float growFraction = 0.35f;

        [Tooltip("SPACE level-5: the chance an own-domain PLAIN prism is raised to SUPER-SHIELDED " +
                 "instead of its normal roll. Rare by design — a super-shield is invulnerable, so " +
                 "it is the strongest thing the dust can leave behind.")]
        [SerializeField, Range(0f, 1f)] float superShieldChance = 0.06f;

        [Tooltip("The element whose level-5 upgrade unlocks the super-shield roll. Gated on the " +
                 "replicated unlock bit — it decides the tier of conserved mass every peer lays.")]
        [SerializeField] Element superShieldUpgradeElement = Element.Space;

        [Header("Opposing domain — blight")]
        [Tooltip("Relative weight of DESTROY among opposing-domain outcomes.")]
        [SerializeField, Min(0f)] float destroyWeight = 1f;
        [Tooltip("Relative weight of SHRINK among opposing-domain outcomes.")]
        [SerializeField, Min(0f)] float shrinkWeight = 1f;
        [Tooltip("Relative weight of STEAL among opposing-domain outcomes.")]
        [SerializeField, Min(0f)] float stealWeight = 1f;

        [Tooltip("A shrink removes this fraction of the prism's size on every axis. Bounded by " +
                 "its scale window's floor, so a prism is never shrunk out of existence — " +
                 "removing it is DESTROY's job, which animates.")]
        [SerializeField, Range(0.05f, 0.9f)] float shrinkFraction = 0.35f;

        [Header("Destroy")]
        [Tooltip("Debris speed as a multiple of the capsule's contact speed (proportional debris).")]
        [SerializeField] float restitution = 1f / 3f;
        [Tooltip("Ceiling on debris speed, real units.")]
        [SerializeField] float debrisSpeedLimit = 120f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor != null && impactor.Skimmer ? impactor.Skimmer.VesselStatus : null;
            var prism = prismImpactee != null ? prismImpactee.Prism : null;
            if (status == null || !prism || prism.destroyed || prism.prismProperties == null) return;

            uint h = Roll(prism);
            if (prism.Domain == status.Domain) Tend(prism, status, h);
            else Blight(impactor, prismImpactee, prism, status, h);
        }

        void Tend(Prism prism, IVesselStatus status, uint h)
        {
            var kind = PrismKinds.Of(prism);
            if (kind == PrismKind.SuperShielded) return;       // nothing the dust can add

            if (kind == PrismKind.Plain && superShieldChance > 0f && Upgraded(status)
                && Unit(h, 1) < superShieldChance)
            {
                prism.ActivateSuperShield();
                return;
            }

            // A tiered prism may only grow — see the class note.
            if (kind != PrismKind.Plain)
            {
                Grow(prism, h);
                return;
            }

            switch (Pick(Unit(h, 2), growWeight, dangerWeight, shieldWeight))
            {
                case 0: Grow(prism, h); break;
                case 1: prism.MakeDangerous(); break;
                case 2: prism.ActivateShield(); break;
            }
        }

        void Blight(SkimmerImpactor impactor, PrismImpactor impactee, Prism prism,
                    IVesselStatus status, uint h)
        {
            int outcome = Pick(Unit(h, 3), destroyWeight, shrinkWeight, stealWeight);

            // An invulnerable prism only ever deflects, whatever was rolled.
            if (prism.prismProperties.IsSuperShielded) outcome = 0;

            switch (outcome)
            {
                case 0:
                    var velocity = PrismEffectHelper.ContactVelocity(
                        impactor, status, prism.transform.position, 0f, 0f);
                    PrismEffectHelper.DamageProportional(status, impactee, velocity,
                                                         restitution, debrisSpeedLimit);
                    break;
                case 1:
                    prism.GrowAlong(-prism.TargetScale * shrinkFraction);
                    break;
                case 2:
                    PrismEffectHelper.Steal(impactee, status);
                    break;
            }
        }

        void Grow(Prism prism, uint h)
        {
            var size = prism.TargetScale;
            int axis = (int)(Hash(h, 4) % 3u);
            var delta = Vector3.zero;
            delta[axis] = size[axis] * growFraction;
            prism.GrowAlong(delta);
        }

        bool Upgraded(IVesselStatus status)
        {
            if (superShieldUpgradeElement == Element.None) return true;
            var abilities = status.ElementalAbilityHandler;
            return abilities != null && abilities.IsUpgradeActive(superShieldUpgradeElement);
        }

        // ── the roll ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The contact's seed, from state every peer agrees on: position and target size,
        /// quantized, and domain. Quantization absorbs the float noise between two peers'
        /// replicated trails; the size term re-rolls a prism whose last outcome changed it.
        /// </summary>
        static uint Roll(Prism prism)
        {
            var p = prism.transform.position;
            var s = prism.TargetScale;
            uint h = 2166136261u;
            h = Mix(h, Mathf.RoundToInt(p.x));
            h = Mix(h, Mathf.RoundToInt(p.y));
            h = Mix(h, Mathf.RoundToInt(p.z));
            h = Mix(h, Mathf.RoundToInt(s.x * 10f));
            h = Mix(h, Mathf.RoundToInt(s.y * 10f));
            h = Mix(h, Mathf.RoundToInt(s.z * 10f));
            h = Mix(h, (int)prism.Domain);
            return h;
        }

        static uint Mix(uint h, int v)
        {
            unchecked
            {
                h ^= (uint)v;
                h *= 16777619u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>An independent stream per decision, so two decisions on one contact never
        /// share a draw.</summary>
        static uint Hash(uint h, uint salt)
        {
            unchecked
            {
                uint x = h ^ (salt * 0x9E3779B9u);
                x ^= x >> 16; x *= 0x7FEB352Du;
                x ^= x >> 15; x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        static float Unit(uint h, uint salt) => (Hash(h, salt) & 0xFFFFFF) / 16777216f;

        /// <summary>Index of the weighted bucket <paramref name="u"/> (0..1) falls in; 0 when
        /// every weight is zero.</summary>
        static int Pick(float u, float a, float b, float c)
        {
            float total = a + b + c;
            if (total <= 0f) return 0;
            float x = u * total;
            if (x < a) return 0;
            if (x < a + b) return 1;
            return 2;
        }
    }
}

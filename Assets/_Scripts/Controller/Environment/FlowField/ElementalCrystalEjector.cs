using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// KNOCKS PETALS OUT OF A HULL AS COLLECTABLE CRYSTALS - the physical half of an ejecting
    /// elemental transfer (<see cref="ElementalTransfer"/>).
    ///
    /// <para><b>One crystal is one petal, exactly.</b> The collect reward is
    /// <c>SkimmerAdjustElementLevelByCrystalEffectSO.ComputeLevelGain</c> =
    /// <c>lossyScale.x * levelPerUnitScale</c>, and the shipped <c>levelPerUnitScale</c> is
    /// <c>0.1</c> - which is <see cref="ResourceSystem.PetalNormalized"/>. So a crystal minted
    /// at WORLD SCALE 1 returns precisely the one integer level that was taken, and the
    /// economy closes: nothing is created, nothing is destroyed, the petal simply changes
    /// hands. That is the whole reason the ejector mints at a fixed scale rather than encoding
    /// the amount in the SIZE - a size-encoded petal would clip
    /// <c>maxLevelGainPerCrystal</c> (0.5) the moment a hit took more than five, and a clipped
    /// crystal is a quantity the player can see and a reward they cannot.</para>
    ///
    /// <para><b>They belong to nobody.</b> An ejected petal is minted
    /// <see cref="Domains.Blue"/>, the platform's free-for-all, so it wears the lime
    /// collect-me signal (<c>Docs/PALETTE.md</c> §2.2) and anyone may take it - including the
    /// pilot it came out of, if they turn round fast enough. That is the whole difference
    /// between ejecting and stealing: a steal hands the petal over, an ejection puts it back on
    /// the table.</para>
    ///
    /// <para><b>Per-peer, like every other loose crystal.</b> These are local objects minted
    /// wherever the hit resolved, exactly as the food web's crystals are per-peer unless a
    /// species sets <c>FloraConfigurationSO.NetworkSynced</c>. Two peers can therefore disagree
    /// about who collected one. That is the platform's existing stance rather than a new
    /// compromise, and <c>FloraNetworkSync</c> is the precedent to follow if a mode ever needs
    /// them authoritative.</para>
    /// </summary>
    public static class ElementalCrystalEjector
    {
        /// <summary>The world scale at which one crystal is worth exactly one petal. See the type doc.</summary>
        public const float PetalCrystalWorldScale = 1f;

        /// <summary>
        /// How much of the hull's half-width a petal starts outside the centre, so crystals leave
        /// the SKIN rather than materialising inside the cockpit. Multiplied by the measured hull
        /// radius, so it is correct on a 7-unit Urchin and a 130-unit Shark alike.
        /// </summary>
        const float HullSurfaceFraction = 0.6f;

        /// <summary>
        /// Fraction of the launch speed that is scattered sideways, so several petals from one hit
        /// fan out instead of stacking into a single travelling clump the eye reads as one object.
        /// </summary>
        const float ScatterFraction = 0.35f;

        /// <summary>The floor under a launch: even a standing-still contact has to visibly expel.</summary>
        const float MinLaunchSpeed = 12f;

        /// <summary>
        /// Mints <paramref name="petals"/> crystals of <paramref name="element"/> at the victim's
        /// hull and throws them along <paramref name="impactVelocity"/>.
        /// </summary>
        /// <param name="victim">The hull the petals came out of - the spawn origin and its size.</param>
        /// <param name="element">Which element was knocked loose. One crystal carries one petal of it.</param>
        /// <param name="petals">Whole petals to expel; already deducted from the victim's levels.</param>
        /// <param name="impactVelocity">
        /// The velocity of whatever landed the hit, straight from the same accessor the prism
        /// debris model uses (<c>PrismEffectHelper.ContactVelocity</c> for a contact,
        /// <c>ExplosionImpactor.BlastImpactVector</c> for a blast, the round's own
        /// <c>Projectile.Velocity</c> for a shot). A hard hit scatters far; a graze drops them
        /// underfoot. Zero is legal and falls back to a gentle radial puff.
        /// </param>
        public static void Eject(IVesselStatus victim, Element element, int petals, Vector3 impactVelocity)
        {
            if (victim == null || petals <= 0) return;
            if (element == Element.None) return;

            var set = ElementalCrystalSetSO.Load();
            var prefab = set ? set.GetPrefab(element) : null;
            if (!prefab)
            {
                // Loud, once per hit rather than per petal: an ejection that cannot mint is a
                // petal DESTROYED, which breaks the conservation this whole path exists for.
                CSDebug.LogError($"[ElementalCrystalEjector] No '{element}' prefab in " +
                    $"Resources/{ElementalCrystalSetSO.ResourcePath} - {petals} petal(s) knocked " +
                    $"off {victim.PlayerName} could not be minted and are LOST.");
                return;
            }

            // NOT victim.Transform: that property is `Vessel.Transform` with no guard, and
            // VesselStatus.Vessel logs an error and returns null - so it THROWS rather than
            // answering null, and would take the whole contact down with it.
            var hull = victim.Vessel?.Transform;
            if (!hull) return;

            Vector3 origin = hull.position;
            float radius = Mathf.Max(1f, PrismOcclusionCorridor.MeasureCircumscribedRadius(hull));

            float speed = Mathf.Max(MinLaunchSpeed, impactVelocity.magnitude);
            // A zero-velocity hit still has to expel something, and "away from the hull" is the
            // only direction available when nothing was moving.
            Vector3 forward = impactVelocity.sqrMagnitude > 1e-4f
                ? impactVelocity.normalized
                : Random.onUnitSphere;

            for (int i = 0; i < petals; i++)
            {
                // Each petal gets its own scatter, so the fan is a property of the burst rather
                // than of the weapon - deterministic seeding would make every ejection identical.
                Vector3 scatter = Random.onUnitSphere * ScatterFraction;
                Vector3 direction = (forward + scatter).normalized;

                Mint(prefab, set, origin + direction * (radius * HullSurfaceFraction),
                     direction * speed);
            }
        }

        static void Mint(Crystal prefab, ElementalCrystalSetSO set, Vector3 position, Vector3 velocity)
        {
            // Unparented on purpose: local scale IS world scale, so the petal's worth cannot be
            // altered by whatever it happens to hang from - and it does not follow the hull that
            // shed it, which would read as the pilot keeping what they just lost.
            var crystal = Object.Instantiate(prefab, position, Random.rotation);
            crystal.transform.localScale = Vector3.one * PetalCrystalWorldScale;
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);

            // Free-for-all: the lime collect-me signal, and no domain gate on the pickup.
            crystal.ApplyDomainPreview(Domains.Blue);

            // The standalone elemental prefabs carry no collection components (lifeform prefabs
            // author them as overrides), so wire the same pair Microscene and LifeFormCrystal do -
            // the impactor collects, the ImpactCollider lets the skimmer side react.
            if (!crystal.GetComponentInChildren<ElementalCrystalImpactor>(true))
            {
                var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
                impactor.Crystal = crystal;
                if (set != null && set.CollectionEffects is { Length: > 0 })
                    impactor.SetCollectionEffects(set.CollectionEffects);
                crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);
            }

            // Continuity of existence: a petal BLOOMS out of the hull rather than popping into
            // being beside it. FadeIn starts itself in Start().
            foreach (var renderer in crystal.GetComponentsInChildren<Renderer>(true))
                if (!renderer.TryGetComponent(out FadeIn _))
                    renderer.gameObject.AddComponent<FadeIn>();

            crystal.gameObject.AddComponent<EjectedCrystal>().Launch(velocity);
        }
    }
}

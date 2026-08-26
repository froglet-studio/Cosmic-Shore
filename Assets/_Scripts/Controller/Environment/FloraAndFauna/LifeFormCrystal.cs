using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Enforces the lifeform invariant: every lifeform (flora and fauna) carries exactly one
    /// elemental crystal (Charge / Mass / Space / Time) that drops as a collectible powerup on
    /// death. Both <see cref="LifeForm"/> and the concrete fauna route their crystal through
    /// <see cref="EnsureElementalCrystal"/> at init, so it is not possible for a spawned
    /// lifeform to die without dropping a powerup:
    ///
    ///   • authored elemental crystal child   → used as-is (the normal, per-prefab path)
    ///   • authored crystal but non-elemental  → its Element is set to a random element in place
    ///   • no crystal at all                   → a default elemental crystal is provisioned from
    ///                                           ElementalCrystalSet (Resources), random element
    ///
    /// The misconfigured branches log loudly; the editor validator (FrogletTools ▸ Validation ▸
    /// Validate Lifeform Crystals) flags the same prefabs at author time so they get fixed.
    /// Per the prompter: element is per-lifeform AUTHORED - random is only the misconfig fallback.
    /// </summary>
    public static class LifeFormCrystal
    {
        // --- Heart sizing (Docs/ECOSYSTEM.md §39.2) -------------------------------------
        //
        // A lifeform heart's size is a function of its LIFEFORM: it is authored per element in
        // that species' own variant tuning (FaunaVariantTuning.HeartWorldScale /
        // FloraVariantTuning.HeartWorldScale) and sized to suit the body it beats inside, so a
        // tadpole's heart is a tadpole's heart and a shark's is a shark's. There is no level
        // curve and no level: the four elemental variations ARE the whole variation a species
        // has, and each one states its heart size once, alongside everything else it states.
        //
        // §33 previously flattened every heart to ONE size (3.5 world, x1.05 per level) because
        // the per-PREFAB scales it replaced were an accident nobody authored - 0.7 on a tadpole
        // up to 4.0 on a gyroid, a 5.7x reward spread that nobody had chosen. The flattening
        // fixed the accident and created a different one: a uniform 3.5 renders 6.8-9.5 units
        // ACROSS, which is 3.6x a Mass tadpole's own width and 1.1x a piranha's ENTIRE LENGTH,
        // while being 11% of a shark. The band is now AUTHORED and DELIBERATE, generated from
        // each lifeform's measured body by Tools/Build/author_lifeform_heart_sizes.py.
        //
        // THE ROOT SCALE IS THE GAMEPLAY NUMBER, and that is now the point rather than a
        // hazard: the collect reward (SkimmerAdjustElementLevelByCrystalEffectSO) and the live
        // domain fauna buff (DomainFaunaBuffSystem) both read the root's lossyScale, so a
        // bigger lifeform's heart is worth more - a bigger kill pays more. What must NOT happen
        // is a heart CLIPPING the reward cap, because a clipped heart is a size the player can
        // see and a reward they cannot. The authoring tool holds the whole band under
        // ElementalCrystalSetSO.MaxSafeHeartWorldScale and fails if it ever escapes.
        //
        // A per-ELEMENT APPARENT-SIZE correction is a different thing and still never goes
        // here. The four exported crystal models are very different sizes in their own FBX
        // units, so each prefab carries a correction on its model child BELOW the root
        // (Charge 1.0 / Mass 1.38 / Space 1.34 / Time 1.42). Those children equalize apparent
        // EXTENT per unit of root scale; the root states the lifeform's size. If an ELEMENT
        // reads wrong on every species at once, fix that element's crystal PREFAB's model
        // child. If ONE SPECIES reads wrong, fix that species' authored HeartWorldScale.

        // Used only if Resources/ElementalCrystalSet is missing - the same misconfiguration the
        // provisioning paths below already report loudly. Mirrors the shipped asset's value.
        const float FallbackDefaultHeartWorldScale = 3f;

        /// <summary>
        /// The world scale a heart renders at when its species authors none - the set's single
        /// default. An authored size always wins; this is the floor under a config that has not
        /// been given one yet, and under the runtime-provisioned misconfiguration path.
        /// </summary>
        public static float DefaultHeartWorldScale
        {
            get
            {
                var set = ElementalCrystalSetSO.Load();
                return set ? set.DefaultHeartWorldScale : FallbackDefaultHeartWorldScale;
            }
        }

        /// <summary>
        /// The world scale to render <paramref name="authored"/> at: the lifeform's own authored
        /// size when it has one, else the set's default. A non-positive authored value is the
        /// 'not authored' sentinel, so a config that has never been sized still gets a heart.
        /// </summary>
        public static float ResolveHeartWorldScale(float authored) =>
            authored > 0f ? authored : DefaultHeartWorldScale;

        /// <summary>
        /// The LOCAL scale this crystal needs to render at <paramref name="worldScale"/> world
        /// units, given whatever it is currently parented to. A degenerate parent scale falls
        /// back to the world value rather than dividing by ~zero.
        /// </summary>
        public static float LocalScaleForWorld(Crystal crystal, float worldScale)
        {
            if (!crystal) return worldScale;
            var parent = crystal.transform.parent;
            if (!parent) return worldScale;
            float lossy = Mathf.Abs(parent.lossyScale.x);
            return lossy > 1e-4f ? worldScale / lossy : worldScale;
        }

        /// <summary>
        /// Sizes a heart to its lifeform's authored size immediately - it spawns AT size, so
        /// nothing pops. <paramref name="authoredWorldScale"/> is the owner's
        /// <see cref="ILifeFormEntity.HeartWorldScale"/>; non-positive falls back to the default.
        /// </summary>
        public static void ApplyHeartSize(Crystal crystal, float authoredWorldScale)
        {
            if (!crystal) return;
            SetWorldScale(crystal, ResolveHeartWorldScale(authoredWorldScale));
        }

        /// <summary>Writes a crystal's WORLD scale, dividing out whatever it hangs from.</summary>
        public static void SetWorldScale(Crystal crystal, float worldScale)
        {
            if (!crystal) return;
            crystal.transform.localScale = Vector3.one * LocalScaleForWorld(crystal, worldScale);
        }

        /// <summary>
        /// Elemental-contract path: guarantees the lifeform carries a crystal of EXACTLY this
        /// element (one base prefab, element defined as data - see FaunaConfigurationSO.Element).
        /// A prefab-authored crystal of a different element is replaced with the set's prefab for
        /// the requested one, so the per-element visual model stays correct. None falls back to
        /// the legacy authored/random path below.
        /// </summary>
        public static Crystal EnsureElementalCrystal(Component owner, CosmicShore.Data.Element element)
        {
            if (!owner) return null;
            if (element == CosmicShore.Data.Element.None) return EnsureElementalCrystal(owner);

            var crystal = owner.GetComponentInChildren<Crystal>(true);
            if (crystal && crystal.crystalProperties.Element == element)
                return crystal;

            var set = ElementalCrystalSetSO.Load();
            var prefab = set ? set.GetPrefab(element) : null;
            if (!prefab)
            {
                CSDebug.LogError($"[LifeFormCrystal] {owner.name}: no elemental crystal prefab for " +
                    $"'{element}' in Resources/{ElementalCrystalSetSO.ResourcePath} - keeping the " +
                    $"authored crystal so death still drops a powerup.");
                return EnsureElementalCrystal(owner);
            }

            Vector3 position = crystal ? crystal.transform.localPosition : Vector3.zero;

            // The four elemental prefabs share ONE scale convention (root 1.5, and a root scale
            // of r renders ~2r world units for every element - the model children compensate for
            // each export's mesh size), so the authored crystal's root scale transfers directly
            // to the replacement element. It is only a placeholder for the frames before the
            // heart is sized: the moment it becomes a heart (Crystal.SetEmbeddedIn) the
            // lifeform's own authored size overwrites it.
            float scale = crystal ? crystal.transform.localScale.x : 0f;

            SkimmerCrystalEffectSO[] authoredEffects = null;
            if (crystal)
            {
                // The authored crystal carries the collection components (impactor + collider) as
                // inspector overrides; the set's standalone prefabs do not. Capture the effects so
                // the replacement stays skim-collectable with the same payoff.
                var authoredImpactor = crystal.GetComponentInChildren<ElementalCrystalImpactor>(true);
                if (authoredImpactor) authoredEffects = authoredImpactor.CollectionEffects;

                // Deactivate BEFORE the deferred Destroy so same-frame GetComponentInChildren
                // lookups (e.g. LifeForm.Initialize's crystal fetch) find the replacement, not
                // the dying authored crystal.
                crystal.gameObject.SetActive(false);
                Object.Destroy(crystal.gameObject);
            }

            var provisioned = Object.Instantiate(prefab, owner.transform);
            provisioned.transform.localPosition = position;
            if (scale > 0f) provisioned.transform.localScale = Vector3.one * scale;
            WireCollection(provisioned, authoredEffects, set);
            return provisioned;
        }

        /// <summary>
        /// Makes a runtime-provisioned crystal skim-collectable. The standalone elemental prefabs
        /// in ElementalCrystalSet carry no collection components (lifeform prefabs author them as
        /// inspector overrides), so every provisioned crystal gets the impactor + ImpactCollider
        /// pair wired here - same pattern as the conveyor toy's pickups. Effects come from the
        /// replaced authored crystal when there was one, else from the set's defaults.
        /// </summary>
        static void WireCollection(Crystal provisioned, SkimmerCrystalEffectSO[] authoredEffects, ElementalCrystalSetSO set)
        {
            if (!provisioned || provisioned.GetComponentInChildren<ElementalCrystalImpactor>(true))
                return;

            var impactor = provisioned.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = provisioned;
            var effects = authoredEffects is { Length: > 0 } ? authoredEffects : set ? set.CollectionEffects : null;
            if (effects is { Length: > 0 })
                impactor.SetCollectionEffects(effects);
            provisioned.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);
        }

        public static Crystal EnsureElementalCrystal(Component owner)
        {
            if (!owner) return null;

            var crystal = owner.GetComponentInChildren<Crystal>(true);
            if (crystal)
            {
                if (!crystal.crystalProperties.IsElemental)
                {
                    var element = ElementalCrystalSetSO.RandomElement();
                    CSDebug.LogWarning($"[LifeFormCrystal] {owner.name}: crystal element was " +
                        $"'{crystal.crystalProperties.Element}' (not one of the four elementals); assigning " +
                        $"'{element}' so it still drops a powerup. Author an elemental crystal to fix.");
                    crystal.crystalProperties.Element = element;
                }
                return crystal;
            }

            // No crystal authored - provision a default so death still drops a powerup.
            var set = ElementalCrystalSetSO.Load();
            if (!set)
            {
                CSDebug.LogError($"[LifeFormCrystal] {owner.name} has no crystal and " +
                    $"Resources/{ElementalCrystalSetSO.ResourcePath} is missing - cannot guarantee a death " +
                    $"powerup. Author an elemental crystal on the lifeform, or add the set asset.");
                return null;
            }

            var fallbackElement = ElementalCrystalSetSO.RandomElement();
            var prefab = set.GetPrefab(fallbackElement);
            if (!prefab)
            {
                CSDebug.LogError($"[LifeFormCrystal] {owner.name} has no crystal and the elemental crystal " +
                    $"set has no prefab for '{fallbackElement}' - cannot guarantee a death powerup.");
                return null;
            }

            CSDebug.LogWarning($"[LifeFormCrystal] {owner.name} had no elemental crystal; provisioning a " +
                $"'{fallbackElement}' crystal so it drops a powerup. Author one on the prefab to fix.");
            var provisioned = Object.Instantiate(prefab, owner.transform);
            WireCollection(provisioned, null, set);
            return provisioned;
        }
    }
}

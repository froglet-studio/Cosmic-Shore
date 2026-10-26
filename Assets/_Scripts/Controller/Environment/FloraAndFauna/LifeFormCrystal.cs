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
        // --- Heart sizing (Docs/ECOSYSTEM.md §40.2) -------------------------------------
        //
        // EVERY HEART IS ONE SIZE: ElementalCrystalSetSO.CrystalWorldScale, the size of the
        // elemental crystals in the Elemental Converter (Garrett, 2026-10-26). A heart is an
        // elemental crystal like any other, and every elemental crystal is worth exactly one
        // petal (SkimmerAdjustElementLevelByCrystalEffectSO.LevelGainPerCrystal), so a size has
        // nothing left to say about a lifeform. A tadpole's heart and a shark's heart read as the
        // same pickup because they ARE the same pickup.
        //
        // This retired the authored band (2026-08 to 2026-10): per-element HeartWorldScale values
        // in each species' variant tuning, generated from the measured body by
        // Tools/Build/author_lifeform_heart_sizes.py, with the reward following the size. Those
        // fields are still serialized and still generated, and nothing reads them.
        //
        // A per-ELEMENT APPARENT-SIZE correction is a different thing and still never goes
        // here. The four exported crystal models are very different sizes in their own FBX
        // units, so each prefab carries a correction on its model child BELOW the root
        // (Charge 1.0 / Mass 1.38 / Space 1.34 / Time 1.42). Those children equalize apparent
        // EXTENT per unit of root scale; the root carries the one size. If an ELEMENT reads
        // wrong everywhere at once, fix that element's crystal PREFAB's model child.

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
        /// Sizes a heart to the one elemental crystal size immediately - it spawns AT size, so
        /// nothing pops. Re-apply it after anything that rewrites the parent chain's scale: the
        /// size is WORLD scale, divided out of whatever the heart hangs from.
        /// </summary>
        public static void ApplyHeartSize(Crystal crystal)
        {
            if (!crystal) return;
            SetWorldScale(crystal, ElementalCrystalSetSO.CrystalWorldScale);
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
            var set = ElementalCrystalSetSO.Load();
            if (crystal && crystal.crystalProperties.Element == element)
            {
                // The authored crystal is kept, so it must be collectable on its OWN wiring - and
                // a prefab that dropped in a bare elemental crystal prefab has none (the QuadFish,
                // Clawfish, Nerve, Pine and Wall flora all did). A heart with a Crystal and a
                // trigger but no impactor is the worst shape there is: the skimmer's vacuum reads
                // the Crystal off the trigger and drags it after the vessel forever, while no
                // contact can ever collect it.
                WireCollection(crystal, null, set);
                return crystal;
            }
            var prefab = set ? set.GetPrefab(element) : null;
            if (!prefab)
            {
                CSDebug.LogError($"[LifeFormCrystal] {owner.name}: no elemental crystal prefab for " +
                    $"'{element}' in Resources/{ElementalCrystalSetSO.ResourcePath} - keeping the " +
                    $"authored crystal so death still drops a powerup.");
                return EnsureElementalCrystal(owner);
            }

            Vector3 position = crystal ? crystal.transform.localPosition : Vector3.zero;

            // The four elemental prefabs share ONE scale convention (one root scale renders one
            // apparent size for every element - the model children compensate for each export's
            // mesh size), so the authored crystal's root scale transfers directly to the
            // replacement element. It is only a placeholder for the frames before the heart is
            // sized: the moment it becomes a heart (Crystal.SetEmbeddedIn) ApplyHeartSize gives
            // it the one elemental crystal size.
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
        /// Makes a lifeform's heart skim-collectable. The standalone elemental prefabs in
        /// ElementalCrystalSet carry no collection components (most lifeform prefabs author them
        /// as inspector overrides, and some never did), so every heart that lacks the impactor +
        /// ImpactCollider pair gets it wired here - same pattern as the conveyor toy's pickups.
        /// A no-op for a crystal that already carries an impactor. Effects come from the replaced
        /// authored crystal when there was one, else from the set's defaults.
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
                // Same as the elemental path: a kept crystal is collected on its own wiring.
                WireCollection(crystal, null, ElementalCrystalSetSO.Load());
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

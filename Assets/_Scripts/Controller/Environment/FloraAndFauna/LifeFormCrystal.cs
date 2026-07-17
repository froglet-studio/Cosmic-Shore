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
    /// The misconfigured branches log loudly; the editor validator (Tools ▸ Cosmic Shore ▸
    /// Validate Lifeform Crystals) flags the same prefabs at author time so they get fixed.
    /// Per the prompter: element is per-lifeform AUTHORED - random is only the misconfig fallback.
    /// </summary>
    public static class LifeFormCrystal
    {
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

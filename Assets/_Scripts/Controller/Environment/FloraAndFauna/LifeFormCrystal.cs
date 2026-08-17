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
        // --- Heart sizing (Docs/ECOSYSTEM.md §33) ---------------------------------------
        //
        // A lifeform heart's size is a function of its owner's LEVEL and nothing else. It is
        // deliberately NOT a function of the species, the element, the prefab's authored scale,
        // or the creature's body size: the crystal's world scale is what the collect reward and
        // the domain fauna buff are both computed from (SkimmerAdjustElementLevelByCrystalEffectSO,
        // DomainFaunaBuffSystem), so a per-prefab scale is a per-prefab REWARD - and the shipped
        // prefabs ranged 0.7 (tadpole) to 4.0 (gyroid), a 5.7x spread nobody authored on purpose.
        //
        // Applied at the ONE gate every heart passes through - Crystal.SetEmbeddedIn - and
        // re-applied whenever the owner's level changes (LifeForm.ApplyLevel / LevelUp,
        // Fauna.SetLevel). Callers work in WORLD scale; the local-scale conversion divides out
        // the parent chain, so a heart carried by a body that is itself growing holds its size.
        //
        // THE ROOT SCALE IS THE GAMEPLAY NUMBER - a per-element SIZE fix never goes here.
        // The four exported crystal models are very different sizes in their own FBX units, so
        // each prefab carries a correction on its model child BELOW the root (Charge 1.0 /
        // Mass 1.38 / Space 1.34 / Time 1.42). Those children exist to equalize apparent
        // EXTENT, and at 1.0/1.0/1.34/1.42 they already did so within 7% (measured off the FBX
        // Vertices bounds normalized by UnitScaleFactor - Space's file is unit-1, the others
        // unit-100). Mass is raised to 1.38 anyway because it was reported reading THIN rather
        // than small: it is four concentric ShepardGraph shells whose visible shell sits well
        // inside the envelope, where Space is one solid body inflated by _spread. That 1.38 is
        // an eye-calibration pending a playtest, not a measurement.
        //
        // If an element reads wrong, fix ITS crystal PREFAB's model child. Scaling the root
        // instead would move the collect reward and the live domain fauna buff with it, both of
        // which read the root's lossyScale - re-opening the per-element reward spread this
        // whole system removed.

        // Used only if Resources/ElementalCrystalSet is missing - the same misconfiguration the
        // provisioning paths below already report loudly. Mirrors the shipped asset's values.
        const float FallbackLevelOneWorldScale = 3.5f;
        const float FallbackWorldScalePerLevel = 1.05f;

        /// <summary>The world scale a heart of this level renders at, for every species/element.</summary>
        public static float WorldScaleForLevel(int level)
        {
            var set = ElementalCrystalSetSO.Load();
            if (set) return set.WorldScaleForLevel(level);
            return FallbackLevelOneWorldScale * Mathf.Pow(
                FallbackWorldScalePerLevel, Mathf.Clamp(level, 1, Fauna.MaxLifeformLevel) - 1);
        }

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

        /// <summary>The local scale this crystal needs to render at its level's world size.</summary>
        public static float LocalScaleForLevel(Crystal crystal, int level) =>
            LocalScaleForWorld(crystal, WorldScaleForLevel(level));

        /// <summary>Sizes a heart to its level immediately (spawn seeding - it spawns AT size).</summary>
        public static void ApplyLevelSize(Crystal crystal, int level)
        {
            if (!crystal) return;
            SetWorldScale(crystal, WorldScaleForLevel(level));
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
            // heart is sized: the moment it becomes a heart (Crystal.SetEmbeddedIn) the level
            // curve above overwrites it, so no element or species keeps a private size.
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

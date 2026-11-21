using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Engine;

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
    /// Per the prompter: element is per-lifeform AUTHORED — random is only the misconfig fallback.
    /// </summary>
    public static class LifeFormCrystal
    {
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

            // No crystal authored — provision a default so death still drops a powerup.
            var set = ElementalCrystalSetSO.Load();
            if (!set)
            {
                CSDebug.LogError($"[LifeFormCrystal] {owner.name} has no crystal and " +
                    $"Resources/{ElementalCrystalSetSO.ResourcePath} is missing — cannot guarantee a death " +
                    $"powerup. Author an elemental crystal on the lifeform, or add the set asset.");
                return null;
            }

            var fallbackElement = ElementalCrystalSetSO.RandomElement();
            var prefab = set.GetPrefab(fallbackElement);
            if (!prefab)
            {
                CSDebug.LogError($"[LifeFormCrystal] {owner.name} has no crystal and the elemental crystal " +
                    $"set has no prefab for '{fallbackElement}' — cannot guarantee a death powerup.");
                return null;
            }

            CSDebug.LogWarning($"[LifeFormCrystal] {owner.name} had no elemental crystal; provisioning a " +
                $"'{fallbackElement}' crystal so it drops a powerup. Author one on the prefab to fix.");
            return Object.Instantiate(prefab, owner.transform);
        }
    }
}

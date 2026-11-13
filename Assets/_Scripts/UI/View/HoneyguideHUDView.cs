namespace CosmicShore.UI
{
    /// <summary>
    /// The Honeyguide's HUD view (its own since 2026-11-13; it flew the Sparrow's view and controller until then, so its
    /// round-1 cards never showed). Each card is the icon kit's picture (Tools/Build/icon_kit/specs/honeyguide.py), and its
    /// level-5 upgrade is the binding's <c>upgradedSprite</c>, swapped by the base view when the upgrade unlocks: Flit /
    /// Relentless, Hive / Hive Chain, Peck / Piercing Pecks, Slip / Slipstream. The prefab is written by
    /// Tools/Build/author_honeyguide_hud.py; the hull's controller is the fleet's base VesselHUDController.
    /// </summary>
    public class HoneyguideHUDView : VesselHUDView
    {
        /// <summary>Every card shows its authored picture from the first frame.</summary>
        public override void Initialize()
        {
            foreach (var binding in abilityIcons)
            {
                if (!binding.icon) continue;
                binding.icon.enabled = binding.icon.sprite;
            }
        }
    }
}

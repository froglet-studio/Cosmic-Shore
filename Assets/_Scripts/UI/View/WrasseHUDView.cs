namespace CosmicShore.UI
{
    /// <summary>
    /// The Wrasse's HUD view (its own since 2026-11-13; it flew the Sparrow's view until then, so its cards drew the
    /// Sparrow's parts). Each card is the kite at the knot of its lasso with what the loop makes (the icon kit's
    /// Tools/Build/icon_kit/specs/wrasse.py), and its level-5 upgrade is the binding's <c>upgradedSprite</c>, swapped by
    /// the base view when the upgrade unlocks: Kite Ram / Crystal Steal, Mass Cluster / Shielded Clusters, Space Blast /
    /// Shield Breaker, Saved Boosts / Tailwind. The counts and the Mass flash are the controller's
    /// (<see cref="WrasseHUDController"/>). The prefab is written by Tools/Build/author_wrasse_hud.py.
    /// </summary>
    public class WrasseHUDView : VesselHUDView
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

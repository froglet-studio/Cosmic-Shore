using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Orca's HUD view (its own since 2026-11-13; it flew the Sparrow's view until then, so its cards drew the
    /// Sparrow's rocket bay and stance hull). Each card is one picture of its element's seat, drawn by the icon kit
    /// (Tools/Build/icon_kit/specs/orca.py), and its level-5 upgrade is the binding's <c>upgradedSprite</c>, swapped by
    /// the base view when the upgrade unlocks:
    /// Charge Flown Missiles / Rearm, Mass Danger Shield / Bulwark, Space Burst / Chain Burst, Time Boost Tank /
    /// Afterburner. The counts and the gunner buttons are the controller's (<see cref="OrcaHUDController"/>).
    /// The prefab is written by Tools/Build/author_orca_hud.py.
    /// </summary>
    public class OrcaHUDView : VesselHUDView
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

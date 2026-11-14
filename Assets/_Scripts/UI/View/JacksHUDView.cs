namespace CosmicShore.UI
{
    /// <summary>
    /// The Jacks' HUD view (their own since 2026-11-13; they flew the Rhino's view until then, and after the Rhino's
    /// rework that view built its katana and reach blade over the Charge and Space cards). Each card is one picture drawn
    /// by the icon kit (Tools/Build/icon_kit/specs/jacks.py), and its level-5 upgrade is the binding's
    /// <c>upgradedSprite</c>, swapped by the base view when the upgrade unlocks: Charge Danger Share / Attack, Mass The
    /// Call / Mass Clusters, Space Wide / Eat Shields, Time Decoys / Second Decoy. The herd count, the danger gauge and the
    /// lit states are the controller's (<see cref="JacksHUDController"/>). The prefab is written by
    /// Tools/Build/author_jacks_hud.py.
    /// </summary>
    public class JacksHUDView : VesselHUDView
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

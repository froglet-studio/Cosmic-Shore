using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Honeyguide's HUD view (its own since 2026-11-13; it flew the Sparrow's view and controller until then, so its
    /// round-1 cards never showed). Each card is the icon kit's picture (Tools/Build/icon_kit/specs/honeyguide.py), and its
    /// level-5 upgrade is the binding's <c>upgradedSprite</c>, swapped by the base view when the upgrade unlocks: Flit /
    /// Relentless, Hive / Hive Chain, Peck / Piercing Pecks, Slip / Slipstream. The prefab is written by
    /// Tools/Build/author_honeyguide_hud.py; the hull's controller is the fleet's base VesselHUDController.
    ///
    /// <para>The two trigger cards also show what the trigger will fire (the roster audit, 2026-11-13): while a Ratel upgrade
    /// is banked on RT or LT, that trigger's card (RT the Space card, LT the Charge card) shows the banked ability's picture
    /// (<see cref="HoneyguideUpgrade.icon"/>, the kit's <c>Honeyguide/Upgrades/</c>); when the bank empties it goes back to
    /// the card's own picture, or its level-5 picture while that upgrade is live.</para>
    /// </summary>
    public class HoneyguideHUDView : VesselHUDView
    {
        static readonly Element[] TriggerCards = { Element.Space, Element.Charge };   // trigger 0 RT, 1 LT

        readonly Dictionary<Element, Sprite> _rest = new();
        readonly Sprite[] _shown = new Sprite[2];
        HoneyguideCrew _crew;

        /// <summary>Every card shows its authored picture from the first frame.</summary>
        public override void Initialize()
        {
            foreach (var binding in abilityIcons)
            {
                if (!binding.icon) continue;
                binding.icon.enabled = binding.icon.sprite;
                if (!_rest.ContainsKey(binding.element)) _rest[binding.element] = binding.icon.sprite;
            }
        }

        /// <summary>The base swaps the level-5 picture; a banked ability's picture stays on top of it.</summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            // the base remembers the card's rest sprite on its first call: hand it the card's own, never a banked picture
            foreach (var binding in abilityIcons)
                if (binding.element == element && binding.icon && _rest.TryGetValue(element, out var rest))
                    binding.icon.sprite = rest;
            base.SetAbilityUpgraded(element, upgraded);
            for (int k = 0; k < TriggerCards.Length; k++)
                if (TriggerCards[k] == element) Paint(k, true);
        }

        void LateUpdate()
        {
            if (!_crew) _crew = GetComponentInParent<HoneyguideCrew>();
            if (!_crew) return;
            for (int k = 0; k < TriggerCards.Length; k++) Paint(k, false);
        }

        void Paint(int k, bool force)
        {
            var banked = _crew ? _crew.CurrentTriggerIcon(k) : null;
            if (!force && banked == _shown[k]) return;
            _shown[k] = banked;
            var element = TriggerCards[k];
            foreach (var binding in abilityIcons)
            {
                if (binding.element != element || !binding.icon) continue;
                _rest.TryGetValue(element, out var rest);
                var own = IsAbilityUpgraded(element) && binding.upgradedSprite ? binding.upgradedSprite : rest;
                binding.icon.sprite = banked ? banked : own;
                binding.icon.enabled = binding.icon.sprite;
            }
        }
    }
}

using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Jewel Wasp's HUD view (its own since 2026-11-14; it flew the Tiger Moth's <c>ButterflyHUDView</c> until then,
    /// whose dust, drop and key parts drew the Moth's abilities over the wasp's cards). Each card is one picture drawn by
    /// the icon kit (<c>Tools/Build/icon_kit/specs/jewelwasp.py</c>), and its level-5 upgrade is the binding's
    /// <c>upgradedSprite</c>: Charge Attack / Burst, Mass Root / Overgrowth, Space Sting / Maw, Time Swim / Restless.
    ///
    /// <para>Two cards change with the wasp's mode (design: <c>R_VesselActions/JEWEL_WASP.md</c> §7): while it rides a
    /// creature, RT at prisms is the creature's SUCTION and LT its speed BURST, so the Space and Time cards show
    /// <see cref="spaceInHostIcon"/> and <see cref="timeInHostIcon"/> (<see cref="SetInHost"/>, called by
    /// <see cref="JewelWaspHUDController"/>). An unlocked level-5 card keeps its upgrade picture in either mode, because
    /// the upgrade is what the card says first. The prefab is written by <c>Tools/Build/author_jewel_wasp_hud.py</c>.</para>
    /// </summary>
    public class JewelWaspHUDView : VesselHUDView
    {
        [Header("Jewel Wasp: cards while riding a creature")]
        [Tooltip("The Space card while the wasp rides a creature: RT at prisms draws the nearest rival prisms in (Suction). " +
                 "Empty keeps the free card (Sting).")]
        [SerializeField] Sprite spaceInHostIcon;

        [Tooltip("The Time card while the wasp rides a creature: LT is the creature's burst of speed (Dash). Empty keeps the " +
                 "free card (Swim).")]
        [SerializeField] Sprite timeInHostIcon;

        Sprite _spaceFree, _timeFree;
        bool _inHost, _spaceUpgraded, _timeUpgraded, _captured;

        /// <summary>Every card shows its authored picture from the first frame.</summary>
        public override void Initialize()
        {
            foreach (var binding in abilityIcons)
            {
                if (!binding.icon) continue;
                binding.icon.enabled = binding.icon.sprite;
            }
            Capture();
        }

        /// <summary>Show the in-host cards (true) or the free ones (false). Idempotent; cheap to call every frame.</summary>
        public void SetInHost(bool inHost)
        {
            if (_captured && inHost == _inHost) return;
            _inHost = inHost;
            Apply();
        }

        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            Capture();   // before the base view swaps in the level-5 picture
            base.SetAbilityUpgraded(element, upgraded);
            if (element == Element.Space) _spaceUpgraded = upgraded;
            else if (element == Element.Time) _timeUpgraded = upgraded;
            else return;
            Apply();
        }

        void Capture()
        {
            if (_captured) return;
            _captured = true;
            if (TryGetAbilityIcon(Element.Space, out var space)) _spaceFree = space.sprite;
            if (TryGetAbilityIcon(Element.Time, out var time)) _timeFree = time.sprite;
        }

        void Apply()
        {
            Capture();
            Show(Element.Space, _spaceUpgraded, _spaceFree, spaceInHostIcon);
            Show(Element.Time, _timeUpgraded, _timeFree, timeInHostIcon);
        }

        void Show(Element element, bool upgraded, Sprite free, Sprite inHost)
        {
            if (!TryGetAbilityIcon(element, out Image icon)) return;
            if (upgraded && UpgradedSprite(element)) return;   // the base view already shows the level-5 card
            var sprite = _inHost && inHost ? inHost : free;
            if (sprite) icon.sprite = sprite;
        }

        Sprite UpgradedSprite(Element element)
        {
            foreach (var binding in abilityIcons)
                if (binding.element == element && binding.upgradedSprite) return binding.upgradedSprite;
            return null;
        }
    }
}

using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
namespace CosmicShore.UI
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        [Header("Base View (fallback)")]
        [SerializeField] private VesselHUDView baseView;

        [Header("Control hints (optional)")]
        [Tooltip("Drives the LT/RT/A/B glyph sets and attaches each hint to the ability icon its " +
                 "input drives. Found under this vessel automatically when left empty.")]
        [SerializeField] private InputDeviceIconSetSwitcher _iconSetSwitcher;

        protected R_VesselActionHandler Actions { get; private set; }
        protected VesselHUDView View => baseView;

        R_VesselElementalAbilityHandler _abilityHandler;

        // One ordering contract for the fleet - the ability row, the element flowers and this
        // seeding loop all read the same array.
        static Element[] AllElements => VesselHUDView.AbilityDisplayOrder;

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_abilityHandler)
                _abilityHandler.OnUpgradeStateChanged -= HandleUpgradeStateChanged;
            if (_iconSetSwitcher)
                _iconSetSwitcher.OnSetChanged -= HandleControlDeviceChanged;
        }

        public virtual void Initialize(IVesselStatus vesselStatus)
        {
            Actions = vesselStatus.ActionHandler;

            if (!baseView)
                baseView = GetComponentInChildren<VesselHUDView>(true);

            // Fleet-wide ability lockup (Docs/ABILITY_LOCKUP.md): the totem card that fuses each
            // ability icon with the element flower that upgrades it, and the owner of the whole
            // row's position, pitch and icon size.
            //
            // BEFORE the view initializes, not after: per-vessel views capture their icons' rest
            // scales in Initialize, and those scales are only correct once the lockup has normalised
            // each icon to the fleet's one drawn size. Seeding the upgrade state below then finds
            // the cards already built.
            baseView?.EnsureAbilityLockup();

            baseView?.Initialize();

            // Elemental upgrade highlight - shared across all vessel HUDs: the view binds each
            // ability icon to the element that upgrades it (per the vessel's ElementalAbilityMapSO)
            // and the icon glows while that upgrade is active. Idempotent across re-inits.
            if (_abilityHandler)
                _abilityHandler.OnUpgradeStateChanged -= HandleUpgradeStateChanged;
            _abilityHandler = vesselStatus.ElementalAbilityHandler;
            if (_abilityHandler && baseView)
            {
                _abilityHandler.OnUpgradeStateChanged += HandleUpgradeStateChanged;
                foreach (var element in AllElements) // seed already-active upgrades
                    baseView.SetAbilityUpgraded(element, _abilityHandler.IsUpgradeActive(element));
            }

            // Control chips. The lockup DRAWS them, from the fleet's one glyph set, keyed by the
            // control each ability's own map entry names - so a vessel authors no glyphs at all.
            // This is where it happens because this is where the ability map lives.
            SeedAbilityControls();

            // The switcher is what knows which device the player is holding. It is ENSURED rather
            // than required: three HUDs never had one, which is exactly why their authored glyphs
            // were never lit, never device-matched and never placed.
            if (!_iconSetSwitcher)
                _iconSetSwitcher = GetComponentInChildren<InputDeviceIconSetSwitcher>(true)
                                ?? gameObject.AddComponent<InputDeviceIconSetSwitcher>();

            _iconSetSwitcher.OnSetChanged -= HandleControlDeviceChanged;
            _iconSetSwitcher.OnSetChanged += HandleControlDeviceChanged;
            baseView?.SetControlDevice(_iconSetSwitcher.IsKeyboard);

#if UNITY_EDITOR
            // Structural contract: four ability icons, charge/mass/space/time, left to right.
            baseView?.ValidateAbilityIconRow(vesselStatus.VesselType);
#endif
        }

        private void HandleUpgradeStateChanged(Element element, bool active)
            => baseView?.SetAbilityUpgraded(element, active);

        private void HandleControlDeviceChanged(InputDeviceIconSetSwitcher.IconSet set)
            => baseView?.SetControlDevice(set == InputDeviceIconSetSwitcher.IconSet.KeyboardText);

        /// <summary>
        /// Hands every card the input its ability is bound to. An ability with no button
        /// (<c>FullSpeedStraightAction</c>) is passive and its chip stays blank, which is the
        /// contract the row has always had.
        /// </summary>
        private void SeedAbilityControls()
        {
            var map = _abilityHandler ? _abilityHandler.Map : null;
            if (map == null || !baseView) return;

            foreach (var entry in map.Entries)
                if (entry != null) baseView.SetAbilityControl(entry.Element, entry.Input);
        }

        public void SubscribeToEvents()
        {
            if (!Actions || !baseView) return;
            Actions.OnInputEventStarted += HandleStart;
            Actions.OnInputEventStopped += HandleStop;
        }

        public void UnsubscribeFromEvents()
        {
            if (!Actions) return;
            Actions.OnInputEventStarted -= HandleStart;
            Actions.OnInputEventStopped -= HandleStop;
        }

        public void ShowHUD() => baseView?.Show();
        public void HideHUD() => baseView?.Hide();

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop(InputEvents ev)  => Toggle(ev, false);

        /// <summary>
        /// Press feedback. The lockup CARD carries it - one signal, identical on every vessel -
        /// resolved from the input through the vessel's own ability map, so a hull that rebinds an
        /// ability to another control needs no HUD change.
        ///
        /// <para>The legacy per-vessel <c>highlights</c> list is still driven for a HUD the lockup
        /// could not claim. On a lockup vessel those images are retired chrome, so writing them is
        /// a deliberate no-op rather than a second, divergent press glow.</para>
        /// </summary>
        private void Toggle(InputEvents ev, bool on)
        {
            if (!baseView) return;

            if (TryResolveAbilityElement(ev, out var element))
                baseView.SetAbilityPressed(element, on);

            foreach (var h in baseView.highlights)
            {
                if (h.input == ev && h.image)
                    h.image.enabled = on;
            }
        }

        bool TryResolveAbilityElement(InputEvents ev, out Element element)
        {
            element = Element.None;
            var map = _abilityHandler ? _abilityHandler.Map : null;
            if (map == null) return false;

            foreach (var entry in map.Entries)
            {
                if (entry == null || entry.Input != ev) continue;
                element = entry.Element;
                return true;
            }
            return false;
        }
    }
}

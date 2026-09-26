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

            // Fleet-wide SAFE AREA (Docs/UI_ARCHITECTURE_AUDIT.md §1.3). Structural, like the lockup
            // below and for the same reason: the ability row anchors to (1, 0) of the HUD root - the
            // bottom-right corner, which on a landscape phone is the gesture pill and, on the wide
            // side, a cutout. A vessel cannot be authored without it and a new vessel inherits it.
            //
            // It WRAPS the HUD root rather than sitting on it, because NormaliseHudRoot stamps
            // anchorMin 0 / anchorMax 1 onto that rect once at build. A SafeAreaFitter there would
            // be overwritten and would never notice: it caches the safe area it last applied
            // against, and an overwrite it did not cause never reads as a change. Wrapped, the two
            // compose - the HUD root stretches to fill the SAFE rect instead of the screen.
            if (baseView)
                SafeAreaLayer.Wrap(baseView.transform as RectTransform);

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
        ///
        /// <para>The two kinds of card answer "which control?" from two places, and that follows
        /// from where the fact lives rather than from taste: an ELEMENTAL ability's input is in the
        /// vessel's <c>ElementalAbilityMapSO</c> entry, and a NON-elemental one has no map entry at
        /// all, so its binding names it. Both are one authored fact with the glyph derived from
        /// it.</para>
        /// </summary>
        private void SeedAbilityControls()
        {
            if (!baseView) return;

            // Non-elemental cards first, so a vessel with no ability map still gets its chips.
            baseView.SeedCoreAbilityControls();

            var map = _abilityHandler ? _abilityHandler.Map : null;
            if (map == null) return;

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

            if (TryResolveCoreAbility(ev, out var core))
                baseView.SetCoreAbilityPressed(core, on);

            foreach (var h in baseView.highlights)
            {
                if (h.input == ev && h.image)
                    h.image.enabled = on;
            }
        }

        /// <summary>
        /// Which ELEMENTAL card an input event presses.
        ///
        /// <para><c>FullSpeedStraightAction</c> is REFUSED before the map is searched, and that is
        /// the whole of why a Squirrel's joust card used to light up whenever the pilot flew flat
        /// out. The input enum's zero is two things at once: a real event (every input strategy
        /// raises it while the throttle is buried and the stick is centred) AND the project's
        /// "no button" sentinel - a passive ability, or an open design slot, is authored
        /// <c>Input: 0</c>. So the first-match search below matched the full-speed gesture to the
        /// FIRST passive entry in the map and pressed that card: the joust on the Squirrel, Butterfly,
        /// Manta, Rhino and Scarab, the Mass card on the Dolphin, Serpent and Urchin. The chip side
        /// already reads the zero as "no control" (<see cref="SeedAbilityControls"/>); the press side
        /// now agrees, so a passive card is never pressed by flying fast.</para>
        /// </summary>
        bool TryResolveAbilityElement(InputEvents ev, out Element element)
        {
            element = Element.None;
            if (IsPassiveSentinel(ev)) return false;

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

        /// <summary>
        /// Which NON-elemental card an input event presses, read off the binding's own input the
        /// same way its control chip is (<see cref="VesselHUDView.SeedCoreAbilityControls"/>). Before
        /// this a core card drew a chip naming its trigger and never lit when that trigger was
        /// pulled - the press path only ever searched the elemental map. The passive sentinel is
        /// refused for the reason given on <see cref="TryResolveAbilityElement"/>, which is what
        /// keeps the omni crystal card (bound to no input) from lighting at full speed.
        /// </summary>
        bool TryResolveCoreAbility(InputEvents ev, out CoreAbility ability)
        {
            ability = CoreAbility.None;
            if (IsPassiveSentinel(ev) || !baseView) return false;

            var cores = baseView.coreAbilities;
            for (int i = 0; i < cores.Count; i++)
            {
                if (cores[i].input != ev) continue;
                ability = cores[i].ability;
                return true;
            }
            return false;
        }

        /// <summary>
        /// True for the input that means "no button". Internal and pure so the rule can be held by a
        /// test without standing up a HUD.
        /// </summary>
        internal static bool IsPassiveSentinel(InputEvents ev) => ev == InputEvents.FullSpeedStraightAction;
    }
}

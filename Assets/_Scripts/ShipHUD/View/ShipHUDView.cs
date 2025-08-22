using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Game.UI;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    [System.Serializable]
    public struct ButtonIconMapping
    {
        [Tooltip("The numeric ID you pass into OnButtonPressed / OnButtonReleased")]
        public int ButtonNumber;
        public ControllerButtonIconReferences Icon;
    }

    public enum ControllerType
    {
        PlayStation,
        Xbox,
        Unknown
    }

    /// <summary>
    /// Minimal view: owns references to variant-specific UI and exposes IHUDEffects.
    /// No ResourceDisplay usage. All meter/anim calls are forwarded to IHUDEffects.
    /// Keeps controller icon auto-detection and toggling.
    /// </summary>
    public class ShipHUDView : MonoBehaviour, IShipHUDView, IHasEffects
    {
        public ShipClassType ShipHUDType => hudType;

        [Header("Core")]
        [SerializeField] private ShipClassType hudType;
        
        [Tooltip("Effects hub implementing IHUDEffects (ShipHUDEffectsHub).")]
        [SerializeField] private MonoBehaviour effectsBehaviour; 
        public IHUDEffects Effects => effectsCache ??= (effectsBehaviour as IHUDEffects);
        private IHUDEffects effectsCache;

        [Header("Containers")]
        [SerializeField] private Transform silhouetteContainer;
        [SerializeField] private Transform trailContainer;

        [Header("Controller Icon Roots")]
        [SerializeField] private GameObject psIconRoot;
        [SerializeField] private GameObject xboxIconRoot;

        [Header("Button-to-Icon mappings")]
        [SerializeField] private ButtonIconMapping[] buttonIconMappings;

        // --- Serpent Variant ---
        [Header("Serpent")]
        [SerializeField] private Button serpentBoostButton;
        [SerializeField] private Button serpentWallDisplayButton;

        // --- Dolphin Variant ---
        [Header("Dolphin")]
        [SerializeField] private Button dolphinBoostFeedback;

        // --- Manta Variant ---
        [Header("Manta")]
        [SerializeField] private Button mantaBoostButton;
        [SerializeField] private Button mantaBoost2Button;

        // --- Rhino Variant ---
        [Header("Rhino")]
        [SerializeField] private Image rhinoBoostFeedback;

        // --- Squirrel Variant ---
        [Header("Squirrel")]
        [SerializeField] private Image squirrelBoostDisplay;

        // --- Sparrow Variant ---
        [Header("Sparrow")]
        [SerializeField] private Button sparrowFullAutoAction;
        [SerializeField] private Button sparrowOverheatingBoostAction;
        [SerializeField] private Button sparrowSkyBurstMissileAction;
        [SerializeField] private Button sparrowExhaustBarrage;

        // Fast lookup for icons
        private readonly Dictionary<int, ControllerButtonIconReferences> _iconMap = new();
        public Transform GetSilhouetteContainer() => silhouetteContainer;
        public Transform GetTrailContainer() => trailContainer;
  
        private void Awake()
        {
            _iconMap.Clear();
            foreach (var bm in buttonIconMappings)
            {
                if (bm.Icon != null && !_iconMap.ContainsKey(bm.ButtonNumber))
                    _iconMap.Add(bm.ButtonNumber, bm.Icon);
            }
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            UpdateControllerIcons();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        public void Initialize(IShipHUDController controller)
        {
            // Keep listeners wiring out of here (controller/profile/subs do the work).
            // Only ensure controller icons start in the right state.
            UpdateControllerIcons();
        }

        // ---------- Controller Icons ----------

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
 
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.Disabled:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.ConfigurationChanged:
                case InputDeviceChange.SoftReset:
                    if (device is Gamepad) UpdateControllerIcons();
                    break;
                default:
                    UpdateControllerIcons();
                    break;
            }
        }

        /// <summary>
        /// Call this to auto-update which icon set is visible (PS/Xbox).
        /// </summary>
        public void UpdateControllerIcons()
        {
            var type = DetectControllerType();
            // Common toggle for all ships
            bool showPS = type == ControllerType.PlayStation || type == ControllerType.Unknown; // default PS if unknown
            bool showXbox = type == ControllerType.Xbox;

            if (psIconRoot)   psIconRoot.SetActive(showPS);
            if (xboxIconRoot) xboxIconRoot.SetActive(showXbox);
        }

        private ControllerType DetectControllerType()
        {
            foreach (var name in Gamepad.all.Select(pad => (pad.displayName ?? pad.name ?? string.Empty).ToLowerInvariant()))
            {
                if (name.Contains("xbox") || name.Contains("xinput"))
                    return ControllerType.Xbox;

                if (name.Contains("playstation") ||
                    name.Contains("dualshock") ||
                    name.Contains("dualsense") ||
                    name.Contains("sony") ||
                    name.Contains("wireless controller"))
                    return ControllerType.PlayStation;
            }

            return ControllerType.Unknown;
        }

        // ---------- IShipHUDView input feedback (icon highlight) ----------

        public void OnInputPressed(int buttonNumber)
        {
            if (_iconMap.TryGetValue(buttonNumber, out var icon))
                icon.ShowActive();
        }

        public void OnInputReleased(int buttonNumber)
        {
            if (_iconMap.TryGetValue(buttonNumber, out var icon))
                icon.ShowInactive();
        }
    }

    /// <summary>
    /// Optional adapter used by controller to get Effects without knowing concrete view type.
    /// </summary>
    public interface IHasEffects { IHUDEffects Effects { get; } }
}

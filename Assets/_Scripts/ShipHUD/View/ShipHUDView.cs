using CosmicShore.Game.UI;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    [System.Serializable]
    public struct ResourceDisplayRef
    {
        public string ResourceName;
        public ResourceDisplay Display;
    }

    public enum ControllerType
    {
        PlayStation,
        Xbox,
        Unknown
    }

    public class ShipHUDView : MonoBehaviour, IShipHUDView
    {
        public ShipTypes ShipHUDType => hudType;

        [SerializeField] private ShipTypes hudType;
        [SerializeField] private ResourceDisplayRef[] resourceDisplays;
        [SerializeField] private Transform silhouetteContainer;
        [SerializeField] private Transform trailContainer;
        [SerializeField] private GameObject psIconRoot;
        [SerializeField] private GameObject xboxIconRoot;

        // --- Serpent Variant ---
        [SerializeField] private Button serpentBoostButton;
        [SerializeField] private Button serpentWallDisplayButton;

        // --- Dolphin Variant ---
        [SerializeField] private Button dolphinBoostFeedback;

        // --- Manta Variant ---
        [SerializeField] private Button mantaBoostButton;
        [SerializeField] private Button mantaBoost2Button;

        // --- Rhino Variant ---
        [SerializeField] private Image rhinoBoostFeedback;

        // --- Squirrel Variant ---
        [SerializeField] private Image squirrelBoostDisplay;

        // --- Sparrow Variant ---
        [SerializeField] private Button sparrowFullAutoAction;
        [SerializeField] private Button sparrowOverheatingBoostAction;
        [SerializeField] private Button sparrowSkyBurstMissileAction;
        [SerializeField] private Button sparrowExhaustBarrage;


        public Transform GetSilhouetteContainer() => silhouetteContainer;
        public Transform GetTrailContainer() => trailContainer;

        public ResourceDisplay GetResourceDisplay(string name)
        {
            foreach (var rd in resourceDisplays)
                if (rd.ResourceName == name) return rd.Display;
            return null;
        }

        public void Initialize(IShipHUDController controller)
        {
            // Remove previous listeners if re-initializing
            RemoveAllButtonListeners();
            Debug.Log($"[ShipHUDView] Initialize called for {hudType}");
            switch (hudType)
            {
                case ShipTypes.Serpent:
                    if (serpentBoostButton != null)
                        serpentBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                    if (serpentWallDisplayButton != null)
                        serpentWallDisplayButton.onClick.AddListener(() => controller.OnButtonPressed(2));
                    break;
                case ShipTypes.Dolphin:
                    if (dolphinBoostFeedback != null)
                        dolphinBoostFeedback.onClick.AddListener(() => controller.OnButtonPressed(1));
                    break;
                case ShipTypes.Manta:
                    if (mantaBoostButton != null)
                        mantaBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                    break;
                case ShipTypes.Rhino:
                    //if (rhinoBoostButton != null)
                    //    rhinoBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                    break;
                case ShipTypes.Squirrel:

                    break;
                case ShipTypes.Sparrow:
                    if (sparrowFullAutoAction != null)
                        Debug.Log($"[ShipHUDView] Adding listener to: {sparrowFullAutoAction.gameObject.name}");
                    sparrowFullAutoAction.onClick.AddListener(() => {
                            Debug.Log($"[Sparrow HUD] UI Button: {sparrowFullAutoAction.gameObject.name} triggers Right Stick Action (OnButtonPressed(4))");
                            controller.OnButtonPressed(4); // Right Stick Action
                        });
                    if (sparrowOverheatingBoostAction != null)
                        sparrowOverheatingBoostAction.onClick.AddListener(() => {
                            Debug.Log($"[Sparrow HUD] UI Button: {sparrowOverheatingBoostAction.gameObject.name} triggers Overheating Boost (OnButtonPressed(2))");
                            controller.OnButtonPressed(2); // Button 2 Action
                        });
                    if (sparrowSkyBurstMissileAction != null)
                        sparrowSkyBurstMissileAction.onClick.AddListener(() => {
                            Debug.Log($"[Sparrow HUD] UI Button: {sparrowSkyBurstMissileAction.gameObject.name} triggers Sky Burst Missile (OnButtonPressed(3))");
                            controller.OnButtonPressed(3); // Button 3 Action
                        });
                    if (sparrowExhaustBarrage != null)
                        sparrowExhaustBarrage.onClick.AddListener(() => {
                            Debug.Log($"[Sparrow HUD] UI Button: {sparrowExhaustBarrage.gameObject.name} triggers Exhaust Barrage (OnButtonPressed(1))");
                            controller.OnButtonPressed(1); // Button 1 Action
                        });
                    break;
            }
        }

        private void RemoveAllButtonListeners()
        {
            if (serpentBoostButton != null) serpentBoostButton.onClick.RemoveAllListeners();
            if (serpentWallDisplayButton != null) serpentWallDisplayButton.onClick.RemoveAllListeners();

        }

        public ResourceDisplay GetResourceDisplayByIndex(int index)
        {
            if (index >= 0 && index < resourceDisplays.Length)
                return resourceDisplays[index].Display;
            return null;
        }

        public void AnimateBoostFillDown(int idx, float duration, float startAmt)
        {
            var rd = GetResourceDisplayByIndex(idx);
            rd.AnimateFillDown(duration, startAmt);
        }

        public void AnimateBoostFillUp(int idx, float duration, float endAmt)
        {
            var rd = GetResourceDisplayByIndex(idx);
            rd.AnimateFillUp(duration, endAmt);
        }

        /// <summary>
        /// Call this method to auto-update which icons are visible. Call it at Start, on scene load, and when input devices change.
        /// </summary>
        public void UpdateControllerIcons()
        {
            ControllerType type = DetectControllerType();

            switch (type)
            {
                case ControllerType.Xbox:
                    if (xboxIconRoot) xboxIconRoot.SetActive(true);
                    if (psIconRoot) psIconRoot.SetActive(false);
                    break;
                case ControllerType.PlayStation:
                    if (psIconRoot) psIconRoot.SetActive(true);
                    if (xboxIconRoot) xboxIconRoot.SetActive(false);
                    break;
                default:
                    // Default to PS, or hide both if you want
                    if (psIconRoot) psIconRoot.SetActive(true);
                    if (xboxIconRoot) xboxIconRoot.SetActive(false);
                    break;
            }
        }

        /// <summary>
        /// Returns detected controller type based on connected device names.
        /// </summary>
        public ControllerType DetectControllerType()
        {
            foreach (string joystickName in Input.GetJoystickNames())
            {
                if (string.IsNullOrEmpty(joystickName)) continue;

                string lower = joystickName.ToLower();
                if (lower.Contains("xbox") || lower.Contains("xinput"))
                    return ControllerType.Xbox;
                if (lower.Contains("playstation") || lower.Contains("dualshock") || lower.Contains("sony") || lower.Contains("wireless controller"))
                    return ControllerType.PlayStation;
            }
            return ControllerType.Unknown;
        }

    }
}
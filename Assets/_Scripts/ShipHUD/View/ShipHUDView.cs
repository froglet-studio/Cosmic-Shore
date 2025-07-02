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

    public class ShipHUDView : MonoBehaviour, IShipHUDView
    {
        public ShipTypes ShipHUDType => hudType;

        [SerializeField] private ShipTypes hudType;
        [SerializeField] private ResourceDisplayRef[] resourceDisplays;
        [SerializeField] private Transform silhouetteContainer;
        [SerializeField] private Transform trailContainer;

        // --- Serpent Variant ---
        [SerializeField] private Button serpentBoostButton;
        [SerializeField] private Button serpentWallDisplayButton;

        // --- Dolphin Variant ---
        [SerializeField] private Image dolphinBoostFeedback;

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

            switch (hudType)
            {
                case ShipTypes.Serpent:
                    if (serpentBoostButton != null)
                        serpentBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
                    if (serpentWallDisplayButton != null)
                        serpentWallDisplayButton.onClick.AddListener(() => controller.OnButtonPressed(2));
                    break;
                case ShipTypes.Dolphin:
                    //if (dolphinBoostButton != null)
                    //    dolphinBoostButton.onClick.AddListener(() => controller.OnButtonPressed(1));
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
                    sparrowFullAutoAction.onClick.AddListener(() => controller.OnButtonPressed(1));
                    sparrowOverheatingBoostAction.onClick.AddListener(() => controller.OnButtonPressed(1));
                    sparrowSkyBurstMissileAction.onClick.AddListener(() => controller.OnButtonPressed(1));
                    sparrowExhaustBarrage.onClick.AddListener(() => controller.OnButtonPressed(1));
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

    }
}




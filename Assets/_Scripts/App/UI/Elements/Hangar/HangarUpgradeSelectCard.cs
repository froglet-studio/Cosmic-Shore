using CosmicShore.App.Systems.CTA;
using CosmicShore.App.UI.Menus;
using CosmicShore.App.UI;
using CosmicShore.Integrations.PlayFab.Economy;
using UnityEngine;
using UnityEngine.UI;


namespace CosmicShore
{
    /// <summary>
    /// TODO: this is just a placeholder direct rip of the HangarOverviewView
    /// </summary>
    public class HangarUpgradeSelectCard : MonoBehaviour
    {
        /// <summary>
        /// The dynamic UI components of the card UI
        /// </summary>
        [Header("UI Components")]
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image ShipImage;
        [SerializeField] Image LockImage;
        [SerializeField] Button Button;
        [SerializeField] MenuAudio MenuAudio;
        [SerializeField] CallToActionTarget CallToActionTarget;
        [SerializeField] float ActiveSize = 64;
        [SerializeField] float InactiveSize = 52;


        /// <summary>
        /// The sprites to use when the element is active or inactive within the list
        /// </summary>
        [Header("Sprites")]
        [SerializeField] Sprite ActiveBackgroundSprite;
        [SerializeField] Sprite InactiveBackgroundSprite;

        [HideInInspector] public HangarMenu HangarMenu;
        public SO_Ship Ship;

        public void AssignShipClass(SO_Ship ship)
        {
            Ship = ship;
            LockImage.enabled = !CatalogManager.Inventory.ContainsShipClass(ship.Name);
            ShipImage.sprite = ship.Icon;
            //Button.onClick.RemoveAllListeners();
            //Button.onClick.AddListener(() => Select());
        }

        public void Select()
        {
            //HangarMenu.SelectShip(Ship);
            MenuAudio.PlayAudio();
        }

        public void SetActive()
        {
            BackgroundImage.sprite = ActiveBackgroundSprite;
            BackgroundImage.rectTransform.sizeDelta = new Vector2(ActiveSize, ActiveSize);
        }

        public void SetInactive()
        {
            BackgroundImage.sprite = InactiveBackgroundSprite;
            BackgroundImage.rectTransform.sizeDelta = new Vector2(InactiveSize, InactiveSize);
        }
    }
}

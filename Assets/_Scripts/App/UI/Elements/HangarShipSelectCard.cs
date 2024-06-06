using CosmicShore.App.Systems.CTA;
using CosmicShore.App.UI;
using CosmicShore.App.UI.Menus;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    /// <summary>
    /// The UI card view for individual items in the ship selection nav bar of the hangar
    /// </summary>
    public class HangarShipSelectCard : MonoBehaviour
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
        [SerializeField] Sprite ActiveLockSprite;
        [SerializeField] Sprite InactiveLockSprite;


        [HideInInspector] public HangarMenu HangarMenu;
        public SO_Ship Ship;

        public void AssignShipClass(SO_Ship ship)
        {
            Ship = ship;
            LockImage.enabled = !CatalogManager.Inventory.ContainsShipClass(ship.Name);
            Debug.Log($"HangarShipSelectCard.AssignShipClass - LockImage.enabled: {LockImage.enabled}");
            Debug.Log($"HangarShipSelectCard.AssignShipClass - !CatalogManager.Inventory.ContainsShipClass({ship.Name}): {!CatalogManager.Inventory.ContainsShipClass(ship.Name)}");
            ShipImage.sprite = ship.Icon;
        }

        public void Select()
        {
            HangarMenu.SelectShip(Ship);
            MenuAudio.PlayAudio();
        }

        public void SetActive()
        {
            BackgroundImage.sprite = ActiveBackgroundSprite;
            LockImage.sprite = ActiveLockSprite;
            BackgroundImage.rectTransform.sizeDelta = new Vector2(ActiveSize, ActiveSize);
        }

        public void SetInactive()
        {
            BackgroundImage.sprite = InactiveBackgroundSprite;
            LockImage.sprite = InactiveLockSprite;
            BackgroundImage.rectTransform.sizeDelta = new Vector2(InactiveSize, InactiveSize);
        }
    }
}
using CosmicShore.App.Systems.CTA;
using CosmicShore.App.UI;
using CosmicShore.App.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    /// <summary>
    /// The UI card view for individual items in the ship selection nav bar of the hangar
    /// 
    /// </summary>
    public class HangarShipSelectNavLink : NavLink
    {
        /// <summary>
        /// The dynamic UI components of the card UI
        /// </summary>
        [Header("UI Components")]
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image ShipImage;
        [SerializeField] Image LockImage;
        [SerializeField] Button Button;
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

        [HideInInspector] public HangarScreen HangarMenu;
        public SO_Ship Ship;
        int index;

        public void AssignShipClass(SO_Ship ship)
        {
            Ship = ship;
            LockImage.enabled = ship.IsLocked;
            ShipImage.sprite = ship.Icon;
        }

        public void AssignIndex(int index)
        {
            this.index = index;
        }

        public void Select()
        {
            HangarMenu.SelectShip(index);
        }

        public override void SetActive(bool isActive)
        {
            if (isActive)
            {
                BackgroundImage.sprite = ActiveBackgroundSprite;
                LockImage.sprite = ActiveLockSprite;
                BackgroundImage.rectTransform.sizeDelta = new Vector2(ActiveSize, ActiveSize);
            } else
            {
                BackgroundImage.sprite = InactiveBackgroundSprite;
                LockImage.sprite = InactiveLockSprite;
                BackgroundImage.rectTransform.sizeDelta = new Vector2(InactiveSize, InactiveSize);
            }
        }
    }
}
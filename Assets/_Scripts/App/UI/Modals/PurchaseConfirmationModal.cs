using CosmicShore.App.UI.FX;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Modals
{
    public class PurchaseConfirmationModal : ModalWindowManager
    {
        [SerializeField] TMP_Text PriceLabel;
        [SerializeField] TMP_Text UnlockText;
        [SerializeField] TMP_Text CrystalBalanceText;
        [SerializeField] Button ConfirmButton;
        [SerializeField] IconEmitter IconEmitter;
        [SerializeField] Image CaptainImage;
        [SerializeField] Image GameImage;

        Action OnConfirm;
        const string UnlockTextTemplate = "to unlock {0}";

        public void SetVirtualItem(VirtualItem virtualItem, Action confirmCallback)
        {
            PriceLabel.text = virtualItem.Price[0].Amount.ToString();
            UnlockText.text = string.Format(UnlockTextTemplate, virtualItem.Name);
            CrystalBalanceText.text = CatalogManager.Instance.GetCrystalBalance().ToString();
            OnConfirm = confirmCallback;

            switch(virtualItem.ContentType) 
            {
                case "Captain":
                    var captain = CaptainManager.Instance.GetCaptainByName(virtualItem.Name);
                    CaptainImage.sprite = captain.Image;
                    break;
                case "Game":
                    break;
                default: 
                    break;
            }
        }

        public void Confirm()
        {
            OnConfirm?.Invoke();
        }

        public void EmitIcons()
        {
            IconEmitter.EmitIcons();
        }
    }
}
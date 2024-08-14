using CosmicShore.App.UI;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class PurchaseConfirmationModal : ModalWindowManager
    {
        [SerializeField] TMP_Text PriceLabel;
        [SerializeField] TMP_Text UnlockText;
        [SerializeField] TMP_Text CrystalBalanceText;
        [SerializeField] Button ConfirmButton;
        [SerializeField] IconEmitter IconEmitter;
        Action OnConfirm;
        const string UnlockTextTemplate = "to unlock {0}";

        public void SetVirtualItem(VirtualItem virtualItem, Action confirmCallback)
        {
            PriceLabel.text = virtualItem.Price[0].Amount.ToString();
            UnlockText.text = string.Format(UnlockTextTemplate, virtualItem.Name);
            CrystalBalanceText.text = CatalogManager.Instance.GetCrystalBalance().ToString();
            OnConfirm = confirmCallback;
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
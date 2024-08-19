using CosmicShore.App.UI.FX;
using CosmicShore.Core;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using System.Collections;
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
                    GameImage.gameObject.SetActive(false);
                    CaptainImage.gameObject.SetActive(true);
                    CaptainImage.sprite = captain.Image;
                    break;
                case "Game":
                    var game = Arcade.Instance.GetArcadeGameSOByName(virtualItem.Name);
                    CaptainImage.gameObject.SetActive(false);
                    GameImage.gameObject.SetActive(true);
                    GameImage.sprite = game.CardBackground;
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

        public void UpdateBalance()
        {
            StartCoroutine(UpdateBalanceCoroutine());
        }

        IEnumerator UpdateBalanceCoroutine()
        {
            var crystalBalance = int.Parse(CrystalBalanceText.text);
            var price = int.Parse(PriceLabel.text);
            var duration = 1f;
            var elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                CrystalBalanceText.text = ((int)(crystalBalance - (price*elapsedTime/duration))).ToString();
                yield return null;
                elapsedTime += Time.unscaledDeltaTime;
            }
            CrystalBalanceText.text = CatalogManager.Instance.GetCrystalBalance().ToString();
        }
    }
}
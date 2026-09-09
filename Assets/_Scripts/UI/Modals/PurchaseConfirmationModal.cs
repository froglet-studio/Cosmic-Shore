using CosmicShore.Core;
using CosmicShore.UI;
using CosmicShore.Gameplay;
using Reflex.Attributes;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.UI
{
    public class PurchaseConfirmationModal : ModalWindowManager
    {
        [Inject] CaptainManager _captainManager;

        [SerializeField] TMP_Text PriceLabel;
        [SerializeField] TMP_Text UnlockText;
        [SerializeField] TMP_Text CrystalBalanceText;
        [SerializeField] TMP_Text TicketBalanceText;
        [SerializeField] Button ConfirmButton;
        [SerializeField] IconEmitter IconEmitter;
        [SerializeField] Image CaptainImage;
        [SerializeField] Image GameImage;
        [SerializeField] Image TicketImage;

        Action OnConfirm;
        const string UnlockTextTemplate = "to unlock {0}?";
        const string UpgradeTextTemplate = "to upgrade {0}?";

        public void SetVirtualItem(VirtualItem virtualItem, Action confirmCallback)
        {

            PriceLabel.text = virtualItem.Price[0].Amount.ToString();
            UnlockText.text = string.Format(UnlockTextTemplate, virtualItem.Name);
            CrystalBalanceText.text = CatalogManager.Instance.GetCrystalBalance().ToString();
            TicketBalanceText.text = CatalogManager.Instance.GetDailyChallengeTicketBalance().ToString();
            OnConfirm = confirmCallback;

            switch(virtualItem.ContentType)
            {
                case "Captain":
                    var captain = _captainManager.GetCaptainByName(virtualItem.Name);
                    GameImage.gameObject.SetActive(false);
                    TicketImage.gameObject.SetActive(false);
                    CaptainImage.gameObject.SetActive(true);
                    CaptainImage.sprite = captain.Image;
                    break;
                /*case "Game":
                    var game = Arcade.Instance.GetArcadeGameSOByName(virtualItem.Name);
                    CaptainImage.gameObject.SetActive(false);
                    TicketImage.gameObject.SetActive(false);
                    GameImage.gameObject.SetActive(true);
                    GameImage.sprite = game.CardBackground;
                    break;*/
                case "CaptainUpgrade":
                    var upgradeCaptain = _captainManager.GetCaptainFromUpgrade(virtualItem);
                    GameImage.gameObject.SetActive(false);
                    TicketImage.gameObject.SetActive(false);
                    CaptainImage.gameObject.SetActive(true);
                    CaptainImage.sprite = upgradeCaptain.Image;

                    // TODO: Adjust price to display correct element to spend

                    UnlockText.text = string.Format(UpgradeTextTemplate, upgradeCaptain.Name);

                    break;
                case "Ticket":
                    GameImage.gameObject.SetActive(false);
                    CaptainImage.gameObject.SetActive(false);
                    TicketImage.gameObject.SetActive(true);
                    break;
                default:
                    break;
            }
        }

        public void Confirm()
        {
            audioSystem.PlayMenuAudio(MenuAudioCategory.Confirmed);
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
            // Parse defensively - these TMP fields can hold placeholder/non-numeric text,
            // and int.Parse would throw a FormatException that aborts the coroutine.
            int.TryParse(CrystalBalanceText.text, out var crystalBalance);
            int.TryParse(PriceLabel.text, out var price);
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

        public void UpdateTicketBalance()
        {
            StartCoroutine(UpdateTicketBalanceCoroutine());
        }

        IEnumerator UpdateTicketBalanceCoroutine()
        {
            // Parse defensively, for the same reason UpdateBalanceCoroutine does: the field
            // can hold placeholder/non-numeric text and int.Parse would throw a
            // FormatException on the coroutine's FIRST line, aborting it before the balance
            // is ever written - so the player pays for a ticket and the count does not move.
            //
            // The label is read rather than the catalog, and the +1 is not a guess: the
            // label still holds the PRE-purchase balance (SetVirtualItem wrote it when the
            // modal opened and nothing has rewritten it), while CatalogManager.PurchaseItem
            // calls AddToInventory BEFORE its success callback - so the catalog is already
            // incremented by the time this runs. Reading it here and adding one would be
            // off by one.
            //
            // That is also why the fallback is the catalog rather than a defaulted 0: on the
            // parse path the label never held the pre-purchase number, so there is nothing to
            // add one to, and the catalog is exactly the value the label should be showing.
            // A ticket balance is a real-money surface - displaying a fabricated "1" is worse
            // than displaying nothing.
            var newTicketBalance = int.TryParse(TicketBalanceText.text, out var ticketBalance)
                ? ticketBalance + 1
                : CatalogManager.Instance.GetDailyChallengeTicketBalance();

            var duration = .5f;
            var elapsedTime = 0f;

            float initialFontSize = TicketBalanceText.fontSize;

            var TargetPulseMultiplier = 1.5f;


            TicketBalanceText.text = newTicketBalance.ToString();

            // pulse up
            while (elapsedTime < duration)
            {
                TicketBalanceText.fontSize = Mathf.Lerp(initialFontSize, initialFontSize * TargetPulseMultiplier, elapsedTime / duration);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            TicketBalanceText.fontSize = initialFontSize * TargetPulseMultiplier;

            elapsedTime = 0f;

            // pulse down
            while (elapsedTime < duration)
            {
                TicketBalanceText.fontSize = Mathf.Lerp(initialFontSize * TargetPulseMultiplier, initialFontSize, elapsedTime / duration);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            TicketBalanceText.fontSize = initialFontSize;
        }
    }
}
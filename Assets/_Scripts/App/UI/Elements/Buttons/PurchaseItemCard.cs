using CosmicShore.Integrations.PlayFab.Economy;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore
{
    public abstract class PurchaseItemCard : PurchaseCard
    {
        [SerializeField] protected TMP_Text PriceLabel;
        [SerializeField] protected TMP_Text UnavailablePriceLabel;
        [FormerlySerializedAs("CaptainNameLabel")]
        [SerializeField] protected TMP_Text ItemNameLabel;
        [FormerlySerializedAs("CaptainDescriptionLabel")]
        [SerializeField] protected TMP_Text ItemDescriptionLabel;
        [FormerlySerializedAs("CaptainImage")]
        [SerializeField] protected Image ItemImage;

        [SerializeField] protected Image PriceButton;
        [SerializeField] protected Image UnavailableButton;
        [Tooltip("")]
        [SerializeField] protected Image PurchasedButton;

        [SerializeField] protected Sprite DefaultBackgroundSprite;
        [SerializeField] protected Sprite PurchasedBackgroundSprite;
        [SerializeField] protected Sprite UnavailableBackgroundSprite;
        [SerializeField] protected float CardFlipAnimDuration = .5f;

        [HideInInspector] public Button ConfirmationButton;

        protected void OnEnable()
        {
            CatalogManager.OnCurrencyBalanceChange += UpdateCardOnCurrencyBalanceChange;
        }

        protected void OnDisable()
        {
            CatalogManager.OnCurrencyBalanceChange -= UpdateCardOnCurrencyBalanceChange;
        }

        protected void UpdateCardOnCurrencyBalanceChange()
        {
            // If we own the captain, just display the purchased view
            if (PlayerOwnsItem())
                return;

            // Check if it can't be afforded
            if (virtualItem.Price[0].Amount > CatalogManager.Instance.GetCrystalBalance())
            {
                // Disable the card
                GetComponent<Button>().enabled = false;

                // Hide the price button and show the unavailable version
                PriceButton.gameObject.SetActive(false);
                UnavailableButton.gameObject.SetActive(true);

                // Update background color
                BackgroundImage.sprite = UnavailableBackgroundSprite;
            }
            // Standard view otherwise
            else
            {
                // Disable the card
                GetComponent<Button>().enabled = true;

                // Hide the price button and show the unavailable version
                PriceButton.gameObject.SetActive(true);
                UnavailableButton.gameObject.SetActive(false);

                // Update background color
                BackgroundImage.sprite = DefaultBackgroundSprite;
            }
        }

        public override void SetVirtualItem(VirtualItem virtualItem)
        {
            Debug.Log($"SetVirtualItem - {virtualItem.Name},{virtualItem.Type},{virtualItem.ContentType}");
            this.virtualItem = virtualItem;
            PriceLabel.text = virtualItem.Price[0].Amount.ToString();
            UnavailablePriceLabel.text = virtualItem.Price[0].Amount.ToString();

            // Check if owned and update UI accordingly
            if (PlayerOwnsItem())
            {
                // Disable the card to prevent duplicate purchases
                GetComponent<Button>().enabled = false;

                // Hide the price button and show the purchased button
                PriceButton.gameObject.SetActive(false);
                PurchasedButton.gameObject.SetActive(true);

                // Update background color
                BackgroundImage.sprite = PurchasedBackgroundSprite;
            }
            // Check if it can't be afforded
            else if (virtualItem.Price[0].Amount > CatalogManager.Instance.GetCrystalBalance())
            {
                // Disable the card
                GetComponent<Button>().enabled = false;

                // Hide the price button and show the unavailable version
                PriceButton.gameObject.SetActive(false);
                UnavailableButton.gameObject.SetActive(true);

                // Update background color
                BackgroundImage.sprite = UnavailableBackgroundSprite;
            }
        }


        public override void OnClickBuy()
        {
            base.OnClickBuy();
            ConfirmationModal.SetVirtualItem(virtualItem, Purchase);
        }

        public override void Purchase()
        {
            ConfirmationButton.enabled = false;

            PriceButton.gameObject.SetActive(false);
            PurchasedButton.gameObject.SetActive(false);
            CatalogManager.Instance.PurchaseItem(virtualItem, virtualItem.Price[0], 1, OnPurchaseComplete, OnPurchaseFailed);
        }

        protected void OnPurchaseComplete()
        {
            // Disable the card to prevent duplicate purchases
            GetComponent<Button>().enabled = false;

            StartCoroutine(PurchaseVisualEffectCoroutine());
        }

        protected void OnPurchaseFailed()
        {
            GetComponent<Button>().enabled = true;

            // Re-enable the button in the modal in case there is an additional purchase
            ConfirmationButton.enabled = true;

            PriceButton.gameObject.SetActive(true);
            PurchasedButton.gameObject.SetActive(false);
        }

        protected IEnumerator PurchaseVisualEffectCoroutine()
        {
            ConfirmationModal.EmitIcons();
            ConfirmationModal.UpdateBalance();
            yield return new WaitForSecondsRealtime(1.25f);
            // Close Modal Window
            ConfirmationModal.ModalWindowOut();
            // Re-enable the button in the modal in case there is an additional purchase
            ConfirmationButton.enabled = true;

            yield return new WaitForSecondsRealtime(.25f);

            float halfDuration = CardFlipAnimDuration / 2f;
            float elapsedTime = 0f;

            // Rotate to 90 degrees Y
            while (elapsedTime < halfDuration)
            {
                float t = elapsedTime / halfDuration;
                float easedT = EaseInOutQuad(t);
                float angle = Mathf.Lerp(0, 90, easedT);
                transform.localRotation = Quaternion.Euler(0, angle, 0);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure the final rotation is exactly 90 degrees
            transform.localRotation = Quaternion.Euler(0, 90, 0);

            // Hide the price button and show the purchased button
            PriceButton.gameObject.SetActive(false);
            PurchasedButton.gameObject.SetActive(true);

            // Update background color
            BackgroundImage.sprite = PurchasedBackgroundSprite;

            // Reset the timer
            elapsedTime = 0f;

            // Rotate back to 0 degrees Y
            while (elapsedTime < halfDuration)
            {
                float t = elapsedTime / halfDuration;
                float easedT = EaseInOutQuad(t);
                float angle = Mathf.Lerp(90, 0, easedT);
                transform.localRotation = Quaternion.Euler(0, angle, 0);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure the final rotation is exactly 0 degrees
            transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        protected float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        protected abstract bool PlayerOwnsItem();
    }
}

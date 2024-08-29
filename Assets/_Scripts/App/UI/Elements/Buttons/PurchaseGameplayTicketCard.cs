using CosmicShore.Integrations.PlayFab.Economy;
namespace CosmicShore
{
    public class PurchaseGameplayTicketCard : PurchaseItemCard
    {
        // TODO: maybe pull out max owned so this doesn't have to override Purchase
        public override void Purchase()
        {
            ConfirmationButton.enabled = false;

            // PriceButton.gameObject.SetActive(false);
            // PurchasedButton.gameObject.SetActive(false);
            CatalogManager.Instance.PurchaseItem(virtualItem, virtualItem.Price[0], CatalogManager.MaxDailyChallengeTicketBalance, OnPurchaseComplete, OnPurchaseFailed);
        }

        protected override void OnPurchaseComplete()
        {
            base.OnPurchaseComplete();

            if (!PurchaseLimitReached())
                PriceButton.gameObject.SetActive(true);
        }

        protected override void PreModalCloseEffects()
        {
            ConfirmationModal.EmitIcons();
            ConfirmationModal.UpdateBalance();
            ConfirmationModal.UpdateTicketBalance();
        }

        protected override bool PurchaseLimitReached()
        {
            return CatalogManager.Instance.GetDailyChallengeTicketBalance() >= CatalogManager.MaxDailyChallengeTicketBalance;
        }
    }
}
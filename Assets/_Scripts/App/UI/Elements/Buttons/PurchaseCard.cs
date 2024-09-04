using CosmicShore.App.UI.Modals;
using CosmicShore.Integrations.PlayFab.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public abstract class PurchaseCard : MonoBehaviour
    {
        [HideInInspector] public PurchaseConfirmationModal ConfirmationModal;
        [SerializeField] protected Image BackgroundImage;
        protected VirtualItem virtualItem;

        public abstract void Purchase();
        public abstract void SetVirtualItem(VirtualItem virtualItem);

        public virtual void OnClickBuy()
        {
            ConfirmationModal.ModalWindowIn();
        }
    }
}
using CosmicShore.UI.Modals;
using CosmicShore.Integrations.Playfab;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Game.Managers;
namespace CosmicShore.UI.Elements
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
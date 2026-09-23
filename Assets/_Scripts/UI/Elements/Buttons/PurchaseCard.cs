using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
namespace CosmicShore.UI
{
    public abstract class PurchaseCard : MonoBehaviour
    {
        [HideInInspector] public PurchaseConfirmationModal ConfirmationModal;
        [SerializeField] protected Image BackgroundImage;
        protected VirtualItem virtualItem;

        /// <summary>
        /// Whether the last <see cref="OnClickBuy"/> was admitted by the commerce posture. A
        /// subclass that adds its own work to the press reads this instead of asking the gate
        /// again, so one press produces one refusal rather than two stings and two toasts.
        /// </summary>
        protected bool PurchaseAdmitted { get; private set; }

        public abstract void Purchase();
        public abstract void SetVirtualItem(VirtualItem virtualItem);

        /// <summary>
        /// ONE of the two paths that can open <see cref="PurchaseConfirmationModal"/> (the other is
        /// <c>HangarCaptainsView.OnClickBuy</c>), and therefore one of the two places the de-scope
        /// has to hold. While <see cref="CommerceSurface.CatalogPurchase"/> is not Available the
        /// press refuses with the shell's own sting and reason and the modal never opens
        /// (<c>Docs/STEAM_RELEASE_TASKS.md</c> R4).
        ///
        /// <para>The gate sits here rather than on the modal because a modal that opens and then
        /// explains itself has already told the player this screen takes money. It is also the
        /// reason the gate is on the PRESS and not on the card's construction: the cards are
        /// instantiated at runtime by <c>StoreScreen</c>, so there is no authored moment to mark.</para>
        ///
        /// <para><c>DailyRewardCard</c> is a subclass and is deliberately NOT affected: its claim
        /// runs through <see cref="Purchase"/> directly and it never opens the modal, so a free
        /// daily reward cannot be locked by a commerce gate.</para>
        /// </summary>
        public virtual void OnClickBuy()
        {
            PurchaseAdmitted = SO_CommerceAvailability.TryPress(gameObject, CommerceSurface.CatalogPurchase);
            if (!PurchaseAdmitted) return;

            ConfirmationModal.ModalWindowIn();
        }
    }
}

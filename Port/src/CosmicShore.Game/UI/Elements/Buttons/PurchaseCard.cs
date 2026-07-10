// Ported from Assets/_Scripts/UI/Elements/Buttons/PurchaseCard.cs (Store unit
// 2026-07-10) — verbatim; UnityEngine → CosmicShore.Engine, UnityEngine.UI →
// CosmicShore.Engine.UI.
using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.Gameplay;
namespace CosmicShore.UI
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

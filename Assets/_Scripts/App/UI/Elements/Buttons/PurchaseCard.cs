using CosmicShore.App.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public abstract class PurchaseCard : MonoBehaviour
    {
        [SerializeField] ModalWindowManager purchaseModal;
        [SerializeField] protected Image BackgroundImage;
        //[SerializeField] Sprite BackgroundSprite;

        public abstract void Purchase();
        public virtual void InitializeView()
        {
            //BackgroundImage.sprite = BackgroundSprite;
        }

        public void OnClickBuy()
        {
            purchaseModal.ModalWindowIn();
        }
    }
}
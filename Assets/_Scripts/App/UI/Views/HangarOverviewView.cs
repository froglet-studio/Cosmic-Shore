using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class HangarOverviewView : View
    {
        [SerializeField] TMP_Text ShipName;
        [SerializeField] TMP_Text ShipSummary;
        [SerializeField] TMP_Text ShipDescription;
        [SerializeField] Image ShipPreviewImage;
        [SerializeField] Image ShipLockedImage;

        void Start()
        {
            if (ShipName != null) Debug.LogWarning("HangarOverviewView - ShipName Serialized Field is not set");
            if (ShipSummary != null) Debug.LogWarning("HangarOverviewView - ShipSummary Serialized Field is not set");
            if (ShipDescription != null) Debug.LogWarning("HangarOverviewView - ShipDescription Serialized Field is not set");
            if (ShipPreviewImage != null) Debug.LogWarning("HangarOverviewView - ShipPreviewImage Serialized Field is not set");
            if (ShipLockedImage != null) Debug.LogWarning("HangarOverviewView - ShipLockedImage Serialized Field is not set");
        }

        public override void UpdateView()
        {
            var model = SelectedModel as SO_Ship;

            if (ShipName != null) ShipName.text = model.Name;
            if (ShipSummary != null) ShipSummary.text = model.Summary;
            if (ShipDescription != null) ShipDescription.text = model.Description;
            if (ShipPreviewImage != null) ShipPreviewImage.sprite = Instantiate(model.PreviewImage);
            if (ShipLockedImage != null) ShipLockedImage.gameObject.SetActive(model.IsLocked); // TODO: put this as a helper function on the SO_Ship
        }
    }
}
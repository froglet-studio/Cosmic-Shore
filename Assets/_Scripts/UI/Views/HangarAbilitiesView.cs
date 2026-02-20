using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class HangarAbilitiesView : View
    {
        [SerializeField] TMP_Text ClassName;
        [SerializeField] Image ClassLockedImage;
        [SerializeField] TMP_Text AbilityName;
        [SerializeField] TMP_Text AbilityDescription;
        [SerializeField] GameObject AbilityPreviewWindow;
        [SerializeField] Vector2 PreviewDimensions = new(256, 144);
        [SerializeField] Button TrainButton;
        [SerializeField] Button GoToStoreButton;

        void Start()
        {
            if (AbilityName == null) Debug.LogWarning("HangarAbilitiesView - AbilityName Serialized Field is not set");
            if (AbilityDescription == null) Debug.LogWarning("HangarAbilitiesView - ShipDescription Serialized Field is not set");
            if (AbilityPreviewWindow == null) Debug.LogWarning("HangarAbilitiesView - AbilityPreviewWindow Serialized Field is not set");
            if (ClassName == null) Debug.LogWarning("HangarAbilitiesView - ClassName Serialized Field is not set");
            if (ClassLockedImage == null) Debug.LogWarning("HangarAbilitiesView - ClassLockedImage Serialized Field is not set");
            if (TrainButton == null) Debug.LogWarning("HangarOverviewView - TrainButton Serialized Field is not set");
            if (GoToStoreButton == null) Debug.LogWarning("HangarOverviewView - GoToStoreButton Serialized Field is not set");
        }

        public override void UpdateView()
        {
            var model = SelectedModel as SO_ShipAbility;

            if (ClassName != null) ClassName.text = model.Ship.Name;
            if (ClassLockedImage != null) ClassLockedImage.gameObject.SetActive(model.Ship.IsLocked);
            if (AbilityName != null) AbilityName.text = model.Name;
            if (AbilityDescription != null) AbilityDescription.text = model.Description;
            if (AbilityPreviewWindow != null)
            {
                for (var i = 0; i < AbilityPreviewWindow.transform.childCount; i++)
                    Destroy(AbilityPreviewWindow.transform.GetChild(i).gameObject);

                var preview = Instantiate(model.PreviewClip, AbilityPreviewWindow.transform);
                preview.GetComponent<RawImage>().rectTransform.sizeDelta = PreviewDimensions;
                AbilityPreviewWindow.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }
            if (TrainButton != null) TrainButton.gameObject.SetActive(!model.Ship.IsLocked);
            if (GoToStoreButton != null) GoToStoreButton.gameObject.SetActive(model.Ship.IsLocked);
        }
    }
}
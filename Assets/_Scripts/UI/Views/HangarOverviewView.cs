using CosmicShore.ScriptableObjects;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class HangarOverviewView : View
    {
        [SerializeField] TMP_Text ShipName;
        [SerializeField] TMP_Text ShipDescription;
        [SerializeField] Image ShipPreviewImage;
        [SerializeField] Image ShipLockedImage;
        [SerializeField] Button TrainButton;
        [SerializeField] GameObject UnlockMessagePanel;
        [SerializeField] HangarGameplayParameterDisplayGroup HangarGameplayParameterDisplayGroup;

        void Start()
        {
            if (ShipName == null) CSDebug.LogWarning("HangarOverviewView - ShipName Serialized Field is not set");
            if (ShipDescription == null) CSDebug.LogWarning("HangarOverviewView - ShipDescription Serialized Field is not set");
            if (ShipPreviewImage == null) CSDebug.LogWarning("HangarOverviewView - ShipPreviewImage Serialized Field is not set");
            if (ShipLockedImage == null) CSDebug.LogWarning("HangarOverviewView - ShipLockedImage Serialized Field is not set");
            if (TrainButton == null) CSDebug.LogWarning("HangarOverviewView - TrainButton Serialized Field is not set");
            if (UnlockMessagePanel == null) CSDebug.LogWarning("HangarOverviewView - UnlockMessagePanel Serialized Field is not set");
            if (HangarGameplayParameterDisplayGroup == null) CSDebug.LogWarning("HangarOverviewView - HangarGameplayParameterDisplayGroup Serialized Field is not set");
        }

        public override void UpdateView()
        {
            var model = SelectedModel as SO_Ship;

            if (ShipName != null) ShipName.text = model.Name;
            if (ShipDescription != null) ShipDescription.text = model.Description;
            if (ShipPreviewImage != null) ShipPreviewImage.sprite = Instantiate(model.PreviewImage);
            if (ShipLockedImage != null) ShipLockedImage.gameObject.SetActive(model.IsLocked);
            if (TrainButton != null) TrainButton.gameObject.SetActive(!model.IsLocked);
            if (UnlockMessagePanel != null) UnlockMessagePanel.SetActive(model.IsLocked);
            if (HangarGameplayParameterDisplayGroup != null)
                HangarGameplayParameterDisplayGroup.AssignGameplayParameters(new List<GameplayParameter>() { model.gameplayParameter1, model.gameplayParameter2, model.gameplayParameter3 });
        }
    }
}
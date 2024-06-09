using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class HangarAbilitiesView : View
    {
        [SerializeField] TMP_Text AbilityName;
        [SerializeField] TMP_Text AbilityDescription;
        [SerializeField] GameObject AbilityPreviewWindow;
        [SerializeField] Vector2 PreviewDimensions = new(256, 144);

        void Start()
        {
            if (AbilityName != null) Debug.LogWarning("HangarAbilitiesView - AbilityName Serialized Field is not set");
            if (AbilityDescription != null) Debug.LogWarning("HangarAbilitiesView - ShipDescription Serialized Field is not set");
            if (AbilityPreviewWindow != null) Debug.LogWarning("HangarAbilitiesView - AbilityPreviewWindow Serialized Field is not set");
        }

        public override void UpdateView()
        {
            var model = SelectedModel as SO_ShipAbility;

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
        }
    }
}
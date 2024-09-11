using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Views
{
    public class FactionMissionGameView : View
    {
        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameDescription;
        [SerializeField] GameObject PreviewWindow;

        public override void UpdateView()
        {
            var mission = SelectedModel as SO_Mission;
            GameDescription.text = $"{mission.Description}";

            var preview = Instantiate(mission.PreviewClip);
            preview.transform.SetParent(PreviewWindow.transform, false);
            preview.GetComponent<RectTransform>().sizeDelta = new Vector2(256, 144);

            Canvas.ForceUpdateCanvases();
        }
    }
}
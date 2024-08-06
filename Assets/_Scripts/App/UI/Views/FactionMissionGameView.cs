using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI
{
    public class FactionMissionGameView : View
    {
        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameDescription;
        [SerializeField] GameObject PreviewWindow;


        public override void UpdateView()
        {
            var game = SelectedModel as SO_ArcadeGame;
            GameDescription.text = $"{game.Description}";

            var preview = Instantiate(game.PreviewClip);
            preview.transform.SetParent(PreviewWindow.transform, false);
            preview.GetComponent<RectTransform>().sizeDelta = new Vector2(256, 144);

            Canvas.ForceUpdateCanvases();
        }
    }
}
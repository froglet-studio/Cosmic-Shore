using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
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

            // The per-game preview video was retired with SO_Game.PreviewClip.

            Canvas.ForceUpdateCanvases();
        }
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    /// Holds all UI refs. No logic.
    public sealed class OverviewPanelComponentReferences : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] CanvasGroup panelCanvasGroup;

        [Header("Card Grid (children already placed in scene)")]
        [SerializeField] Transform shipCardContainer;

        [Header("Buttons")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button closeButton;

        [Header("Labels (Optional)")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text subtitleText;

        public GameObject PanelRoot => panelRoot;
        public CanvasGroup PanelCanvasGroup => panelCanvasGroup;
        public Transform ShipCardContainer => shipCardContainer;
        public Button ResumeButton => resumeButton;
        public Button CloseButton  => closeButton;
        public TMP_Text TitleText => titleText;
        public TMP_Text SubtitleText => subtitleText;
    }
}
using CosmicShore.App.UI.Controllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    /// <summary>
    /// Manages all UI elements and presentation logic (Show, Hide, alpha, interactable).
    /// No game logic.
    /// </summary>
    public sealed class OverviewPanelUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Card Grid")]
        [SerializeField] private Transform shipCardContainer;
        
        public Transform ShipCardContainer => shipCardContainer;
        
        // ---------------------------------------------------------
        // UI CONTROL
        // ---------------------------------------------------------
        public void Show()
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }
    }
}
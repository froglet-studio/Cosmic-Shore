using CosmicShore.App.Systems.VesselUnlock;
using CosmicShore.App.UI.Elements.Hangar;
using CosmicShore.App.UI.Views;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.Screens
{
    /// <summary>
    /// Hangar screen with two panels:
    /// 1. Grid selection panel - shows all vessels in a grid layout with staggered fade-in
    /// 2. Detail panel - shows vessel details, abilities, and unlock button
    /// </summary>
    public class HangarScreen : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] SO_VesselList ShipList;

        [Header("Grid Selection Panel")]
        [SerializeField] GameObject gridPanel;
        [SerializeField] Transform gridContainer;
        [SerializeField] HangarVesselGridCard gridCardPrefab;

        [Header("Grid Animation")]
        [Tooltip("Delay in seconds between each card fade-in.")]
        [SerializeField] float cardStaggerDelay = 0.08f;
        [Tooltip("Duration of each card's fade-in animation.")]
        [SerializeField] float cardFadeDuration = 0.25f;

        [Header("Detail Panel")]
        [SerializeField] GameObject detailPanel;
        [SerializeField] HangarVesselDetailView detailView;

        [Header("Legacy Views (kept for backward compatibility)")]
        [SerializeField] HangarOverviewView OverviewView;
        [SerializeField] HangarAbilitiesView AbilitiesView;
        [SerializeField] HangarTrainingModal HangarTrainingModal;
        [SerializeField] NavGroup TopNav;

        [Header("Legacy Ship Selection (deprecated)")]
        [SerializeField] Transform ShipSelectionContainer;
        [SerializeField] InfiniteScroll ShipSelectionScrollView;
        [SerializeField] HangarShipSelectNavLink ShipSelectCardPrefab;

        [Header("Legacy Training UI")]
        [SerializeField] Transform GameSelectionContainer;
        [SerializeField] Image ShipModelImage;
        [SerializeField] TMPro.TMP_Text SelectedGameName;
        [SerializeField] TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;

        List<SO_Vessel> Ships;
        SO_Vessel SelectedShip;
        readonly List<HangarVesselGridCard> _gridCards = new();
        Coroutine _gridAnimCoroutine;

        void OnEnable()
        {
            VesselUnlockSystem.OnUnlockStateChanged += RefreshGridCards;
        }

        void OnDisable()
        {
            VesselUnlockSystem.OnUnlockStateChanged -= RefreshGridCards;
        }

        public void LoadView()
        {
            Ships = ShipList.VesselList;
            VesselUnlockSystem.Initialize(ShipList);

            // New grid-based flow
            if (gridPanel && gridContainer && gridCardPrefab)
            {
                PopulateGrid();
                ShowGridPanel();
            }
            else
            {
                // Legacy flow fallback
                LoadViewLegacy();
            }
        }

        #region Grid Panel

        void PopulateGrid()
        {
            // Clear existing cards
            _gridCards.Clear();
            for (int i = gridContainer.childCount - 1; i >= 0; i--)
                Destroy(gridContainer.GetChild(i).gameObject);

            foreach (var ship in Ships)
            {
                var card = Instantiate(gridCardPrefab, gridContainer);
                card.name = $"GridCard_{ship.Name}";
                card.Configure(ship, this);
                _gridCards.Add(card);
            }
        }

        void RefreshGridCards()
        {
            foreach (var card in _gridCards)
            {
                if (card)
                    card.UpdateLockState();
            }
        }

        void ShowGridPanel()
        {
            if (gridPanel) gridPanel.SetActive(true);
            if (detailPanel) detailPanel.SetActive(false);

            // Animate grid cards in with staggered fade
            if (_gridAnimCoroutine != null)
                StopCoroutine(_gridAnimCoroutine);
            _gridAnimCoroutine = StartCoroutine(AnimateGridCardsIn());
        }

        IEnumerator AnimateGridCardsIn()
        {
            // Hide all cards immediately
            foreach (var card in _gridCards)
            {
                if (!card) continue;
                card.SetAlpha(0f);
                card.gameObject.SetActive(true);
            }

            // Stagger fade-in one by one
            for (int i = 0; i < _gridCards.Count; i++)
            {
                var card = _gridCards[i];
                if (!card) continue;

                StartCoroutine(FadeCard(card, 0f, 1f, cardFadeDuration));
                yield return new WaitForSecondsRealtime(cardStaggerDelay);
            }

            _gridAnimCoroutine = null;
        }

        IEnumerator FadeCard(HangarVesselGridCard card, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                card.SetAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }
            card.SetAlpha(to);
        }

        #endregion

        #region Detail Panel

        /// <summary>
        /// Called by HangarVesselGridCard when a vessel is tapped in the grid.
        /// Transitions to the detail panel for the selected vessel.
        /// </summary>
        public void SelectVesselForDetail(SO_Vessel ship)
        {
            if (ship == null) return;

            SelectedShip = ship;
            CSDebug.Log($"HangarScreen: Selected vessel for detail: {ship.Name}");

            if (detailView)
            {
                detailView.SetVessel(ship);
                detailView.OnBackPressed = ShowGridPanel;
            }

            ShowDetailPanel();

            // Also update legacy views if wired
            UpdateLegacyViews(ship);
        }

        void ShowDetailPanel()
        {
            if (_gridAnimCoroutine != null)
            {
                StopCoroutine(_gridAnimCoroutine);
                _gridAnimCoroutine = null;
            }

            if (gridPanel) gridPanel.SetActive(false);
            if (detailPanel) detailPanel.SetActive(true);
        }

        #endregion

        #region Legacy Support

        void LoadViewLegacy()
        {
            if (OverviewView)
                OverviewView.AssignModels(Ships.ConvertAll(x => (ScriptableObject)x));
            PopulateShipSelectionList();
        }

        void PopulateShipSelectionList()
        {
            if (ShipSelectionContainer == null)
            {
                CSDebug.LogError($"SerializedField 'ShipSelectionContainer' has not been assigned in HangarMenu");
                return;
            }

            for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            {
                var child = ShipSelectionContainer.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            for (var i = 0; i < Ships.Count; i++)
            {
                var ship = Ships[i];
                CSDebug.Log($"Populating Vessel Select List: {ship.Name}");
                var shipSelectCard = Instantiate(ShipSelectCardPrefab, ShipSelectionContainer.transform);
                shipSelectCard.name = shipSelectCard.name.Replace("(Clone)", "");
                shipSelectCard.AssignShipClass(ship);
                shipSelectCard.AssignIndex(i);
                shipSelectCard.HangarMenu = this;
            }

            if (ShipSelectionScrollView)
                ShipSelectionScrollView.Initialize(true);

            StartCoroutine(SelectFirstShipCoroutine());
        }

        public void SelectShip(int index)
        {
            var selectedShip = Ships[index];
            CSDebug.Log($"SelectShip: {selectedShip.Name}");

            if (ShipSelectionContainer)
            {
                for (var i = 0; i < ShipSelectionContainer.childCount; i++)
                {
                    var selectCard = ShipSelectionContainer.GetChild(i).gameObject.GetComponent<HangarShipSelectNavLink>();
                    selectCard.SetActive(selectCard.Ship == selectedShip);
                }
            }

            SelectedShip = selectedShip;
            UpdateLegacyViews(selectedShip);
        }

        void UpdateLegacyViews(SO_Vessel ship)
        {
            if (!ship) return;

            int shipIndex = Ships.IndexOf(ship);

            if (OverviewView && shipIndex >= 0)
                OverviewView.Select(shipIndex);

            if (AbilitiesView && ship.Abilities != null)
            {
                foreach (var ability in ship.Abilities)
                    ability.Vessel = ship;
                AbilitiesView.AssignModels(ship.Abilities.ConvertAll(x => (ScriptableObject)x));
            }
        }

        public void DisplayTrainingModal()
        {
            if (SelectedShip == null || SelectedShip.IsLocked) return;

            if (HangarTrainingModal && SelectedShip.TrainingGames != null)
            {
                HangarTrainingModal.SetTrainingGames(SelectedShip.TrainingGames);
                HangarTrainingModal.ModalWindowIn();
            }
        }

        IEnumerator SelectFirstShipCoroutine()
        {
            yield return null;
            if (ShipSelectionContainer && ShipSelectionContainer.childCount > 0)
            {
                var shipSelectCard = ShipSelectionContainer.GetChild(0).gameObject.GetComponent<HangarShipSelectNavLink>();
                CSDebug.Log($"Starting SelectShipCoroutine: {shipSelectCard.name}, {shipSelectCard.Ship.Name}");
                shipSelectCard.Select();
            }
        }

        #endregion
    }
}

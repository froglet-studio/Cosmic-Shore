using CosmicShore.App.Systems.VesselUnlock;
using CosmicShore.App.UI.Elements.Hangar;
using CosmicShore.App.UI.Views;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.Screens
{
    /// <summary>
    /// Hangar screen with two panels:
    /// 1. Grid selection panel - shows all vessels in a grid layout
    /// 2. Detail panel - shows vessel details, abilities, and unlock button
    /// </summary>
    public class HangarScreen : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] SO_ShipList ShipList;

        [Header("Grid Selection Panel")]
        [SerializeField] GameObject gridPanel;
        [SerializeField] Transform gridContainer;
        [SerializeField] HangarVesselGridCard gridCardPrefab;

        [Header("Detail Panel")]
        [SerializeField] GameObject detailPanel;
        [SerializeField] HangarVesselDetailView detailView;

        [Header("Legacy Views (kept for backward compatibility)")]
        [SerializeField] HangarOverviewView OverviewView;
        [SerializeField] HangarAbilitiesView AbilitiesView;
        [SerializeField] HangarCaptainsView CaptainsView;
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

        List<SO_Ship> Ships;
        SO_Ship SelectedShip;
        readonly List<HangarVesselGridCard> _gridCards = new();

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
            VesselUnlockSystem.Initialize();
            Ships = ShipList.ShipList;

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
        }

        #endregion

        #region Detail Panel

        /// <summary>
        /// Called by HangarVesselGridCard when a vessel is tapped in the grid.
        /// Transitions to the detail panel for the selected vessel.
        /// </summary>
        public void SelectVesselForDetail(SO_Ship ship)
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
            if (gridPanel) gridPanel.SetActive(false);
            if (detailPanel) detailPanel.SetActive(true);
        }

        #endregion

        #region Legacy Support

        /// <summary>
        /// Legacy load flow for when grid components are not wired up.
        /// </summary>
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

        /// <summary>
        /// Legacy method: called by HangarShipSelectNavLink when a vessel is selected in the old horizontal list.
        /// </summary>
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

        void UpdateLegacyViews(SO_Ship ship)
        {
            if (!ship) return;

            int shipIndex = Ships.IndexOf(ship);

            if (OverviewView && shipIndex >= 0)
                OverviewView.Select(shipIndex);

            if (AbilitiesView && ship.Abilities != null)
            {
                foreach (var ability in ship.Abilities)
                    ability.Ship = ship;
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

        System.Collections.IEnumerator SelectFirstShipCoroutine()
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

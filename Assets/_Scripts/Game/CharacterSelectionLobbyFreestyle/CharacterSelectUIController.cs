using CosmicShore.App.UI.Views;
using CosmicShore.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Controllers
{
    public class CharacterSelectUIController : MonoBehaviour
    {
        // UI view holding UI elements.
        [SerializeField] private CharacterSelectViewUI view;
        // Reference to game logic controller.
        [SerializeField] private CharacterSelectController gameController;

        // List of available ships extracted from the captains.
        private List<SO_Ship> _availableShips = new();
        // Currently selected ship.
        private SO_Ship _currentShip;
        // Index of the selected ship.
        private int _selectedShipIndex;

        // Delegate reference for proper removal of the confirm button listener.
        private UnityEngine.Events.UnityAction _onConfirmClickedAction;

        private void Awake()
        {
            // Subscribe to UI events.
            view.ShipSelectButton.onClick.AddListener(OnShipSelectClicked);
            view.ReadyButton.onClick.AddListener(OnReadyClicked);
            _onConfirmClickedAction = OnConfirmClicked;
            view.ConfirmButton.onClick.AddListener(_onConfirmClickedAction);
        }


        private void Start()
        {
            foreach (Toggle toggle in view.TeamToggles)
            {
                toggle.group = view.TeamToggleGroup;
                toggle.onValueChanged.AddListener((isOn) => OnTeamToggleChanged(toggle, isOn));
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from UI events to avoid memory leaks.
            view.ShipSelectButton.onClick.RemoveListener(OnShipSelectClicked);
            view.ReadyButton.onClick.RemoveListener(OnReadyClicked);
            view.UnreadyButton.onClick.RemoveListener(OnUnreadyClicked);
            view.ConfirmButton.onClick.RemoveListener(_onConfirmClickedAction);


        }

        /// <summary>
        /// Logs the names of all available ships and sets the first ship as the current ship.
        /// Also spawns the first ship in the designated placeholder.
        /// </summary>
        /// <param name="selectedGame">The selected arcade game data.</param>
        internal void LogAllShips(SO_ArcadeGame selectedGame)
        {
            // Extract available ships from the captains.
            _availableShips = selectedGame.Captains.Select(captain => captain.Ship).ToList();

            if (_availableShips != null && _availableShips.Count > 0)
            {
                // Assign the first ship as the current ship and update the placeholder.
                _currentShip = _availableShips[0];
                SpawnFirstShipOnPlaceholder();

                // Log the name of each available ship.
                foreach (var ship in _availableShips)
                {
                    Debug.Log("Available Ship: " + ship.Name);
                }
            }
            else
            {
                Debug.LogWarning("No available ships found.");
            }
        }

        /// <summary>
        /// Updates the UI image to display the current ship's squad image.
        /// </summary>
        private void SpawnFirstShipOnPlaceholder()
        {
            if (view.ClientClassImage != null && _currentShip != null)
            {
                view.ClientClassImage.sprite = _currentShip.SquadImage;
            }
            else
            {
                Debug.LogWarning("ClientClassImage or current ship is not assigned.");
            }
        }

        /// <summary>
        /// Called when the Ship Select button is clicked.
        /// Displays the ship grid menu.
        /// </summary>
        private void OnShipSelectClicked()
        {
            ShowShipGridMenu();
        }

        /// <summary>
        /// Displays the ship grid menu and instantiates UI elements for each available ship.
        /// </summary>
        private void ShowShipGridMenu()
        {
            if (view.CharacterSelectionPanel != null)
            {
                view.CharacterSelectionPanel.SetActive(true);

                foreach (Transform child in view.CharacterDataContent)
                {
                    Destroy(child.gameObject);
                }

                // Instantiate a UI element for each available ship.
                for (int i = 0; i < _availableShips.Count; i++)
                {
                    SO_Ship shipData = _availableShips[i];
                    CharacterSelectClassComponentReference shipComponent = Instantiate(view.CharacterSelectListElement, view.CharacterDataContent);

                    // Set ship icon.
                    if (shipComponent.ClassIcon != null)
                        shipComponent.ClassIcon.sprite = shipData.SquadImage;

                    // Set the selection image to active only if this ship is the current ship.
                    if (shipData != _currentShip)
                    {
                        shipComponent.ClassSelectedImage.SetActive(false);
                    }
                    else
                    {
                        shipComponent.ClassSelectedImage.SetActive(true);
                    }

                    int index = i;

                    // Assign listener for selecting a ship.
                    shipComponent.SelectClassButton.onClick.AddListener(() =>
                    {
                        if (view.MainClassSelectionPanelImage != null)
                            view.MainClassSelectionPanelImage.sprite = shipData.SquadImage;
                        if (view.CharacterSelectText != null)
                            view.CharacterSelectText.text = shipData.Name;

                        _selectedShipIndex = index;
                        ToggleSelectionImage();
                        shipComponent.ClassSelectedImage.SetActive(true);
                    });
                }
            }
            else
            {
                Debug.LogWarning("CharacterSelectionPanel is not assigned in the view.");
            }
        }

        /// <summary>
        /// Updates the selection visuals so that only the currently selected ship is highlighted.
        /// </summary>
        private void ToggleSelectionImage()
        {
            // Iterate over the children in the content area.
            foreach (Transform child in view.CharacterDataContent)
            {
                CharacterSelectClassComponentReference shipComponent = child.GetComponent<CharacterSelectClassComponentReference>();
                if (child.GetSiblingIndex() == _selectedShipIndex)
                {
                    shipComponent.ClassSelectedImage.SetActive(true);
                }
                else
                {
                    shipComponent.ClassSelectedImage.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Hides the ship grid menu.
        /// </summary>
        public void HideGridMenu()
        {
            if (view.CharacterSelectionPanel != null)
            {
                view.CharacterSelectionPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Called when the Confirm button is clicked.
        /// </summary>
        private void OnConfirmClicked()
        {
            OnShipConfirmed(_selectedShipIndex);
        }

        /// <summary>
        /// Locks the ship selection and updates the main UI to reflect the confirmed ship.
        /// </summary>
        /// <param name="selectedShipIndex">Index of the confirmed ship.</param>
        public void OnShipConfirmed(int selectedShipIndex)
        {
            Debug.Log("Ship selected and locked: Index " + selectedShipIndex);
            _currentShip = _availableShips[selectedShipIndex];
            SpawnFirstShipOnPlaceholder();
            HideGridMenu();

            gameController.OnShipChoose(selectedShipIndex);
        }

        /// <summary>
        /// Called when the Ready button is clicked.
        /// Sends the ready command to the game controller and swaps the UI button.
        /// </summary>
        public void OnReadyClicked()
        {
            gameController.OnReadyButtonClicked();
        }

        /// <summary>
        /// Called when the Unready button is clicked.
        /// Sends the unready command to the game controller and swaps the UI button.
        /// </summary>
        private void OnUnreadyClicked()
        {
            gameController.OnUnreadyButtonClicked();
        }

        /// <summary>
        /// Swaps the Ready button with a locked button to indicate the confirmed ready state.
        /// </summary>
        public void SwapReadyButton(bool isReady)
        {
            view.ReadyButton.gameObject.SetActive(!isReady);
            view.UnreadyButton.gameObject.SetActive(isReady);

            // this needs to be streamed accross the server
            view.StateText.text = isReady ? "Ready" : "Idle";
        }

        /// <summary>
        /// Called when a team toggle is selected.
        /// </summary>
        /// <param name="selectedToggle">The toggle that was selected.</param>
        private void OnTeamToggleChanged(Toggle changedToggle, bool isOn)
        {
            changedToggle.image.sprite = isOn ? view.ToggleSelectedSprites[1] : view.ToggleSelectedSprites[0];

            if (isOn)
            {
                int teamIndex = view.TeamToggles.IndexOf(changedToggle);
                Debug.Log("Team selected: " + teamIndex);

                gameController.OnTeamChoose(teamIndex);
            }
        }
    }
}

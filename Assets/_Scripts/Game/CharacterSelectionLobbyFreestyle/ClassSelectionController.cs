using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace CosmicShore.Game.UI
{
    public interface ICharacterSelectionController
    {
        /// <summary>
        /// Fired when the player selects a ship.
        /// </summary>
        event Action<int> OnShipSelected;

        /// <summary>
        /// Initialize the selection UI with a list of captains.
        /// </summary>
        void Initialize();
    }

    [RequireComponent(typeof(CanvasGroup))]
    public class ClassSelectionController : MonoBehaviour, ICharacterSelectionController
    {
        [SerializeField]
        SO_ArcadeGame _selectedGame;

        private List<SO_Ship> _availableShips;

        public event Action<int> OnShipSelected;

        private void Start()
        {
            Initialize();
        }


        /// <summary>
        /// Populates UI buttons and sets default selection.
        /// </summary>
        public void Initialize()
        {
            _availableShips = _selectedGame.Captains.Select(c => c.Ship).ToList();
            //CreateButtons();
            if (_availableShips.Count > 0)
                SelectShip(0);
            Debug.Log($"Initialization Completed!");
        }

        private void CreateButtons()
        {
            // Clear old buttons
            //foreach (Transform child in _buttonContainer)
            //    Destroy(child.gameObject);

            //// Create a button for each ship
            //for (int i = 0; i < _availableShips.Count; i++)
            //{
            //    var ship = _availableShips[i];
            //    var btnObj = Instantiate(_shipButtonPrefab, _buttonContainer);
            //    var button = btnObj.GetComponent<Button>();
            //    var label = btnObj.GetComponentInChildren<TMP_Text>();
            //    label.text = ship.Name;
            //    int index = i; // capture
            //    button.onClick.AddListener(() => SelectShip(index));
            //}
        }

        public void SelectShip(int index)
        {
            //SpawnShipPreview();
            OnShipSelected?.Invoke(index);
            Debug.Log($"Ship selected and locked: Index {index}");
        }

        private void SpawnShipPreview()
        {
            // Remove previous preview
            //foreach (Transform child in _spawnPlaceholder)
            //    Destroy(child.gameObject);

            // Instantiate new ship prefab under placeholder
            //Instantiate(_currentShip.Prefab, _spawnPlaceholder);
        }
    }
}

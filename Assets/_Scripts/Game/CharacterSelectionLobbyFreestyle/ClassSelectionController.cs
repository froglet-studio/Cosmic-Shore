using CosmicShore.Utilities;
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

        [SerializeField] IntDataSO _shipTypeData;

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

        public void SelectShip(int index)
        {
            _shipTypeData.Value = index;

            //SpawnShipPreview();
            OnShipSelected?.Invoke(index);
            Debug.Log($"Ship selected and locked: Index {index}");
        }
    }
}

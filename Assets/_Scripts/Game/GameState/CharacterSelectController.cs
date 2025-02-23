using CosmicShore.App.UI.Views;
using CosmicShore.Integrations.PlayFab.Economy;
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;

namespace CosmicShore.Game.GameState
{
    public class CharacterSelectController : MonoBehaviour
    {
        [SerializeField]
        NetcodeHooks _netcodeHooks;

        [Tooltip("If true, will filter out unowned ships from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] 
        bool _respectInventoryForShipSelection = false;

        [SerializeField]
        SO_ArcadeGame _selectedGame;

        [SerializeField]
        CharacterSelectionView _characterSelectionView;

        private void OnEnable()
        {
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
        }

        private void OnDisable()
        {
            _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
        }

        private void OnNetworkSpawn()
        {
            InitializeShipSelectionView();
        }

        void InitializeShipSelectionView()
        {
            if (_respectInventoryForShipSelection)
            {
                List<SO_Captain> filteredCaptains = _selectedGame.Captains.Where(x => CaptainManager.Instance.UnlockedShips.Contains(x.Ship)).ToList();
                _characterSelectionView.AssignModels(filteredCaptains.ConvertAll(x => (ScriptableObject)x.Ship));
            }
            else
            {
                _characterSelectionView.AssignModels(_selectedGame.Captains.ConvertAll(x => (ScriptableObject)x.Ship));
            }
        }
    }
}

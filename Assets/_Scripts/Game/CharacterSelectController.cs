using CosmicShore.App.UI.Controllers;
using CosmicShore.App.UI.Views;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace CosmicShore.Game
{
    public class CharacterSelectController : NetworkBehaviour
    {
        [Tooltip("If true, will filter out unowned ships from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS)")]
        [SerializeField]
        bool _respectInventoryForShipSelection = false;

        [SerializeField]
        SO_ArcadeGame _selectedGame;

        [SerializeField]
        CharacterSelectionView _characterSelectionView;
        [SerializeField]
        CharacterSelectUIController _characterSelectUIController;

        // NetworkList holding each client's selection.
        public NetworkList<CharacterSelectData> CharacterSelections = new();

        [Inject]
        SceneNameListSO _sceneNameList;

        // server -side datas
        int _readyCount = 0;

        public override void OnNetworkSpawn()
        {
            InitializeShipSelectionView();
        }

        // Called by the local client when selecting a ship.
        public void OnShipChoose(int index)
        {
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            OnShipChoose_ServerRpc(index, clientId);
        }

        public void OnTeamChoose(int index)
        {
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            OnTeamChoose_ServerRpc(index, clientId);
        }

        // Called by the local client when clicking the ready button.
        public void OnReadyButtonClicked()
        {
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            OnReadyButtonClicked_ServerRpc(clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnTeamChoose_ServerRpc(int index, ulong clientId)
        {
            // Update the client's ship selection while preserving its current ready state.
            bool updated = false;
            for (int i = 0; i < CharacterSelections.Count; i++)
            {
                if (CharacterSelections[i].ClientId == clientId)
                {
                    int shipTypeIndex = CharacterSelections[i].ShipTypeIndex;
                    bool currentReady = CharacterSelections[i].IsReady;
                    CharacterSelections[i] = new CharacterSelectData(clientId, shipTypeIndex, index, currentReady);
                    updated = true;
                    break;
                }
            }
            if (!updated)
            {
                // New entry with IsReady set to false by default.
                CharacterSelections.Add(new CharacterSelectData(clientId, 0, index, false));
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void OnShipChoose_ServerRpc(int index, ulong clientId)
        {
            // Update the client's ship selection while preserving its current ready state.
            bool updated = false;
            for (int i = 0; i < CharacterSelections.Count; i++)
            {
                if (CharacterSelections[i].ClientId == clientId)
                {
                    int currentTeamIndex = CharacterSelections[i].TeamIndex;
                    bool currentReady = CharacterSelections[i].IsReady;
                    CharacterSelections[i] = new CharacterSelectData(clientId, index, currentTeamIndex, currentReady);
                    updated = true;
                    break;
                }
            }
            if (!updated)
            {
                // New entry with IsReady set to false by default.
                CharacterSelections.Add(new CharacterSelectData(clientId, index, 0, false));
            }
        }

        public void OnUnreadyButtonClicked()
        {
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            OnUnreadyButtonClicked_ServerRpc(clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnUnreadyButtonClicked_ServerRpc(ulong clientId)
        {
            bool found = false;
            for (int i = 0; i < CharacterSelections.Count; i++)
            {
                if (CharacterSelections[i].ClientId == clientId)
                {
                    if (CharacterSelections[i].IsReady) // Only update if currently ready
                    {
                        CharacterSelectData updatedData = new(
                            clientId,
                            CharacterSelections[i].ShipTypeIndex,
                            CharacterSelections[i].TeamIndex,
                            false); // Force unready state
                        CharacterSelections[i] = updatedData;
                        _readyCount--;
                    }
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                // If no entry exists, add one with ready state set to false.
                CharacterSelections.Add(new CharacterSelectData(clientId, 0, 0, false));
            }

            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { clientId } }
            };
            // Update UI for unready state (commented out as per your UI integration needs)
            ToggleReadyButtonTextClientRpc(false, rpcParams);
        }


        [ServerRpc(RequireOwnership = false)]
        void OnReadyButtonClicked_ServerRpc(ulong clientId)
        {
            bool updatedReadyState = false;
            bool found = false;
            for (int i = 0; i < CharacterSelections.Count; i++)
            {
                if (CharacterSelections[i].ClientId == clientId)
                {
                    bool currentReady = CharacterSelections[i].IsReady;
                    // Toggle the ready state.
                    updatedReadyState = !currentReady;
                    CharacterSelectData updatedData = new(
                        clientId,
                        CharacterSelections[i].ShipTypeIndex,
                        CharacterSelections[i].TeamIndex,
                        updatedReadyState);

                    CharacterSelections[i] = updatedData;

                    // Update _readyCount accordingly.
                    if (updatedReadyState)
                    {
                        _readyCount++;
                    }
                    else
                    {
                        _readyCount--;
                    }

                    found = true;
                    break;
                }
            }
            if (!found)
            {
                // If the client doesn't have an entry yet, add one with a default ship index (-1) and ready true.
                updatedReadyState = true;
                CharacterSelections.Add(new CharacterSelectData(clientId, 0, 0, true));
                _readyCount++;
            }

            // Send a ClientRpc to update the ready button text on the specific client.
            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new List<ulong> { clientId }
                }
            };
            ToggleReadyButtonTextClientRpc(updatedReadyState, rpcParams);
        }

        [ClientRpc]
        void ToggleReadyButtonTextClientRpc(bool isReady, ClientRpcParams clientRpcParams = default)
        {
            _characterSelectUIController.SwapReadyButton(isReady);
        }

        void InitializeShipSelectionView()
        {
            if (_respectInventoryForShipSelection)
            {
                List<SO_Captain> filteredCaptains = _selectedGame.Captains
                    .Where(x => CaptainManager.Instance.UnlockedShips.Contains(x.Ship))
                    .ToList();
                _characterSelectUIController.LogAllShips(_selectedGame);
                // _characterSelectionView.AssignModels(filteredCaptains.ConvertAll(x => (ScriptableObject)x.Ship));
            }
            else
            {
                _characterSelectUIController.LogAllShips(_selectedGame);
                // _characterSelectionView.AssignModels(_selectedGame.Captains.ConvertAll(x => (ScriptableObject)x.Ship));
            }
        }

        private ShipTypes GetShipTypeFromIndex(int index)
        {
            // Get the list of available captains (filtered if necessary)
            List<SO_Captain> availableCaptains;
            if (_respectInventoryForShipSelection)
            {
                availableCaptains = _selectedGame.Captains
                    .Where(x => CaptainManager.Instance.UnlockedShips.Contains(x.Ship))
                    .ToList();
            }
            else
            {
                availableCaptains = _selectedGame.Captains.ToList();
            }

            // Check if the index is valid.
            if (index < 0 || index >= availableCaptains.Count)
            {
                Debug.LogWarning($"Invalid ship selection index: {index}");
                // Return a default ShipType or handle the error as needed.
                return ShipTypes.Manta;
            }

            // Return the ShipType of the captain at the given index.
            return availableCaptains[index].Ship.Class;
        }

        private Teams GetTeamFromIndex(int index)
        {
            return index switch
            {
                0 => Teams.Jade,
                1 => Teams.Ruby,
                2 => Teams.Blue,
                _ => Teams.Gold,
            };
        }
    }
}

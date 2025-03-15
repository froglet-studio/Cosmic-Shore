using CosmicShore.App.UI.Views;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Utilities;
using System;
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

        // NetworkList holding each client's selection.
        public NetworkList<CharacterSelectData> CharacterSelections = new();

        [Inject]
        SceneNameListSO _sceneNameList;

        int _readyCount = 0;
        // Remove Coroutine field since we're using async/await
        // Coroutine _loadGameRoutine;

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

        // Called by the local client when clicking the ready button.
        public void OnReadyButtonClicked()
        {
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            OnReadyButtonClicked_ServerRpc(clientId);
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
                    bool currentReady = CharacterSelections[i].IsReady;
                    CharacterSelections[i] = new CharacterSelectData(clientId, index, currentReady);
                    updated = true;
                    break;
                }
            }
            if (!updated)
            {
                // New entry with IsReady set to false by default.
                CharacterSelections.Add(new CharacterSelectData(clientId, index, false));
            }

            // Convert the selected index to a ShipTypes value.
            ShipTypes newShipType = GetShipTypeFromIndex(index);

            // Retrieve the player's NetworkObject and update their default ship type.
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerNetObj != null)
            {
                NetworkPlayer player = playerNetObj.GetComponent<NetworkPlayer>();
                if (player != null)
                {
                    player.SetDefaultShipType(newShipType);
                }
            }
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
                    CharacterSelectData updatedData = new CharacterSelectData(clientId, CharacterSelections[i].Index, updatedReadyState);
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
                CharacterSelections.Add(new CharacterSelectData(clientId, -1, true));
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

            // If all connected players are ready, load the multiplayer scene after 3 seconds using async/await.
            if (_readyCount == NetworkManager.Singleton.ConnectedClients.Count && IsServer)
            {
                // Fire-and-forget async method (on server)
                DelayedSceneLoadAsync();
            }
        }

        [ClientRpc]
        void ToggleReadyButtonTextClientRpc(bool isReady, ClientRpcParams clientRpcParams = default)
        {
            _characterSelectionView.ToggleReadyButtonText(isReady);
        }

        // Async method to wait 3 seconds before loading the scene.
        private async void DelayedSceneLoadAsync()
        {
            await Task.Delay(3000);
            Debug.Log("3 seconds elapsed. Attempting to load scene: " + _sceneNameList.MultiplayerScene);
            if (SceneLoaderWrapper.Instance == null)
            {
                Debug.LogError("SceneLoaderWrapper.Instance is null! Cannot load scene.");
            }
            else
            {
                SceneLoaderWrapper.Instance.LoadScene(_sceneNameList.MultiplayerScene, true);
                Debug.Log("LoadScene called on SceneLoaderWrapper.Instance.");
            }
        }

        void InitializeShipSelectionView()
        {
            if (_respectInventoryForShipSelection)
            {
                List<SO_Captain> filteredCaptains = _selectedGame.Captains
                    .Where(x => CaptainManager.Instance.UnlockedShips.Contains(x.Ship))
                    .ToList();
                _characterSelectionView.AssignModels(filteredCaptains.ConvertAll(x => (ScriptableObject)x.Ship));
            }
            else
            {
                _characterSelectionView.AssignModels(_selectedGame.Captains.ConvertAll(x => (ScriptableObject)x.Ship));
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
    }

    public struct CharacterSelectData : INetworkSerializable, IEquatable<CharacterSelectData>
    {
        private ulong _clientId;
        private int _index;
        private bool _isReady;

        // Constructor to initialize the fields.
        public CharacterSelectData(ulong clientId, int index, bool isReady)
        {
            _clientId = clientId;
            _index = index;
            _isReady = isReady;
        }

        // Convenience constructor with default IsReady = false.
        public CharacterSelectData(ulong clientId, int index) : this(clientId, index, false) { }

        // Public accessors.
        public ulong ClientId => _clientId;
        public int Index => _index;
        public bool IsReady => _isReady;

        // INetworkSerializable implementation.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _clientId);
            serializer.SerializeValue(ref _index);
            serializer.SerializeValue(ref _isReady);
        }

        // IEquatable implementation.
        public bool Equals(CharacterSelectData other)
        {
            return _clientId == other._clientId && _index == other._index && _isReady == other._isReady;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterSelectData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + _clientId.GetHashCode();
                hash = hash * 23 + _index.GetHashCode();
                hash = hash * 23 + _isReady.GetHashCode();
                return hash;
            }
        }
    }
}

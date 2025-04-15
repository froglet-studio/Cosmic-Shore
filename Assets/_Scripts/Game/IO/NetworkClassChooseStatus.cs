using CosmicShore.Game;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Core
{
    public struct ShipSelectData : INetworkSerializable, IEquatable<ShipSelectData>
    {
        private ulong _clientId;
        private int _shipTypeIndex;

        // Constructor to initialize the fields.
        public ShipSelectData(ulong clientId, int shipTypeIndex)
        {
            _clientId = clientId;
            _shipTypeIndex = shipTypeIndex;
        }

        // Public accessors.
        public ulong ClientId => _clientId;
        public int ShipTypeIndex => _shipTypeIndex;

        // INetworkSerializable implementation.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _clientId);
            serializer.SerializeValue(ref _shipTypeIndex);
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
                hash = hash * 23 + _shipTypeIndex.GetHashCode();
                return hash;
            }
        }

        public bool Equals(ShipSelectData other)
        {
            return _clientId == other.ClientId &&
                _shipTypeIndex == other.ShipTypeIndex;
        }
    }

    public class NetworkClassChooseStatus : NetworkBehaviour
    {
        [SerializeField]
        int _shipType;

        readonly NetworkList<ShipSelectData> ShipSelections = new();

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                OnShipChoose_ServerRpc(_shipType, clientId);
            }

            StartCoroutine(SpawnShips());
        }

        [ServerRpc(RequireOwnership = false)]
        void OnShipChoose_ServerRpc(int index, ulong clientId)
        {
            // Update the client's ship selection while preserving its current ready state.
            bool updated = false;
            for (int i = 0; i < ShipSelections.Count; i++)
            {
                if (ShipSelections[i].ClientId == clientId)
                {
                    ShipSelections[i] = new ShipSelectData(clientId, index);
                    updated = true;
                    break;
                }
            }
            if (!updated)
            {
                // New entry with IsReady set to false by default.
                ShipSelections.Add(new ShipSelectData(clientId, index));
            }
        }

        IEnumerator SpawnShips()
        {
            yield return new WaitForSeconds(5f);

            // Do debug in green color, spawn ships
            Debug.Log("<color=green>Spawning ships...</color>");
        }
    }
}

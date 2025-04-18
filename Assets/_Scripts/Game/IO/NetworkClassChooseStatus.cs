using CosmicShore.Game;
using CosmicShore.Utilities;
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

        public ShipSelectData(ulong clientId, int shipTypeIndex)
        {
            _clientId = clientId;
            _shipTypeIndex = shipTypeIndex;
        }

        public ulong ClientId => _clientId;
        public int ShipTypeIndex => _shipTypeIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _clientId);
            serializer.SerializeValue(ref _shipTypeIndex);
        }

        public override bool Equals(object obj)
        {
            return obj is ShipSelectData other && Equals(other);
        }

        public bool Equals(ShipSelectData other)
        {
            return _clientId == other._clientId &&
                   _shipTypeIndex == other._shipTypeIndex;
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
    }

    public class NetworkClassChooseStatus : NetworkBehaviour
    {
        [SerializeField] private IntDataSO _shipTypeData;

        private readonly NetworkList<ShipSelectData> ShipSelections = new();

        public int GetShipIndex(ulong clientId)
        {
            foreach (var shipData in ShipSelections)
            {
                if (shipData.ClientId == clientId)
                    return shipData.ShipTypeIndex;
            }
            return 0;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Debug.Log($"Ship to spawn is {_shipTypeData.Value}");
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                OnShipChoose_ServerRpc(_shipTypeData.Value, clientId);
            }

            StartCoroutine(SpawnShips());
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnShipChoose_ServerRpc(int index, ulong clientId)
        {
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
                ShipSelections.Add(new ShipSelectData(clientId, index));
            }
        }

        private IEnumerator SpawnShips()
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("<color=green>Spawning ships...</color>");
        }
    }
}

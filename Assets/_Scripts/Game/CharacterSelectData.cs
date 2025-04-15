using System;
using Unity.Netcode;

namespace CosmicShore.Game
{
    public struct CharacterSelectData : INetworkSerializable, IEquatable<CharacterSelectData>
    {
        private ulong _clientId;
        private int _shipTypeIndex;
        private int _teamIndex;
        private bool _isReady;

        // Constructor to initialize the fields.
        public CharacterSelectData(ulong clientId, int shipTypeIndex, int teamIndex, bool isReady)
        {
            _clientId = clientId;
            _shipTypeIndex = shipTypeIndex;
            _teamIndex = teamIndex;
            _isReady = isReady;
        }

        // Convenience constructor with default IsReady = false.
        public CharacterSelectData(ulong clientId, int shipIndex, int teamIndex) :
            this(clientId, shipIndex, teamIndex, false)
        { }

        // Public accessors.
        public ulong ClientId => _clientId;
        public int ShipTypeIndex => _shipTypeIndex;
        public int TeamIndex => _teamIndex;
        public bool IsReady => _isReady;

        // INetworkSerializable implementation.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _clientId);
            serializer.SerializeValue(ref _shipTypeIndex);
            serializer.SerializeValue(ref _teamIndex);
            serializer.SerializeValue(ref _isReady);
        }

        // IEquatable implementation.
        public bool Equals(CharacterSelectData other)
        {
            return _clientId == other._clientId &&
                _shipTypeIndex == other._shipTypeIndex &&
                _teamIndex == other.TeamIndex &&
                _isReady == other._isReady;
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
                hash = hash * 23 + _teamIndex.GetHashCode();
                hash = hash * 23 + _isReady.GetHashCode();
                return hash;
            }
        }
    }
}

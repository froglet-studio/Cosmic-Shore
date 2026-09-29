using System;
using System.Collections;
using System.Collections.Generic;

namespace CosmicShore.Engine.Networking
{
    /// <summary>Marker: the type is serialized by a raw memory copy (original: INetworkSerializeByMemcpy).</summary>
    public interface INetworkSerializeByMemcpy { }

    /// <summary>A change to a <see cref="NetworkList{T}"/> (original: NetworkListEvent&lt;T&gt;).</summary>
    public struct NetworkListEvent<T>
    {
        public enum EventType : byte { Add, Insert, Remove, RemoveAt, Value, Clear, Full }
        public EventType Type;
        public T Value;
        public T PreviousValue;
        public int Index;
    }

    /// <summary>
    /// Replicated list (original: NetworkList&lt;T&gt;). Single-process: every mutation raises
    /// <see cref="OnListChanged"/> locally, exactly the callbacks a host observes for its own writes.
    /// </summary>
    public class NetworkList<T> : IEnumerable<T>, IDisposable
    {
        public delegate void OnListChangedDelegate(NetworkListEvent<T> changeEvent);
        public event OnListChangedDelegate OnListChanged;

        readonly List<T> _list = new();
        public NetworkVariableReadPermission ReadPerm { get; }
        public NetworkVariableWritePermission WritePerm { get; }

        public NetworkList(IEnumerable<T> values = default,
            NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server)
        {
            ReadPerm = readPerm; WritePerm = writePerm;
            if (values != null) _list.AddRange(values);
        }

        public int Count => _list.Count;
        public int LastModifiedTick => 0;

        public T this[int index]
        {
            get => _list[index];
            set
            {
                var prev = _list[index];
                _list[index] = value;
                Raise(NetworkListEvent<T>.EventType.Value, value, index, prev);
            }
        }

        public void Add(T item) { _list.Add(item); Raise(NetworkListEvent<T>.EventType.Add, item, _list.Count - 1); }
        public void Insert(int index, T item) { _list.Insert(index, item); Raise(NetworkListEvent<T>.EventType.Insert, item, index); }

        public bool Remove(T item)
        {
            int i = _list.IndexOf(item);
            if (i < 0) return false;
            _list.RemoveAt(i);
            Raise(NetworkListEvent<T>.EventType.Remove, item, i);
            return true;
        }

        public void RemoveAt(int index)
        {
            var v = _list[index];
            _list.RemoveAt(index);
            Raise(NetworkListEvent<T>.EventType.RemoveAt, v, index);
        }

        public void Clear() { _list.Clear(); Raise(NetworkListEvent<T>.EventType.Clear, default, 0); }
        public bool Contains(T item) => _list.Contains(item);
        public int IndexOf(T item) => _list.IndexOf(item);
        public void SetDirty(bool isDirty) { }
        public void Dispose() { }
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        void Raise(NetworkListEvent<T>.EventType type, T value, int index, T previous = default)
            => OnListChanged?.Invoke(new NetworkListEvent<T> { Type = type, Value = value, Index = index, PreviousValue = previous });
    }

    /// <summary>Serializable handle to a spawned NetworkBehaviour (original: NetworkBehaviourReference).</summary>
    public struct NetworkBehaviourReference : IEquatable<NetworkBehaviourReference>, INetworkSerializable
    {
        NetworkBehaviour _behaviour;
        public NetworkObjectReference m_NetworkObjectReference;
        public ushort m_NetworkBehaviourId;

        public NetworkBehaviourReference(NetworkBehaviour networkBehaviour)
        {
            if (networkBehaviour == null) throw new ArgumentNullException(nameof(networkBehaviour));
            _behaviour = networkBehaviour;
            m_NetworkObjectReference = default;
            m_NetworkBehaviourId = networkBehaviour.NetworkBehaviourId;
        }

        public bool TryGet(out NetworkBehaviour networkBehaviour, NetworkManager networkManager = null)
        {
            networkBehaviour = _behaviour;
            return networkBehaviour != null;
        }

        public bool TryGet<T>(out T networkBehaviour, NetworkManager networkManager = null) where T : NetworkBehaviour
        {
            networkBehaviour = _behaviour as T;
            return networkBehaviour != null;
        }

        public void NetworkSerialize<TReader>(BufferSerializer<TReader> serializer) where TReader : IReaderWriter { }

        public bool Equals(NetworkBehaviourReference other) => ReferenceEquals(_behaviour, other._behaviour);
        public override bool Equals(object obj) => obj is NetworkBehaviourReference r && Equals(r);
        public override int GetHashCode() => _behaviour?.GetHashCode() ?? 0;

        public static implicit operator NetworkBehaviour(NetworkBehaviourReference r) => r._behaviour;
        public static implicit operator NetworkBehaviourReference(NetworkBehaviour b) => new(b);
    }
}

namespace CosmicShore.Engine.Networking.Components
{
    /// <summary>
    /// Transform replication (original: NetworkTransform). Single-process there is nothing to
    /// replicate; the authority split is preserved so the game's authority checks read true.
    /// </summary>
    public class NetworkTransform : NetworkBehaviour
    {
        public bool SyncPositionX = true, SyncPositionY = true, SyncPositionZ = true;
        public bool SyncRotAngleX = true, SyncRotAngleY = true, SyncRotAngleZ = true;
        public bool SyncScaleX = true, SyncScaleY = true, SyncScaleZ = true;
        public float PositionThreshold = 0.001f, RotAngleThreshold = 0.01f, ScaleThreshold = 0.01f;
        public bool InLocalSpace;
        public bool Interpolate = true;
        public bool UseQuaternionSynchronization;
        public bool UseHalfFloatPrecision;
        public bool SlerpPosition;

        public bool CanCommitToTransform => OnIsServerAuthoritative() ? IsServer : IsOwner;
        public bool IsServerAuthoritative() => OnIsServerAuthoritative();
        protected virtual bool OnIsServerAuthoritative() => true;

        public void Teleport(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            transform.position = newPosition;
            transform.rotation = newRotation;
            transform.localScale = newScale;
        }

        public void SetState(Vector3? posIn = null, Quaternion? rotIn = null, Vector3? scaleIn = null, bool teleportDisabled = true)
        {
            if (posIn.HasValue) transform.position = posIn.Value;
            if (rotIn.HasValue) transform.rotation = rotIn.Value;
            if (scaleIn.HasValue) transform.localScale = scaleIn.Value;
        }
    }

    /// <summary>Animator replication (original: NetworkAnimator).</summary>
    public class NetworkAnimator : NetworkBehaviour
    {
        [SerializeField] Animator m_Animator;
        public Animator Animator { get => m_Animator; set => m_Animator = value; }
        public bool IsServerAuthoritative() => OnIsServerAuthoritative();
        protected virtual bool OnIsServerAuthoritative() => true;
        public void SetTrigger(string triggerName) => m_Animator?.SetTrigger(triggerName);
        public void SetTrigger(int hash) => m_Animator?.SetTrigger(hash);
        public void ResetTrigger(string triggerName) => m_Animator?.ResetTrigger(triggerName);
        public void ResetTrigger(int hash) => m_Animator?.ResetTrigger(hash);
    }

    /// <summary>Rigidbody replication (original: NetworkRigidbody).</summary>
    public class NetworkRigidbody : NetworkBehaviour
    {
        public bool UseRigidBodyForMotion;
        public bool AutoUpdateKinematicState = true;
    }
}

namespace CosmicShore.Engine.Networking.Transports.UTP
{
    /// <summary>
    /// The Unity Transport (original: UnityTransport). Offline the port's NetworkManager is
    /// single-process; connection data is recorded for observability and the RTT is zero.
    /// </summary>
    public class UnityTransport : NetworkTransport
    {
        [Serializable]
        public struct ConnectionAddressData
        {
            public string Address;
            public ushort Port;
            public string ServerListenAddress;
        }

        public ConnectionAddressData ConnectionData = new() { Address = "127.0.0.1", Port = 7777, ServerListenAddress = "0.0.0.0" };
        public int MaxPacketQueueSize = 128;
        public int MaxPayloadSize = 6144;
        public int HeartbeatTimeoutMS = 500;
        public int ConnectTimeoutMS = 1000;
        public int MaxConnectAttempts = 60;
        public int DisconnectTimeoutMS = 30000;
        public bool UseWebSockets;
        public bool UseEncryption;

        public void SetConnectionData(string ipv4Address, ushort port, string listenAddress = null)
            => ConnectionData = new ConnectionAddressData { Address = ipv4Address, Port = port, ServerListenAddress = listenAddress ?? ConnectionData.ServerListenAddress };

        public void SetRelayServerData(object relayServerData) { }
        public void SetDebugSimulatorParameters(int packetDelay, int packetJitter, int dropRate) { }
        public override ulong GetCurrentRtt(ulong clientId) => 0;
    }
}

namespace CosmicShore.Engine.Networking
{
    /// <summary>Serializable handle to a spawned NetworkObject (original: NetworkObjectReference).</summary>
    public struct NetworkObjectReference : IEquatable<NetworkObjectReference>, INetworkSerializable
    {
        NetworkObject _object;
        public ulong NetworkObjectId { get; private set; }

        public NetworkObjectReference(NetworkObject networkObject)
        {
            _object = networkObject ?? throw new ArgumentNullException(nameof(networkObject));
            NetworkObjectId = networkObject.NetworkObjectId;
        }

        public NetworkObjectReference(GameObject gameObject) : this(gameObject.GetComponent<NetworkObject>()) { }

        public bool TryGet(out NetworkObject networkObject, NetworkManager networkManager = null)
        {
            networkObject = _object;
            if (networkObject == null && (networkManager ?? NetworkManager.Singleton)?.SpawnManager?.SpawnedObjects.TryGetValue(NetworkObjectId, out var o) == true)
                networkObject = o;
            return networkObject != null;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
        public bool Equals(NetworkObjectReference other) => NetworkObjectId == other.NetworkObjectId;
        public override bool Equals(object obj) => obj is NetworkObjectReference r && Equals(r);
        public override int GetHashCode() => NetworkObjectId.GetHashCode();

        public static implicit operator NetworkObject(NetworkObjectReference r) { r.TryGet(out var o); return o; }
        public static implicit operator NetworkObjectReference(NetworkObject o) => new(o);
        public static implicit operator GameObject(NetworkObjectReference r) { r.TryGet(out var o); return o != null ? o.gameObject : null; }
        public static implicit operator NetworkObjectReference(GameObject g) => new(g);
    }
}

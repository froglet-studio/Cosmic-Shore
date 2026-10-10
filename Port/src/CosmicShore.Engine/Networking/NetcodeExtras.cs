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
    [Serializable] // inlined by value on Instantiate, like NetworkVariable (original: NetworkVariableBase)
    public class NetworkList<T> : IEnumerable<T>, IDisposable, INetVar
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
        public void SetDirty(bool isDirty) { if (isDirty && _behaviour != null) NetDriver.MarkDirty(this); }
        public void Dispose() { }
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [NonSerialized] NetworkBehaviour _behaviour;
        [NonSerialized] int _index;
        [NonSerialized] List<NetworkListEvent<T>> _pending;

        void Raise(NetworkListEvent<T>.EventType type, T value, int index, T previous = default)
        {
            var e = new NetworkListEvent<T> { Type = type, Value = value, Index = index, PreviousValue = previous };
            if (_behaviour != null && !_applyingRemote)
            {
                if (!NetDriver.CanWrite(this)) NetDriver.ReportWritePermission(this);
                (_pending ??= new()).Add(e);
                NetDriver.MarkDirty(this);
            }
            else if (_behaviour != null && NetDriver.IsServer)
                (_pending ??= new()).Add(e); // a client's change the server relays onward
            OnListChanged?.Invoke(e);
        }

        [NonSerialized] bool _applyingRemote;

        NetworkBehaviour INetVar.Behaviour => _behaviour;
        int INetVar.Index => _index;
        void INetVar.Bind(NetworkBehaviour behaviour, int index) { _behaviour = behaviour; _index = index; }

        void INetVar.WriteState(System.IO.BinaryWriter w)
        {
            w.Write(_list.Count);
            foreach (var v in _list) NetWire.Write(w, typeof(T), v);
        }

        void INetVar.ReadState(System.IO.BinaryReader r, bool notify)
        {
            int n = r.ReadInt32();
            _list.Clear();
            for (int i = 0; i < n; i++) _list.Add((T)NetWire.Read(r, typeof(T)));
            if (notify) OnListChanged?.Invoke(new NetworkListEvent<T> { Type = NetworkListEvent<T>.EventType.Full });
        }

        void INetVar.WriteDelta(System.IO.BinaryWriter w)
        {
            var ops = _pending ?? new List<NetworkListEvent<T>>();
            w.Write(ops.Count);
            foreach (var e in ops)
            {
                w.Write((byte)e.Type);
                w.Write(e.Index);
                NetWire.Write(w, typeof(T), e.Value);
            }
        }

        void INetVar.ClearDelta() => _pending?.Clear();

        void INetVar.ReadDelta(System.IO.BinaryReader r)
        {
            int n = r.ReadInt32();
            _applyingRemote = true;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var type = (NetworkListEvent<T>.EventType)r.ReadByte();
                    int index = r.ReadInt32();
                    var value = (T)NetWire.Read(r, typeof(T));
                    switch (type)
                    {
                        case NetworkListEvent<T>.EventType.Add: Add(value); break;
                        case NetworkListEvent<T>.EventType.Insert: Insert(Math.Min(index, _list.Count), value); break;
                        case NetworkListEvent<T>.EventType.Remove: Remove(value); break;
                        case NetworkListEvent<T>.EventType.RemoveAt: if (index < _list.Count) RemoveAt(index); break;
                        case NetworkListEvent<T>.EventType.Value: if (index < _list.Count) this[index] = value; break;
                        case NetworkListEvent<T>.EventType.Clear: Clear(); break;
                    }
                }
            }
            finally { _applyingRemote = false; }
        }
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
    /// Transform replication (original: NetworkTransform). The authority - the server, or the
    /// owner when <see cref="AuthorityMode"/> is Owner (or a subclass says so, the
    /// ClientNetworkTransform idiom) - sends its pose; everyone else interpolates.
    /// </summary>
    public class NetworkTransform : NetworkBehaviour
    {
        public enum AuthorityModes { Server = 0, Owner = 1 }

        /// <summary>Who may move this transform (serialized; the default is Server).</summary>
        public AuthorityModes AuthorityMode = AuthorityModes.Server;

        public bool SyncPositionX = true, SyncPositionY = true, SyncPositionZ = true;
        public bool SyncRotAngleX = true, SyncRotAngleY = true, SyncRotAngleZ = true;
        public bool SyncScaleX = true, SyncScaleY = true, SyncScaleZ = true;
        public float PositionThreshold = 0.001f, RotAngleThreshold = 0.01f, ScaleThreshold = 0.01f;
        public bool InLocalSpace;
        public bool Interpolate = true;
        public bool UseQuaternionSynchronization;
        public bool UseHalfFloatPrecision;
        public bool SlerpPosition;
        /// <summary>
        /// Send pose changes on the unreliable channel (serialized, as the original's field). Each pose
        /// carries the network time it was taken at, so a late or reordered one is dropped; a teleport
        /// still goes reliably, and once the transform settles its last pose is sent reliably too
        /// (a lost final update must not leave it stuck). Four fauna prefabs opt in.
        /// </summary>
        public bool UseUnreliableDeltas;

        public bool CanCommitToTransform => OnIsServerAuthoritative() ? IsServer : IsOwner;
        public bool IsServerAuthoritative() => OnIsServerAuthoritative();
        protected virtual bool OnIsServerAuthoritative() => AuthorityMode == AuthorityModes.Server;

        public void Teleport(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            transform.position = newPosition;
            transform.rotation = newRotation;
            transform.localScale = newScale;
            _teleportPending = true;
        }

        // ── Replication (driven by NetDriver) ──────────────────────────
        // The authority sends its pose when it moved past the thresholds; everyone else keeps a short
        // buffer of received poses and renders slightly in the past, interpolating between them.

        [NonSerialized] bool _teleportPending, _sentOnce;
        [NonSerialized] Vector3 _sentPos, _sentScale;
        [NonSerialized] Quaternion _sentRot;
        [NonSerialized] readonly List<(double t, Vector3 p, Quaternion q, Vector3 s)> _buffer = new();

        Vector3 PosNow => InLocalSpace ? transform.localPosition : transform.position;
        Quaternion RotNow => InLocalSpace ? transform.localRotation : transform.rotation;

        internal bool PortTakeOutgoing(out Vector3 p, out Quaternion q, out Vector3 s, out bool teleport)
        {
            p = PosNow; q = RotNow; s = transform.localScale;
            teleport = _teleportPending;
            bool moved = !_sentOnce || teleport
                || (p - _sentPos).sqrMagnitude > PositionThreshold * PositionThreshold
                || Quaternion.Angle(q, _sentRot) > RotAngleThreshold
                || (s - _sentScale).sqrMagnitude > ScaleThreshold * ScaleThreshold;
            if (!moved) return false;
            _sentOnce = true; _teleportPending = false;
            PortSent++;
            _sentPos = p; _sentRot = q; _sentScale = s;
            return true;
        }

        [NonSerialized] double _lastStamp = double.MinValue, _lastSendTime;
        [NonSerialized] bool _lastSendUnreliable;

        /// <summary>Keeps only poses newer than the newest applied (a teleport always applies). False = drop it.</summary>
        internal bool PortAcceptStamp(double stamp, bool teleport)
        {
            if (!teleport && stamp <= _lastStamp) { PortStale++; return false; }
            _lastStamp = Math.Max(_lastStamp, stamp);
            return true;
        }

        internal void PortMarkSent(double now, bool unreliable)
        {
            _lastSendTime = now;
            _lastSendUnreliable = unreliable;
        }

        /// <summary>
        /// True once, when the last pose went unreliably and the transform has not moved for a quarter
        /// second: its settled pose is then sent reliably, so a lost last datagram cannot strand it.
        /// </summary>
        internal bool PortNeedsKeyframe(double now, out Vector3 p, out Quaternion q, out Vector3 s)
        {
            p = _sentPos; q = _sentRot; s = _sentScale;
            if (!_lastSendUnreliable || now - _lastSendTime < 0.25) return false;
            _lastSendUnreliable = false;
            return true;
        }

        /// <summary>Diagnostics: stamped poses dropped because a newer one was already applied.</summary>
        public int PortStale { get; private set; }

        /// <summary>Diagnostics: poses received / sent since spawn.</summary>
        public int PortReceived { get; private set; }
        public int PortSent { get; private set; }

        internal void PortPushState(double time, Vector3 p, Quaternion q, Vector3 s, bool teleport)
        {
            PortReceived++;
            if (teleport || !Interpolate) _buffer.Clear();
            _buffer.Add((time, p, q, s));
            if (_buffer.Count > 16) _buffer.RemoveAt(0);
            if (teleport || !Interpolate || _buffer.Count == 1) Apply(p, q, s);
        }

        internal void PortInterpolate(double now)
        {
            if (_buffer.Count == 0 || !Interpolate) return;
            uint tick = NetworkManager.Singleton?.NetworkConfig?.TickRate ?? 30;
            double renderTime = now - 3.0 / Math.Max(1u, tick);
            while (_buffer.Count > 2 && _buffer[1].t <= renderTime) _buffer.RemoveAt(0);
            var a = _buffer[0];
            if (_buffer.Count == 1 || renderTime <= a.t) { Apply(a.p, a.q, a.s); return; }
            var b = _buffer[1];
            float f = (float)Math.Clamp((renderTime - a.t) / Math.Max(1e-6, b.t - a.t), 0, 1);
            Apply(Vector3.Lerp(a.p, b.p, f), Quaternion.Slerp(a.q, b.q, f), Vector3.Lerp(a.s, b.s, f));
        }

        void Apply(Vector3 p, Quaternion q, Vector3 s)
        {
            if (InLocalSpace) { transform.localPosition = p; transform.localRotation = q; }
            else transform.SetPositionAndRotation(p, q);
            transform.localScale = s;
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

    /// <summary>
    /// Rigidbody replication (original: NetworkRigidbody). With motion left to the sibling
    /// NetworkTransform (UseRigidBodyForMotion off - the case every shipped prefab authors), its
    /// job is the kinematic state: every non-authority copy is kinematic so its local physics
    /// cannot fight the replicated pose, and it is restored on despawn.
    /// </summary>
    public class NetworkRigidbody : NetworkBehaviour
    {
        public bool UseRigidBodyForMotion;
        public bool AutoUpdateKinematicState = true;
        public bool AutoSetKinematicOnDespawn = true;

        Rigidbody _body;
        NetworkTransform _transform;
        bool _originalKinematic;

        public override void OnNetworkSpawn()
        {
            _body = GetComponent<Rigidbody>();
            _transform = GetComponent<NetworkTransform>();
            if (_body == null) return;
            _originalKinematic = _body.isKinematic;
            UpdateKinematic();
        }

        public override void OnGainedOwnership() => UpdateKinematic();
        public override void OnLostOwnership() => UpdateKinematic();

        public override void OnNetworkDespawn()
        {
            if (_body != null && AutoSetKinematicOnDespawn) _body.isKinematic = true;
        }

        void UpdateKinematic()
        {
            if (_body == null || !AutoUpdateKinematicState) return;
            bool authority = _transform != null ? _transform.CanCommitToTransform : IsServer;
            _body.isKinematic = authority ? _originalKinematic : true;
        }
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

        /// <summary>A reference received over the wire: the id only, resolved through the spawn manager.</summary>
        internal static NetworkObjectReference FromId(ulong id) => new() { NetworkObjectId = id };

        public bool TryGet(out NetworkObject networkObject, NetworkManager networkManager = null)
        {
            networkObject = _object != null && _object ? _object : null;
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

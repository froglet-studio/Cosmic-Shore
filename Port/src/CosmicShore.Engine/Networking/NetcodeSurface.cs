using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Original-contract NetworkConfig. Offline it is data only: the single-process host reads
    /// <see cref="ConnectionData"/> as its own approval payload and <see cref="PlayerPrefab"/> is
    /// what live code instantiates for AI players; nothing is sent anywhere.
    /// </summary>
    public sealed class NetworkConfig
    {
        public GameObject PlayerPrefab { get; set; }
        public NetworkTransport NetworkTransport { get; set; }
        public byte[] ConnectionData { get; set; } = Array.Empty<byte>();
        public bool ConnectionApproval { get; set; } = true;
        public uint TickRate { get; set; } = 30;
        public bool EnableSceneManagement { get; set; } = true;
        public bool ForceSamePrefabs { get; set; } = true;
        public bool RecycleNetworkIds { get; set; } = true;
        public int ClientConnectionBufferTimeout { get; set; } = 10;
        public NetworkTopologyTypes NetworkTopology { get; set; } = NetworkTopologyTypes.ClientServer;
        public NetworkPrefabs Prefabs { get; } = new NetworkPrefabs();
    }

    public enum NetworkTopologyTypes { ClientServer = 0, DistributedAuthority = 1 }

    /// <summary>One registered network prefab (original: <c>NetworkPrefab</c>).</summary>
    public sealed class NetworkPrefab
    {
        public GameObject Prefab { get; set; }
        public GameObject SourcePrefabToOverride { get; set; }
        public GameObject OverridingTargetPrefab { get; set; }
        public uint SourceHashToOverride { get; set; }
    }

    /// <summary>The prefab registry (original: <c>NetworkPrefabs</c>).</summary>
    public sealed class NetworkPrefabs
    {
        readonly List<NetworkPrefab> _prefabs = new();
        public IReadOnlyList<NetworkPrefab> Prefabs => _prefabs;

        public bool Add(NetworkPrefab prefab)
        {
            if (prefab?.Prefab == null || Contains(prefab.Prefab)) return false;
            _prefabs.Add(prefab);
            return true;
        }

        public bool Contains(GameObject prefab)
        {
            foreach (var p in _prefabs)
                if (p.Prefab == prefab) return true;
            return false;
        }

        public void Remove(GameObject prefab) => _prefabs.RemoveAll(p => p.Prefab == prefab);
        public void Remove(NetworkPrefab prefab) => _prefabs.Remove(prefab);
    }

    /// <summary>Custom prefab spawn handler hook (original: <c>INetworkPrefabInstanceHandler</c>).</summary>
    public interface INetworkPrefabInstanceHandler
    {
        NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation);
        void Destroy(NetworkObject networkObject);
    }

    /// <summary>Prefab handler registry. Offline nothing is spawned remotely, so it is a recorder.</summary>
    public sealed class NetworkPrefabHandler
    {
        readonly Dictionary<GameObject, INetworkPrefabInstanceHandler> _handlers = new();

        public bool AddHandler(GameObject prefab, INetworkPrefabInstanceHandler handler)
        {
            if (prefab == null || handler == null) return false;
            _handlers[prefab] = handler;
            return true;
        }

        public bool AddHandler(NetworkObject prefab, INetworkPrefabInstanceHandler handler)
            => prefab != null && AddHandler(prefab.gameObject, handler);

        public bool RemoveHandler(GameObject prefab) => prefab != null && _handlers.Remove(prefab);
        public bool RemoveHandler(NetworkObject prefab) => prefab != null && RemoveHandler(prefab.gameObject);
        public bool ContainsHandler(GameObject prefab) => prefab != null && _handlers.ContainsKey(prefab);
    }

    /// <summary>Named-message surface. Offline a message sent to the server is delivered locally.</summary>
    public sealed class CustomMessagingManager
    {
        public delegate void HandleNamedMessageDelegate(ulong senderClientId, FastBufferReader messagePayload);

        readonly Dictionary<string, HandleNamedMessageDelegate> _named = new();

        public void RegisterNamedMessageHandler(string name, HandleNamedMessageDelegate callback) => _named[name] = callback;
        public void UnregisterNamedMessageHandler(string name) => _named.Remove(name);

        public void SendNamedMessage(string name, ulong clientId, FastBufferWriter writer,
            NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
        {
            if (_named.TryGetValue(name, out var handler))
                handler(0, new FastBufferReader(writer.ToArray()));
        }

        public void SendNamedMessageToAll(string name, FastBufferWriter writer,
            NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
            => SendNamedMessage(name, 0, writer, delivery);
    }

    public enum NetworkDelivery { Unreliable, UnreliableSequenced, Reliable, ReliableSequenced, ReliableFragmentedSequenced }

    /// <summary>Minimal byte writer (original: FastBufferWriter). Offline payloads never leave the process.</summary>
    public struct FastBufferWriter : IDisposable
    {
        List<byte> _bytes;
        public FastBufferWriter(int size, CosmicShore.Engine.Collections.Allocator allocator, int maxSize = -1) { _bytes = new List<byte>(Math.Max(0, size)); }
        public int Length => _bytes?.Count ?? 0;
        public void WriteValueSafe(in byte value) => (_bytes ??= new()).Add(value);
        public void WriteValueSafe(in int value) => (_bytes ??= new()).AddRange(BitConverter.GetBytes(value));
        public void WriteValueSafe(in ulong value) => (_bytes ??= new()).AddRange(BitConverter.GetBytes(value));
        public void WriteValueSafe(in float value) => (_bytes ??= new()).AddRange(BitConverter.GetBytes(value));
        public void WriteValueSafe(in bool value) => (_bytes ??= new()).Add(value ? (byte)1 : (byte)0);
        public void WriteValueSafe(string value, bool oneByteChars = false)
        {
            var b = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteValueSafe(b.Length);
            (_bytes ??= new()).AddRange(b);
        }
        public byte[] ToArray() => _bytes?.ToArray() ?? Array.Empty<byte>();
        public void Dispose() { }
    }

    /// <summary>Minimal byte reader matching <see cref="FastBufferWriter"/>.</summary>
    public struct FastBufferReader : IDisposable
    {
        readonly byte[] _bytes;
        int _pos;
        public FastBufferReader(byte[] bytes) { _bytes = bytes ?? Array.Empty<byte>(); _pos = 0; }
        public int Length => _bytes?.Length ?? 0;
        public int Position => _pos;
        public void ReadValueSafe(out byte value) { value = _bytes[_pos]; _pos += 1; }
        public void ReadValueSafe(out int value) { value = BitConverter.ToInt32(_bytes, _pos); _pos += 4; }
        public void ReadValueSafe(out ulong value) { value = BitConverter.ToUInt64(_bytes, _pos); _pos += 8; }
        public void ReadValueSafe(out float value) { value = BitConverter.ToSingle(_bytes, _pos); _pos += 4; }
        public void ReadValueSafe(out bool value) { value = _bytes[_pos] != 0; _pos += 1; }
        public void ReadValueSafe(out string value, bool oneByteChars = false)
        {
            ReadValueSafe(out int len);
            value = System.Text.Encoding.UTF8.GetString(_bytes, _pos, len);
            _pos += len;
        }
        public void Dispose() { }
    }

    public enum ConnectionEvent
    {
        ClientConnected,
        PeerConnected,
        ClientDisconnected,
        PeerDisconnected,
    }

    public struct ConnectionEventData
    {
        public ConnectionEvent EventType;
        public ulong ClientId;
        public IReadOnlyList<ulong> PeerClientIds;
    }

    /// <summary>Tick system (original: <c>NetworkTickSystem</c>). Offline the tick rate is a record.</summary>
    public sealed class NetworkTickSystem
    {
        readonly NetworkManager _nm;
        public NetworkTickSystem(NetworkManager nm) { _nm = nm; }
        public uint TickRate => _nm?.NetworkConfig?.TickRate ?? 30;
        public event Action Tick;
        internal void RaiseTick() => Tick?.Invoke();
    }

    /// <summary>Transport base (original: <c>NetworkTransport</c>). Offline no transport runs.</summary>
    public abstract class NetworkTransport : MonoBehaviour
    {
        public virtual ulong ServerClientId => 0;
        public virtual bool IsSupported => true;
        public event Action<NetworkEvent, ulong, ArraySegment<byte>, float> OnTransportEvent;
        protected void InvokeOnTransportEvent(NetworkEvent evt, ulong clientId, ArraySegment<byte> payload, float time)
            => OnTransportEvent?.Invoke(evt, clientId, payload, time);
        public virtual bool StartClient() => true;
        public virtual bool StartServer() => true;
        public virtual void Shutdown() { }
        public virtual void DisconnectRemoteClient(ulong clientId) { }
        public virtual void DisconnectLocalClient() { }
        public virtual ulong GetCurrentRtt(ulong clientId) => 0;
    }

    public enum NetworkEvent { Data, Connect, Disconnect, TransportFailure, Nothing }

    /// <summary>Universal RPC target set (original: <c>RpcTarget</c>). Offline every target is local.</summary>
    public sealed class RpcTarget
    {
        public BaseRpcTarget Everyone { get; } = new BaseRpcTarget(RpcTargetUse.Everyone);
        public BaseRpcTarget Server { get; } = new BaseRpcTarget(RpcTargetUse.Server);
        public BaseRpcTarget NotServer { get; } = new BaseRpcTarget(RpcTargetUse.NotServer);
        public BaseRpcTarget Owner { get; } = new BaseRpcTarget(RpcTargetUse.Owner);
        public BaseRpcTarget NotOwner { get; } = new BaseRpcTarget(RpcTargetUse.NotOwner);
        public BaseRpcTarget Me { get; } = new BaseRpcTarget(RpcTargetUse.Me);
        public BaseRpcTarget NotMe { get; } = new BaseRpcTarget(RpcTargetUse.NotMe);
        public BaseRpcTarget ClientsAndHost { get; } = new BaseRpcTarget(RpcTargetUse.ClientsAndHost);
        public BaseRpcTarget SpecifiedInParams { get; } = new BaseRpcTarget(RpcTargetUse.SpecifiedInParams);

        public BaseRpcTarget Single(ulong clientId, RpcTargetUse use) => new BaseRpcTarget(use, clientId);
        public BaseRpcTarget Group(IEnumerable<ulong> clientIds, RpcTargetUse use) => new BaseRpcTarget(use);
        public BaseRpcTarget Not(ulong excludedClientId, RpcTargetUse use) => new BaseRpcTarget(use);
    }

    public class BaseRpcTarget
    {
        public RpcTargetUse Use { get; }
        public ulong ClientId { get; }
        public BaseRpcTarget(RpcTargetUse use, ulong clientId = 0) { Use = use; ClientId = clientId; }
    }

    public enum RpcTargetUse
    {
        Temp,
        Persistent,
        Everyone,
        Server,
        NotServer,
        Owner,
        NotOwner,
        Me,
        NotMe,
        ClientsAndHost,
        SpecifiedInParams,
    }

    public enum RpcDelivery { Unreliable, Reliable }
    public enum RpcInvokePermission { Everyone, Owner, Server }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CosmicShore.Engine.Networking.Components;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// The port's replication layer - what Netcode for GameObjects does between a NetworkManager
    /// and its transport. One driver runs per process (there is one NetworkManager singleton):
    ///
    ///  • CONNECTION. A client connects, sends its approval payload; the server runs
    ///    ConnectionApprovalCallback, assigns a client id and answers with the scene it must load.
    ///    The client loads it (message processing paused, as Netcode defers), reports ready, and
    ///    receives a snapshot of every spawned object; its player object is spawned last and the
    ///    connection callbacks fire on both ends.
    ///  • SPAWNS. A server spawn/despawn is broadcast. Dynamic objects are instantiated on the
    ///    client from the registered prefab with the same GlobalObjectIdHash; in-scene objects are
    ///    matched by hash in the loaded scene. NetworkVariable state rides the spawn and is applied
    ///    before OnNetworkSpawn (no change callback, as the original).
    ///  • VARIABLES. Writes honour the write permission; dirty variables are sent at the tick rate,
    ///    owner-read ones only to their owner. An owner-written variable goes client → server → the
    ///    other clients.
    ///  • RPCS. ServerRpc/ClientRpc bodies are intercepted by the rewritten game source
    ///    (<see cref="NetRpc"/>) and delivered here; ServerRpc ownership is enforced server-side.
    ///  • SCENES. A server NetworkSceneManager.LoadScene loads on every client; LoadEventCompleted
    ///    fires when every client reported (or timed out).
    ///  • TRANSFORMS. NetworkTransforms are sent by their authority (server, or the owner for
    ///    client-authoritative ones) and interpolated on everyone else.
    /// Messages reach a client that is still synchronizing only after its snapshot (queued in order).
    /// </summary>
    public static class NetDriver
    {
        /// <summary>Allow a real transport (the player turns it on; headless tests stay single-process).</summary>
        public static bool Enabled { get; set; }

        /// <summary>Opens the transport (TCP unless a test or a backend swaps it). Read at StartServer/StartClient.</summary>
        internal static INetTransportFactory TransportFactory { get; set; } = new TcpTransportFactory();

        /// <summary>Verbose connection/spawn tracing (CS_PORT_TRACE_NET=1).</summary>
        static readonly bool Trace = Environment.GetEnvironmentVariable("CS_PORT_TRACE_NET") == "1";

        enum Msg : byte
        {
            ConnectRequest = 1, ConnectAccept, ConnectReject, SceneReady, Spawn, Despawn, SyncComplete,
            NetVar, Rpc, Ownership, Parent, Transform, Named, SceneLoad, SceneLoadDone, SceneLoadEventCompleted,
            ClientConnected, ClientDisconnected, TimePing, TimePong,
        }

        sealed class ClientConn
        {
            public ulong Id;
            public int Peer;
            public bool Approved, Synced;
            public NetworkManager.ConnectionApprovalResponse Approval;
            public readonly List<byte[]> Pending = new();
        }

        static INetTransport s_sock;
        static NetworkManager s_nm;
        static bool s_server;
        static readonly Dictionary<int, ClientConn> s_byPeer = new();
        static readonly Dictionary<ulong, ClientConn> s_byId = new();
        static ulong s_nextClientId = 1;

        static bool s_clientPaused, s_clientAccepted;
        static byte[] s_connectPayload;

        static readonly HashSet<INetVar> s_dirty = new();
        static readonly Dictionary<INetVar, ulong> s_dirtyOrigin = new();
        static float s_tickAccum;
        static double s_timeBase, s_clientOffset;
        static float s_pingTimer;
        static bool s_serverSceneTransition;

        public static bool IsActive => s_sock != null;
        public static bool IsServer => s_sock != null && s_server;
        public static bool IsClientOnly => s_sock != null && !s_server;
        public static int ListenPort => s_sock != null && s_server ? s_sock.ListenPort : 0;

        static double Now => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

        // ── Lifecycle ───────────────────────────────────────────────

        internal static bool StartServer(NetworkManager nm, string address, int port)
        {
            Stop();
            try { s_sock = TransportFactory.Listen(address, port); }
            catch (Exception)
            {
                try { s_sock = TransportFactory.Listen(address, 0); }
                catch (Exception e) { Console.WriteLine($"[net] listen failed: {e.Message}"); s_sock = null; return false; }
            }
            s_nm = nm;
            s_server = true;
            s_timeBase = Now;
            s_nextClientId = 1;
            Console.WriteLine($"[net] hosting on port {s_sock.ListenPort}");
            return true;
        }

        internal static void StartClient(NetworkManager nm, string address, int port, byte[] payload)
        {
            Stop();
            s_nm = nm;
            s_server = false;
            s_clientAccepted = false;
            s_clientPaused = false;
            s_connectPayload = payload ?? Array.Empty<byte>();
            s_timeBase = Now;
            s_sock = TransportFactory.Connect(address, port, 10000);
            Console.WriteLine($"[net] connecting to {address}:{port}");
        }

        internal static void Stop()
        {
            if (s_sock == null) return;
            s_sock.Dispose();
            s_sock = null;
            s_nm = null;
            s_byPeer.Clear();
            s_byId.Clear();
            s_dirty.Clear();
            s_dirtyOrigin.Clear();
            s_transforms.Clear();
            s_pendingSceneEvents.Clear();
            s_deferred.Clear();
            s_clientPaused = false;
            s_serverSceneTransition = false;
        }

        /// <summary>Server-side kick (NetworkManager.DisconnectClient).</summary>
        internal static void Kick(ulong clientId, string reason)
        {
            if (!IsServer || !s_byId.TryGetValue(clientId, out var c)) return;
            var w = Begin(Msg.ConnectReject); w.Write(reason ?? "kicked"); SendRaw(c.Peer, End());
            s_sock.Disconnect(c.Peer);
        }

        // ── Frame hooks (GameLoop) ──────────────────────────────────

        /// <summary>Early update: deliver everything the transport received, advance the network clock, interpolate.</summary>
        public static void EarlyUpdate()
        {
            if (s_sock == null) return;
            var nm = s_nm;
            if (nm == null || !nm) { Stop(); return; }
            nm.ServerTime = new NetworkTime(ServerNow());
            int guard = 0;
            while (s_sock != null && !s_clientPaused && guard++ < 20000 && s_sock.Poll(out var e))
            {
                try { Handle(e); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            if (s_sock == null) return;
            if (!s_server) ExpireDeferred();
            double now = Now;
            foreach (var nt in s_transforms)
                if (nt != null && nt.IsSpawned && !IsTransformAuthority(nt)) nt.PortInterpolate(now);
            CheckSceneEventTimeouts();
        }

        /// <summary>Post-late update: at the tick rate send dirty variables, authoritative transforms and clock pings.</summary>
        public static void PostLateUpdate()
        {
            if (s_sock == null || s_nm == null) return;
            float tickRate = Math.Max(1u, s_nm.NetworkConfig?.TickRate ?? 30);
            s_tickAccum += Time.unscaledDeltaTime;
            if (s_tickAccum < 1f / tickRate) return;
            s_tickAccum %= 1f / tickRate;
            s_nm.NetworkTickSystem.RaiseTick();
            FlushVariables();
            SendTransforms();
            if (!s_server && s_clientAccepted && (s_pingTimer -= 1f / tickRate) <= 0f)
            {
                s_pingTimer = 2f;
                var w = Begin(Msg.TimePing); w.Write(Now); SendRaw(0, End());
            }
        }

        static double ServerNow() => s_server ? Now - s_timeBase : Now + s_clientOffset;

        // ── Messages ────────────────────────────────────────────────

        static readonly MemoryStream s_ms = new();
        static readonly BinaryWriter s_w = new(s_ms);

        static BinaryWriter Begin(Msg kind)
        {
            s_ms.SetLength(0);
            s_w.Write((byte)kind);
            return s_w;
        }

        static byte[] End() { s_w.Flush(); return s_ms.ToArray(); }

        static void SendRaw(int peer, byte[] bytes) => s_sock?.Send(peer, bytes);

        /// <summary>Server → one client; held until the client has its snapshot.</summary>
        static void ToClient(ClientConn c, byte[] bytes)
        {
            if (c.Synced) SendRaw(c.Peer, bytes);
            else if (c.Approved) c.Pending.Add(bytes);
        }

        static void ToClients(byte[] bytes, ulong except = ulong.MaxValue, bool syncedOnly = false)
        {
            foreach (var c in s_byId.Values)
                if (c.Id != except && (!syncedOnly || c.Synced)) ToClient(c, bytes);
        }

        static void Handle(NetEvent e)
        {
            if (s_server) HandleServer(e);
            else HandleClient(e);
        }

        // ── Server side ─────────────────────────────────────────────

        static void HandleServer(NetEvent e)
        {
            var nm = s_nm;
            switch (e.Kind)
            {
                case NetEventKind.Connected:
                    s_byPeer[e.Peer] = new ClientConn { Peer = e.Peer };
                    return;
                case NetEventKind.Disconnected:
                    if (s_byPeer.Remove(e.Peer, out var gone) && gone.Approved) ServerClientGone(gone);
                    return;
            }
            if (!s_byPeer.TryGetValue(e.Peer, out var c) || e.Payload.Length == 0) return;
            using var r = new BinaryReader(new MemoryStream(e.Payload));
            var kind = (Msg)r.ReadByte();
            if (!c.Approved && kind != Msg.ConnectRequest) return;
            switch (kind)
            {
                case Msg.ConnectRequest: ServerApprove(c, r.ReadBytes(r.ReadInt32())); break;
                case Msg.SceneReady: ServerSynchronize(c); break;
                case Msg.NetVar: ReceiveVariable(r, c.Id); break;
                case Msg.Rpc: NetRpc.Receive(r, c.Id); break;
                case Msg.Transform: ReceiveTransform(r, c.Id); break;
                case Msg.Named: ReceiveNamed(r, c.Id); break;
                case Msg.SceneLoadDone: ServerSceneLoadDone(c.Id, r.ReadInt32()); break;
                case Msg.TimePing:
                {
                    double t = r.ReadDouble();
                    var w = Begin(Msg.TimePong); w.Write(t); w.Write(ServerNow()); SendRaw(c.Peer, End());
                    break;
                }
            }
        }

        static void ServerApprove(ClientConn c, byte[] payload)
        {
            var nm = s_nm;
            ulong id = s_nextClientId++;
            var response = new NetworkManager.ConnectionApprovalResponse { Approved = true, CreatePlayerObject = nm.NetworkConfig?.PlayerPrefab != null };
            if (nm.ConnectionApprovalCallback != null && (nm.NetworkConfig == null || nm.NetworkConfig.ConnectionApproval))
            {
                response = new NetworkManager.ConnectionApprovalResponse();
                try { nm.ConnectionApprovalCallback(new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = id, Payload = payload }, response); }
                catch (Exception ex) { Debug.LogException(ex); response.Approved = false; response.Reason = ex.Message; }
            }
            if (!response.Approved)
            {
                var wr = Begin(Msg.ConnectReject); wr.Write(response.Reason ?? "rejected"); SendRaw(c.Peer, End());
                s_sock.Disconnect(c.Peer);
                return;
            }
            c.Id = id;
            c.Approved = true;
            c.Approval = response;
            s_byId[id] = c;
            if (!nm.ConnectedClientsIds.Contains(id)) nm.ConnectedClientsIds.Add(id);
            if (!nm.ConnectedClients.ContainsKey(id))
            {
                var client = new NetworkClient { ClientId = id };
                nm.ConnectedClients[id] = client;
                nm.ConnectedClientsList.Add(client);
            }
            var w = Begin(Msg.ConnectAccept);
            w.Write(id);
            w.Write(ServerNow());
            w.Write(nm.ConnectedClientsIds.Count);
            foreach (var cid in nm.ConnectedClientsIds) w.Write(cid);
            var scenes = LoadedSceneNames();
            w.Write(scenes.Count);
            foreach (var s in scenes) w.Write(s);
            SendRaw(c.Peer, End());
            if (Trace) Console.WriteLine($"[net] approved client {id} (peer {c.Peer})");
        }

        static List<string> LoadedSceneNames()
        {
            var list = new List<string>();
            var active = SceneManager.GetActiveScene();
            if (active != null && !string.IsNullOrEmpty(active.name)) list.Add(active.name);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != null && s != active && !string.IsNullOrEmpty(s.name) && s.name != "DontDestroyOnLoad") list.Add(s.name);
            }
            return list;
        }

        static void ServerSynchronize(ClientConn c)
        {
            var nm = s_nm;
            var objects = new List<NetworkObject>(nm.SpawnManager.SpawnedObjectsList);
            objects.Sort((a, b) => a.NetworkObjectId.CompareTo(b.NetworkObjectId));
            foreach (var no in objects)
                if (no != null && no.IsSpawned) SendRaw(c.Peer, SpawnMessage(no, c.Id));
            c.Synced = true;
            foreach (var p in c.Pending) SendRaw(c.Peer, p);
            c.Pending.Clear();
            // The player object, then the connection is complete on both ends.
            if (c.Approval != null && c.Approval.CreatePlayerObject) nm.SpawnPlayerObjectFor(c.Id, c.Approval);
            SendRaw(c.Peer, new[] { (byte)Msg.SyncComplete });
            var wc = Begin(Msg.ClientConnected); wc.Write(c.Id); ToClients(End(), except: c.Id);
            if (Trace) Console.WriteLine($"[net] client {c.Id} synchronized ({objects.Count} objects)");
            nm.SceneManager.NotifySynchronized(c.Id);
            nm.NotifyClientConnected(c.Id);
        }

        static void ServerClientGone(ClientConn c)
        {
            var nm = s_nm;
            s_byId.Remove(c.Id);
            if (Trace) Console.WriteLine($"[net] client {c.Id} disconnected");
            // Netcode destroys what the leaving client owned (unless told otherwise).
            foreach (var no in new List<NetworkObject>(nm.SpawnManager.SpawnedObjectsList))
            {
                if (no == null || !no.IsSpawned || no.OwnerClientId != c.Id) continue;
                if (no.DontDestroyWithOwner) no.ChangeOwnership(NetworkManager.ServerClientId);
                else no.Despawn(true);
            }
            nm.ConnectedClientsIds.Remove(c.Id);
            if (nm.ConnectedClients.Remove(c.Id, out var client)) nm.ConnectedClientsList.Remove(client);
            var w = Begin(Msg.ClientDisconnected); w.Write(c.Id); ToClients(End());
            foreach (var ev in s_pendingSceneEvents) ev.Waiting.Remove(c.Id);
            nm.NotifyClientDisconnect(c.Id);
        }

        // ── Client side ─────────────────────────────────────────────

        static void HandleClient(NetEvent e)
        {
            var nm = s_nm;
            switch (e.Kind)
            {
                case NetEventKind.Connected:
                {
                    var w = Begin(Msg.ConnectRequest);
                    w.Write(s_connectPayload.Length);
                    w.Write(s_connectPayload);
                    SendRaw(0, End());
                    return;
                }
                case NetEventKind.Disconnected:
                    ClientLost(string.IsNullOrEmpty(nm.DisconnectReason) ? "disconnected from server" : nm.DisconnectReason);
                    return;
            }
            ClientMessage(e.Payload);
        }

        /// <summary>
        /// A message that names an object this client has not spawned yet (a server RPC or
        /// variable write from the object's own OnNetworkSpawn can overtake its spawn) - held and
        /// replayed when the spawn lands, like Netcode's deferred messages, and dropped with a
        /// warning after the spawn timeout.
        /// </summary>
        sealed class Deferred { public ulong Id; public byte[] Payload; public double Since; }
        static readonly List<Deferred> s_deferred = new();
        const double SpawnTimeoutSeconds = 10;

        static bool DeferIfUnspawned(Msg kind, byte[] payload)
        {
            if (kind != Msg.Rpc && kind != Msg.NetVar && kind != Msg.Ownership && kind != Msg.Parent) return false;
            if (payload.Length < 9) return false;
            ulong id = BitConverter.ToUInt64(payload, 1);
            if (Find(id) != null) return false;
            s_deferred.Add(new Deferred { Id = id, Payload = payload, Since = Now });
            return true;
        }

        static void ReplayDeferred(ulong id)
        {
            if (s_deferred.Count == 0) return;
            List<Deferred> ready = null;
            for (int i = 0; i < s_deferred.Count; i++)
                if (s_deferred[i].Id == id) (ready ??= new()).Add(s_deferred[i]);
            if (ready == null) return;
            s_deferred.RemoveAll(d => d.Id == id);
            foreach (var d in ready)
            {
                try { ClientMessage(d.Payload); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        static void ExpireDeferred()
        {
            if (s_deferred.Count == 0) return;
            double now = Now;
            s_deferred.RemoveAll(d =>
            {
                if (now - d.Since < SpawnTimeoutSeconds) return false;
                Debug.LogWarning($"[Deferred OnSpawn] Deferred messages were received for a trigger of type OnSpawn with key {d.Id}, but that trigger was not received within within {SpawnTimeoutSeconds} second(s).");
                return true;
            });
        }

        static void ClientMessage(byte[] payload)
        {
            var nm = s_nm;
            if (nm == null) return;
            using var r = new BinaryReader(new MemoryStream(payload));
            var kind = (Msg)r.ReadByte();
            if (DeferIfUnspawned(kind, payload)) return;
            switch (kind)
            {
                case Msg.ConnectAccept: ClientAccepted(r); break;
                case Msg.ConnectReject: nm.DisconnectReason = r.ReadString(); break;
                case Msg.Spawn: ClientSpawn(r); break;
                case Msg.Despawn: ClientDespawn(r); break;
                case Msg.SyncComplete:
                    nm.SceneManager.NotifySynchronized(nm.LocalClientId);
                    nm.NotifyClientConnected(nm.LocalClientId);
                    break;
                case Msg.NetVar: ReceiveVariable(r, NetworkManager.ServerClientId); break;
                case Msg.Rpc: NetRpc.Receive(r, NetworkManager.ServerClientId); break;
                case Msg.Ownership:
                {
                    var no = Find(r.ReadUInt64());
                    ulong owner = r.ReadUInt64();
                    if (no != null) no.ApplyRemoteOwnership(owner);
                    break;
                }
                case Msg.Parent: ClientParent(r); break;
                case Msg.Transform: ReceiveTransform(r, NetworkManager.ServerClientId); break;
                case Msg.Named: ReceiveNamed(r, NetworkManager.ServerClientId); break;
                case Msg.SceneLoad: ClientSceneLoad(r.ReadString(), (LoadSceneMode)r.ReadByte(), r.ReadInt32()); break;
                case Msg.SceneLoadEventCompleted: ClientSceneEventCompleted(r); break;
                case Msg.ClientConnected:
                {
                    ulong id = r.ReadUInt64();
                    if (!nm.ConnectedClientsIds.Contains(id)) nm.ConnectedClientsIds.Add(id);
                    nm.NotifyPeerConnected(id);
                    break;
                }
                case Msg.ClientDisconnected:
                {
                    ulong id = r.ReadUInt64();
                    nm.ConnectedClientsIds.Remove(id);
                    nm.NotifyPeerDisconnected(id);
                    break;
                }
                case Msg.TimePong:
                {
                    double sent = r.ReadDouble(), server = r.ReadDouble(), now = Now;
                    s_clientOffset = server + (now - sent) * 0.5 - now;
                    break;
                }
            }
        }

        static void ClientAccepted(BinaryReader r)
        {
            var nm = s_nm;
            s_clientAccepted = true;
            ulong id = r.ReadUInt64();
            double serverTime = r.ReadDouble();
            s_clientOffset = serverTime - Now;
            nm.LocalClientId = id;
            nm.SceneManager.LocalClientId = id;
            nm.ConnectedClientsIds.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) nm.ConnectedClientsIds.Add(r.ReadUInt64());
            if (!nm.ConnectedClientsIds.Contains(id)) nm.ConnectedClientsIds.Add(id);
            int sceneCount = r.ReadInt32();
            var scenes = new List<string>();
            for (int i = 0; i < sceneCount; i++) scenes.Add(r.ReadString());
            if (Trace) Console.WriteLine($"[net] accepted as client {id}; server scenes: {string.Join(", ", scenes)}");
            // Scene synchronization: load the server's scenes, then ask for the snapshot.
            s_clientPaused = true;
            ClientLoadScenes(scenes, 0);
        }

        static void ClientLoadScenes(List<string> scenes, int index)
        {
            if (s_sock == null) return;
            if (index >= scenes.Count)
            {
                s_clientPaused = false;
                SendRaw(0, new[] { (byte)Msg.SceneReady });
                return;
            }
            var mode = index == 0 ? s_nm.SceneManager.ClientSynchronizationMode : LoadSceneMode.Additive;
            var op = SceneManager.LoadSceneAsync(scenes[index], mode);
            if (op == null) { ClientLoadScenes(scenes, index + 1); return; }
            op.completed += _ => ClientLoadScenes(scenes, index + 1);
        }

        static void ClientLost(string reason)
        {
            var nm = s_nm;
            ulong local = nm.LocalClientId;
            nm.DisconnectReason = reason;
            Console.WriteLine($"[net] connection closed: {reason}");
            Stop();
            nm.NotifyClientDisconnect(local);
            if (nm.IsListening) nm.Shutdown();
        }

        // ── Spawning ────────────────────────────────────────────────

        static NetworkObject Find(ulong id)
            => s_nm != null && s_nm.SpawnManager.SpawnedObjects.TryGetValue(id, out var o) ? o : null;

        /// <summary>Server: a NetworkObject finished spawning.</summary>
        internal static void OnServerSpawned(NetworkObject no)
        {
            if (!IsServer) return;
            foreach (var c in s_byId.Values)
                if (c.Synced) SendRaw(c.Peer, SpawnMessage(no, c.Id));
            RegisterTransforms(no);
        }

        internal static void OnServerDespawned(NetworkObject no, bool destroy, bool bySceneUnload)
        {
            UnregisterTransforms(no);
            if (!IsServer || (bySceneUnload && s_serverSceneTransition)) return;
            var w = Begin(Msg.Despawn); w.Write(no.NetworkObjectId); w.Write(destroy);
            ToClients(End(), syncedOnly: true);
        }

        internal static void OnServerOwnershipChanged(NetworkObject no, ulong owner)
        {
            if (!IsServer) return;
            var w = Begin(Msg.Ownership); w.Write(no.NetworkObjectId); w.Write(owner);
            ToClients(End(), syncedOnly: true);
        }

        internal static void OnServerParentChanged(NetworkObject no, NetworkObject parent, bool worldPositionStays)
        {
            if (!IsServer || !no.IsSpawned) return;
            var w = Begin(Msg.Parent);
            w.Write(no.NetworkObjectId);
            w.Write(parent != null && parent.IsSpawned ? parent.NetworkObjectId : 0UL);
            w.Write(worldPositionStays);
            WritePose(w, no.transform);
            ToClients(End(), syncedOnly: true);
        }

        static byte[] SpawnMessage(NetworkObject no, ulong forClient)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((byte)Msg.Spawn);
            w.Write(no.NetworkObjectId);
            w.Write(no.GlobalObjectIdHash);
            bool sceneObject = no.IsSceneObject == true;
            w.Write(sceneObject);
            w.Write(sceneObject ? no.gameObject.scene?.name ?? "" : "");
            w.Write(no.OwnerClientId);
            w.Write(no.IsPlayerObject);
            w.Write(no.DestroyWithScene);
            var parent = no.transform.parent != null ? no.transform.parent.GetComponentInParent<NetworkObject>() : null;
            w.Write(parent != null && parent.IsSpawned ? parent.NetworkObjectId : 0UL);
            w.Write(no.gameObject.activeSelf);
            WritePose(w, no.transform);
            var behaviours = no.SpawnedBehaviours;
            w.Write((ushort)behaviours.Length);
            foreach (var nb in behaviours)
            {
                var vars = NetVarBinding.Of(nb);
                w.Write((ushort)vars.Length);
                foreach (var v in vars)
                {
                    bool readable = v.ReadPerm == NetworkVariableReadPermission.Everyone || no.OwnerClientId == forClient;
                    w.Write(readable);
                    if (!readable) continue;
                    var bytes = StateBytes(v);
                    w.Write(bytes.Length);
                    w.Write(bytes);
                }
            }
            w.Flush();
            return ms.ToArray();
        }

        static byte[] StateBytes(INetVar v)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            v.WriteState(w);
            w.Flush();
            return ms.ToArray();
        }

        static void WritePose(BinaryWriter w, Transform t)
        {
            var p = t.position; var q = t.rotation; var s = t.localScale;
            w.Write(p.x); w.Write(p.y); w.Write(p.z);
            w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
            w.Write(s.x); w.Write(s.y); w.Write(s.z);
        }

        static void ReadPose(BinaryReader r, out Vector3 p, out Quaternion q, out Vector3 s)
        {
            p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            s = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        static void ClientSpawn(BinaryReader r)
        {
            var nm = s_nm;
            ulong id = r.ReadUInt64();
            uint hash = r.ReadUInt32();
            bool sceneObject = r.ReadBoolean();
            string sceneName = r.ReadString();
            ulong owner = r.ReadUInt64();
            bool isPlayer = r.ReadBoolean();
            bool destroyWithScene = r.ReadBoolean();
            ulong parentId = r.ReadUInt64();
            bool active = r.ReadBoolean();
            ReadPose(r, out var pos, out var rot, out var scale);

            if (Find(id) is { } existing && existing) { SkipBehaviourState(r); return; }

            NetworkObject no = sceneObject ? FindSceneObject(hash, sceneName, pos) : null;
            if (no == null)
            {
                var prefab = nm.FindNetworkPrefab(hash);
                if (prefab == null)
                {
                    Debug.LogError($"[Netcode] Failed to create object locally. [globalObjectIdHash={hash}]. NetworkPrefab could not be found. Is the prefab registered with NetworkManager?");
                    SkipBehaviourState(r);
                    return;
                }
                var go = nm.PrefabHandler.Instantiate(prefab, owner, pos, rot) ?? Object.Instantiate(prefab, pos, rot);
                no = go.GetComponent<NetworkObject>() ?? go.AddComponent<NetworkObject>();
            }
            if (parentId != 0 && Find(parentId) is { } parent) no.transform.SetParent(parent.transform, true);
            no.transform.SetPositionAndRotation(pos, rot);
            no.transform.localScale = scale;
            if (no.gameObject.activeSelf != active) no.gameObject.SetActive(active);

            var behaviours = no.CollectBehaviours();
            int count = r.ReadUInt16();
            for (int b = 0; b < count; b++)
            {
                int varCount = r.ReadUInt16();
                var vars = b < behaviours.Length ? NetVarBinding.Bind(behaviours[b]) : Array.Empty<INetVar>();
                for (int v = 0; v < varCount; v++)
                {
                    if (!r.ReadBoolean()) continue;
                    var bytes = r.ReadBytes(r.ReadInt32());
                    if (v < vars.Length)
                        using (var vr = new BinaryReader(new MemoryStream(bytes)))
                            vars[v].ReadState(vr, notify: false);
                }
            }
            if (Trace) Console.WriteLine($"[net] spawn #{id} '{no.name}' owner={owner} player={isPlayer}");
            no.SpawnRemote(id, owner, isPlayer, destroyWithScene);
            RegisterTransforms(no);
            ReplayDeferred(id);
        }

        static void SkipBehaviourState(BinaryReader r)
        {
            int count = r.ReadUInt16();
            for (int b = 0; b < count; b++)
            {
                int varCount = r.ReadUInt16();
                for (int v = 0; v < varCount; v++)
                    if (r.ReadBoolean()) r.ReadBytes(r.ReadInt32());
            }
        }

        static NetworkObject FindSceneObject(uint hash, string sceneName, Vector3 pos)
        {
            NetworkObject best = null;
            float bestD = float.MaxValue;
            foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (no == null || no.IsSpawned || no.GlobalObjectIdHash != hash) continue;
                if (!string.IsNullOrEmpty(sceneName) && no.gameObject.scene?.name != sceneName) continue;
                float d = (no.transform.position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = no; }
            }
            return best;
        }

        static void ClientDespawn(BinaryReader r)
        {
            var no = Find(r.ReadUInt64());
            bool destroy = r.ReadBoolean();
            if (no == null) return;
            UnregisterTransforms(no);
            no.DespawnRemote(destroy || no.IsSceneObject != true);
        }

        static void ClientParent(BinaryReader r)
        {
            var no = Find(r.ReadUInt64());
            ulong parentId = r.ReadUInt64();
            bool stays = r.ReadBoolean();
            ReadPose(r, out var p, out var q, out var s);
            if (no == null) return;
            var parent = parentId != 0 ? Find(parentId) : null;
            no.transform.SetParent(parent != null ? parent.transform : null, true);
            no.transform.SetPositionAndRotation(p, q);
            no.transform.localScale = s;
            foreach (var nb in no.SpawnedBehaviours) nb.OnNetworkObjectParentChanged(parent);
        }

        // ── Variables ───────────────────────────────────────────────

        internal static bool CanWrite(INetVar v)
        {
            if (s_sock == null) return true;
            var nb = v.Behaviour;
            if (nb == null || !nb.IsSpawned) return true;
            return v.WritePerm == NetworkVariableWritePermission.Server ? nb.IsServer : nb.IsOwner;
        }

        static readonly HashSet<string> s_permReported = new();

        internal static void ReportWritePermission(INetVar v)
        {
            var nb = v.Behaviour;
            string key = $"{nb?.GetType().Name}#{v.Index}";
            if (s_permReported.Add(key))
                Debug.LogError($"[Netcode] Client is not allowed to write to this NetworkVariable ({nb?.GetType().Name}, variable {v.Index}, write permission {v.WritePerm}).");
        }

        internal static void MarkDirty(INetVar v, ulong origin = ulong.MaxValue)
        {
            if (s_sock == null) { v.ClearDelta(); return; }
            s_dirty.Add(v);
            if (origin != ulong.MaxValue) s_dirtyOrigin[v] = origin;
        }

        static readonly List<INetVar> s_flush = new();

        static void FlushVariables()
        {
            if (s_dirty.Count == 0) return;
            s_flush.Clear();
            s_flush.AddRange(s_dirty);
            s_dirty.Clear();
            foreach (var v in s_flush)
            {
                var nb = v.Behaviour;
                if (nb == null || !nb || !nb.IsSpawned) { v.ClearDelta(); continue; }
                ulong origin = s_dirtyOrigin.Remove(v, out var o) ? o : ulong.MaxValue;
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write((byte)Msg.NetVar);
                w.Write(nb.NetworkObjectId);
                w.Write(NetObjects.BehaviourIndex(nb));
                w.Write((ushort)v.Index);
                v.WriteDelta(w);
                w.Flush();
                var bytes = ms.ToArray();
                v.ClearDelta();
                if (s_server)
                {
                    foreach (var c in s_byId.Values)
                    {
                        if (c.Id == origin) continue;
                        if (v.ReadPerm == NetworkVariableReadPermission.Owner && nb.OwnerClientId != c.Id) continue;
                        if (c.Synced) SendRaw(c.Peer, bytes);
                    }
                }
                else if (v.WritePerm == NetworkVariableWritePermission.Owner && nb.IsOwner)
                    SendRaw(0, bytes);
            }
        }

        static void ReceiveVariable(BinaryReader r, ulong sender)
        {
            var nb = NetObjects.Behaviour(r.ReadUInt64(), r.ReadUInt16());
            int index = r.ReadUInt16();
            if (nb == null) return;
            var vars = NetVarBinding.Of(nb);
            if (index >= vars.Length) return;
            var v = vars[index];
            if (s_server && (v.WritePerm != NetworkVariableWritePermission.Owner || nb.OwnerClientId != sender))
            {
                Debug.LogWarning($"[Netcode] client {sender} wrote {nb.GetType().Name} variable {index} without permission; dropped.");
                return;
            }
            v.ReadDelta(r);
            if (s_server) MarkDirty(v, sender); // relay to the other clients
        }

        // ── RPC transport (NetRpc builds and consumes the payload) ──

        internal static void SendRpcToServer(byte[] payload) { if (IsClientOnly) SendRaw(0, payload); }

        internal static void SendRpcToClients(byte[] payload, IReadOnlyList<ulong> targets)
        {
            if (!IsServer) return;
            if (targets == null) { ToClients(payload); return; }
            foreach (var id in targets)
                if (s_byId.TryGetValue(id, out var c)) ToClient(c, payload);
        }

        internal static byte RpcKindByte => (byte)Msg.Rpc;

        // ── Named messages ──────────────────────────────────────────

        internal static bool SendNamed(string name, ulong clientId, byte[] body, bool toAll)
        {
            if (s_sock == null) return false;
            var w = Begin(Msg.Named); w.Write(name); w.Write(body.Length); w.Write(body);
            var bytes = End();
            if (!s_server) SendRaw(0, bytes);
            else if (toAll) ToClients(bytes);
            else if (s_byId.TryGetValue(clientId, out var c)) ToClient(c, bytes);
            return true;
        }

        static void ReceiveNamed(BinaryReader r, ulong sender)
        {
            string name = r.ReadString();
            var body = r.ReadBytes(r.ReadInt32());
            s_nm?.CustomMessagingManager.Deliver(name, sender, body);
        }

        // ── Scenes ──────────────────────────────────────────────────

        sealed class SceneEventState
        {
            public int Id;
            public string Scene;
            public LoadSceneMode Mode;
            public readonly HashSet<ulong> Waiting = new();
            public readonly List<ulong> Done = new();
            public bool LocalDone;
            public double Deadline;
        }

        static readonly List<SceneEventState> s_pendingSceneEvents = new();
        static int s_nextSceneEvent = 1;

        /// <summary>Server NetworkSceneManager.LoadScene: every synchronized client loads too.</summary>
        internal static void ServerLoadScene(NetworkSceneManager sm, string sceneName, LoadSceneMode mode)
        {
            var ev = new SceneEventState { Id = s_nextSceneEvent++, Scene = sceneName, Mode = mode, Deadline = Now + 60 };
            foreach (var c in s_byId.Values) if (c.Synced) ev.Waiting.Add(c.Id);
            s_pendingSceneEvents.Add(ev);
            var w = Begin(Msg.SceneLoad); w.Write(sceneName); w.Write((byte)mode); w.Write(ev.Id);
            ToClients(End(), syncedOnly: true);
            if (mode == LoadSceneMode.Single) s_serverSceneTransition = true;
            ulong serverId = NetworkManager.ServerClientId;
            var op = SceneManager.LoadSceneAsync(sceneName, mode);
            sm.RaiseLoad(serverId, sceneName, mode, op);
            op.completed += _ =>
            {
                s_serverSceneTransition = false;
                s_nm?.SpawnInSceneObjects();
                sm.RaiseLoadComplete(serverId, sceneName, mode);
                ev.Done.Add(serverId);
                ev.LocalDone = true;
                TryCompleteSceneEvent(sm, ev);
            };
        }

        static void ServerSceneLoadDone(ulong client, int eventId)
        {
            foreach (var ev in s_pendingSceneEvents)
            {
                if (ev.Id != eventId) continue;
                if (ev.Waiting.Remove(client)) ev.Done.Add(client);
                s_nm.SceneManager.RaiseLoadComplete(client, ev.Scene, ev.Mode);
                TryCompleteSceneEvent(s_nm.SceneManager, ev);
                return;
            }
        }

        static void CheckSceneEventTimeouts()
        {
            if (!s_server || s_pendingSceneEvents.Count == 0) return;
            double now = Now;
            foreach (var ev in s_pendingSceneEvents.ToArray())
                if (ev.LocalDone && now > ev.Deadline) { TryCompleteSceneEvent(s_nm.SceneManager, ev, force: true); }
                else if (ev.LocalDone && ev.Waiting.Count == 0) TryCompleteSceneEvent(s_nm.SceneManager, ev);
        }

        static void TryCompleteSceneEvent(NetworkSceneManager sm, SceneEventState ev, bool force = false)
        {
            if (!ev.LocalDone || (ev.Waiting.Count > 0 && !force)) return;
            if (!s_pendingSceneEvents.Remove(ev)) return;
            var timedOut = new List<ulong>(ev.Waiting);
            sm.RaiseLoadEventCompleted(NetworkManager.ServerClientId, ev.Scene, ev.Mode, ev.Done, timedOut);
            var w = Begin(Msg.SceneLoadEventCompleted);
            w.Write(ev.Scene); w.Write((byte)ev.Mode);
            w.Write(ev.Done.Count); foreach (var id in ev.Done) w.Write(id);
            w.Write(timedOut.Count); foreach (var id in timedOut) w.Write(id);
            ToClients(End(), syncedOnly: true);
        }

        static void ClientSceneLoad(string sceneName, LoadSceneMode mode, int eventId)
        {
            var sm = s_nm.SceneManager;
            ulong local = s_nm.LocalClientId;
            s_clientPaused = true;
            var op = SceneManager.LoadSceneAsync(sceneName, mode);
            sm.RaiseLoad(local, sceneName, mode, op);
            op.completed += _ =>
            {
                if (s_sock == null) return;
                sm.RaiseLoadComplete(local, sceneName, mode);
                s_clientPaused = false;
                var w = Begin(Msg.SceneLoadDone); w.Write(eventId); SendRaw(0, End());
            };
        }

        static void ClientSceneEventCompleted(BinaryReader r)
        {
            string scene = r.ReadString();
            var mode = (LoadSceneMode)r.ReadByte();
            var done = new List<ulong>(); int n = r.ReadInt32(); for (int i = 0; i < n; i++) done.Add(r.ReadUInt64());
            var timedOut = new List<ulong>(); n = r.ReadInt32(); for (int i = 0; i < n; i++) timedOut.Add(r.ReadUInt64());
            s_nm.SceneManager.RaiseLoadEventCompleted(s_nm.LocalClientId, scene, mode, done, timedOut);
        }

        // ── Transforms ──────────────────────────────────────────────

        static readonly List<NetworkTransform> s_transforms = new();

        static void RegisterTransforms(NetworkObject no)
        {
            foreach (var nb in no.SpawnedBehaviours)
                if (nb is NetworkTransform nt && !s_transforms.Contains(nt)) s_transforms.Add(nt);
        }

        static void UnregisterTransforms(NetworkObject no)
        {
            foreach (var nb in no.SpawnedBehaviours)
                if (nb is NetworkTransform nt) s_transforms.Remove(nt);
        }

        public static bool IsTransformAuthority(NetworkTransform nt)
            => nt.IsServerAuthoritative() ? nt.IsServer : nt.IsOwner;

        static void SendTransforms()
        {
            for (int i = s_transforms.Count - 1; i >= 0; i--)
            {
                var nt = s_transforms[i];
                if (nt == null || !nt) { s_transforms.RemoveAt(i); continue; }
                if (!nt.IsSpawned || !IsTransformAuthority(nt)) continue;
                if (!nt.PortTakeOutgoing(out var p, out var q, out var s, out bool teleport)) continue;
                var w = Begin(Msg.Transform);
                w.Write(nt.NetworkObjectId);
                w.Write(NetObjects.BehaviourIndex(nt));
                w.Write(teleport);
                w.Write(p.x); w.Write(p.y); w.Write(p.z);
                w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
                w.Write(s.x); w.Write(s.y); w.Write(s.z);
                var bytes = End();
                if (s_server)
                {
                    foreach (var c in s_byId.Values)
                        if (c.Synced && (nt.IsServerAuthoritative() || c.Id != nt.OwnerClientId)) SendRaw(c.Peer, bytes);
                }
                else SendRaw(0, bytes);
            }
        }

        static void ReceiveTransform(BinaryReader r, ulong sender)
        {
            ulong id = r.ReadUInt64();
            ushort idx = r.ReadUInt16();
            bool teleport = r.ReadBoolean();
            var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var s = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if (NetObjects.Behaviour(id, idx) is not NetworkTransform nt) return;
            if (s_server)
            {
                // Only the owner may drive a client-authoritative transform.
                if (nt.IsServerAuthoritative() || nt.OwnerClientId != sender) return;
                nt.PortPushState(Now, p, q, s, teleport);
                var w = Begin(Msg.Transform);
                w.Write(id); w.Write(idx); w.Write(teleport);
                w.Write(p.x); w.Write(p.y); w.Write(p.z);
                w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
                w.Write(s.x); w.Write(s.y); w.Write(s.z);
                ToClients(End(), except: sender, syncedOnly: true);
            }
            else if (!IsTransformAuthority(nt)) nt.PortPushState(Now, p, q, s, teleport);
        }

        internal static double LocalNow => Now;
    }

    /// <summary>A NetworkBehaviour's replicated fields, discovered once per type and bound per instance.</summary>
    internal static class NetVarBinding
    {
        static readonly Dictionary<Type, FieldInfo[]> s_fields = new();
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NetworkBehaviour, INetVar[]> s_bound = new();

        static FieldInfo[] Fields(Type t)
        {
            if (s_fields.TryGetValue(t, out var f)) return f;
            var list = new List<FieldInfo>();
            var chain = new List<Type>();
            for (var tt = t; tt != null && tt != typeof(NetworkBehaviour); tt = tt.BaseType) chain.Insert(0, tt);
            foreach (var tt in chain)
            {
                var own = new List<FieldInfo>();
                foreach (var fi in tt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(INetVar).IsAssignableFrom(fi.FieldType)) own.Add(fi);
                own.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                list.AddRange(own);
            }
            return s_fields[t] = list.ToArray();
        }

        /// <summary>Bind (idempotent) and return the behaviour's variables in wire order.</summary>
        public static INetVar[] Bind(NetworkBehaviour nb)
        {
            if (s_bound.TryGetValue(nb, out var existing)) return existing;
            var fields = Fields(nb.GetType());
            var vars = new List<INetVar>(fields.Length);
            foreach (var f in fields)
            {
                if (f.GetValue(nb) is INetVar v)
                {
                    v.Bind(nb, vars.Count);
                    vars.Add(v);
                }
            }
            var arr = vars.ToArray();
            s_bound.AddOrUpdate(nb, arr);
            return arr;
        }

        public static INetVar[] Of(NetworkBehaviour nb) => Bind(nb);
    }

    /// <summary>Addressing: a behaviour is (its object's network id, its index among the object's behaviours).</summary>
    internal static class NetObjects
    {
        public static ushort BehaviourIndex(NetworkBehaviour nb)
        {
            var list = nb.NetworkObject.SpawnedBehaviours;
            for (int i = 0; i < list.Length; i++) if (ReferenceEquals(list[i], nb)) return (ushort)i;
            return 0;
        }

        public static NetworkBehaviour Behaviour(ulong objectId, ushort index)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.SpawnManager.SpawnedObjects.TryGetValue(objectId, out var no) || no == null) return null;
            var list = no.SpawnedBehaviours;
            return index < list.Length ? list[index] : null;
        }
    }
}

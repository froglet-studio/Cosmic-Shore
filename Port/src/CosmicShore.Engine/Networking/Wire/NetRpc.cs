using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// RPC routing - the runtime half of what Netcode's IL post-processor weaves into every
    /// [ServerRpc]/[ClientRpc] method. The port's source sync rewrites each RPC body to begin with
    /// <c>if (NetRpc.Intercept(this, "Name", new object[] { args })) return;</c>:
    ///
    ///  • a ServerRpc called on a client is sent to the server and not run locally; on the server
    ///    (host) it runs directly - once the ownership requirement holds;
    ///  • a ClientRpc called on the server is sent to every remote target (ClientRpcParams, else
    ///    every client) and runs locally only when the host is itself a target; a client may not
    ///    invoke one;
    ///  • received RPCs are invoked through the same method with interception bypassed, a
    ///    ServerRpcParams argument carrying the sender.
    /// With no transport running every RPC runs locally, the single-process behaviour.
    /// </summary>
    public static class NetRpc
    {
        sealed class Info
        {
            public MethodInfo Method;
            public bool IsServerRpc;
            public bool RequireOwnership;
            public ParameterInfo[] Params;
        }

        static readonly Dictionary<(Type, string), Info> s_info = new();
        static readonly HashSet<string> s_warned = new();

        [ThreadStatic] static object s_bypassTarget;
        [ThreadStatic] static string s_bypassName;

        static Info Lookup(Type t, string name)
        {
            if (s_info.TryGetValue((t, name), out var info)) return info;
            MethodInfo m = null;
            for (var tt = t; tt != null && m == null; tt = tt.BaseType)
                m = tt.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (m != null)
            {
                var srv = m.GetCustomAttribute<ServerRpcAttribute>();
                info = new Info
                {
                    Method = m,
                    IsServerRpc = srv != null || !name.EndsWith("ClientRpc", StringComparison.Ordinal),
                    RequireOwnership = srv?.RequireOwnership ?? false,
                    Params = m.GetParameters(),
                };
            }
            s_info[(t, name)] = info;
            return info;
        }

        /// <summary>True when the call was routed away and the local body must not run.</summary>
        public static bool Intercept(NetworkBehaviour nb, string method, object[] args)
        {
            if (s_bypassTarget != null && ReferenceEquals(s_bypassTarget, nb) && s_bypassName == method)
            {
                s_bypassTarget = null;
                s_bypassName = null;
                return false;
            }
            if (!NetDriver.IsActive || nb == null || !nb.IsSpawned) return false;
            var info = Lookup(nb.GetType(), method);
            if (info == null) return false;
            var nm = NetworkManager.Singleton;

            if (info.IsServerRpc)
            {
                if (NetDriver.IsServer)
                {
                    if (info.RequireOwnership && nb.OwnerClientId != nm.LocalClientId)
                    {
                        WarnOnce($"own:{method}", $"[Netcode] Only the owner can invoke a ServerRpc that requires ownership! ({nb.GetType().Name}.{method})");
                        return true;
                    }
                    return false;
                }
                if (info.RequireOwnership && !nb.IsOwner)
                {
                    WarnOnce($"own:{method}", $"[Netcode] Only the owner can invoke a ServerRpc that requires ownership! ({nb.GetType().Name}.{method})");
                    return true;
                }
                NetDriver.SendRpcToServer(Build(nb, info, method, args));
                return true;
            }

            if (!NetDriver.IsServer)
            {
                WarnOnce($"cli:{method}", $"[Netcode] Only the server can invoke a ClientRpc ({nb.GetType().Name}.{method}).");
                return true;
            }
            IReadOnlyList<ulong> targets = null;
            foreach (var a in args)
                if (a is ClientRpcParams p && p.Send.TargetClientIds != null) { targets = p.Send.TargetClientIds; break; }
            ulong local = nm.LocalClientId;
            List<ulong> remote = null;
            bool localTargeted = nm.IsClient;
            if (targets != null)
            {
                remote = new List<ulong>();
                localTargeted = false;
                foreach (var id in targets)
                {
                    if (id == local) localTargeted = nm.IsClient;
                    else remote.Add(id);
                }
            }
            NetDriver.SendRpcToClients(Build(nb, info, method, args), remote);
            return !localTargeted;
        }

        static byte[] Build(NetworkBehaviour nb, Info info, string method, object[] args)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(NetDriver.RpcKindByte);
            w.Write(nb.NetworkObjectId);
            w.Write(NetObjects.BehaviourIndex(nb));
            w.Write(method);
            for (int i = 0; i < info.Params.Length && i < args.Length; i++)
            {
                var pt = info.Params[i].ParameterType;
                if (pt == typeof(ServerRpcParams) || pt == typeof(ClientRpcParams)) continue;
                NetWire.Write(w, pt, args[i]);
            }
            w.Flush();
            return ms.ToArray();
        }

        /// <summary>An RPC arrived from <paramref name="sender"/> (payload after the kind byte).</summary>
        internal static void Receive(BinaryReader r, ulong sender)
        {
            ulong objectId = r.ReadUInt64();
            ushort index = r.ReadUInt16();
            string method = r.ReadString();
            var nb = NetObjects.Behaviour(objectId, index);
            if (nb == null)
            {
                WarnOnce($"miss:{method}", $"[Netcode] RPC {method} for object #{objectId} arrived before the object exists; dropped.");
                return;
            }
            var info = Lookup(nb.GetType(), method);
            if (info == null) return;
            if (info.IsServerRpc)
            {
                if (!NetDriver.IsServer) return;
                if (info.RequireOwnership && nb.OwnerClientId != sender)
                {
                    WarnOnce($"own:{method}", $"[Netcode] client {sender} invoked {method} without owning the object; dropped.");
                    return;
                }
            }
            var args = new object[info.Params.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var pt = info.Params[i].ParameterType;
                if (pt == typeof(ServerRpcParams))
                    args[i] = new ServerRpcParams { Receive = new ServerRpcReceiveParams { SenderClientId = sender } };
                else if (pt == typeof(ClientRpcParams))
                    args[i] = default(ClientRpcParams);
                else
                    args[i] = NetWire.Read(r, pt);
            }
            s_bypassTarget = nb;
            s_bypassName = method;
            try { info.Method.Invoke(nb, args); }
            catch (TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); }
            finally { s_bypassTarget = null; s_bypassName = null; }
        }

        static void WarnOnce(string key, string message)
        {
            if (s_warned.Add(key)) Debug.LogWarning(message);
        }
    }
}

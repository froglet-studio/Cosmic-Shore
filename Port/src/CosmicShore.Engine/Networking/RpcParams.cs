using System.Collections.Generic;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Original-contract RPC parameter structs (engine addition for the vessel-initializer
    /// arc). RPCs carry local-invoke semantics until the transport phase, so these are pure
    /// data: ported call sites construct targeted <see cref="ClientRpcParams"/>
    /// (<c>new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = … } }</c>)
    /// and read <c>rpcParams.Receive.SenderClientId</c> from a defaulted
    /// <see cref="ServerRpcParams"/> — 0, the host, matching single-process host-mode.
    /// </summary>
    public struct ClientRpcSendParams
    {
        public IReadOnlyList<ulong> TargetClientIds;
    }

    public struct ClientRpcParams
    {
        public ClientRpcSendParams Send;
    }

    public struct ServerRpcReceiveParams
    {
        public ulong SenderClientId;
    }

    public struct ServerRpcParams
    {
        public ServerRpcReceiveParams Receive;
    }
}

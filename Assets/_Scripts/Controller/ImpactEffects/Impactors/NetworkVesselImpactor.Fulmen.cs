using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// FULMEN's wire (<see cref="FulmenNetSync"/>): the server's strike, child and burn messages ride the collector
    /// vessel's impactor, already on every vessel prefab, so no new NetworkObject or prefab change is needed. All three are
    /// reliable and share the object's ordered channel, so a client always sees the strike before its children and burns.
    /// The host receives its own broadcasts and ignores them (it ran the strike).
    /// </summary>
    public partial class NetworkVesselImpactor
    {
        // Server-authoritative Fulmen charge (crystals toward the next bolt): survives a late join; a client HUD reads it.
        readonly NetworkVariable<byte> _fulmenCharge = new(0);

        /// <summary>Crystals this vessel holds toward its next Fulmen (server-authoritative).</summary>
        public int FulmenCharge => _fulmenCharge.Value;

        /// <summary>SERVER: set the replicated charge.</summary>
        public void SetFulmenCharge(int n)
        {
            if (!IsServer || !IsSpawned) return;
            byte b = (byte)UnityEngine.Mathf.Clamp(n, 0, 255);
            if (_fulmenCharge.Value != b) _fulmenCharge.Value = b;
        }

        internal void SendFulmenStrike(FulmenNetSync.StrikeMsg m) => FulmenStrike_ClientRpc(m);
        internal void SendFulmenChild(FulmenNetSync.ChildMsg m) => FulmenChild_ClientRpc(m);
        internal void SendFulmenBurns(FulmenNetSync.BurnBatch b) => FulmenBurns_ClientRpc(b);

        [ClientRpc]
        void FulmenStrike_ClientRpc(FulmenNetSync.StrikeMsg m)
        {
            if (IsServer) return;
            FulmenNetSync.ApplyStrike(this, m);
        }

        [ClientRpc]
        void FulmenChild_ClientRpc(FulmenNetSync.ChildMsg m)
        {
            if (IsServer) return;
            FulmenNetSync.ApplyChild(m);
        }

        [ClientRpc]
        void FulmenBurns_ClientRpc(FulmenNetSync.BurnBatch b)
        {
            if (IsServer) return;
            FulmenNetSync.ApplyBurns(b);
        }
    }
}

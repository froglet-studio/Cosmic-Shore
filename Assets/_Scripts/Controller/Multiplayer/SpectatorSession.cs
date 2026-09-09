using System;
using System.Collections.Generic;
using System.Text;
using CosmicShore.ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The SPECTATOR contract between a viewing client and the host it watches.
    ///
    /// <para><b>A spectator is a Netcode client with NO Player object.</b> It joins the match's
    /// Relay session (the party session - <c>MultiplayerSetup</c> reuses it at launch), and
    /// announces itself in the one place the host can hear it synchronously: the connection
    /// approval payload (<see cref="NetworkConfig.ConnectionData"/>, read back as
    /// <c>ConnectionApprovalRequest.Payload</c>). The host answers with
    /// <c>CreatePlayerObject = false</c>, so nothing downstream - the vessel spawner, the arena
    /// roster, the ready gate, the scoreboard, the AI domain balance - ever sees a pilot that is
    /// not there. That is deliberately the ONE shape: a spectator Player with a null vessel
    /// would have to be filtered out of every consumer of <c>gameData.Players</c>, and the
    /// first consumer nobody remembered would be the one that threw.</para>
    ///
    /// <para>Everything the viewing machine needs beyond that it gets for free: Netcode
    /// synchronises every Player and vessel NetworkObject to it, and the scene
    /// <c>ClientPlayerVesselInitializer</c>'s client-pull roster request initialises every pair
    /// (materials, domain paint, HUD-less). What it does NOT get is <c>OnClientReady</c> - that
    /// event means "the LOCAL vessel is initialised" - so <see cref="SpectatorController"/>
    /// owns the veil fade and the success gate instead.</para>
    ///
    /// <para>Server side, the approved spectator client ids are kept here so the two ready gates
    /// that count <c>ConnectedClientsIds</c> as "humans who must press Ready" can subtract them
    /// (<see cref="CountHumanClients"/>). Cleared whenever this machine starts a server, because
    /// Netcode client ids restart from 1 per server session.</para>
    ///
    /// Record: Docs/PartySystem/SPECTATOR.md.
    /// </summary>
    public static class SpectatorSession
    {
        /// <summary>
        /// Versioned so a future payload format can be told apart from this one; a mismatch is
        /// simply "not a spectator", never an error.
        /// </summary>
        public const string ApprovalPayloadToken = "cosmicshore.spectator.v1";

        static readonly byte[] PayloadBytes = Encoding.UTF8.GetBytes(ApprovalPayloadToken);
        static readonly HashSet<ulong> ServerSpectatorClientIds = new();

        /// <summary>True while THIS machine is (or is becoming) a spectator.</summary>
        public static bool IsLocalSpectator { get; private set; }

        /// <summary>The pilot whose match this machine is spectating (the row that was clicked).</summary>
        public static PartyPlayerData Target { get; private set; }

        /// <summary>Raised on the local machine whenever <see cref="IsLocalSpectator"/> changes.</summary>
        public static event Action LocalSpectatorChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain-reload-off safety: statics survive a play-mode exit in the editor.
            IsLocalSpectator = false;
            Target = default;
            LocalSpectatorChanged = null;
            ServerSpectatorClientIds.Clear();
        }

        // ── Local (viewer) side ──────────────────────────────────────────────

        /// <summary>
        /// Enter the spectator role BEFORE the session join: arms the approval payload the
        /// host reads when this client connects. Idempotent.
        /// </summary>
        public static void BeginLocal(PartyPlayerData target)
        {
            Target = target;
            ArmApprovalPayload();
            if (IsLocalSpectator) return;
            IsLocalSpectator = true;
            LocalSpectatorChanged?.Invoke();
        }

        /// <summary>
        /// Leave the spectator role and DISARM the payload. Called by every exit - the
        /// overlay's close, the watched match ending, host loss, a failed join - and it must
        /// run before this machine next starts a host, or the solo session would present the
        /// spectator payload to itself. Idempotent.
        /// </summary>
        public static void EndLocal()
        {
            ClearApprovalPayload();
            Target = default;
            if (!IsLocalSpectator) return;
            IsLocalSpectator = false;
            LocalSpectatorChanged?.Invoke();
        }

        /// <summary>Write the spectator token into the NetworkManager's connection payload.</summary>
        public static void ArmApprovalPayload()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null) return;
            nm.NetworkConfig.ConnectionData = (byte[])PayloadBytes.Clone();
        }

        /// <summary>Remove the spectator token from the connection payload if it is the one armed.</summary>
        public static void ClearApprovalPayload()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null) return;
            if (IsSpectatorPayload(nm.NetworkConfig.ConnectionData))
                nm.NetworkConfig.ConnectionData = Array.Empty<byte>();
        }

        /// <summary>True when an approval payload carries the spectator token.</summary>
        public static bool IsSpectatorPayload(byte[] payload)
        {
            if (payload == null || payload.Length != PayloadBytes.Length) return false;
            for (int i = 0; i < payload.Length; i++)
                if (payload[i] != PayloadBytes[i]) return false;
            return true;
        }

        // ── Server (host) side ───────────────────────────────────────────────

        /// <summary>Record an approved spectator connection. Server only.</summary>
        public static void ServerRegister(ulong clientId) => ServerSpectatorClientIds.Add(clientId);

        /// <summary>Forget a spectator that disconnected. Server only; no-op for a non-spectator.</summary>
        public static void ServerUnregister(ulong clientId) => ServerSpectatorClientIds.Remove(clientId);

        /// <summary>Forget every spectator - call when this machine STARTS a server (ids restart).</summary>
        public static void ServerClear() => ServerSpectatorClientIds.Clear();

        /// <summary>True when <paramref name="clientId"/> connected to this server as a spectator.</summary>
        public static bool IsSpectatorClient(ulong clientId) => ServerSpectatorClientIds.Contains(clientId);

        /// <summary>
        /// The connected clients that are PLAYERS: <c>ConnectedClientsIds.Count</c> minus the
        /// spectators still connected. This is the number a ready gate must wait for - a
        /// viewer has no Ready button and would otherwise hold the whole match at the line.
        /// Zero when there is no listening server.
        /// </summary>
        public static int CountHumanClients(NetworkManager nm)
        {
            if (nm == null || !nm.IsListening) return 0;
            int humans = 0;
            var ids = nm.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
                if (!ServerSpectatorClientIds.Contains(ids[i])) humans++;
            return humans;
        }

        /// <summary>Spectators currently connected to this server (registered AND still connected).</summary>
        public static int CountConnectedSpectators(NetworkManager nm)
        {
            if (nm == null || !nm.IsListening) return 0;
            int count = 0;
            var ids = nm.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
                if (ServerSpectatorClientIds.Contains(ids[i])) count++;
            return count;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Services.Multiplayer;
using UnityEngine;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    /// <summary>
    /// Backward-compatible facade that delegates to <see cref="HostConnectionService"/>
    /// and <see cref="HostConnectionDataSO"/>.
    ///
    /// Existing code that references PartyManager.Instance, OnlinePlayerInfo, PartyInvite,
    /// or the C# events will continue to compile.  New code should prefer the SOAP data
    /// container (<see cref="HostConnectionDataSO"/>) directly.
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Legacy types kept for backward compat
        // ─────────────────────────────────────────────────────────────────────

        [Serializable]
        public struct OnlinePlayerInfo
        {
            public string PlayerId;
            public string DisplayName;
            public int AvatarId;

            public static OnlinePlayerInfo FromSOAP(PartyPlayerData d) =>
                new() { PlayerId = d.PlayerId, DisplayName = d.DisplayName, AvatarId = d.AvatarId };
        }

        [Serializable]
        public struct PartyInvite
        {
            public string HostPlayerId;
            public string PartySessionId;
            public string HostDisplayName;
            public int HostAvatarId;

            public static PartyInvite FromSOAP(PartyInviteData d) =>
                new() { HostPlayerId = d.HostPlayerId, PartySessionId = d.PartySessionId,
                         HostDisplayName = d.HostDisplayName, HostAvatarId = d.HostAvatarId };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Static access
        // ─────────────────────────────────────────────────────────────────────

        public static PartyManager Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Legacy events (bridged from SOAP)
        // ─────────────────────────────────────────────────────────────────────

#pragma warning disable CS0067 // Legacy backward-compat events bridged from SOAP
        public event Action<IReadOnlyList<OnlinePlayerInfo>> OnOnlinePlayersUpdated;
        public event Action<PartyInvite> OnInviteReceived;
        public event Action<string> OnJoinedParty;
        public event Action<string> OnPartyMemberJoined;
#pragma warning restore CS0067

        // ─────────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("SOAP Data Container")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("Auth (Source of Truth)")]
        [SerializeField] AuthenticationDataVariable authenticationDataVariable;

        [Inject] private PlayerDataService playerDataService;

        // ─────────────────────────────────────────────────────────────────────
        // Public state (delegates to connectionData)
        // ─────────────────────────────────────────────────────────────────────

        public bool IsInPresenceLobby => connectionData != null && connectionData.IsConnected;
        public bool IsHost => connectionData != null && connectionData.IsHost;
        public ISession PartySession => HostConnectionService.Instance?.PartySession;
        public ISession PresenceLobby => null; // Session ref now internal to HostConnectionService
        public string LocalPlayerId => connectionData?.LocalPlayerId ?? string.Empty;
        public string LocalDisplayName => connectionData?.LocalDisplayName ?? string.Empty;
        public int LocalAvatarId => connectionData != null ? connectionData.LocalAvatarId : 0;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            if (connectionData == null) return;

            if (connectionData.OnlinePlayers != null)
                connectionData.OnlinePlayers.OnItemCountChanged += BridgeOnlinePlayersUpdated;

            if (connectionData.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised += BridgeInviteReceived;

            if (connectionData.OnPartyMemberJoined != null)
                connectionData.OnPartyMemberJoined.OnRaised += BridgePartyMemberJoined;
        }

        void OnDisable()
        {
            if (connectionData == null) return;

            if (connectionData.OnlinePlayers != null)
                connectionData.OnlinePlayers.OnItemCountChanged -= BridgeOnlinePlayersUpdated;

            if (connectionData.OnInviteReceived != null)
                connectionData.OnInviteReceived.OnRaised -= BridgeInviteReceived;

            if (connectionData.OnPartyMemberJoined != null)
                connectionData.OnPartyMemberJoined.OnRaised -= BridgePartyMemberJoined;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Legacy API — delegates to HostConnectionService
        // ─────────────────────────────────────────────────────────────────────

        public void SyncProfileFromPlayerDataService()
        {
            // No-op: HostConnectionService handles this internally.
        }

        public void HandleSignedInEvent()
        {
            HostConnectionService.Instance?.HandleSignedInEvent();
        }

        public void HandleSignedOutEvent()
        {
            HostConnectionService.Instance?.HandleSignedOutEvent();
        }

        public Task SendInviteAsync(string targetPlayerId)
        {
            if (HostConnectionService.Instance == null) return Task.CompletedTask;
            return HostConnectionService.Instance.SendInviteAsync(targetPlayerId);
        }

        public Task AcceptInviteAsync(PartyInvite invite)
        {
            if (HostConnectionService.Instance == null) return Task.CompletedTask;
            var soapInvite = new PartyInviteData(
                invite.HostPlayerId, invite.PartySessionId,
                invite.HostDisplayName, invite.HostAvatarId);
            return HostConnectionService.Instance.AcceptInviteAsync(soapInvite);
        }

        public Task DeclineInviteAsync()
        {
            if (HostConnectionService.Instance == null) return Task.CompletedTask;
            return HostConnectionService.Instance.DeclineInviteAsync();
        }

        public void HandOffToMultiplayerSetup(GameDataSO gameData)
        {
            HostConnectionService.Instance?.HandOffToMultiplayerSetup(gameData);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOAP → Legacy bridges
        // ─────────────────────────────────────────────────────────────────────

        private void BridgeOnlinePlayersUpdated()
        {
            if (connectionData?.OnlinePlayers == null) return;

            var legacyList = new List<OnlinePlayerInfo>();
            foreach (var p in connectionData.OnlinePlayers)
                legacyList.Add(OnlinePlayerInfo.FromSOAP(p));

            OnOnlinePlayersUpdated?.Invoke(legacyList);
        }

        private void BridgeInviteReceived(PartyInviteData data)
        {
            OnInviteReceived?.Invoke(PartyInvite.FromSOAP(data));
        }

        private void BridgePartyMemberJoined(PartyPlayerData data)
        {
            OnPartyMemberJoined?.Invoke(data.DisplayName);
        }
    }
}

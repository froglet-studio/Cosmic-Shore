using System;
using Cysharp.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Services.Friends.Models;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// MonoBehaviour bridge that initializes the <see cref="FriendsServiceFacade"/>
    /// after authentication and sets presence when entering/leaving the menu.
    ///
    /// <para>
    /// Initialization is event-driven: the inspector-wired SOAP EventListenerNoParam
    /// on <c>AuthenticationData.OnSignedIn</c> calls <see cref="HandleSignedInEvent"/>.
    /// <c>Start()</c> additionally bootstraps immediately if auth already completed
    /// before this MonoBehaviour loaded (e.g. after a scene reload).
    /// </para>
    ///
    /// Place on the same persistent GameObject as <see cref="HostConnectionService"/>.
    /// Lifetime: DontDestroyOnLoad MonoBehaviour.
    /// Thread-safety: main-thread only.
    /// </summary>
    public class FriendsInitializer : MonoBehaviour
    {
        [Header("Auth (Source of Truth)")]
        [SerializeField] private AuthenticationDataVariable authenticationDataVariable;
        private AuthenticationData AuthData => authenticationDataVariable.Value;

        [Header("SOAP Data")]
        [SerializeField] private FriendsDataSO friendsData;

        [Tooltip("Party/lobby data container. If assigned, presence is updated to 'In Party' " +
                 "when members join and back to 'In Menu' when the party empties.")]
        [SerializeField] private HostConnectionDataSO hostConnectionData;

        [Inject] private FriendsServiceFacade friendsService;

        // Assigned in Start() - HostConnectionService is on the same persistent GO
        // and sets its Instance in Awake(), which runs before Start().
        private IPartyStateQuery _partyQuery;

        private bool _initialized;
        private bool _partySubscriptionsWired;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Start()
        {
            // HostConnectionService.Awake() sets Instance before any Start() runs.
            _partyQuery = HostConnectionService.Instance;

            // Wire party SOAP subscriptions immediately - hostConnectionData is
            // available from the inspector-serialized field.
            WirePartySubscriptions();

            // UGS auth completes asynchronously AFTER Start in the normal flow, so
            // OnSignedIn is the primary trigger - subscribe in code (same pattern as
            // MultiplayerSetup). There is no inspector EventListenerNoParam for this
            // handler. The immediate call covers the already-signed-in case;
            // HandleSignedInEvent is idempotent (guarded by _initialized).
            if (authenticationDataVariable != null)
                authenticationDataVariable.Value.OnSignedIn.OnRaised += HandleSignedInEvent;
            if (IsAuthSignedIn())
                HandleSignedInEvent();
        }

        void OnDestroy()
        {
            if (authenticationDataVariable != null)
                authenticationDataVariable.Value.OnSignedIn.OnRaised -= HandleSignedInEvent;
            UnwirePartySubscriptions();
            SetPresenceOffline();
        }

        void WirePartySubscriptions()
        {
            if (_partySubscriptionsWired || hostConnectionData == null) return;
            _partySubscriptionsWired = true;

            if (hostConnectionData.OnPartyMemberJoined != null)
                hostConnectionData.OnPartyMemberJoined.OnRaised += HandlePartyMemberJoined;
            if (hostConnectionData.OnPartyMemberLeft != null)
                hostConnectionData.OnPartyMemberLeft.OnRaised += HandlePartyMemberLeft;
        }

        void UnwirePartySubscriptions()
        {
            if (!_partySubscriptionsWired || hostConnectionData == null) return;
            _partySubscriptionsWired = false;

            if (hostConnectionData.OnPartyMemberJoined != null)
                hostConnectionData.OnPartyMemberJoined.OnRaised -= HandlePartyMemberJoined;
            if (hostConnectionData.OnPartyMemberLeft != null)
                hostConnectionData.OnPartyMemberLeft.OnRaised -= HandlePartyMemberLeft;
        }

        void HandlePartyMemberJoined(PartyPlayerData _) => SetPresenceInParty();

        void HandlePartyMemberLeft(PartyPlayerData _)
        {
            // Once the last remote member leaves, return to solo "In Menu" presence.
            if (hostConnectionData != null && hostConnectionData.RemotePartyMemberCount == 0)
                SetPresenceInMenu();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public: Auth hooks (wire via SOAP EventListenerNoParam in inspector)
        // ─────────────────────────────────────────────────────────────────────

        public async void HandleSignedInEvent()
        {
            if (_initialized) return;
            if (!IsAuthSignedIn()) return;

            await InitializeFriendsAsync();
        }

        public void HandleSignedOutEvent()
        {
            _initialized = false;
            friendsService?.HandleSignedOut();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public: Presence helpers for scene transitions
        // ─────────────────────────────────────────────────────────────────────

        public async void SetPresenceInMenu()
        {
            if (friendsService == null || !friendsService.IsInitialized) return;

            await friendsService.SetPresenceAsync(
                Availability.Online,
                new FriendPresenceActivity("In Menu", "Menu_Main"));
        }

        /// <summary>
        /// Sets presence to "In Party" so friends see when this player joins or creates
        /// a party lobby. Called by <see cref="PartyInviteController"/> after the local
        /// player successfully joins/creates a party session.
        /// </summary>
        public async void SetPresenceInParty()
        {
            if (friendsService == null || !friendsService.IsInitialized) return;

            var partySessionId = _partyQuery?.ActivePartySessionId ?? "";
            int memberCount = hostConnectionData != null && hostConnectionData.PartyMembers != null
                ? hostConnectionData.PartyMembers.Count : 0;
            // The party size players SEE (4), never the transport capacity (6).
            int maxSlots = hostConnectionData != null ? hostConnectionData.PartyDisplaySlots : 0;

            await friendsService.SetPresenceAsync(
                Availability.Online,
                new FriendPresenceActivity(
                    "In Party",
                    "Menu_Main",
                    "",
                    partySessionId,
                    memberCount,
                    maxSlots));
        }

        public async void SetPresenceInGame(string sceneName, string vesselClass, string matchName = "")
        {
            if (friendsService == null || !friendsService.IsInitialized) return;

            var partySessionId = _partyQuery?.ActivePartySessionId ?? "";
            int memberCount = hostConnectionData != null && hostConnectionData.PartyMembers != null
                ? hostConnectionData.PartyMembers.Count : 0;
            // The party size players SEE (4), never the transport capacity (6).
            int maxSlots = hostConnectionData != null ? hostConnectionData.PartyDisplaySlots : 0;

            await friendsService.SetPresenceAsync(
                Availability.Busy,
                new FriendPresenceActivity(
                    "In Game",
                    sceneName,
                    vesselClass,
                    partySessionId,
                    memberCount,
                    maxSlots,
                    matchName));
        }

        public async void SetPresenceOffline()
        {
            if (friendsService == null || !friendsService.IsInitialized) return;

            try
            {
                await friendsService.SetAvailabilityAsync(Availability.Offline);
            }
            catch (Exception)
            {
                // Suppress errors during shutdown
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal
        // ─────────────────────────────────────────────────────────────────────

        private async UniTask InitializeFriendsAsync()
        {
            if (_initialized || friendsService == null) return;

            try
            {
                await friendsService.InitializeAsync();
                _initialized = true;

                // Set initial presence to "In Menu"
                await friendsService.SetPresenceAsync(
                    Availability.Online,
                    new FriendPresenceActivity("In Menu", "Menu_Main"));

                Debug.Log("[FriendsInitializer] Friends service initialized and presence set.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsInitializer] Init failed: {e.Message}");
            }
        }

        private bool IsAuthSignedIn()
        {
            if (AuthData == null) return false;
            return AuthData.IsSignedIn ||
                   AuthData.State == AuthenticationData.AuthState.SignedIn;
        }
    }
}

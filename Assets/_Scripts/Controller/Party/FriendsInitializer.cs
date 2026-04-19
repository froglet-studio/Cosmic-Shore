using System;
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
    /// Place on the same persistent GameObject as <see cref="HostConnectionService"/>.
    /// Wires auth SOAP events to trigger Friends SDK initialization and presence updates.
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

        private bool _initialized;
        private bool _partySubscriptionsWired;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        async void Start()
        {
            // Wait for auth to be signed in
            while (!IsAuthSignedIn())
                await System.Threading.Tasks.Task.Delay(500);

            await InitializeFriendsAsync();
            WirePartySubscriptions();
        }

        void OnDestroy()
        {
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

            var partySessionId = HostConnectionService.Instance?.PartySession?.Id ?? "";
            await friendsService.SetPresenceAsync(
                Availability.Online,
                new FriendPresenceActivity("In Party", "Menu_Main", "", partySessionId));
        }

        public async void SetPresenceInGame(string sceneName, string vesselClass)
        {
            if (friendsService == null || !friendsService.IsInitialized) return;

            var partySessionId = HostConnectionService.Instance?.PartySession?.Id ?? "";

            await friendsService.SetPresenceAsync(
                Availability.Busy,
                new FriendPresenceActivity("In Game", sceneName, vesselClass, partySessionId));
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

        private async System.Threading.Tasks.Task InitializeFriendsAsync()
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

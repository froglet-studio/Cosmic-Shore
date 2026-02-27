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

        [Inject] private FriendsServiceFacade friendsService;

        private bool _initialized;

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        async void Start()
        {
            // Wait for auth to be signed in
            while (!IsAuthSignedIn())
                await System.Threading.Tasks.Task.Delay(500);

            await InitializeFriendsAsync();
        }

        void OnDestroy()
        {
            SetPresenceOffline();
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

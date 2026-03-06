using System;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Single-writer facade for the UGS Friends service.
    /// Initializes the Friends SDK after authentication, synchronizes relationship
    /// data into <see cref="FriendsDataSO"/>, and exposes public API for UI consumers.
    ///
    /// Follows the same single-writer / multi-reader pattern as
    /// <see cref="AuthenticationServiceFacade"/>.
    /// </summary>
    public class FriendsServiceFacade
    {
        readonly AuthenticationDataVariable _authDataVariable;
        readonly FriendsDataSO _friendsData;
        readonly bool _allowLog;

        bool _initialized;
        bool _initializing;

        IFriendsService Service => FriendsService.Instance;

        public bool IsInitialized => _initialized;

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        public FriendsServiceFacade(
            AuthenticationDataVariable authDataVariable,
            FriendsDataSO friendsData,
            bool allowLog = false)
        {
            _authDataVariable = authDataVariable;
            _friendsData = friendsData;
            _allowLog = allowLog;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes the Friends service. Must be called after UGS auth sign-in.
        /// Safe to call multiple times — subsequent calls are no-ops.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized || _initializing) return;
            _initializing = true;

            try
            {
                Log("Initializing Friends service...");
                await Service.InitializeAsync();

                WireEvents();
                SyncAllRelationships();

                _initialized = true;
                _friendsData.IsInitialized = true;
                _friendsData.OnFriendsServiceReady?.Raise();

                Log($"Friends service ready. Friends={_friendsData.FriendCount}, " +
                    $"IncomingRequests={_friendsData.IncomingRequestCount}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] Initialization failed: {e.Message}");
                _initializing = false;
            }
        }

        /// <summary>
        /// Resets state on sign-out. Call from auth sign-out handler.
        /// </summary>
        public void HandleSignedOut()
        {
            UnwireEvents();
            _initialized = false;
            _initializing = false;
            _friendsData.ResetRuntimeData();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Friend Requests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a friend request to a player by their display name.
        /// If the target already sent a request to us, this creates a mutual friendship.
        /// </summary>
        public async Task SendFriendRequestByNameAsync(string playerName)
        {
            EnsureInitialized();

            try
            {
                Log($"Sending friend request to '{playerName}'...");
                await Service.AddFriendByNameAsync(playerName);

                // Refresh lists to capture the new relationship state
                SyncAllRelationships();
                Log($"Friend request sent to '{playerName}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] SendFriendRequestByName error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends a friend request to a player by their player ID.
        /// </summary>
        public async Task SendFriendRequestAsync(string playerId)
        {
            EnsureInitialized();

            try
            {
                Log($"Sending friend request to ID '{playerId}'...");
                await Service.AddFriendAsync(playerId);

                SyncAllRelationships();
                Log($"Friend request sent to ID '{playerId}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] SendFriendRequest error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Accepts an incoming friend request by adding the requester as a friend.
        /// </summary>
        public async Task AcceptFriendRequestAsync(string playerId)
        {
            EnsureInitialized();

            try
            {
                Log($"Accepting friend request from '{playerId}'...");
                await Service.AddFriendAsync(playerId);

                SyncAllRelationships();
                Log($"Friend request accepted from '{playerId}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] AcceptFriendRequest error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Declines/rejects an incoming friend request.
        /// </summary>
        public async Task DeclineFriendRequestAsync(string playerId)
        {
            EnsureInitialized();

            try
            {
                Log($"Declining friend request from '{playerId}'...");
                await Service.DeleteIncomingFriendRequestAsync(playerId);

                SyncAllRelationships();
                Log($"Friend request declined from '{playerId}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] DeclineFriendRequest error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cancels an outgoing friend request that hasn't been accepted yet.
        /// </summary>
        public async Task CancelFriendRequestAsync(string playerId)
        {
            EnsureInitialized();

            try
            {
                Log($"Cancelling friend request to '{playerId}'...");
                await Service.DeleteOutgoingFriendRequestAsync(playerId);

                SyncAllRelationships();
                Log($"Friend request cancelled to '{playerId}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] CancelFriendRequest error: {e.Message}");
                throw;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Friend Management
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Removes an existing friend.
        /// </summary>
        public async Task RemoveFriendAsync(string playerId)
        {
            EnsureInitialized();

            try
            {
                Log($"Removing friend '{playerId}'...");
                await Service.DeleteFriendAsync(playerId);

                SyncAllRelationships();
                Log($"Friend removed: '{playerId}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] RemoveFriend error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Blocks a player. Removes any existing friendship or pending request.
        /// </summary>
        public async Task BlockPlayerAsync(string playerId)
        {
            EnsureInitialized();

            try
            {
                Log($"Blocking player '{playerId}'...");
                await Service.AddBlockAsync(playerId);

                SyncAllRelationships();
                Log($"Player blocked: '{playerId}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] BlockPlayer error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Unblocks a player.
        /// </summary>
        public async Task UnblockPlayerAsync(string playerId)
        {
            EnsureInitialized();

            try
            {
                Log($"Unblocking player '{playerId}'...");
                await Service.DeleteBlockAsync(playerId);

                SyncAllRelationships();
                Log($"Player unblocked: '{playerId}'.");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] UnblockPlayer error: {e.Message}");
                throw;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Presence
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the local player's presence (availability + activity).
        /// </summary>
        public async Task SetPresenceAsync(Availability availability, FriendPresenceActivity activity)
        {
            if (!_initialized) return;

            try
            {
                await Service.SetPresenceAsync(availability, activity);
                Log($"Presence set: {availability}, status='{activity.Status}'");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] SetPresence error: {e.Message}");
            }
        }

        /// <summary>
        /// Sets availability only (no activity change).
        /// </summary>
        public async Task SetAvailabilityAsync(Availability availability)
        {
            if (!_initialized) return;

            try
            {
                await Service.SetPresenceAvailabilityAsync(availability);
                Log($"Availability set: {availability}");
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] SetAvailability error: {e.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Refresh
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Force-refreshes all relationship data from the server.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (!_initialized) return;

            try
            {
                await Service.ForceRelationshipsRefreshAsync();
                SyncAllRelationships();
            }
            catch (FriendsServiceException e)
            {
                Debug.LogWarning($"[FriendsServiceFacade] Refresh error: {e.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API: Queries
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether a given player ID is in the local friend list.
        /// </summary>
        public bool IsFriend(string playerId)
        {
            if (!_initialized || _friendsData.Friends == null) return false;

            foreach (var f in _friendsData.Friends)
                if (f.PlayerId == playerId) return true;

            return false;
        }

        /// <summary>
        /// Checks whether a given player ID is blocked.
        /// </summary>
        public bool IsBlocked(string playerId)
        {
            if (!_initialized || _friendsData.BlockedPlayers == null) return false;

            foreach (var b in _friendsData.BlockedPlayers)
                if (b.PlayerId == playerId) return true;

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Event Wiring
        // ─────────────────────────────────────────────────────────────────────

        void WireEvents()
        {
            Service.RelationshipAdded += OnRelationshipAdded;
            Service.RelationshipDeleted += OnRelationshipDeleted;
            Service.PresenceUpdated += OnPresenceUpdated;
        }

        void UnwireEvents()
        {
            if (Service == null) return;

            Service.RelationshipAdded -= OnRelationshipAdded;
            Service.RelationshipDeleted -= OnRelationshipDeleted;
            Service.PresenceUpdated -= OnPresenceUpdated;
        }

        void OnRelationshipAdded(IRelationshipAddedEvent evt)
        {
            Log($"Relationship added: {evt.Relationship.Type} with {evt.Relationship.Member.Id}");
            SyncAllRelationships();

            var data = RelationshipToFriendData(evt.Relationship);
            if (evt.Relationship.Type == RelationshipType.Friend)
                _friendsData.OnFriendAdded?.Raise(data);
            else if (evt.Relationship.Type == RelationshipType.FriendRequest &&
                     evt.Relationship.Member.Role == MemberRole.Source)
                _friendsData.OnFriendRequestReceived?.Raise(data);
        }

        void OnRelationshipDeleted(IRelationshipDeletedEvent evt)
        {
            Log($"Relationship deleted: {evt.Relationship.Type} with {evt.Relationship.Member.Id}");

            var data = RelationshipToFriendData(evt.Relationship);
            if (evt.Relationship.Type == RelationshipType.Friend)
                _friendsData.OnFriendRemoved?.Raise(data);

            SyncAllRelationships();
        }

        void OnPresenceUpdated(IPresenceUpdatedEvent evt)
        {
            Log($"Presence updated for {evt.ID}: {evt.Presence.Availability}");
            SyncAllRelationships();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Sync: SDK → SOAP
        // ─────────────────────────────────────────────────────────────────────

        void SyncAllRelationships()
        {
            SyncFriends();
            SyncIncomingRequests();
            SyncOutgoingRequests();
            SyncBlocked();
        }

        void SyncFriends()
        {
            _friendsData.Friends?.Clear();
            if (Service.Friends == null) return;

            foreach (var rel in Service.Friends)
                _friendsData.Friends?.Add(RelationshipToFriendData(rel));
        }

        void SyncIncomingRequests()
        {
            _friendsData.IncomingRequests?.Clear();
            if (Service.IncomingFriendRequests == null) return;

            foreach (var rel in Service.IncomingFriendRequests)
                _friendsData.IncomingRequests?.Add(RelationshipToFriendData(rel));
        }

        void SyncOutgoingRequests()
        {
            _friendsData.OutgoingRequests?.Clear();
            if (Service.OutgoingFriendRequests == null) return;

            foreach (var rel in Service.OutgoingFriendRequests)
                _friendsData.OutgoingRequests?.Add(RelationshipToFriendData(rel));
        }

        void SyncBlocked()
        {
            _friendsData.BlockedPlayers?.Clear();
            if (Service.Blocks == null) return;

            foreach (var rel in Service.Blocks)
                _friendsData.BlockedPlayers?.Add(RelationshipToFriendData(rel));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        static FriendData RelationshipToFriendData(Relationship relationship)
        {
            var member = relationship.Member;
            string displayName = member.Profile?.Name ?? "Unknown Pilot";
            int availability = AvailabilityToInt(member.Presence?.Availability ?? Availability.Unknown);
            string activityStatus = "";

            try
            {
                var activity = member.Presence?.GetActivity<FriendPresenceActivity>();
                if (activity != null)
                    activityStatus = activity.Status ?? "";
            }
            catch
            {
                // Activity deserialization can fail if the other player uses a different format
            }

            return new FriendData(member.Id, displayName, availability, activityStatus);
        }

        static int AvailabilityToInt(Availability availability)
        {
            return availability switch
            {
                Availability.Online => 1,
                Availability.Busy => 2,
                Availability.Away => 3,
                Availability.Invisible => 4,
                Availability.Offline => 5,
                _ => 0 // Unknown
            };
        }

        void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException(
                    "[FriendsServiceFacade] Service not initialized. Call InitializeAsync() after auth sign-in.");
        }

        void Log(string msg)
        {
            if (_allowLog)
                Debug.Log($"[UGS Friends] {msg}");
        }
    }
}

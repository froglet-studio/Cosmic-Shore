using Obvious.Soap;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Central SOAP data container for the UGS Friends system.
    /// Holds runtime state + SOAP events that decouple FriendsServiceFacade from all UI consumers.
    /// Create one asset and wire it into FriendsServiceFacade, FriendsPanel, etc.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FriendsData",
        menuName = "ScriptableObjects/DataContainers/Friends Data")]
    public class FriendsDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        // Friends List
        // ─────────────────────────────────────────────────────────────────────

        [Header("Friends")]
        [Tooltip("Reactive list of confirmed friends.")]
        public ScriptableListFriendData Friends;

        [Tooltip("Raised when a new friend relationship is established.")]
        public ScriptableEventFriendData OnFriendAdded;

        [Tooltip("Raised when a friend is removed.")]
        public ScriptableEventFriendData OnFriendRemoved;

        // ─────────────────────────────────────────────────────────────────────
        // Friend Requests
        // ─────────────────────────────────────────────────────────────────────

        [Header("Friend Requests")]
        [Tooltip("Reactive list of incoming friend requests (awaiting local player's response).")]
        public ScriptableListFriendData IncomingRequests;

        [Tooltip("Reactive list of outgoing friend requests (awaiting remote player's response).")]
        public ScriptableListFriendData OutgoingRequests;

        [Tooltip("Raised when a new incoming friend request arrives.")]
        public ScriptableEventFriendData OnFriendRequestReceived;

        [Tooltip("Raised when an outgoing friend request is sent.")]
        public ScriptableEventFriendData OnFriendRequestSent;

        // ─────────────────────────────────────────────────────────────────────
        // Blocked Players
        // ─────────────────────────────────────────────────────────────────────

        [Header("Blocked")]
        [Tooltip("Reactive list of blocked players.")]
        public ScriptableListFriendData BlockedPlayers;

        // ─────────────────────────────────────────────────────────────────────
        // Service State
        // ─────────────────────────────────────────────────────────────────────

        [Header("Service State (runtime)")]
        [HideInInspector] public bool IsInitialized;

        [Tooltip("Raised when the friends service finishes initialization.")]
        public ScriptableEventNoParam OnFriendsServiceReady;

        // ─────────────────────────────────────────────────────────────────────
        // Computed Properties
        // ─────────────────────────────────────────────────────────────────────

        public int FriendCount => Friends != null ? Friends.Count : 0;
        public int IncomingRequestCount => IncomingRequests != null ? IncomingRequests.Count : 0;
        public int OnlineFriendCount
        {
            get
            {
                if (Friends == null) return 0;
                int count = 0;
                foreach (var f in Friends)
                    if (f.IsOnline) count++;
                return count;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public void ResetRuntimeData()
        {
            IsInitialized = false;
            Friends?.Clear();
            IncomingRequests?.Clear();
            OutgoingRequests?.Clear();
            BlockedPlayers?.Clear();
        }
    }
}

using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Immutable snapshot of a friend relationship for UI consumption.
    /// Derived from the UGS Friends SDK Relationship/Member models.
    /// Used as the payload for SOAP events and as the element type for ScriptableList.
    /// </summary>
    [System.Serializable]
    public struct FriendData
    {
        [SerializeField] private string playerId;
        [SerializeField] private string displayName;
        [SerializeField] private int availability;
        [SerializeField] private string activityStatus;

        public string PlayerId => playerId;
        public string DisplayName => displayName;

        /// <summary>
        /// Maps to Unity.Services.Friends.Models.Availability enum values.
        /// 0=Unknown, 1=Online, 2=Busy, 3=Away, 4=Invisible, 5=Offline
        /// </summary>
        public int Availability => availability;
        public string ActivityStatus => activityStatus;

        public bool IsOnline => availability == 1 || availability == 2 || availability == 3;

        public FriendData(string playerId, string displayName, int availability = 0, string activityStatus = "")
        {
            this.playerId = playerId;
            this.displayName = displayName;
            this.availability = availability;
            this.activityStatus = activityStatus;
        }

        public override bool Equals(object obj)
        {
            if (obj is not FriendData other) return false;
            return playerId == other.playerId;
        }

        public override int GetHashCode() => playerId?.GetHashCode() ?? 0;
    }
}

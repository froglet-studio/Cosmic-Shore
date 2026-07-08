// ─────────────────────────────────────────────────────────────────────────────
// FriendsSdk.cs — engine placeholder surface for the UGS Friends SDK
// (original contract: Unity.Services.Friends / .Exceptions / .Models /
// .Notifications). Grown per the ISession precedent so consumers
// (FriendsServiceFacade, FriendsInitializer) port FULLY LIVE instead of
// carrying services-phase deviations: the game-side code compiles and runs
// against these types, tests install a fake IFriendsService via
// FriendsService.Instance (settable — the NetworkManager.Singleton test-hook
// pattern), and the real SDK binding lands when UGS services port.
//
// Data semantics mirror the UGS SDK shapes the game actually reads:
//   Relationship { Type, Member } / Member { Id, Role, Profile, Presence } /
//   Profile { Name } / Presence { Availability, GetActivity<T>() }.
// Presence stores one opaque activity object; GetActivity<T> pattern-matches,
// mirroring the SDK's deserialize-or-throw contract loosely (returns default
// on shape mismatch — the game wraps calls in try/catch anyway).
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Services.Friends
{
    /// <summary>Presence availability (original contract: Unity.Services.Friends.Models.Availability).</summary>
    public enum Availability
    {
        Unknown = 0,
        Online = 1,
        Busy = 2,
        Away = 3,
        Invisible = 4,
        Offline = 5,
    }

    /// <summary>Relationship kind (original contract: Unity.Services.Friends.Models.RelationshipType).</summary>
    public enum RelationshipType
    {
        Friend = 0,
        FriendRequest = 1,
        Block = 2,
    }

    /// <summary>
    /// Which side of the relationship the member is on (original contract:
    /// Unity.Services.Friends.Models.MemberRole). <c>Source</c> = they initiated
    /// (an incoming request, from our perspective); <c>Target</c> = we initiated.
    /// </summary>
    public enum MemberRole
    {
        None = 0,
        Source = 1,
        Target = 2,
    }

    /// <summary>Member profile data (original contract: Unity.Services.Friends.Models.Profile).</summary>
    public class Profile
    {
        public string Name { get; set; }
        public Profile() { }
        public Profile(string name) => Name = name;
    }

    /// <summary>Member presence data (original contract: Unity.Services.Friends.Models.Presence).</summary>
    public class Presence
    {
        public Availability Availability { get; set; } = Availability.Unknown;

        /// <summary>Opaque activity payload set alongside availability.</summary>
        public object Activity { get; set; }

        public T GetActivity<T>() where T : class => Activity as T;
    }

    /// <summary>One side of a relationship (original contract: Unity.Services.Friends.Models.Member).</summary>
    public class Member
    {
        public string Id { get; set; }
        public MemberRole Role { get; set; } = MemberRole.None;
        public Profile Profile { get; set; }
        public Presence Presence { get; set; }
    }

    /// <summary>A relationship with another player (original contract: Unity.Services.Friends.Models.Relationship).</summary>
    public class Relationship
    {
        public RelationshipType Type { get; set; }
        public Member Member { get; set; }
    }

    /// <summary>Original contract: Unity.Services.Friends.Exceptions.FriendsServiceException.</summary>
    public class FriendsServiceException : Exception
    {
        public FriendsServiceException(string message) : base(message) { }
        public FriendsServiceException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Original contract: Unity.Services.Friends.Notifications.IRelationshipAddedEvent.</summary>
    public interface IRelationshipAddedEvent
    {
        Relationship Relationship { get; }
    }

    /// <summary>Original contract: Unity.Services.Friends.Notifications.IRelationshipDeletedEvent.</summary>
    public interface IRelationshipDeletedEvent
    {
        Relationship Relationship { get; }
    }

    /// <summary>Original contract: Unity.Services.Friends.Notifications.IPresenceUpdatedEvent.</summary>
    public interface IPresenceUpdatedEvent
    {
        string ID { get; }
        Presence Presence { get; }
    }

    /// <summary>
    /// The Friends SDK surface the game consumes (original contract:
    /// Unity.Services.Friends.IFriendsService).
    /// </summary>
    public interface IFriendsService
    {
        Task InitializeAsync();

        // Relationship reads — snapshots the SDK keeps synchronized.
        IReadOnlyList<Relationship> Friends { get; }
        IReadOnlyList<Relationship> IncomingFriendRequests { get; }
        IReadOnlyList<Relationship> OutgoingFriendRequests { get; }
        IReadOnlyList<Relationship> Blocks { get; }

        // Relationship writes.
        Task AddFriendByNameAsync(string playerName);
        Task AddFriendAsync(string playerId);
        Task DeleteIncomingFriendRequestAsync(string playerId);
        Task DeleteOutgoingFriendRequestAsync(string playerId);
        Task DeleteFriendAsync(string playerId);
        Task AddBlockAsync(string playerId);
        Task DeleteBlockAsync(string playerId);

        // Presence.
        Task SetPresenceAsync<T>(Availability availability, T activity) where T : class;
        Task SetPresenceAvailabilityAsync(Availability availability);

        // Refresh.
        Task ForceRelationshipsRefreshAsync();

        // Notifications.
        event Action<IRelationshipAddedEvent> RelationshipAdded;
        event Action<IRelationshipDeletedEvent> RelationshipDeleted;
        event Action<IPresenceUpdatedEvent> PresenceUpdated;
    }

    /// <summary>
    /// Static access point (original contract: Unity.Services.Friends.FriendsService).
    /// <see cref="Instance"/> is null until the real SDK binding lands; tests install
    /// a fake here (the NetworkManager.Singleton test-hook pattern) and clear it in
    /// teardown.
    /// </summary>
    public static class FriendsService
    {
        public static IFriendsService Instance { get; set; }
    }
}

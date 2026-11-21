using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Services.Friends
{
    /// <summary>
    /// The player's Friends backend with no social server behind it: a local relationship
    /// book that starts empty (exactly what a signed-in UGS account with no friends reads),
    /// accepts writes, raises the SDK's notifications for them, and records the local
    /// player's own presence. Requests by NAME cannot resolve without a directory, so they
    /// fail the way the SDK fails an unknown name — with a <see cref="FriendsServiceException"/>.
    /// </summary>
    public sealed class LocalFriendsService : IFriendsService
    {
        readonly List<Relationship> _friends = new();
        readonly List<Relationship> _incoming = new();
        readonly List<Relationship> _outgoing = new();
        readonly List<Relationship> _blocks = new();

        public Presence LocalPresence { get; } = new() { Availability = Availability.Offline };

        public IReadOnlyList<Relationship> Friends => _friends;
        public IReadOnlyList<Relationship> IncomingFriendRequests => _incoming;
        public IReadOnlyList<Relationship> OutgoingFriendRequests => _outgoing;
        public IReadOnlyList<Relationship> Blocks => _blocks;

        public event Action<IRelationshipAddedEvent> RelationshipAdded;
        public event Action<IRelationshipDeletedEvent> RelationshipDeleted;
        public event Action<IPresenceUpdatedEvent> PresenceUpdated;

        public Task InitializeAsync() => Task.CompletedTask;

        public Task AddFriendByNameAsync(string playerName)
            => Task.FromException(new FriendsServiceException($"No player named '{playerName}' could be found."));

        public Task AddFriendAsync(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return Task.FromException(new FriendsServiceException("playerId is empty."));
            // Accepting an incoming request promotes it; otherwise this sends one.
            var incoming = Take(_incoming, playerId);
            if (incoming != null)
            {
                var friend = Make(RelationshipType.Friend, playerId, MemberRole.None, incoming.Member?.Profile?.Name);
                _friends.Add(friend);
                RelationshipAdded?.Invoke(new Added(friend));
            }
            else if (Find(_friends, playerId) == null && Find(_outgoing, playerId) == null)
            {
                var request = Make(RelationshipType.FriendRequest, playerId, MemberRole.Target, null);
                _outgoing.Add(request);
                RelationshipAdded?.Invoke(new Added(request));
            }
            return Task.CompletedTask;
        }

        public Task DeleteIncomingFriendRequestAsync(string playerId) => Remove(_incoming, playerId);
        public Task DeleteOutgoingFriendRequestAsync(string playerId) => Remove(_outgoing, playerId);
        public Task DeleteFriendAsync(string playerId) => Remove(_friends, playerId);

        public Task AddBlockAsync(string playerId)
        {
            Take(_friends, playerId);
            Take(_incoming, playerId);
            Take(_outgoing, playerId);
            if (Find(_blocks, playerId) == null)
            {
                var block = Make(RelationshipType.Block, playerId, MemberRole.Target, null);
                _blocks.Add(block);
                RelationshipAdded?.Invoke(new Added(block));
            }
            return Task.CompletedTask;
        }

        public Task DeleteBlockAsync(string playerId) => Remove(_blocks, playerId);

        public Task SetPresenceAsync<T>(Availability availability, T activity) where T : class
        {
            LocalPresence.Availability = availability;
            LocalPresence.Activity = activity;
            return Task.CompletedTask;
        }

        public Task SetPresenceAvailabilityAsync(Availability availability)
        {
            LocalPresence.Availability = availability;
            return Task.CompletedTask;
        }

        public Task ForceRelationshipsRefreshAsync() => Task.CompletedTask;

        /// <summary>Test/tool hook: deliver an incoming friend request as the server would.</summary>
        public void ReceiveFriendRequest(string playerId, string name)
        {
            var r = Make(RelationshipType.FriendRequest, playerId, MemberRole.Source, name);
            _incoming.Add(r);
            RelationshipAdded?.Invoke(new Added(r));
        }

        /// <summary>Test/tool hook: a friend's presence changed.</summary>
        public void UpdatePresence(string playerId, Presence presence)
        {
            var f = Find(_friends, playerId);
            if (f?.Member != null) f.Member.Presence = presence;
            PresenceUpdated?.Invoke(new PresenceChanged(playerId, presence));
        }

        Task Remove(List<Relationship> list, string playerId)
        {
            var r = Take(list, playerId);
            if (r != null) RelationshipDeleted?.Invoke(new Deleted(r));
            return Task.CompletedTask;
        }

        static Relationship Make(RelationshipType type, string id, MemberRole role, string name) => new()
        {
            Type = type,
            Member = new Member
            {
                Id = id,
                Role = role,
                Profile = new Profile(name ?? id),
                Presence = new Presence { Availability = Availability.Offline },
            },
        };

        static Relationship Find(List<Relationship> list, string id)
        {
            foreach (var r in list) if (r.Member?.Id == id) return r;
            return null;
        }

        static Relationship Take(List<Relationship> list, string id)
        {
            var r = Find(list, id);
            if (r != null) list.Remove(r);
            return r;
        }

        sealed record Added(Relationship Relationship) : IRelationshipAddedEvent;
        sealed record Deleted(Relationship Relationship) : IRelationshipDeletedEvent;
        sealed record PresenceChanged(string ID, Presence Presence) : IPresenceUpdatedEvent;
    }
}

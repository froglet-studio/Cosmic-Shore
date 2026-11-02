// ─────────────────────────────────────────────────────────────────────────────
// LobbySdk.cs — engine placeholder surface for the UGS Lobby API that the game
// calls directly (original contract: Unity.Services.Lobbies — LobbyService.Instance
// and ILobbyService.RemovePlayerAsync). The game removes its own STALE lobby
// membership with it (StaleLobbyMembership: a failed join or a stopped Play left
// the player listed, and every later join answered "already a member").
//
// In the single-process port no remote lobby exists, so the local service has no
// membership to remove: RemovePlayerAsync completes at once. Tests swap a fake into
// the settable Instance and call Reset in teardown (MultiplayerSdk's precedent).
// ─────────────────────────────────────────────────────────────────────────────

using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>The lobby calls the game makes outside a session (original contract: Unity.Services.Lobbies.ILobbyService).</summary>
    public interface ILobbyService
    {
        /// <summary>Removes <paramref name="playerId"/> from the lobby <paramref name="lobbyId"/>.</summary>
        Task RemovePlayerAsync(string lobbyId, string playerId);
    }

    /// <summary>Static access point (original contract: Unity.Services.Lobbies.LobbyService).</summary>
    public static class LobbyService
    {
        public static ILobbyService Instance { get; set; } = new LocalLobbyService();

        /// <summary>Restore the local default (test isolation helper).</summary>
        public static void Reset() => Instance = new LocalLobbyService();
    }

    /// <summary>The single-process lobby service: there is no remote membership, so a removal has nothing to do.</summary>
    public sealed class LocalLobbyService : ILobbyService
    {
        public Task RemovePlayerAsync(string lobbyId, string playerId) => Task.CompletedTask;
    }
}

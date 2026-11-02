// ─────────────────────────────────────────────────────────────────────────────
// StaleLobbyMembership.cs
// A lobby membership nobody holds a session handle for: the player is listed in
// the lobby, but this game has no ISession for it, so every join answers
// "player is already a member of the lobby" and nothing ever leaves it.
//
// WHERE THEY COME FROM (the four-player test of 2026-10-30, net_logs.py show 1):
//   1. A join whose Relay step fails. The SDK joins the LOBBY first, then the Relay
//      allocation; when the allocation is gone ("join code not found", a frozen or
//      departed host), its own cleanup's leave fails before it removes the player, so
//      the membership stays. Caleb's next three joins into that party were refused
//      "already a member".
//   2. A Play stopped without a leave (the Editor's Stop, a crash). The next Play's
//      presence join was refused "already a member" (Garrett 1271 s, Caleb 1798 s),
//      and Caleb's presence converge then failed every ~22 s for nine minutes, so he
//      never saw the others' lobby.
//
// THE RULE. On "already a member", remove ourselves from that lobby and join once
// more. After any other failed party join (bar a full party or a throttle, where the
// lobby never took us), remove ourselves best-effort so the failure leaves nothing
// behind. Removing a player that is not there is a 404 the helper swallows.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Clears lobby memberships this game holds no session for, and joins through them. See the file header.
    /// </summary>
    public static class StaleLobbyMembership
    {
        /// <summary>True when UGS refused a join because this player is already listed in that lobby.</summary>
        public static bool IsAlreadyMember(Exception e)
        {
            for (var current = e; current != null; current = current.InnerException)
                if (current.Message != null &&
                    current.Message.IndexOf("already a member", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        /// <summary>
        /// Whether a failed join may have left us in the lobby: everything except a refusal at the lobby's door
        /// (full, throttled) and our own cancel.
        /// </summary>
        public static bool MayHaveLeftAMembership(Exception e) =>
            UgsRequestPolicy.Classify(e) is not (UgsFailureClass.Full or UgsFailureClass.RateLimited or UgsFailureClass.Cancelled);

        /// <summary>
        /// Join through <paramref name="join"/>. On "already a member", remove ourselves from
        /// <paramref name="lobbyId"/> and join once more. With <paramref name="leaveOnFailure"/>, any other failure
        /// that may have left a membership removes it before the exception goes on.
        /// </summary>
        public static async UniTask<ISession> JoinAsync(
            UgsRequestPolicy policy, string lobbyId, Func<UniTask<ISession>> join, bool leaveOnFailure, string tag)
        {
            try
            {
                return await join();
            }
            catch (Exception e) when (IsAlreadyMember(e))
            {
                CSDebug.LogWarning($"[{tag}] Still listed in {lobbyId} from an earlier join or Play - leaving it and joining again.");
                await TryRemoveSelfAsync(policy, lobbyId, tag);
            }
            catch (Exception e) when (leaveOnFailure && MayHaveLeftAMembership(e))
            {
                await TryRemoveSelfAsync(policy, lobbyId, tag);
                throw;
            }

            // The second try, after the stale membership was removed. A failure here goes to the caller as before.
            try
            {
                return await join();
            }
            catch (Exception e) when (leaveOnFailure && MayHaveLeftAMembership(e) && !IsAlreadyMember(e))
            {
                await TryRemoveSelfAsync(policy, lobbyId, tag);
                throw;
            }
        }

        /// <summary>Removes the signed-in player from <paramref name="lobbyId"/>. Never throws; true when the call succeeded.</summary>
        public static async UniTask<bool> TryRemoveSelfAsync(UgsRequestPolicy policy, string lobbyId, string tag)
        {
            if (string.IsNullOrEmpty(lobbyId)) return false;
            string playerId;
            try { playerId = AuthenticationService.Instance?.PlayerId; }
            catch (Exception) { return false; }
            if (string.IsNullOrEmpty(playerId)) return false;

            try
            {
                await (policy ?? UgsRequestPolicy.CreateDefault()).ExecuteAsync($"lobby:remove-self:{lobbyId}",
                    async () => await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId).AsMainThread());
                CSDebug.LogVerbose(CSLogChannel.Party, $"[{tag}] Removed our stale membership of {lobbyId}.");
                return true;
            }
            catch (Exception e)
            {
                // Not a member (404), the lobby is gone, or the service is down: nothing more to do here.
                CSDebug.LogVerbose(CSLogChannel.Party, $"[{tag}] Removing ourselves from {lobbyId} did not go through ({e.GetType().Name}): {e.Message}");
                return false;
            }
        }
    }
}

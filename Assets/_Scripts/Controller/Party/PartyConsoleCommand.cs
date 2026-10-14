// ─────────────────────────────────────────────────────────────────────────────
// PartyConsoleCommand.cs
// The `party` console command: read the party layer's state, and drive it the way the UI does.
//
// WHY IT EXISTS:
//   The party layer's backlog (review Phases 0-1, B18-B25) has compiled for weeks and never been
//   run with four players. A run needs four machines to invite, accept, join, leave and spectate
//   on cue, and a check that reads the SAME state on each one. A console line is the one surface
//   that works in an MPPM virtual player, a standalone dev build, and a scripted headless run
//   (Tools/Build/prisma_party_scenarios/), so this is that surface.
//
// WHAT IT CALLS:
//   Exactly what the buttons call - FriendsListPanel's invite / join / spectate handlers and
//   PartyInviteNotificationPanel's accept / decline go to these same methods with the same
//   arguments. Nothing here is a second path into the party layer: a scenario that passes through
//   `party join` passed through PartyInviteController.JoinPartyAsync, pre-flight and all.
//
// WHY IT SELF-REGISTERS:
//   Same as NetSessionConsoleCommand: DiagnosticsHUD.RegisterCommand's body is already
//   #if UNITY_EDITOR || DEVELOPMENT_BUILD, so the call is a no-op in a release player and needs no
//   guard of its own (Docs/CONDITIONAL_COMPILATION.md Pattern 1).
//
// SUBCOMMANDS (names are display names, matched case-insensitively; spaces allowed)
//   party                    one line of key=value state (see Describe) - what a scenario asserts on
//   party online             the online list as this machine sees it: Name(joinable N/M) per row
//   party invite <name>      HostConnectionService.SendInviteAsync         (the Invite button)
//   party cancel <name>      HostConnectionService.CancelInviteAsync       (the ✕ on a pending row)
//   party accept             PartyInviteController.AcceptInviteAsync      (the invite toast's Accept)
//   party decline            PartyInviteController.DeclineInviteAsync
//   party join <name>        PartyInviteController.JoinPartyAsync          (the Join button)
//   party spectate <name>    PartyInviteController.SpectateAsync           (the eye button)
//   party kick <name>        HostConnectionService.KickPartyMemberAsync    (the ✕ on a member row)
//   party leave              PartyInviteController.LeavePartyAndReturnToMenuAsync
// The async ones return as soon as the call is started; poll `party` for the outcome. A failure is
// a warning (the action a person asked for did not happen), never swallowed.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Text;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Registers and handles the <c>party</c> diagnostics command.</summary>
    public static class PartyConsoleCommand
    {
        public const string CommandName = "party";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() =>
            CosmicShore.Utility.PerformanceBenchmark.DiagnosticsHUD.RegisterCommand(CommandName, Handle);

        /// <summary>Handles <c>party [sub] [name]</c>. Returns the overlay line.</summary>
        public static string Handle(string[] args)
        {
            string sub = args is { Length: > 0 } ? args[0].ToLowerInvariant() : string.Empty;
            string name = args is { Length: > 1 } ? string.Join(" ", args, 1, args.Length - 1) : string.Empty;

            var host = UnityEngine.Object.FindAnyObjectByType<HostConnectionService>();
            var data = host != null ? host.ConnectionData : null;
            if (host == null) return "party: no HostConnectionService loaded";
            if (data == null) return "party: HostConnectionService has no HostConnectionDataSO assigned";

            switch (sub)
            {
                case "":
                    return Describe(host, data);

                case "online":
                    return Online(data);

                case "invite":
                    return WithPlayer(data, name, p => Run($"invite {p.DisplayName}", host.SendInviteAsync(p.PlayerId)));

                case "cancel":
                    return WithPlayer(data, name, p => Run($"cancel {p.DisplayName}", host.CancelInviteAsync(p.PlayerId)));

                case "kick":
                    return WithMember(data, name, p => Run($"kick {p.DisplayName}", host.KickPartyMemberAsync(p.PlayerId)));

                case "accept":
                {
                    var invite = host.LastPendingInvite;
                    if (!invite.HasValue) return "party: no pending invite";
                    return WithController(c => Run($"accept {invite.Value.HostDisplayName}", c.AcceptInviteAsync(invite.Value)));
                }

                case "decline":
                    return WithController(c => Run("decline", c.DeclineInviteAsync()));

                case "join":
                    return WithPlayer(data, name, p => WithController(c => Run($"join {p.DisplayName}", c.JoinPartyAsync(p))));

                case "spectate":
                    return WithPlayer(data, name, p => WithController(c => Run($"spectate {p.DisplayName}", c.SpectateAsync(p))));

                case "leave":
                    return WithController(c => Run("leave", c.LeavePartyAndReturnToMenuAsync()));

                default:
                    return "party: usage - party [online|invite <name>|cancel <name>|accept|decline|join <name>|spectate <name>|kick <name>|leave]";
            }
        }

        /// <summary>
        /// One line of key=value pairs a scenario parses. <c>role</c> is the Netcode role;
        /// <c>members</c> counts DISTINCT player ids against the party size (B25);
        /// <c>conns</c> is the server's connected-client count, <c>humans</c> the part of it that owns a
        /// Ready button (what the ready gate and the rematch tally read) and <c>spectators</c> the rest
        /// (all three 0 on a client).
        /// </summary>
        public static string Describe(HostConnectionService host, HostConnectionDataSO data)
        {
            var nm = NetworkManager.Singleton;
            string role = nm == null || !nm.IsListening ? "none"
                        : nm.IsHost ? "host" : nm.IsServer ? "server" : nm.IsClient ? "client" : "none";
            var query = (IPartyStateQuery)host;
            var invite = host.LastPendingInvite;

            var sb = new StringBuilder("party");
            sb.Append(" role=").Append(role);
            sb.Append(" state=").Append(query.CurrentState);
            sb.Append(" session=").Append(Short(query.ActivePartySessionId));
            sb.Append(" members=").Append(data.DistinctPartyMemberCount).Append('/').Append(data.MaxPartySlots);
            sb.Append(" conns=").Append(nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : 0);
            sb.Append(" humans=").Append(nm != null && nm.IsServer ? SpectatorSession.CountHumanClients(nm) : 0);
            sb.Append(" spectators=").Append(nm != null && nm.IsServer ? SpectatorSession.CountConnectedSpectators(nm) : 0);
            sb.Append(" partyHost=").Append(data.IsPartyHost ? 1 : 0);
            sb.Append(" spectating=").Append(data.IsSpectating ? 1 : 0);
            sb.Append(" offline=").Append(host.IsOfflineSession ? 1 : 0);
            sb.Append(" scene=").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.Append(" invite=").Append(invite.HasValue ? Token(invite.Value.HostDisplayName) : "-");
            sb.Append(" names=").Append(Names(data));
            return sb.ToString();
        }

        static string Online(HostConnectionDataSO data)
        {
            if (data.OnlinePlayers == null || data.OnlinePlayers.Count == 0) return "party online: nobody";
            var sb = new StringBuilder("party online:");
            foreach (var p in data.OnlinePlayers)
            {
                sb.Append(' ').Append(Token(p.DisplayName));
                // The row's own N/M, as polled presence reports it - the number a person reads before
                // pressing Join, and the one the pre-flight refuses on.
                if (p.HasJoinableSession) sb.Append("(joinable ").Append(p.PartyMemberCount).Append('/').Append(p.PartyMaxSlots).Append(')');
            }
            return sb.ToString();
        }

        static string Names(HostConnectionDataSO data)
        {
            if (data.PartyMembers == null || data.PartyMembers.Count == 0) return "-";
            var sb = new StringBuilder();
            foreach (var m in data.PartyMembers)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(Token(m.DisplayName));
            }
            return sb.ToString();
        }

        static string WithPlayer(HostConnectionDataSO data, string name, Func<PartyPlayerData, string> act)
        {
            if (string.IsNullOrEmpty(name)) return "party: name required";
            if (data.OnlinePlayers != null)
                foreach (var p in data.OnlinePlayers)
                    if (string.Equals(p.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return act(p);
            return $"party: '{name}' is not in the online list";
        }

        static string WithMember(HostConnectionDataSO data, string name, Func<PartyPlayerData, string> act)
        {
            if (string.IsNullOrEmpty(name)) return "party: name required";
            if (data.PartyMembers != null)
                foreach (var p in data.PartyMembers)
                    if (string.Equals(p.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return act(p);
            return $"party: '{name}' is not in this party";
        }

        static string WithController(Func<PartyInviteController, string> act)
        {
            var controller = PartyInviteController.Instance;
            return controller == null ? "party: PartyInviteController not available" : act(controller);
        }

        /// <summary>Starts <paramref name="task"/> and reports its end on the Party channel (or as a warning).</summary>
        static string Run(string what, UniTask task)
        {
            Await(what, task).Forget();
            return $"party: {what} started";
        }

        static async UniTaskVoid Await(string what, UniTask task)
        {
            try
            {
                await task;
                CSDebug.LogVerbose(CSLogChannel.Party, $"[PartyConsoleCommand] {what} finished");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[PartyConsoleCommand] {what} failed ({e.GetType().Name}): {e.Message}");
            }
        }

        static string Short(string id) => string.IsNullOrEmpty(id) ? "-" : id.Length > 8 ? id.Substring(0, 8) : id;

        /// <summary>A display name as one token (spaces would break key=value parsing).</summary>
        static string Token(string s) => string.IsNullOrEmpty(s) ? "?" : s.Replace(' ', '_');
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// JoinTargetValidator.cs
// The zero-request pre-flight every Accept / Join / Spectate runs BEFORE the local host is
// torn down.
//
// WHY (Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md §3.7):
//   Both join flows leave the local party session - and PartyInviteController shuts the local
//   NetworkManager down even earlier - before anything has checked that the target can still be
//   joined. A party that filled, a sender who went offline, a session the host recreated, all
//   cost the player their own session and the full RecoverFromFailedTransitionAsync bounce
//   (scene reload, new Relay allocation) to learn what the presence lobby already knew.
//
//   The check reads only the freshest POLLED knowledge - HostConnectionDataSO.OnlinePlayers, the
//   per-player partySession / partyCount / partyMax / matchName properties every peer publishes -
//   so it issues no request and is at most one poll interval stale. It catches the common
//   failures (stale invite, full party, target offline, spectating a lobby); a host whose
//   transport died silently still costs a bounce until Phase 3's reconnection grace.
//
// Pure static, engine-free: PartyPlayerData is a plain serializable struct, so the rule runs
// headlessly in UgsRequestPolicyTests' sibling suite.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>Why a join target was refused before teardown, or <see cref="Ok"/>.</summary>
    public enum JoinTargetVerdict
    {
        Ok             = 0,
        /// <summary>This client is in an offline session - no Relay, no party to join.</summary>
        OfflineSession = 1,
        /// <summary>The caller handed us no session id at all.</summary>
        NoSession      = 2,
        /// <summary>The target is not in the presence lobby any more (left, crashed, offline).</summary>
        TargetOffline  = 3,
        /// <summary>The target advertises a different session than the one we were about to join (recreated, moved, spectating).</summary>
        SessionChanged = 4,
        /// <summary>The target's party shows no free seat by the displayed rule.</summary>
        PartyFull      = 5,
        /// <summary>A spectate was asked for a target who is not in a match.</summary>
        NotInMatch     = 6,
    }

    public static class JoinTargetValidator
    {
        /// <summary>
        /// Decides whether joining <paramref name="targetPlayerId"/>'s session
        /// <paramref name="expectedSessionId"/> can still succeed, from the presence lobby's view.
        /// <paramref name="onlinePlayers"/> is <see cref="HostConnectionDataSO.OnlinePlayers"/>;
        /// a <c>null</c> list means "unknown", never "nobody", and passes.
        /// </summary>
        /// <returns>The verdict; <paramref name="message"/> is the player-facing sentence for a refusal (empty for Ok).</returns>
        public static JoinTargetVerdict Validate(
            IEnumerable<PartyPlayerData> onlinePlayers,
            string targetPlayerId,
            string expectedSessionId,
            bool asSpectator,
            bool offlineSession,
            out string message)
        {
            message = string.Empty;

            if (offlineSession)
            {
                message = asSpectator ? "Offline session - spectating is unavailable." : "Offline session - joining a party is unavailable.";
                return JoinTargetVerdict.OfflineSession;
            }
            if (string.IsNullOrEmpty(expectedSessionId))
            {
                message = asSpectator ? "There is no match to spectate." : "That party has no joinable session.";
                return JoinTargetVerdict.NoSession;
            }
            if (onlinePlayers == null || string.IsNullOrEmpty(targetPlayerId))
                return JoinTargetVerdict.Ok; // unknown, not refused - the join itself will tell

            PartyPlayerData target = default;
            bool found = false;
            foreach (var p in onlinePlayers)
            {
                if (p.PlayerId != targetPlayerId) continue;
                target = p;
                found = true;
                break;
            }
            if (!found)
            {
                message = "That pilot is no longer online.";
                return JoinTargetVerdict.TargetOffline;
            }

            string name = string.IsNullOrEmpty(target.DisplayName) ? "That pilot" : target.DisplayName;
            if (target.PartySessionId != expectedSessionId)
            {
                message = $"{name}'s party is no longer available.";
                return JoinTargetVerdict.SessionChanged;
            }
            if (asSpectator)
            {
                if (!target.IsInMatch)
                {
                    message = $"{name} is not in a match to spectate.";
                    return JoinTargetVerdict.NotInMatch;
                }
                return JoinTargetVerdict.Ok;
            }
            if (target.PartyMaxSlots > 0 && target.PartyMemberCount >= target.PartyMaxSlots)
            {
                message = $"{name}'s party is full.";
                return JoinTargetVerdict.PartyFull;
            }
            return JoinTargetVerdict.Ok;
        }
    }
}

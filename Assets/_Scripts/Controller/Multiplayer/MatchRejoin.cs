using System;
using System.Collections.Generic;
using System.Text;
using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rejoining a running match (Docs/MENU_REDESIGN_B.md section 103; Shombith, 2026-10-29: "If some one leaves a game and
    /// then they boot up while the game is running the game should ask the user if they want to rejoin that game").
    ///
    /// <para><b>What the game already did.</b> A guest who drops mid-match is bounced to their own menu
    /// (<c>PartyInviteController.HandleHostLossAsync</c>) while the host keeps their Player (and its score) and hands the
    /// vessel to an AI pilot (<c>ServerPlayerVesselInitializer.ConvertPlayerToAI</c>). Nothing remembered the match and
    /// nothing gave the ship back.</para>
    ///
    /// <para><b>This adds the three missing parts.</b> The guest REMEMBERS the match (session id, mode) in PlayerPrefs,
    /// so it survives a crash or a restart. A rejoin ANNOUNCES itself in the approval payload (the spectator precedent,
    /// <see cref="SpectatorSession"/>) with the player's UGS id. The host then HANDS THE SEAT BACK instead of minting a
    /// second Player: no Player object at approval, and on connect the AI-flown Player whose <c>NetUgsPlayerId</c>
    /// matches is returned to the client (<c>ServerPlayerVesselInitializer.TryReclaimSeat</c>). A rejoin with no seat
    /// to take back is refused at approval, so it never becomes a half-joined client.</para>
    ///
    /// <para>Only a GUEST can rejoin: the host is a player and the match ends with them.</para>
    /// </summary>
    public static class MatchRejoin
    {
        public const string PayloadPrefix = "cosmicshore.rejoin.v1:";
        const string PrefsKey = "cosmicshore.rejoin.record";

        /// <summary>A record older than this is never offered (a match is minutes long).</summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

        /// <summary>The match this machine last played as a guest and has not finished or left.</summary>
        [Serializable]
        public struct Record
        {
            public string sessionId;
            public int mode;
            public long savedAtUtcTicks;
            /// <summary>Set when the link broke (the bounce), so the window can say "You dropped out".</summary>
            public long droppedAtUtcTicks;

            public GameModes Mode => (GameModes)mode;
            public DateTime SavedAtUtc => new(savedAtUtcTicks, DateTimeKind.Utc);
            public bool Dropped => droppedAtUtcTicks > 0;
        }

        // ── Client: the record ──────────────────────────────────────────────

        /// <summary>Remember the running match (a guest, when the host's config arrives in the game scene).</summary>
        public static void Remember(string sessionId, GameModes mode)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            var r = new Record { sessionId = sessionId, mode = (int)mode, savedAtUtcTicks = DateTime.UtcNow.Ticks };
            Save(r);
        }

        /// <summary>The link broke while a match was remembered: keep it, and say so.</summary>
        public static void MarkDropped()
        {
            if (!TryLoad(out var r)) return;
            r.droppedAtUtcTicks = DateTime.UtcNow.Ticks;
            Save(r);
        }

        /// <summary>The match ended, or the player left it on purpose: nothing to rejoin.</summary>
        public static void Forget()
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return;
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>The remembered match, if there is one and it is not too old.</summary>
        public static bool TryLoad(out Record record) => TryLoad(DateTime.UtcNow, out record);

        public static bool TryLoad(DateTime nowUtc, out Record record)
        {
            record = default;
            var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return false;
            try { record = JsonUtility.FromJson<Record>(json); }
            catch (ArgumentException) { Forget(); return false; }
            if (string.IsNullOrEmpty(record.sessionId) || nowUtc - record.SavedAtUtc > MaxAge)
            {
                Forget();
                return false;
            }
            return true;
        }

        static void Save(Record r)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(r));
            PlayerPrefs.Save();
        }

        // ── Client: the approval payload ────────────────────────────────────

        public static byte[] PayloadFor(string ugsPlayerId) => Encoding.UTF8.GetBytes(PayloadPrefix + ugsPlayerId);

        /// <summary>Announce the rejoin in the next connection's approval payload.</summary>
        public static void ArmApprovalPayload(string ugsPlayerId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null || string.IsNullOrEmpty(ugsPlayerId)) return;
            nm.NetworkConfig.ConnectionData = PayloadFor(ugsPlayerId);
        }

        /// <summary>Remove the rejoin token from the payload if it is the one armed.</summary>
        public static void ClearApprovalPayload()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.NetworkConfig == null) return;
            if (TryParsePayload(nm.NetworkConfig.ConnectionData, out _))
                nm.NetworkConfig.ConnectionData = Array.Empty<byte>();
        }

        /// <summary>The UGS player id a rejoin payload carries, or false when the payload is not a rejoin.</summary>
        public static bool TryParsePayload(byte[] payload, out string ugsPlayerId)
        {
            ugsPlayerId = null;
            if (payload == null || payload.Length <= PayloadPrefix.Length || payload.Length > 256) return false;
            string s;
            try { s = Encoding.UTF8.GetString(payload); }
            catch (ArgumentException) { return false; }
            if (!s.StartsWith(PayloadPrefix, StringComparison.Ordinal)) return false;
            ugsPlayerId = s.Substring(PayloadPrefix.Length);
            return ugsPlayerId.Length > 0;
        }

        // ── Server (host) side ──────────────────────────────────────────────

        static readonly Dictionary<ulong, string> ServerPending = new();

        /// <summary>A client approved as a rejoin, waiting for its seat. Server only.</summary>
        public static void ServerRegister(ulong clientId, string ugsPlayerId) => ServerPending[clientId] = ugsPlayerId;

        /// <summary>Take (and forget) the rejoin a connected client announced. Server only.</summary>
        public static bool ServerTake(ulong clientId, out string ugsPlayerId)
        {
            if (!ServerPending.TryGetValue(clientId, out ugsPlayerId)) return false;
            ServerPending.Remove(clientId);
            return true;
        }

        /// <summary>A pending rejoin that disconnected before it was seated. Server only.</summary>
        public static void ServerUnregister(ulong clientId) => ServerPending.Remove(clientId);

        /// <summary>
        /// The AI-flown seat a returning player may take back: a spawned Player the host handed to the AI whose UGS id is
        /// <paramref name="ugsPlayerId"/>. Null when there is none (the match moved on, or they never played it).
        /// </summary>
        public static Player FindReclaimableSeat(IEnumerable<IPlayer> players, string ugsPlayerId)
        {
            if (players == null || string.IsNullOrEmpty(ugsPlayerId)) return null;
            foreach (var p in players)
                if (p is Player player && player && player.IsSpawned && player.NetIsAI.Value && player.UgsPlayerId == ugsPlayerId)
                    return player;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ServerPending.Clear();
    }
}

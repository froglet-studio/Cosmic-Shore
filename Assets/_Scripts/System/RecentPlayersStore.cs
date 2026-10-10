using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// One pilot the local player shared a multiplayer match with. Plain serializable fields: this
    /// is what <see cref="DataAccessor"/> writes to disk.
    /// </summary>
    [Serializable]
    public struct RecentPlayerRecord
    {
        /// <summary>The pilot's UGS player id: the identity a friend request is sent to.</summary>
        public string PlayerId;
        public string DisplayName;
        public int AvatarId;
        /// <summary>UTC ticks of the most recent match end shared with this pilot.</summary>
        public long LastPlayedUtcTicks;

        public DateTime GetLastPlayedUtc() => new DateTime(LastPlayedUtcTicks, DateTimeKind.Utc);
    }

    /// <summary>
    /// The store's view of one seat at match end: just the facts it needs to decide whether the
    /// seat is somebody worth remembering. Built from <see cref="IPlayer"/> by
    /// <see cref="RecentPlayersStore.RecordMatchEnd"/>; built by hand in tests.
    /// </summary>
    public readonly struct MatchParticipant
    {
        public readonly string PlayerId;
        public readonly string DisplayName;
        public readonly int AvatarId;
        /// <summary>An AI seat (never recorded, whatever id it carries).</summary>
        public readonly bool IsAI;
        /// <summary>The human on THIS machine (never recorded: you did not play with yourself).</summary>
        public readonly bool IsLocal;

        public MatchParticipant(string playerId, string displayName, int avatarId, bool isAI, bool isLocal)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            AvatarId = avatarId;
            IsAI = isAI;
            IsLocal = isLocal;
        }
    }

    /// <summary>
    /// "Recently played with": the OTHER humans of every completed multiplayer match, newest first,
    /// de-duplicated by player id and capped by <see cref="RecentPlayersConfigSO.MaxEntries"/>.
    /// The friends panel's RECENT section reads <see cref="Entries"/> and offers an add-friend
    /// button per row, which is the path from a good match with a stranger to a friend request.
    ///
    /// <para>Recorded once per match end from <c>GameDataSO.InvokeMiniGameEnd</c> (which every
    /// peer raises, so every machine remembers the match), from the authoritative roster the game
    /// already has (<c>GameDataSO.Players</c>). Only humans with a UGS id are kept: AI seats carry
    /// no id and are flagged, the local pilot is excluded by flag AND by id, and a single-player
    /// game simply records nobody.</para>
    ///
    /// <para>Local JSON through <see cref="DataAccessor"/>, like the favourites and loadouts: this
    /// is a memory of who you met, not a relationship, so it is not Cloud Save data. The cap and
    /// file name live on <see cref="RecentPlayersConfigSO"/>. The list-shaping functions
    /// (<see cref="SelectOthers"/>, <see cref="Merge"/>, <see cref="FormatLastPlayed"/>) are pure
    /// and static so the edit-mode tests cover them without touching the disk.</para>
    /// </summary>
    public static class RecentPlayersStore
    {
        static List<RecentPlayerRecord> _records;
        static RecentPlayersConfigSO _config;
        static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _records = null;
            _config = null;
            _initialized = false;
        }

        static void Init()
        {
            _config = RecentPlayersConfigSO.LoadOrDefault();
            _records = DataAccessor.Load<List<RecentPlayerRecord>>(_config.SaveFileName) ?? new List<RecentPlayerRecord>();
            _records.RemoveAll(r => string.IsNullOrEmpty(r.PlayerId));
            _initialized = true;
        }

        /// <summary>Every remembered pilot, newest first. Never null; empty until the first online match.</summary>
        public static IReadOnlyList<RecentPlayerRecord> Entries
        {
            get
            {
                if (!_initialized) Init();
                return _records;
            }
        }

        /// <summary>
        /// Match-end entry point: remember every other human on <paramref name="roster"/>.
        /// Returns how many pilots were recorded (0 for a solo or AI-only game). Persists when
        /// anything changed.
        /// </summary>
        public static int RecordMatchEnd(IReadOnlyList<IPlayer> roster)
        {
            if (roster == null || roster.Count == 0) return 0;

            var seats = new List<MatchParticipant>(roster.Count);
            foreach (var player in roster)
            {
                if (player == null) continue;
                // UgsPlayerId allocates per read (see IPlayer); this runs once per match end, which is fine.
                seats.Add(new MatchParticipant(
                    player.UgsPlayerId,
                    player.Name,
                    player.AvatarId,
                    player.IsInitializedAsAI,
                    player.IsLocalPilot));
            }

            return RecordMatch(seats, DateTime.UtcNow);
        }

        /// <summary>
        /// Remember the other humans among <paramref name="seats"/> as of <paramref name="nowUtc"/>.
        /// Returns how many were recorded; persists when that is more than zero.
        /// </summary>
        public static int RecordMatch(IEnumerable<MatchParticipant> seats, DateTime nowUtc)
        {
            if (!_initialized) Init();

            var others = SelectOthers(seats);
            if (others.Count == 0) return 0;

            int recorded = Merge(_records, others, nowUtc, _config.MaxEntries);
            DataAccessor.Save(_config.SaveFileName, _records);

            CSDebug.LogVerbose(CSLogChannel.Party,
                $"[RecentPlayers] Recorded {recorded} pilot(s) from the match; list holds {_records.Count}/{_config.MaxEntries}.");
            return recorded;
        }

        /// <summary>Forget everything. Tooling and test hygiene; nothing in the UI calls it.</summary>
        public static void Clear()
        {
            if (!_initialized) Init();
            _records.Clear();
            DataAccessor.Save(_config.SaveFileName, _records);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pure list shaping (edit-mode tested)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The seats worth remembering: humans with a UGS id that are not the local pilot. The local
        /// pilot is dropped by flag and, in case the roster carries a second object for the same
        /// account (a stale pre-party shadow), by id as well. A pilot seated twice is kept once.
        /// </summary>
        public static List<MatchParticipant> SelectOthers(IEnumerable<MatchParticipant> seats)
        {
            var result = new List<MatchParticipant>();
            if (seats == null) return result;

            string localId = null;
            var candidates = new List<MatchParticipant>();
            foreach (var seat in seats)
            {
                if (seat.IsLocal)
                {
                    if (!string.IsNullOrEmpty(seat.PlayerId)) localId ??= seat.PlayerId;
                    continue;
                }
                if (seat.IsAI || string.IsNullOrEmpty(seat.PlayerId)) continue;
                candidates.Add(seat);
            }

            var seen = new HashSet<string>();
            foreach (var seat in candidates)
            {
                if (seat.PlayerId == localId) continue;
                if (!seen.Add(seat.PlayerId)) continue;
                result.Add(seat);
            }
            return result;
        }

        /// <summary>
        /// Fold <paramref name="others"/> into <paramref name="records"/> (newest first): a pilot
        /// already present is refreshed (name, avatar, time) and moved to the front rather than
        /// duplicated; the list is then cut to <paramref name="maxEntries"/>, dropping the oldest.
        /// Returns how many of <paramref name="others"/> ended up on the list.
        /// </summary>
        public static int Merge(List<RecentPlayerRecord> records, IReadOnlyList<MatchParticipant> others, DateTime nowUtc, int maxEntries)
        {
            if (records == null || others == null || others.Count == 0) return 0;
            maxEntries = Mathf.Max(1, maxEntries);
            long ticks = nowUtc.ToUniversalTime().Ticks;

            // Walk the match's pilots backwards so that, inserted one by one at the front, they end
            // up in roster order at the head of the list.
            int touched = 0;
            for (int i = others.Count - 1; i >= 0; i--)
            {
                var seat = others[i];
                if (string.IsNullOrEmpty(seat.PlayerId)) continue;

                records.RemoveAll(r => r.PlayerId == seat.PlayerId);
                records.Insert(0, new RecentPlayerRecord
                {
                    PlayerId = seat.PlayerId,
                    DisplayName = string.IsNullOrEmpty(seat.DisplayName) ? "Unknown Pilot" : seat.DisplayName,
                    AvatarId = seat.AvatarId,
                    LastPlayedUtcTicks = ticks,
                });
                touched++;
            }

            if (records.Count > maxEntries)
                records.RemoveRange(maxEntries, records.Count - maxEntries);

            return Mathf.Min(touched, maxEntries);
        }

        /// <summary>
        /// The row label's time: "JUST NOW", "5 MIN AGO", "3 HR AGO", "2 DAYS AGO", "3 WEEKS AGO".
        /// Upper case to match the panel's other status labels.
        /// </summary>
        public static string FormatLastPlayed(DateTime lastPlayedUtc, DateTime nowUtc)
        {
            var elapsed = nowUtc.ToUniversalTime() - lastPlayedUtc.ToUniversalTime();
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

            if (elapsed.TotalMinutes < 1) return "JUST NOW";
            if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes} MIN AGO";
            if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours} HR AGO";
            if (elapsed.TotalDays < 7)
            {
                int days = (int)elapsed.TotalDays;
                return days == 1 ? "1 DAY AGO" : $"{days} DAYS AGO";
            }
            int weeks = (int)(elapsed.TotalDays / 7);
            return weeks == 1 ? "1 WEEK AGO" : $"{weeks} WEEKS AGO";
        }
    }
}

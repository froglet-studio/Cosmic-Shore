using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
// ALIASED, not imported. `CosmicShore.Data` also has a `LeaderboardEntry` (and PlayFab has a
// third), so importing the Models namespace makes the name ambiguous the moment this file uses
// it - which is the collision WeeklyChallengeRanking's own docs exist to warn about, hit here.
// An alias names the ONE type this file means and leaves every other name alone.
using UgsLeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The weekly challenge's leaderboard: <b>who finished this week's objective fastest.</b>
    ///
    /// <para><b>The score is a TIME, and only a COMPLETION earns one.</b> Every weekly challenge is
    /// "reach N of something", so the one thing worth ranking is how long it took — and a player
    /// who never reached the target has no time, not a slow one. Submitting a sentinel for them
    /// would either rank people who never finished above people who did, or bury the real times
    /// under a wall of identical placeholders; either way the reward tiers at the end of the week
    /// would be computed off a list that is mostly not a ranking. So: complete it, and you are on
    /// the board; don't, and you are not.</para>
    ///
    /// <para><b>ONE leaderboard, reset weekly by UGS — not one leaderboard per week.</b> The SDK
    /// cannot create leaderboards, so a per-week id would need a server job minting them forever.
    /// The dashboard's own reset schedule does it, and its <i>archive on reset</i> is what a reward
    /// pass reads when the week closes. See the setup contract in the remarks below: three of those
    /// settings live in the dashboard and NOTHING in this code can enforce them, which is why the
    /// one that fails silently is checked at runtime instead.</para>
    ///
    /// <para>Rewards are deliberately out of scope here. This service ranks; the reward system
    /// being built separately reads the archive.</para>
    /// </summary>
    /// <remarks>
    /// <b>UGS dashboard setup (Leaderboards ▸ the id authored on the catalog):</b>
    /// <list type="bullet">
    /// <item><b>Sort order: ASCENDING.</b> The score is a time, so the fastest run is the smallest
    /// number. Getting this wrong ranks the SLOWEST player first and looks completely normal —
    /// which is why <see cref="FetchTopAsync"/> checks the order it got back and screams once.</item>
    /// <item><b>Update strategy: KEEP BEST.</b> "Best" is relative to the sort order, so with
    /// ascending it keeps the fastest. One attempt a week makes this almost moot; under test mode's
    /// unlimited attempts it is what stops a practice run from overwriting a good one.</item>
    /// <item><b>Reset: weekly, on the same UTC Monday boundary as
    /// <see cref="WeeklyChallengeCatalogSO.WeekStartUtc"/>, with ARCHIVING ON.</b> The archive is
    /// the only record of who won a week once the board has reset.</item>
    /// </list>
    /// A code-side workaround for the sort order — submitting <c>BIG - time</c> so a descending
    /// board ranks correctly — was considered and rejected: it makes every raw score in the
    /// dashboard, in every export, and in the archive the reward pass reads a number nobody can
    /// interpret, to save one dashboard setting.
    /// </remarks>
    public class WeeklyChallengeLeaderboardService
    {
        /// <summary>Rows a page fetch asks for by default — the mock-up's list is four.</summary>
        public const int DefaultPageSize = 10;

        readonly Func<string> _leaderboardId;
        readonly Func<bool> _isOffline;
        readonly Func<string, string> _regionalLeaderboardId;
        readonly Func<IReadOnlyList<string>> _friendIds;
        readonly Func<int> _localAvatarId;

        bool _warnedNoId;
        bool _warnedSortOrder;

        /// <param name="leaderboardId">Resolved late, not captured: the catalog can be reloaded,
        /// and an id read once at construction would outlive the asset it came from.</param>
        /// <param name="isOffline">The session's own offline flag. Read late for the same reason —
        /// a session can go offline after this service exists.</param>
        /// <param name="regionalLeaderboardId">Region key → that region's board id, or null. Late
        /// for the same reason as the world id.</param>
        /// <param name="friendIds">The signed-in player's friends' UGS ids. Null = the Friends
        /// scope reports "no friends service" rather than an empty board, which are different
        /// states and read differently to a player.</param>
        /// <param name="localAvatarId">The local profile's icon id, stamped into a submitted
        /// score's metadata so a leaderboard row can show a face. See
        /// <see cref="WeeklyChallengeRanking.AvatarId"/>.</param>
        public WeeklyChallengeLeaderboardService(
            Func<string> leaderboardId,
            Func<bool> isOffline = null,
            Func<string, string> regionalLeaderboardId = null,
            Func<IReadOnlyList<string>> friendIds = null,
            Func<int> localAvatarId = null)
        {
            _leaderboardId = leaderboardId;
            _isOffline = isOffline;
            _regionalLeaderboardId = regionalLeaderboardId;
            _friendIds = friendIds;
            _localAvatarId = localAvatarId;
        }

        /// <summary>
        /// The board a scope reads, or null when that scope has none configured. Public so the
        /// view can grey a tab out BEFORE a fetch rather than after one comes back empty — an
        /// unconfigured tab and an empty board look identical once the fetch has run.
        /// </summary>
        public string BoardIdFor(LeaderboardScope scope)
        {
            switch (scope)
            {
                case LeaderboardScope.Regional:
                    string region = WeeklyChallengeRegion.Current;
                    return string.IsNullOrEmpty(region)
                        ? null
                        : _regionalLeaderboardId?.Invoke(region);

                // Friends ranks friends against each other ON THE WORLD BOARD - it is a lookup of
                // specific player ids, not a separate board, so a friend's time is the same time
                // it is everywhere else.
                default:
                    return _leaderboardId?.Invoke();
            }
        }

        /// <summary>
        /// Whether a scope can be asked at all right now. A tab that cannot answer should say so
        /// up front; see <see cref="UnavailableReason"/> for the words.
        /// </summary>
        public bool IsScopeAvailable(LeaderboardScope scope)
        {
            if (string.IsNullOrWhiteSpace(BoardIdFor(scope))) return false;
            if (scope == LeaderboardScope.Friends && _friendIds == null) return false;
            return true;
        }

        /// <summary>Why a scope is unavailable, in words a player can read. Empty when it is fine.</summary>
        public string UnavailableReason(LeaderboardScope scope)
        {
            if (IsScopeAvailable(scope)) return string.Empty;

            switch (scope)
            {
                case LeaderboardScope.Regional:
                    return string.IsNullOrEmpty(WeeklyChallengeRegion.Current)
                        ? "REGION UNKNOWN"
                        : "NO BOARD FOR YOUR REGION";
                case LeaderboardScope.Friends:
                    return _friendIds == null ? "FRIENDS UNAVAILABLE" : "NO RANKING YET";
                default:
                    return "NO LEADERBOARD CONFIGURED";
            }
        }

        /// <summary>
        /// Submit a completion time, in seconds.
        ///
        /// <para>Fire-and-forget by design: a leaderboard entry is a claim about a live ranking,
        /// not progress to be replayed later, so a failure is logged and dropped rather than
        /// queued — the same rule <c>UGSStatsManager</c> already applies to mode scores. The
        /// player's own record of the run lives in Cloud Save and is unaffected either way.</para>
        /// </summary>
        public async UniTask SubmitCompletionAsync(double seconds, CancellationToken ct = default)
        {
            if (!TryResolveId(out string id)) return;
            if (!IsUsable("submit")) return;
            if (seconds < 0d || double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                CSDebug.LogWarning($"[WeeklyChallengeLeaderboard] Refusing to submit a nonsense time ({seconds}s).");
                return;
            }

            var options = BuildSubmitOptions();

            await SubmitToBoardAsync(id, seconds, options, ct);

            // The regional board is a SECOND submission of the same run, not a different score.
            // It is separate because UGS has no region concept (WeeklyChallengeRegion), and it is
            // awaited rather than fired alongside so a failure on one cannot cancel the other.
            string regional = BoardIdFor(LeaderboardScope.Regional);
            if (!string.IsNullOrWhiteSpace(regional) && regional != id)
                await SubmitToBoardAsync(regional, seconds, BuildSubmitOptions(), ct);
        }

        async UniTask SubmitToBoardAsync(
            string id, double seconds, AddPlayerScoreOptions options, CancellationToken ct)
        {
            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(id, seconds, options).AsMainThread();
                ct.ThrowIfCancellationRequested();

                CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                    $"[WeeklyChallengeLeaderboard] Submitted {WeeklyChallengeRanking.FormatSeconds(seconds)} to '{id}'.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[WeeklyChallengeLeaderboard] Submit failed for '{id}': {ex.Message}");
            }
        }

        /// <summary>
        /// Metadata is the ONLY field a score carries besides its number, and the one thing worth
        /// spending it on is the avatar: UGS returns a name and an id, so without this a
        /// leaderboard row can never show a face (see <see cref="WeeklyChallengeRanking.AvatarId"/>).
        ///
        /// <para>Null when there is no avatar to state, rather than <c>{"a":-1}</c> — an absent
        /// field and a field saying "nothing" read the same on the way back, and the shorter one
        /// does not have to be parsed.</para>
        /// </summary>
        AddPlayerScoreOptions BuildSubmitOptions()
        {
            int avatar = _localAvatarId?.Invoke() ?? WeeklyChallengeRanking.NoAvatar;
            if (avatar < 0) return null;

            return new AddPlayerScoreOptions
            {
                Metadata = new Dictionary<string, object>
                {
                    { WeeklyChallengeRanking.AvatarMetadataKey, avatar },
                },
            };
        }

        /// <summary>
        /// The top <paramref name="limit"/> rows of the WORLD board, fastest first. Kept as the
        /// no-argument overload because most callers want exactly this.
        /// </summary>
        public UniTask<List<WeeklyChallengeRanking>> FetchTopAsync(
            int limit = DefaultPageSize, CancellationToken ct = default) =>
            FetchAsync(LeaderboardScope.World, limit, ct);

        /// <summary>
        /// The rows for one scope, fastest first. Empty on any failure — a leaderboard that cannot
        /// be read is a panel with nothing in it, never an exception crossing into the UI.
        ///
        /// <para>Each scope is a DIFFERENT REQUEST, not a filter over one answer
        /// (<see cref="LeaderboardScope"/>): World is a page, Regional is a page of another board,
        /// and Friends is a lookup of specific ids.</para>
        /// </summary>
        public async UniTask<List<WeeklyChallengeRanking>> FetchAsync(
            LeaderboardScope scope, int limit = DefaultPageSize, CancellationToken ct = default)
        {
            var rows = new List<WeeklyChallengeRanking>();

            string id = BoardIdFor(scope);
            if (!TryResolveId(scope, id)) return rows;
            if (!IsUsable("fetch")) return rows;

            limit = Mathf.Max(1, limit);
            string localId = LocalPlayerId();

            try
            {
                if (scope == LeaderboardScope.Friends)
                {
                    await FetchFriendsAsync(id, limit, localId, rows, ct);
                }
                else
                {
                    await FetchPageAsync(id, limit, localId, rows, ct);

                    // Only a PAGE is diagnostic: the friends rows are sorted here, so they are
                    // ascending whatever the board does and would report a broken board as fine.
                    WarnIfSortedWrong(id, rows);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[WeeklyChallengeLeaderboard] Fetch failed for '{id}': {ex.Message}");
            }

            return rows;
        }

        async UniTask FetchPageAsync(
            string id, int limit, string localId, List<WeeklyChallengeRanking> rows, CancellationToken ct)
        {
            var page = await LeaderboardsService.Instance
                .GetScoresAsync(id, new GetScoresOptions { Limit = limit, IncludeMetadata = true })
                .AsMainThread();
            ct.ThrowIfCancellationRequested();

            if (page?.Results == null) return;

            foreach (var entry in page.Results)
            {
                if (entry == null) continue;
                if (rows.Count >= limit) break;
                rows.Add(ToRanking(entry, localId));
            }
        }

        /// <summary>
        /// Friends ranked among THEMSELVES. It is a lookup by player id on the world board, so a
        /// friend's time is the same time it is everywhere else — only the company changes.
        ///
        /// <para><b>The ranks are re-numbered 1..n.</b> UGS returns each player's WORLD rank, and a
        /// friends list showing 1st, 4th, 812th is a world board with most of the rows missing
        /// rather than a friends board. The world rank is not lost — it is simply not what this
        /// tab is answering.</para>
        ///
        /// <para>The local player is included: a friends board you are not on cannot tell you
        /// whether you are beating your friends, which is the only reason to open it.</para>
        /// </summary>
        async UniTask FetchFriendsAsync(
            string id, int limit, string localId, List<WeeklyChallengeRanking> rows, CancellationToken ct)
        {
            var ids = new List<string>();
            var friends = _friendIds?.Invoke();
            if (friends != null)
            {
                foreach (string friendId in friends)
                    if (!string.IsNullOrWhiteSpace(friendId) && !ids.Contains(friendId))
                        ids.Add(friendId);
            }
            if (!string.IsNullOrEmpty(localId) && !ids.Contains(localId))
                ids.Add(localId);

            if (ids.Count == 0) return;

            var page = await LeaderboardsService.Instance
                .GetScoresByPlayerIdsAsync(id, ids, new GetScoresByPlayerIdsOptions { IncludeMetadata = true })
                .AsMainThread();
            ct.ThrowIfCancellationRequested();

            if (page?.Results == null) return;

            var found = new List<WeeklyChallengeRanking>();
            foreach (var entry in page.Results)
            {
                // A friend who has not completed the challenge has NO entry, so this list is
                // shorter than the friends list by design - the same rule as the world board.
                if (entry == null) continue;
                found.Add(ToRanking(entry, localId));
            }

            found.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));

            for (int i = 0; i < found.Count && rows.Count < limit; i++)
            {
                var row = found[i];
                row.Rank = i + 1;
                rows.Add(row);
            }
        }

        static WeeklyChallengeRanking ToRanking(UgsLeaderboardEntry entry, string localId) =>
            new()
            {
                Rank = entry.Rank + 1,      // UGS ranks from 0; a player reads from 1
                PlayerId = entry.PlayerId,
                PlayerName = StripNameSuffix(entry.PlayerName),
                Seconds = entry.Score,
                IsLocalPlayer = !string.IsNullOrEmpty(localId) && entry.PlayerId == localId,
                AvatarId = WeeklyChallengeRanking.ReadAvatarIdFromMetadata(entry.Metadata),
            };

        /// <summary>
        /// <b>A leaderboard sorted the wrong way looks completely normal.</b> The rows are real,
        /// the names are real, the times are real — they are simply in the opposite order, so the
        /// slowest run in the world sits at rank 1. Nothing in this code can set the dashboard's
        /// sort order, so the next best thing is to notice: UGS returns rows in RANK order, so a
        /// correctly-configured board hands back non-decreasing times.
        ///
        /// <para>Warned ONCE per session, not per fetch — the panel refreshes, and a
        /// misconfiguration that logs on a timer teaches people to filter the log.</para>
        /// </summary>
        void WarnIfSortedWrong(string id, List<WeeklyChallengeRanking> rows)
        {
            if (_warnedSortOrder || rows.Count < 2) return;

            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Seconds >= rows[i - 1].Seconds) continue;

                _warnedSortOrder = true;
                CSDebug.LogError(
                    $"[WeeklyChallengeLeaderboard] '{id}' is returning times in DESCENDING order " +
                    $"({WeeklyChallengeRanking.FormatSeconds(rows[i - 1].Seconds)} ranked above " +
                    $"{WeeklyChallengeRanking.FormatSeconds(rows[i].Seconds)}). The score is a TIME, " +
                    "so this board is ranking the SLOWEST player first. Set its Sort Order to " +
                    "ASCENDING in the UGS dashboard — nothing in the game can fix this.");
                return;
            }
        }

        // ── Guards ─────────────────────────────────────────────────────────────

        bool TryResolveId(out string id)
        {
            id = _leaderboardId?.Invoke();
            return TryResolveId(LeaderboardScope.World, id);
        }

        /// <summary>
        /// A missing WORLD id is a misconfiguration worth one warning. A missing REGIONAL id is
        /// not — most projects will never author one, and the tab already says so on screen.
        /// </summary>
        bool TryResolveId(LeaderboardScope scope, string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) return true;

            if (scope != LeaderboardScope.World)
            {
                CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                    $"[WeeklyChallengeLeaderboard] No board configured for the {scope} scope.");
                return false;
            }

            if (!_warnedNoId)
            {
                _warnedNoId = true;
                CSDebug.LogWarning(
                    "[WeeklyChallengeLeaderboard] No leaderboard id authored on the weekly challenge " +
                    "catalog (FrogletTools > Game Modes > Weekly Challenge). Ranking is off until one " +
                    "is set - the challenge itself is unaffected.");
            }
            return false;
        }

        /// <summary>
        /// Offline and un-signed-in are DIFFERENT states and neither is an error worth shouting
        /// about: offline is a supported way to play (Docs/OFFLINE_MODE.md) and sign-in may simply
        /// not have finished. Both just mean there is no ranking to take part in right now.
        /// </summary>
        bool IsUsable(string what)
        {
            if (_isOffline != null && _isOffline())
            {
                CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                    $"[WeeklyChallengeLeaderboard] Offline session - skipping {what}.");
                return false;
            }

            if (UnityServices.State != ServicesInitializationState.Initialized ||
                AuthenticationService.Instance == null ||
                !AuthenticationService.Instance.IsSignedIn)
            {
                CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                    $"[WeeklyChallengeLeaderboard] Not signed in - skipping {what}.");
                return false;
            }

            return true;
        }

        static string LocalPlayerId()
        {
            try
            {
                return AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn
                    ? AuthenticationService.Instance.PlayerId
                    : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// UGS display names carry a <c>#1234</c> discriminator. The same strip
        /// <c>Player.StripPlayerNameSuffix</c> does, for the same reason - a leaderboard row and a
        /// scoreboard row must not show the same person under two different names.
        /// </summary>
        static string StripNameSuffix(string ugsName)
        {
            if (string.IsNullOrEmpty(ugsName)) return ugsName;
            int hash = ugsName.LastIndexOf('#');
            return hash > 0 ? ugsName.Substring(0, hash) : ugsName;
        }
    }
}

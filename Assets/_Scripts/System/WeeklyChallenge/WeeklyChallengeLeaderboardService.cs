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
    /// A recurring reset schedule does it, and its <i>archive on reset</i> is what a reward pass
    /// reads when the week closes. See the setup contract in the remarks below: those settings live
    /// on the SERVICE and NOTHING in this code can enforce them — the client SDK has no reset call
    /// at all — which is why both of the ones that fail silently are checked at runtime instead.</para>
    ///
    /// <para><b>Every submitted score is stamped with the PERIOD it was run for, and a read keeps
    /// only the current one.</b> That is the safety net under the reset: when the schedule is
    /// missing or misaligned, last week's times stay on the board and — the score being a time,
    /// sorted ascending — outrank every new run forever, so the board freezes on the week the reset
    /// stopped happening. The stamp is what lets a read tell "fastest this week" from "fastest
    /// ever", and it is deliberately only a net: it can hide the stale rows, it cannot remove them.
    /// <see cref="WarnIfBoardNotResetting"/> is what asks for the real fix.</para>
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
    /// <item><b>Reset: RECURRING WEEKLY, on the same UTC Monday boundary as
    /// <see cref="WeeklyChallengeCatalogSO.WeekStartUtc"/>, with ARCHIVING ON.</b> The archive is
    /// the only record of who won a week once the board has reset. Settable in the dashboard, the
    /// UGS CLI, or the Leaderboards Admin API — but never from the game.
    /// <b>This one fails silently too</b>, which is why <see cref="WarnIfBoardNotResetting"/>
    /// exists: without it the board keeps serving a frozen ranking of an older week.</item>
    /// </list>
    /// A note on KEEP BEST once the reset is missing: "best" is across the whole board's life, so a
    /// player whose stale time is faster than their new one keeps the stale row and gets no entry
    /// for this week at all. That is not a bug in the update strategy — it is the reset's absence
    /// showing up somewhere else, and no client-side rule can undo it.
    /// A code-side workaround for the sort order — submitting <c>BIG - time</c> so a descending
    /// board ranks correctly — was considered and rejected: it makes every raw score in the
    /// dashboard, in every export, and in the archive the reward pass reads a number nobody can
    /// interpret, to save one dashboard setting.
    /// </remarks>
    public class WeeklyChallengeLeaderboardService
    {
        /// <summary>Rows a page fetch asks for by default — the mock-up's list is four.</summary>
        public const int DefaultPageSize = 10;

        /// <summary>
        /// Requests one <see cref="FetchAsync"/> will spend paging PAST rows from other periods
        /// before it gives up and returns what it found.
        ///
        /// <para>A board that reset correctly never pages at all: the first request asks for
        /// exactly the rows the caller wanted and every one of them is current. The budget only
        /// spends on a board that did not reset, where this week's runs sit BELOW an accumulation
        /// of older ones — and it is small on purpose, because no client-side budget can make an
        /// ever-growing board correct. Past it the fetch under-fills and
        /// <see cref="WarnIfBoardNotResetting"/> says why.</para>
        /// </summary>
        const int MaxPagesPerFetch = 5;

        /// <summary>Rows one UGS request may ask for. The service's own documented cap.</summary>
        const int MaxRowsPerRequest = 100;

        readonly Func<string> _leaderboardId;
        readonly Func<bool> _isOffline;
        readonly Func<string, string> _regionalLeaderboardId;
        readonly Func<IReadOnlyList<string>> _friendIds;
        readonly Func<int> _localAvatarId;
        readonly Func<string> _periodKey;

        bool _warnedNoId;
        bool _warnedSortOrder;
        bool _warnedNotResetting;
        bool _warnedNoPeriodKey;

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
        /// <param name="periodKey">The key of the period being ranked — the challenge's DRAW key,
        /// not its record key. Stamped into every submit and used to reject rows from other weeks
        /// on the way back (<see cref="WeeklyChallengeRanking.PeriodKey"/>). Late for the same
        /// reason as the rest, and here that is load-bearing rather than tidy: a week rolls over
        /// while this service is alive.</param>
        public WeeklyChallengeLeaderboardService(
            Func<string> leaderboardId,
            Func<bool> isOffline = null,
            Func<string, string> regionalLeaderboardId = null,
            Func<IReadOnlyList<string>> friendIds = null,
            Func<int> localAvatarId = null,
            Func<string> periodKey = null)
        {
            _leaderboardId = leaderboardId;
            _isOffline = isOffline;
            _regionalLeaderboardId = regionalLeaderboardId;
            _friendIds = friendIds;
            _localAvatarId = localAvatarId;
            _periodKey = periodKey;
        }

        /// <summary>
        /// The period a fetch keeps and a submit stamps, or empty when nothing can say. Empty
        /// disables the filter entirely — see <see cref="WeeklyChallengeRanking.IsForPeriod"/>.
        /// </summary>
        public string CurrentPeriodKey => _periodKey?.Invoke() ?? string.Empty;

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
        /// Metadata is the ONLY field a score carries besides its number, and it carries two
        /// things: which PERIOD the run was for, and the player's avatar.
        ///
        /// <para><b>The period is mandatory and the avatar is not</b>, which is a change from when
        /// the avatar was the whole payload. An avatar is decoration — a row without one draws the
        /// template's art and reads as normal. The period is what makes the row legible at all: an
        /// unstamped entry cannot be told apart from last week's, so
        /// <see cref="WeeklyChallengeRanking.IsForPeriod"/> treats it as not this week's and a read
        /// will hide it. So this must never return null while a period is known — the old
        /// early-out on "no avatar" would have submitted a score the game then refuses to
        /// display.</para>
        ///
        /// <para>The avatar is still omitted rather than sent as <c>{"a":-1}</c>: an absent field
        /// and a field saying "nothing" read the same on the way back, and the shorter one does not
        /// have to be parsed.</para>
        /// </summary>
        AddPlayerScoreOptions BuildSubmitOptions()
        {
            string period = CurrentPeriodKey;
            int avatar = _localAvatarId?.Invoke() ?? WeeklyChallengeRanking.NoAvatar;

            if (string.IsNullOrEmpty(period))
            {
                // Practically unreachable — the board id comes off the same catalog, so a catalog
                // that cannot name a period cannot name a board either and TryResolveId has
                // already refused. Warned rather than blocked all the same: the run really
                // happened, and a ranked-but-hidden row is a better failure than a lost one.
                WarnNoPeriodKey();
                if (avatar < 0) return null;
            }

            var metadata = new Dictionary<string, object>(2);
            if (!string.IsNullOrEmpty(period))
                metadata[WeeklyChallengeRanking.PeriodMetadataKey] = period;
            if (avatar >= 0)
                metadata[WeeklyChallengeRanking.AvatarMetadataKey] = avatar;

            return new AddPlayerScoreOptions { Metadata = metadata };
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
            string period = CurrentPeriodKey;
            int dropped = 0;

            try
            {
                if (scope == LeaderboardScope.Friends)
                {
                    dropped = await FetchFriendsAsync(id, limit, localId, period, rows, ct);
                }
                else
                {
                    dropped = await FetchPageAsync(id, limit, localId, period, rows, ct);

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

            if (dropped > 0) WarnIfBoardNotResetting(id, dropped, period);

            return rows;
        }

        /// <summary>
        /// A page of the board, filtered to <paramref name="period"/>, paging on when a page turns
        /// out to be somebody else's week. Returns how many rows were passed over.
        ///
        /// <para><b>A healthy board costs exactly one request, asking for exactly
        /// <paramref name="limit"/> rows — the behaviour this had before the filter existed.</b>
        /// The loop only turns over when the board is serving other periods, which is the
        /// not-reset case: there this week's runs sit below the accumulation (the score is a time
        /// and the sort is ascending, so an old fast run outranks a new one forever), and without
        /// paging the panel would be empty rather than wrong. It gives up after
        /// <see cref="MaxPagesPerFetch"/>, because the accumulation grows every week and no client
        /// budget can outrun it — the fix is the board's reset schedule, which
        /// <see cref="WarnIfBoardNotResetting"/> asks for by name.</para>
        /// </summary>
        async UniTask<int> FetchPageAsync(
            string id, int limit, string localId, string period,
            List<WeeklyChallengeRanking> rows, CancellationToken ct)
        {
            int pageSize = Mathf.Clamp(limit, 1, MaxRowsPerRequest);
            int dropped = 0;
            int offset = 0;

            for (int request = 0; request < MaxPagesPerFetch && rows.Count < limit; request++)
            {
                var page = await LeaderboardsService.Instance
                    .GetScoresAsync(id, new GetScoresOptions
                    {
                        Offset = offset,
                        Limit = pageSize,
                        IncludeMetadata = true,
                    })
                    .AsMainThread();
                ct.ThrowIfCancellationRequested();

                var results = page?.Results;
                if (results == null || results.Count == 0) break;

                offset += results.Count;

                foreach (var entry in results)
                {
                    if (entry == null) continue;

                    var row = ToRanking(entry, localId);
                    if (!WeeklyChallengeRanking.IsForPeriod(row.PeriodKey, period))
                    {
                        dropped++;
                        continue;
                    }

                    rows.Add(row);
                    if (rows.Count >= limit) break;
                }

                // A short page is the end of the board, so there is nothing further to page to.
                if (results.Count < pageSize) break;
            }

            // Rows dropped above a survivor leave holes in the world ranks UGS gave, so once
            // anything has gone the whole list is re-ranked by POSITION - the same rule, through
            // the same call, that WeeklyChallengeRanking.RetainPeriod applies on the Friends path.
            // A board with nothing to drop keeps its true ranks.
            if (dropped > 0) WeeklyChallengeRanking.Renumber(rows);

            return dropped;
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
        async UniTask<int> FetchFriendsAsync(
            string id, int limit, string localId, string period,
            List<WeeklyChallengeRanking> rows, CancellationToken ct)
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

            if (ids.Count == 0) return 0;

            var page = await LeaderboardsService.Instance
                .GetScoresByPlayerIdsAsync(id, ids, new GetScoresByPlayerIdsOptions { IncludeMetadata = true })
                .AsMainThread();
            ct.ThrowIfCancellationRequested();

            if (page?.Results == null) return 0;

            var found = new List<WeeklyChallengeRanking>();
            foreach (var entry in page.Results)
            {
                // A friend who has not completed the challenge has NO entry, so this list is
                // shorter than the friends list by design - the same rule as the world board.
                if (entry == null) continue;
                found.Add(ToRanking(entry, localId));
            }

            // A friend whose entry is LAST week's is in the same position as one with no entry at
            // all: they have not run THIS challenge. Filtered before the sort, so a stale fast
            // time cannot take the top of a friends list either. There is no paging to do here -
            // this is a lookup of specific ids, not a page, so every row that exists is already in
            // hand.
            int dropped = WeeklyChallengeRanking.RetainPeriod(found, period);

            found.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));

            for (int i = 0; i < found.Count && rows.Count < limit; i++)
            {
                var row = found[i];
                row.Rank = i + 1;
                rows.Add(row);
            }

            return dropped;
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
                PeriodKey = WeeklyChallengeRanking.ReadPeriodKeyFromMetadata(entry.Metadata),
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

        /// <summary>
        /// <b>A board that never resets looks like a working leaderboard too.</b> This is the
        /// sibling of <see cref="WarnIfSortedWrong"/> and the same class of defect: the board is
        /// ONE board reset weekly by UGS, that reset is a schedule nothing in this code can set,
        /// and when it is missing the rows are real, the names are real and the times are real —
        /// they are answers to a challenge that is no longer this week's. Worse than wrong-order,
        /// because the score is a TIME and the sort is ascending: an old fast run outranks every
        /// new one forever, so the board freezes on the week the reset stopped happening.
        ///
        /// <para>A read cannot repair that — it can only refuse to present it as this week's
        /// ranking, which is what the period filter does. So this states what was hidden, and asks
        /// for the one fix that works. Warned ONCE per session, not per fetch: the panel refreshes,
        /// and a misconfiguration that logs on a timer teaches people to filter the log.</para>
        /// </summary>
        void WarnIfBoardNotResetting(string id, int dropped, string period)
        {
            if (_warnedNotResetting) return;
            _warnedNotResetting = true;

            CSDebug.LogError(
                $"[WeeklyChallengeLeaderboard] '{id}' is serving {dropped} row(s) from another " +
                $"period - this week is '{period}'. They have been hidden rather than ranked " +
                "against this week's challenge, so the board will read as short or empty. The " +
                "board is not being reset: give it a RECURRING WEEKLY reset with ARCHIVING ON, " +
                "aligned to UTC Monday 00:00, in the UGS dashboard (Leaderboards > this board > " +
                "Resets), the UGS CLI, or the Leaderboards Admin API. Nothing in the game can " +
                "reset a board - the client SDK has no such call.");
        }

        /// <summary>
        /// A submit that cannot state its period produces a row this game will then hide from
        /// itself, so it is worth exactly one warning. Reachable only with a catalog that names a
        /// leaderboard id but no period, which is not a state the shipped catalog can be in.
        /// </summary>
        void WarnNoPeriodKey()
        {
            if (_warnedNoPeriodKey) return;
            _warnedNoPeriodKey = true;

            CSDebug.LogWarning(
                "[WeeklyChallengeLeaderboard] Submitting without a period stamp - nothing could " +
                "say which week this run was for. The score will rank on the board but every " +
                "read will hide it as belonging to another period.");
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

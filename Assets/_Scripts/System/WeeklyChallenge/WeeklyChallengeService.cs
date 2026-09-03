using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The weekly challenge: ONE curated objective per UTC week, the same one for every player,
    /// with the player's progress against it synced to UGS Cloud Save.
    ///
    /// <para><b>The definition is derived, the progress is stored.</b> ThisWeek's challenge is a pure
    /// function of the UTC date over <see cref="WeeklyChallengeCatalogSO"/>, so it resolves on a
    /// cold launch, offline, and identically on every platform with no server round trip. Cloud
    /// Save holds only what the player has DONE (best value, completed, attempts) - the one thing
    /// that genuinely differs per player and genuinely has to survive a reinstall.</para>
    ///
    /// <para><b>Rollover is a date comparison, never a timer.</b> Nothing is scheduled for
    /// rollover: <see cref="ThisWeek"/> is recomputed whenever the UTC week key changes, which is
    /// checked once a second. A device that was asleep across the rollover, or whose clock jumped,
    /// lands on the correct challenge on the next check rather than on whatever a pending timer
    /// would have fired.</para>
    ///
    /// <para>Zero scene wiring: the service creates itself at
    /// <see cref="RuntimeInitializeLoadType.AfterSceneLoad"/> on a DontDestroyOnLoad object, in
    /// the shape <c>VesselSpeedTunnel</c> uses. It is handed the <see cref="GameDataSO"/> it needs
    /// by whoever launches an attempt rather than hunting for one, so there is no asset reference
    /// to keep in step.</para>
    ///
    /// <para>Supersedes the PlayFab-era <c>WeeklyChallengeSystem</c> (PlayerPrefs storage, a
    /// <c>SO_TrainingGame</c> pool, an <c>Arcade</c> singleton that is in no scene). That class is
    /// left in the tree but is inert - it is in no scene and nothing here reads it.</para>
    /// </summary>
    public class WeeklyChallengeService : MonoBehaviour
    {
        public static WeeklyChallengeService Instance { get; private set; }

        /// <summary>
        /// Attempts the player gets this week, from the catalog (default 1 - the challenge is played
        /// ONCE). 0 = unlimited. Falls back to the catalog default when no catalog is loaded.
        /// </summary>
        public int AttemptsPerPeriod
        {
            get
            {
                var catalog = WeeklyChallengeCatalogSO.Instance;
                return catalog != null
                    ? catalog.EffectiveAttemptsPerPeriod
                    : WeeklyChallengeCatalogSO.DefaultAttemptsPerPeriod;
            }
        }

        /// <summary>Attempts left this week. <see cref="int.MaxValue"/> when unlimited.</summary>
        public int AttemptsRemaining
        {
            get
            {
                int perPeriod = AttemptsPerPeriod;
                if (perPeriod <= 0) return int.MaxValue;
                return Mathf.Max(0, perPeriod - AttemptsThisWeek);
            }
        }

        // ── State ──────────────────────────────────────────────────────────────

        /// <summary>ThisWeek's challenge. Check <see cref="WeeklyChallenge.IsValid"/> before use.</summary>
        public WeeklyChallenge ThisWeek { get; private set; }

        /// <summary>True once the cloud record has been reconciled against this week's date.</summary>
        public bool IsCloudReady { get; private set; }

        /// <summary>True when the player has already met this week's objective.</summary>
        public bool CompletedThisWeek => _data != null && _data.Completed;

        /// <summary>Best value of this week's metric the player has reached.</summary>
        public int BestValueThisWeek => _data?.BestValue ?? 0;

        /// <summary>
        /// Attempts the player has STARTED this week. Spent at launch rather than at the end, so
        /// quitting mid-run does not buy a retry - "played only once" has to mean once.
        /// </summary>
        public int AttemptsThisWeek => _data?.Attempts ?? 0;

        /// <summary>
        /// Time until the current challenge is replaced (the next UTC Monday). This is what the card
        /// counts down once the player has finished this week's - "time left for the next challenge
        /// to begin".
        /// </summary>
        public TimeSpan TimeUntilNextChallenge
        {
            get
            {
                var now = DateTime.UtcNow;
                var catalog = WeeklyChallengeCatalogSO.Instance;
                var end = catalog != null
                    ? catalog.PeriodEndUtc(now)
                    : WeeklyChallengeCatalogSO.NextRolloverUtc(now);

                var span = end - now;
                return span > TimeSpan.Zero ? span : TimeSpan.Zero;
            }
        }

        /// <summary>Raised when this week's challenge OR the player's progress against it changes.</summary>
        public event Action OnChallengeChanged;

        /// <summary>Raised every attempt tick while a challenge run is live: (achieved, target, secondsRemaining).</summary>
        public event Action<int, int, float> OnAttemptProgress;

        // ── Internals ──────────────────────────────────────────────────────────

        WeeklyChallengeCloudData _data;
        WeeklyChallengeRepository _repo;
        string _resolvedPeriodKey = "";
        float _dateCheckAccumulator;

        GameDataSO _gameData;

        // Live attempt
        bool _attemptArmed;      // launched, waiting for the turn to start
        bool _attemptRunning;    // turn started, clock ticking
        bool _attemptFinished;   // recorded, ignore further signals
        float _attemptElapsed;
        int _attemptBest;
        WeeklyChallenge _attemptChallenge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Statics survive play-mode exit in the editor, so a stale Instance from the previous
            // session would make this a no-op and leave the feature dead for the whole run.
            Instance = null;

            // HideInHierarchy, NOT HideAndDontSave - the latter exempts the object from
            // play-mode-exit cleanup and leaks one per session (the same note
            // VesselSpeedTunnel's driver carries).
            var go = new GameObject("[WeeklyChallengeService]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<WeeklyChallengeService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RefreshChallengeForPeriod();
        }

        void OnDestroy()
        {
            UnsubscribeFromGameData();

            if (_repo != null)
                _repo.OnDataChanged -= HandleRepoChanged;

            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            // Rollover check. Once a second is plenty for a weekly cycle and costs one string
            // compare; a scheduled rollover callback would be wrong every time the device
            // slept through it or the clock moved.
            _dateCheckAccumulator += Time.unscaledDeltaTime;
            if (_dateCheckAccumulator >= 1f)
            {
                _dateCheckAccumulator = 0f;

                if (CurrentPeriodKey() != _resolvedPeriodKey)
                    RefreshChallengeForPeriod();

                if (!IsCloudReady)
                    TryBindCloud();
            }

            if (_attemptRunning)
                TickAttempt();
        }

        // ── Challenge resolution ───────────────────────────────────────────────

        void RefreshChallengeForPeriod()
        {
            var catalog = WeeklyChallengeCatalogSO.Instance;
            if (catalog == null)
            {
                // Loud once per rollover, not per frame: an absent catalog is a wiring fault, and
                // the card has no honest thing to show without it.
                CSDebug.LogWarning("[WeeklyChallengeService] No WeeklyChallengeCatalog asset at " +
                                   $"Resources/{WeeklyChallengeCatalogSO.ResourcePath} - the weekly " +
                                   "challenge card will show as unavailable.");
                ThisWeek = default;
                _resolvedPeriodKey = WeeklyChallengeCatalogSO.WeekKeyFor(DateTime.UtcNow);
                OnChallengeChanged?.Invoke();
                return;

            }

            // Written as a local rather than inline: a method group inside a conditional has no
            // type of its own, and relying on target-typing there is the kind of thing that
            // compiles on one C# version and not the next.
            Func<GameModes, bool> isModeAvailable = null;
            var progression = GameModeProgressionService.Instance;
            if (progression != null)
                isModeAvailable = progression.IsGameModeUnlocked;

            ThisWeek = catalog.ForDate(DateTime.UtcNow, isModeAvailable);

            _resolvedPeriodKey = catalog.RecordKeyFor(DateTime.UtcNow);

            ReconcileCloudWithPeriod();
            OnChallengeChanged?.Invoke();
        }

        /// <summary>
        /// The key of the period we are in - the UTC week normally, a shortened test period when
        /// the catalog says so. Falls back to the plain date when no catalog is loaded, so a
        /// missing asset cannot make the rollover check thrash.
        /// </summary>
        string CurrentPeriodKey()
        {
            var catalog = WeeklyChallengeCatalogSO.Instance;
            return catalog != null
                ? catalog.RecordKeyFor(DateTime.UtcNow)
                : WeeklyChallengeCatalogSO.WeekKeyFor(DateTime.UtcNow);
        }

        // ── Cloud ──────────────────────────────────────────────────────────────

        void TryBindCloud()
        {
            var ugs = UGSDataService.Instance;
            if (ugs == null || !ugs.IsInitialized) return;

            _repo = ugs.WeeklyChallengeRepo;
            if (_repo == null) return;

            _data = _repo.Data;
            _repo.OnDataChanged += HandleRepoChanged;
            IsCloudReady = true;

            ReconcileCloudWithPeriod();
            OnChallengeChanged?.Invoke();
        }

        void HandleRepoChanged()
        {
            // A late sign-in reloads the record from the cloud, which replaces the instance the
            // repository hands out - re-read it rather than keeping a reference to the old one.
            if (_repo != null) _data = _repo.Data;
            ReconcileCloudWithPeriod();
            OnChallengeChanged?.Invoke();
        }

        /// <summary>
        /// Wipes the stored progress when it belongs to an earlier UTC week, or when the catalog
        /// was re-authored and this week's objective no longer matches what the record was earned
        /// against. The second case matters in development: a target edited mid-day would
        /// otherwise leave a "completed" flag standing against an objective nobody met.
        /// </summary>
        void ReconcileCloudWithPeriod()
        {
            if (_data == null || !ThisWeek.IsValid) return;

            bool staleDay = _data.IsStale(ThisWeek.PeriodKey);
            bool differentAsk = _data.GameMode != ThisWeek.GameMode.ToString()
                                || _data.Metric != ThisWeek.Metric.ToString()
                                || _data.TargetValue != ThisWeek.TargetValue;

            if (!staleDay && !differentAsk) return;

            _data.ResetForNewDay(
                ThisWeek.PeriodKey,
                ThisWeek.GameMode.ToString(),
                ThisWeek.Intensity,
                ThisWeek.Metric.ToString(),
                ThisWeek.TargetValue);

            _repo?.MarkDirty();
        }

        // ── Attempts ───────────────────────────────────────────────────────────

        /// <summary>
        /// True when the player may start an attempt right now - this week's challenge resolved and
        /// they have an attempt left.
        ///
        /// <para>Running out does NOT lock the mode out: the card stops offering it as the day's
        /// objective and counts down to the next one, while the MODE stays on the arcade grid like
        /// any other. Only the weekly objective is spent.</para>
        /// </summary>
        public bool CanAttempt => ThisWeek.IsValid && AttemptsRemaining > 0;

        /// <summary>
        /// Arms an attempt at launch. Called by <c>ArcadeGameConfigureModal</c> as it syncs the
        /// launch config, which is also where the <see cref="GameDataSO"/> comes from - the
        /// service never hunts for one, so there is no asset reference here to drift.
        /// </summary>
        public void BeginAttempt(GameDataSO gameData)
        {
            if (gameData == null || !ThisWeek.IsValid) return;

            BindGameData(gameData);

            _attemptChallenge = ThisWeek;
            _attemptArmed = true;
            _attemptRunning = false;
            _attemptFinished = false;
            _attemptElapsed = 0f;
            _attemptBest = 0;

            gameData.IsWeeklyChallenge = true;

            // NOTE: the run uses the MODE'S OWN end conditions, untouched. A weekly challenge is an
            // ordinary match of that mode played for a personal objective on top - it is not a
            // shortened variant, and nothing here writes a race target.

            // Spend the attempt NOW, and flush it. "Played only once" has to survive an alt-F4
            // halfway through a bad run, so the attempt is consumed at launch rather than
            // credited at the end - the one ordering that cannot be save-scummed.
            SpendAttempt();

            CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                $"[WeeklyChallenge] Attempt armed - {_attemptChallenge.GameMode} " +
                $"{_attemptChallenge.ObjectiveText}; " +
                $"{AttemptsRemaining} attempt(s) left");
        }

        void SpendAttempt()
        {
            if (_data == null || _data.ChallengeWeek != _attemptChallenge.PeriodKey) return;

            _data.Attempts++;
            _repo?.MarkDirty();

            // Straight to the cloud rather than waiting on the debounce: the very next thing this
            // process does is load a scene, and a player who force-quits during the match must
            // still come back to a spent attempt.
            _ = _repo?.SaveAsync();

            OnChallengeChanged?.Invoke();
        }

        void BindGameData(GameDataSO gameData)
        {
            if (_gameData == gameData) return;

            UnsubscribeFromGameData();
            _gameData = gameData;

            if (_gameData == null) return;

            _gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            _gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
            _gameData.OnMiniGameEnd.OnRaised += HandleGameEnded;
            _gameData.OnSessionEnded.OnRaised += HandleSessionEnded;
        }

        void UnsubscribeFromGameData()
        {
            if (_gameData == null) return;

            _gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            _gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
            _gameData.OnMiniGameEnd.OnRaised -= HandleGameEnded;
            _gameData.OnSessionEnded.OnRaised -= HandleSessionEnded;
            _gameData = null;
        }

        void HandleTurnStarted()
        {
            if (!_attemptArmed || _attemptFinished) return;
            if (_gameData == null || !_gameData.IsWeeklyChallenge) return;

            _attemptRunning = true;
            _attemptElapsed = 0f;
            _attemptBest = 0;
        }

        /// <summary>
        /// The number this attempt is measured against. An authored target is the catalog's; a
        /// mode-target challenge reads the MATCH's own end condition through the scoring rule
        /// (<c>ScoringRuleSO.TargetFor</c>) - for Skim Race that is the crystal count the turn
        /// monitor publishes, so it is exactly what the game itself races to. Re-read every tick
        /// rather than cached at launch, because the monitor publishes it AFTER the scene loads
        /// and a client receives it as a NetworkVariable; before it lands the rule's fallback is
        /// never SMALLER than the real number, so nothing can complete early.
        /// </summary>
        int ResolveAttemptTarget()
        {
            if (!_attemptChallenge.UsesModeTarget) return _attemptChallenge.TargetValue;

            var rule = _gameData != null ? _gameData.ScoringRule : null;
            return rule != null ? Mathf.Max(0, rule.TargetFor(_gameData)) : 0;
        }

        /// <summary>
        /// Whether an attempt at <paramref name="achieved"/> is COMPLETE. Personal count at the
        /// target, or - for a mode-target challenge - the match's own verdict: the player's domain
        /// reached the mode's objective. The second clause is what lets a party finish together
        /// (a race ends on the domain SUM, so two teammates each hold half the target).
        /// </summary>
        bool IsAttemptComplete(int achieved)
        {
            int target = ResolveAttemptTarget();
            if (target > 0 && achieved >= target) return true;
            if (!_attemptChallenge.UsesModeTarget) return false;

            var rule = _gameData != null ? _gameData.ScoringRule : null;
            var local = _gameData != null ? _gameData.LocalPlayer : null;
            if (rule == null || local == null) return false;

            return rule.IsObjectiveReached(_gameData, out var winner) && winner == local.Domain;
        }

        void TickAttempt()
        {
            _attemptElapsed += Time.deltaTime;

            int achieved = ReadLocalMetric(_attemptChallenge.Metric);
            if (achieved > _attemptBest) _attemptBest = achieved;

            OnAttemptProgress?.Invoke(_attemptBest, ResolveAttemptTarget(), float.PositiveInfinity);

            if (!IsAttemptComplete(_attemptBest)) return;

            // Target reached: stamp the completion and its TIME, which is the leaderboard score.
            //
            // <b>The turn is NOT ended, and there is no time limit that could end it either.</b> A
            // weekly challenge is an ordinary match of its mode played for a personal objective ON
            // TOP - it does not shorten, extend or otherwise alter the run. This used to end the
            // turn the moment the objective was met OR a per-entry countdown expired, which made a
            // weekly run a different, shorter match than the mode it claimed to be - and, worse,
            // meant a player who ran out of that clock had their attempt spent and NOTHING
            // submitted. FinishAttempt clears _attemptRunning, so this stops ticking and the rest
            // of the match costs nothing.
            FinishAttempt(_attemptBest, completed: true);
        }

        void HandleTurnEnded()
        {
            // The mode reached its own end condition first (target hit, race over). Record what
            // the player actually achieved.
            if (_attemptRunning)
                FinishAttemptAtEnd();
        }

        void HandleGameEnded()
        {
            if (_attemptRunning || (_attemptArmed && !_attemptFinished))
                FinishAttemptAtEnd();

            ClearAttempt();
        }

        void FinishAttemptAtEnd()
        {
            int achieved = Mathf.Max(_attemptBest, ReadLocalMetric(_attemptChallenge.Metric));
            FinishAttempt(achieved, IsAttemptComplete(achieved));
        }

        void HandleSessionEnded()
        {
            // Abandoning mid-run (quit to menu) deliberately records NOTHING: it is not a failed
            // attempt, and counting it would punish a disconnect.
            ClearAttempt();
        }

        void FinishAttempt(int achieved, bool completed)
        {
            if (_attemptFinished) return;
            _attemptFinished = true;
            _attemptRunning = false;

            int target = ResolveAttemptTarget();

            if (_data != null && _attemptChallenge.IsValid)
            {
                // Only fold in progress that belongs to THIS WEEK's ask - a run that started before
                // the rollover and ended after it is scored against the challenge it was launched
                // for, and that challenge is gone.
                if (_data.ChallengeWeek == _attemptChallenge.PeriodKey &&
                    _data.TargetValue == _attemptChallenge.TargetValue)
                {
                    if (_data.RecordResult(achieved, completed, DateTime.UtcNow))
                        _repo?.MarkDirty();

                    // The ranking is "who finished it fastest", so a COMPLETION submits its time
                    // and anything else submits nothing - a run that never reached the target has
                    // no time, not a slow one. Submitted here rather than at the scoreboard so
                    // there is exactly one site that can produce an entry.
                    // Submitted only when the attempt actually RAN. `_attemptElapsed` starts at 0
                    // and only accumulates once the turn has started, so an attempt that reached
                    // its target without ever ticking - a game that ended before
                    // OnMiniGameTurnStarted, or a mode that never raises it - would submit a time
                    // of ZERO, which on an ascending board is first place forever.
                    if (completed && _attemptElapsed > 0f)
                        SubmitLeaderboardTime(_attemptElapsed);
                    else if (completed)
                        CSDebug.LogWarning(
                            "[WeeklyChallenge] Objective met but the attempt never ticked, so there " +
                            "is no time to rank. The completion is recorded; nothing is submitted. " +
                            "This means OnMiniGameTurnStarted never fired for this run.");

                    CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                        $"[WeeklyChallenge] Attempt finished - achieved {achieved}/{target}" +
                        $"{(_attemptChallenge.UsesModeTarget ? " (mode's own)" : "")}, " +
                        $"best {_data.BestValue}, completed={_data.Completed}");
                }
            }

            OnChallengeChanged?.Invoke();
        }

        void ClearAttempt()
        {
            _attemptArmed = false;
            _attemptRunning = false;
            _attemptElapsed = 0f;

            if (_gameData != null)
                _gameData.IsWeeklyChallenge = false;
        }

        // ── Leaderboard ────────────────────────────────────────────────────────

        WeeklyChallengeLeaderboardService _leaderboard;

        /// <summary>
        /// The weekly ranking - "who completed this week's objective fastest". Built lazily and
        /// handed LATE-BOUND accessors rather than values, because the catalog can be reloaded and
        /// a session can go offline after this service exists.
        /// </summary>
        public WeeklyChallengeLeaderboardService Leaderboard =>
            _leaderboard ??= new WeeklyChallengeLeaderboardService(
                () => WeeklyChallengeCatalogSO.Instance != null
                    ? WeeklyChallengeCatalogSO.Instance.leaderboardId
                    : null,
                () => _gameData != null && _gameData.IsOfflineSession,
                region => WeeklyChallengeCatalogSO.Instance != null
                    ? WeeklyChallengeCatalogSO.Instance.RegionalLeaderboardId(region)
                    : null,
                () => FriendIdSource?.Invoke(),
                ResolveLocalAvatarId);

        /// <summary>
        /// Where the Friends scope gets its player ids. <b>Published, not looked up</b> — this
        /// service is a hidden runtime-created object with no inspector and no Reflex injection,
        /// so it cannot reach the DI-registered <c>FriendsDataSO</c> itself; the view that CAN
        /// (any scene object under a ContainerScope) hands the source in.
        ///
        /// <para>Returning <b>null</b> and returning an EMPTY list are different answers and the
        /// difference is load-bearing: null means "we cannot ask", which greys the tab out, while
        /// empty means "asked, and nobody you know has a time", which is a legitimately empty
        /// board. Collapsing them tells a player with no friends that the feature is broken.</para>
        /// </summary>
        public static Func<IReadOnlyList<string>> FriendIdSource { get; set; }

        /// <summary>
        /// The local profile's icon id, stamped into a submitted score so a leaderboard row can
        /// show a face. Read off <see cref="GameDataSO.LocalPlayerAvatarId"/> — the mirror
        /// <c>PlayerDataService</c> already publishes — rather than the profile service, which
        /// this object also cannot reach. <see cref="WeeklyChallengeRanking.NoAvatar"/> before the
        /// profile has loaded, which is the same state as every score submitted before avatars
        /// were carried and is drawn correctly by the view.
        /// </summary>
        int ResolveLocalAvatarId() =>
            _gameData != null ? _gameData.LocalPlayerAvatarId : WeeklyChallengeRanking.NoAvatar;

        void SubmitLeaderboardTime(float seconds)
        {
            // Fire and forget: the run is over, the player's own record is already saved, and a
            // leaderboard entry is a claim about a live ranking rather than progress to replay.
            Leaderboard.SubmitCompletionAsync(seconds).Forget();
        }

        // ── Testing ────────────────────────────────────────────────────────────

        /// <summary>
        /// Wipes this week's stored progress (attempts, best, completion) and re-resolves the
        /// challenge - the "let me play it again" button in
        /// <c>FrogletTools &gt; Game Modes &gt; Weekly Challenge</c>.
        ///
        /// <para>Refuses outside the editor and development builds. It is a real write to the
        /// player's cloud record, so it is gated at the only place that can honestly enforce it -
        /// here - rather than trusting every caller to check.</para>
        /// </summary>
        public void ResetPeriodForTesting()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                CSDebug.LogWarning("[WeeklyChallengeService] ResetPeriodForTesting refused - " +
                                   "release build.");
                return;
            }

            if (_data == null)
            {
                CSDebug.LogWarning("[WeeklyChallengeService] ResetPeriodForTesting - no cloud " +
                                   "record loaded yet; nothing to reset.");
                return;
            }

            // Blank the stamped date so the reconcile below treats this week as new and rewrites the
            // record from the challenge, rather than having to duplicate its field-by-field reset.
            _data.ChallengeWeek = "";
            ReconcileCloudWithPeriod();

            _repo?.MarkDirty();
            _ = _repo?.SaveAsync();

            CSDebug.LogVerbose(CSLogChannel.WeeklyChallenge,
                "[WeeklyChallenge] ThisWeek's progress reset for testing.");
            OnChallengeChanged?.Invoke();
        }

        /// <summary>
        /// Re-resolves this week's challenge from the catalog - what the editor tool calls after an
        /// edit so the running game picks the change up without a domain reload.
        /// </summary>
        public void RefreshFromCatalog() => RefreshChallengeForPeriod();

        /// <summary>
        /// The challenge metric off the LOCAL player's own round stats. Personal by design - a
        /// domain sum would let the AI seated beside you finish your challenge.
        /// </summary>
        int ReadLocalMetric(ScoringMetric metric)
        {
            if (_gameData == null) return 0;

            var stats = _gameData.LocalRoundStats;
            if (stats == null && _gameData.LocalPlayer != null && _gameData.RoundStatsList != null)
            {
                var localName = _gameData.LocalPlayer.Name;
                for (int i = 0; i < _gameData.RoundStatsList.Count; i++)
                {
                    var candidate = _gameData.RoundStatsList[i];
                    if (candidate != null && candidate.Name == localName)
                    {
                        stats = candidate;
                        break;
                    }
                }
            }

            return stats != null ? ScoringMetrics.Read(stats, metric) : 0;
        }
    }
}

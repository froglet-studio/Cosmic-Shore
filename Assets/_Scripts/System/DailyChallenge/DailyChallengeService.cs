using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The daily challenge: ONE curated objective per UTC day, the same one for every player,
    /// with the player's progress against it synced to UGS Cloud Save.
    ///
    /// <para><b>The definition is derived, the progress is stored.</b> Today's challenge is a pure
    /// function of the UTC date over <see cref="DailyChallengeCatalogSO"/>, so it resolves on a
    /// cold launch, offline, and identically on every platform with no server round trip. Cloud
    /// Save holds only what the player has DONE (best value, completed, attempts) - the one thing
    /// that genuinely differs per player and genuinely has to survive a reinstall.</para>
    ///
    /// <para><b>Rollover is a date comparison, never a timer.</b> Nothing is scheduled for
    /// midnight: <see cref="Today"/> is recomputed whenever the UTC date key changes, which is
    /// checked once a second. A device that was asleep across midnight, or whose clock jumped,
    /// lands on the correct challenge on the next check rather than on whatever a pending timer
    /// would have fired.</para>
    ///
    /// <para>Zero scene wiring: the service creates itself at
    /// <see cref="RuntimeInitializeLoadType.AfterSceneLoad"/> on a DontDestroyOnLoad object, in
    /// the shape <c>VesselSpeedTunnel</c> uses. It is handed the <see cref="GameDataSO"/> it needs
    /// by whoever launches an attempt rather than hunting for one, so there is no asset reference
    /// to keep in step.</para>
    ///
    /// <para>Supersedes the PlayFab-era <c>DailyChallengeSystem</c> (PlayerPrefs storage, a
    /// <c>SO_TrainingGame</c> pool, an <c>Arcade</c> singleton that is in no scene). That class is
    /// left in the tree but is inert - it is in no scene and nothing here reads it.</para>
    /// </summary>
    public class DailyChallengeService : MonoBehaviour
    {
        public static DailyChallengeService Instance { get; private set; }

        /// <summary>
        /// Attempts granted per day. 0 = unlimited (the shipped default): the challenge is a
        /// daily invitation rather than an economy, and a ticket balance a player can exhaust
        /// needs a place to buy more, which nothing here provides.
        /// </summary>
        public const int DailyAttempts = 0;

        // ── State ──────────────────────────────────────────────────────────────

        /// <summary>Today's challenge. Check <see cref="DailyChallenge.IsValid"/> before use.</summary>
        public DailyChallenge Today { get; private set; }

        /// <summary>True once the cloud record has been reconciled against today's date.</summary>
        public bool IsCloudReady { get; private set; }

        /// <summary>True when the player has already met today's objective.</summary>
        public bool CompletedToday => _data != null && _data.Completed;

        /// <summary>Best value of today's metric the player has reached.</summary>
        public int BestValueToday => _data?.BestValue ?? 0;

        /// <summary>Attempts the player has finished today.</summary>
        public int AttemptsToday => _data?.Attempts ?? 0;

        /// <summary>
        /// Time until the current challenge is replaced (UTC midnight). This is what the card
        /// counts down once the player has finished today's - "time left for the next challenge
        /// to begin".
        /// </summary>
        public TimeSpan TimeUntilNextChallenge
        {
            get
            {
                var now = DateTime.UtcNow;
                var span = DailyChallengeCatalogSO.NextRolloverUtc(now) - now;
                return span > TimeSpan.Zero ? span : TimeSpan.Zero;
            }
        }

        /// <summary>Raised when today's challenge OR the player's progress against it changes.</summary>
        public event Action OnChallengeChanged;

        /// <summary>Raised every attempt tick while a challenge run is live: (achieved, target, secondsRemaining).</summary>
        public event Action<int, int, float> OnAttemptProgress;

        // ── Internals ──────────────────────────────────────────────────────────

        DailyChallengeCloudData _data;
        DailyChallengeRepository _repo;
        string _resolvedDateKey = "";
        float _dateCheckAccumulator;

        GameDataSO _gameData;

        // Live attempt
        bool _attemptArmed;      // launched, waiting for the turn to start
        bool _attemptRunning;    // turn started, clock ticking
        bool _attemptFinished;   // recorded, ignore further signals
        float _attemptElapsed;
        int _attemptBest;
        DailyChallenge _attemptChallenge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Statics survive play-mode exit in the editor, so a stale Instance from the previous
            // session would make this a no-op and leave the feature dead for the whole run.
            Instance = null;

            // HideInHierarchy, NOT HideAndDontSave - the latter exempts the object from
            // play-mode-exit cleanup and leaks one per session (the same note
            // VesselSpeedTunnel's driver carries).
            var go = new GameObject("[DailyChallengeService]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<DailyChallengeService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RefreshChallengeForToday();
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
            // Rollover check. Once a second is plenty for a 24h cycle and costs one string
            // compare; a scheduled midnight callback would be wrong every time the device
            // slept through it or the clock moved.
            _dateCheckAccumulator += Time.unscaledDeltaTime;
            if (_dateCheckAccumulator >= 1f)
            {
                _dateCheckAccumulator = 0f;

                if (DailyChallengeCatalogSO.DateKeyFor(DateTime.UtcNow) != _resolvedDateKey)
                    RefreshChallengeForToday();

                if (!IsCloudReady)
                    TryBindCloud();
            }

            if (_attemptRunning)
                TickAttempt();
        }

        // ── Challenge resolution ───────────────────────────────────────────────

        void RefreshChallengeForToday()
        {
            var catalog = DailyChallengeCatalogSO.Instance;
            if (catalog == null)
            {
                // Loud once per rollover, not per frame: an absent catalog is a wiring fault, and
                // the card has no honest thing to show without it.
                CSDebug.LogWarning("[DailyChallengeService] No DailyChallengeCatalog asset at " +
                                   $"Resources/{DailyChallengeCatalogSO.ResourcePath} - the daily " +
                                   "challenge card will show as unavailable.");
                Today = default;
                _resolvedDateKey = DailyChallengeCatalogSO.DateKeyFor(DateTime.UtcNow);
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

            Today = catalog.ForDate(DateTime.UtcNow, isModeAvailable);

            _resolvedDateKey = DailyChallengeCatalogSO.DateKeyFor(DateTime.UtcNow);

            ReconcileCloudWithToday();
            OnChallengeChanged?.Invoke();
        }

        // ── Cloud ──────────────────────────────────────────────────────────────

        void TryBindCloud()
        {
            var ugs = UGSDataService.Instance;
            if (ugs == null || !ugs.IsInitialized) return;

            _repo = ugs.DailyChallengeRepo;
            if (_repo == null) return;

            _data = _repo.Data;
            _repo.OnDataChanged += HandleRepoChanged;
            IsCloudReady = true;

            ReconcileCloudWithToday();
            OnChallengeChanged?.Invoke();
        }

        void HandleRepoChanged()
        {
            // A late sign-in reloads the record from the cloud, which replaces the instance the
            // repository hands out - re-read it rather than keeping a reference to the old one.
            if (_repo != null) _data = _repo.Data;
            ReconcileCloudWithToday();
            OnChallengeChanged?.Invoke();
        }

        /// <summary>
        /// Wipes the stored progress when it belongs to an earlier UTC day, or when the catalog
        /// was re-authored and today's objective no longer matches what the record was earned
        /// against. The second case matters in development: a target edited mid-day would
        /// otherwise leave a "completed" flag standing against an objective nobody met.
        /// </summary>
        void ReconcileCloudWithToday()
        {
            if (_data == null || !Today.IsValid) return;

            bool staleDay = _data.IsStale(Today.DateKey);
            bool differentAsk = _data.GameMode != Today.GameMode.ToString()
                                || _data.Metric != Today.Metric.ToString()
                                || _data.TargetValue != Today.TargetValue;

            if (!staleDay && !differentAsk) return;

            _data.ResetForNewDay(
                Today.DateKey,
                Today.GameMode.ToString(),
                Today.Intensity,
                Today.Metric.ToString(),
                Today.TargetValue,
                DailyAttempts);

            _repo?.MarkDirty();
        }

        // ── Attempts ───────────────────────────────────────────────────────────

        /// <summary>
        /// True when the player may start an attempt right now. Completing today's challenge does
        /// NOT lock the mode out - the card simply stops offering it as the day's objective and
        /// counts down to the next one; the mode is still on the arcade grid like any other.
        /// </summary>
        public bool CanAttempt =>
            Today.IsValid && (DailyAttempts <= 0 || _data == null || _data.TicketBalance > 0);

        /// <summary>
        /// Arms an attempt at launch. Called by <c>ArcadeGameConfigureModal</c> as it syncs the
        /// launch config, which is also where the <see cref="GameDataSO"/> comes from - the
        /// service never hunts for one, so there is no asset reference here to drift.
        /// </summary>
        public void BeginAttempt(GameDataSO gameData)
        {
            if (gameData == null || !Today.IsValid) return;

            BindGameData(gameData);

            _attemptChallenge = Today;
            _attemptArmed = true;
            _attemptRunning = false;
            _attemptFinished = false;
            _attemptElapsed = 0f;
            _attemptBest = 0;

            gameData.IsDailyChallenge = true;

            if (DailyAttempts > 0 && _data != null && _data.TicketBalance > 0)
            {
                _data.TicketBalance--;
                _repo?.MarkDirty();
            }

            CSDebug.LogVerbose(CSLogChannel.DailyChallenge,
                $"[DailyChallenge] Attempt armed - {_attemptChallenge.GameMode} " +
                $"{_attemptChallenge.ObjectiveText}");
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
            if (_gameData == null || !_gameData.IsDailyChallenge) return;

            _attemptRunning = true;
            _attemptElapsed = 0f;
            _attemptBest = 0;
        }

        void TickAttempt()
        {
            _attemptElapsed += Time.deltaTime;

            int achieved = ReadLocalMetric(_attemptChallenge.Metric);
            if (achieved > _attemptBest) _attemptBest = achieved;

            float remaining = _attemptChallenge.TimeLimitSeconds > 0f
                ? Mathf.Max(0f, _attemptChallenge.TimeLimitSeconds - _attemptElapsed)
                : float.PositiveInfinity;

            OnAttemptProgress?.Invoke(_attemptBest, _attemptChallenge.TargetValue, remaining);

            bool met = _attemptBest >= _attemptChallenge.TargetValue;
            bool expired = _attemptChallenge.TimeLimitSeconds > 0f && remaining <= 0f;

            if (!met && !expired) return;

            // Either way the attempt is over - record it, then end the turn through the mode's
            // OWN end channel (the one TurnMonitorController raises) rather than tearing the
            // scene down ourselves, so the scoreboard, stats and replay flow are untouched.
            FinishAttempt(_attemptBest);
            RequestTurnEnd();
        }

        void RequestTurnEnd()
        {
            if (_gameData == null) return;

            // Only the launch authority may end a turn - a client raising it would end the turn
            // on its machine alone and desync the match. Solo and offline players ARE the server
            // under the eager-Relay design, so they fall straight through.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) return;

            _gameData.InvokeGameTurnConditionsMet();
        }

        void HandleTurnEnded()
        {
            // The mode reached its own end condition first (target hit, race over). Record what
            // the player actually achieved.
            if (_attemptRunning)
                FinishAttempt(Mathf.Max(_attemptBest, ReadLocalMetric(_attemptChallenge.Metric)));
        }

        void HandleGameEnded()
        {
            if (_attemptRunning || (_attemptArmed && !_attemptFinished))
                FinishAttempt(Mathf.Max(_attemptBest, ReadLocalMetric(_attemptChallenge.Metric)));

            ClearAttempt();
        }

        void HandleSessionEnded()
        {
            // Abandoning mid-run (quit to menu) deliberately records NOTHING: it is not a failed
            // attempt, and counting it would punish a disconnect.
            ClearAttempt();
        }

        void FinishAttempt(int achieved)
        {
            if (_attemptFinished) return;
            _attemptFinished = true;
            _attemptRunning = false;

            if (_data != null && _attemptChallenge.IsValid)
            {
                // Only fold in progress that belongs to TODAY's ask - a run that started before
                // midnight and ended after it is scored against the challenge it was launched
                // for, and that challenge is gone.
                if (_data.ChallengeDate == _attemptChallenge.DateKey &&
                    _data.TargetValue == _attemptChallenge.TargetValue)
                {
                    _data.RecordAttempt(achieved, _attemptChallenge.TargetValue, DateTime.UtcNow);
                    _repo?.MarkDirty();

                    CSDebug.LogVerbose(CSLogChannel.DailyChallenge,
                        $"[DailyChallenge] Attempt finished - achieved {achieved}/" +
                        $"{_attemptChallenge.TargetValue}, best {_data.BestValue}, " +
                        $"completed={_data.Completed}");
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
                _gameData.IsDailyChallenge = false;
        }

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

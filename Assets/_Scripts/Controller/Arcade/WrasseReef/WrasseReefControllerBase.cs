using System.Collections;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shared controller of the Wrasse and Grouper's three reef games (WRASSEREEF.md): Cleaner Ram (Charge),
    /// Reefbuild (Mass) and Blastline (Space). Each is the platform's one-round, one-turn domain race over a metric the
    /// platform already counts; the subclass names the metric, its breakdown and the AI's plan, and this class does the
    /// rest - the shape Kelpwood and Tapestry ship (HasEndGame = false, server winner detection, a snapshot
    /// SyncFinalScores_ClientRpc carrying the metric AND its breakdown, SetupNewRound suppressed after the whistle, the
    /// AI hooks armed at the countdown and cleared on despawn, end and replay).
    ///
    /// <para><b>The Wrasse is untouched</b> (the LOCKED vessel rule). What the game owns is the REEF: a peer-local
    /// crystal scatter weighted to the game's element (<see cref="WrasseReefCrystals"/>), which brings that element's
    /// motes to every kite that gathers it, and the AI's choice of where to fly the Grouper
    /// (<see cref="WrasseGrouperGuide"/>). The kite's loops, rams, pops and blasts score through the counters they
    /// already move in every mode.</para>
    ///
    /// WRASSE-ONLY is enforced by each arcade card's Vessels list - no mode-local vessel check.
    /// </summary>
    public abstract class WrasseReefControllerBase : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag this game's WrasseReef settings asset - the reef's crystals and the AI Grouper. The end " +
                 "condition lives in EndConditionOverridesSO, the point values on the rule.")]
        [SerializeField] protected WrasseReefSettingsSO settings;

        [Tooltip("Drag this game's scoring rule asset.")]
        [SerializeField] protected ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The reef's cell. Read-only to this controller: its centre anchors the crystal scatter and the AI's " +
                 "cruise, and its targeting grids answer 'where is the densest mass not my colour'.")]
        [SerializeField] protected Cell arenaCell;

        readonly WrasseReefCrystals _crystals = new();
        readonly WrasseGrouperGuide _guide = new();
        DomainRaceToasts _raceToasts;
        Coroutine _progressRoutine;
        Domains _leaderDomain = Domains.Blue;
        float _lastLeadAnnounce = float.NegativeInfinity;
        bool _finalResultsSent;
        bool _hintPosted;

        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        /// <summary>The Grouper's AI plan for this game.</summary>
        protected abstract WrasseGrouperGuide.Plan AIPlan { get; }

        /// <summary>The one hint posted to the local pilot as the turn starts.</summary>
        protected abstract GameToastSituation HintSituation { get; }

        /// <summary>Zeroes every counter this game's metric and breakdown read (server, and every peer on replay).</summary>
        protected abstract void ZeroCounters(IRoundStats stats);

        /// <summary>This game's metric for one pilot, as the rule reads it (orders the winner's representative).</summary>
        protected abstract float MetricOf(IRoundStats stats);

        /// <summary>The metric and its breakdown, as the server's snapshot carries them to every client.</summary>
        protected abstract void ReadSnapshot(IRoundStats stats, out float metric, out float breakdown);

        /// <summary>Writes the server's snapshot over a client's live values.</summary>
        protected abstract void WriteSnapshot(IRoundStats stats, float metric, float breakdown);

        /// <summary>A race to a target ticks the shared race beats (128-130); a timed game samples its lead instead.</summary>
        protected virtual bool IsTimed => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;
            _leaderDomain = Domains.Blue;

            // The hit latch is static and Time.time keeps running across a scene load, so a fast rematch could
            // otherwise inherit a claimed window and eat the first ram (Kelpwood's).
            VesselCombatHitLatch.Clear();

            if (IsServer) ZeroAll();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            _guide.Disarm();
            _crystals.Clear();
            base.OnNetworkDespawn();
        }

        void ZeroAll()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) ZeroCounters(list[i]);
        }

        protected override void OnCountdownTimerEnded()
        {
            // Peer-local BEFORE the server gate (Bloomrush's shape): the crystal scatter is local and deterministic,
            // so every machine lays it. SelectedIntensity is set by the config ClientRpc before the countdown starts.
            int intensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1;
            _crystals.Lay(settings, intensity, Centre);

            if (!IsServer) return;

            ZeroAll();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning($"[{GetType().Name}] no settings asset wired; the reef has no crystals and the AI " +
                                   "Groupers fly the platform's own crystal seeking.");
                return;
            }
            _guide.Arm(gameData, settings, _crystals, arenaCell, AIPlan, ShouldRaid);

            if (IsTimed)
            {
                StopProgressSampler();
                _progressRoutine = StartCoroutine(ProgressRoutine());
            }
        }

        void Update()
        {
            _crystals.Tick(Time.time);
            if (_finalResultsSent) return;

            if (!IsTimed && rule != null)
            {
                // Quarter, halfway and lead change as toasts: a local poll over the replicated score (128-130).
                _raceToasts ??= new DomainRaceToasts(rule);
                _raceToasts.Tick(gameData);
            }

            if (gameData == null || !gameData.IsTurnRunning) return;

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(HintSituation, local.Domain);
            }

            if (IsServer && _guide.IsArmed) _guide.Tick();
        }

        // ── A timed game's lead (server samples, every peer gets the toast) ──

        void StopProgressSampler()
        {
            if (_progressRoutine == null) return;
            StopCoroutine(_progressRoutine);
            _progressRoutine = null;
        }

        IEnumerator ProgressRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings ? settings.progressSampleSeconds : 1f));
            while (!_finalResultsSent)
            {
                SampleLead();
                yield return wait;
            }
            _progressRoutine = null;
        }

        void SampleLead()
        {
            if (!IsServer || rule == null || settings == null) return;
            if (Time.time - gameData.TurnStartTime < settings.leadAnnounceAfterSeconds) return;

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue || leader == _leaderDomain) return;

            int value = rule.DomainValue(gameData, leader);
            if (value <= 0) return;

            bool first = _leaderDomain == Domains.Blue;
            _leaderDomain = leader;
            if (first) return;   // the first leader after the opening is not a CHANGE
            if (Time.time - _lastLeadAnnounce < settings.leadAnnounceMinGapSeconds) return;

            _lastLeadAnnounce = Time.time;
            AnnounceLeadChanged_ClientRpc((int)leader, value);
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int value)
        {
            GameToastAPI.Post(GameToastSituation.DomainRaceLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), value.ToString());
        }

        /// <summary>Reefbuild's AI question: is this domain behind the leader (or is the clock nearly out)?</summary>
        bool ShouldRaid(Domains own)
        {
            if (rule == null || settings == null) return false;
            if (IsTimed)
            {
                if (!_clock) _clock = FindAnyObjectByType<TimeBasedTurnMonitor>();
                if (_clock && _clock.TimeRemaining <= settings.aiLateRaidSeconds) return true;
            }

            int mine = rule.DomainValue(gameData, own);
            int best = 0;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (d == own) continue;
                best = Mathf.Max(best, rule.DomainValue(gameData, d));
            }
            return best > 0 && mine < best * (1f - settings.aiRaidDeficitFraction);
        }

        TimeBasedTurnMonitor _clock;

        // ── Server-authoritative game end ──

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            if (rule == null) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s != null && s.Domain == winningDomain)
                .OrderByDescending(MetricOf)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopProgressSampler();
            _guide.Disarm();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var metricArray = new float[count];
            var breakdownArray = new float[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                ReadSnapshot(statsList[i], out metricArray[i], out breakdownArray[i]);
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, metricArray, breakdownArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] metrics,
            float[] breakdowns,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[{GetType().Name}] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // breakdown as 0. And it is the whistle's snapshot, not the live value: mass can still move in the
                // frames after the server decided.
                WriteSnapshot(stat, metrics[i], breakdowns[i]);
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            _finalResultsSent = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ──

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _hintPosted = false;
            _raceToasts = null;
            _leaderDomain = Domains.Blue;
            _lastLeadAnnounce = float.NegativeInfinity;

            StopProgressSampler();
            _guide.Disarm();
            _crystals.Clear();
            VesselCombatHitLatch.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                ZeroCounters(s);
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}

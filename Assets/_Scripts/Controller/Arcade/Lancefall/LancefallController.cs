using System.Collections.Generic;
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
    /// Lancefall - the Shrike's SPACE game: the wide, thin plates of neutral prisms the Shatterplane arena hangs in the
    /// cell (its cells, referenced read-only). The Void Lance is a 2,400 u hitscan that breaks every block it passes
    /// (SHRIKE.md §3), so a lance fired at a plate's face breaks a tile or two, and the same lance laid EDGE-ON along
    /// the plate's plane breaks a whole strip across it. First DOMAIN to the target of hostile prisms wins, golf-timed,
    /// on Rampage's metric (<see cref="ScoringMetric.PrismsDestroyed"/> through <see cref="LancefallScoringRuleSO"/>,
    /// resolved by <see cref="LancefallPrismTurnMonitor"/>). See LANCEFALL.md.
    ///
    /// <para><b>Nothing about the Shrike changes</b> (the LOCKED vessel rule). The lance, its range, its radius (Space)
    /// and its cooldown are the Shrike's own kit. The mode's levers are the plates (Shatterplane's cells), the crystals
    /// (the scene's CrystalManager) and the AI edgers. The arena's nucleus is play geometry here, not a claim, so the
    /// controller clears <see cref="Cell.NucleusIsControlZone"/> on every peer (Wrecking Ball's rule); it leaves the
    /// nucleus its standard size, since the Shrike reads nothing off it.</para>
    ///
    /// <para>Structurally <see cref="ShatterplaneController"/> without the Brittle Star's crystal field (1 round / 1 turn,
    /// HasEndGame=false, server winner detection, a snapshot SyncFinalScores_ClientRpc carrying the breakdown).
    /// SHRIKE-ONLY is enforced by the arcade card's Vessels list.</para>
    /// </summary>
    public class LancefallController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag LancefallSettings.asset - the AI edgers. The prism target lives in EndConditionOverridesSO, " +
                 "resolved by LancefallPrismTurnMonitor.")]
        [SerializeField] LancefallSettingsSO settings;

        [Tooltip("Drag LancefallScoringRule.asset (metric = PrismsDestroyed, Rampage's rule).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Shatterplane cell, read-only to this controller: its SpawnableShatterplane answers where the " +
                 "plates are.")]
        [SerializeField] Cell arenaCell;

        readonly List<LancefallEdger.Plate> _plates = new();
        readonly List<ShatterplanePlates.Structure> _structureScratch = new();
        readonly LancefallEdger _edger = new();
        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        Vector3 Centre => arenaCell ? arenaCell.transform.position : Vector3.zero;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;

            if (arenaCell) arenaCell.NucleusIsControlZone = false;

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            _edger.Disarm();
            base.OnNetworkDespawn();
        }

        /// <summary>Server-only: the setters push through server-write NetworkVariables.</summary>
        void ZeroCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].HostilePrismsDestroyed = 0;
                list[i].HostileVolumeDestroyed = 0f;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn

            if (settings == null)
            {
                CSDebug.LogWarning("[LancefallController] no settings asset wired; the AI edgers are not armed and fly " +
                                   "the platform's own steering.");
                return;
            }
            float plateRadius = CollectPlates();
            _edger.Arm(gameData, settings, Centre, _plates, plateRadius);
        }

        void Update()
        {
            if (_finalResultsSent) return;

            if (rule != null)
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
                if (local != null) GameToastAPI.Post(GameToastSituation.LancefallHint, local.Domain);
            }

            if (IsServer && _edger.IsArmed) _edger.Tick();
        }

        // ── The arena, read off its environment ──

        SpawnableShatterplane Environment()
        {
            var env = arenaCell ? arenaCell.GetComponentInChildren<SpawnableShatterplane>(true) : null;
            if (!env) env = FindAnyObjectByType<SpawnableShatterplane>();
            return env;
        }

        /// <summary>Every plate in world space, each plate of a stack its own (an edge-on lance lies in ONE plate's
        /// plane). Returns the plates' world radius, 0 when the cell has no SpawnableShatterplane.</summary>
        float CollectPlates()
        {
            _plates.Clear();
            var env = Environment();
            if (!env) return 0f;
            env.CollectStructures(_structureScratch);
            float front = env.FrontOffset;
            int plates = env.PlatesPerStack;
            var tf = env.transform;
            for (int i = 0; i < _structureScratch.Count; i++)
            {
                var st = _structureScratch[i];
                var axis = tf.TransformDirection(st.N).normalized;
                for (int p = 0; p < plates; p++)
                    _plates.Add(new LancefallEdger.Plate(
                        tf.TransformPoint(st.Centre + st.N * (front - p * env.ShelfGap)), axis));
            }
            _structureScratch.Clear();
            return env.PlateWorldRadius;
        }

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
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            _edger.Disarm();
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
            var prismsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsDestroyed,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Lancefall] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's
                // prisms as 0.
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
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

            _edger.Disarm();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.HostileVolumeDestroyed = 0f;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}

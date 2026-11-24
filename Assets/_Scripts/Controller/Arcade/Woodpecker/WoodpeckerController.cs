using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Woodpecker - the Honeyguide's SPACE game: a pecking race through Rampage's cactus forest. Hold the right trigger
    /// to see the aim line, let go to fire a burst of Pecks (3 shots at Space 0, 6 at Space 10; at Space 5 they pierce),
    /// and every hostile prism a round chips is taken off the board. The Ratel's Space cards bank stronger uses on the
    /// same trigger (Genesis Lance, the Little Bang grenade, the Particle Harmonizer, the Fugacity Discharger, the Sepia
    /// Drone, the jets), and what they break counts too. First DOMAIN to the hostile-prism target wins, golf-timed, on
    /// Rampage's metric (<see cref="ScoringMetric.PrismsDestroyed"/> through <see cref="WoodpeckerScoringRuleSO"/>),
    /// resolved by <see cref="WoodpeckerPrismTurnMonitor"/>. See WOODPECKER.md.
    ///
    /// <para><b>It changes nothing about the Honeyguide</b> (the LOCKED vessel rule). The arena is Rampage's, referenced
    /// read-only - the four cactus-forest configs The Bends, Bloomrush and Sirocco reference - because the mode wants
    /// exactly what that cell authors: a belt of breakable flora ringing the membrane that grows back, with intensity as
    /// its density. Your own colour's plants never score, so the forest also says whose stand is whose.</para>
    ///
    /// <para><b>The AI only STEERS.</b> The Honeyguide's crew already pecks when an AI flies it (the lab's
    /// <c>wanted8</c>: a hostile prism dead ahead within the gun's reach that is not a colour the battery holds,
    /// input-only), so a second presser would fight it. The hunters write <see cref="AIPilot.SetExternalTargetProvider"/>
    /// and nothing else: a run at the densest hostile mass (<see cref="Cell.GetExplosionTarget"/>), re-read on a slow
    /// clock so a stand is pecked through rather than abandoned. Crystal seeking is deliberately overridden - the
    /// objective here is mass, not a crystal (Sirocco's shape).</para>
    ///
    /// Structurally <see cref="SiroccoController"/> (1 round / 1 turn, HasEndGame=false, server winner detection, a
    /// snapshot SyncFinalScores_ClientRpc carrying the breakdown), with the race beats from
    /// <see cref="DomainRaceToasts"/> and one hint. HONEYGUIDE-ONLY is enforced by the arcade card's Vessels list.
    /// </summary>
    public class WoodpeckerController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag WoodpeckerSettings.asset - the AI's pecking runs. The prism target lives in " +
                 "EndConditionOverridesSO, resolved by WoodpeckerPrismTurnMonitor.")]
        [SerializeField] WoodpeckerSettingsSO settings;

        [Tooltip("Drag WoodpeckerScoringRule.asset (metric = PrismsDestroyed, Rampage's rule).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The cactus-forest cell. Read-only to this controller: its targeting grids answer 'where is the " +
                 "densest hostile mass' for an AI's pecking run.")]
        [SerializeField] Cell arenaCell;

        DomainRaceToasts _raceToasts;
        bool _finalResultsSent;
        bool _hintPosted;
        bool _huntersArmed;

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _hintPosted = false;

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            DisarmHunters();
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
            ArmHunters();
        }

        void Update()
        {
            if (_finalResultsSent || gameData == null) return;

            if (rule != null)
            {
                // Quarter, halfway and lead change as toasts: a local poll over the replicated score (128-130).
                _raceToasts ??= new DomainRaceToasts(rule);
                _raceToasts.Tick(gameData);
            }

            if (_hintPosted || !gameData.IsTurnRunning) return;
            _hintPosted = true;
            var local = gameData.LocalPlayer;
            if (local != null) GameToastAPI.Post(GameToastSituation.WoodpeckerHint, local.Domain);
        }

        // ── AI (server): steering only ──

        void ArmHunters()
        {
            // The providers below read settings every server frame; without it they would throw inside
            // AIPilot.Update. Fail safe: bots fly the platform's own crystal seeking.
            if (settings == null)
            {
                CSDebug.LogWarning("[WoodpeckerController] no settings asset wired; the AI pecking runs are not armed.");
                return;
            }

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                Vector3 run = centre;
                float nextTarget = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    if (captured.Vessel?.Transform == null) return centre;
                    if (Time.time >= nextTarget && arenaCell)
                    {
                        nextTarget = Time.time + settings.aiRetargetSeconds;
                        run = arenaCell.GetExplosionTarget(captured.Domain);
                    }
                    return run;
                });
            }
            _huntersArmed = true;
        }

        void DisarmHunters()
        {
            if (!_huntersArmed) return;
            _huntersArmed = false;
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }
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
            DisarmHunters();
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
                    CSDebug.LogError($"[Woodpecker] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The breakdown travels too: a client that only replicated the total would show every loser's prisms
                // as 0.
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

            DisarmHunters();

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

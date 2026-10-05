using System.Collections;
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
    /// Sirocco - the Butterfly's SPACE game: an erosion race through Rampage's cactus forest. On
    /// the Butterfly, SPACE is the REACH of the Scale Dust - the capsule hanging below the hull in
    /// Dust mode, 60 units long at rest and 150 at Space 10 - and on opposing mass the dust takes
    /// one prism in three it touches (the others it shrinks or steals). So the mode is flying LOW
    /// and LONG over the forest in Dust mode, and a longer capsule is a wider swath. First DOMAIN to
    /// destroy the hostile-prism target wins, on Rampage's metric and machinery
    /// (<see cref="ScoringMetric.PrismsDestroyed"/> through <see cref="SiroccoScoringRuleSO"/>).
    ///
    /// <para><b>The arena is Rampage's, referenced read-only</b> - the same four cactus-forest
    /// configs The Bends and Bloomrush reference, because the mode wants exactly what that cell
    /// authors (a belt of breakable flora ringing the membrane, and intensity as its DENSITY). A
    /// flora prism wearing the pilot's own colour is friendly: the dust TENDS it (grows, arms or
    /// shields it) instead, and destroying it would score nothing anyway - the rule StatsManager
    /// applies to all environment mass. So a pass over the forest erodes the other colours' plants
    /// and fortifies your own, which is the whole map read in one flight.</para>
    ///
    /// <para><b>The AI flies EROSION RUNS</b>: Dust mode for the whole turn
    /// (<see cref="ButterflyAutopilotModeDriver"/> - a Butterfly spawns in Mass mode and an
    /// autopilot presses nothing) and steering to a point ABOVE the densest hostile mass
    /// (<see cref="Cell.GetExplosionTarget"/>), re-read on a slow clock so a pass is finished rather
    /// than abandoned. Crystal seeking is deliberately overridden - the objective here is mass, not
    /// a crystal (the Wrecking Ball shape, the reverse of Rampage's rule).</para>
    ///
    /// Structurally <see cref="UndertowController"/>'s sibling (1 round / 1 turn, HasEndGame=false,
    /// server winner detection, snapshot SyncFinalScores_ClientRpc). BUTTERFLY-ONLY is enforced by
    /// the arcade card's Vessels list.
    /// </summary>
    public class SiroccoController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag SiroccoSettings.asset - lead feedback and AI. The prism target lives in " +
                 "EndConditionOverridesSO, resolved by SiroccoPrismTurnMonitor.")]
        [SerializeField] SiroccoSettingsSO settings;

        [Tooltip("Drag SiroccoScoringRule.asset (metric = PrismsDestroyed, Rampage's rule).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The cactus-forest cell. Read-only to this controller: its targeting grids answer " +
                 "'where is the densest hostile mass' for an AI's erosion run.")]
        [SerializeField] Cell arenaCell;

        readonly ButterflyAutopilotModeDriver _modes = new();

        bool _finalResultsSent;
        Coroutine _progressRoutine;
        Domains _leaderDomain = Domains.Blue;

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
            _leaderDomain = Domains.Blue;

            if (IsServer) ZeroCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            DisarmEroders();
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
                list[i].PrismStolen = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            ZeroCounters();

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmEroders();

            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

        // ── Lead feedback ───────────────────────────────────────────────────

        void StopProgressSampler()
        {
            if (_progressRoutine == null) return;
            StopCoroutine(_progressRoutine);
            _progressRoutine = null;
        }

        IEnumerator ProgressRoutine()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.1f, settings ? settings.progressSampleSeconds : 0.5f));
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

            int target = gameData.PrismTargetCount;
            if (target <= 0) return;

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue || leader == _leaderDomain) return;

            int prisms = rule.DomainValue(gameData, leader);
            if (prisms < settings.leadAnnounceFraction * target) return;

            bool first = _leaderDomain == Domains.Blue;
            _leaderDomain = leader;
            if (!first) AnnounceLeadChanged_ClientRpc((int)leader, prisms, target);
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int prisms, int target)
        {
            GameToastAPI.Post(GameToastSituation.SiroccoLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), prisms.ToString(), target.ToString());
        }

        // ── AI (server): erosion runs ────────────────────────────────────────

        void ArmEroders()
        {
            // The providers below read settings every server frame; without it they would throw
            // inside AIPilot.Update. Fail safe: bots fly the platform's own crystal seeking.
            if (settings == null)
            {
                CSDebug.LogWarning($"[SiroccoController] no settings asset wired; AI mode hooks not armed.");
                return;
            }

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            _modes.RetrySeconds = settings ? settings.aiModeRetrySeconds : 1f;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                float nextTarget = 0f;
                Vector3 run = centre;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;

                    // The only destructive verb this hull has - hold it on for the whole turn.
                    if (!_finalResultsSent) _modes.Drive(captured, wantDust: true);

                    if (Time.time >= nextTarget && arenaCell)
                    {
                        nextTarget = Time.time + settings.aiRetargetSeconds;
                        run = arenaCell.GetExplosionTarget(captured.Domain);
                    }
                    return run + selfTf.up * settings.aiDustHover;
                });
            }
        }

        void DisarmEroders()
        {
            _modes.ReleaseAll();
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
            StopProgressSampler();
            DisarmEroders();
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
                    CSDebug.LogError($"[Sirocco] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

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
            _leaderDomain = Domains.Blue;

            StopProgressSampler();
            DisarmEroders();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.HostileVolumeDestroyed = 0f;
                s.PrismStolen = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}

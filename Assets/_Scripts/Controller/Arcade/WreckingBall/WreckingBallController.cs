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
    /// Wrecking Ball - the Scarab-only demolition race, and Rampage's analog for the hull whose
    /// weapons are a BALL and a PLATE. A sphere court (the cell nucleus, resized - the Scramble
    /// court) is grown full of Rampage's five breakable flora; every bright crystal a Scarab
    /// flies through becomes its ball, and every hostile prism that ball plows through, or the
    /// juke dash's cavitation plate shreds, is credited to the pilot. First DOMAIN to the
    /// hostile-prism target wins (<see cref="ScoringMetric.PrismsDestroyed"/> - the Rampage /
    /// Cleave / Salvo metric and machinery, zero new scoring code).
    ///
    /// Structurally a sibling of <see cref="RampageController"/> (1 round / 1 turn,
    /// HasEndGame=false, server winner detection in OnTurnEndedCustom, snapshot
    /// SyncFinalScores_ClientRpc) with the court/cell integration of
    /// <see cref="ScarabScrambleController"/> (the nucleus IS the court, NucleusIsControlZone
    /// cleared). What is new is upstream of the mode and platform-wide:
    ///
    /// <para><b>1. A forged ball scores for its pilot.</b> The ball has always eaten opposing
    /// prisms; it named itself "Astro League" as the attacker, which is on no roster, so nothing
    /// it ate ever scored. <c>AstroLeagueBall.RecordPilotServer</c> now stamps the forger at the
    /// forge and every striker at every strike (replicated, because the prism scan runs on every
    /// peer and environment mass is credited by the machine that simulates the attacker), so a
    /// ball you bowl into the forest is a demolition tool rather than a payload - and a ball a
    /// rival bats away scores for THEM from then on, which is the Scarab way (Tollway: you score
    /// off other people's shots).</para>
    ///
    /// <para><b>2. An AI Scarab can dash.</b> The juke is stick-driven and autopilot vessels
    /// produce no stick input, so an AI could never fire the cavitation plate;
    /// <c>ScarabJukeController.TryAutopilotDash</c> runs a committed dash through the ordinary
    /// fire path for a host-simulated AI, and the plate follows it exactly as it follows a
    /// human's flick.</para>
    ///
    /// <para><b>The forest is INSIDE the court, and that is the whole arena.</b> A ball outside
    /// its cell's nucleus bleeds speed six times as fast (SCARAB.md §4.1c - a soft boundary,
    /// never a wall), so a forest planted where Rampage plants it (0.76-0.94 of the membrane,
    /// far outside any nucleus) would kill every ball that reached it. The four WreckingBall
    /// cells fork Rampage's five species with their planting band re-cut INSIDE this court and
    /// declare the nucleus play geometry, so the band's outside-the-nucleus clamp lifts
    /// (Docs/ECOSYSTEM.md §42) and the court wall carries every wild shot back through the
    /// stands. Crystals respawn in the nucleus - which is the whole court - so a ball is forged
    /// wherever the fight is.</para>
    ///
    /// SCARAB-ONLY is enforced entirely by the arcade card's Vessels list, read by the three
    /// platform layers (GameDataSO.SyncFromArcadeGame, ResolveSpawnVesselType, the AI clamp) -
    /// no mode-local vessel check, per the Astro League / Cleave rule.
    /// </summary>
    public class WreckingBallController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag WreckingBallSettings.asset - court, feedback, AI. The prism target lives " +
                 "in EndConditionOverridesSO (FrogletTools > Game Modes > End Game Conditions), " +
                 "resolved by WreckingBallPrismTurnMonitor.")]
        [SerializeField] WreckingBallSettingsSO settings;

        [Tooltip("Drag WreckingBallScoringRule.asset (metric = PrismsDestroyed, golf-timed).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The court cell. Its NUCLEUS is resized to the court radius - which IS the " +
                 "court, because every ball bounces off its cell's nucleus on its own. " +
                 "NucleusIsControlZone is cleared because the nucleus here is play geometry, " +
                 "not a territorial claim, and because the forest is planted inside it.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The cell's runtime data (the same asset the scene's NetworkCrystalManager " +
                 "writes crystal slots into). The AI reads it to find the nearest crystal when " +
                 "its domain has no ball to bowl.")]
        [SerializeField] CellRuntimeDataSO cellData;

        // Replicated court radius (server -> all; an NV so late joiners get the court).
        readonly NetworkVariable<float> n_CourtRadius =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        bool _finalResultsSent;
        float _appliedCourtRadius = -1f;
        Coroutine _progressRoutine;
        Domains _leaderDomain = Domains.Blue;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (RampageScoringRuleSO.AssignScores) - lower is better, like every race here.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom (server-side winner detection) ->
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base turn->round->game flow so there is no duplicate.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _leaderDomain = Domains.Blue;

            if (IsServer)
            {
                int intensity = gameData.SelectedIntensity != null
                    ? Mathf.Max(1, gameData.SelectedIntensity.Value)
                    : 1;
                // Resolved GEOMETRY replicates, not the intensity index - a client whose settings
                // asset ever drifts from the host's still builds the host's court.
                n_CourtRadius.Value = settings.CourtRadiusForIntensity(intensity);
            }

            n_CourtRadius.OnValueChanged += (_, _) => ApplyCourtConfig();
            ApplyCourtConfig();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            DisarmWreckers();
            base.OnNetworkDespawn();
        }

        // ── Court (every peer - the cell wiring is per-peer local, like Scramble's) ──

        void ApplyCourtConfig()
        {
            float radius = n_CourtRadius.Value;
            if (radius <= 0f) return;                                   // server hasn't published yet
            if (Mathf.Approximately(radius, _appliedCourtRadius)) return;
            _appliedCourtRadius = radius;

            if (!arenaCell) return;

            // The court IS the nucleus (AstroLeagueBall.ResolveNucleusBoundary bounces every ball
            // off its cell's nucleus by itself), so resizing the nucleus is building the court.
            arenaCell.SetNucleusWorldRadius(radius);
            // ...and it is play geometry, NOT a claim. Two things hang off this flag here, and
            // both are load-bearing: with it set every prism in the match would read as "inside
            // the nucleus" and the food web would starve (Docs/ECOSYSTEM.md §25.1); and the flora
            // planting band's outside-the-nucleus clamp would push the whole forest out past the
            // court wall (§42), where a ball's drag ramp kills it.
            arenaCell.NucleusIsControlZone = false;
        }

        // ── Turn start: AI + progress beats ──

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            if (!IsServer) return;
            ArmWreckers();
            StopProgressSampler();
            _progressRoutine = StartCoroutine(ProgressRoutine());
        }

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
                SampleProgress();
                yield return wait;
            }
            _progressRoutine = null;
        }

        /// <summary>
        /// Server-side sampler for the lead-change beat. The per-player destruction toasts are
        /// the platform's StatToastDriver (PrismsDestroyedMilestone, everyN authored on the toast
        /// config); this only announces the LEAD changing hands once the race is under way.
        /// </summary>
        void SampleProgress()
        {
            if (!IsServer || rule == null || settings == null) return;

            int target = gameData.PrismTargetCount;
            if (target <= 0) return;

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue) return;

            int leaderPrisms = rule.DomainValue(gameData, leader);
            if (leaderPrisms < target * settings.leadChangeAnnounceFraction) return;

            if (leader == _leaderDomain) return;
            bool announce = _leaderDomain != Domains.Blue;   // the FIRST leader is not a change
            _leaderDomain = leader;
            if (announce)
                AnnounceLeadChanged_ClientRpc((int)leader, leaderPrisms, target);
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int prisms, int target)
        {
            GameToastAPI.Post(GameToastSituation.WreckingBallLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), prisms.ToString(), target.ToString());
        }

        // ── AI (server) ──

        /// <summary>
        /// Steers every AI Scarab through the mode's own loop and lets it swing. No ball on your
        /// team -> fetch the nearest forge-source crystal (forging happens by flying through it -
        /// the AI needs no ability call); team ball live -> bowl it, aiming BEHIND the predicted
        /// ball on the far side from the densest hostile forest
        /// (<see cref="Cell.GetExplosionTarget"/>, the fauna hunting query), so driving to the
        /// aim point pushes the ball into the stands; no ball and no crystal -> dive at that
        /// forest yourself. Steering is <see cref="AIPilot.SetExternalTargetProvider"/> and
        /// nothing else. The PLATE is the second half: on a slow sample clock, whenever the
        /// densest hostile forest is within <see cref="WreckingBallSettingsSO.aiDashRange"/>,
        /// the AI asks its juke for a committed dash toward it
        /// (<see cref="ScarabJukeController.TryAutopilotDash"/>), and the cavitation plate rides
        /// that dash exactly as it rides a human's flick.
        /// </summary>
        void ArmWreckers()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                var juke = captured.Vessel?.Transform != null
                    ? captured.Vessel.Transform.GetComponent<ScarabJukeController>()
                    : null;
                AstroLeagueBall targetBall = null;
                Crystal targetCrystal = null;
                float nextSample = 0f;
                float nextDash = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;
                    Vector3 selfPos = selfTf.position;
                    Vector3 forest = arenaCell ? arenaCell.GetExplosionTarget(captured.Domain) : centre;

                    // THE PLATE. Sampled on its own clock; the juke refuses while spent or
                    // rolling, so this can never fire faster than the plate's cooldown allows.
                    if (juke != null && Time.time >= nextDash)
                    {
                        nextDash = Time.time + settings.aiDashSampleSeconds;
                        Vector3 toForest = forest - selfPos;
                        if (toForest.sqrMagnitude <= settings.aiDashRange * settings.aiDashRange)
                            juke.TryAutopilotDash(toForest);
                    }

                    bool latchDied =
                        (targetBall != null && !IsBallEscortable(targetBall, captured.Domain))
                        || (targetBall == null && targetCrystal != null
                            && (!targetCrystal.gameObject.activeInHierarchy
                                || !ScarabScrambleController.IsForgeSource(targetCrystal)));
                    if (Time.time >= nextSample || latchDied)
                    {
                        nextSample = Time.time + settings.aiRetargetSeconds;
                        targetBall = FindNearestDomainBall(captured.Domain, selfPos);
                        targetCrystal = targetBall == null ? FindNearestCrystal(selfPos) : null;
                    }

                    if (targetBall != null)
                    {
                        Vector3 predicted = targetBall.transform.position
                                            + targetBall.Velocity * settings.aiInterceptLeadSeconds;
                        Vector3 toForest = forest - predicted;
                        Vector3 push = toForest.sqrMagnitude > 1e-4f
                            ? toForest.normalized
                            : (predicted - selfPos).normalized;
                        return predicted - push * settings.aiApproachLead;
                    }

                    if (targetCrystal != null && targetCrystal.gameObject.activeInHierarchy)
                        return targetCrystal.transform.position;

                    // Nothing to bowl and nothing to forge: fly through the forest and let the
                    // plate do the work.
                    return forest;
                });
            }
        }

        void DisarmWreckers()
        {
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }
        }

        static bool IsBallEscortable(AstroLeagueBall ball, Domains domain) =>
            ball != null && !ball.IsHidden && !ball.IsFrozen && ball.LastHitDomain == domain;

        static AstroLeagueBall FindNearestDomainBall(Domains domain, Vector3 from)
        {
            AstroLeagueBall best = null;
            float bestSqr = float.MaxValue;
            var live = AstroLeagueBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var ball = live[i];
                if (!IsBallEscortable(ball, domain)) continue;
                float sqr = (ball.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = ball;
            }
            return best;
        }

        Crystal FindNearestCrystal(Vector3 from)
        {
            if (cellData == null || cellData.Crystals == null) return null;
            Crystal best = null;
            float bestSqr = float.MaxValue;
            var crystals = cellData.Crystals;
            for (int i = 0; i < crystals.Count; i++)
            {
                var crystal = crystals[i];
                if (!ScarabScrambleController.IsForgeSource(crystal) || !crystal.gameObject.activeInHierarchy) continue;
                float sqr = (crystal.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = crystal;
            }
            return best;
        }

        // ── Server-authoritative game end (Rampage pattern) ──

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
            DisarmWreckers();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return; // suppress the Ready button after the final whistle
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
                    CSDebug.LogError($"[WreckingBall] Client could not match RoundStats for '{sName}'. " +
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

        // ── Replay (full scene reload; state below covers the pre-reload window) ──

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _leaderDomain = Domains.Blue;
            StopProgressSampler();
            DisarmWreckers();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}

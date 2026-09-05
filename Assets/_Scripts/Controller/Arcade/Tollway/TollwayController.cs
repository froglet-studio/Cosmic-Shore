using System.Collections.Generic;
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
    /// Tollway — the Scarab-only ring race, and the mode built on the one idea the vessel's own
    /// design record calls its best and no mode had ever used (R_VesselActions/SCARAB.md §5):
    /// <b>a switch pays its PLACER when ANY ball threads it, friend or enemy.</b>
    ///
    /// The whole game in one sentence: plant rings anywhere you like, and every ball that threads
    /// one — yours, theirs, a stray off the wall — pays the pilot who planted it and raises a
    /// monument on the spot; first domain to the toll target wins.
    ///
    /// Three things make it neither Astro League nor Scarab Scramble, and each is the opposite of
    /// one of their rules:
    ///
    ///   1. THE SCORING SURFACES ARE PLACED BY PLAYERS AND CONSUMED ON USE. There is no fixed net
    ///      and no arena-owned hoop. A ring is one point, it is spent when it pays, and it must be
    ///      replanted — which is exactly why the switch's charge had to start recharging
    ///      (SCARAB.md §5.2). Where to put the next ring is the whole strategy layer.
    ///   2. YOU SCORE OFF OTHER PEOPLE'S SHOTS. Because any ball pays the ring's owner, the
    ///      defensive play and the economic play are the same play: rings belong where the
    ///      enemy's balls are going. A pilot who only attacks starves.
    ///   3. THE ARENA IS BUILT BY THE SCORING. Every paid toll raises a 255-prism scarab-wing
    ///      dais where it happened (SCARAB.md §5.1), so the terrain grows out of the match and
    ///      the scoreboard is readable off the court itself. Those monuments are ordinary
    ///      conserved mass — they block lanes, their danger blades punish pilots who fly them,
    ///      and the food web eats them once the cell's volume ladder wakes up.
    ///
    /// A BALL IS NOT SPENT BY A TOLL. Scramble detonates a scored ball because its hoops are
    /// permanent and its balls are the scarce thing; here it is the other way round, so one shot
    /// threading two rings is the mode's signature screamer (the CHAIN toast) and traffic is
    /// allowed to keep paying until something else claims it.
    ///
    /// Structurally a sibling of ScarabScrambleController (1 round / 1 turn, HasEndGame=false,
    /// server winner detection in OnTurnEndedCustom, snapshot SyncFinalScores_ClientRpc) with the
    /// same court integration: the nucleus IS the court and the ball supplies that wall itself,
    /// so building the arena is one <c>SetNucleusWorldRadius</c> call plus the declaration that
    /// the nucleus here is play geometry rather than a territorial claim.
    ///
    /// SCARAB-ONLY is enforced entirely by the arcade card's Vessels list, read by the three
    /// platform layers (GameDataSO.SyncFromArcadeGame, ResolveSpawnVesselType, the AI clamp) —
    /// no mode-local vessel check, per the Astro League / PeelTheCage rule.
    /// </summary>
    public class TollwayController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag TollwaySettings.asset — court, AI, scoring feel. The toll target lives in " +
                 "EndConditionOverridesSO (FrogletTools ▸ Game Modes ▸ End Game Conditions), " +
                 "resolved by TollwayTollTurnMonitor. The SWITCH's own numbers live on " +
                 "PlaceSwitchAction.asset, because they belong to the vessel, not to this mode.")]
        [SerializeField] TollwaySettingsSO settings;

        [Tooltip("Drag TollwayScoringRule.asset (metric = Goals, points not golf).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The court cell. Its NUCLEUS is resized to the court radius — which IS the " +
                 "court, because every ball bounces off its cell's nucleus on its own. " +
                 "NucleusIsControlZone is cleared because the nucleus here is play geometry, not " +
                 "a territorial claim (Docs/ECOSYSTEM.md §25.1) — skip that and every prism in " +
                 "the match reads as inside the nucleus and the food web silently starves.")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The cell's runtime data (the same asset the scene's NetworkCrystalManager " +
                 "writes crystal slots into). The AI reads it to find a crystal to forge from.")]
        [SerializeField] CellRuntimeDataSO cellData;

        // ── Replicated match config (server → all; an NV so late joiners get the court) ──
        readonly NetworkVariable<float> n_CourtRadius =
            new(readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server);

        bool _hooksInstalled;
        bool _finalResultsSent;
        float _appliedCourtRadius = -1f;

        // Lead tracking for the toast beats. Tolls are the ONLY score source, so the leader can
        // only change on a toll — no sampler coroutine needed (the Scramble simplification).
        Domains _leaderDomain = Domains.Blue;

        // Per-ball chain tracking: how many tolls THIS ball has paid inside the chain window.
        // Server-only. Pruned on every toll rather than on a timer, because tolls are rare and a
        // dictionary keyed on balls that have since died is the only thing here that could grow.
        readonly Dictionary<AstroLeagueBall, ChainRun> _chainByBall = new();
        readonly List<AstroLeagueBall> _chainScratch = new();

        struct ChainRun
        {
            public int Count;
            public float LastTollTime;
        }

        // Fauna exclusion sweep state (the Astro League / Scramble cleanup-crew pattern).
        float _faunaExclusionCurrent;

        // AI switch placement (server-only): when each AI next plants a ring.
        readonly Dictionary<IPlayer, float> _aiNextSwitchTime = new();

        protected override bool UseGolfRules => false;
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

            if (IsServer)
            {
                int intensity = gameData.SelectedIntensity != null
                    ? Mathf.Max(1, gameData.SelectedIntensity.Value)
                    : 1;
                // Resolved GEOMETRY replicates, not the intensity index — so a client whose
                // settings asset ever drifts from the host's still builds the host's court.
                n_CourtRadius.Value = settings.CourtRadiusForIntensity(intensity);
            }

            InstallHooks();

            n_CourtRadius.OnValueChanged += (_, _) => ApplyCourtConfig();
            ApplyCourtConfig();
        }

        public override void OnNetworkDespawn()
        {
            RemoveHooks();
            base.OnNetworkDespawn();
        }

        // ── The one hook the mode is built on ──

        void InstallHooks()
        {
            if (_hooksInstalled) return;
            // Subscribed on EVERY peer and gated on IsServer inside, rather than subscribed only
            // on the server: the event is raised per-peer by design (each machine runs its own
            // crossing test), so a client-side subscription is the natural place for anything
            // presentational to live later. Everything that SCORES is server-authoritative.
            ScarabSwitch.OnThreaded += HandleSwitchThreaded;
            _hooksInstalled = true;
        }

        void RemoveHooks()
        {
            if (!_hooksInstalled) return;
            ScarabSwitch.OnThreaded -= HandleSwitchThreaded;
            _hooksInstalled = false;
            _chainByBall.Clear();
            _aiNextSwitchTime.Clear();
        }

        /// <summary>
        /// SERVER: a ball threaded a switch. The payer is read off the SWITCH, never off the ball
        /// — "any ball pays the ring's owner" is the entire rule, so there is deliberately no
        /// arming gate, no ownership test and no own-goal here. Pushing your ball through an
        /// enemy's ring scores for THEM, and that is the mode's central tension rather than a
        /// trap: rings are visible, domain-coloured, and you chose your line.
        ///
        /// The switch spends and raises its dais by itself (it is a vessel ability, not mode
        /// content); this method only scores. The BALL is untouched and flies on, which is what
        /// makes a chain possible.
        /// </summary>
        void HandleSwitchThreaded(ScarabSwitch sw, AstroLeagueBall ball)
        {
            if (!IsServer || _finalResultsSent || sw == null) return;

            Domains scoringDomain = sw.PlacerDomain;
            if (scoringDomain == Domains.Blue) return;   // an unowned ring cannot pay anyone

            var stats = ResolveTollkeeperStats(sw.PlacerName, scoringDomain);
            if (stats == null) return;                   // roster empty for that domain

            stats.GoalsScored++;

            int chain = AdvanceChain(ball);

            int target = gameData.GoalTargetCount;
            int domainTolls = ScoringMetrics.SumByDomain(gameData, rule.Metric, scoringDomain);

            var leader = rule.ResolveWinner(gameData);
            bool leadChanged = leader != Domains.Blue && leader != _leaderDomain
                               && _leaderDomain != Domains.Blue;
            _leaderDomain = leader;

            bool matchPoint = target > 0 && domainTolls == target - 1;

            AnnounceToll_ClientRpc(new FixedString64Bytes(stats.Name ?? string.Empty),
                (int)scoringDomain, domainTolls, target, matchPoint, leadChanged, (int)leader,
                chain);
        }

        /// <summary>
        /// How many tolls this ball has paid in an unbroken run. A chain is the mode's signature
        /// moment — one shot through two rings — and it needs a WINDOW, or a ball that happens to
        /// wander back through a ring a minute later claims one it did not earn.
        /// </summary>
        int AdvanceChain(AstroLeagueBall ball)
        {
            if (ball == null) return 1;

            float now = Time.time;
            float window = settings != null ? settings.chainWindowSeconds : 4f;

            // Prune first: entries whose window has closed are dead weight, and a ball that has
            // since been destroyed would otherwise be kept alive as a dictionary key for the
            // rest of the match.
            _chainScratch.Clear();
            foreach (var kv in _chainByBall)
                if (kv.Key == null || now - kv.Value.LastTollTime > window)
                    _chainScratch.Add(kv.Key);
            for (int i = 0; i < _chainScratch.Count; i++) _chainByBall.Remove(_chainScratch[i]);

            int count = _chainByBall.TryGetValue(ball, out var run) ? run.Count + 1 : 1;
            _chainByBall[ball] = new ChainRun { Count = count, LastTollTime = now };
            return count;
        }

        /// <summary>
        /// Personal credit goes to the pilot who PLANTED the ring — the only person whose act
        /// this was. Falls back to that domain's best current contributor so a team sum never
        /// loses a toll to a disconnect mid-flight.
        /// </summary>
        IRoundStats ResolveTollkeeperStats(string placerName, Domains domain)
        {
            var list = gameData.RoundStatsList;
            if (list == null || list.Count == 0) return null;

            if (!string.IsNullOrEmpty(placerName))
            {
                var byName = list.FirstOrDefault(s =>
                    s != null && s.Name == placerName && s.Domain == domain);
                if (byName != null) return byName;
            }

            return list
                .Where(s => s != null && s.Domain == domain)
                .OrderByDescending(s => s.GoalsScored)
                .FirstOrDefault();
        }

        [ClientRpc]
        void AnnounceToll_ClientRpc(FixedString64Bytes tollkeeper, int domain, int domainTolls,
                                    int target, bool matchPoint, bool leadChanged, int leader,
                                    int chain)
        {
            var d = (Domains)domain;

            if (chain >= 2)
                GameToastAPI.Post(GameToastSituation.TollwayChain, d,
                    tollkeeper.ToString(), domainTolls.ToString(), target.ToString(),
                    chain.ToString());
            else
                GameToastAPI.Post(GameToastSituation.TollwayToll, d,
                    tollkeeper.ToString(), domainTolls.ToString(), target.ToString());

            if (matchPoint)
                GameToastAPI.Post(GameToastSituation.TollwayMatchPoint, d,
                    d.ToString(), domainTolls.ToString(), target.ToString());
            else if (leadChanged)
                GameToastAPI.Post(GameToastSituation.TollwayLeadChanged, (Domains)leader,
                    ((Domains)leader).ToString(), domainTolls.ToString(), target.ToString());
        }

        // ── Court (every peer — the cell wiring is per-peer local, like AL's arena) ──

        void ApplyCourtConfig()
        {
            float radius = n_CourtRadius.Value;
            if (radius <= 0f) return;                      // server hasn't published yet
            if (Mathf.Approximately(radius, _appliedCourtRadius)) return;
            _appliedCourtRadius = radius;

            if (!arenaCell) return;

            // The court IS the nucleus, and that is the entire court: a ball bounces off its
            // cell's nucleus by itself, everywhere (AstroLeagueBall.ResolveNucleusBoundary), so
            // this mode installs no per-ball boundary. Pushing a sphere onto each ball would
            // dress a platform behaviour up as a mode feature and leave every ball a Scarab
            // forges elsewhere with no containment at all.
            arenaCell.SetNucleusWorldRadius(radius);
            arenaCell.NucleusIsControlZone = false;
        }

        // ── Match start / AI ──

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            if (IsServer) ArmTollkeepers();
        }

        /// <summary>
        /// Steers every AI Scarab through the mode's own loop, and — the part without which an
        /// AI-only domain could not score at all — makes it PLANT RINGS.
        ///
        /// Steering: no ball on your team → fetch the nearest omni crystal (forging happens by
        /// flying through it, so the AI needs no ability call); team ball live → escort it,
        /// aiming BEHIND the predicted ball on the far side from your domain's nearest ring, so
        /// driving to the aim point pushes the ball ringward. Only the steering hook is installed
        /// (<c>SetExternalTargetProvider</c>), so nothing leaks into other modes, and throttle
        /// needs no wiring because the Scarab's transformer runs full throttle under autopilot.
        ///
        /// An AI deliberately aims at its OWN domain's rings. It will sometimes thread an
        /// enemy's by accident and pay them — which is the mode working, not a bug.
        /// </summary>
        void ArmTollkeepers()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            float firstDelay = settings != null ? settings.aiFirstSwitchDelaySeconds : 5f;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                _aiNextSwitchTime[p] = Time.time + firstDelay;

                var captured = p;
                AstroLeagueBall targetBall = null;
                Crystal targetCrystal = null;
                float nextSample = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;
                    Vector3 selfPos = selfTf.position;

                    // Resample on the pacing timer, or EARLY only when the held latch dies
                    // (escorted ball spent/stolen, fetched crystal collected). The latch test
                    // must cover BOTH states — gating on the ball alone makes the crystal-fetch
                    // state resample every frame, because IsBallEscortable(null, …) is false.
                    bool latchDied =
                        (targetBall != null && !IsBallEscortable(targetBall, captured.Domain))
                        || (targetBall == null && targetCrystal != null
                            && (!targetCrystal.gameObject.activeInHierarchy
                                || !IsForgeSource(targetCrystal)));
                    float retarget = settings != null ? settings.aiRetargetSeconds : 1f;
                    if (Time.time >= nextSample || latchDied)
                    {
                        nextSample = Time.time + retarget;
                        targetBall = FindNearestDomainBall(captured.Domain, selfPos);
                        targetCrystal = targetBall == null ? FindNearestCrystal(selfPos) : null;
                    }

                    if (targetBall != null)
                    {
                        float lead = settings != null ? settings.aiInterceptLeadSeconds : 0.5f;
                        float approach = settings != null ? settings.aiApproachLead : 45f;
                        Vector3 predicted = targetBall.transform.position
                                            + targetBall.Velocity * lead;
                        Vector3 ringPos = NearestOwnRingPosition(captured.Domain, predicted, centre);
                        Vector3 toRing = ringPos - predicted;
                        Vector3 push = toRing.sqrMagnitude > 1e-4f
                            ? toRing.normalized
                            : (predicted - selfPos).normalized;
                        return predicted - push * approach;
                    }

                    if (targetCrystal != null && targetCrystal.gameObject.activeInHierarchy)
                        return targetCrystal.transform.position;

                    return centre;
                });
            }
        }

        /// <summary>
        /// SERVER: plant an AI's ring when its timer comes up. It goes through
        /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/> — the same
        /// owner→server→every-peer trip a human's press makes — and NOT through
        /// <c>AIPilot.abilities</c>, which calls <c>StartAction</c> locally. An AI runs
        /// server-only, so a local press would build the ring and lay its dais on the server
        /// alone: invisible to every client, and conserved mass that exists on one machine.
        /// The rule the platform already records is "replicate an AI's press when the ability's
        /// output does not already ride some other replicated channel", and a placed structure
        /// rides nothing.
        ///
        /// The control is ASKED FOR by ability type rather than hardcoded, so if the Scarab ever
        /// rebinds the switch this keeps working.
        /// </summary>
        void TickAISwitchPlacement()
        {
            if (!IsServer || _finalResultsSent || gameData.Players == null) return;

            float interval = settings != null ? settings.aiSwitchIntervalSeconds : 22f;
            float now = Time.time;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;

                // An AI that ArmTollkeepers never saw - one spawned after the countdown, or a
                // backfill that arrived late - would otherwise never appear in this book and so
                // would never plant a ring, which in this mode means never score. Seed it here
                // rather than only at the countdown, so the book cannot be the thing that
                // silently excludes a pilot from the game.
                if (!_aiNextSwitchTime.TryGetValue(p, out float due))
                {
                    _aiNextSwitchTime[p] = now + (settings != null ? settings.aiFirstSwitchDelaySeconds : 5f);
                    continue;
                }
                if (now < due) continue;
                _aiNextSwitchTime[p] = now + interval;

                var handler = p.Vessel?.VesselStatus?.ActionHandler;
                if (handler == null) continue;
                if (!handler.TryGetInputForAction<PlaceSwitchActionSO>(out var input)) continue;

                // Press only. Placement is a one-shot on press and PlaceSwitchActionSO.StopAction
                // is a no-op, so a release would buy nothing but a second RPC per ring.
                handler.PerformShipControllerActionsReplicated(input);
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

        /// <summary>
        /// The nearest STANDING ring of <paramref name="domain"/>, or <paramref name="fallback"/>
        /// when that domain has none — which is the state an AI answers by planting one on its
        /// own timer, not by steering somewhere. Deliberately never returns another domain's
        /// ring: herding a ball into one pays the enemy.
        /// </summary>
        public static Vector3 NearestOwnRingPosition(Domains domain, Vector3 from, Vector3 fallback)
        {
            var ring = NearestOwnRing(domain, from);
            return ring ? ring.transform.position : fallback;
        }

        /// <summary>
        /// The nearest STANDING ring of <paramref name="domain"/>, or null. Shared with
        /// <see cref="TollwayObjectiveProvider"/> so the HUD arrow and the AI agree about which
        /// ring is "yours and nearest" — one answer, two readers.
        /// </summary>
        public static ScarabSwitch NearestOwnRing(Domains domain, Vector3 from)
        {
            ScarabSwitch best = null;
            float bestSqr = float.MaxValue;
            var live = ScarabSwitch.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var sw = live[i];
                if (sw == null || sw.PlacerDomain != domain) continue;
                float sqr = (sw.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = sw;
            }
            return best;
        }

        /// <summary>
        /// True for a crystal that FORGES a ball on contact: the omni pickup. Elemental crystals
        /// (fauna hearts, drops) power up elements instead, and an embedded heart is not
        /// collectable at all. Shared by the AI and <see cref="TollwayObjectiveProvider"/>; the
        /// predicate itself is the Scramble one, reused rather than re-derived.
        /// </summary>
        public static bool IsForgeSource(Crystal crystal) =>
            ScarabScrambleController.IsForgeSource(crystal);

        Crystal FindNearestCrystal(Vector3 from)
        {
            if (cellData == null || cellData.Crystals == null) return null;
            Crystal best = null;
            float bestSqr = float.MaxValue;
            var crystals = cellData.Crystals;
            for (int i = 0; i < crystals.Count; i++)
            {
                var crystal = crystals[i];
                if (!IsForgeSource(crystal) || !crystal.gameObject.activeInHierarchy) continue;
                float sqr = (crystal.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = crystal;
            }
            return best;
        }

        // ── Per-frame: fauna sweep (every peer) + AI ring planting (server) ──

        void Update()
        {
            UpdateFaunaExclusion();
            TickAISwitchPlacement();
        }

        void UpdateFaunaExclusion()
        {
            if (arenaCell == null || settings == null || !settings.faunaWaitOutsideCourt) return;
            if (_appliedCourtRadius <= 0f) return;   // court not published yet

            // "The court has silted up" is read from the cell's own volume ladder, never a
            // bespoke signal: Calm holds the swarm out; Restless+ lets it pour in to graze. In
            // this mode that silt is the MONUMENTS, so the crew arriving is the arena reporting
            // how much has been scored.
            float closed = _appliedCourtRadius * settings.faunaExclusionCourtFraction;
            float target = arenaCell.Phase == CellPhase.Calm ? closed : 0f;

            float rate = closed / Mathf.Max(0.1f, settings.faunaExclusionSweepSeconds);
            _faunaExclusionCurrent = Mathf.MoveTowards(_faunaExclusionCurrent, target,
                                                       rate * Time.deltaTime);
            arenaCell.FaunaExclusionRadius = _faunaExclusionCurrent;
        }

        // ── Server-authoritative game end (the DogFight / Scramble pattern) ──

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
                .OrderByDescending(s => s.GoalsScored)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;

            // Steering hooks come off at full time so the shared end-game flow owns the AIs.
            foreach (var p in gameData.Players)
                p?.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();

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
            var tollArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                tollArray[i] = statsList[i].GoalsScored;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, tollArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] tolls,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Tollway] Client could not match RoundStats for " +
                                     $"'{sName}'. Available: " +
                                     $"{string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.GoalsScored = tolls[i];
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
            _chainByBall.Clear();
            _aiNextSwitchTime.Clear();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.GoalsScored = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}

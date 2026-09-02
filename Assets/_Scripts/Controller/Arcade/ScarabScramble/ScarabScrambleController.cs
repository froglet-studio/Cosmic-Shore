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
    /// Scarab Scramble — the Scarab-only party game, and the accessible sibling of Astro
    /// League (design record: R_VesselActions/SCARAB.md; the mode answers its §15.11 as "a
    /// second mode that shares the arena machinery", and lands the §4.2–§4.5 mode-side ball
    /// work: permanent ownership, multi-ball, per-ball attribution, a ball ceiling).
    ///
    /// The whole game in one sentence: fly through a white crystal anywhere in the court and
    /// it becomes YOUR ball; roll, bat, or escort it through ANY hoop and your DOMAIN scores;
    /// first domain to the goal target wins. Three deliberate accessibility choices, each the
    /// opposite of an Astro League rule:
    ///
    ///   1. OWNERSHIP IS PERMANENT (SetOwnershipLockedServer). A ball is its maker's colour
    ///      forever, and the goal credits the BALL's domain — so there are no own goals and
    ///      no wrong way to touch anything. Defence is knocking enemy balls off their line,
    ///      never a way to score for the wrong side.
    ///   2. GOALS STOP NOTHING (§15.13's party answer). A scored ball detonates and play
    ///      flows on — no kickoff resets, no world-stop celebrations, nobody waits.
    ///   3. THE COURT IS A SPHERE and walls REFLECT. Every carom carries a wild shot back
    ///      toward the middle where the hoops are; a new player's mistakes are recycled into
    ///      chances instead of punished. (§4.3's boundary-death idea is deliberately NOT used
    ///      here — a resource you can waste is the wrong feel for a beachhead mode.)
    ///
    /// Structurally a sibling of DogFightController (1 round / 1 turn, HasEndGame=false,
    /// server winner detection in OnTurnEndedCustom, snapshot SyncFinalScores_ClientRpc), with
    /// the court/cell integration of AstroLeagueController (the nucleus IS the court,
    /// NucleusIsControlZone=false, the cleanup-crew fauna wait outside until the volume ladder
    /// says the pitch has silted up).
    ///
    /// SCARAB-ONLY is enforced entirely by the arcade card's Vessels list, read by the three
    /// platform layers (GameDataSO.SyncFromArcadeGame, ResolveSpawnVesselType, the AI clamp) —
    /// no mode-local vessel check, per the Astro League/PeelTheCage rule.
    /// </summary>
    public class ScarabScrambleController : MultiplayerDomainGamesController
    {
        [Header("Config")]
        [Tooltip("Drag ScarabScrambleSettings.asset — court, hoops, cap, AI. The goal target " +
                 "lives in EndConditionOverridesSO (FrogletTools ▸ Game Modes ▸ End Game " +
                 "Conditions), resolved by ScarabScrambleGoalTurnMonitor.")]
        [SerializeField] ScarabScrambleSettingsSO settings;

        [Tooltip("Drag ScarabScrambleScoringRule.asset (metric = Goals, points not golf).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The court cell. Its NUCLEUS is resized to the court radius — which IS the " +
                 "court, because every ball bounces off its cell's nucleus on its own. " +
                 "NucleusIsControlZone is cleared because the nucleus here is play geometry, " +
                 "not a territorial claim (Docs/ECOSYSTEM.md §25.1).")]
        [SerializeField] Cell arenaCell;

        [Tooltip("The cell's runtime data (the same asset the scene's NetworkCrystalManager " +
                 "writes crystal slots into). The AI reads it to find the nearest crystal " +
                 "when its domain has no ball to escort.")]
        [SerializeField] CellRuntimeDataSO cellData;

        // ── Replicated match config (server → all; NVs so late joiners get the court) ──
        readonly NetworkVariable<float> n_CourtRadius =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_HoopCount =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_HoopMouthRadius =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly List<ScarabScrambleHoop> _hoops = new();
        GameObject _hoopRoot;

        // Server-side per-ball attribution: who FORGED each live ball. The ball's replicated
        // domain answers "which team scores"; this answers "whose personal tally" — the §4.5.4
        // strike-list attribution that cannot express a ball is replaced by per-ball identity.
        readonly Dictionary<AstroLeagueBall, string> _forgerByBall = new();

        bool _forgeHooksInstalled;
        bool _finalResultsSent;
        float _appliedCourtRadius = -1f;
        int _appliedHoopCount = -1;
        float _appliedHoopMouth = -1f;

        // Lead tracking for the toast beats. Goals are the ONLY score source in this mode, so
        // the leader can only change on a goal event — no sampler coroutine needed.
        Domains _leaderDomain = Domains.Blue;


        // Fauna exclusion sweep state (the Astro League cleanup-crew pattern).
        float _faunaExclusionCurrent;

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
                n_HoopCount.Value = settings.HoopCountForIntensity(intensity);
                n_HoopMouthRadius.Value = settings.HoopMouthRadiusForIntensity(intensity);

                InstallForgeHooks();
            }

            n_CourtRadius.OnValueChanged += (_, _) => ApplyCourtConfig();
            n_HoopCount.OnValueChanged += (_, _) => ApplyCourtConfig();
            n_HoopMouthRadius.OnValueChanged += (_, _) => ApplyCourtConfig();
            ApplyCourtConfig();
        }

        public override void OnNetworkDespawn()
        {
            RemoveForgeHooks();
            base.OnNetworkDespawn();
        }

        // ── Court (every peer — visuals and cell wiring are per-peer local, like AL's arena) ──

        void ApplyCourtConfig()
        {
            float radius = n_CourtRadius.Value;
            int hoopCount = n_HoopCount.Value;
            float mouth = n_HoopMouthRadius.Value;
            if (radius <= 0f || hoopCount <= 0 || mouth <= 0f) return; // server hasn't published yet

            bool unchanged = Mathf.Approximately(radius, _appliedCourtRadius)
                             && hoopCount == _appliedHoopCount
                             && Mathf.Approximately(mouth, _appliedHoopMouth);
            if (unchanged) return;
            _appliedCourtRadius = radius;
            _appliedHoopCount = hoopCount;
            _appliedHoopMouth = mouth;

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            if (arenaCell)
            {
                // The court IS the nucleus: resize the standard nucleus sphere to the court
                // radius so the wall the ball bounces off is the surface the player sees —
                // no bespoke cage, no mesh morph, the Cell's own visual at a new size.
                //
                // AND THAT IS THE ENTIRE COURT. This mode installs no per-ball boundary: a ball
                // bounces off its cell's nucleus by itself, everywhere, so resizing the nucleus IS
                // building the court (AstroLeagueBall.ResolveNucleusBoundary). Pushing the same
                // sphere onto each ball as well used to be how the wall existed here, which dressed
                // a platform behaviour up as a mode feature — and left every ball a Scarab forges
                // anywhere ELSE with no containment at all. Astro League still overrides, because
                // its polytope court is a shape a nucleus radius cannot express.
                arenaCell.SetNucleusWorldRadius(radius);
                // …and declare it play geometry, NOT a territorial claim: without this every
                // prism in the match reads as "inside the nucleus" and the food web starves
                // (the Astro League lesson, Docs/ECOSYSTEM.md §25.1).
                arenaCell.NucleusIsControlZone = false;
            }

            BuildHoops(centre, radius, hoopCount, mouth);
        }

        void BuildHoops(Vector3 centre, float courtRadius, int count, float mouthRadius)
        {
            // A bare Destroy is tolerable here ONLY because a rebuild can never hit live play:
            // the court NVs are written once at server spawn and the zero-guard above keeps
            // clients from building until those real values land, so at most one build ever
            // shows on screen. A future mode feature that re-configures the court MID-MATCH
            // must wither the old hoops out (continuity of existence) before building anew.
            if (_hoopRoot != null) Destroy(_hoopRoot);
            _hoops.Clear();

            _hoopRoot = new GameObject("ScarabScrambleHoops");
            _hoopRoot.transform.SetParent(transform, false);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Hoop_{i}");
                go.transform.SetParent(_hoopRoot.transform, false);

                Vector3 pos;
                Vector3 axis;
                if (count == 1)
                {
                    // The sweaty layout: one hoop at the court centre, axis world-up — the
                    // sphere's focus point, where every carom eventually returns the ball.
                    pos = centre;
                    axis = Vector3.up;
                }
                else
                {
                    // The party layout: hoops ring the centre on the horizontal plane, mouths
                    // facing the middle (axis radial). Threading works from either side, so
                    // "drive it through a ring" needs no further explanation.
                    float angle = (Mathf.PI * 2f * i) / count;
                    Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    pos = centre + radial * (courtRadius * settings.hoopRingRadiusFraction);
                    axis = radial;
                }

                go.transform.position = pos;

                var hoop = go.AddComponent<ScarabScrambleHoop>();
                hoop.Configure(this, axis, mouthRadius, settings.hoopAccentColor,
                               settings.hoopFlareSeconds, serverDetects: IsServer);
                _hoops.Add(hoop);
            }
        }

        // ── Forge policy (server): the cap, the adoption, the attribution ──

        void InstallForgeHooks()
        {
            if (_forgeHooksInstalled) return;
            // NO ForgeGate: the live-ball rule is now the BALL's own cell overload (any domain,
            // any way in), so the mode installs no per-domain refusal. ScarabBallForge.ForgeGate
            // stays a live platform capability for a future mode — null means always allowed.
            ScarabBallForge.OnForged += HandleBallForged;
            AstroLeagueBall.OnCellOverload += HandleCellOverload;
            _forgeHooksInstalled = true;
        }

        void RemoveForgeHooks()
        {
            if (!_forgeHooksInstalled) return;
            ScarabBallForge.OnForged -= HandleBallForged;
            AstroLeagueBall.OnCellOverload -= HandleCellOverload;
            _forgeHooksInstalled = false;
            _forgerByBall.Clear();
        }

        /// <summary>
        /// SERVER + CLIENTS: a cell overloaded — four balls loose in one cell, any domain — and
        /// every one of them has just detonated. Announced by <c>AstroLeagueBall.OnCellOverload</c>,
        /// which is raised from a ClientRpc, so this runs on EVERY peer and the notice is one
        /// court-wide event rather than N unrelated bursts.
        ///
        /// It replaced a per-DOMAIN forge cap (a per-player count x that domain's roster,
        /// overloaded at forge time; its `ballsPerPlayer` setting is retired with it).
        /// Two reasons the cell rule is the better shape, beyond being what was asked for: the old
        /// cap could only ever fire on a FORGE, so a ball knocked loose from the nucleus — the
        /// Scarab's other way of putting one in play — could never trigger it; and being per-domain
        /// it fired at 2 balls for a solo pilot while the court held 6, which reads as arbitrary.
        /// Counting what is actually IN THE CELL, regardless of who made it, is a rule a player
        /// can see.
        /// </summary>
        void HandleCellOverload(Vector3 at, int count)
        {
            GameToastAPI.Post(GameToastSituation.ScarabScrambleBallCap, Domains.Blue, string.Empty);
            CSDebug.LogVerbose(CSLogChannel.ScarabNucleus,
                $"[ScarabScramble] Cell overload at {at} — {count} ball(s) detonated.");
        }

        void HandleBallForged(AstroLeagueBall ball, IVesselStatus forger)
        {
            if (ball == null) return;
            // Permanent colour (§4.2) is NOT installed here: ScarabBallForge locks every ball it
            // mints, so "the ball is its maker's and only a dash steals it" is a Scarab property
            // the mode inherits rather than configures. This is what makes "knock the enemy's
            // ball AWAY" the only sensible defence — pushing it through a hoop scores for them.
            // Containment is inherited the same way and so is likewise absent here: the ball
            // bounces off this cell's nucleus — the court — without being told to.
            _forgerByBall[ball] = forger != null ? forger.PlayerName : string.Empty;

            // No cap check here any more. A forged ball is only ONE of the ways a ball enters a
            // cell, and the ball itself notices all of them
            // (AstroLeagueBall.TickCellMembershipServer), so the overload cannot be reached by one
            // route and missed by another.
        }

        // ── Scoring (server; reported by the hoops) ──

        /// <summary>
        /// Server: a live ball crossed <paramref name="hoop"/>'s mouth. Scoring is gated on the
        /// ARMING rule (the design panel's accessibility fix): a crossing scores only when the
        /// ball's LAST TOUCH belongs to its owning domain — or nobody has touched it since the
        /// forge, so it still carries its maker's launch. An enemy shoving your ball through a
        /// ring therefore scores NOTHING (the ball sails on, disarmed, until your team re-touches
        /// it) — which is what makes "there is no wrong way to touch anything" literally true.
        /// The one enemy act that converts is the juke STEAL, resolved inside the ball itself.
        ///
        /// A scoring ball's domain scores; personal credit goes to the last toucher (the escort
        /// or the stealer), falling back to the forger. The ball is spent through its own
        /// detonation beat and play continues — goals stop nothing.
        /// </summary>
        public void HandleHoopThreadedServer(ScarabScrambleHoop hoop, AstroLeagueBall ball)
        {
            if (!IsServer || _finalResultsSent || ball == null) return;

            Domains scoringDomain = ball.LastHitDomain;
            if (scoringDomain == Domains.Blue)
            {
                // A neutral ball cannot exist in this mode (every forge stamps a domain), but
                // a stray Astro League-style ball must not crash scoring — spend it quietly.
                _forgerByBall.Remove(ball);
                ball.SpendServer();
                return;
            }

            // The arming gate. Blue = untouched since forge (the maker's own launch scores).
            bool armed = ball.LastTouchDomainServer == Domains.Blue
                         || ball.LastTouchDomainServer == scoringDomain;
            if (!armed) return; // disarmed pass-through: no score, no spend, ball plays on

            int bankBounces = ball.WallBouncesSinceTouchServer; // read BEFORE the spend
            var stats = ResolveScorerStats(ball, scoringDomain);
            _forgerByBall.Remove(ball);
            ball.SpendServer();
            if (stats == null) return; // roster empty for that domain — nothing to credit

            stats.GoalsScored++;

            int target = gameData.GoalTargetCount;
            int domainGoals = ScoringMetrics.SumByDomain(gameData, rule.Metric, scoringDomain);

            // Goals are the only score source, so the lead can only move HERE — the DogFight
            // sampler collapses into edge checks on the goal event itself.
            var leader = rule.ResolveWinner(gameData);
            bool leadChanged = leader != Domains.Blue && leader != _leaderDomain
                               && _leaderDomain != Domains.Blue;
            _leaderDomain = leader;

            bool matchPoint = target > 0 && domainGoals == target - 1;
            int hoopIndex = _hoops.IndexOf(hoop);

            AnnounceGoal_ClientRpc(hoopIndex, new FixedString64Bytes(stats.Name ?? string.Empty),
                (int)scoringDomain, domainGoals, target, matchPoint, leadChanged, (int)leader,
                bankBounces);
        }

        /// <summary>
        /// Personal credit: the LAST TOUCHER first (the escort who pushed it home, or the
        /// stealer — the arming gate guarantees they are on the scoring domain), then the
        /// forger, then the domain's best current contributor, so the team sum never loses a
        /// goal to a disconnect.
        /// </summary>
        IRoundStats ResolveScorerStats(AstroLeagueBall ball, Domains domain)
        {
            var list = gameData.RoundStatsList;
            if (list == null || list.Count == 0) return null;

            string toucherName = ball.LastToucherNameServer;
            if (!string.IsNullOrEmpty(toucherName))
            {
                var byToucher = list.FirstOrDefault(s =>
                    s != null && s.Name == toucherName && s.Domain == domain);
                if (byToucher != null) return byToucher;
            }

            _forgerByBall.TryGetValue(ball, out string forgerName);
            if (!string.IsNullOrEmpty(forgerName))
            {
                var byName = list.FirstOrDefault(s =>
                    s != null && s.Name == forgerName && s.Domain == domain);
                if (byName != null) return byName;
            }
            return list
                .Where(s => s != null && s.Domain == domain)
                .OrderByDescending(s => s.GoalsScored)
                .FirstOrDefault();
        }

        [ClientRpc]
        void AnnounceGoal_ClientRpc(int hoopIndex, FixedString64Bytes scorer, int domain,
                                    int domainGoals, int target, bool matchPoint,
                                    bool leadChanged, int leader, int bankBounces)
        {
            if (hoopIndex >= 0 && hoopIndex < _hoops.Count && _hoops[hoopIndex] != null)
                _hoops[hoopIndex].Flare();

            var d = (Domains)domain;

            // A 2+ carom goal is the mode's signature screamer — the sphere court manufactures
            // them for novices, so the room should hear about it (the panel's referral note).
            if (bankBounces >= 2)
                GameToastAPI.Post(GameToastSituation.ScarabScrambleBankGoal, d,
                    scorer.ToString(), domainGoals.ToString(), target.ToString(),
                    bankBounces.ToString());
            else
                GameToastAPI.Post(GameToastSituation.ScarabScrambleGoal, d,
                    scorer.ToString(), domainGoals.ToString(), target.ToString());

            if (matchPoint)
                GameToastAPI.Post(GameToastSituation.ScarabScrambleMatchPoint, d,
                    d.ToString(), domainGoals.ToString(), target.ToString());
            else if (leadChanged)
                GameToastAPI.Post(GameToastSituation.ScarabScrambleLeadChanged, (Domains)leader,
                    ((Domains)leader).ToString(), domainGoals.ToString(), target.ToString());
        }

        // ── Match start / AI ──

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            if (IsServer) ArmRollers();
        }

        /// <summary>
        /// Steers every AI Scarab through the mode's own loop: no ball on your team → fetch
        /// the nearest crystal (forging happens by flying through it — the AI needs no
        /// ability call); team ball live → escort it, aiming BEHIND the predicted ball on the
        /// far side from the nearest hoop, so driving to the aim point pushes it hoopward.
        /// Steering only — the provider drives AIPilot.SetExternalTargetProvider and nothing
        /// else, so nothing leaks into other modes. Throttle needs no wiring: the Scarab's
        /// transformer runs full throttle under autopilot.
        /// </summary>
        void ArmRollers()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

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
                    // must cover BOTH states — gating on the ball alone made the crystal-fetch
                    // state resample every frame, because IsBallEscortable(null, …) is false.
                    bool latchDied =
                        (targetBall != null && !IsBallEscortable(targetBall, captured.Domain))
                        || (targetBall == null && targetCrystal != null
                            && (!targetCrystal.gameObject.activeInHierarchy
                                || !IsForgeSource(targetCrystal)));
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
                        Vector3 hoopPos = NearestHoopPosition(predicted, centre);
                        Vector3 toHoop = hoopPos - predicted;
                        Vector3 push = toHoop.sqrMagnitude > 1e-4f
                            ? toHoop.normalized
                            : (predicted - selfPos).normalized;
                        return predicted - push * settings.aiApproachLead;
                    }

                    if (targetCrystal != null && targetCrystal.gameObject.activeInHierarchy)
                        return targetCrystal.transform.position;

                    return centre;
                });
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
        /// True for a crystal that FORGES a ball on contact: the omni pickup. Elemental
        /// crystals (fauna hearts, drops) power up elements instead, and an embedded heart is
        /// not collectable at all — steering a pilot or the marker at either teaches the wrong
        /// lesson in a mode whose whole loop is "crystal → ball" (the panel's Risk 2: the two
        /// classes share the lime family on screen and only brightness separates them).
        /// Shared by the AI and <see cref="ScarabScrambleObjectiveProvider"/>.
        /// </summary>
        public static bool IsForgeSource(Crystal crystal) =>
            crystal != null
            && !crystal.IsEmbedded
            && !crystal.crystalProperties.IsElemental; // CrystalProperties is a struct — always present

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

        Vector3 NearestHoopPosition(Vector3 from, Vector3 fallback)
        {
            Vector3 best = fallback;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _hoops.Count; i++)
            {
                var hoop = _hoops[i];
                if (hoop == null) continue;
                float sqr = (hoop.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = hoop.transform.position;
            }
            return best;
        }

        // ── Fauna: the cleanup crew waits outside the court until it silts up (AL pattern) ──

        void Update()
        {
            UpdateFaunaExclusion();
        }

        void UpdateFaunaExclusion()
        {
            if (arenaCell == null || settings == null || !settings.faunaWaitOutsideCourt) return;
            if (_appliedCourtRadius <= 0f) return;   // court not published yet

            // "The pitch is crowded" is read from the cell's own volume ladder, never a
            // bespoke signal: Calm holds the swarm out; Restless+ lets it pour in to graze.
            float closed = _appliedCourtRadius * settings.faunaExclusionCourtFraction;
            float target = arenaCell.Phase == CellPhase.Calm ? closed : 0f;

            float rate = closed / Mathf.Max(0.1f, settings.faunaExclusionSweepSeconds);
            _faunaExclusionCurrent = Mathf.MoveTowards(_faunaExclusionCurrent, target,
                                                       rate * Time.deltaTime);
            arenaCell.FaunaExclusionRadius = _faunaExclusionCurrent;
        }

        // ── Server-authoritative game end (DogFight pattern) ──

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
            var goalsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                goalsArray[i] = statsList[i].GoalsScored;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, goalsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] goals,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[ScarabScramble] Client could not match RoundStats for " +
                                     $"'{sName}'. Available: " +
                                     $"{string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.GoalsScored = goals[i];
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
            _forgerByBall.Clear();

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

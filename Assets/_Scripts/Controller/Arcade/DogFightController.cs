using System.Collections;
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
    /// Dog Fight - the Sparrow-only gun duel. Two to four pilots hunt each other through the
    /// Boneyard; a bullet hit banks 1 point, a missile hit (direct strike or caught in the
    /// blast) banks 50, and the first DOMAIN to the point target wins.
    ///
    /// Structurally a sibling of <see cref="RampageController"/> / <see cref="RibcageController"/>
    /// (1 round / 1 turn, HasEndGame=false, server winner detection in OnTurnEndedCustom,
    /// snapshot SyncFinalScores_ClientRpc), with three deliberate differences:
    ///
    ///   1. THE SCORE COMES FROM GUNNERY. The metric is <see cref="IRoundStats.CombatPoints"/>,
    ///      fed by the platform combat-hit path (the two combat-hit impact effects →
    ///      <c>GameDataSO.OnCombatHitLanded</c> → <c>StatsManager.CombatHitLanded</c> →
    ///      <c>CombatHitScoring.Credit</c>), so like Rampage and Ribcage there is no per-event
    ///      listener here at all. The only thing that scores is landing a shot on an OPPOSING
    ///      pilot: not prisms, not crystals, not wildlife.
    ///   2. THE ARENA IS COVER, NOT THE OBJECTIVE. Ribcage's bone IS the score and Wildlife
    ///      Liberation's cages ARE the walls; the Boneyard is neither. Shooting it is worth
    ///      nothing - it exists to break sightlines, so the fight is a series of close
    ///      encounters rather than one long open-space joust.
    ///   3. AI FLIES LEAD PURSUIT, NOT RAMMING. <see cref="ArmDogfighters"/> aims each AI at
    ///      where its quarry is GOING and breaks off on the overshoot; see that method for why
    ///      chasing the exact position makes a worse opponent, not a better one.
    ///
    /// SPARROW-ONLY is enforced entirely by the platform machinery Wildlife Liberation put in
    /// place and is deliberately not re-implemented here - three independent layers, all reading
    /// the single <c>Vessels</c> list on ArcadeGameDogFight:
    ///   (a) <c>GameDataSO.SyncFromArcadeGame</c> clamps the launching machine's selection;
    ///   (b) <c>ServerPlayerVesselInitializer.ResolveSpawnVesselType</c> re-clamps SERVER-SIDE at
    ///       spawn, which is the one that catches a client whose owner-write NetDefaultVesselType
    ///       still carries the hull it last flew;
    ///   (c) <c>ServerPlayerVesselInitializerWithAI</c> clamps the AI's scene-authored class too.
    /// </summary>
    public class DogFightController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag DogFightScoringRule.asset - the per-mode scoring strategy. It also owns " +
                 "the point VALUES (bullet 1 / missile 50), which is why the platform can count " +
                 "landed hits everywhere and score them only here.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Boneyard cell. Read-only to this controller: it supplies the arena CENTRE " +
                 "an AI falls back to when it has no quarry. The wreckage itself comes from the " +
                 "cell's config for the selected intensity (CellTypeChoiceOptions.IntensityWise).")]
        [SerializeField] Cell arenaCell;

        [Tooltip("Fraction of the point target at which the LEADING DOMAIN crosses the first " +
                 "milestone (toast + alert haptic). A FRACTION, so moving the target moves the " +
                 "milestones with it. Feedback only - no game state changes here.")]
        [SerializeField, Range(0.05f, 0.9f)] float firstMilestoneFraction = 0.25f;

        [Tooltip("Fraction of the point target at which the leading domain crosses the second " +
                 "milestone. Feedback only.")]
        [SerializeField, Range(0.1f, 0.95f)] float secondMilestoneFraction = 0.5f;

        [Tooltip("Seconds between server-side progress samples. Milestones are a coarse state " +
                 "machine, so this does not need to be per-frame.")]
        [SerializeField, Min(0.1f)] float progressSampleSeconds = 0.5f;

        [Header("Crystal pickups")]
        [Tooltip("How many ELEMENTAL crystals are scattered through the arena, on TOP of the " +
                 "scene's own omni crystal. Dog Fight scores gunnery and nothing else, so these " +
                 "are pure progression - and specifically Mass, which stretches the Sparrow's " +
                 "fired prisms. A reason to fly the wreckage between engagements. 0 disables.")]
        [SerializeField, Min(0)] int elementalCrystalCount = 14;

        [Tooltip("Radius of the shell the crystals scatter through. Keep it inside the arena " +
                 "(the Boneyard is 520 at every intensity) so they land among the cover.")]
        [SerializeField, Min(1f)] float crystalScatterRadius = 400f;

        [Tooltip("Seed for the crystal scatter. Placement is DETERMINISTIC from this plus the " +
                 "count, so every peer lays the same crystals in the same places without a " +
                 "network message - see SpawnElementalCrystals.")]
        [SerializeField] int crystalScatterSeed = 41;

        [Header("AI")]
        [Tooltip("Seconds between AI quarry re-selections. Between samples the AI keeps flying " +
                 "lead pursuit on the pilot it already picked, so this is 'how long before it " +
                 "reconsiders', not its steering rate.")]
        [SerializeField, Min(0.25f)] float aiRetargetSeconds = 1.5f;

        [Tooltip("How far ahead of its quarry an AI aims, as a multiple of the quarry's own " +
                 "per-second travel. Lead pursuit: aim where the target is GOING. 0 = pure " +
                 "pursuit (tail-chasing, and easy to shake).")]
        [SerializeField, Min(0f)] float aiLeadSeconds = 0.6f;

        [Tooltip("Inside this distance the AI COMMITS to a break-off: it stops chasing and flies " +
                 "a fixed escape vector until it is clear. This is what makes an AI read as a " +
                 "dogfighter rather than a battering ram.")]
        [SerializeField, Min(1f)] float aiBreakOffDistance = 120f;

        [Tooltip("How far out the AI extends before turning back in, as a MULTIPLE of the " +
                 "break-off distance. This is the 'run away a bit' half of the loop - the AI " +
                 "buys separation so its next pass is a firing pass instead of a wrestle.")]
        [SerializeField, Min(1f)] float aiExtendDistanceMultiplier = 3f;

        [Tooltip("Hard ceiling on one extend, in seconds, so an AI that loses its quarry mid-" +
                 "extend cannot fly to the membrane. It re-engages when EITHER the distance or " +
                 "this timer is satisfied, whichever comes first.")]
        [SerializeField, Min(0.5f)] float aiMaxExtendSeconds = 4f;

        // Milestone rungs the leading domain crosses. Feedback only - nothing here changes game
        // state, so a missed or late sample costs a toast, never a rule.
        const int MilestoneNone = 0;
        const int MilestoneFirst = 1;
        const int MilestoneSecond = 2;

        readonly List<Crystal> _spawnedCrystals = new();

        bool _finalResultsSent;
        Coroutine _progressRoutine;
        int _milestone = MilestoneNone;
        Domains _leaderDomain = Domains.Blue;

        // Golf: the winning domain's pilots carry their finish time, everyone else a
        // DnfThreshold+remaining sentinel - lower is better, like every race here.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base turn→round→game flow so there is no duplicate.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _milestone = MilestoneNone;
            _leaderDomain = Domains.Blue;

            // The hit latch is static and Time.time keeps running across a scene load, so a
            // fast rematch could otherwise inherit a claimed window and silently eat the first
            // hit of the new match. Cheap to clear, and it runs on every peer because the latch
            // is consulted wherever a shot is simulated - not only on the server.
            VesselCombatHitLatch.Clear();

            // Belt-and-braces against the Ribcage regression where players started a match on a
            // non-zero score. The authoritative reset is
            // ServerPlayerVesselInitializer.PrepareForNewScene (unconditional, once per player,
            // on the processing path) - this is a second, cheap sweep at the one moment every
            // peer agrees the match has not started, because RoundStats lives on the PERSISTENT
            // Player object and a stat that survives is worth zeroing twice rather than never.
            if (IsServer) ZeroCombatCounters();
        }

        public override void OnNetworkDespawn()
        {
            StopProgressSampler();
            ClearElementalCrystals();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Zeroes the scored stat and its breakdown on every roster entry. Server-only: the
        /// setters push through server-write NetworkVariables and replication clears every
        /// client's mirror, so a client zeroing its own would just be overwritten (and would
        /// desync until the next delta).
        /// </summary>
        void ZeroCombatCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].CombatPoints = 0;
                list[i].BulletHitsLanded = 0;
                list[i].MissileHitsLanded = 0;
            }
        }

        // ── Progress milestones (server samples, every peer gets the feedback) ──

        protected override void OnCountdownTimerEnded()
        {
            // Crystals are LOCAL objects laid identically on every peer from a fixed seed, so
            // this runs BEFORE the server gate - a client that never reached the lines below
            // would otherwise fly an arena with no pickups in it.
            ClearElementalCrystals();
            SpawnElementalCrystals();

            if (!IsServer) return;

            // The last moment before anyone can score. A player who joined late, or whose
            // PrepareForNewScene landed before their RoundStats had replicated its name, is on
            // the roster by now - so this is the sweep that actually guarantees "everyone starts
            // at 0" in a real lobby.
            ZeroCombatCounters();

            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmDogfighters();

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
            var wait = new WaitForSeconds(Mathf.Max(0.1f, progressSampleSeconds));
            while (!_finalResultsSent)
            {
                SampleProgress();
                yield return wait;
            }
            _progressRoutine = null;
        }

        /// <summary>
        /// Server-side sampler: which domain is leading and how far along it is. Both are coarse
        /// states, so a half-second cadence is ample and costs one roster scan per domain.
        /// </summary>
        void SampleProgress()
        {
            if (!IsServer || rule == null) return;

            int target = gameData.CombatPointTargetCount;
            if (target <= 0) return; // monitor hasn't resolved the target yet

            var leader = rule.ResolveWinner(gameData);
            if (leader == Domains.Blue) return;

            int leaderPoints = ScoringMetrics.SumByDomain(gameData, rule.Metric, leader);
            // Nobody has landed anything yet - no leader to announce rather than handing the
            // lead to whoever sorts first on a 0-0 tie-break.
            if (leaderPoints <= 0) return;

            float progress = leaderPoints / (float)target;
            int milestone = progress >= secondMilestoneFraction ? MilestoneSecond
                : progress >= firstMilestoneFraction ? MilestoneFirst
                : MilestoneNone;

            bool leaderChanged = leader != _leaderDomain;
            bool milestoneChanged = milestone != _milestone;
            if (!leaderChanged && !milestoneChanged) return;

            _leaderDomain = leader;

            if (milestoneChanged)
            {
                _milestone = milestone;
                if (milestone > MilestoneNone)
                    AnnounceMilestone_ClientRpc(milestone, (int)leader, leaderPoints, target);
            }
            else if (leaderChanged && milestone > MilestoneNone)
            {
                // The lead changes hands late in the fight - worth calling out.
                AnnounceLeadChanged_ClientRpc((int)leader, leaderPoints, target);
            }
        }

        [ClientRpc]
        void AnnounceMilestone_ClientRpc(int milestone, int domain, int points, int target)
        {
            GameToastAPI.Post(
                milestone == MilestoneSecond
                    ? GameToastSituation.DogFightHalfDown
                    : GameToastSituation.DogFightQuarterDown,
                (Domains)domain, ((Domains)domain).ToString(), points.ToString(), target.ToString());

            // Milestone reached: the alert haptic (the game's third feel, fenced to
            // match-changing events - see Docs/HAPTICS.md). Safe on every peer:
            // HapticController gates on the local player's own haptics setting.
            HapticController.PlayAlert();
        }

        [ClientRpc]
        void AnnounceLeadChanged_ClientRpc(int domain, int points, int target)
        {
            GameToastAPI.Post(GameToastSituation.DogFightLeadChanged, (Domains)domain,
                ((Domains)domain).ToString(), points.ToString(), target.ToString());
        }

        // ── Crystal pickups ──────────────────────────────────────────────────

        /// <summary>
        /// Scatters ELEMENTAL crystals through the arena, ALONGSIDE the scene's
        /// <c>NetworkCrystalManager</c> - they are two different pickups, not alternatives.
        ///
        /// <b>The manager still runs and still spawns the OMNI crystal</b>, on the same
        /// platform-normal settings every other mode uses (one crystal, <c>spawnOnClientReady</c>).
        /// The scene does author one thing the donor did not: <c>noNucleusSpawnRadius</c> 420.
        /// <c>CrystalManager.GetAnchorlessSpawnRadius</c> falls back to the cell's NUCLEUS radius
        /// and this cell has no nucleus by design, so without that override it fell through to the
        /// crystal's own <c>SphereRadius</c> and every spawn landed on the exact centre of the
        /// arena - which is how a pickup came to read as a bouncable objective.
        ///
        /// <b>Why elementals on top:</b> they are the mode's comeback loop made physical. A
        /// trailing pilot is already being buffed by <c>ElementalComebackSystem</c>; Mass stretches
        /// the Sparrow's fired prisms, so crystals scattered through the wreckage are a way to buy
        /// the same thing by flying for it. Scattered rather than central for the same reason the
        /// omni crystal needed a radius.
        ///
        /// <b>Why the runtime provisioning:</b> the four standalone elemental prefabs
        /// (<see cref="ElementalCrystalSetSO"/>) deliberately carry no collection components -
        /// lifeform prefabs author them as overrides - so they are scenery until something wires
        /// an <c>ElementalCrystalImpactor</c> + <c>ImpactCollider</c> onto them. That recipe is
        /// the platform's, not this mode's: it is exactly what the Wanderway's
        /// <c>Microscene.MintElementalCrystal</c> does, down to the collection effects coming off
        /// the set itself.
        ///
        /// <b>Why here rather than in the environment:</b> the arena is authored per intensity
        /// (four <c>SpawnableBoneyard</c> variants), and pickups have nothing to do with how much
        /// wreckage there is. Spawning from the controller covers every intensity with one path
        /// and one seed.
        ///
        /// <b>Determinism instead of replication.</b> Placement runs from a fixed seed and a
        /// fixed count, so every peer lays the same crystals in the same places with no network
        /// message. Collection is still LOCAL - each peer collects its own copy, the same
        /// standing caveat the Wanderway's crystals and the whole fauna simulation carry
        /// (<c>Docs/ECOSYSTEM.md</c> §7 caveat 4). That is tolerable here only because crystals
        /// score NOTHING in this mode; if they ever do, this has to become server-authoritative.
        /// </summary>
        void SpawnElementalCrystals()
        {
            if (elementalCrystalCount <= 0) return;

            var set = ElementalCrystalSetSO.Load();
            // No elemental set in this project state - the arena still works, just without
            // pickups. Same non-fatal stance Microscene takes.
            if (set == null) return;

            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            var rng = new System.Random(crystalScatterSeed);

            for (int i = 0; i < elementalCrystalCount; i++)
            {
                var element = ElementalCrystalSetSO.RandomElementFrom(rng);
                var prefab = set.GetPrefab(element);
                if (prefab == null) continue;

                // Equal-volume scatter through the shell (cube root of a uniform draw), so they
                // are spread evenly through the arena rather than bunched at the middle - a
                // uniform radius draw gives density proportional to 1/r^2.
                double u = rng.NextDouble();
                float r = crystalScatterRadius * Mathf.Pow((float)u, 1f / 3f);
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float y = (float)(rng.NextDouble() * 2.0 - 1.0);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var pos = centre + new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta)) * r;

                var crystal = Instantiate(prefab, pos, Quaternion.identity);
                // Sized BEFORE Start(): Crystal stamps crystalValue from lossyScale, and the
                // element-level gain reads it too.
                crystal.transform.localScale *= (float)(rng.NextDouble() * 0.7 + 0.5);
                crystal.enabled = true;
                crystal.gameObject.SetActive(true);

                var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
                impactor.Crystal = crystal;
                if (set.CollectionEffects is { Length: > 0 })
                    impactor.SetCollectionEffects(set.CollectionEffects);
                crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

                _spawnedCrystals.Add(crystal);
            }
        }

        void ClearElementalCrystals()
        {
            for (int i = 0; i < _spawnedCrystals.Count; i++)
                if (_spawnedCrystals[i]) Destroy(_spawnedCrystals[i].gameObject);
            _spawnedCrystals.Clear();
        }

        // ── AI dogfighters (server) ──────────────────────────────────────────

        /// <summary>
        /// Drives every AI Sparrow as a dogfighter rather than a missile.
        ///
        /// The AI's guns need no wiring here: the Sparrow prefab's <c>AIPilot</c> already runs
        /// FullAuto and SkyBurst on their own cooldowns, so an AI that has an opponent in front
        /// of it is already shooting. What this method decides is what "in front of it" means.
        ///
        /// <b>Lead pursuit, not pure pursuit.</b> <c>AIPilot</c> has no arrive-and-stop
        /// behaviour - it steers at its target forever and flies through on arrival - so an AI
        /// aimed at an opponent's CURRENT position permanently trails them and only ever fires
        /// at where they were. Aiming <see cref="aiLeadSeconds"/> ahead along the quarry's own
        /// course puts the nose where the target is going, which is both how a real intercept
        /// works and what makes the AI's shots connect.
        ///
        /// <b>Break off on the merge.</b> Inside <see cref="aiBreakOffDistance"/> the aim point
        /// flips to a spot BEYOND the quarry, so the AI commits to an overshoot and comes back
        /// around instead of grinding hull-to-hull. Without it, "steer at the enemy forever"
        /// degenerates into a ramming contest that neither pilot can shoot their way out of -
        /// the same class of mistake as Wildlife Liberation's AI orbiting a cage wall, and the
        /// exact inverse of Rampage, where ramming IS the scoring verb.
        ///
        /// Quarry selection re-runs every <see cref="aiRetargetSeconds"/> and simply takes the
        /// nearest live opponent, so a pilot who flies into a brawl gets picked up by whoever is
        /// closest rather than every AI in the arena converging on one victim. Between samples
        /// the AI keeps flying lead pursuit on the pilot it already picked - the provider is
        /// sampled every frame, so the aim point tracks a live position even though the CHOICE
        /// is re-made on a slow cadence.
        /// </summary>
        /// <summary>
        /// One AI's dogfight state. A pilot is either closing on someone or deliberately getting
        /// away from them; there is no third thing, and mixing the two is what produced the
        /// wrestling match this replaces.
        /// </summary>
        enum DogfightPhase { Pursue, Extend }

        /// <summary>
        /// Gives every AI a committed PURSUE → EXTEND → PURSUE loop.
        ///
        /// <b>Why a state machine and not a distance test.</b> The first version had no state: it
        /// aimed at the quarry, and inside the break-off radius aimed at a point derived from the
        /// CURRENT geometry instead. That point is recomputed every frame, so the moment the AI
        /// slipped past its target the vector to it flipped and the "escape" point landed back
        /// behind the AI — it turned straight round. The result was two ships welded together
        /// grinding in a circle, which is exactly the complaint. A break-off has to be a
        /// DECISION the pilot commits to, not a function of where the enemy is this instant.
        ///
        /// So the escape vector is <b>latched once</b> at the merge and flown until the AI is
        /// genuinely clear (<see cref="aiExtendDistanceMultiplier"/> × the break-off distance) or
        /// the extend times out. Only then does it look for a quarry again.
        ///
        /// <b>What this buys beyond not-ramming.</b> The Sparrow's AI already carries a skyburst
        /// on its ability list and always did; it fires on its own timer whether or not anyone is
        /// in front of it. Welded to a target at zero range, a rocket has no room to arm, fly, or
        /// put its blast anywhere useful, so the missiles were invisible. Separation is what makes
        /// them read: the AI comes back in from a few hundred units with the target ahead of it,
        /// which is the geometry a skyburst is for. The turret stance
        /// (<c>ModeSwitchingFire</c>, 2 s every 12 s) gets the same benefit.
        ///
        /// The whole loop is steering only — it drives <c>AIPilot.SetExternalTargetProvider</c>
        /// and nothing else. No weapon, ability, throttle or platform AI behaviour is touched
        /// from here, so this cannot leak into another mode.
        /// </summary>
        void ArmDogfighters()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            float extendDistance = aiBreakOffDistance * aiExtendDistanceMultiplier;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                IPlayer quarry = null;
                float nextSample = 0f;

                var phase = DogfightPhase.Pursue;
                Vector3 extendTarget = Vector3.zero;   // latched at the merge, then flown
                float extendUntil = 0f;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;
                    Vector3 selfPos = selfTf.position;

                    // ── EXTEND ──────────────────────────────────────────────────────────
                    // Fly the latched vector. Nothing about the quarry is read here, which is
                    // the entire point: a break-off the target can steer is not a break-off.
                    if (phase == DogfightPhase.Extend)
                    {
                        bool arrived = (extendTarget - selfPos).sqrMagnitude
                                       <= aiBreakOffDistance * aiBreakOffDistance;
                        if (!arrived && Time.time < extendUntil)
                            return extendTarget;

                        phase = DogfightPhase.Pursue;
                        nextSample = 0f;   // pick a quarry fresh on the turn-in
                    }

                    // ── PURSUE ──────────────────────────────────────────────────────────
                    if (Time.time >= nextSample || !IsLiveOpponent(quarry, captured))
                    {
                        nextSample = Time.time + aiRetargetSeconds;
                        quarry = FindNearestOpponent(captured, selfPos);
                    }

                    var quarryTf = quarry?.Vessel?.Transform;
                    // No live opponent (a 1v1 mid-respawn, everyone on our own domain) - loiter
                    // toward the arena centre rather than holding a stale or zero target.
                    if (quarryTf == null) return centre;

                    Vector3 quarryPos = quarryTf.position;
                    Vector3 toQuarry = quarryPos - selfPos;

                    // Merged. Commit to the break-off NOW and latch where it goes: straight on
                    // through the target and out the far side, so the pass keeps its energy
                    // instead of bleeding it in a turn.
                    if (toQuarry.sqrMagnitude <= aiBreakOffDistance * aiBreakOffDistance)
                    {
                        Vector3 through = toQuarry.sqrMagnitude > 1e-4f
                            ? toQuarry.normalized
                            : selfTf.forward;

                        phase = DogfightPhase.Extend;
                        extendTarget = quarryPos + through * extendDistance;
                        extendUntil = Time.time + aiMaxExtendSeconds;
                        quarry = null;   // re-acquire on the turn-in; it may not be the same pilot
                        return extendTarget;
                    }

                    // Closing: lead pursuit — aim where the target is GOING, not where it is.
                    var quarryStatus = quarry.Vessel?.VesselStatus;
                    Vector3 lead = quarryStatus != null
                        ? quarryStatus.Course * (quarryStatus.Speed * aiLeadSeconds)
                        : Vector3.zero;
                    return quarryPos + lead;
                });
            }
        }

        /// <summary>The nearest player on a different domain with a live vessel.</summary>
        IPlayer FindNearestOpponent(IPlayer self, Vector3 from)
        {
            IPlayer best = null;
            float bestSqr = float.MaxValue;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveOpponent(candidate, self)) continue;

                float sqr = (candidate.Vessel.Transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveOpponent(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;  // teammates cannot be shot at all
            return candidate.Vessel?.Transform != null;
        }

        // ── Server-authoritative game end ────────────────────────────────────

        /// <summary>
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so _finalResultsSent is set in time to suppress
        /// the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
            if (rule == null) return;

            // Winning domain (highest point sum, Jade→Ruby→Gold tie-break) delegated to the
            // rule; representative winner-name = the best individual contributor on that domain
            // (legacy display field - victory/defeat attribution uses WinnerDomain).
            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s != null && s.Domain == winningDomain)
                .OrderByDescending(s => s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            StopProgressSampler();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the fight just ended. HasEndGame=false
        /// causes ExecuteServerRoundEnd to call SetupNewRound instead of ExecuteServerGameEnd -
        /// this override prevents the Ready button from reappearing.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────────

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var pointsArray = new int[count];
            var bulletArray = new int[count];
            var missileArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                pointsArray[i] = statsList[i].CombatPoints;
                bulletArray[i] = statsList[i].BulletHitsLanded;
                missileArray[i] = statsList[i].MissileHitsLanded;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, pointsArray,
                bulletArray, missileArray, new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] points,
            int[] bulletHits,
            int[] missileHits,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[DogFight] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.CombatPoints = points[i];
                // The breakdown travels too: the scoreboard's secondary line is "N pts, X
                // bullets, Y rockets", and a client that only replicated the total would show
                // every loser's breakdown as 0-0.
                stat.BulletHitsLanded = bulletHits[i];
                stat.MissileHitsLanded = missileHits[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ───────────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _milestone = MilestoneNone;
            _leaderDomain = Domains.Blue;

            StopProgressSampler();
            VesselCombatHitLatch.Clear();
            ClearElementalCrystals();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.CombatPoints = 0;        // the scored metric AND the milestone trigger
                s.BulletHitsLanded = 0;
                s.MissileHitsLanded = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}

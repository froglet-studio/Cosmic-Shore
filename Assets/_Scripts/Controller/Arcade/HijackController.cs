using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hijack - the <b>Urchin-only</b> heist race through the Switchyard, and the first mode on
    /// the platform whose score is OWNERSHIP rather than destruction: nothing here is ever
    /// removed, mass only changes hands. First DOMAIN to steal the prism target wins
    /// (<see cref="ScoringMetric.PrismsStolen"/> against
    /// <see cref="GameDataSO.PrismTargetCount"/>).
    ///
    /// <para><b>The three verbs are all the Urchin's own, and the arena is what makes each one
    /// pay.</b> ATTACH: the yard is 24 open rails, so there is always one to latch onto, and each
    /// is painted in three domain thirds - your colour grinds at 150, someone else's crawls at 10
    /// while flipping every prism you cross, so the speed cliff IS the tutorial that stealing is
    /// the score. LAUNCH: a rail's far end is a real end, and
    /// <c>SpawnableSwitchyard</c> places every burr exactly on the tangent that end throws you
    /// along - fly it out and you arrive at a cluster without steering. STEAL: a burr is a few
    /// hundred prisms in one colour, and the chain cascade rakes a hundred of them in a
    /// heartbeat.</para>
    ///
    /// <para><b>The launch pays NOTHING bespoke, deliberately.</b> No per-launch bonus, no
    /// airtime multiplier. It pays through geometry (burrs sit on rail-end tangents), physics
    /// (<c>Gun.FireSingle</c> composes a spike's velocity as direction*speed + the vessel's, so a
    /// volley thrown at grind speed reaches roughly 3.5x further than one thrown at cruise) and
    /// economy (only RIDING banks spike ammo). Scoring the record of a manoeuvre rather than its
    /// effect is the scripted-outcome cheat; Dog Fight and Bends weight distinct scoring EVENTS,
    /// and the event here is the prism - a steal is a steal, however you reached it.</para>
    ///
    /// <para>Structural clone of <see cref="SalvoController"/>: 1 round / 1 turn,
    /// server-authoritative winner detection in <see cref="OnTurnEndedCustom"/>, final scores
    /// replicated by snapshot ClientRpc, golf-timed (winners carry their finish time).</para>
    ///
    /// <para><b>No food web, and the reason is the comeback.</b> The cell's SpawnProfile is empty.
    /// In a nucleus-less cell herbivores eat OPPOSING-domain mass, and the leader's colour is by
    /// definition the most abundant - so a swarm would preferentially eat whatever the TRAILING
    /// team had just stolen. An anti-comeback current is the wrong current in a mode whose entire
    /// economy is contested ownership. The profile is the one-asset door if it is ever wanted.</para>
    ///
    /// <para>Urchin-only is enforced entirely by the platform's three clamp layers, all reading
    /// the single <c>Vessels</c> list on ArcadeGameHijack (<c>GameDataSO.SyncFromArcadeGame</c>,
    /// <c>ServerPlayerVesselInitializer.ResolveSpawnVesselType</c>, and the AI clamp in
    /// <c>ServerPlayerVesselInitializerWithAI</c>) - never re-implemented here.</para>
    /// </summary>
    public class HijackController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag HijackScoringRule.asset - the per-mode scoring strategy (winner, scores, " +
                 "results). Metric = PrismsStolen, golf-timed like Rampage and Salvo.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Arena")]
        [Tooltip("The Switchyard cell. Read-only to this controller: it supplies the arena centre " +
                 "an AI falls back to when the yard has not finished laying. The rails and burrs " +
                 "come from the cell's config for the selected intensity " +
                 "(CellTypeChoiceOptions.IntensityWise).")]
        [SerializeField] Cell arenaCell;

        [Header("AI")]
        [Tooltip("Seconds between AI raid-target refreshes - how often each AI Urchin re-picks " +
                 "the rail it is running. Slow on purpose: a heist leg is a whole grind plus a " +
                 "launch, and re-deciding mid-rail is what turns a raider into a ditherer.")]
        [SerializeField, Min(0.5f)] float aiRetargetSeconds = 3f;

        [Tooltip("How far short of a rail's near end the AI aims while approaching. Arriving " +
                 "ALONG the rail is what makes TrailFollower.Attach seed its travel direction " +
                 "toward the far end; arriving broadside latches it pointing at the wall it " +
                 "just came through.")]
        [SerializeField, Min(10f)] float aiRailApproachLead = 60f;

        [Tooltip("Inside this distance of a rail's near end the AI stops aiming at the approach " +
                 "lead and aims THROUGH the rail, at a point past its far end.")]
        [SerializeField, Min(10f)] float aiRailCommitDistance = 80f;

        [Tooltip("How far past a rail's far end (and past a burr's centre) the AI aims, so it " +
                 "flies THROUGH rather than orbiting a point it has arrived at. AIPilot has no " +
                 "arrive-and-stop behaviour - see Docs AI_ORBIT_BREAK.md.")]
        [SerializeField, Min(50f)] float aiThroughDistance = 200f;

        [Tooltip("Seconds between AI spike taps while it is grinding hostile rail or raking a " +
                 "burr. The volley converts the prisms ahead and restores grind speed.")]
        [SerializeField, Min(0.5f)] float aiSpikeIntervalSeconds = 2f;

        [Tooltip("Minimum spike ammo (0-1 of the meter) before the AI will spend a tap. Riding " +
                 "recharges it, so this is what keeps an AI from arriving at a burr dry.")]
        [SerializeField, Range(0f, 1f)] float aiMinSpikeAmmo = 0.15f;

        [Tooltip("Seconds an AI may sit on a rail below aiParkedSpeed before it Slips off and " +
                 "picks a different one. This catches a ride that has genuinely STOPPED - a " +
                 "reversal parked in the throttle deadband, a ribbon whose prisms were taken out " +
                 "from under it. It deliberately does NOT catch the 10 u/s hostile crawl, which " +
                 "is a raid in progress: the crawler is converting one prism per hop and will " +
                 "cross a 13-prism third in about ten seconds.")]
        [SerializeField, Min(1f)] float aiStuckSeconds = 6f;

        [Tooltip("Speed below which an attached AI counts as PARKED, world units per second. " +
                 "Under the 10 u/s hostile crawl on purpose - see aiStuckSeconds. Raising it " +
                 "past 10 would make every legitimate raid read as a stall and Slip the AI off " +
                 "the mass it was stealing.")]
        [SerializeField, Min(0.5f)] float aiParkedSpeed = 6f;

        [Tooltip("Seconds a rail the AI just Slipped off is excluded from its next choice. " +
                 "ChooseRail is deterministic given position and domain, so without this the AI " +
                 "re-picks the rail it just abandoned on the very next frame and the escape " +
                 "hatch is a no-op.")]
        [SerializeField, Min(1f)] float aiSlippedRailCooldown = 10f;

        // The Urchin's controls, from Resources/ElementalAbilityMaps/Urchin.asset. Named rather
        // than looked up: the AI drives exactly two of the four, and a binding sweep that
        // silently found nothing would read as an AI that simply never used its weapon.
        const InputEvents SpikeControl = InputEvents.RightStickAction;   // Charge - Chain Spikes
        const InputEvents SlipControl = InputEvents.Button2Action;       // Time  - Slip

        bool _finalResultsSent;

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

            // RoundStats lives on the PERSISTENT Player object, so a stat that survives a scene
            // load is worth zeroing twice rather than never (the PeelTheCage regression where
            // players started a match already on the board).
            if (IsServer) ZeroStealCounters();
        }

        public override void OnNetworkDespawn()
        {
            DisarmRaiders();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Zeroes the scored stat on every roster entry. Server-only: the setter pushes through a
        /// server-write NetworkVariable and replication clears every client's mirror.
        /// </summary>
        void ZeroStealCounters()
        {
            var list = gameData.RoundStatsList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                list[i].PrismStolen = 0;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // The last moment before anyone can score - a late joiner is on the roster by now.
            ZeroStealCounters();

            base.OnCountdownTimerEnded();

            ArmRaiders();
        }

        // ── The AI raider loop ───────────────────────────────────────────────

        /// <summary>
        /// Arms one closure per AI pilot. Steering goes through
        /// <c>AIPilot.SetExternalTargetProvider</c> and abilities through
        /// <c>R_VesselActionHandler.PerformShipControllerActionsReplicated</c>.
        ///
        /// <para><b>Overriding crystal seeking is correct HERE and would be a defect in Rampage.</b>
        /// The prohibition Rampage records is about a mode whose OBJECTIVE is a crystal - there,
        /// replacing the seek disarms the vessel outright. This mode's objective is a burr; the
        /// yard's one omni crystal is a passing elemental pickup, and an AI that spent the match
        /// orbiting it would steal nothing.</para>
        ///
        /// <para><b>The AI's flight plan is the player's, in three states.</b> APPROACH a chosen
        /// rail's near end from outside, so the latch seeds its direction down-rail; RIDE, aiming
        /// past the far end so the nose stays along the ribbon (and tapping spikes when the mass
        /// under it is not its own); then RAID the burr that rail throws it at. It never targets a
        /// point it is going to arrive at and stop on - every target is a point BEYOND the thing
        /// it wants, because AIPilot has no arrive-and-stop behaviour and a target inside its own
        /// minimum turn radius becomes something it orbits (Docs/AI_ORBIT_BREAK.md).</para>
        ///
        /// <para>The ONE authored field this leans on is <c>ram: 1</c> on the Urchin prefab's
        /// AIPilot - see HIJACK.md "Why the Urchin needed ram". Without it an AI grinds at 30 u/s,
        /// slower than it flies, and carries nothing off a launch.</para>
        /// </summary>
        void ArmRaiders()
        {
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                var pilot = status?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                int rail = -1;
                int excludedRail = -1;
                float excludedUntil = 0f;
                float nextRetarget = 0f;
                float nextSpike = 0f;
                // Stamped NOW, not 0: the stall test is Time.time - movingSince, and a zero
                // seed makes the very first attached frame read as several minutes parked.
                float movingSince = Time.time;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    var self = captured.Vessel?.VesselStatus;
                    var yard = HijackYard.Current;
                    if (selfTf == null || self == null || yard == null || yard.Rails.Count == 0)
                        return centre;

                    Vector3 pos = selfTf.position;
                    var domain = captured.Domain;

                    // Riding the rail we chose is the one state worth protecting: the whole
                    // value of a rail is riding it to the END, and re-deciding mid-grind throws
                    // away both the steal and the launch it was building toward. Attachment
                    // ALONE is not that state - a burr is attachable too (its prisms carry a
                    // Volume trail and the surface follower keeps IsAttached true), so a raider
                    // that reached the cluster its rail aimed it at would otherwise be pinned in
                    // the ride branch, steered back at the rail it came from, and unable to
                    // re-pick for as long as it stuck to the burr.
                    bool onChosenRail = rail >= 0 && rail < yard.Rails.Count
                                        && self.IsAttached && self.AttachedPrism
                                        && self.AttachedPrism.Trail == yard.Rails[rail].Trail;

                    if (!onChosenRail && Time.time >= nextRetarget)
                    {
                        nextRetarget = Time.time + aiRetargetSeconds;
                        rail = ChooseRail(yard, pos, domain,
                                          Time.time < excludedUntil ? excludedRail : -1);
                    }

                    if (rail < 0 || rail >= yard.Rails.Count) return centre;
                    var r = yard.Rails[rail];
                    Vector3 railStart = yard.WorldPoint(r.LocalStart);
                    Vector3 railEnd = yard.WorldPoint(r.LocalEnd);
                    Vector3 along = (railEnd - railStart).normalized;

                    // ── RIDE ────────────────────────────────────────────────
                    if (onChosenRail)
                    {
                        // Parked - a reversal caught in the throttle deadband, or a ribbon taken
                        // out from under us. NOT the 10 u/s hostile crawl, which is a raid in
                        // progress (see aiStuckSeconds).
                        if (self.Speed >= aiParkedSpeed) movingSince = Time.time;
                        else if (Time.time - movingSince > aiStuckSeconds)
                        {
                            Slip(self);
                            excludedRail = rail;
                            excludedUntil = Time.time + aiSlippedRailCooldown;
                            rail = -1;
                            nextRetarget = 0f;
                            movingSince = Time.time;
                            return centre;
                        }

                        TrySpike(self, ref nextSpike, IsHostileUnderfoot(self, domain));

                        // Keep the nose down-rail: the ride constrains position, never attitude,
                        // so where the AI looks is what it launches along.
                        return railEnd + along * aiThroughDistance;
                    }

                    movingSince = Time.time;

                    // ── RAID (past the rail's far end - airborne, or rolling the burr) ──
                    // Head for the burr this rail aims at and rake it, flying THROUGH the centre
                    // so the pass does not become an orbit. Reached both ways: launched and
                    // still in the air, and attached to the cluster itself.
                    if (r.TargetBurr >= 0 && Vector3.Dot(pos - railEnd, along) > 0f)
                    {
                        Vector3 burr = yard.BurrCentre(r.TargetBurr);
                        float range = Vector3.Distance(pos, burr);
                        if (range < aiThroughDistance * 1.25f)
                        {
                            // Airborne: the cluster ahead is what the volley is for. Rolling it:
                            // ask what is actually underfoot, or a raider on an emptied burr
                            // spends its whole meter on its own mass.
                            TrySpike(self, ref nextSpike,
                                     !self.IsAttached || IsHostileUnderfoot(self, domain));
                            Vector3 through = range > 1e-3f ? (burr - pos) / range : selfTf.forward;
                            return burr + through * aiThroughDistance;
                        }
                        return burr;
                    }

                    // ── APPROACH ────────────────────────────────────────────
                    // Aim at a point short of the near end while far out, then aim THROUGH the
                    // rail once close: arriving along the ribbon is what makes the latch seed its
                    // travel direction toward the far end instead of back the way it came.
                    Vector3 lead = railStart - along * aiRailApproachLead;
                    return (pos - railStart).sqrMagnitude < aiRailCommitDistance * aiRailCommitDistance
                        ? railEnd + along * aiThroughDistance
                        : lead;
                });
            }
        }

        void DisarmRaiders()
        {
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }
        }

        /// <summary>
        /// Score every rail by what it is worth to this pilot and take the best. Three terms, and
        /// each one is a thing a human weighs: how much LOOT is in the burr at the far end, how
        /// far away the rail is, and how much of the rail itself is already this pilot's colour
        /// (a rail you own is a fast run at the burr; a rail you do not is loot in its own right,
        /// so it is a bonus rather than a requirement).
        ///
        /// <para><paramref name="excluded"/> is the rail this pilot just Slipped off, or -1. The
        /// scoring is a pure function of position and domain, so without it the escape hatch
        /// re-picks the rail it just abandoned on the very next frame.</para>
        ///
        /// <para>Burr loot is counted ONCE PER BURR, not once per rail. Two rails launch into
        /// every big burr, and a burr is up to 1,143 prisms - so the naive per-rail walk costs
        /// ~27k prism reads per pilot per refresh to answer 18 questions. The scratch array is a
        /// field rather than a local because this runs on a 3s cadence per AI, forever.</para>
        /// </summary>
        int ChooseRail(HijackYard yard, Vector3 from, Domains domain, int excluded)
        {
            int burrCount = yard.Burrs.Count;
            if (_burrLoot == null || _burrLoot.Length < burrCount) _burrLoot = new int[burrCount];
            for (int i = 0; i < burrCount; i++) _burrLoot[i] = yard.HostileMassAt(i, domain);

            int best = -1;
            float bestScore = 0f;

            for (int i = 0; i < yard.Rails.Count; i++)
            {
                if (i == excluded) continue;

                var r = yard.Rails[i];
                if (r.TargetBurr < 0 || r.TargetBurr >= burrCount) continue;

                // An emptied burr is still a place to be, faintly - so the AI keeps flying the
                // network instead of parking when a cluster runs dry.
                float loot = Mathf.Max(1, _burrLoot[r.TargetBurr]);
                float distance = Vector3.Distance(from, yard.WorldPoint(r.LocalStart));
                float ownFraction = OwnFractionOf(r.Trail, domain);

                float score = loot / (1f + distance / 300f) * (ownFraction + 0.3f);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>Scratch for <see cref="ChooseRail"/>'s per-burr loot census. One array for the
        /// whole controller: the provider closures run on the main thread, one after another.</summary>
        int[] _burrLoot;

        static float OwnFractionOf(Trail trail, Domains domain)
        {
            var list = trail?.TrailList;
            if (list == null || list.Count == 0) return 0f;

            int own = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] && list[i].Domain == domain) own++;
            return own / (float)list.Count;
        }

        static bool IsHostileUnderfoot(IVesselStatus status, Domains domain)
        {
            var prism = status.AttachedPrism;
            return prism && prism.Domain != domain;
        }

        /// <summary>
        /// Tap the chain-spike trigger, if the mass in front is worth spending a volley on and the
        /// meter can pay for it. Press and release in the same call: the Urchin's trigger is
        /// tap-for-shotgun / hold-for-burst, and an AI that held it would charge a burst it never
        /// released.
        /// </summary>
        void TrySpike(IVesselStatus status, ref float nextSpike, bool hostileUnderfoot)
        {
            if (!hostileUnderfoot || Time.time < nextSpike) return;

            var handler = status.ActionHandler;
            if (handler == null) return;
            if (!HasSpikeAmmo(status)) return;

            nextSpike = Time.time + aiSpikeIntervalSeconds;
            handler.PerformShipControllerActionsReplicated(SpikeControl);
            handler.StopShipControllerActionsReplicated(SpikeControl);
        }

        /// <summary>
        /// True while the vessel's spike meter is above the floor. Reads the FIRST resource, which
        /// is the ammo meter <c>GunVesselTransformer.SlideActions</c> recharges - a named index
        /// would be a second place the Urchin's meter order has to be kept in step, and the wrong
        /// one silently reads a different resource rather than failing.
        /// </summary>
        bool HasSpikeAmmo(IVesselStatus status)
        {
            var resources = status.ResourceSystem?.Resources;
            if (resources == null || resources.Count == 0) return true;   // no meter: never gate
            var ammo = resources[0];
            return ammo.MaxAmount <= 0f || ammo.CurrentAmount / ammo.MaxAmount >= aiMinSpikeAmmo;
        }

        static void Slip(IVesselStatus status)
        {
            var handler = status.ActionHandler;
            if (handler == null) return;
            handler.PerformShipControllerActionsReplicated(SlipControl);
            handler.StopShipControllerActionsReplicated(SlipControl);
        }

        // ── Server-authoritative game end (the Rampage/Salvo shape) ──────────

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
                .OrderByDescending(s => s.PrismStolen)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (winnerRep == null) return;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            DisarmRaiders();
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the race just ended - prevents the Ready
        /// button from reappearing (HasEndGame=false routes round end back through here).
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
            var stolenArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                stolenArray[i] = statsList[i].PrismStolen;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, stolenArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsStolen,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Hijack] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.PrismStolen = prismsStolen[i];
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
            DisarmRaiders();

            foreach (var s in gameData.RoundStatsList)
            {
                if (s == null) continue;
                s.PrismStolen = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}

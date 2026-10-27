using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Time - the Serpent-only circuit race, and the Serpent's TIME card: the <b>dirty</b> race (Shombith,
    /// 2026-10-27, <c>Docs/Studios/SERPENT_TIME.md</c>). A closed loop of gates is cut through the cell and every
    /// pilot flies LAPS of it in order; the first domain whose lead runner threads the last gate of the last lap wins,
    /// scored on finish TIME.
    ///
    /// <para><b>What makes it dirty</b> (each piece is a rule of this mode; the vessel is the shipped Serpent):</para>
    /// <list type="bullet">
    /// <item><b>Ring clusters.</b> Every gate stands as 3 rings (I1, I2) or 2 (I3, I4) side by side, any one of which
    /// threads it (<see cref="SerpentTimeCluster"/>). The course's own ring stays as the gate the platform, the
    /// objective arrow and the race toasts read; only its drawing is hidden.</item>
    /// <item><b>Seed Walls cost speed.</b> A Serpent's cloak (RT unscoped) leaves a Seed Wall that grows from the loose
    /// prisms round it. A hull that flies into a RIVAL's wall loses speed: half for a bare brick, most of it for a
    /// shielded one (the Serpent's own prism impact damages the brick and does not slow the hull, so the cost is this
    /// mode's). Your own domain's walls let you through.</item>
    /// <item><b>Sniper rounds cost crystals.</b> A pilot starts with <see cref="startingRounds"/> round, takes one per
    /// crystal up to <see cref="maxRounds"/>, and spends one per shot; a scoped rifle press with no round is dropped.
    /// The crystals sit in the cell, off the racing line, so a round is a detour.</item>
    /// <item><b>The AI plays dirty</b> (input-only, through the replicated press like a human's): it picks the open
    /// ring of each gate, shoots a wall out of its ring when it holds a round, goes for crystals when it is short of
    /// rounds, and cloaks just past a ring a rival still needs, so the wall grows in that rival's way.</item>
    /// </list>
    ///
    /// <para><b>Where each rule runs.</b> Rounds, the press gate and the wall cost run on the machine that FLIES the
    /// hull (the owner: a human's client, the server for an AI) - the same machine whose rifle cooldown is
    /// authoritative (<see cref="SniperShotActionExecutor.Fired"/>) and whose transform the network follows - so
    /// nothing new is replicated. The ring clusters are laid on every peer from the course each peer was sent.</para>
    ///
    /// <para><b>The fuel question stays.</b> The Serpent cruises at 60 u/s; its Time ability is a tank of four Solid
    /// Fuel Pellets (each press burns one for 3 s, burns stack). See <see cref="SerpentTimeCourse"/> and
    /// SERPENTTIME.md.</para>
    /// </summary>
    public class SerpentTimeController : GateRaceController
    {
        [Header("Serpent Time circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = SerpentTimeCourseSource.DefaultLaps;

        [Header("Ring clusters")]
        [Tooltip("Rings standing at each gate on intensity 1-4; any one of them threads the gate. 1 = the plain gate.")]
        [SerializeField] int[] ringsPerGateByIntensity = { 3, 3, 2, 2 };
        [Tooltip("Seconds each cluster ring takes to bloom in when the course is raised.")]
        [SerializeField, Min(0f)] float clusterBloomSeconds = 0.9f;

        [Header("Sniper rounds")]
        [Tooltip("Rounds every pilot starts the race with.")]
        [SerializeField, Min(0)] int startingRounds = 1;
        [Tooltip("The most rounds a pilot can hold; a crystal taken at the cap is wasted.")]
        [SerializeField, Min(1)] int maxRounds = 3;
        [Tooltip("Rounds one crystal gives.")]
        [SerializeField, Min(0)] int roundsPerCrystal = 1;
        [Tooltip("Seconds between two 'no rounds' toasts, so a held trigger does not flood the screen.")]
        [SerializeField, Min(0f)] float noRoundsToastSeconds = 2.5f;

        [Header("Seed Walls")]
        [Tooltip("Throttle multiplier a hull drops to on hitting a rival's bare wall brick (recovers over the slow).")]
        [SerializeField, Range(0f, 1f)] float bareBrickThrottle = 0.5f;
        [Tooltip("Throttle multiplier on hitting a rival's shielded brick or seed.")]
        [SerializeField, Range(0f, 1f)] float shieldedBrickThrottle = 0.15f;
        [Tooltip("Seconds the throttle takes to recover from a wall hit.")]
        [SerializeField, Min(0.05f)] float wallSlowSeconds = 1.2f;
        [Tooltip("Seconds after a wall hit before another can slow the same hull.")]
        [SerializeField, Min(0f)] float wallHitCooldownSeconds = 0.5f;
        [Tooltip("How close (u) a brick's centre must pass to the hull's path to count as a hit.")]
        [SerializeField, Min(0.5f)] float wallContactRadius = 5f;

        [Header("Dirty AI")]
        [Tooltip("Chance an AI cloaks just past a ring a rival still needs (when its cloak is ready).")]
        [SerializeField, Range(0f, 1f)] float aiCloakChance = 0.8f;
        [Tooltip("A rival counts as needing the ring only within this distance (u).")]
        [SerializeField, Min(0f)] float aiCloakRivalRange = 1400f;
        [Tooltip("Seconds between an AI's dirty cloaks (the shipped cloak recovers in 15 s).")]
        [SerializeField, Min(0f)] float aiCloakCooldownSeconds = 15.5f;
        [Tooltip("Share of a ring's mouth rival bricks must cover before the AI calls the ring walled.")]
        [SerializeField, Range(0.05f, 1f)] float aiBlockedCoverage = 0.4f;
        [Tooltip("Extra cost (u) the AI puts on a walled ring while it holds a round (it can shoot it open).")]
        [SerializeField, Min(0f)] float aiBlockedCostWithRounds = 120f;
        [Tooltip("Extra cost (u) the AI puts on a walled ring with no round (it would have to ram it).")]
        [SerializeField, Min(0f)] float aiBlockedCostWithoutRounds = 600f;
        [Tooltip("The AI keeps its ring unless another is cheaper by this much (u).")]
        [SerializeField, Min(0f)] float aiRingSwitchHysteresis = 60f;
        [Tooltip("Within this distance (u) of its ring the AI stops changing its mind, unless the ring is walled and it has no round.")]
        [SerializeField, Min(0f)] float aiChoiceLockDistance = 250f;
        [Tooltip("The AI shoots a wall in its ring only from this range (u): near enough to aim, far enough to clear it in time.")]
        [SerializeField] Vector2 aiShotRange = new(90f, 1100f);
        [Tooltip("Seconds the AI holds the scope before it may fire.")]
        [SerializeField, Min(0f)] float aiScopeSettleSeconds = 0.35f;
        [Tooltip("Seconds the AI tries to line up a shot before it lowers the scope.")]
        [SerializeField, Min(0.1f)] float aiShotGiveUpSeconds = 3f;
        [Tooltip("Extra flying (u) an AI with NO round spends to take a crystal on the way.")]
        [SerializeField, Min(0f)] float aiDetourWithoutRounds = 900f;
        [Tooltip("Extra flying (u) an AI below the round cap spends to take a crystal on the way (0 at the cap).")]
        [SerializeField, Min(0f)] float aiDetourBelowCap = 250f;

        protected override string ModeName => "Serpent Time";

        /// <summary>Regatta's gate steering, which made the AI race Switchback and Warpline: commit from
        /// the approach point and aim through the ring's centre.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new SerpentTimeCourseSource { Laps = laps };

        const float ChoiceSeconds = 0.25f;
        const float PressHoldSeconds = 0.2f;
        const float WallSlabHalfDepth = 12f;

        enum ShotPhase { Idle, Scoping, Fired }

        /// <summary>One pilot's state in this race, on the machine that flies it (and, for an AI, on the server).</summary>
        sealed class Pilot
        {
            public IPlayer Player;
            public bool FliesHere;
            public int Rounds;
            public R_VesselActionHandler Handler;
            public SniperScopeActionExecutor Scope;
            public SniperShotActionExecutor Rifle;
            public AIPilot Autopilot;
            public bool FilterInstalled;

            public Vector3 LastPosition;
            public bool HasLastPosition;
            public float WallCooldownUntil;

            // AI: which ring of the gate it flies for.
            public int ChoiceIndex = -1;
            public int ChoiceSlot = -1;
            public float NextChoiceAt;
            public readonly bool[] SlotBlocked = new bool[MaxRings];
            public readonly Vector3[] SlotCentroid = new Vector3[MaxRings];

            // AI: dirty cloak and wall shots.
            public int LastThreaded = -1;
            public float NextCloakAt;
            public float ReleaseCloakAt = -1f;
            public ShotPhase Shot;
            public float ShotPhaseAt;
            public float NextShotAt;
            public System.Random Rng;
        }

        const int MaxRings = 3;

        readonly Dictionary<IPlayer, Pilot> _pilots = new();
        readonly List<RaceGateRing[]> _clusters = new();
        readonly List<RaceGate[]> _clusterGates = new();
        static readonly List<Prism> s_prisms = new(128);

        bool _localRaceStarted;
        float _nextNoRoundsToastAt;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ElementalCrystalImpactor.OnCrystalCollected += HandleCrystalCollected;
            SniperShotActionExecutor.Fired += HandleShotFired;
        }

        public override void OnNetworkDespawn()
        {
            ElementalCrystalImpactor.OnCrystalCollected -= HandleCrystalCollected;
            SniperShotActionExecutor.Fired -= HandleShotFired;
            ReleasePilots();
            base.OnNetworkDespawn();
            _clusters.Clear();
            _clusterGates.Clear();
            _localRaceStarted = false;
        }

        /// <summary>Hand every vessel back as the race found it: no press gate, no AI filter, no held trigger.</summary>
        void ReleasePilots()
        {
            foreach (var pilot in _pilots.Values)
            {
                if (pilot.Handler && pilot.Handler.PressGate != null && pilot.FliesHere) pilot.Handler.PressGate = null;
                if (pilot.Autopilot) pilot.Autopilot.SetCycledAbilityFilter(null);
                if (pilot.Handler && pilot.FliesHere && pilot.Shot != ShotPhase.Idle)
                {
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
                }
            }
            _pilots.Clear();
        }

        // ── Ring clusters (every peer) ────────────────────────────────────

        protected override void OnCourseRaised()
        {
            _clusters.Clear();
            _clusterGates.Clear();

            int rings = Mathf.Min(MaxRings, SerpentTimeCluster.RingsFor(Intensity, ringsPerGateByIntensity));
            if (rings <= 1) return;

            var theme = gameData ? gameData.ThemeManagerData : null;
            for (int i = 0; i < _rings.Count; i++)
            {
                var courseRing = _rings[i];
                var cluster = new RaceGateRing[rings];
                var gates = new RaceGate[rings];
                for (int slot = 0; slot < rings; slot++)
                {
                    gates[slot] = SerpentTimeCluster.Ring(_course[i], i, slot, rings);
                    // A child of the course's ring, so the ring's retirement takes the cluster with it.
                    var go = new GameObject($"Ring_{slot + 1}");
                    go.transform.SetParent(courseRing ? courseRing.transform : transform, false);
                    var ring = go.AddComponent<RaceGateRing>();
                    ring.Build(i, gates[slot], theme, clusterBloomSeconds);
                    cluster[slot] = ring;
                }
                if (courseRing) courseRing.SetVisualHidden(true);
                _clusters.Add(cluster);
                _clusterGates.Add(gates);
            }
        }

        bool HasCluster(int ringIndex) => ringIndex >= 0 && ringIndex < _clusters.Count;

        protected override bool ThreadsGate(int ringIndex, Vector3 prev, Vector3 cur)
        {
            if (!HasCluster(ringIndex)) return base.ThreadsGate(ringIndex, prev, cur);
            var cluster = _clusters[ringIndex];
            for (int i = 0; i < cluster.Length; i++)
                if (cluster[i] && cluster[i].CrossedMouth(prev, cur)) return true;
            return false;
        }

        protected override void OnLocalNextGateChanged(int previousRing, int nextRing)
        {
            LightCluster(previousRing, false);
            LightCluster(nextRing, true);
        }

        void LightCluster(int ringIndex, bool lit)
        {
            if (!HasCluster(ringIndex)) return;
            var cluster = _clusters[ringIndex];
            for (int i = 0; i < cluster.Length; i++)
                if (cluster[i]) cluster[i].SetIsNextForLocalPilot(lit);
        }

        // ── Pilots, rounds and the press gate (the machine that flies each hull) ──

        /// <summary>The same rule as the rifle's own: not spawned is the only machine there is, else the owner.</summary>
        static bool FliesHere(IPlayer player)
        {
            var status = player?.Vessel?.VesselStatus;
            if (status == null) return false;
            var handler = status.ActionHandler;
            if (handler == null || !handler.IsSpawned) return true;
            return status.IsNetworkOwner;
        }

        Pilot PilotFor(IPlayer player)
        {
            if (player == null) return null;
            if (_pilots.TryGetValue(player, out var pilot))
            {
                if (pilot.Handler == null || !pilot.Handler) RefreshVessel(pilot);
                return pilot;
            }

            pilot = new Pilot
            {
                Player = player,
                FliesHere = FliesHere(player),
                Rounds = startingRounds,
                Rng = new System.Random(unchecked(System.Environment.TickCount * 17 + (int)player.PlayerNetId)),
            };
            _pilots[player] = pilot;
            RefreshVessel(pilot);
            return pilot;
        }

        void RefreshVessel(Pilot pilot)
        {
            var status = pilot.Player.Vessel?.VesselStatus;
            if (status == null) return;
            pilot.Handler = status.ActionHandler;
            var root = status.Transform;
            if (root)
            {
                pilot.Scope = root.GetComponentInChildren<SniperScopeActionExecutor>(true);
                pilot.Rifle = root.GetComponentInChildren<SniperShotActionExecutor>(true);
            }
            // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a human's hull.
            pilot.Autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;

            if (pilot.FliesHere && pilot.Handler)
            {
                var captured = pilot;
                pilot.Handler.PressGate = ie => AllowPress(captured, ie);
            }
        }

        bool IsScoped(Pilot pilot) => pilot.Scope && pilot.Scope.IsScoped;

        /// <summary>RT while scoped is the rifle: it needs a round. Unscoped RT is the cloak, always allowed.</summary>
        bool AllowPress(Pilot pilot, InputEvents ie)
        {
            if (ie != InputEvents.RightStickAction || !IsScoped(pilot) || pilot.Rounds > 0) return true;
            if (pilot.Player.IsLocalUser && Time.time >= _nextNoRoundsToastAt)
            {
                _nextNoRoundsToastAt = Time.time + noRoundsToastSeconds;
                GameToastAPI.Post(GameToastSituation.SerpentTimeNoRounds, pilot.Player.Domain);
            }
            return false;
        }

        void SetRounds(Pilot pilot, int rounds)
        {
            rounds = Mathf.Clamp(rounds, 0, maxRounds);
            if (rounds == pilot.Rounds) return;
            pilot.Rounds = rounds;
            if (pilot.Player.IsLocalUser)
                GameToastAPI.Post(GameToastSituation.SerpentTimeRounds, pilot.Player.Domain,
                                  rounds.ToString(), maxRounds.ToString());
        }

        void HandleCrystalCollected(string playerName)
        {
            if (gameData == null || !gameData.IsTurnRunning) return;
            foreach (var pilot in _pilots.Values)
                if (pilot.FliesHere && pilot.Player.Name == playerName)
                    SetRounds(pilot, pilot.Rounds + roundsPerCrystal);
        }

        void HandleShotFired(IVesselStatus shooter)
        {
            if (shooter == null) return;
            foreach (var pilot in _pilots.Values)
                if (pilot.FliesHere && ReferenceEquals(pilot.Player.Vessel?.VesselStatus, shooter))
                {
                    SetRounds(pilot, pilot.Rounds - 1);
                    return;
                }
        }

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            if (!IsSpawned || gameData == null || !gameData.IsTurnRunning) return;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player?.Vessel == null) continue;
                var pilot = PilotFor(player);
                if (pilot == null || !pilot.FliesHere) continue;
                TickWallCost(pilot);
            }

            if (!_localRaceStarted)
            {
                _localRaceStarted = true;
                var local = gameData.LocalPlayer;
                if (local != null)
                {
                    GameToastAPI.Post(GameToastSituation.SerpentTimeWallHint, local.Domain);
                    GameToastAPI.Post(GameToastSituation.SerpentTimeRounds, local.Domain,
                                      startingRounds.ToString(), maxRounds.ToString());
                }
            }
        }

        /// <summary>
        /// The wall's cost: did this hull's path this frame (and a short lead ahead of its nose, so a fast hull cannot
        /// step over a brick between frames) pass a rival's Seed Wall brick? Then it loses speed.
        /// </summary>
        void TickWallCost(Pilot pilot)
        {
            var vessel = pilot.Player.Vessel;
            var status = vessel.VesselStatus;
            var tf = vessel.Transform;
            if (status == null || !tf) return;

            Vector3 cur = tf.position;
            Vector3 prev = pilot.HasLastPosition ? pilot.LastPosition : cur;
            pilot.LastPosition = cur;
            pilot.HasLastPosition = true;

            if (Time.time < pilot.WallCooldownUntil) return;
            if ((cur - prev).sqrMagnitude > 400f * 400f) return;   // a teleport, not a flight

            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;

            float lead = Mathf.Max(status.Speed * Time.deltaTime * 2f, 6f);
            index.QuerySegment(prev, cur + tf.forward * lead, wallContactRadius, s_prisms);

            bool hit = false, shielded = false;
            for (int i = 0; i < s_prisms.Count; i++)
            {
                var prism = s_prisms[i];
                if (!prism || prism.destroyed || prism.Domain == pilot.Player.Domain) continue;
                if (!SerpentWallAssembler.IsWallBrick(prism)) continue;
                hit = true;
                if (prism.prismProperties is { IsShielded: true } or { IsSuperShielded: true }) { shielded = true; break; }
            }
            if (!hit) return;

            pilot.WallCooldownUntil = Time.time + wallHitCooldownSeconds;
            status.VesselTransformer?.ModifyThrottle(shielded ? shieldedBrickThrottle : bareBrickThrottle, wallSlowSeconds);
            if (pilot.Player.IsLocalUser)
                GameToastAPI.Post(GameToastSituation.SerpentTimeWallHit, pilot.Player.Domain);
        }

        // ── The dirty AI (server) ─────────────────────────────────────────

        protected override float AICrystalDetourSlackFor(IPlayer player)
        {
            var pilot = PilotFor(player);
            if (pilot == null) return 0f;
            if (pilot.Rounds <= 0) return aiDetourWithoutRounds;
            return pilot.Rounds < maxRounds ? aiDetourBelowCap : 0f;
        }

        /// <summary>
        /// The ring of this gate the AI flies for: the cheapest by distance (here, through it, on to the next gate) plus
        /// a cost for a walled ring - small while it holds a round (it shoots it open), large without one (it would ram).
        /// Re-decided a few times a second, with hysteresis, and held once the pilot is close.
        /// </summary>
        protected override RaceGate ChooseAIGate(IPlayer player, int threaded, int ringIndex, RaceGate gate)
        {
            if (!HasCluster(ringIndex)) return gate;
            var pilot = PilotFor(player);
            var tf = player.Vessel?.Transform;
            if (pilot == null || !tf) return _clusterGates[ringIndex][0];

            var gates = _clusterGates[ringIndex];
            bool fresh = pilot.ChoiceIndex != threaded || pilot.ChoiceSlot < 0 || pilot.ChoiceSlot >= gates.Length;
            if (fresh || Time.time >= pilot.NextChoiceAt)
                ChooseRing(pilot, tf.position, threaded, gates, fresh);
            return gates[pilot.ChoiceSlot];
        }

        void ChooseRing(Pilot pilot, Vector3 self, int threaded, RaceGate[] gates, bool fresh)
        {
            pilot.NextChoiceAt = Time.time + ChoiceSeconds;
            Vector3 next = threaded + 1 < RaceLength ? _course[RingIndexFor(threaded + 1)].Position : gates[0].Position;
            float blockedCost = pilot.Rounds > 0 ? aiBlockedCostWithRounds : aiBlockedCostWithoutRounds;

            int best = 0;
            float bestCost = float.MaxValue, keptCost = float.MaxValue;
            for (int slot = 0; slot < gates.Length; slot++)
            {
                pilot.SlotBlocked[slot] = IsWalled(gates[slot], pilot.Player.Domain, out pilot.SlotCentroid[slot]);
                float cost = Vector3.Distance(self, gates[slot].Position) + Vector3.Distance(gates[slot].Position, next)
                             + (pilot.SlotBlocked[slot] ? blockedCost : 0f);
                if (cost < bestCost) { bestCost = cost; best = slot; }
                if (!fresh && slot == pilot.ChoiceSlot) keptCost = cost;
            }

            if (!fresh)
            {
                var kept = gates[pilot.ChoiceSlot];
                bool stuck = pilot.SlotBlocked[pilot.ChoiceSlot] && pilot.Rounds <= 0;
                bool close = (kept.Position - self).sqrMagnitude < aiChoiceLockDistance * aiChoiceLockDistance;
                if ((close && !stuck) || keptCost <= bestCost + aiRingSwitchHysteresis) return;
            }

            pilot.ChoiceIndex = threaded;
            pilot.ChoiceSlot = best;
        }

        /// <summary>
        /// Do rival Seed Wall bricks cover enough of this ring's mouth to call it walled? Measured as the bricks' face
        /// area against the mouth's, counting only bricks in a slab round the ring's plane and inside its mouth.
        /// </summary>
        bool IsWalled(in RaceGate ring, Domains domain, out Vector3 centroid)
        {
            centroid = ring.Position;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return false;

            index.QuerySphere(ring.Position, ring.Radius + WallSlabHalfDepth, s_prisms);
            float area = 0f;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < s_prisms.Count; i++)
            {
                var prism = s_prisms[i];
                if (!prism || prism.destroyed || prism.Domain == domain) continue;
                if (!SerpentWallAssembler.IsWallBrick(prism)) continue;

                Vector3 offset = prism.transform.position - ring.Position;
                float along = Vector3.Dot(offset, ring.Axis);
                if (Mathf.Abs(along) > WallSlabHalfDepth) continue;
                if ((offset - ring.Axis * along).sqrMagnitude > ring.Radius * ring.Radius) continue;

                // A brick's broad face: the product of its two largest extents (a wall stands its bricks face-on).
                Vector3 s = prism.transform.lossyScale;
                float a = Mathf.Abs(s.x), b = Mathf.Abs(s.y), c = Mathf.Abs(s.z);
                area += a * b * c / Mathf.Max(1e-3f, Mathf.Min(a, Mathf.Min(b, c)));
                sum += prism.transform.position;
                count++;
            }
            if (count == 0) return false;
            centroid = sum / count;
            return area / (Mathf.PI * ring.Radius * ring.Radius) >= aiBlockedCoverage;
        }

        protected override void OnServerTick()
        {
            if (gameData == null || !gameData.IsTurnRunning || _rings.Count == 0) return;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player?.Vessel == null) continue;
                var pilot = PilotFor(player);
                if (pilot == null || !pilot.FliesHere || !pilot.Handler) continue;
                if (!pilot.Autopilot || !pilot.Autopilot.AutoPilotEnabled) continue;

                if (!pilot.FilterInstalled)
                {
                    // The cycler pressed the cloak every few seconds; here the AI cloaks when it hurts a rival.
                    pilot.Autopilot.SetCycledAbilityFilter(a => a is not CloakSeedWallActionSO);
                    pilot.FilterInstalled = true;
                }

                TickDirtyCloak(pilot);
                TickWallShot(pilot);
            }
        }

        /// <summary>Just past a ring a rival still needs (and close), cloak: the seed lands at the ring and the wall grows there.</summary>
        void TickDirtyCloak(Pilot pilot)
        {
            if (pilot.ReleaseCloakAt >= 0f && Time.time >= pilot.ReleaseCloakAt)
            {
                pilot.ReleaseCloakAt = -1f;
                pilot.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
            }

            int threaded = pilot.Player.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded == pilot.LastThreaded) return;
            bool justThreaded = pilot.LastThreaded >= 0 && threaded > pilot.LastThreaded;
            pilot.LastThreaded = threaded;
            if (!justThreaded || threaded <= 0) return;

            if (Time.time < pilot.NextCloakAt || pilot.Shot != ShotPhase.Idle || IsScoped(pilot)) return;
            if (!RivalNeeds(pilot, RingIndexFor(threaded - 1))) return;

            pilot.NextCloakAt = Time.time + aiCloakCooldownSeconds;
            if (pilot.Rng.NextDouble() >= aiCloakChance) return;

            pilot.Handler.PerformShipControllerActionsReplicated(InputEvents.RightStickAction);
            pilot.ReleaseCloakAt = Time.time + PressHoldSeconds;
        }

        bool RivalNeeds(Pilot pilot, int ringIndex)
        {
            var self = pilot.Player.Vessel?.Transform;
            if (!self) return false;
            float range2 = aiCloakRivalRange * aiCloakRivalRange;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var rival = players[i];
                if (rival == null || rival == pilot.Player || rival.Domain == pilot.Player.Domain) continue;
                var tf = rival.Vessel?.Transform;
                if (!tf || rival.RoundStats == null) continue;
                int next = rival.RoundStats.SwitchesThreaded;
                if (next < 0 || next >= RaceLength || RingIndexFor(next) != ringIndex) continue;
                if ((tf.position - self.position).sqrMagnitude <= range2) return true;
            }
            return false;
        }

        /// <summary>
        /// The AI's ring is walled and it holds a round: raise the scope (LT), and once it has settled and the nose is on
        /// the wall, fire (RT), then lower both. Every press goes the replicated way, so every peer sees the shot.
        /// </summary>
        void TickWallShot(Pilot pilot)
        {
            var tf = pilot.Player.Vessel?.Transform;
            if (!tf) return;
            float now = Time.time;
            int threaded = pilot.Player.RoundStats?.SwitchesThreaded ?? 0;
            int ringIndex = threaded < RaceLength ? RingIndexFor(threaded) : -1;
            bool aimed = HasCluster(ringIndex) && pilot.ChoiceIndex == threaded && pilot.ChoiceSlot >= 0;
            bool walled = aimed && pilot.SlotBlocked[pilot.ChoiceSlot];
            Vector3 target = aimed ? pilot.SlotCentroid[pilot.ChoiceSlot] : tf.position;
            float distance = Vector3.Distance(tf.position, target);

            switch (pilot.Shot)
            {
                case ShotPhase.Idle:
                    if (!walled || pilot.Rounds <= 0 || now < pilot.NextShotAt || pilot.ReleaseCloakAt >= 0f) return;
                    if (distance < aiShotRange.x || distance > aiShotRange.y) return;
                    if (pilot.Rifle && pilot.Rifle.CooldownRemaining > 0f) return;
                    pilot.Handler.PerformShipControllerActionsReplicated(InputEvents.LeftStickAction);
                    pilot.Shot = ShotPhase.Scoping;
                    pilot.ShotPhaseAt = now;
                    return;

                case ShotPhase.Scoping:
                    if (!walled || pilot.Rounds <= 0 || distance < aiShotRange.x || now - pilot.ShotPhaseAt > aiShotGiveUpSeconds)
                    {
                        pilot.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
                        pilot.Shot = ShotPhase.Idle;
                        pilot.NextShotAt = now + 1f;
                        return;
                    }
                    if (!IsScoped(pilot) || now - pilot.ShotPhaseAt < aiScopeSettleSeconds) return;
                    if (!OnTarget(pilot, tf, target, distance)) return;
                    pilot.Handler.PerformShipControllerActionsReplicated(InputEvents.RightStickAction);
                    pilot.Shot = ShotPhase.Fired;
                    pilot.ShotPhaseAt = now;
                    return;

                case ShotPhase.Fired:
                    if (now - pilot.ShotPhaseAt < PressHoldSeconds) return;
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
                    pilot.Shot = ShotPhase.Idle;
                    pilot.NextShotAt = now + 0.5f;
                    return;
            }
        }

        /// <summary>Is the wall's centre inside the rifle's cone (its radius floored at 6 u, as the rifle's own path is)?</summary>
        static bool OnTarget(Pilot pilot, Transform tf, Vector3 target, float distance)
        {
            float half = pilot.Rifle ? pilot.Rifle.ConeHalfAngleDegrees : 1.5f;
            Vector3 to = target - tf.position;
            float along = Vector3.Dot(to, tf.forward);
            if (along <= 0f) return false;
            float lateral = (to - tf.forward * along).magnitude;
            return lateral <= Mathf.Max(6f, along * Mathf.Tan(half * Mathf.Deg2Rad));
        }
    }
}

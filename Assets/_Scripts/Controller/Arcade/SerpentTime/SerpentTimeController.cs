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
    /// <item><b>Gate debris.</b> Loose neutral prisms lie round every gate and grow back, so a Serpent that cloaks
    /// there has the mass its Seed Wall grows from. The debris is the environment's; the wall is the shipped one.</item>
    /// <item><b>The AI plays dirty</b> (input-only, through the replicated press like a human's): it picks the open
    /// ring of each gate, shoots a wall out of its ring when its rifle is ready, and cloaks just past a ring a rival
    /// still needs, so the wall grows in that rival's way.</item>
    /// </list>
    ///
    /// <para><b>The vessel is never changed here</b> (Shombith, 2026-10-31: "the vessel kit as designed always stays
    /// the same, we just make the game mode interesting"). No press is gated, no ability is rationed, no contact effect
    /// is added: the Sniper Shot, the Seed Wall, the cloak and the pellets do exactly what they do in every other mode.
    /// The mode owns the track (the clusters, the debris) and its AI's choices. The AI runs on the server; the ring
    /// clusters are laid on every peer from the course each peer was sent.</para>
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

        [Header("Gate debris (the walls' fuel)")]
        [Tooltip("The prism every piece of gate debris is laid from: the environment prism (SpawnablePrism). " +
                 "Authored by Tools/Build/author_serpent_time_assets.py. Unset = no debris, and a warning.")]
        [SerializeField] Prism debrisPrefab;
        [Tooltip("Loose prisms kept round each gate. A Seed Wall pulls its bricks from loose mass near its seed, so " +
                 "this is what a wall laid at a gate is built from (with the trails flown through it).")]
        [SerializeField, Min(0)] int debrisPerGate = 48;
        [Tooltip("Every this many seconds, each gate short of its debris grows one piece back, so a gate is never " +
                 "stripped bare for the rest of the race.")]
        [SerializeField, Min(0.1f)] float debrisRegrowSeconds = 1.5f;
        [Tooltip("How far beyond the gate's outermost ring (u) the debris lies: nearest and farthest.")]
        [SerializeField] Vector2 debrisDistanceBeyondRings = new(26f, 120f);
        [Tooltip("Size of one piece of debris (u).")]
        [SerializeField] Vector3 debrisScale = new(2.6f, 2.6f, 2.6f);

        [Header("Dirty AI")]
        [Tooltip("Chance an AI cloaks just past a ring a rival still needs (when its cloak is ready).")]
        [SerializeField, Range(0f, 1f)] float aiCloakChance = 0.8f;
        [Tooltip("A rival counts as needing the ring only within this distance (u).")]
        [SerializeField, Min(0f)] float aiCloakRivalRange = 1400f;
        [Tooltip("Seconds between an AI's dirty cloaks (the shipped cloak recovers in 15 s).")]
        [SerializeField, Min(0f)] float aiCloakCooldownSeconds = 15.5f;
        [Tooltip("Share of a ring's mouth rival prisms must cover before the AI calls the ring walled.")]
        [SerializeField, Range(0.05f, 1f)] float aiBlockedCoverage = 0.4f;
        [Tooltip("Extra cost (u) the AI puts on a walled ring (it shoots it open when its rifle is ready).")]
        [SerializeField, Min(0f)] float aiBlockedCost = 120f;
        [Tooltip("The AI keeps its ring unless another is cheaper by this much (u).")]
        [SerializeField, Min(0f)] float aiRingSwitchHysteresis = 60f;
        [Tooltip("Within this distance (u) of its ring the AI stops changing its mind, unless the ring is walled and its rifle is not ready.")]
        [SerializeField, Min(0f)] float aiChoiceLockDistance = 250f;
        [Tooltip("The AI shoots a wall in its ring only from this range (u): near enough to aim, far enough to clear it in time.")]
        [SerializeField] Vector2 aiShotRange = new(90f, 1100f);
        [Tooltip("Seconds the AI holds the scope before it may fire.")]
        [SerializeField, Min(0f)] float aiScopeSettleSeconds = 0.35f;
        [Tooltip("Seconds the AI tries to line up a shot before it lowers the scope.")]
        [SerializeField, Min(0.1f)] float aiShotGiveUpSeconds = 3f;

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
            public R_VesselActionHandler Handler;
            public SniperScopeActionExecutor Scope;
            public SniperShotActionExecutor Rifle;
            public AIPilot Autopilot;
            public bool FilterInstalled;

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

        /// <summary>Owner id no pilot carries: the debris is nobody's trail, and only pieces still carrying it count.</summary>
        const string DebrisOwnerId = "SerpentTimeDebris";

        /// <summary>How far (u) a gate's debris keeps from another gate's outermost ring.</summary>
        const float DebrisClearOfOtherGates = 20f;

        /// <summary>One gate's debris: where it lies, its own seeded draw, and the pieces laid so far.</summary>
        sealed class GateDebris
        {
            public Vector3 Centre;
            public float Nearest, Farthest;
            public System.Random Rng;
            public Trail Trail;
            public readonly List<Prism> Pieces = new();
        }

        readonly List<GateDebris> _debris = new();
        Transform _debrisRoot;
        float _nextDebrisRegrowAt;

        readonly Dictionary<IPlayer, Pilot> _pilots = new();
        readonly List<RaceGateRing[]> _clusters = new();
        readonly List<RaceGate[]> _clusterGates = new();
        static readonly List<Prism> s_prisms = new(128);

        bool _localRaceStarted;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            ReleasePilots();
            ClearDebris();
            base.OnNetworkDespawn();
            _clusters.Clear();
            _clusterGates.Clear();
            _localRaceStarted = false;
        }

        /// <summary>Hand every vessel back as the race found it: no AI filter, no held trigger.</summary>
        void ReleasePilots()
        {
            foreach (var pilot in _pilots.Values)
            {
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
            LayGateDebris(rings);
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

        // ── Gate debris (every peer) ──────────────────────────────────────

        /// <summary>
        /// Lay <see cref="debrisPerGate"/> loose prisms round every gate, beyond its outermost ring, in random directions
        /// and poses: the mass a Seed Wall laid at the gate is built from (the studio's rule, <c>serpent.html</c>
        /// <c>addDebris</c>). Every peer lays its own copy from the course it was sent - a wall is assembled on every
        /// peer out of that peer's prisms - so each gate's draw is seeded from the gate itself and the copies match.
        /// Laid behind the connecting panel, so the arena-ready gate waits for them.
        /// </summary>
        void LayGateDebris(int rings)
        {
            ClearDebris();
            if (debrisPerGate <= 0) return;
            if (!debrisPrefab)
            {
                CSDebug.LogWarning("[Serpent Time] No debris prefab on the controller: walls grow only from trails. " +
                                   "Re-run Tools/Build/author_serpent_time_assets.py.");
                return;
            }

            // At the scene root, at world identity: a laid prism's position is local to its parent, and the course is
            // already in world coordinates.
            _debrisRoot = new GameObject("SerpentTimeDebris").transform;

            SerpentTimeCluster.Layout(rings, out float ringScale, out float spread);
            float reachInMouths = rings > 1 ? spread + ringScale : 1f;
            for (int i = 0; i < _course.Count; i++)
            {
                var gate = _course[i];
                float reach = gate.Radius * reachInMouths;
                // Never into another gate's rings: on a tight course two gates can sit closer than two debris shells.
                float farthest = reach + Mathf.Max(debrisDistanceBeyondRings.x, debrisDistanceBeyondRings.y);
                for (int j = 0; j < _course.Count; j++)
                    if (j != i)
                        farthest = Mathf.Min(farthest, Vector3.Distance(gate.Position, _course[j].Position)
                                                       - _course[j].Radius * reachInMouths - DebrisClearOfOtherGates);
                farthest = Mathf.Max(farthest, reach + 1f);
                var debris = new GateDebris
                {
                    Centre = gate.Position,
                    Nearest = Mathf.Min(reach + Mathf.Min(debrisDistanceBeyondRings.x, debrisDistanceBeyondRings.y), farthest),
                    Farthest = farthest,
                    Rng = new System.Random(DebrisSeed(i, gate.Position)),
                    Trail = new Trail(),
                };
                _debris.Add(debris);
                for (int n = 0; n < debrisPerGate; n++) LayDebris(debris, forReveal: true);
            }
            _nextDebrisRegrowAt = Time.time + debrisRegrowSeconds;
        }

        /// <summary>The same number on every peer: the gate's index and its position (the course is broadcast).</summary>
        static int DebrisSeed(int gateIndex, Vector3 position) => unchecked(
            gateIndex * 7919 ^ Mathf.RoundToInt(position.x) * 73856093 ^
            Mathf.RoundToInt(position.y) * 19349663 ^ Mathf.RoundToInt(position.z) * 83492791);

        void LayDebris(GateDebris gate, bool forReveal)
        {
            var r = gate.Rng;
            Vector3 direction;
            do direction = new Vector3(Signed(r), Signed(r), Signed(r));
            while (direction.sqrMagnitude > 1f || direction.sqrMagnitude < 0.05f);
            float distance = Mathf.Lerp(gate.Nearest, gate.Farthest, (float)r.NextDouble());
            var rotation = Quaternion.Euler((float)r.NextDouble() * 360f, (float)r.NextDouble() * 360f,
                                            (float)r.NextDouble() * 360f);

            // A regrown piece comes mid-race, so it must not hold the arena-ready gate (a continuous source can never
            // all-clear it) nor queue behind the cell's flora for its creation.
            var prism = PrismTrailBuilder.LayOne(debrisPrefab,
                new PrismLay(new SpawnPoint(gate.Centre + direction.normalized * distance, rotation, debrisScale), Domains.Blue),
                _debrisRoot, gate.Trail, DebrisOwnerId, watchForReveal: forReveal);
            if (!prism) return;
            if (!forReveal) prism.CompletesAsLiveRibbon = true;
            gate.Pieces.Add(prism);
        }

        static float Signed(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        /// <summary>Still loose debris: alive, still the debris's, not stolen into a wall (a steal re-colours it).</summary>
        static bool IsLooseDebris(Prism prism) =>
            prism && !prism.destroyed && prism.ownerID == DebrisOwnerId && prism.Domain == Domains.Blue;

        /// <summary>
        /// The debris grows back: every <see cref="debrisRegrowSeconds"/>, each gate holding fewer than
        /// <see cref="debrisPerGate"/> loose pieces grows one (the studio's rule). A piece a wall took, a hull broke or a
        /// shot cleared no longer counts.
        /// </summary>
        void TickDebrisRegrow()
        {
            if (_debris.Count == 0 || !debrisPrefab || Time.time < _nextDebrisRegrowAt) return;
            _nextDebrisRegrowAt = Time.time + debrisRegrowSeconds;

            for (int g = 0; g < _debris.Count; g++)
            {
                var gate = _debris[g];
                int loose = 0;
                for (int i = gate.Pieces.Count - 1; i >= 0; i--)
                {
                    var piece = gate.Pieces[i];
                    if (IsLooseDebris(piece)) loose++;
                    // Gone, or handed to someone else (the pool re-issues a destroyed prism): stop tracking it.
                    else if (!piece || piece.destroyed || piece.ownerID != DebrisOwnerId) gate.Pieces.RemoveAt(i);
                }
                if (loose < debrisPerGate) LayDebris(gate, forReveal: false);
            }
        }

        void ClearDebris()
        {
            for (int g = 0; g < _debris.Count; g++)
            {
                var pieces = _debris[g].Pieces;
                for (int i = 0; i < pieces.Count; i++)
                    if (IsLooseDebris(pieces[i]) && !EnvironmentPrismPool.TryRelease(pieces[i]))
                        Destroy(pieces[i].gameObject);
            }
            _debris.Clear();
            // A piece a wall still holds stays parented here; the scene's teardown takes it.
            if (_debrisRoot && _debrisRoot.childCount == 0) Destroy(_debrisRoot.gameObject);
            _debrisRoot = null;
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

        // ── Pilots (the machine that flies each hull) ──

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

        }

        bool IsScoped(Pilot pilot) => pilot.Scope && pilot.Scope.IsScoped;

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            if (!IsSpawned || gameData == null || !gameData.IsTurnRunning) return;

            TickDebrisRegrow();

            if (!_localRaceStarted)
            {
                _localRaceStarted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.SerpentTimeWallHint, local.Domain);
            }
        }

        // ── The dirty AI (server) ─────────────────────────────────────────

        /// <summary>
        /// The ring of this gate the AI flies for: the cheapest by distance (here, through it, on to the next gate) plus
        /// a cost for a walled ring (it shoots it open when its rifle is ready).
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

            int best = 0;
            float bestCost = float.MaxValue, keptCost = float.MaxValue;
            for (int slot = 0; slot < gates.Length; slot++)
            {
                pilot.SlotBlocked[slot] = IsWalled(gates[slot], pilot.Player.Domain, out pilot.SlotCentroid[slot]);
                float cost = Vector3.Distance(self, gates[slot].Position) + Vector3.Distance(gates[slot].Position, next)
                             + (pilot.SlotBlocked[slot] ? aiBlockedCost : 0f);
                if (cost < bestCost) { bestCost = cost; best = slot; }
                if (!fresh && slot == pilot.ChoiceSlot) keptCost = cost;
            }

            if (!fresh)
            {
                var kept = gates[pilot.ChoiceSlot];
                bool stuck = pilot.SlotBlocked[pilot.ChoiceSlot] && !RifleReady(pilot);
                bool close = (kept.Position - self).sqrMagnitude < aiChoiceLockDistance * aiChoiceLockDistance;
                if ((close && !stuck) || keptCost <= bestCost + aiRingSwitchHysteresis) return;
            }

            pilot.ChoiceIndex = threaded;
            pilot.ChoiceSlot = best;
        }

        /// <summary>
        /// Does rival mass cover enough of this ring's mouth to call it walled? Measured as the prisms' face area against
        /// the mouth's, counting only prisms in a slab round the ring's plane and inside its mouth. Any rival prism counts
        /// (a Seed Wall's bricks are the usual ones); the question is asked of the prisms, never of the vessel's code.
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
                if (!prism || prism.destroyed || prism.Domain == domain || prism.Domain == Domains.Blue) continue;

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
        /// The AI's ring is walled and its rifle is ready: raise the scope (LT), and once it has settled and the nose is on
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
                    if (!walled || now < pilot.NextShotAt || pilot.ReleaseCloakAt >= 0f) return;
                    if (distance < aiShotRange.x || distance > aiShotRange.y) return;
                    if (!RifleReady(pilot)) return;
                    pilot.Handler.PerformShipControllerActionsReplicated(InputEvents.LeftStickAction);
                    pilot.Shot = ShotPhase.Scoping;
                    pilot.ShotPhaseAt = now;
                    return;

                case ShotPhase.Scoping:
                    if (!walled || distance < aiShotRange.x || now - pilot.ShotPhaseAt > aiShotGiveUpSeconds)
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

        /// <summary>The shipped rifle's own cooldown is the only limit on a shot.</summary>
        static bool RifleReady(Pilot pilot) => !pilot.Rifle || pilot.Rifle.CooldownRemaining <= 0f;

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

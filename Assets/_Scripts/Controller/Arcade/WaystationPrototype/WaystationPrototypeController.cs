using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// WAYSTATION PROTOTYPE - the Tiger Moth's Time race rebuilt around SHORT HOPS, beside Waystation, which stays as
    /// Garrett made it until he approves a replacement (Shombith, 2026-10-31; <c>Docs/Studios/TIGER_MOTH_TIME.md</c>).
    ///
    /// <para><b>Everything of Waystation's is kept by inheritance, not copied.</b> The clusters, the square exit gates
    /// and the belly-aim autopilot are <see cref="WaystationController"/>'s and <see cref="WaystationCourse"/>'s,
    /// unedited. This class adds three things: the Drop Tower course (<see cref="WaystationPrototypeCourseSource"/>), the
    /// tower's WALLS (floor plates and pipes of environment prisms, plain and danger), and an autopilot for the tower's
    /// short hops. The prototype scene raises Waystation's long-drop threshold (<c>aiFoldMinDistance</c>, 700) above
    /// every tower link, so Garrett's autopilot only takes the long final drop and the clusters, and this one takes the
    /// hops.</para>
    ///
    /// <para><b>The vessel is not touched (LOCKED, Shombith 2026-10-31).</b> A wall is an ordinary environment prism: a
    /// hull that lands in or flies into one gets what any hull gets from that kind of prism, and the Drop's fall is
    /// incorporeal, so a plate costs nothing to drop through. The mode adds only track and environment.</para>
    /// </summary>
    public class WaystationPrototypeController : WaystationController
    {
        protected override string ModeName => "Waystation Prototype";

        protected override RaceCourseSource CreateCourseSource() => new WaystationPrototypeCourseSource();

        [Header("Drop Tower walls")]
        [Tooltip("The prism every wall tile is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_waystation_prototype_assets.py. Unset = no walls, and a warning.")]
        [SerializeField] Prism wallPrefab;

        [Tooltip("Every this many seconds, each plate grows ONE missing tile back, so a hole a pilot punched closes " +
                 "before the next pilot arrives.")]
        [SerializeField, Min(0.1f)] float wallRegrowSeconds = 1.5f;

        [Header("Autopilot hops (the tower)")]
        [Tooltip("How closely the BELLY must be on a tower ring's drop line before the autopilot presses.")]
        [SerializeField, Range(1f, 30f)] float aiHopBellyDegrees = 8f;

        [Tooltip("How far off the ring's centre (as a share of its radius) the drop line may pass when the autopilot " +
                 "presses. The pilot cruises over the ring, so this is the press's timing window.")]
        [SerializeField, Range(0.1f, 0.9f)] float aiHopLineShare = 0.45f;

        [Tooltip("How far ahead of the press point the autopilot steers along the line it arrives on (u): it flies the " +
                 "cruise line over the ring rather than at the ring, which sits under a plate.")]
        [SerializeField, Min(10f)] float aiHopLookahead = 150f;

        const string WallOwnerId = "WaystationTowerWall";

        sealed class WallPlate
        {
            public readonly List<WaystationTowerTile> Tiles = new();
            public Prism[] Pieces;
            public readonly Trail Trail = new();
            public int NextTile;
        }

        readonly List<WallPlate> _plates = new();
        static readonly List<WaystationTowerTile> s_tiles = new(2048);
        Transform _wallRoot;
        float _nextRegrowAt;
        bool _warnedNoPrefab;

        readonly Dictionary<IPlayer, float> _hopReleaseAt = new();
        readonly HashSet<IPlayer> _hopRolls = new();
        readonly Dictionary<Transform, TigerMothDropExecutor> _hopExecutors = new();

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void OnNetworkDespawn()
        {
            ReleaseEveryHop();
            ClearWalls();
            base.OnNetworkDespawn();
        }

        // ── Walls (every peer) ────────────────────────────────────────────

        /// <summary>Lay the tower walls from the course this peer was sent, behind the connecting panel.</summary>
        protected override void OnCourseRaised()
        {
            base.OnCourseRaised();
            ClearWalls();
            if (!wallPrefab)
            {
                if (!_warnedNoPrefab)
                {
                    _warnedNoPrefab = true;
                    CSDebug.LogWarning($"[{ModeName}] wallPrefab is unset - the towers stand with no walls. Re-run " +
                                       "Tools/Build/author_waystation_prototype_assets.py.");
                }
                return;
            }

            WaystationTower.Walls(_course, Intensity, s_tiles);
            if (s_tiles.Count == 0) return;
            _wallRoot = new GameObject($"{ModeName}Walls").transform;
            _wallRoot.SetParent(transform, false);   // the rings' frame: the course is laid in the controller's space

            WallPlate plate = null;
            for (int i = 0; i < s_tiles.Count; i++)
            {
                var tile = s_tiles[i];
                if (plate == null || plate.Tiles[0].Gate != tile.Gate || plate.Tiles[0].Wall != tile.Wall)
                {
                    plate = new WallPlate();
                    _plates.Add(plate);
                }
                plate.Tiles.Add(tile);
            }
            foreach (var p in _plates)
            {
                p.Pieces = new Prism[p.Tiles.Count];
                for (int t = 0; t < p.Tiles.Count; t++) LayTile(p, t, forReveal: true);
            }
            _nextRegrowAt = Time.time + wallRegrowSeconds;
        }

        void LayTile(WallPlate plate, int index, bool forReveal)
        {
            var tile = plate.Tiles[index];
            var prism = PrismTrailBuilder.LayOne(wallPrefab,
                new PrismLay(new SpawnPoint(tile.Position, tile.Rotation, tile.Scale), Domains.Blue,
                             tile.Danger ? PrismKind.Danger : PrismKind.Plain),
                _wallRoot, plate.Trail, WallOwnerId, watchForReveal: forReveal);
            if (!prism) return;
            if (!forReveal) prism.CompletesAsLiveRibbon = true;   // a regrown tile must not hold the arena-ready gate
            plate.Pieces[index] = prism;
        }

        static bool IsStanding(Prism prism) =>
            prism && !prism.destroyed && prism.ownerID == WallOwnerId && prism.Domain == Domains.Blue;

        /// <summary>Each plate grows one missing tile back per <see cref="wallRegrowSeconds"/>, walking its tiles.</summary>
        void TickWallRegrow()
        {
            if (_plates.Count == 0 || !wallPrefab || Time.time < _nextRegrowAt) return;
            _nextRegrowAt = Time.time + wallRegrowSeconds;
            foreach (var plate in _plates)
            {
                for (int step = 0; step < plate.Pieces.Length; step++)
                {
                    int t = (plate.NextTile + step) % plate.Pieces.Length;
                    if (IsStanding(plate.Pieces[t])) continue;
                    LayTile(plate, t, forReveal: false);
                    plate.NextTile = t + 1;
                    break;
                }
            }
        }

        void ClearWalls()
        {
            foreach (var plate in _plates)
            {
                if (plate.Pieces == null) continue;
                foreach (var piece in plate.Pieces)
                    if (IsStanding(piece) && !EnvironmentPrismPool.TryRelease(piece))
                        Destroy(piece.gameObject);
            }
            _plates.Clear();
            if (_wallRoot && _wallRoot.childCount == 0) Destroy(_wallRoot.gameObject);
            _wallRoot = null;
        }

        // GateRaceController.Update runs the race and a subclass must not declare Update; LateUpdate is free.
        void LateUpdate()
        {
            if (!IsSpawned || gameData == null || !gameData.IsTurnRunning) return;
            TickWallRegrow();
        }

        // ── The hop autopilot (server) ────────────────────────────────────

        /// <summary>The pilot's next gate when it is a tower ring, in world space; false otherwise.</summary>
        bool TryNextTowerRing(IPlayer pilot, out int index, out Vector3 ring, out Vector3 drop, out Vector3 press,
                              out Vector3 cruise)
        {
            index = -1;
            ring = drop = press = cruise = default;
            if (pilot?.RoundStats == null || gameData == null) return false;
            index = pilot.RoundStats.SwitchesThreaded;
            if (index < 1 || index >= _course.Count) return false;
            var s = WaystationTowerSettings.ForIntensity(Intensity);
            if (!WaystationTower.IsTowerRing(_course[index], s)) return false;

            ring = transform.TransformPoint(_course[index].Position);
            drop = transform.TransformDirection(_course[index].Axis);
            press = transform.TransformPoint(WaystationTower.PressOf(_course, index, s));
            Vector3 arrive = transform.TransformDirection(_course[index - 1].Axis);
            cruise = RaceCourseGeometry.SafeNormalize(Vector3.ProjectOnPlane(arrive, drop),
                                                      RaceCourseGeometry.Perpendicular(drop));
            return true;
        }

        /// <summary>
        /// On a tower, fly the cruise line the ring hangs under - not at the ring, which sits behind a floor plate - and
        /// roll the belly onto the ring's drop line (<see cref="OverrideBellyTarget"/>). Anything else is Waystation's.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            var status = pilot?.Vessel?.VesselStatus;
            if (status != null && TryNextTowerRing(pilot, out _, out _, out _, out Vector3 press, out Vector3 cruise))
            {
                Vector3 hull = status.Transform.position;
                float along = Mathf.Max(0f, Vector3.Dot(hull - press, cruise));
                target = press + cruise * (along + aiHopLookahead);
                _hopRolls.Add(pilot);
                return true;
            }
            if (pilot != null) _hopRolls.Remove(pilot);
            return base.TryOverrideAim(pilot, out target);
        }

        protected override bool OverrideBellyTarget(IPlayer pilot, out Vector3 worldDirection)
        {
            if (_hopRolls.Contains(pilot) && TryNextTowerRing(pilot, out _, out _, out Vector3 drop, out _, out _))
            {
                worldDirection = drop;
                return true;
            }
            return base.OverrideBellyTarget(pilot, out worldDirection);
        }

        protected override void OnServerTick()
        {
            base.OnServerTick();
            if (gameData == null) return;
            if (!gameData.IsTurnRunning)
            {
                if (_hopReleaseAt.Count > 0) ReleaseEveryHop();
                return;
            }
            TickHops();
        }

        void TickHops()
        {
            float now = Time.time;
            var players = gameData.Players;
            if (players == null) return;
            var s = WaystationTowerSettings.ForIntensity(Intensity);

            for (int i = 0; i < players.Count; i++)
            {
                var pilot = players[i];
                var status = pilot?.Vessel?.VesselStatus;
                var handler = status?.ActionHandler;
                if (handler == null) continue;

                // An AUTOPILOT, not merely an AI player. TryGetComponent, never VesselStatus.AIPilot: that getter ADDS an
                // autopilot, and this loop reads every player every server tick (vessel-ai skill §1.7).
                if (!(status is Component statusComponent) || !statusComponent.TryGetComponent(out AIPilot autopilot)
                    || !autopilot.AutoPilotEnabled)
                {
                    if (_hopReleaseAt.ContainsKey(pilot)) ReleaseHop(pilot);
                    continue;
                }

                if (_hopReleaseAt.TryGetValue(pilot, out float releaseAt))
                {
                    if (now >= releaseAt) ReleaseHop(pilot);
                    continue;
                }

                if (!TryNextTowerRing(pilot, out int index, out Vector3 ring, out Vector3 drop, out _, out _)) continue;
                if (!handler.TryGetBoundAction<TigerMothDropActionSO>(out _, out var input)) continue;
                var executor = ExecutorOf(status.Transform);
                if (!executor || executor.IsDropping || !executor.IsReady) continue;

                Transform hull = status.Transform;
                if (TigerMothDropMath.BellyAimError(hull.rotation, drop) > aiHopBellyDegrees) continue;
                Vector3 toRing = ring - hull.position;
                float along = Vector3.Dot(toRing, drop);
                if (along <= 0f) continue;
                if (Vector3.ProjectOnPlane(toRing, drop).magnitude > _course[index].Radius * aiHopLineShare) continue;

                // Through the ring to the middle of the window beyond it: the plunge ring sits deeper, and the same
                // margin past it lands the hull in the plunge's own clear water.
                float travel = along + (s.WindowCentre - s.RingDepth);
                _hopReleaseAt[pilot] = now + WaystationTower.HoldForTravel(travel);
                handler.PerformShipControllerActionsReplicated(input);
            }
        }

        TigerMothDropExecutor ExecutorOf(Transform hull)
        {
            if (!hull) return null;
            if (_hopExecutors.TryGetValue(hull, out var cached) && cached) return cached;
            var found = hull.GetComponentInChildren<TigerMothDropExecutor>(true);
            _hopExecutors[hull] = found;
            return found;
        }

        void ReleaseEveryHop()
        {
            foreach (var pilot in new List<IPlayer>(_hopReleaseAt.Keys)) ReleaseHop(pilot);
            _hopReleaseAt.Clear();
            _hopRolls.Clear();
            _hopExecutors.Clear();
        }

        void ReleaseHop(IPlayer pilot)
        {
            _hopReleaseAt.Remove(pilot);
            var handler = pilot?.Vessel?.VesselStatus?.ActionHandler;
            if (handler == null) return;
            if (handler.TryGetBoundAction<TigerMothDropActionSO>(out _, out var input))
                handler.StopShipControllerActionsReplicated(input);
        }
    }
}

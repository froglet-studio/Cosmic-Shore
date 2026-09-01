using System.Collections.Generic;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Arkway's corridor: whole CELLS as the conveyor's unit, where the Wanderway's belt
    /// carries microscenes. Keeps <see cref="TargetStanding"/> traversal cells alive at once -
    /// previous / current / next - each a real satellite <see cref="Cell"/> (the mode preview's
    /// own machinery: isolated runtime data, thinned environment build via
    /// <see cref="Cell.SatellitePrismStride"/>) drawn from the cell selector's rotation and
    /// tuned for the voyage:
    ///
    ///   • <see cref="Cell.NucleusIsControlZone"/> = false - control is whole-cell VOLUME and
    ///     the herbivore diet is the legacy opposing-domain rule, which is the toy's whole
    ///     mechanic: out-lay a cell and its fauna waves spawn in your colour and cannot eat the
    ///     Ark; lose the volume and the waves hunt it.
    ///   • <see cref="Cell.SatelliteEcologyEnabled"/> = true - unlike a preview, a traversal
    ///     cell RUNS its life spawner (the food web is the point), scaled down by
    ///     <see cref="Cell.RuntimePopulationScale"/>.
    ///
    /// As the Ark crosses into the next cell a fresh one is stood beyond it, and the cell
    /// two-behind retires - but only once its whole membrane sphere is outside the camera
    /// frustum (the microscene conveyor's own removal gate: a world must never be watched
    /// vanishing), struck pool-safely through <see cref="Cell.StrikeSatelliteWorld"/> and
    /// drained a slice per frame. Explicit player-opted apparatus, never decay: the corridor
    /// exists only inside a live voyage and every removal is the voyage's own machinery
    /// (Docs/ECOSYSTEM.md §19).
    /// </summary>
    public sealed class CellConveyor : MonoBehaviour
    {
        /// <summary>Steady-state standing cells (previous / current / next).</summary>
        public const int TargetStanding = 3;

        /// <summary>How far inside a cell's centre the Ark must reach before the corridor
        /// advances (stand the next cell, retire the oldest).</summary>
        public const float ArriveDistance = 140f;

        sealed class TraversalCell
        {
            public GameObject Root;
            public Cell Cell;
            public CellRuntimeDataSO Runtime;
            public CellConfigDataSO Config;
            public Vector3 Centre;
            public bool RetireQueued;
        }

        ArkwayConfig _cfg;
        Container _container;
        System.Random _rng;

        readonly List<TraversalCell> _cells = new();
        readonly List<CellConfigDataSO> _bag = new();      // shuffle-bag of traversal configs
        readonly Plane[] _frustumPlanes = new Plane[6];
        Camera _cam;

        Cell _template;            // the scene's own cell - prefab + runtime shape to clone
        Vector3 _heading = Vector3.forward;
        int _targetIndex;          // index into _cells of the cell the Ark is sailing toward

        // Drains in flight - a COUNT, not a bool: a routine off-screen retire and a voyage-end
        // sweep can overlap, and a bool that either one clears reopens Update's one-at-a-time
        // gate while the other still runs.
        int _drains;

        /// <summary>True while any strike/drain is in flight.</summary>
        public bool IsDraining => _drains > 0;

        /// <summary>The cell the Ark is currently sailing toward (or through).</summary>
        public Cell CurrentCell =>
            _targetIndex >= 0 && _targetIndex < _cells.Count ? _cells[_targetIndex].Cell : null;

        /// <summary>Centre of the cell the Ark is currently sailing toward.</summary>
        public Vector3 CurrentTargetCentre =>
            _targetIndex >= 0 && _targetIndex < _cells.Count
                ? _cells[_targetIndex].Centre
                : transform.position;

        /// <summary>Membrane radius of the current traversal cell, else the config fallback -
        /// the live meaning of "a cell radius" for the leash.</summary>
        public float CurrentCellRadius
        {
            get
            {
                var cell = CurrentCell;
                float r = cell ? cell.MembraneRadius : 0f;
                return r > 1f ? r : _cfg?.LeashRadiusFallback ?? 1200f;
            }
        }

        public bool HasCells => _cells.Count > 0;

        /// <summary>
        /// Stand the first two traversal cells (current + next) down the player's heading.
        /// Call inside the run's arena-build bracket: the environment lays join the raised
        /// veil's hold and the veil releases when everything is laid, created and grown.
        /// </summary>
        public bool Begin(ArkwayConfig cfg, Container container, Vector3 origin, Vector3 heading)
        {
            // The previous voyage's corridor must be fully gone first: cells a new voyage
            // stands land in the SAME _cells list, and a drain still in flight would strike
            // them out from under the live Ark (and then zero the index bookkeeping mid-run).
            // The run enforces this by awaiting idle (and force-striking behind the veil);
            // this guard is the belt refusing to corrupt itself if some future caller forgets.
            if (IsDraining || _cells.Count > 0)
            {
                CSDebug.LogWarning("[Arkway] CellConveyor.Begin while the previous corridor is " +
                                   "still retiring - refused. Await idle (or StrikeAllAsync) first.");
                return false;
            }

            _cfg = cfg;
            _container = container;
            _rng = cfg.Seed != 0 ? new System.Random(cfg.Seed) : new System.Random(System.Environment.TickCount);
            _heading = heading.sqrMagnitude > 0.01f ? heading.normalized : Vector3.forward;
            _targetIndex = 0;

            _template = Cell.FindCellContaining(origin);
            if (!_template) _template = Cell.FindNearestActiveCell(origin);
            if (!_template || !_template.RuntimeData)
            {
                CSDebug.LogWarning("[Arkway] No scene cell to clone traversal cells from - no voyage.");
                return false;
            }

            // First centre far enough out that its membrane clears the host cell's, and the
            // player + Ark start in open water and fly IN. MembraneRadius reads 0 until the
            // membrane has spawned (the ModePreviewArena.FramingRadius bug class) - floor it
            // at the freestyle membrane's authored size so a mid-rebuild host can't fold the
            // corridor back onto itself.
            float hostRadius = Mathf.Max(1200f, _template.MembraneRadius);
            Vector3 first = origin + _heading * (hostRadius + _cfg.CellSpacing * 0.5f);

            if (!StandCell(first)) return false;
            StandCell(NextCentreFrom(first));
            return _cells.Count > 0;
        }

        /// <summary>
        /// The Ark reached the current target's centre: aim it at the NEXT cell, stand a fresh
        /// one beyond that, and queue the oldest for its off-screen retirement. Returns false
        /// when there is no next cell to sail to (a stand failed earlier) - the run should end
        /// the voyage rather than sail the Ark into nothing.
        /// </summary>
        public bool AdvancePastTarget()
        {
            if (_targetIndex + 1 >= _cells.Count)
            {
                // The next cell failed to stand earlier (config exhaustion, template death).
                // Try once more now; a second failure ends the voyage gracefully.
                if (!StandCell(NextCentreFrom(CurrentTargetCentre)))
                    return false;
            }

            _targetIndex++;

            // Stand the new NEXT beyond the new current, so the corridor always reaches one
            // cell ahead of the Ark.
            if (_targetIndex + 1 >= _cells.Count)
                StandCell(NextCentreFrom(_cells[_targetIndex].Centre));

            // Everything older than the new PREVIOUS is done - queue it for the gated retire.
            for (int i = 0; i < _targetIndex - 1; i++)
                _cells[i].RetireQueued = true;

            return true;
        }

        void Update()
        {
            if (IsDraining) return;
            for (int i = 0; i < _cells.Count; i++)
            {
                var record = _cells[i];
                if (!record.RetireQueued || !record.Root) continue;
                if (!IsCellOffScreen(record)) continue;

                RetireCell(record, this.GetCancellationTokenOnDestroy()).Forget();
                break; // one retirement in flight at a time keeps the drain cost flat
            }
        }

        /// <summary>
        /// End of the voyage, the ORDINARY path: queue every standing cell for the same
        /// off-screen-gated retirement the mid-voyage advance uses, so a corridor that is
        /// still in view is never watched popping out (continuity of existence - the
        /// microscene conveyor's removal gate, applied at voyage end too). The cells drain
        /// one at a time as they leave view; a NEW voyage's Begin is what forces the
        /// remainder, behind its raised veil, via <see cref="StrikeAllAsync"/>.
        /// </summary>
        public void RetireAllWhenUnseen()
        {
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].RetireQueued = true;
        }

        // ── Standing a traversal cell ────────────────────────────────────────

        bool StandCell(Vector3 centre)
        {
            var config = NextConfig();
            if (!config || !_template || !_template.RuntimeData) return false;

            // The mode preview's satellite recipe, verbatim: instantiate under an INACTIVE root
            // so the cell's OnEnable cannot wipe the shared runtime asset's config before it has
            // been handed its own instance (Cell.BindSatelliteRuntime documents the race).
            var root = new GameObject($"ArkwayCell_{_cells.Count}");
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            root.transform.position = centre;

            var cellGo = Instantiate(_template.gameObject, root.transform);
            cellGo.name = $"ArkwayCell_{_cells.Count} ({config.CellName})";
            cellGo.transform.localPosition = Vector3.zero;
            cellGo.transform.localRotation = Quaternion.identity;

            // A runtime Instantiate gets no dependency injection, and the whole of
            // Controller/Environment relies on being present at load - inject or the cell's
            // spawners come up with null GameData and refuse to run.
            if (_container != null)
                GameObjectInjector.InjectRecursive(cellGo, _container);
            else
                CSDebug.LogWarning("[Arkway] No DI container - the traversal cell's injected " +
                                   "dependencies will be null.");

            var cell = cellGo.GetComponentInChildren<Cell>(true);
            if (!cell)
            {
                CSDebug.LogError("[Arkway] The cloned cell carries no Cell component - stand aborted.");
                Destroy(root);
                return false;
            }

            var runtime = Instantiate(_template.RuntimeData);
            runtime.name = $"{_template.RuntimeData.name} (arkway {_cells.Count})";
            runtime.ResetRuntimeData();
            cell.BindSatelliteRuntime(runtime);

            root.SetActive(true);

            // The voyage's tuning, all BEFORE InitializeSatellite - the spawner starts inside it.
            cell.SatellitePrismStride = Mathf.Max(1, _cfg.PrismStride);
            cell.SatelliteEcologyEnabled = true;
            cell.RuntimePopulationScale = Mathf.Clamp(_cfg.PopulationScale, 0.1f, 1f);
            // Whole-cell volume control + the legacy opposing-domain diet: the state the
            // ecology already supports for a cell with no claim (Docs/ECOSYSTEM.md §25.1),
            // and the spine of the Arkway's protect-the-Ark mechanic.
            cell.NucleusIsControlZone = false;

            if (!cell.InitializeSatellite(config))
            {
                Destroy(root);
                Destroy(runtime);
                return false;
            }

            _cells.Add(new TraversalCell
            {
                Root = root,
                Cell = cell,
                Runtime = runtime,
                Config = config,
                Centre = centre,
            });

            CSDebug.Log($"[Arkway] Traversal cell stood: {config.CellName} at {centre} " +
                        $"(stride {cell.SatellitePrismStride}, populations ×{cell.RuntimePopulationScale:0.##}).");
            return true;
        }

        /// <summary>
        /// Next corridor centre: one spacing on from <paramref name="from"/>, with the heading
        /// deviated inside the authored cone so the corridor wanders instead of running straight.
        /// </summary>
        Vector3 NextCentreFrom(Vector3 from)
        {
            float maxTurn = Mathf.Clamp(_cfg.MaxTurnDegrees, 0f, 60f);
            if (maxTurn > 0.01f && _rng != null)
            {
                float angle = (float)(_rng.NextDouble() * maxTurn);
                float roll = (float)(_rng.NextDouble() * 360.0);
                // A random axis perpendicular to the heading, so the cone is genuinely 3D -
                // the hypersea has no up the corridor must respect.
                Vector3 side = Vector3.Cross(_heading, Mathf.Abs(_heading.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 axis = Quaternion.AngleAxis(roll, _heading) * side;
                _heading = (Quaternion.AngleAxis(angle, axis) * _heading).normalized;
            }
            return from + _heading * Mathf.Max(2000f, _cfg.CellSpacing);
        }

        /// <summary>
        /// The traversal rotation: the definition's authored list when present, else the host
        /// cell's own configs (the cell selector's list) minus its environment-free entries - a
        /// cell that builds nothing is open water, which the corridor already has between cells.
        /// Shuffle-bag so every world appears before any repeats.
        /// </summary>
        CellConfigDataSO NextConfig()
        {
            if (_bag.Count == 0)
            {
                var authored = _cfg?.Cells;
                IReadOnlyList<CellConfigDataSO> source = authored is { Count: > 0 }
                    ? authored
                    : _template ? _template.AvailableConfigs : null;
                if (source == null) return null;

                foreach (var config in source)
                    if (config && config.EnvironmentPrefab && !_bag.Contains(config))
                        _bag.Add(config);

                // Fisher-Yates on the seeded stream, so a seeded voyage is reproducible.
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }

                if (_bag.Count == 0)
                {
                    CSDebug.LogWarning("[Arkway] No traversal-cell configs with an authored " +
                                       "environment - nothing to sail through.");
                    return null;
                }
            }

            var next = _bag[^1];
            _bag.RemoveAt(_bag.Count - 1);
            return next;
        }

        // ── Retiring a traversal cell ────────────────────────────────────────

        /// <summary>
        /// True when the record's whole membrane sphere lies outside the camera frustum - the
        /// only state in which striking it is invisible (the microscene conveyor's own removal
        /// gate, with the cell's membrane as the sphere). Conservative: no camera = not off
        /// screen, so the retire simply waits.
        /// </summary>
        bool IsCellOffScreen(TraversalCell record)
        {
            float radius = (record.Cell ? Mathf.Max(record.Cell.MembraneRadius, 600f) : 1200f) + 200f;

            if (!_cam) _cam = Camera.main;
            if (!_cam) return false;

            GeometryUtility.CalculateFrustumPlanes(_cam, _frustumPlanes);
            for (int i = 0; i < _frustumPlanes.Length; i++)
                if (_frustumPlanes[i].GetDistanceToPoint(record.Centre) < -radius)
                    return true;
            return false;
        }

        async UniTaskVoid RetireCell(TraversalCell record, CancellationToken ct)
        {
            _drains++;
            try
            {
                await StrikeAndDrain(record, ct);
            }
            finally
            {
                _drains--;
            }
        }

        /// <summary>
        /// Pool-safe strike + frame-sliced drain (the mode preview's teardown, per cell): pooled
        /// prisms go back to their pool inside <see cref="Cell.StrikeSatelliteWorld"/> - never
        /// through Destroy, which corrupts the pool and with it every trail in the scene - and
        /// the instantiated remainder is destroyed a slice per frame.
        /// </summary>
        async UniTask StrikeAndDrain(TraversalCell record, CancellationToken ct)
        {
            // Keep _targetIndex pointing at the same cell after the removal: retiring a cell
            // BELOW the target shifts every later index down by one.
            int index = _cells.IndexOf(record);
            _cells.Remove(record);
            if (index >= 0 && index < _targetIndex)
                _targetIndex = Mathf.Max(0, _targetIndex - 1);

            GameObject retiring = null;
            if (record.Cell) retiring = record.Cell.StrikeSatelliteWorld();

            if (record.Root) Destroy(record.Root);
            if (record.Runtime) Destroy(record.Runtime);

            if (retiring)
            {
                const int PrismsPerFrame = 150;
                var prisms = retiring.GetComponentsInChildren<Prism>(true);
                for (int i = 0; i < prisms.Length; i++)
                {
                    if (prisms[i]) Destroy(prisms[i].gameObject);
                    if ((i + 1) % PrismsPerFrame == 0)
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                Destroy(retiring);
            }
        }

        /// <summary>
        /// FORCE-strike every standing cell, awaited. This has no off-screen gate, so its only
        /// legitimate callers are contexts where the removal is unseen BY CONSTRUCTION: a new
        /// voyage's Begin with the <see cref="EnvironmentLoadVeil"/> already covering the screen
        /// (the one place a leftover corridor must be cleared NOW), and teardown paths. The
        /// ordinary voyage-end path is <see cref="RetireAllWhenUnseen"/>.
        /// </summary>
        public async UniTask StrikeAllAsync(CancellationToken ct)
        {
            _drains++;
            try
            {
                while (_cells.Count > 0)
                    await StrikeAndDrain(_cells[_cells.Count - 1], ct);
            }
            finally
            {
                _drains--;
                _targetIndex = 0;
                _bag.Clear();
            }
        }

        void OnDestroy()
        {
            // Scene teardown: the drain coroutine dies with us - strike synchronously, pool-safe,
            // and let the scene unload take the instantiated remainder in one sweep (the unload
            // pays that cost anyway).
            for (int i = 0; i < _cells.Count; i++)
            {
                var record = _cells[i];
                if (record.Cell) record.Cell.StrikeSatelliteWorld();
                if (record.Runtime) Destroy(record.Runtime);
            }
            _cells.Clear();
        }
    }
}

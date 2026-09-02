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
    ///   • An ORDINARY CELL: it keeps its authored nucleus AND its control zone (the shipped
    ///     default), and it is handed a CRYSTAL at its core. So control is the nucleus claim -
    ///     lay environment mass through the core to take the cell and its fauna waves spawn in
    ///     your colour - and the herbivore diet is the shipped SPATIAL rule: the nucleus is
    ///     sanctuary and everything outside it is voraciously grazed by any domain. The Ark's
    ///     hull is ordinary mass sailing that exterior, so it is FOOD the whole crossing and
    ///     safe only under the core it is making for. Nothing here is bespoke: it is the
    ///     nucleus-cell ecology as shipped, and both halves of the toy - who owns the cell, and
    ///     what the swarm does to your ship - fall out of it.
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

        // Worlds struck but not yet fully drained. A strike hands back a NEW world-space root
        // that is deliberately NOT parented to anything the conveyor owns (so the cell can be
        // destroyed immediately while its mass drains a slice per frame) - which also means
        // nothing else can collect it. If the drain is cancelled (the toybox root torn down
        // mid-retire) the root would survive with its whole world in it, so it is tracked and
        // swept on teardown. The general shape: an object deliberately orphaned for the
        // duration of an async is an object whose async no longer owns its cleanup.
        readonly List<GameObject> _retiringRoots = new();

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
        /// Raised as a traversal cell is struck, BEFORE its drain runs. The run listens so the
        /// player's own trail mass laid in (and on the way to) that cell goes with it — a struck
        /// world takes its loose trail mass with it, exactly as
        /// <see cref="Cell.RequestCellSwap"/>'s <c>clearLooseTrailMass</c> does for a world swap.
        /// That is what makes the corridor explorable indefinitely rather than accumulating an
        /// unbounded ribbon behind it.
        /// </summary>
        public event System.Action CellRetired;

        /// <summary>
        /// Stand the FIRST traversal cell down the player's heading. Call inside the run's
        /// arena-build bracket: its environment lay joins the raised veil's hold and the veil
        /// releases when it is laid, created and grown. Only ONE cell stands behind the veil —
        /// the second is <see cref="StandAhead"/>, called once the screen is open, so it streams
        /// in beside live play exactly as every later cell does. Two 10k-prism worlds behind
        /// the veil was a 30–90 s blind opening (`Docs/ECOSYSTEM.md` §41.3.3.3); one is half that,
        /// and a satellite build beside live play is what a satellite build is for.
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
                CSDebug.LogWarning($"[Arkway] No scene cell to clone traversal cells from " +
                                   $"(template {(_template ? _template.name : "null")}, runtime " +
                                   $"{(_template && _template.RuntimeData ? "ok" : "null")}) - no voyage.");
                return false;
            }

            // First centre far enough out that its membrane clears the host cell's, and the
            // player + Ark start in open water and fly IN. MembraneRadius reads 0 until the
            // membrane has spawned (the ModePreviewArena.FramingRadius bug class) - floor it
            // at the freestyle membrane's authored size so a mid-rebuild host can't fold the
            // corridor back onto itself.
            float hostRadius = Mathf.Max(1200f, _template.MembraneRadius);
            Vector3 first = origin + _heading * (hostRadius + _cfg.CellSpacing * 0.5f);

            if (!StandCell(first))
            {
                CSDebug.LogWarning("[Arkway] The FIRST traversal cell could not be stood - no voyage.");
                return false;
            }
            return _cells.Count > 0;
        }

        /// <summary>
        /// Stand one more cell beyond the last standing one — the voyage's second cell, stood
        /// UNVEILED once the screen is open. Idempotent in effect: <see cref="AdvancePastTarget"/>
        /// retries a missing next cell on its own, so a failure here only costs a warning.
        /// </summary>
        public bool StandAhead()
        {
            if (_cells.Count == 0) return false;
            if (!StandCell(NextCentreFrom(_cells[^1].Centre)))
            {
                CSDebug.LogWarning("[Arkway] The second traversal cell could not be stood - the " +
                                   "corridor will retry when the Ark reaches the first.");
                return false;
            }
            return true;
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

            StripAccumulatedContent(cellGo.transform);

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
            // NucleusIsControlZone is deliberately LEFT AT ITS DEFAULT (true). A traversal cell
            // is an ordinary cell, not a mode's borrowed play geometry: its nucleus is a claim
            // to contest, its interior is fauna sanctuary, and its exterior is the feeding
            // ground the Ark has to cross. Collapsing the control zone (which this used to do)
            // made the whole cell legacy opposing-domain territory, which is what left the Ark
            // untouchable by any swarm wearing its own colour.

            if (!cell.InitializeSatellite(config))
            {
                CSDebug.LogWarning($"[Arkway] Traversal cell '{config.CellName}' refused " +
                                   "InitializeSatellite - stand aborted (the Cell warned above with the reason).");
                Destroy(root);
                Destroy(runtime);
                return false;
            }

            SpawnCoreCrystal(runtime, root.transform);

            _cells.Add(new TraversalCell
            {
                Root = root,
                Cell = cell,
                Runtime = runtime,
                Config = config,
                Centre = centre,
            });

            CSDebug.LogVerbose(CSLogChannel.CellLifecycle,
                $"[Arkway] Traversal cell stood: {config.CellName} at {centre} " +
                $"(stride {cell.SatellitePrismStride}, populations x{cell.RuntimePopulationScale:0.##}).");
            return true;
        }

        /// <summary>
        /// A traversal cell must START EMPTY.
        ///
        /// The corridor clones the LIVE SCENE CELL — there is no prefab to instantiate at
        /// runtime, and the scene cell is the only thing that carries the right prefab, runtime
        /// shape and component wiring — but a live cell ACCUMULATES: <see cref="Cell"/> parents
        /// its authored environment to itself, every lifeform heart the food web drops is
        /// re-homed onto it (<see cref="Crystal.ActivateCrystal"/>,
        /// <see cref="Crystal.DetachHeartToCell"/>), and anything a mode or toy parents there
        /// stays. Cloning it verbatim copies all of that into EVERY traversal cell, three
        /// standing at a time, for the whole voyage — so a session that has been running a while
        /// makes each new cell more expensive than the last, which is exactly the shape of "the
        /// world got sparser and the frame rate got worse".
        ///
        /// So the clone is stripped of world CONTENT and keeps only the cell's own structure.
        /// The doomed branches are re-parented into an INACTIVE scrap root first and destroyed
        /// with it: <c>Destroy</c> alone defers to end of frame, and <c>root.SetActive(true)</c>
        /// runs a few lines later would wake every one of them (a cloned Prism registering with
        /// the spatial index, a cloned Crystal joining <c>Crystal.Active</c>) before the deferred
        /// destroy took them away again.
        ///
        /// Deliberately a DENYLIST of content types rather than an allowlist of components: the
        /// cell's own structure is whatever the prefab author put there and must survive
        /// untouched, while the things that accumulate are a short, knowable list.
        /// </summary>
        static void StripAccumulatedContent(Transform cellRoot)
        {
            GameObject scrap = null;
            int stripped = 0;

            // Depth-first over the clone; a branch that is stripped is not descended into.
            var stack = new Stack<Transform>();
            for (int i = cellRoot.childCount - 1; i >= 0; i--) stack.Push(cellRoot.GetChild(i));

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (!t) continue;

                if (t.GetComponent<Prism>() || t.GetComponent<Crystal>() ||
                    t.GetComponent<LifeForm>() || t.GetComponent<Toy>() ||
                    t.GetComponent<Unity.Netcode.NetworkObject>())
                {
                    if (!scrap)
                    {
                        scrap = new GameObject("ArkwayCell_StrippedContent");
                        scrap.SetActive(false);
                    }
                    t.SetParent(scrap.transform, false);
                    stripped++;
                    continue;
                }

                for (int i = t.childCount - 1; i >= 0; i--) stack.Push(t.GetChild(i));
            }

            if (!scrap) return;
            Destroy(scrap);
            CSDebug.LogVerbose(CSLogChannel.CellLifecycle,
                $"[Arkway] Traversal cell clone stripped of {stripped} accumulated object(s).");
        }

        /// <summary>
        /// Give a traversal cell the CRYSTAL every cell has: one omni crystal at the core,
        /// inside the nucleus - the canonical omni volume (Docs/ECOSYSTEM.md §27: the nucleus
        /// IS the crystal volume, and a crystal that respawns elsewhere makes the nucleus
        /// marker a lie). A satellite gets none by itself, because a scene cell's crystals come
        /// from a <see cref="CrystalManager"/> and a satellite has no manager feeding it, so
        /// this is the one thing the corridor has to hand its cells.
        ///
        /// It is a real crystal, not a marker: registered in the satellite's OWN runtime
        /// (never the scene asset's list), collectable by anyone, and it blooms in through the
        /// crystal's own fade rather than popping. It is also what makes the cell's core worth
        /// flying to, which is the same place the Ark is heading and the one place the food web
        /// cannot follow it.
        ///
        /// One always-on trigger collider per standing cell - three in steady state.
        ///
        /// The prefab's own serialized <c>cellData</c> still points at the shared asset, so its
        /// self-removal on destroy may miss this list; <see cref="CellRuntimeDataSO.PruneDestroyed"/>
        /// makes that self-healing. Same accepted trade the mode preview's crystals make.
        /// </summary>
        void SpawnCoreCrystal(CellRuntimeDataSO runtime, Transform parent)
        {
            var prefab = _cfg?.CrystalPrefab;
            if (!prefab)
            {
                var library = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
                prefab = library ? library.OmniCrystalPrefab : null;
            }
            if (!prefab)
            {
                CSDebug.LogWarning("[Arkway] No crystal prefab (definition, and no omni crystal on " +
                                   "Resources/ModePreviewLibrary) - the traversal cell's core is bare.");
                return;
            }

            var crystal = Instantiate(prefab, parent);
            crystal.transform.localPosition = Vector3.zero;   // the cell's core
            crystal.gameObject.SetActive(true);
            crystal.enabled = true;
            crystal.DeactivateModels();                        // the crystal's own fade-in bloom
            if (runtime) runtime.AddCrystalToList(crystal);
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
            CellRetired?.Invoke();
            if (index >= 0 && index < _targetIndex)
                _targetIndex = Mathf.Max(0, _targetIndex - 1);

            GameObject retiring = null;
            if (record.Cell) retiring = record.Cell.StrikeSatelliteWorld();
            if (retiring) _retiringRoots.Add(retiring);

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
                _retiringRoots.Remove(retiring);
                Destroy(retiring);
            }
        }

        /// <summary>
        /// One line naming everything the corridor is holding. Off by default (channel
        /// <see cref="CSLogChannel.CellLifecycle"/>) and raised once per advance, so it costs
        /// nothing in a normal session and answers "what is growing?" in the one that is
        /// getting slower — which is a question no amount of reading the code settles.
        /// </summary>
        public string Census()
        {
            int prisms = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i].Cell;
                if (cell) prisms += cell.LiveBlockCount;
            }
            return $"cells {_cells.Count} (target {_targetIndex}), tracked prisms {prisms}, " +
                   $"draining {_drains}, retiring roots {_retiringRoots.Count}, bag {_bag.Count}";
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

            // Anything a cancelled drain orphaned. Not a scene-unload concern (that sweeps
            // everything anyway) - it is the toybox root being torn down while the scene lives.
            for (int i = 0; i < _retiringRoots.Count; i++)
                if (_retiringRoots[i]) Destroy(_retiringRoots[i]);
            _retiringRoots.Clear();
        }
    }
}

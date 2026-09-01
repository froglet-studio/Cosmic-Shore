using System.Collections.Generic;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime for the <see cref="CellSelectorToyDefinitionSO"/> - the freestyle <b>world
    /// picker</b>, and the freestyle <b>reset</b>.
    ///
    /// Pass 1 (the toy itself): a matrix of worlds blooms out ahead - one station per config in
    /// the containing <see cref="Cell"/>'s own rotation (the Cell owns the environment; the toy
    /// reads its list rather than authoring a parallel one). Each station IS a genuine SCALE
    /// MODEL of the world that config creates, sampled by <see cref="CellMiniatureBuilder"/>
    /// straight from the generator's own output (real silhouette, real structure, real domain
    /// composition; no prisms spawned) - no cage, no orb, nothing around it. A config with no
    /// authored environment has nothing to model, so its slot reads visibly EMPTY.
    ///
    /// Pass 2 (a world): <see cref="Cell.RequestCellSwap"/> - the cell retires its current world
    /// in a suction and grows the chosen one back behind the standard
    /// <see cref="EnvironmentLoadVeil"/>. Choosing the cell you are already in is the reset.
    ///
    /// This is the opt-in half of <c>CellTypeChoiceOptions.EnvironmentFree</c>: freestyle boots
    /// with no authored environment (so entering Menu_Main - and returning to it from an arcade
    /// game - is fast), and the multi-second world build happens only when a player flies into
    /// a model and asks for it.
    /// </summary>
    public sealed class CellSelectorToy : MatrixToy, IToyShellSurface
    {
        CellSelectorToyDefinitionSO _def;

        // Built models, keyed by the config they portray. Generation is the expensive part, so a
        // re-opened matrix is free. Instance-scoped (not static) and destroyed with the toy, so
        // the meshes cannot outlive the scene.
        readonly Dictionary<CellConfigDataSO, CellMiniatureBuilder.Miniature> _miniatures = new();

        // The configs the open matrix is showing, index-aligned with _stationRoots.
        readonly List<CellConfigDataSO> _offered = new();
        readonly List<Transform> _stationRoots = new();
        Cell _offeringCell;

        // Cancels the model stream when the matrix closes - a selection tears down the grid AND
        // starts a cell swap, and the streamer must not still be generating (and releasing the
        // generator's cache) underneath the build that swap just started.
        CancellationTokenSource _streamCts;

        public void Configure(CellSelectorToyDefinitionSO definition) => _def = definition;

        // ── The toy's own emblem: the world you are in, in miniature ─────────

        protected override void OnInitialized() => AttachEmblem(new EmblemSource(this), 0f);

        /// <summary>
        /// The Cell Selector's root shows <b>the world you are in right now</b> - core only.
        ///
        /// <see cref="IEmblemSource.SatelliteCount"/> is 0 by construction, not by tuning: a
        /// satellite here would be another world, and every world that is not already loaded costs
        /// a full environment generation to picture. The core itself is free or it is nothing -
        /// it reads the miniature the matrix already built, else the LIVE environment's cached
        /// lays, else stays empty. At Menu_Main boot the cell is EnvironmentFree, so the emblem is
        /// a small bare core: iconographically exact - you are in the empty one.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly CellSelectorToy _toy;
            public EmblemSource(CellSelectorToy toy) => _toy = toy;

            public int SatelliteCount => 0;

            // The world model wears its own real domain prism materials.
            public bool UsesSharedMaterial => false;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = true; // mesh assembly - give the streamer a clear frame after it
                var cell = _toy.HostCell;
                if (!cell || cell.IsSwappingConfig) return false;
                if (!_toy.TryGetEmblemMiniature(cell.Config, out var miniature)) return false;

                // NOT Own()ed: these meshes live in _miniatures and are freed by OnDestroy.
                var go = _toy.AttachMiniature(holder, miniature, spinRate: 8f, bloom: false);
                if (!go) return false;

                // Every cached miniature is fitted to the STATION radius (one canonical size, so
                // the emblem and the matrix share one mesh); the emblem wears it smaller.
                go.transform.localScale = Vector3.one * (radius / Mathf.Max(0.01f, _toy._def.StationRadius));
                return true;
            }

            public bool TryGetLiveKey(out object key)
            {
                key = null;
                var cell = _toy.HostCell;
                // Mid-swap the cell has no settled identity - returning false holds the current
                // emblem until it does, instead of rebuilding against a half-changed world.
                if (!cell || cell.IsSwappingConfig) return false;
                key = cell.Config;
                return true;
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = default;
                return false; // the model wears the world's real domain materials
            }
        }

        /// <summary>
        /// A miniature of <paramref name="config"/> WITHOUT ever generating an environment: the
        /// matrix's cache first, else the live environment's already-generated lays. Anything else
        /// returns false - <see cref="CellMiniatureBuilder.Build"/> would trigger a full ~34k-lay
        /// generation, which is exactly the cost the whole EnvironmentFree boot exists to avoid.
        /// </summary>
        bool TryGetEmblemMiniature(CellConfigDataSO config, out CellMiniatureBuilder.Miniature miniature)
        {
            miniature = default;
            if (!config || !_def) return false;

            if (_miniatures.TryGetValue(config, out var cached) && cached.Mesh)
            {
                miniature = cached;
                return true;
            }

            if (config.EnvironmentPrefab is not CellEnvironmentSpawnableBase env) return false;
            if (env.CachedLays is not { Count: > 0 } lays) return false;

            miniature = CellMiniatureBuilder.BuildFromLays(lays, _def.StationRadius, _def.ModelPointBudget,
                _def.SignatureCoverage, config.name);
            if (!miniature.IsValid) return false;

            // Cached against the config, so a later matrix open reuses this one mesh.
            _miniatures[config] = miniature;
            return true;
        }

        // ── Layout ───────────────────────────────────────────────────────────

        protected override int StationCount => _offered.Count;
        protected override float StationSpacing => _def.StationSpacing;
        protected override float StationRadius => _def.StationRadius;
        protected override float MatrixDistanceFactor => _def.MatrixDistanceFactor;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            if (IsMatrixOpen)
            {
                CloseMatrix();
                return;
            }

            // Resolve what to offer BEFORE the base opens - StationCount reads from it.
            if (!ResolveOffer()) return;
            base.OnActivated(localVessel);
        }

        /// <summary>
        /// The cell this toy lives in. Resolved from the TOY's position (always inside the
        /// membrane), never from a station - the matrix sits outward and can cross it.
        /// </summary>
        Cell HostCell
        {
            get
            {
                // Unity's lifetime-aware operator, not `??` - a destroyed Cell is non-null by
                // reference and would slip straight through the null-coalescing form.
                var containing = Cell.FindCellContaining(transform.position);
                return containing ? containing : Cell.FindNearestActiveCell(transform.position);
            }
        }

        /// <summary>
        /// Fill <see cref="_offered"/> with the cells to show. The definition's explicit list when
        /// authored, else the host Cell's own rotation - reading the Cell is the default on
        /// purpose, since it is the single source of truth for what this scene's cell can be.
        /// </summary>
        bool ResolveOffer()
        {
            _offered.Clear();
            _stationRoots.Clear();
            _offeringCell = HostCell;

            if (!_offeringCell)
            {
                CSDebug.LogWarning("[CellSelector] No active Cell in the scene - nothing to select.");
                return false;
            }
            if (_offeringCell.IsSwappingConfig)
            {
                // Mid-swap the cell has no settled identity, so a matrix built now would
                // mislabel which world is current (and which pass is the reset).
                CSDebug.Log("[CellSelector] A cell swap is in flight - try again once it settles.");
                return false;
            }

            var authored = _def ? _def.Cells : null;
            var source = authored is { Count: > 0 } ? authored : _offeringCell.AvailableConfigs;
            if (source == null) return false;

            foreach (var config in source)
                if (config && !_offered.Contains(config))
                    _offered.Add(config);

            if (_offered.Count != 0) return true;
            CSDebug.LogWarning($"[CellSelector] Cell {_offeringCell.ID} lists no CellConfigs - nothing to select.");
            return false;
        }

        // ── Stations: the model, and nothing but the model ───────────────────

        protected override void BuildStation(int index, Transform parent, Vector3 position, float radius)
        {
            var config = _offered[index];
            bool isCurrent = config == _offeringCell.Config;

            var station = CreateStation(parent, position, DisplayNameOf(config), radius * 1.6f);
            _stationRoots.Add(station.transform);

            // No shell, no cage: the scale model is the station. It streams in after every
            // station exists (see OnMatrixOpened), so the matrix is legible immediately and the
            // generation cost is spread.
            //
            // "The world you are in" is told by a SHAPE, not by the word RESET: every station wears
            // the switch ring that says "fly me", and the current cell's model wears a SECOND,
            // inner halo hugging the model itself - "this one is already yours". Everything else is
            // a plain model, and an environment-free config has nothing to model, so its slot reads
            // visibly empty (= instant).
            if (isCurrent)
            {
                var halo = ToyFactory.AddRingBody(station.transform, radius * 1.25f, Definition.AccentColor);
                halo.name = "CurrentWorldHalo";
                // Hugging the model, well inside the station's own switch ring, and turning the
                // OTHER way: two concentric rings spinning together would read as one thick rim
                // rather than as "a model that is already ringed".
                if (halo.TryGetComponent(out ToyIdleSpin haloSpin))
                    haloSpin.Configure(Vector3.forward, -15f);
            }

            var text = ToyFactory.AddRingedLabel(station.transform, DisplayNameOf(config),
                Definition.AccentColor, StationRingRadius(radius * 1.6f), radius);
            if (isCurrent && text) text.fontStyle = TMPro.FontStyles.Bold;

            var capturedConfig = config;
            var capturedCell = _offeringCell;
            station.OnVesselPassed = () => SelectCell(capturedCell, capturedConfig);
        }

        protected override void OnMatrixOpened()
        {
            var pending = new List<(Transform host, CellConfigDataSO config)>();
            for (int i = 0; i < _offered.Count && i < _stationRoots.Count; i++)
                if (_offered[i].EnvironmentPrefab)
                    pending.Add((_stationRoots[i], _offered[i]));

            CancelStream();
            _streamCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            StreamMiniatures(pending, _streamCts.Token).Forget();

            CSDebug.Log($"[CellSelector] {_offered.Count} cells offered " +
                        $"(current: {DisplayNameOf(_offeringCell.Config)}); {pending.Count} scale models building.");
        }

        protected override void OnMatrixClosed() => CancelStream();

        protected override void OnDestroy()
        {
            base.OnDestroy();
            CancelStream();
            foreach (var miniature in _miniatures.Values)
                if (miniature.Mesh) Destroy(miniature.Mesh);
            _miniatures.Clear();
        }

        void CancelStream()
        {
            if (_streamCts == null) return;
            _streamCts.Cancel();
            _streamCts.Dispose();
            _streamCts = null;
        }

        static string DisplayNameOf(CellConfigDataSO config)
        {
            if (!config) return "—";
            return string.IsNullOrWhiteSpace(config.CellName) ? config.name : config.CellName;
        }

        // ── Pass 2: become that cell ─────────────────────────────────────────

        void SelectCell(Cell cell, CellConfigDataSO config)
        {
            if (!cell || !config) return;
            if (cell.IsSwappingConfig)
            {
                CSDebug.Log("[CellSelector] A cell swap is already in flight - ignoring this pass.");
                return;
            }

            // The matrix closes first: the world it describes is about to stop being true.
            CloseMatrix();

            if (cell.RequestCellSwap(config, _def.ClearLooseTrailMass))
                CSDebug.Log($"[CellSelector] → {DisplayNameOf(config)} " +
                            $"(environment: {(config.EnvironmentPrefab ? config.EnvironmentPrefab.name : "none")}, " +
                            $"clear loose trail mass: {_def.ClearLooseTrailMass}).");
        }

        // ── App-shell face ───────────────────────────────────────────────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        // Mid-swap the cell has no settled identity, so the list would mislabel which world is
        // current - the same reason ResolveOffer refuses to open the matrix then.
        bool IToyShellSurface.ShellAvailable
        {
            get
            {
                var cell = HostCell;
                return cell && !cell.IsSwappingConfig;
            }
        }

        /// <summary>
        /// The cell's own rotation, the world you are in flagged as current. Choosing it is the
        /// freestyle RESET, exactly as flying its haloed model is - so unlike the other surfaces
        /// the current row stays actionable.
        /// </summary>
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into)
        {
            var cell = HostCell;
            if (!cell) return;

            var authored = _def ? _def.Cells : null;
            var source = authored is { Count: > 0 } ? authored : cell.AvailableConfigs;
            if (source == null) return;

            var seen = new HashSet<CellConfigDataSO>();
            foreach (var config in source)
            {
                if (!config || !seen.Add(config)) continue;

                var capturedConfig = config;
                var capturedCell = cell;
                bool isCurrent = config == cell.Config;

                into.Add(new ToyShellOption
                {
                    Label = DisplayNameOf(config),
                    Detail = isCurrent
                        ? "you are here - choose it again to reset"
                        : config.EnvironmentPrefab ? "" : "no environment",
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    IsCurrent = isCurrent,
                    Apply = () => SelectCell(capturedCell, capturedConfig),
                });
            }
        }

        // ── Scale models ─────────────────────────────────────────────────────

        /// <summary>
        /// Fill each slot with a scale model of the world its config creates, ONE PER FRAME.
        /// Each model costs one environment generation (pure math - no prism is ever spawned),
        /// which is small next to a real build but too big to do seven of in a single frame.
        /// The models bloom in as they land, so nothing pops.
        /// </summary>
        async UniTaskVoid StreamMiniatures(
            List<(Transform host, CellConfigDataSO config)> pending, CancellationToken ct)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                var (host, config) = pending[i];
                if (!host || !config) continue;

                var miniature = ResolveMiniature(config);
                if (!miniature.IsValid) continue;

                AttachMiniature(host, miniature);

                // A clear frame between generations so a heavy one never stacks with the next.
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        /// <summary>
        /// The model for a config, built once and kept. The generator's point data is released
        /// immediately after sampling: holding seven 34k-entry lay lists so the menu can show
        /// seven thumbnails is the wrong trade on mobile, and re-generating on load is a small
        /// fraction of the lay cost.
        /// </summary>
        CellMiniatureBuilder.Miniature ResolveMiniature(CellConfigDataSO config)
        {
            if (_miniatures.TryGetValue(config, out var cached) && cached.Mesh)
                return cached;

            var prefab = config.EnvironmentPrefab;
            if (!prefab) return default;

            var built = CellMiniatureBuilder.Build(prefab, _def.StationRadius, _def.ModelPointBudget,
                _def.SignatureCoverage);
            if (prefab is CellEnvironmentSpawnableBase env) env.ReleaseGeneratedData();

            if (built.IsValid) _miniatures[config] = built;
            else CSDebug.LogWarning($"[CellSelector] {prefab.name} generated no points - " +
                                    $"{DisplayNameOf(config)} shows as an empty slot.");
            return built;
        }

        GameObject AttachMiniature(Transform host, CellMiniatureBuilder.Miniature miniature,
            float spinRate = 12f, bool bloom = true)
        {
            var go = ToyFactory.AddMiniatureBody(host, miniature, Context, "ScaleModel");
            if (!go) return null;

            // The world turns in place - a thing you can watch.
            go.AddComponent<ToyIdleSpin>().Configure(Vector3.up, spinRate);

            // Continuity of existence: the model grows in rather than appearing. Zeroed inside the
            // helper before the first tick, so it can never render at full size for a frame first.
            if (bloom) ToyFactory.ScaleInFromZero(go.transform, 0.6f).Forget();
            return go;
        }
    }
}

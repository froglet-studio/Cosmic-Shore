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
    /// Pass 1 (the toy itself): a matrix of MINI-CELLS blooms one layer outward - one station
    /// per config in the containing <see cref="Cell"/>'s own rotation (the Cell owns the
    /// environment; the toy reads its list rather than authoring a parallel one). Each station
    /// is a little cell: three gyroscopic rings for the membrane, a nucleus dot, and - inside -
    /// a genuine SCALE MODEL of the world that config creates, sampled by
    /// <see cref="CellMiniatureBuilder"/> straight from the generator's own output (real
    /// silhouette, real structure, real domain composition; no prisms spawned). A config with no
    /// authored environment has nothing to model, so it reads visibly EMPTY.
    ///
    /// Pass 2 (a mini-cell): <see cref="Cell.RequestCellSwap"/> - the cell retires its current
    /// world in a suction and grows the chosen one back behind the standard
    /// <see cref="EnvironmentLoadVeil"/>. Choosing the cell you are already in is the reset.
    ///
    /// This is the opt-in half of <c>CellTypeChoiceOptions.EnvironmentFree</c>: freestyle boots
    /// with no authored environment (so entering Menu_Main - and returning to it from an arcade
    /// game - is fast), and the multi-second world build happens only when a player flies into
    /// a mini-cell and asks for it.
    ///
    /// Collider note: stations are transient trigger spheres (one per config, Menu_Main
    /// freestyle only) torn down with the matrix - no per-cell budget impact.
    /// </summary>
    public sealed class CellSelectorToy : Toy
    {
        CellSelectorToyDefinitionSO _def;
        GameObject _grid;

        // Built models, keyed by the config they portray. Generation is the expensive part, so a
        // re-opened matrix is free. Instance-scoped (not static) and destroyed with the toy, so
        // the meshes cannot outlive the scene.
        readonly Dictionary<CellConfigDataSO, CellMiniatureBuilder.Miniature> _miniatures = new();

        // Cancels the model stream when the matrix closes - a selection tears down the grid AND
        // starts a cell swap, and the streamer must not still be generating (and releasing the
        // generator's cache) underneath the build that swap just started.
        CancellationTokenSource _streamCts;

        public void Configure(CellSelectorToyDefinitionSO definition) => _def = definition;

        protected override void OnInitialized()
        {
            // Findability + identity: the world picker wears three EMPTY little worlds as moons -
            // the same membrane shell the matrix stations wear, waiting to be filled. They stay
            // empty on purpose: filling them would mean generating environments at menu boot,
            // which is the exact cost this toy exists to defer.
            //
            // Sized off the ACTUAL body radius - the toybox places toys at menu scale (tens of
            // world units), so decoration authored in raw units would sit inside the body sphere.
            float body = Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f;
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                var moon = new GameObject($"Moon_{i}");
                moon.transform.SetParent(transform, false);
                moon.transform.localPosition =
                    new Vector3(Mathf.Cos(a), 0.2f * ((i % 2 == 0) ? 1f : -1f), Mathf.Sin(a)) * (body * 1.7f);
                BuildMiniCellShell(moon.transform, body * 0.28f, Definition.AccentColor);
            }

            CSDebug.Log($"[CellSelector] Toy placed at {transform.position} " +
                        "(the sphere ringed by three little worlds). Fly it for the cell matrix.");
        }

        // Each successive matrix sits FURTHER from the cell centre, so a pass through one layer
        // carries you outward toward the next instead of back through the previous one. The toy
        // faces the cell centre, so outward is -forward.
        Vector3 Outward => -transform.forward;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            // Toggle: a pass opens the shelf of worlds; another pass closes it.
            if (_grid)
            {
                ClearGrid();
                return;
            }

            BuildCellGrid();
        }

        void OnDestroy() // teardown with the toybox
        {
            CancelStream();
            if (_grid) Destroy(_grid);
            foreach (var miniature in _miniatures.Values)
                if (miniature.Mesh) Destroy(miniature.Mesh);
            _miniatures.Clear();
        }

        void ClearGrid()
        {
            CancelStream();
            if (!_grid) return;
            ToyFactory.ScaleOutAndDestroy(_grid, 0.8f).Forget();
            _grid = null;
        }

        void CancelStream()
        {
            if (_streamCts == null) return;
            _streamCts.Cancel();
            _streamCts.Dispose();
            _streamCts = null;
        }

        // ── Pass 1: the mini-cell matrix ─────────────────────────────────────

        /// <summary>
        /// The cell this toy lives in. Resolved from the TOY's position (always inside the
        /// membrane), never from a station - outward-layered stations can sit beyond it.
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

        void BuildCellGrid()
        {
            var cell = HostCell;
            if (!cell)
            {
                CSDebug.LogWarning("[CellSelector] No active Cell in the scene - nothing to select.");
                return;
            }
            if (cell.IsSwappingConfig)
            {
                // Mid-swap the cell has no settled identity, so a shelf built now would
                // mislabel which world is current (and which pass is the reset).
                CSDebug.Log("[CellSelector] A cell swap is in flight - try again once it settles.");
                return;
            }

            var configs = ResolveConfigs(cell);
            if (configs.Count == 0)
            {
                CSDebug.LogWarning($"[CellSelector] Cell {cell.ID} lists no CellConfigs - nothing to select.");
                return;
            }

            _grid = new GameObject("CellSelector_Cells");
            _grid.transform.SetParent(transform.parent, true);

            float spacing = _def.StationSpacing;
            Vector3 origin = transform.position + Outward * (spacing * _def.MatrixDistanceFactor);
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(configs.Count)));
            int rows = Mathf.CeilToInt(configs.Count / (float)cols);
            var current = cell.Config;
            var pendingModels = new List<(Transform host, CellConfigDataSO config)>();

            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                int col = i % cols;
                int row = i / cols;
                Vector3 pos = origin
                              + right * (spacing * (col - (cols - 1) * 0.5f))
                              + up * (spacing * ((rows - 1) * 0.5f - row));

                bool isCurrent = config == current;
                bool hasEnvironment = config.EnvironmentPrefab != null;

                // The label says exactly what the pass will cost you: the cell you are in
                // rebuilds (the reset), an environment-free cell is instant, everything else
                // pays a build behind the veil.
                string cost = isCurrent ? "RESET" : hasEnvironment ? "LOAD" : "INSTANT";
                string label = $"{DisplayNameOf(config)}\n<size=60%>{cost}</size>";

                var station = CreateStation(_grid.transform, pos, config, label, isCurrent);
                var capturedConfig = config;
                var capturedCell = cell;
                station.OnVesselPassed = () => SelectCell(capturedCell, capturedConfig);

                if (hasEnvironment) pendingModels.Add((station.transform, config));
            }

            // The shells are up NOW; the scale models fill them in over the next frames. Each
            // model costs one environment generation (pure math, no prisms), which is small next
            // to a real build but too big to do seven of in one frame.
            CancelStream();
            _streamCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            StreamMiniatures(pendingModels, _streamCts.Token).Forget();

            CSDebug.Log($"[CellSelector] {configs.Count} cells offered (current: {DisplayNameOf(current)}); " +
                        $"{pendingModels.Count} scale models building.");
        }

        /// <summary>
        /// The definition's explicit list when authored, else the host Cell's own rotation.
        /// Reading the Cell is the default on purpose - it is the single source of truth for
        /// what this scene's cell can be, so the toy can never drift from it.
        /// </summary>
        List<CellConfigDataSO> ResolveConfigs(Cell cell)
        {
            var result = new List<CellConfigDataSO>();
            var authored = _def ? _def.Cells : null;
            var source = authored is { Count: > 0 } ? authored : cell.AvailableConfigs;
            if (source == null) return result;

            foreach (var config in source)
                if (config && !result.Contains(config))
                    result.Add(config);
            return result;
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

            // The shelf closes first: the world it describes is about to stop being true.
            ClearGrid();

            if (cell.RequestCellSwap(config, _def.ClearLooseTrailMass))
                CSDebug.Log($"[CellSelector] → {DisplayNameOf(config)} " +
                            $"(environment: {(config.EnvironmentPrefab ? config.EnvironmentPrefab.name : "none")}, " +
                            $"clear loose trail mass: {_def.ClearLooseTrailMass}).");
        }

        // ── Stations ─────────────────────────────────────────────────────────

        ToyMatrixStation CreateStation(Transform parent, Vector3 position, CellConfigDataSO config,
            string label, bool isCurrent)
        {
            float radius = _def.StationRadius;
            var go = ToyFactory.CreateBareRoot(DisplayNameOf(config), parent, position,
                transform.position, radius * 1.6f);

            // Shell only here - the scale model of the world this config creates streams in
            // afterwards (see StreamMiniatures). A config with no authored environment has
            // nothing to model, so it stays EMPTY: the picture tells you the entry costs
            // nothing before you read the label.
            BuildMiniCellShell(go.transform, radius, Definition.AccentColor);

            var text = ToyFactory.AddLabel(go.transform, label, Definition.AccentColor, radius * 1.9f);
            if (isCurrent && text) text.fontStyle = TMPro.FontStyles.Bold;

            var station = go.AddComponent<ToyMatrixStation>();
            station.Bind(Context);
            return station;
        }

        // ── Mini-cell visual ─────────────────────────────────────────────────

        /// <summary>
        /// The empty little cell: three gyroscopic rings for the membrane (the existing
        /// fly-through-ring shape language, hollow so you can see inside) and a nucleus dot.
        /// Whatever lives inside is the config's own scale model, added separately.
        /// </summary>
        static void BuildMiniCellShell(Transform parent, float radius, Color accent)
        {
            var membrane = new GameObject("Membrane");
            membrane.transform.SetParent(parent, false);
            var ringAngles = new[] { Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0f, 90f, 0f) };
            foreach (var euler in ringAngles)
            {
                var ring = ToyFactory.AddRingBody(membrane.transform, radius, accent);
                ring.transform.localRotation = Quaternion.Euler(euler);
            }

            ToyFactory.AddSphereBody(parent, radius * 0.10f, accent).name = "Nucleus";
        }

        // ── Scale models ─────────────────────────────────────────────────────

        /// <summary>
        /// Fill each mini-cell with a scale model of the world its config creates, ONE PER FRAME.
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

            var built = CellMiniatureBuilder.Build(prefab, _def.StationRadius, _def.ModelPointBudget);
            if (prefab is CellEnvironmentSpawnableBase env) env.ReleaseGeneratedData();

            if (built.IsValid) _miniatures[config] = built;
            else CSDebug.LogWarning($"[CellSelector] {prefab.name} generated no points - " +
                                    $"{DisplayNameOf(config)} shows as an empty cell.");
            return built;
        }

        void AttachMiniature(Transform host, CellMiniatureBuilder.Miniature miniature)
        {
            var go = new GameObject("ScaleModel");
            go.transform.SetParent(host, false);

            go.AddComponent<MeshFilter>().sharedMesh = miniature.Mesh;

            // One material per domain submesh - the model wears the world's REAL domain
            // composition, in the same prism materials the world itself is built from.
            var materials = new Material[miniature.SubmeshDomains.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var domain = miniature.SubmeshDomains[i];
                var material = ToyFactory.DomainPrismMaterial(Context, domain);
                materials[i] = material
                    ? material
                    : ToyFactory.AccentMaterial(ToyFactory.DomainAccentColor(Context, domain));
            }

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // The world turns inside its still membrane - a thing you can watch.
            go.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 12f);

            // Continuity of existence: the model grows in rather than appearing. Zeroed here,
            // before the first tick, so it can never render at full size for a frame first.
            go.transform.localScale = Vector3.zero;
            BloomModelIn(go.transform, 0.6f, this.GetCancellationTokenOnDestroy()).Forget();
        }

        static async UniTaskVoid BloomModelIn(Transform target, float seconds, CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!target) return;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                target.localScale = Vector3.one * (t * t * (3f - 2f * t));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (target) target.localScale = Vector3.one;
        }

    }
}

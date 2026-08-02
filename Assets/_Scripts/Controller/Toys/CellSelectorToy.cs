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
    public sealed class CellSelectorToy : MatrixToy
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
            // "The world you are in" is told by a SHAPE, not by the word RESET: the current cell's
            // model wears a halo ring, the one piece of the toy shape vocabulary that means "this
            // one is already yours". Everything else is a plain model, and an environment-free
            // config has nothing to model, so its slot reads visibly empty (= instant).
            if (isCurrent)
                ToyFactory.AddRingBody(station.transform, radius * 1.25f, Definition.AccentColor);

            var text = ToyFactory.AddLabel(station.transform, DisplayNameOf(config),
                Definition.AccentColor, radius * 1.9f);
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

            // The world turns in place - a thing you can watch.
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

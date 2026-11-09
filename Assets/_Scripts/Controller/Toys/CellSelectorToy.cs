using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime for the <see cref="CellSelectorToyDefinitionSO"/> - the freestyle <b>world
    /// picker</b>, and the freestyle <b>reset</b>.
    ///
    /// The Toy Box lists one row per config in the containing <see cref="Cell"/>'s own rotation
    /// (the Cell owns the environment; the toy reads its list), each previewed as a SCALE MODEL
    /// (<see cref="CellMiniatureBuilder"/>). Choosing one is <see cref="Cell.RequestCellSwap"/> -
    /// the cell retires its current world in a suction and grows the chosen one back behind the standard
    /// <see cref="EnvironmentLoadVeil"/>. Choosing the cell you are already in is the reset.
    ///
    /// This is the opt-in half of <c>CellTypeChoiceOptions.EnvironmentFree</c>: freestyle boots
    /// with no authored environment (so entering Menu_Main - and returning to it from an arcade
    /// game - is fast), and the multi-second world build happens only when a player picks
    /// a world in the Toy Box.
    /// </summary>
    public sealed class CellSelectorToy : MatrixToy
    {
        CellSelectorToyDefinitionSO _def;

        // Built models, keyed by the config they portray. Generation is the expensive part, so a
        // re-picked preview is free. Instance-scoped (not static) and destroyed with the toy, so
        // the meshes cannot outlive the scene.
        readonly Dictionary<CellConfigDataSO, CellMiniatureBuilder.Miniature> _miniatures = new();

        public void Configure(CellSelectorToyDefinitionSO definition) => _def = definition;

        // ── The one declaration ──────────────────────────────────────────────

        /// <summary>
        /// The cell's own rotation, the world you are in flagged as current. The current row stays
        /// ACTIONABLE: choosing the world you are already in IS the freestyle reset.
        /// </summary>
        protected override void BuildOptions(List<ToyShellOption> into)
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
                        : config.EnvironmentPrefab ? ""
                        : Cell.IsBareCanvas(config) ? "no environment"
                        // Environment-free is how a world is BUILT, not what it CONTAINS: this
                        // one has no lay to picture because its whole world is GROWN from its
                        // spawn profile (Docs/ECOSYSTEM.md §36.10, met by a third reader).
                        : "grown, not laid",
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    IsCurrent = isCurrent,
                    Payload = capturedConfig,
                    Apply = () => SelectCell(capturedCell, capturedConfig),
                    BuildPreview = parent => BuildShellPreview(capturedConfig, parent),
                });
            }
        }

        /// <summary>
        /// The cell this toy lives in, resolved from the toy's anchor (always inside the membrane).
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

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var miniature in _miniatures.Values)
                if (miniature.Mesh) Destroy(miniature.Mesh);
            _miniatures.Clear();
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
                CSDebug.LogVerbose(CSLogChannel.ToyBox, "[CellSelector] A cell swap is already in flight - ignoring this pass.");
                return;
            }

            if (cell.RequestCellSwap(config, _def.ClearLooseTrailMass) && CSDebug.IsVerbose(CSLogChannel.ToyBox))
                CSDebug.LogVerbose(CSLogChannel.ToyBox,
                    $"[CellSelector] -> {DisplayNameOf(config)} " +
                    $"(environment: {(config.EnvironmentPrefab ? config.EnvironmentPrefab.name : "none")}, " +
                    $"clear loose trail mass: {_def.ClearLooseTrailMass}).");
        }

        // ── App-shell face ───────────────────────────────────────────────────

        /// <summary>
        /// Mid-swap the cell has no settled identity, so nothing can honestly be labelled
        /// current.
        /// </summary>
        protected override bool ShellAvailable
        {
            get
            {
                var cell = HostCell;
                return cell && !cell.IsSwappingConfig;
            }
        }

        // ── Scale models ─────────────────────────────────────────────────────

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

        /// <summary>
        /// A scale model of <paramref name="config"/> for the app shell's preview window,
        /// built at full size with no spin: the preview camera orbits it, and frames it on the
        /// frame it is handed.
        /// </summary>
        GameObject BuildShellPreview(CellConfigDataSO config, Transform parent)
        {
            if (!config || !_def || !parent) return null;

            var miniature = ResolveMiniature(config);
            if (!miniature.IsValid) return null;

            return ToyFactory.AddMiniatureBody(parent, miniature, Context, $"{config.name} Preview");
        }
    }
}

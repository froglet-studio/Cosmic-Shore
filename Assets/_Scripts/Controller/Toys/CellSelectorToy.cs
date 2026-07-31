using System.Collections.Generic;
using CosmicShore.Data;
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
    /// is a little cell: three gyroscopic rings for the membrane, a nucleus dot, and a
    /// constellation of prism shards seeded from the config's name, so every world reads
    /// distinct at a glance and an environment-free config reads visibly EMPTY.
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

        public void Configure(CellSelectorToyDefinitionSO definition) => _def = definition;

        protected override void OnInitialized()
        {
            // Findability + identity: the world picker wears three little worlds as moons.
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
                BuildMiniCell(moon.transform, body * 0.28f, Definition.AccentColor,
                    Hash01(i * 977), shardCount: 5);
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
            if (_grid) Destroy(_grid);
        }

        void ClearGrid()
        {
            if (!_grid) return;
            ToyFactory.ScaleOutAndDestroy(_grid, 0.8f).Forget();
            _grid = null;
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
            Vector3 origin = transform.position + Outward * (spacing * 1.5f);
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(configs.Count)));
            int rows = Mathf.CeilToInt(configs.Count / (float)cols);
            var current = cell.Config;

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
            }

            CSDebug.Log($"[CellSelector] {configs.Count} cells offered (current: {DisplayNameOf(current)}).");
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

            // A cell with no authored environment is drawn EMPTY - the picture tells you it
            // costs nothing to enter, before you read the label.
            int shards = config.EnvironmentPrefab != null ? _def.ShardsPerCell : 0;
            BuildMiniCell(go.transform, radius, Definition.AccentColor,
                Hash01(StableHash(DisplayNameOf(config))), shards);

            var text = ToyFactory.AddLabel(go.transform, label, Definition.AccentColor, radius * 1.9f);
            if (isCurrent && text) text.fontStyle = TMPro.FontStyles.Bold;

            var station = go.AddComponent<ToyMatrixStation>();
            station.Bind(Context);
            return station;
        }

        // ── Mini-cell visual ─────────────────────────────────────────────────

        /// <summary>
        /// A little cell: three gyroscopic rings for the membrane (the existing fly-through-ring
        /// shape language, hollow so you can see inside), a nucleus dot, and
        /// <paramref name="shardCount"/> prism shards on a phyllotaxis shell seeded by
        /// <paramref name="seed01"/>. Cells are told apart by SHAPE and CONTENT, never by tint -
        /// colour belongs to domains (the same rule the Lifeform Matrix follows for elements).
        /// </summary>
        void BuildMiniCell(Transform parent, float radius, Color accent, float seed01, int shardCount)
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
            if (shardCount <= 0) return;

            var prismMaterial = ToyFactory.DomainPrismMaterial(Context, LocalDomain);
            var contents = new GameObject("Contents");
            contents.transform.SetParent(parent, false);

            // Phyllotaxis shell - the same golden-angle placement the cell environments use,
            // offset by the seed so each config's constellation is its own and is stable
            // across sessions.
            const float goldenAngle = 2.39996323f;
            int seed = Mathf.RoundToInt(seed01 * 4096f);
            float phase = seed01 * Mathf.PI * 2f;
            float shardSize = radius * 0.16f;

            // Per-cell anisotropy on top of the rotation phase: some worlds read as flat discs,
            // some as spheres. Two cheap seeded axes are enough to tell seven worlds apart.
            float squash = Mathf.Lerp(0.30f, 1f, Hash01(seed + 7));

            for (int i = 0; i < shardCount; i++)
            {
                float t = (i + 0.5f) / shardCount;
                float y = 1f - 2f * t;
                float ringRadius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = goldenAngle * i + phase;

                // Jitter the shell radius per shard so the constellation reads as a world with
                // depth rather than a perfect sphere of dots.
                float shell = Mathf.Lerp(0.35f, 0.82f, Hash01(i * 131 + seed));
                var local = new Vector3(Mathf.Cos(theta) * ringRadius, y * squash, Mathf.Sin(theta) * ringRadius)
                            * (radius * shell);

                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shard";
                if (shard.TryGetComponent(out Collider shardCollider)) Destroy(shardCollider);
                shard.transform.SetParent(contents.transform, false);
                shard.transform.localPosition = local;
                shard.transform.localRotation = Quaternion.Euler(
                    Hash01(i * 17) * 360f, Hash01(i * 37) * 360f, Hash01(i * 57) * 360f);
                shard.transform.localScale = Vector3.one * shardSize;

                if (shard.TryGetComponent(out MeshRenderer shardRenderer))
                {
                    if (prismMaterial) shardRenderer.sharedMaterial = prismMaterial;
                    shardRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    shardRenderer.receiveShadows = false;
                }
            }

            // The contents turn inside the still membrane - a world you can watch.
            contents.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 12f);
        }

        Domains LocalDomain =>
            Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus?.Domain ?? Domains.Blue;

        /// <summary>Order-independent hash in [0,1) - stable decoration values.</summary>
        static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n;
                h = (h ^ 61u) ^ (h >> 16);
                h *= 9u;
                h ^= h >> 4;
                h *= 0x27d4eb2du;
                h ^= h >> 15;
                return (h & 0xffffffu) / (float)0x1000000;
            }
        }

        /// <summary>
        /// FNV-1a over the name. <c>string.GetHashCode</c> is explicitly not stable across
        /// runtimes, and a constellation that reshuffles between sessions is not an identity.
        /// </summary>
        static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }
}

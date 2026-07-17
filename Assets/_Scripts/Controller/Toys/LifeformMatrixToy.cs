using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime for the <see cref="LifeformMatrixToyDefinitionSO"/> - the ecology tuning bench.
    ///
    /// Pass 1 (the toy itself): builds/tears down the SPECIES matrix - one station per
    /// registered flora/fauna species. Pass 2 (a species station): blooms the VARIANT matrix -
    /// 4 element columns x level rows {1, 3, 5}, station spheres tinted the element's accent
    /// and sized by level (level 5 reads biggest). Pass 3 (a variant station): spawns that
    /// exact lifeform live into the containing cell through the canonical spawn paths
    /// (SpawnFlora / SpawnFaunaWithDomain + AssignLineage), on a runtime CLONE of the config
    /// with the station's level - the authored assets are never mutated.
    ///
    /// Collider note: stations are transient trigger spheres (species count + up to 12),
    /// Menu_Main freestyle only, torn down with the matrix - no per-cell budget impact.
    /// </summary>
    public sealed class LifeformMatrixToy : Toy
    {
        static readonly int[] TestLevels = { 1, 3, 5 };

        LifeformMatrixToyDefinitionSO _def;
        GameObject _speciesGrid;
        GameObject _variantGrid;

        public void Configure(LifeformMatrixToyDefinitionSO definition) => _def = definition;

        protected override void OnInitialized()
        {
            // Findability + identity: the tuning bench wears the four element CRYSTAL MODELS
            // as orbiting moons - elements have SHAPE signatures, not colour signatures
            // (colour belongs to domains) - and logs its spot.
            var elements = new[] { Element.Charge, Element.Mass, Element.Space, Element.Time };
            for (int i = 0; i < elements.Length; i++)
            {
                float a = i / (float)elements.Length * Mathf.PI * 2f;
                var moon = AddElementCrystalVisual(transform, elements[i], 0.35f);
                if (!moon) continue;
                moon.name = $"Moon_{elements[i]}";
                moon.transform.localPosition =
                    new Vector3(Mathf.Cos(a), 0.25f * ((i % 2 == 0) ? 1f : -1f), Mathf.Sin(a)) * 2.2f;
            }

            CSDebug.Log($"[LifeformMatrix] Toy placed at {transform.position} " +
                        $"(the sphere orbited by the four element crystals).");
        }

        /// <summary>
        /// Pure-visual clone of an element's crystal MODEL (the element's canonical in-world
        /// shape signature) - just the model subtree, no Crystal behaviour, registry entry, or
        /// collider. Scale is relative to the model's authored scale.
        /// </summary>
        static GameObject AddElementCrystalVisual(Transform parent, Element element, float scale)
        {
            var set = ElementalCrystalSetSO.Load();
            var prefab = set ? set.GetPrefab(element) : null;
            var models = prefab ? prefab.CrystalModels : null;
            var source = models is { Count: > 0 } ? models[0]?.model : null;
            if (!source) return null;

            var visual = Instantiate(source, parent, false);
            visual.SetActive(true);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = source.transform.localScale * scale;
            return visual;
        }

        // The player flies AT the matrix and keeps flying: each successive matrix sits at a
        // FURTHER radius from the cell centre, so a pass through one layer carries you toward
        // the next instead of back through the previous one. The toy faces the cell centre,
        // so outward is -forward.
        Vector3 Outward => -transform.forward;

        protected override void OnActivated(IVesselStatus localVessel)
        {
            // Toggle: a pass builds the menagerie; another pass clears everything.
            if (_speciesGrid)
            {
                ClearGrid(ref _variantGrid);
                ClearGrid(ref _speciesGrid);
                return;
            }

            BuildSpeciesGrid();
        }

        void OnDestroy() // teardown with the toybox
        {
            if (_variantGrid) Destroy(_variantGrid);
            if (_speciesGrid) Destroy(_speciesGrid);
        }

        static void ClearGrid(ref GameObject grid)
        {
            if (!grid) return;
            ToyFactory.ScaleOutAndDestroy(grid, 0.8f).Forget();
            grid = null;
        }

        // ── Pass 1: species matrix ───────────────────────────────────────────

        void BuildSpeciesGrid()
        {
            _speciesGrid = new GameObject("LifeformMatrix_Species");
            _speciesGrid.transform.SetParent(transform.parent, true);

            float spacing = _def.StationSpacing;
            // One layer OUTWARD from the toy (away from the cell centre): fly through the toy,
            // keep flying, and the species row is directly ahead.
            Vector3 origin = transform.position + Outward * (spacing * 1.5f);
            Vector3 right = transform.right;

            int index = 0;
            int total = (_def.Fauna?.Length ?? 0) + (_def.Flora?.Length ?? 0);

            if (_def.Fauna != null)
                foreach (var species in _def.Fauna)
                {
                    if (species?.ElementConfigs is not { Length: > 0 }) continue;
                    var pos = origin + right * (spacing * (index - (total - 1) * 0.5f));
                    var station = CreateStation(_speciesGrid.transform, pos, species.Name,
                        _def.StationRadius, Definition.AccentColor);
                    var captured = species;
                    station.OnVesselPassed = () => BuildVariantGrid(captured.Name, captured.ElementConfigs, null);
                    index++;
                }

            if (_def.Flora != null)
                foreach (var species in _def.Flora)
                {
                    if (species?.ElementConfigs is not { Length: > 0 }) continue;
                    var pos = origin + right * (spacing * (index - (total - 1) * 0.5f));
                    var station = CreateStation(_speciesGrid.transform, pos, species.Name,
                        _def.StationRadius, Definition.AccentColor);
                    var captured = species;
                    station.OnVesselPassed = () => BuildVariantGrid(captured.Name, null, captured.ElementConfigs);
                    index++;
                }
        }

        // ── Pass 2: variant matrix (element columns x level rows {1,3,5}) ───

        void BuildVariantGrid(string speciesName,
            FaunaConfigurationSO[] faunaConfigs, FloraConfigurationSO[] floraConfigs)
        {
            ClearGrid(ref _variantGrid);
            _variantGrid = new GameObject($"LifeformMatrix_{speciesName}");
            _variantGrid.transform.SetParent(transform.parent, true);

            float spacing = _def.StationSpacing;
            // A further layer OUTWARD than the species row: fly through a species, keep
            // flying, and its variant matrix is directly ahead at a larger radius from the
            // cell centre - each pass carries you toward the next layer, never back through
            // the previous one.
            Vector3 origin = transform.position + Outward * (spacing * 3.5f);
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            var elements = new[] { Element.Charge, Element.Mass, Element.Space, Element.Time };
            for (int col = 0; col < elements.Length; col++)
            {
                var element = elements[col];
                var faunaCfg = FindByElement(faunaConfigs, element);
                var floraCfg = FindByElement(floraConfigs, element);
                if (!faunaCfg && !floraCfg) continue; // species doesn't express this element yet

                for (int row = 0; row < TestLevels.Length; row++)
                {
                    int level = TestLevels[row];
                    Vector3 pos = origin
                                  + right * (spacing * (col - (elements.Length - 1) * 0.5f))
                                  + up * (spacing * (row - (TestLevels.Length - 1) * 0.5f));

                    // Element identity = the crystal's SHAPE; level telegraph = its SIZE
                    // (level 5 reads biggest before you touch it).
                    float radius = _def.StationRadius * (1f + 0.35f * (level - 1));
                    var station = CreateStation(_variantGrid.transform, pos,
                        $"{speciesName} · {element} {level}", radius, Definition.AccentColor,
                        bodySphere: false);
                    AddElementCrystalVisual(station.transform, element, 0.6f + 0.4f * (level - 1) * 0.5f);

                    int capturedLevel = level;
                    if (faunaCfg)
                        station.OnVesselPassed = () => SpawnFaunaVariant(faunaCfg, capturedLevel, pos);
                    else
                        station.OnVesselPassed = () => SpawnFloraVariant(floraCfg, capturedLevel, pos);
                }
            }
        }

        static T FindByElement<T>(T[] configs, Element element) where T : ScriptableObject
        {
            if (configs == null) return null;
            foreach (var cfg in configs)
            {
                if (!cfg) continue;
                switch (cfg)
                {
                    case FaunaConfigurationSO f when f.Element == element: return cfg;
                    case FloraConfigurationSO fl when fl.Element == element: return cfg;
                }
            }
            return null;
        }

        // ── Pass 3: spawn the exact variant ──────────────────────────────────

        void SpawnFaunaVariant(FaunaConfigurationSO config, int level, Vector3 position)
        {
            // Outward-layered stations can sit beyond the membrane - resolve the cell from the
            // TOY's position (always inside) and spawn the creature at the station.
            var cell = Cell.FindCellContaining(transform.position);
            if (!cell)
            {
                CSDebug.LogWarning("[LifeformMatrix] No cell contains the station - cannot spawn fauna.");
                return;
            }

            // Runtime clone so the authored asset keeps its level; the clone IS the lineage
            // config, so reproduction inherits the variant identity too.
            var clone = Instantiate(config);
            clone.name = $"{config.name} (L{level})";
            clone.InitialLevel = level;

            Domains domain = Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus?.Domain ?? cell.ControllingDomain;
            var fauna = CellLifeSpawnerBase.SpawnFaunaWithDomain(
                cell, clone.FaunaPrefab, cell.transform.position, domain, position);
            if (fauna) fauna.AssignLineage(cell, clone);
        }

        void SpawnFloraVariant(FloraConfigurationSO config, int level, Vector3 position)
        {
            var cell = Cell.FindCellContaining(transform.position);
            if (!cell)
            {
                CSDebug.LogWarning("[LifeformMatrix] No cell contains the station - cannot spawn flora.");
                return;
            }

            var clone = Instantiate(config);
            clone.name = $"{config.name} (L{level})";
            clone.InitialLevel = level;

            CellLifeSpawnerBase.SpawnFlora(cell, clone.FloraPrefab, null, clone);
        }

        // ── Stations ─────────────────────────────────────────────────────────

        LifeformMatrixStation CreateStation(Transform parent, Vector3 position, string label,
            float radius, Color accent, bool bodySphere = true)
        {
            var go = ToyFactory.CreateBareRoot(label, parent, position, transform.position, radius * 1.6f);
            if (bodySphere)
                ToyFactory.AddSphereBody(go.transform, radius, accent);
            ToyFactory.AddLabel(go.transform, label, accent, radius * 1.9f);

            var station = go.AddComponent<LifeformMatrixStation>();
            station.Bind(Context);
            return station;
        }
    }

    /// <summary>
    /// A single fly-through station of the lifeform matrix. Same local-vessel + freestyle
    /// gating as <see cref="Toy"/>, with a short per-station cooldown instead of the full
    /// exit-gated re-arm (stations sit close together; the cooldown stops one pass from
    /// double-firing while still allowing rapid A/B spawning).
    /// </summary>
    public sealed class LifeformMatrixStation : MonoBehaviour
    {
        const float CooldownSeconds = 1.5f;

        ToyContext _context;
        float _readyTime;

        /// <summary>What happens when the local vessel passes through.</summary>
        public System.Action OnVesselPassed;

        public void Bind(ToyContext context) => _context = context;

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < _readyTime) return;
            if (!Toy.TryGetLocalVessel(other, out _)) return;
            if (_context?.IsFreestyleActive != null && !_context.IsFreestyleActive()) return;

            _readyTime = Time.time + CooldownSeconds;
            OnVesselPassed?.Invoke();
        }
    }
}

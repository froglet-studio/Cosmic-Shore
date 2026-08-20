using System.Collections.Generic;
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

        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        protected override void OnInitialized()
        {
            AttachEmblem(new EmblemSource(this), 8f);
            CSDebug.Log($"[LifeformMatrix] Toy placed at {transform.position} " +
                        "(the four element crystals, orbited by its menagerie).");
        }

        /// <summary>
        /// The menagerie in one glyph: the CORE is the four element crystal MODELS clustered on a
        /// sub-ring (elements are told apart by SHAPE - all four share the emblem's one material,
        /// so nothing here can accidentally encode an element as a colour), and the SATELLITES are
        /// four real species from the bench's own lists.
        ///
        /// This replaces the four "moons" that used to hang off the root: at a 2.2-unit offset
        /// inside a 44-unit sphere they were invisible, and they were built by Instantiating a
        /// gameplay crystal prefab - dragging a live fade-in coroutine and a transparent gameplay
        /// material into an icon. The crystals keep their load-bearing home at the VARIANT
        /// stations, where element identity IS the choice.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly LifeformMatrixToy _toy;
            public EmblemSource(LifeformMatrixToy toy) => _toy = toy;

            public int SatelliteCount => 4;

            // One material for all four crystals AND the species: that is what makes "elements
            // have SHAPE signatures, never colour signatures" true by construction here.
            public bool UsesSharedMaterial => true;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = false;
                return slot == 0
                    ? BuildCrystalCore(holder, radius, shared)
                    : _toy.TryBuildSpeciesSatellite(slot - 1, holder, radius, shared);
            }

            static bool BuildCrystalCore(Transform holder, float radius, Material shared)
            {
                float ring = radius * 0.62f;
                float each = radius * 0.42f;
                bool any = false;

                for (int i = 0; i < Elements.Length; i++)
                {
                    if (!ElementCrystalModelBuilder.TryBuild(Elements[i], each, shared, out var model)) continue;
                    model.transform.SetParent(holder, false);
                    float a = i / (float)Elements.Length * Mathf.PI * 2f;
                    model.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ring;
                    any = true;
                }
                return any;
            }

            public bool TryGetLiveKey(out object key)
            {
                key = null;
                return false; // the menagerie's roster is authored, not live
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = default;
                return false;
            }
        }

        /// <summary>
        /// Satellite <paramref name="index"/> of the emblem: first and middle fauna, then first and
        /// middle flora, so the ring samples both halves of the menagerie. A short list skips its
        /// slot rather than showing the same creature twice.
        /// </summary>
        bool TryBuildSpeciesSatellite(int index, Transform holder, float radius, Material shared)
        {
            var fauna = _def ? _def.Fauna : null;
            var flora = _def ? _def.Flora : null;

            FaunaConfigurationSO[] faunaConfigs = null;
            FloraConfigurationSO[] floraConfigs = null;

            switch (index)
            {
                case 0: faunaConfigs = SpeciesConfigs(fauna, 0); break;
                case 1: faunaConfigs = SpeciesConfigs(fauna, fauna?.Length / 2 ?? 0); break;
                case 2: floraConfigs = SpeciesConfigs(flora, 0); break;
                default: floraConfigs = SpeciesConfigs(flora, flora?.Length / 2 ?? 0); break;
            }

            if (faunaConfigs == null && floraConfigs == null) return false;
            if (!AddSpeciesModel(faunaConfigs, floraConfigs, radius, out var model, shared)) return false;

            model.transform.SetParent(holder, false);
            return true;
        }

        static FaunaConfigurationSO[] SpeciesConfigs(LifeformMatrixToyDefinitionSO.FaunaSpecies[] list, int index)
        {
            if (list == null || index < 0 || index >= list.Length) return null;
            var species = list[index];
            return species?.ElementConfigs is { Length: > 0 } ? species.ElementConfigs : null;
        }

        static FloraConfigurationSO[] SpeciesConfigs(LifeformMatrixToyDefinitionSO.FloraSpecies[] list, int index)
        {
            if (list == null || index < 0 || index >= list.Length) return null;
            var species = list[index];
            return species?.ElementConfigs is { Length: > 0 } ? species.ElementConfigs : null;
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

        // Meshes this toy generated for its flora icons. Instance-scoped and freed here, so they
        // cannot outlive the scene across Menu_Main re-entries.
        readonly List<Mesh> _iconMeshes = new();

        /// <summary>
        /// The domain a species icon wears. The local player's, matching what a station would
        /// actually spawn (see SpawnFaunaVariant), with Jade as the neutral fallback.
        /// </summary>
        Domains IconDomain =>
            Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus?.Domain ?? Domains.Jade;

        void OnDestroy() // teardown with the toybox
        {
            if (_variantGrid) Destroy(_variantGrid);
            if (_speciesGrid) Destroy(_speciesGrid);

            foreach (var mesh in _iconMeshes)
                if (mesh) Destroy(mesh);
            _iconMeshes.Clear();
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
            // keep flying, and the species matrix is directly ahead - FAUNA on the lower row,
            // FLORA on the upper row (a full menagerie in one wall).
            Vector3 origin = transform.position + Outward * (spacing * 1.5f);
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            int faunaCount = 0;
            if (_def.Fauna != null)
                foreach (var species in _def.Fauna)
                {
                    if (species?.ElementConfigs is not { Length: > 0 }) continue;
                    var pos = origin - up * (spacing * 0.5f)
                              + right * (spacing * (faunaCount - (_def.Fauna.Length - 1) * 0.5f));
                    // The station IS its creature: a mini model of the species, anonymous sphere
                    // only when the prefab carries no visible geometry.
                    bool builtFauna = AddSpeciesModel(species.ElementConfigs, null,
                        _def.StationRadius, out var faunaModel);
                    var station = CreateStation(_speciesGrid.transform, pos, species.Name,
                        _def.StationRadius, Definition.AccentColor,
                        bodySphere: !builtFauna, model: faunaModel);
                    var captured = species;
                    station.OnVesselPassed = () => BuildVariantGrid(captured.Name, captured.ElementConfigs, null);
                    faunaCount++;
                }

            int floraCount = 0;
            if (_def.Flora != null)
                foreach (var species in _def.Flora)
                {
                    if (species?.ElementConfigs is not { Length: > 0 }) continue;
                    var pos = origin + up * (spacing * 0.5f)
                              + right * (spacing * (floraCount - (_def.Flora.Length - 1) * 0.5f));
                    bool builtFlora = AddSpeciesModel(null, species.ElementConfigs,
                        _def.StationRadius, out var floraModel);
                    var station = CreateStation(_speciesGrid.transform, pos, species.Name,
                        _def.StationRadius, Definition.AccentColor,
                        bodySphere: !builtFlora, model: floraModel);
                    var captured = species;
                    station.OnVesselPassed = () => BuildVariantGrid(captured.Name, null, captured.ElementConfigs);
                    floraCount++;
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
            // The matrix is the tuning BENCH: a station spawns the EXACT variant it shows, so
            // the cell's element/level spread must not re-roll it here.
            clone.SpreadElements = false;

            // Spawn INTO THE FOOD, not at the station. The variant stations are layered
            // outward and can sit hundreds of units BEYOND the membrane; a creature
            // hatched out there starts in empty space with nothing to graze, which
            // defeats the bench's whole purpose (watching the variant feed, breed and
            // fight). Hatch on the cell's densest sensed mass instead - the same target
            // the cell spawner and every forager seek - which also falls back to the
            // cell anchor when the cell is empty. Flora still plant AT their station:
            // a rooted structure is placed deliberately, a creature roams anyway.
            Vector3 anchor = cell.GetDensestRegionAnyDomain();

            // A POPULATION, not an individual - the same seed-floor count the cell spawner
            // uses, jittered around the anchor so the group disperses like a spawner wave.
            Domains domain = Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus?.Domain ?? cell.ControllingDomain;
            int count = Mathf.Max(1, clone.PopulationSize);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = anchor + Random.insideUnitSphere * (_def.StationRadius * 2.5f);
                var fauna = CellLifeSpawnerBase.SpawnFaunaWithDomain(
                    cell, clone.FaunaPrefab, anchor, domain, pos);
                if (!fauna) continue;
                fauna.AssignLineage(cell, clone);
                spawned++;
            }
            CSDebug.Log($"[LifeformMatrix] Spawned {spawned}/{count} x {clone.name} ({domain}) " +
                        $"on the cell's densest mass at {anchor} (station was at {position})");
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
            // Bench semantics - see SpawnFaunaVariant.
            clone.SpreadElements = false;

            // A POPULATION (InitialSpawnCount), rooted AT the station so the tester sees it
            // grow right where they flew - Plant() would otherwise disperse it across the cell.
            int count = Mathf.Max(1, clone.InitialSpawnCount);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = position + Random.insideUnitSphere * (_def.StationRadius * 3f);
                if (CellLifeSpawnerBase.SpawnFlora(cell, clone.FloraPrefab, null, clone, pos))
                    spawned++;
            }

            // Frenzy gate honesty: flora growth freezes cell-wide above Frenzy (the ecology's
            // one growth brake). A long-running lava-lamp cell is often AT Frenzy, so a fresh
            // spawn sits as seed prisms until mass is cleared - say so instead of looking broken.
            string growth = cell.FloraGrowingEnabled
                ? "growing (from seed prisms - watch them build)"
                : "FROZEN - cell is at Frenzy; clear prism mass (graze/joust/ability) and growth resumes";
            CSDebug.Log($"[LifeformMatrix] Spawned {spawned}/{count} x {clone.name} at {position}; growth: {growth}");
        }

        // ── Stations ─────────────────────────────────────────────────────────

        // Non-body subsystems on a lifeform prefab - the same class of thing the vessel hull filter
        // drops, so a creature icon is the creature, not its effects.
        static readonly string[] NonBodyNameHints = { "trail", "vfx", "pip", "explosion", "particle" };

        /// <summary>
        /// A species station SHOWS ITS SPECIES: a display-only model harvested from the first
        /// element config's prefab asset (never instantiated - no Fauna/Flora behaviour, no
        /// registry entry, no spawn). Returns false when the prefab carries no visible geometry
        /// (an all-prism flora, say), and the caller keeps the anonymous sphere.
        /// </summary>
        bool AddSpeciesModel(FaunaConfigurationSO[] fauna, FloraConfigurationSO[] flora,
            float radius, out GameObject model, Material shared = null)
        {
            model = null;
            Transform source = null;

            if (fauna != null)
                foreach (var cfg in fauna)
                    if (cfg && cfg.FaunaPrefab) { source = cfg.FaunaPrefab.transform; break; }

            // FLORA HAVE NO MODEL - a species is its GROWTH PATTERN. So instead of harvesting
            // meshes that aren't there (which is why these stations were anonymous spheres), ask
            // the species to run its own growth rule in the abstract and draw the result. See
            // Flora.TryPreviewGrowth / FloraIconBuilder.
            if (!source && flora != null)
            {
                foreach (var cfg in flora)
                {
                    if (!cfg || !cfg.FloraPrefab) continue;
                    if (FloraIconBuilder.TryBuild(cfg.FloraPrefab, radius, Context, IconDomain,
                            out model, out var iconMesh))
                    {
                        _iconMeshes.Add(iconMesh);   // this toy owns every mesh it builds
                        model.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 16f);
                        return true;
                    }
                    // A species whose pattern can't be previewed (the Schwarz-P walk) falls through
                    // to the mesh path, then to the sphere - never to an invented shape.
                    source = cfg.FloraPrefab.transform;
                    break;
                }
            }
            if (!source) return false;

            ToyModelBuilder.RendererFilter bodyOnly = (root, node, mesh, renderer) =>
                !ToyModelBuilder.AnyAncestorNameContains(node, root, NonBodyNameHints);

            bool built = shared
                ? ToyModelBuilder.TryBuild(source, radius, shared, out model, bodyOnly)
                : ToyModelBuilder.TryBuild(source, radius, Definition.AccentColor, out model, bodyOnly);
            if (!built) return false;

            // Turntable, like every other toy icon - a creature you can walk around before you
            // decide to release it.
            model.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 16f);
            return true;
        }

        ToyMatrixStation CreateStation(Transform parent, Vector3 position, string label,
            float radius, Color accent, bool bodySphere = true, GameObject model = null)
        {
            var go = ToyFactory.CreateBareRoot(label, parent, position, transform.position, radius * 1.6f);
            // Clamped against the bench's spacing: a level-5 variant station is 2.4x the base
            // radius, so its trigger overruns half the gap to its neighbour and an un-clamped ring
            // would interpenetrate the one beside it.
            float ringRadius = ToyFactory.StationRingRadius(radius * 1.6f, _def.StationSpacing);
            ToyFactory.AddSwitchRing(go.transform, ringRadius, accent);
            if (bodySphere)
                ToyFactory.AddSphereBody(go.transform, radius, accent);
            if (model) model.transform.SetParent(go.transform, false);
            ToyFactory.AddRingedLabel(go.transform, label, accent, ringRadius, radius);

            var station = go.AddComponent<ToyMatrixStation>();
            station.Bind(Context);
            return station;
        }
    }
}

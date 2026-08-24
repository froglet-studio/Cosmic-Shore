using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime for the <see cref="LifeformMatrixToyDefinitionSO"/> - the bench for everything you
    /// can RELEASE into the cell. Four passes deep at most, each one a layer further OUT:
    ///
    /// <list type="number">
    /// <item>the toy itself blooms the <b>KINGDOM</b> row - Fauna, Flora, Vessels;</item>
    /// <item>Fauna/Flora bloom that kingdom's <b>SPECIES</b> row (one station per registered
    /// species); Vessels blooms the <b>HANGAR</b> row instead (one mini hull per class);</item>
    /// <item>a species blooms its <b>VARIANT</b> matrix - 4 element columns x level rows
    /// {1, 3, 5}, station spheres sized by level so level 5 reads biggest;</item>
    /// <item>a variant spawns that exact lifeform live into the containing cell through the
    /// canonical spawn paths (SpawnFlora / SpawnFaunaWithDomain + AssignLineage), on a runtime
    /// CLONE of the config with the station's level - the authored assets are never mutated.</item>
    /// </list>
    ///
    /// A hangar station releases an <b>AI-piloted vessel of that class in the player's own
    /// domain</b>, through <see cref="MenuServerPlayerVesselInitializer.RequestSpawnAiCompanion"/>
    /// - the menu's ordinary networked spawn pipeline, so the bot exists once on the server and
    /// replicates to the whole party. The mini hulls and the roster are shared with the vessel
    /// changer (<see cref="ToyVesselRoster"/>): one curated list, one hull builder.
    ///
    /// The KINGDOM layer exists because the flat menagerie had outgrown one wall - 14 species on
    /// two rows, with nowhere to put a third kind of thing. Splitting by kingdom first gives each
    /// branch its own row AND makes "what else can I release?" a question the toy answers by shape.
    ///
    /// Collider note: stations are transient trigger spheres (3 kingdoms + at most one species row
    /// + up to 12 variants), Menu_Main freestyle only, torn down with the matrix - no per-cell
    /// budget impact. A released COMPANION is a real vessel and does count, exactly like the
    /// player's own; it is despawned with every other AI on the way out of the menu
    /// (SceneLoader.ClearPlayerVesselReferences).
    /// </summary>
    public sealed class LifeformMatrixToy : Toy
    {
        static readonly int[] TestLevels = { 1, 3, 5 };

        /// <summary>The three things this toy can release. Order is the kingdom row, left to right.</summary>
        enum Kingdom { Fauna = 0, Flora = 1, Vessels = 2 }

        static readonly Kingdom[] Kingdoms = { Kingdom.Fauna, Kingdom.Flora, Kingdom.Vessels };

        LifeformMatrixToyDefinitionSO _def;

        // One grid per layer. Opening a layer clears every layer BELOW it, so the matrix is always
        // a single path from the toy outward rather than an accumulating pile of walls.
        GameObject _kingdomGrid;
        GameObject _branchGrid;     // a kingdom's species row, or the hangar row
        GameObject _variantGrid;

        // The hangar's offer, resolved from the definition (or the shared curated default).
        readonly List<VesselClassType> _offeredVessels = new();

        // Every mini HULL currently on screen (the Vessels kingdom station and the hangar row).
        // They wear the player's domain colour, so a domain change has to reach them - see Update.
        readonly List<Transform> _hullBodies = new();
        Domains _lastHullDomain;
        bool _hasHullDomain;

        public void Configure(LifeformMatrixToyDefinitionSO definition) => _def = definition;

        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        protected override void OnInitialized()
        {
            AttachEmblem(new EmblemSource(this), 8f);
            CSDebug.Log($"[LifeformMatrix] Toy placed at {transform.position} " +
                        "(the four element crystals, orbited by its three kingdoms).");
        }

        /// <summary>
        /// The bench in one glyph: the CORE is the four element crystal MODELS clustered on a
        /// sub-ring (elements are told apart by SHAPE - all four share the emblem's one material,
        /// so nothing here can accidentally encode an element as a colour), and the SATELLITES are
        /// its three KINGDOMS - a real creature, a real plant, a real hull, in the same order as
        /// the row a pass opens. "You are the elements; these are the three things you can let go."
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            readonly LifeformMatrixToy _toy;
            public EmblemSource(LifeformMatrixToy toy) => _toy = toy;

            public int SatelliteCount => Kingdoms.Length;

            // One material for all four crystals AND the kingdom samples: that is what makes
            // "elements have SHAPE signatures, never colour signatures" true by construction here.
            public bool UsesSharedMaterial => true;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = false;
                return slot == 0
                    ? BuildCrystalCore(holder, radius, shared)
                    : _toy.TryBuildKingdomSatellite(Kingdoms[slot - 1], holder, radius, shared);
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
                return false; // the three kingdoms never change - nothing to rebuild against
            }

            public bool TryGetLiveTint(out Color tint)
            {
                tint = default;
                // Deliberately dead, even though the hangar's STATIONS re-tint on a domain change:
                // one shared material paints the four element crystals too, and tinting it would
                // make an element read as a colour. The emblem stays the toy's own accent.
                return false;
            }
        }

        /// <summary>
        /// One emblem satellite per kingdom, each built from that kingdom's OWN content: the first
        /// fauna species, the first flora species, the first hull on the hangar's roster. A kingdom
        /// with nothing registered leaves its slot empty rather than borrowing another's shape.
        /// </summary>
        bool TryBuildKingdomSatellite(Kingdom kingdom, Transform holder, float radius, Material shared)
        {
            GameObject model = null;
            switch (kingdom)
            {
                case Kingdom.Fauna:
                {
                    var fauna = ValidFauna();
                    if (fauna.Count == 0) return false;
                    if (!AddSpeciesModel(fauna[0].ElementConfigs, null, radius, out model, shared))
                        return false;
                    break;
                }
                case Kingdom.Flora:
                {
                    var flora = ValidFlora();
                    if (flora.Count == 0) return false;
                    if (!AddSpeciesModel(null, flora[0].ElementConfigs, radius, out model, shared))
                        return false;
                    break;
                }
                default:
                    ResolveVesselOffer();
                    if (_offeredVessels.Count == 0) return false;
                    // Built UNPARENTED first: the model builder fits by world bounds and assumes an
                    // origin-anchored, unrotated, unit-scale root.
                    if (!ToyVesselRoster.TryBuildHull(Context, _offeredVessels[0], radius, shared, out model))
                        return false;
                    break;
            }

            if (!model) return false;
            model.transform.SetParent(holder, false);
            return true;
        }

        // ── What the bench can actually show ─────────────────────────────────
        //
        // A species entry with no element configs has nothing to build a station from, so it is
        // dropped ONCE here rather than tested at each of the three places that ask (does this
        // kingdom have anything? what does its icon look like? what goes in its row?). Filtering
        // before laying out is also what keeps a row dense - a skipped entry must not leave a hole
        // the player flies through and nothing happens.

        List<LifeformMatrixToyDefinitionSO.FaunaSpecies> ValidFauna()
        {
            var valid = new List<LifeformMatrixToyDefinitionSO.FaunaSpecies>();
            if (_def && _def.Fauna != null)
                foreach (var entry in _def.Fauna)
                    if (entry?.ElementConfigs is { Length: > 0 }) valid.Add(entry);
            return valid;
        }

        List<LifeformMatrixToyDefinitionSO.FloraSpecies> ValidFlora()
        {
            var valid = new List<LifeformMatrixToyDefinitionSO.FloraSpecies>();
            if (_def && _def.Flora != null)
                foreach (var entry in _def.Flora)
                    if (entry?.ElementConfigs is { Length: > 0 }) valid.Add(entry);
            return valid;
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

        // ── Layering ─────────────────────────────────────────────────────────

        // The player flies AT the matrix and keeps flying: each successive matrix sits at a
        // FURTHER radius from the cell centre, so a pass through one layer carries you toward
        // the next instead of back through the previous one. The toy faces the cell centre,
        // so outward is -forward.
        Vector3 Outward => -transform.forward;

        /// <summary>
        /// Where layer <paramref name="layer"/> sits, in spacings out from the toy: 1.5, 3.5,
        /// 5.5 … The rhythm (one clear gap of two spacings per layer) is what the player learns,
        /// so a new layer extends it rather than re-tuning the ones before it.
        /// </summary>
        Vector3 LayerOrigin(int layer) =>
            transform.position + Outward * (_def.StationSpacing * (1.5f + 2f * layer));

        /// <summary>Station <paramref name="index"/> of a <paramref name="cols"/>x<paramref name="rows"/>
        /// grid centred on <paramref name="origin"/>, laid out in the toy's own right x up plane.</summary>
        Vector3 GridPosition(Vector3 origin, int index, int cols, int rows)
        {
            float spacing = _def.StationSpacing;
            int col = index % cols;
            int row = index / cols;
            return origin
                   + transform.right * (spacing * (col - (cols - 1) * 0.5f))
                   + transform.up * (spacing * ((rows - 1) * 0.5f - row));
        }

        protected override void OnActivated(IVesselStatus localVessel)
        {
            // Toggle: a pass builds the kingdom row; another pass clears the whole path.
            if (_kingdomGrid)
            {
                ClearGrid(ref _variantGrid);
                ClearGrid(ref _branchGrid);
                ClearGrid(ref _kingdomGrid);
                return;
            }

            BuildKingdomGrid();
        }

        // Meshes this toy generated for its flora icons. Instance-scoped and freed here, so they
        // cannot outlive the scene across Menu_Main re-entries.
        readonly List<Mesh> _iconMeshes = new();

        /// <summary>
        /// The domain a station's icon wears. The local player's, matching what a pass would
        /// actually release (fauna in your colour, a companion on your side), Jade as the
        /// neutral fallback.
        /// </summary>
        Domains IconDomain => ToyVesselRoster.PlayerDomain(Context);

        void OnDestroy() // teardown with the toybox
        {
            if (_variantGrid) Destroy(_variantGrid);
            if (_branchGrid) Destroy(_branchGrid);
            if (_kingdomGrid) Destroy(_kingdomGrid);

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

        GameObject NewGrid(string label)
        {
            // Any hull that belonged to a grid we just released is gone; drop the dead references
            // here rather than letting them accumulate until the next domain change sweeps them.
            for (int i = _hullBodies.Count - 1; i >= 0; i--)
                if (!_hullBodies[i]) _hullBodies.RemoveAt(i);

            var go = new GameObject($"LifeformMatrix_{label}");
            // Sibling of the toy (the toybox root), not a child: a grid must not inherit the toy's
            // own bloom scaling, and it is released independently.
            go.transform.SetParent(transform.parent, true);
            return go;
        }

        /// <summary>
        /// Re-tint every mini hull the instant the player's domain changes (through the
        /// domain-changer toy or anywhere else). They are built once when their grid opens, so
        /// without this a hull keeps the colour it was born with - and here that colour is a
        /// CLAIM: it says which side the companion you release will fly for.
        /// </summary>
        protected override void Update()
        {
            base.Update();
            if (_hullBodies.Count == 0) return;

            Domains domain = IconDomain;
            if (_hasHullDomain && domain == _lastHullDomain) return;
            _hasHullDomain = true;
            _lastHullDomain = domain;

            Color color = ToyVesselRoster.PreviewColor(Context, Definition.AccentColor);
            for (int i = _hullBodies.Count - 1; i >= 0; i--)
            {
                if (!_hullBodies[i]) { _hullBodies.RemoveAt(i); continue; }
                ToyVesselRoster.Recolor(_hullBodies[i], color);
            }
        }

        // ── Pass 1: the kingdom row ──────────────────────────────────────────

        void BuildKingdomGrid()
        {
            // Only kingdoms that have something to offer take a slot, and the row is laid out
            // over the survivors - an empty roster leaves no hole for the player to fly through.
            var offered = new List<Kingdom>(Kingdoms.Length);
            foreach (var kingdom in Kingdoms)
                if (HasContent(kingdom)) offered.Add(kingdom);

            if (offered.Count == 0)
            {
                CSDebug.LogWarning("[LifeformMatrix] Nothing registered in any kingdom - matrix not opened.");
                return;
            }

            _kingdomGrid = NewGrid("Kingdoms");

            Vector3 origin = LayerOrigin(0);
            // A kingdom station reads as a top-level choice: half again the radius of the species
            // stations behind it, so the row you meet first is the biggest thing in the corridor.
            float radius = _def.StationRadius * 1.5f;

            for (int i = 0; i < offered.Count; i++)
                BuildKingdomStation(offered[i], GridPosition(origin, i, offered.Count, 1), radius);
        }

        bool HasContent(Kingdom kingdom)
        {
            switch (kingdom)
            {
                case Kingdom.Fauna: return ValidFauna().Count > 0;
                case Kingdom.Flora: return ValidFlora().Count > 0;
                default:
                    ResolveVesselOffer();
                    return _offeredVessels.Count > 0;
            }
        }

        void BuildKingdomStation(Kingdom kingdom, Vector3 position, float radius)
        {
            GameObject icon;
            Color accent = Definition.AccentColor;
            System.Action onPassed;

            switch (kingdom)
            {
                case Kingdom.Fauna:
                    // AddSpeciesModel gives its own model the turntable; only the hull below needs one.
                    AddSpeciesModel(ValidFauna()[0].ElementConfigs, null, radius, out icon);
                    onPassed = BuildFaunaSpeciesGrid;
                    break;

                case Kingdom.Flora:
                    AddSpeciesModel(null, ValidFlora()[0].ElementConfigs, radius, out icon);
                    onPassed = BuildFloraSpeciesGrid;
                    break;

                default:
                    // The hangar wears YOUR domain colour, because that is what a pass means here:
                    // a pilot on your side. Domain is the one thing this toy is allowed to say
                    // with colour.
                    accent = ToyVesselRoster.PreviewColor(Context, Definition.AccentColor);
                    if (ToyVesselRoster.TryBuildHull(Context, _offeredVessels[0], radius, accent, out icon))
                    {
                        icon.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 16f);
                        _hullBodies.Add(icon.transform);
                    }
                    onPassed = BuildHangarGrid;
                    break;
            }

            var station = CreateStation(_kingdomGrid.transform, position, kingdom.ToString(),
                radius, accent, bodySphere: !icon, model: icon);
            station.OnVesselPassed = onPassed;
        }

        // ── Pass 2a: a kingdom's species row ─────────────────────────────────

        void BuildFaunaSpeciesGrid()
        {
            ClearGrid(ref _variantGrid);
            ClearGrid(ref _branchGrid);
            _branchGrid = NewGrid("Fauna");

            var species = ValidFauna();
            Vector3 origin = LayerOrigin(1);
            for (int i = 0; i < species.Count; i++)
            {
                var entry = species[i];
                Vector3 pos = GridPosition(origin, i, species.Count, 1);
                // The station IS its creature: a mini model of the species, anonymous sphere
                // only when the prefab carries no visible geometry.
                bool built = AddSpeciesModel(entry.ElementConfigs, null, _def.StationRadius, out var model);
                var station = CreateStation(_branchGrid.transform, pos, entry.Name,
                    _def.StationRadius, Definition.AccentColor, bodySphere: !built, model: model);
                var captured = entry;
                station.OnVesselPassed = () => BuildVariantGrid(captured.Name, captured.ElementConfigs, null);
            }
        }

        void BuildFloraSpeciesGrid()
        {
            ClearGrid(ref _variantGrid);
            ClearGrid(ref _branchGrid);
            _branchGrid = NewGrid("Flora");

            var species = ValidFlora();
            Vector3 origin = LayerOrigin(1);
            for (int i = 0; i < species.Count; i++)
            {
                var entry = species[i];
                Vector3 pos = GridPosition(origin, i, species.Count, 1);
                bool built = AddSpeciesModel(null, entry.ElementConfigs, _def.StationRadius, out var model);
                var station = CreateStation(_branchGrid.transform, pos, entry.Name,
                    _def.StationRadius, Definition.AccentColor, bodySphere: !built, model: model);
                var captured = entry;
                station.OnVesselPassed = () => BuildVariantGrid(captured.Name, null, captured.ElementConfigs);
            }
        }

        // ── Pass 2b: the hangar row ──────────────────────────────────────────

        void ResolveVesselOffer() =>
            ToyVesselRoster.Resolve(_def ? _def.VesselRoster : null, _offeredVessels);

        /// <summary>
        /// The hangar: one mini hull per class, in the player's own domain colour. Unlike the
        /// vessel changer's matrix this excludes NOTHING - you release a companion, you do not
        /// become it, so "the one you are flying" is a perfectly good thing to ask for a wingman in.
        /// </summary>
        void BuildHangarGrid()
        {
            ClearGrid(ref _variantGrid);
            ClearGrid(ref _branchGrid);
            ResolveVesselOffer();

            if (_offeredVessels.Count == 0)
            {
                CSDebug.LogWarning("[LifeformMatrix] Vessel roster is empty - hangar not opened.");
                return;
            }

            _branchGrid = NewGrid("Hangar");

            Vector3 origin = LayerOrigin(1);
            float radius = _def.StationRadius;
            Color color = ToyVesselRoster.PreviewColor(Context, Definition.AccentColor);

            for (int i = 0; i < _offeredVessels.Count; i++)
            {
                var vessel = _offeredVessels[i];
                Vector3 pos = GridPosition(origin, i, _offeredVessels.Count, 1);

                bool built = ToyVesselRoster.TryBuildHull(Context, vessel, radius, color, out var model);
                if (built)
                {
                    model.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 16f);
                    _hullBodies.Add(model.transform);
                }

                var station = CreateStation(_branchGrid.transform, pos, vessel.ToString(),
                    radius, color, bodySphere: !built, model: model);
                var captured = vessel;
                station.OnVesselPassed = () => ReleaseCompanion(captured, pos);
            }
        }

        // ── Pass 3: variant matrix (element columns x level rows {1,3,5}) ───

        void BuildVariantGrid(string speciesName,
            FaunaConfigurationSO[] faunaConfigs, FloraConfigurationSO[] floraConfigs)
        {
            ClearGrid(ref _variantGrid);
            _variantGrid = NewGrid(speciesName);

            float spacing = _def.StationSpacing;
            Vector3 origin = LayerOrigin(2);
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            for (int col = 0; col < Elements.Length; col++)
            {
                var element = Elements[col];
                var faunaCfg = FindByElement(faunaConfigs, element);
                var floraCfg = FindByElement(floraConfigs, element);
                if (!faunaCfg && !floraCfg) continue; // species doesn't express this element yet

                for (int row = 0; row < TestLevels.Length; row++)
                {
                    int level = TestLevels[row];
                    Vector3 pos = origin
                                  + right * (spacing * (col - (Elements.Length - 1) * 0.5f))
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

        // ── Pass 4: release ──────────────────────────────────────────────────

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

        /// <summary>
        /// Release an AI-piloted vessel of <paramref name="vessel"/> in the LOCAL PLAYER'S domain.
        /// Routed through the menu's networked spawn pipeline, so the companion is an ordinary
        /// server-owned AI player: it replicates to the whole party, it flies the lava lamp on
        /// autopilot, and it is despawned with every other AI when the menu is left.
        ///
        /// It lands one spacing back toward the cell CENTRE from the station, facing in. The player
        /// is still flying OUTWARD through the matrix when it appears, so the two are moving apart
        /// - a bot materialising on the nose would be a vessel-vs-vessel impact, not a release.
        /// </summary>
        void ReleaseCompanion(VesselClassType vessel, Vector3 stationPosition)
        {
            var init = Context?.VesselInitializer;
            if (!init)
            {
                CSDebug.LogWarning("[LifeformMatrix] No menu vessel initializer - cannot release a companion.");
                return;
            }

            // The toy faces the cell centre, so +forward is "inward" for both the offset and the
            // heading. No cell lookup needed, and it stays correct if the toy is ever re-placed.
            Vector3 inward = transform.forward;
            Vector3 position = stationPosition + inward * _def.StationSpacing;
            var pose = new Pose(position, Quaternion.LookRotation(inward, transform.up));

            Domains domain = ToyVesselRoster.PlayerDomain(Context);
            init.RequestSpawnAiCompanion(vessel, domain, pose);
            CSDebug.Log($"[LifeformMatrix] Released a {vessel} companion ({domain}) at {position}.");
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

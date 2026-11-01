using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runtime for the <see cref="SpawnMatrixToyDefinitionSO"/> - the bench for everything you
    /// can RELEASE into the cell, in the Toy Box. Three layers deep at most:
    ///
    /// <list type="number">
    /// <item>the <b>KINGDOM</b> - Fauna, Flora, Vessels;</item>
    /// <item>Fauna/Flora list that kingdom's <b>SPECIES</b>; Vessels lists the <b>HANGAR</b>
    /// (one hull per class);</item>
    /// <item>a species lists its <b>VARIANTS</b> - one per ELEMENT, and that is the whole bench:
    /// a lifeform is its species and its element, and nothing else (Docs/ECOSYSTEM.md §40). Spawn
    /// releases that exact lifeform live into the containing cell through the cell spawner's own
    /// placement calls (PlantFlora / SpawnFaunaBanded), so it lands where the species lives; on a
    /// runtime CLONE of the config - the authored assets are never mutated.</item>
    /// </list>
    ///
    /// A hangar row releases an <b>AI-piloted vessel of that class in the player's own
    /// domain</b>, through <see cref="MenuServerPlayerVesselInitializer.RequestSpawnAiCompanion"/>
    /// - the menu's ordinary networked spawn pipeline, so the bot exists once on the server and
    /// replicates to the whole party. It is despawned with every other AI on the way out of the
    /// menu (SceneLoader.ClearPlayerVesselReferences).
    /// </summary>
    public sealed class SpawnMatrixToy : Toy, IToyShellSurface
    {
        /// <summary>The three things this toy can release. Order is the kingdom row, left to right.</summary>
        enum Kingdom { Fauna = 0, Flora = 1, Vessels = 2 }

        static readonly Kingdom[] Kingdoms = { Kingdom.Fauna, Kingdom.Flora, Kingdom.Vessels };

        SpawnMatrixToyDefinitionSO _def;

        // The hangar's offer, resolved from the definition (or the shared curated default).
        readonly List<VesselClassType> _offeredVessels = new();

        public void Configure(SpawnMatrixToyDefinitionSO definition) => _def = definition;

        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // ── What the bench can actually show ─────────────────────────────────
        //
        // A species entry with no element configs has nothing to release, so it is dropped ONCE
        // (ValidFauna / ValidFlora) rather than tested at each place that asks.

        // ── App-shell face ───────────────────────────────────────────────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        bool IToyShellSurface.ShellAvailable => _def;

        /// <summary>
        /// The bench's KINGDOMS, each expanding into the next layer (kingdom, then species or hull,
        /// then element). Only a kingdom with something to offer is listed (<see cref="HasContent"/>).
        /// Releasing works from the menu exactly as in flight, so none of these asks for freestyle.
        /// </summary>
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into)
        {
            foreach (var kingdom in Kingdoms)
            {
                if (!HasContent(kingdom)) continue;
                var captured = kingdom;

                into.Add(new ToyShellOption
                {
                    Label = KingdomLabel(kingdom),
                    Detail = KingdomDetail(kingdom),
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    Payload = captured,
                    Expand = () => BuildShellKingdom(captured),
                });
            }
        }

        static string KingdomLabel(Kingdom kingdom) => kingdom switch
        {
            Kingdom.Fauna => "Fauna",
            Kingdom.Flora => "Flora",
            _ => "Vessels",
        };

        static string KingdomDetail(Kingdom kingdom) => kingdom switch
        {
            Kingdom.Fauna => "release a creature into this cell",
            Kingdom.Flora => "plant a lifeform in this cell",
            _ => "release an AI wingman in your own domain",
        };

        List<ToyShellOption> BuildShellKingdom(Kingdom kingdom) => kingdom switch
        {
            Kingdom.Fauna => BuildShellFaunaSpecies(),
            Kingdom.Flora => BuildShellFloraSpecies(),
            _ => BuildShellHangar(),
        };

        /// <summary>
        /// The hangar row, flat: one hull per class, released as an AI companion in the player's
        /// own domain.
        /// </summary>
        List<ToyShellOption> BuildShellHangar()
        {
            var options = new List<ToyShellOption>();
            ResolveVesselOffer();

            foreach (var vessel in _offeredVessels)
            {
                var captured = vessel;
                options.Add(new ToyShellOption
                {
                    Label = captured.ToString(),
                    Accent = ToyVesselRoster.PreviewColor(Context, Definition ? Definition.AccentColor : Color.white),
                    Payload = captured,
                    // A wingman is RELEASED, not become - the same verb a lifeform takes, and
                    // deliberately not the vessel changer's "Switch".
                    CommitVerb = "Spawn",
                    Apply = () => ReleaseCompanion(captured, ShellReleasePoint),
                    BuildPreview = parent => BuildShellHullPreview(captured, parent),
                    // No WatchAfterApply, deliberately: ReleaseCompanion is a REQUEST (a ServerRpc
                    // on a party client), so there is no object to turn the picture onto and
                    // pretending there is would be the lie that method's own comment warns about.
                    // The window keeps its picture on the hull that was asked for.
                });
            }
            return options;
        }

        /// <summary>A mini hull for the window's picture - the flat builder, not the live one: a
        /// preview sits close to the camera, inside the vision band's near cutoff, where a real
        /// hull reads as a black blob (ToyVesselRoster.TryBuildLiveHull says so).</summary>
        GameObject BuildShellHullPreview(VesselClassType vessel, Transform parent)
        {
            if (!_def || !parent) return null;
            Color tint = ToyVesselRoster.PreviewColor(Context, Definition ? Definition.AccentColor : Color.white);
            if (!ToyVesselRoster.TryBuildHull(Context, vessel, _def.StationRadius, tint, out var model))
                return null;
            model.transform.SetParent(parent, false);
            if (model.TryGetComponent(out ToyIdleSpin spin)) Destroy(spin);
            return model;
        }

        const string FrozenNotice =
            "Growth frozen: this cell holds so much prism mass that it is at Frenzy, so new plants " +
            "stay seed prisms. Clear mass (graze, joust, abilities) or start a fresh Barren cell.";

        // The last lifeform a shell press released, so the window can turn its picture onto it.
        Transform _lastShellRelease;

        static ElementalBarsConfigSO _bars;

        static Sprite PetalFor(Element element)
        {
            if (!_bars) _bars = Resources.Load<ElementalBarsConfigSO>(ElementalBarsConfigSO.ResourcePath);
            return _bars ? _bars.GetPetalSprite(element) : null;
        }

        List<ToyShellOption> BuildShellFaunaSpecies()
        {
            var options = new List<ToyShellOption>();
            foreach (var entry in ValidFauna())
            {
                var captured = entry;
                options.Add(new ToyShellOption
                {
                    Label = captured.Name,
                    Description = _def.DescriptionOf(captured.Name),
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    Expand = () => BuildShellVariants(captured.Name, captured.ElementConfigs, null),
                    BuildPreview = parent => BuildShellSpeciesPreview(captured.ElementConfigs, null, null, parent),
                });
            }
            return options;
        }

        List<ToyShellOption> BuildShellFloraSpecies()
        {
            var options = new List<ToyShellOption>();
            foreach (var entry in ValidFlora())
            {
                var captured = entry;
                options.Add(new ToyShellOption
                {
                    Label = captured.Name,
                    Description = _def.DescriptionOf(captured.Name),
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    Expand = () => BuildShellVariants(captured.Name, null, captured.ElementConfigs),
                    BuildPreview = parent => BuildShellSpeciesPreview(null, captured.ElementConfigs, null, parent),
                });
            }
            return options;
        }

        /// <summary>
        /// A species' element row - one option per element the species actually expresses, which
        /// is the whole matrix (a lifeform is its species and its element and nothing else).
        /// </summary>
        List<ToyShellOption> BuildShellVariants(string species, FaunaConfigurationSO[] faunaConfigs,
            FloraConfigurationSO[] floraConfigs)
        {
            var options = new List<ToyShellOption>();

            // Frenzy freezes flora growth cell-wide (the ecology's one growth brake), and a Barren
            // cell has no grazers to bring it back down, so a flora row released into a frozen cell
            // sits as seed prisms for good. The rows say so - the layer is re-asked after every
            // press, so the notice appears on the press that crosses the line.
            bool floraFrozen = floraConfigs != null &&
                               Cell.FindCellContaining(transform.position) is { } host &&
                               !host.FloraGrowingEnabled;
            string frozenDescription = floraFrozen
                ? (_def ? _def.DescriptionOf(species) + "\n\n" : "") + FrozenNotice
                : "";

            foreach (var element in Elements)
            {
                var faunaCfg = FindByElement(faunaConfigs, element);
                var floraCfg = FindByElement(floraConfigs, element);
                if (!faunaCfg && !floraCfg) continue; // species doesn't express this element yet

                var capturedFauna = faunaCfg;
                var capturedFlora = floraCfg;

                var capturedElement = element;
                System.Action release;
                if (capturedFauna) release = () => _lastShellRelease = SpawnFaunaVariant(capturedFauna);
                else               release = () => _lastShellRelease = SpawnFloraVariant(capturedFlora);

                options.Add(new ToyShellOption
                {
                    Label = element.ToString(),
                    // The element's own petal - the art the vessel HUD and the arcade card draw.
                    Icon = PetalFor(element),
                    Detail = capturedFlora && floraFrozen ? "Growth frozen" : "",
                    // Empty unless frozen: the layer then shows the species row's own description.
                    Description = capturedFlora ? frozenDescription : "",
                    Accent = Definition ? Definition.AccentColor : Color.white,
                    // A lifeform is SPAWNED: it is released into the cell and lives there on its
                    // own terms, so "Switch" would name a thing this press does not do.
                    CommitVerb = "Spawn",
                    Apply = release,
                    BuildPreview = parent => BuildShellSpeciesPreview(
                        capturedFauna ? new[] { capturedFauna } : null,
                        capturedFlora ? new[] { capturedFlora } : null,
                        capturedElement, parent),
                    // The picture turns onto what was released, so the player sees the creature
                    // bloom in where it landed rather than being told it did. A release now lands
                    // where the species lives (its band, its grove, open water), often far from
                    // the toy, so the picture follows the first one released and frames it.
                    WatchAfterApply = () => _lastShellRelease,
                    WatchRadius = _def ? _def.StationRadius * 3f : 0f,
                    // Spawn stays armed: each press releases more of this variant, into the
                    // population it already has when that can take them (see LineageFor).
                    Repeatable = true,
                    // Where this variant LIVES once released - Navigate takes the player there
                    // rather than back to the bench.
                    WorldAnchor = () => ReleaseAnchor(capturedFauna ? capturedFauna : capturedFlora),
                    WorldAnchorRadius = _def ? _def.StationRadius * 3f : 0f,
                    Payload = capturedFauna ? capturedFauna : capturedFlora,
                });
            }

            return options;
        }

        /// <summary>
        /// A species (or one element of it) for the window's picture, with that element's crystal
        /// seated at its heart size when an element is named. No turntable: the preview camera
        /// already orbits.
        /// </summary>
        GameObject BuildShellSpeciesPreview(FaunaConfigurationSO[] fauna, FloraConfigurationSO[] flora,
            Element? element, Transform parent)
        {
            if (!_def || !parent) return null;
            if (!AddSpeciesModel(fauna, flora, _def.StationRadius, out var model) || !model) return null;

            model.transform.SetParent(parent, false);

            // Every heart is one size, so the crystal is drawn at its model's own scale.
            if (element.HasValue)
                AddElementCrystalVisual(model.transform, element.Value, 1f);
            return model;
        }

        /// <summary>
        /// Where a companion released from the Toy Box lands: out along the toy's outward radial,
        /// clear of the bench. (Lifeforms land where their species lives.)
        /// </summary>
        Vector3 ShellReleasePoint => transform.position - transform.forward * (_def.StationSpacing * 3.5f);

        List<SpawnMatrixToyDefinitionSO.FaunaSpecies> ValidFauna()
        {
            var valid = new List<SpawnMatrixToyDefinitionSO.FaunaSpecies>();
            if (_def && _def.Fauna != null)
                foreach (var entry in _def.Fauna)
                    if (entry?.ElementConfigs is { Length: > 0 }) valid.Add(entry);
            return valid;
        }

        List<SpawnMatrixToyDefinitionSO.FloraSpecies> ValidFlora()
        {
            var valid = new List<SpawnMatrixToyDefinitionSO.FloraSpecies>();
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

        // Meshes this toy generated for its flora icons. Instance-scoped and freed here, so they
        // cannot outlive the scene across Menu_Main re-entries.
        readonly List<Mesh> _iconMeshes = new();

        /// <summary>
        /// The domain a preview wears. The local player's, matching what a Spawn would
        /// actually release (fauna in your colour, a companion on your side), Jade as the
        /// neutral fallback.
        /// </summary>
        Domains IconDomain => ToyVesselRoster.PlayerDomain(Context);

        protected override void OnDestroy() // teardown with the toybox
        {
            base.OnDestroy();

            foreach (var mesh in _iconMeshes)
                if (mesh) Destroy(mesh);
            _iconMeshes.Clear();
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

        void ResolveVesselOffer() =>
            ToyVesselRoster.ResolveOffered(Context, _def ? _def.VesselRoster : null, _offeredVessels);

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

        // ── Release ──────────────────────────────────────────────────────────

        // How far an UNBANDED population is jittered around its one feeding ground - the cell
        // spawner's own wave jitter (RandomLifeSpawner.FaunaSpawnJitter), so a bench release
        // arrives as the same loose group a spawner wave does.
        const float UnbandedReleaseJitter = 150f;

        /// <summary>
        /// Release a population of this exact variant where the species LIVES, not at the toy.
        ///
        /// <para>Placement is the cell spawner's own (<see cref="CellLifeSpawnerBase.SpawnFaunaBanded"/>):
        /// a BANDED species (the swarms, the substrate species, the builders) is scattered one
        /// point per creature through its own band and inside the cell's pens - the room the
        /// Swarm cell would have hatched it in. An unbanded species (tadpole, shark, piranha …)
        /// hatches as a group on the cell's densest mass, which is what it would forage; in an
        /// EMPTY cell (the Barren cell, where single species are inspected) there is no mass,
        /// and the old fallback - the crystal / the cell centre - is where every release piled
        /// up, so it hatches at a fresh random point in the cell's open water instead.</para>
        /// </summary>
        Transform SpawnFaunaVariant(FaunaConfigurationSO config)
        {
            // Resolve the cell from the toy's anchor (always inside the membrane).
            var cell = Cell.FindCellContaining(transform.position);
            if (!cell)
            {
                CSDebug.LogWarning("[SpawnMatrix] No cell contains the toy - cannot spawn fauna.");
                return null;
            }

            // A POPULATION, not an individual - the same seed-floor count the cell spawner uses.
            int count = Mathf.Max(1, config.PopulationSize);
            var clone = (FaunaConfigurationSO)LineageFor(config, count,
                c => cell.GetLiveFaunaCount((FaunaConfigurationSO)c),
                c => cell.ResolveFaunaCap((FaunaConfigurationSO)c),
                out bool joined);

            // Joining a live population: hatch beside it (an unbanded group should read as ONE
            // group, not two). A new one goes to the densest mass. In an EMPTY cell (the Barren
            // inspection cell) the pilot's own trail is the only food there will ever be, so it
            // hatches a short way ahead of the pilot - where it is seen and can start grazing -
            // rather than at a random point it would wither in before anything reached it.
            Vector3 goal = joined && TryFindLineageMember(cell, clone, out var member) ? member
                : cell.TryGetDensestRegionAnyDomain(out var densest) ? densest
                : TryPointAheadOfPilot(cell, out var ahead) ? ahead
                : CellLifeSpawnerBase.RandomPointInCytoplasm(cell);

            Domains domain = Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus?.Domain ?? cell.ControllingDomain;
            int spawned = 0;
            Transform first = null;
            for (int i = 0; i < count; i++)
            {
                // Banded species ignore both fallbacks and take their own point in their band.
                var fauna = CellLifeSpawnerBase.SpawnFaunaBanded(cell, clone, domain, goal,
                    goal + Random.insideUnitSphere * UnbandedReleaseJitter);
                if (!fauna) continue;
                if (!first) first = fauna.transform;
                RecordRelease(config, fauna.transform);
                spawned++;
            }
            if (CSDebug.IsVerbose(CSLogChannel.ToyBox))
                CSDebug.LogVerbose(CSLogChannel.ToyBox,
                    $"[SpawnMatrix] Spawned {spawned}/{count} x {clone.name} ({domain}) into " +
                    (joined ? "its live population; " : "a new population; ") +
                    (clone.BandOuterRadius > 0f
                        ? $"scattered through its band {clone.BandInnerRadius:0}-{clone.BandOuterRadius:0}"
                        : $"around {goal}"));
            return first;
        }

        /// <summary>
        /// Release a population of this exact variant where the species GROWS: each plant goes
        /// through the cell spawner's own planting call (<see cref="CellLifeSpawnerBase.PlantFlora"/>),
        /// so it takes a prepared planting site when the cell has one and otherwise disperses
        /// through its own planting band - threat flora root in their grove, the Swarm
        /// Borromean bands honour their pens. It used to be pinned around the station, which
        /// clumped every plant at the toy and, for the threat flora, put them outside the
        /// grove their whole behaviour is built around.
        /// </summary>
        Transform SpawnFloraVariant(FloraConfigurationSO config)
        {
            var cell = Cell.FindCellContaining(transform.position);
            if (!cell)
            {
                CSDebug.LogWarning("[SpawnMatrix] No cell contains the toy - cannot spawn flora.");
                return null;
            }

            int count = Mathf.Max(1, config.InitialSpawnCount);
            var clone = (FloraConfigurationSO)LineageFor(config, count,
                c => cell.GetLiveFloraCount((FloraConfigurationSO)c),
                c => cell.ResolveFloraCap((FloraConfigurationSO)c),
                out bool joined);
            int spawned = 0;
            Transform first = null;
            for (int i = 0; i < count; i++)
            {
                var flora = CellLifeSpawnerBase.PlantFlora(cell, clone, null);
                if (!flora) continue;
                if (!first) first = flora.transform;
                RecordRelease(config, flora.transform);
                spawned++;
            }

            // Frenzy gate honesty: flora growth freezes cell-wide above Frenzy (the ecology's
            // one growth brake). A long-running lava-lamp cell is often AT Frenzy, so a fresh
            // spawn sits as seed prisms until mass is cleared - say so instead of looking broken.
            string growth = cell.FloraGrowingEnabled
                ? "growing (from seed prisms - watch them build)"
                : FrozenNotice;
            if (CSDebug.IsVerbose(CSLogChannel.ToyBox))
                CSDebug.LogVerbose(CSLogChannel.ToyBox,
                    $"[SpawnMatrix] Planted {spawned}/{count} x {clone.name} in its own band, into " +
                    (joined ? "its live population" : "a new population") + $"; growth: {growth}");
            return first;
        }

        // ── Populations ──────────────────────────────────────────────────────

        // The runtime lineage config each variant is releasing into, keyed by the AUTHORED asset.
        // A population IS a config instance: the cell counts, caps and breeds live lifeforms per
        // SourceConfig reference (Cell.RegisterLiveFauna / liveFloraCounts), so reusing one clone
        // is what makes a second press join the first press's population.
        readonly Dictionary<ScriptableObject, ScriptableObject> _lineages = new();

        // What each variant has released, newest last - for Navigate and the window's watch.
        readonly Dictionary<ScriptableObject, List<Transform>> _releases = new();

        /// <summary>
        /// The lineage a release of <paramref name="incoming"/> more should join: the variant's
        /// current one when it can take them all (no cap, or live + incoming within the cap),
        /// otherwise a NEW population - a fresh runtime clone, so the authored asset is never
        /// mutated and the clone's own count and cap start empty. One-anchor species (swarms,
        /// builder colonies, substrate packs: cap 1) therefore found a new population on every
        /// press, which is the only honest answer: none of them can grow an anchor after it seeds.
        /// </summary>
        ScriptableObject LineageFor(ScriptableObject authored, int incoming,
            System.Func<ScriptableObject, int> liveCount, System.Func<ScriptableObject, int> cap,
            out bool joined)
        {
            if (_lineages.TryGetValue(authored, out var current) && current)
            {
                int live = liveCount(current);
                int limit = cap(current);
                if (limit <= 0 || live + incoming <= limit)
                {
                    joined = live > 0;
                    return current;
                }
            }

            var clone = Instantiate(authored);
            clone.name = authored.name;
            // The matrix is the tuning BENCH: a station spawns the EXACT variant it shows, so
            // the cell's element spread must not re-roll it here.
            switch (clone)
            {
                case FaunaConfigurationSO f: f.SpreadElements = false; break;
                case FloraConfigurationSO fl: fl.SpreadElements = false; break;
            }
            _lineages[authored] = clone;
            joined = false;
            return clone;
        }

        static bool TryFindLineageMember(Cell cell, FaunaConfigurationSO lineage, out Vector3 position)
        {
            var live = cell.LiveFauna;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var f = live[i];
                if (f && f.SourceConfig == lineage)
                {
                    position = f.transform.position;
                    return true;
                }
            }
            position = default;
            return false;
        }

        // How far ahead of the pilot an unbanded release hatches in an empty cell: clear of the
        // hull (the group jitters ±UnbandedReleaseJitter around it), close enough to be in view.
        const float PilotHatchDistance = 220f;

        bool TryPointAheadOfPilot(Cell cell, out Vector3 point)
        {
            point = default;
            // IsAlive, not `?.`: mid-swap the player holds a DESTROYED vessel (interface ref).
            var pilot = Context?.GameData?.LocalPlayer?.Vessel;
            if (!pilot.IsAlive()) return false;
            var vessel = pilot.Transform;
            if (!cell.IsInsideMembrane(vessel.position)) return false;
            point = vessel.position + vessel.forward * PilotHatchDistance;
            if (!cell.IsInsideMembrane(point)) point = vessel.position;
            return true;
        }

        // Navigate's target for a population that is a BODY rather than one creature (a swarm, a
        // substrate pack, a builder colony): its anchor GameObject can sit far from where its
        // members actually are (a stampede's herds are seeded across its whole sector), so the
        // window is pointed at this marker, moved onto the population's live centre on each ask.
        Transform _navigateMarker;

        /// <summary>
        /// Where this variant's newest release IS: the creature or plant itself, or for a
        /// population body (<see cref="IMacroPopulation"/>) the body's live centre.
        /// </summary>
        Transform ReleaseAnchor(ScriptableObject authored)
        {
            var made = LatestRelease(authored);
            if (!made) return null;
            if (!made.TryGetComponent(out IMacroPopulation body)) return made;

            if (!_navigateMarker)
            {
                _navigateMarker = new GameObject("SpawnMatrix NavigateMarker").transform;
                _navigateMarker.SetParent(transform, false);
            }
            var c = body.MacroCentre;
            _navigateMarker.position = new Vector3(c.X, c.Y, c.Z);
            return _navigateMarker;
        }

        void RecordRelease(ScriptableObject authored, Transform made)
        {
            if (!authored || !made) return;
            if (!_releases.TryGetValue(authored, out var list)) _releases[authored] = list = new List<Transform>();
            list.RemoveAll(t => !t);
            list.Add(made);
        }

        /// <summary>The newest still-living thing this variant released, or null.</summary>
        Transform LatestRelease(ScriptableObject authored)
        {
            if (!authored || !_releases.TryGetValue(authored, out var list)) return null;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i]) return list[i];
            return null;
        }

        /// <summary>
        /// Release an AI-piloted vessel of <paramref name="vessel"/> in the LOCAL PLAYER'S domain.
        /// Routed through the menu's networked spawn pipeline, so the companion is an ordinary
        /// server-owned AI player: it replicates to the whole party, it flies the lava lamp on
        /// autopilot, and it is despawned with every other AI when the menu is left.
        ///
        /// It lands one spacing back toward the cell CENTRE from <paramref name="releasePoint"/>,
        /// facing in.
        /// </summary>
        void ReleaseCompanion(VesselClassType vessel, Vector3 releasePoint)
        {
            var init = Context?.VesselInitializer;
            if (!init)
            {
                CSDebug.LogWarning("[SpawnMatrix] No menu vessel initializer - cannot release a companion.");
                return;
            }

            // The toy faces the cell centre, so +forward is "inward" for both the offset and the
            // heading. No cell lookup needed, and it stays correct if the toy is ever re-placed.
            Vector3 inward = transform.forward;
            Vector3 position = releasePoint + inward * _def.StationSpacing;
            var pose = new Pose(position, Quaternion.LookRotation(inward, transform.up));

            Domains domain = ToyVesselRoster.PlayerDomain(Context);
            // "Requested", not "released": on a party CLIENT this is a ServerRpc going out, and
            // the server is the only thing that can say whether a companion actually appeared
            // (it logs that end itself). Claiming completion here would be a lie on every
            // machine that is not the host.
            init.RequestSpawnAiCompanion(vessel, domain, pose);
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[SpawnMatrix] Requested a {vessel} companion ({domain}) at {position}.");
        }

        // ── Previews ─────────────────────────────────────────────────────────

        // Non-body subsystems on a lifeform prefab - the same class of thing the vessel hull filter
        // drops, so a creature icon is the creature, not its effects.
        static readonly string[] NonBodyNameHints = { "trail", "vfx", "pip", "explosion", "particle" };

        /// <summary>
        /// A species preview SHOWS ITS SPECIES: a display-only model harvested from the first
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
            // meshes that aren't there (which is why these icons were anonymous spheres), ask
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
            return built;
        }
    }
}

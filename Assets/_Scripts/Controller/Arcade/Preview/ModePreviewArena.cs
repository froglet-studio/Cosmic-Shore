using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Stands a mode's arena up as a <b>satellite cell</b> far from the menu world, so a preview
    /// can be flown without the scene the player is looking at changing in any way.
    ///
    /// <para>Isolation is by DISTANCE, not by layer: prisms, crystals and lifeforms set their own
    /// layers and interact through the physics matrix, so moving an arena onto a private layer
    /// would quietly change how it plays. Parking it past every gameplay camera's far clip (8,000
    /// in Menu_Main) keeps it out of the menu view while leaving it a completely ordinary cell.</para>
    ///
    /// <para><b>Collider budget.</b> This is the expensive half of the preview and it is deliberate:
    /// a satellite pays for a second cell on top of the menu's, where a
    /// <see cref="Cell.RequestCellSwap"/> would have kept the budget flat by replacing it. The
    /// trade buys "the menu never changes". Keep preview cells to their lightest authored
    /// intensity, and see Docs/ModePreview/ARCHITECTURE.md for the measured impact.</para>
    ///
    /// <para><b>Mass is conserved.</b> The arena is created by an explicit player action (clicking
    /// into a preview) and removed by one (leaving it) - the same event class as a cell swap or a
    /// scene load. Nothing here is on a clock; nothing ages out.</para>
    /// </summary>
    public sealed class ModePreviewArena
    {
        /// <summary>The live satellite cell, or null when nothing is standing.</summary>
        public Cell Cell { get; private set; }

        // The camera that shows the WORLD, with no vessel in it. Arena furniture - created with
        // the arena and struck with it - deliberately NOT the gameplay camera: opening a card must
        // not touch the player's ship or their camera at all, so that closing it again has nothing
        // to put back.
        Camera _arenaCamera;
        float _orbitAngle;

        // The SCALE MODEL shown while a card is only being looked at, and the root it lives under.
        // Separate from _root on purpose: the model is struck the instant the real arena stands, and
        // one root holding both would take the arena down with it.
        GameObject _modelRoot;
        float _modelRadius;

        // The meshes this class GENERATED. Tracked explicitly because the model root also holds
        // membrane/nucleus display copies, whose MeshFilters point at PROJECT ASSETS - sweeping
        // every MeshFilter under the root and destroying its sharedMesh asks Unity to delete those
        // assets, which it refuses with "Destroying assets is not permitted". Owning a mesh is a
        // fact about where it came from, never something to infer from where it is parented.
        readonly List<Mesh> _generatedMeshes = new();

        /// <summary>
        /// The arena's own runtime data instance — what the previewing vessel's
        /// <see cref="AIPilot"/> is retargeted onto so it hunts THIS cell's contents instead of
        /// the menu's. Null when nothing is standing.
        /// </summary>
        public CellRuntimeDataSO RuntimeInstance => _runtimeInstance;

        /// <summary>Where the arena was parked.</summary>
        public Vector3 Origin { get; private set; }

        /// <summary>True once the cell exists and has a config (its world may still be growing).</summary>
        public bool IsStanding => Cell && Cell.HasConfigAssigned;

        GameObject _root;
        CellRuntimeDataSO _runtimeInstance;
        GameObject _structure;

        /// <summary>
        /// Build the arena for <paramref name="definition"/> at <paramref name="origin"/>.
        /// <paramref name="template"/> is any live cell to clone the prefab and runtime shape from
        /// - normally the scene's own cell.
        /// </summary>
        public bool Stand(ModePreviewDefinitionSO definition, CellConfigDataSO config, Cell template,
                          GameObject cellPrefab, Vector3 origin, Container container = null,
                          int intensity = 1)
        {
            if (IsStanding) return false;

            // The CONFIG is passed in rather than read off the definition, because which arena a
            // mode previews is a function of the chosen INTENSITY (ModePreviewDefinitionSO.
            // ResolveCell) and the arena has no business knowing where that number comes from.
            if (!definition || !config)
            {
                CSDebug.LogWarning("[ModePreview] Arena asked to stand with no preview cell - ignored.");
                return false;
            }

            var runtimeSource = template ? template.RuntimeData : null;
            if (!cellPrefab || !runtimeSource)
            {
                CSDebug.LogWarning("[ModePreview] Arena needs a Cell prefab and a runtime data " +
                                   "asset to clone - ignored.");
                return false;
            }

            Origin = origin;

            // Instantiated under an INACTIVE root so the cell's OnEnable does not run before it
            // has been handed its own runtime data. OnEnable clears runtime.Config, and the
            // prefab still points at the SHARED asset at this instant - so activating first would
            // wipe the live menu cell's config out from under it. See Cell.BindSatelliteRuntime.
            _root = new GameObject("ModePreviewArena");
            _root.SetActive(false);
            _root.transform.position = origin;

            var cellGo = Object.Instantiate(cellPrefab, _root.transform);
            cellGo.transform.localPosition = Vector3.zero;
            cellGo.transform.localRotation = Quaternion.identity;

            // A runtime Instantiate gets NO dependency injection: Reflex populates [Inject] for
            // objects present at scene load, and for whatever a call site explicitly injects.
            // Cell.gameData is [Inject], and so is every spawner it starts - so without this the
            // satellite came up with null GameData and its life spawners refused to run
            // ("[CellLifeSpawner] GameData is null for host 'Cell(Clone)'"). This is the same
            // InjectRecursive the vessel spawner does for exactly the same reason; the whole of
            // Controller/Environment relies on being present at load, and a satellite is not.
            if (container != null)
                GameObjectInjector.InjectRecursive(cellGo, container);
            else
                CSDebug.LogWarning("[ModePreview] Arena stood with no DI container - the satellite " +
                                   "cell's injected dependencies will be null.");

            Cell = cellGo.GetComponentInChildren<Cell>(true);
            if (!Cell)
            {
                CSDebug.LogError("[ModePreview] The Cell prefab carries no Cell component - arena aborted.");
                FinishStrike();
                return false;
            }

            _runtimeInstance = Object.Instantiate(runtimeSource);
            _runtimeInstance.name = $"{runtimeSource.name} (preview {definition.Mode})";
            _runtimeInstance.ResetRuntimeData();
            Cell.BindSatelliteRuntime(_runtimeInstance);

            _root.SetActive(true);

            // The flight arena builds THINNED: every dense trail lays every Nth prism, so the
            // shape a player flies through is the mode's real shape at a fraction of the prisms,
            // colliders and spatial-index load - a preview stands beside a menu that is still
            // running, and building the full world is what made tapping in hitch. The cell
            // carries its stride itself because its environment build can be deferred past this
            // method; the track structure below builds inside an explicit scope.
            int stride = 1;
            var previewLibrary = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
            if (previewLibrary) stride = previewLibrary.FlightPrismStride;
            Cell.SatellitePrismStride = stride;

            if (!Cell.InitializeSatellite(config))
            {
                FinishStrike();
                return false;
            }

            using (PrismLayDecimation.At(stride))
            {
                SpawnStructure(definition);
                SpawnTrackStructure(definition, intensity);
            }
            SpawnPreviewCrystals(definition, intensity);
            SpawnPreviewFauna(definition);

            CSDebug.Log($"[ModePreview] Arena standing for {definition.Mode} " +
                        $"({config.CellName}) at {origin}.");
            return true;
        }

        /// <summary>
        /// A local prop for a mode whose gameplay structure is built by its CONTROLLER rather than
        /// by its cell (Scarab's hoops, Astro League's goals, SkimRace's track). Refused outright if
        /// it is networked - Menu_Main hosts the party, so a NetworkObject here would spawn the
        /// preview's furniture into everybody else's menu.
        /// </summary>
        void SpawnStructure(ModePreviewDefinitionSO definition)
        {
            if (!definition.StructurePrefab) return;

            if (definition.StructurePrefab.GetComponentInChildren<Unity.Netcode.NetworkObject>(true))
            {
                CSDebug.LogError($"[ModePreview] '{definition.StructurePrefab.name}' carries a " +
                                 "NetworkObject. A preview is strictly local - skipped.");
                return;
            }

            _structure = Object.Instantiate(definition.StructurePrefab, Origin, Quaternion.identity);
            _structure.name = $"ModePreviewStructure ({definition.Mode})";
            _structure.transform.SetParent(_root.transform, true);
        }

        // The mode's REAL track/structure, built for the flight phase, and its spawnable instance.
        GameObject _trackStructure;
        GameObject _trackSource;

        /// <summary>
        /// Build the mode's scene-built structure FOR REAL - the thing you actually fly through.
        ///
        /// <para>Scurry's torus and shells, Skim Race's track, are built by their scene's
        /// <c>SegmentSpawner</c>, not by their cell - so a flight arena that stands only the cell
        /// is open water: the spawn seat is correct and there is nothing recognisable near it.
        /// The looking phase already MODELS these spawnables
        /// (<c>ModePreviewDefinitionSO.TrackSpawnablesByIntensity</c>, baked from the scenes);
        /// this builds the same asset the scene builds, at the same place the scene builds it
        /// (the spawners sit at the scene origin = the cell centre).</para>
        ///
        /// <para><b>Spawn-then-parent in the same frame is the Cell's own environment idiom</b>
        /// and is safe for STREAMED spawnables (prisms lay on later frames, after the container is
        /// placed). A synchronous lay would register every prism's spatial-index pose at the
        /// world origin and then move 120k - the stale-registration class SPATIAL_INDEX.md exists
        /// to prevent - which is why the torus was opted into <c>layAcrossFrames</c> alongside
        /// this change rather than moved after the fact.</para>
        ///
        /// <para>These prisms are INSTANTIATED, never pooled (<c>SpawnPrismTrail</c>), so the
        /// teardown may destroy them - gradually, via the strike's retiring root.</para>
        /// </summary>
        void SpawnTrackStructure(ModePreviewDefinitionSO definition, int intensity)
        {
            var spawnable = definition ? definition.ResolveTrackSpawnable(intensity) : null;
            if (!spawnable) return;

            if (spawnable.GetComponentInChildren<Unity.Netcode.NetworkObject>(true))
            {
                CSDebug.LogError($"[ModePreview] Track spawnable '{spawnable.name}' carries a " +
                                 "NetworkObject. A preview is strictly local - skipped.");
                return;
            }

            // An INSTANCE, so the generator's cached state never mutates the project asset.
            var source = Object.Instantiate(spawnable, _root.transform);
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            _trackSource = source.gameObject;

            var built = source.Spawn(Mathf.Max(1, intensity));
            if (!built) return;

            built.transform.SetParent(_root.transform, false);
            built.transform.localPosition = Vector3.zero;
            built.transform.localRotation = Quaternion.identity;
            built.name = $"ModePreviewTrack ({definition.Mode})";
            _trackStructure = built;

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] Track structure '{spawnable.name}' built for flight " +
                $"(intensity {intensity}).");
        }

        /// <summary>
        /// Collectable crystals for the flight phase of a crystal-scored mode.
        ///
        /// <para>Scurry races for crystals and Skim Race skims a crystal line down its track, and
        /// both previewed with NONE - the real modes' crystals belong to CrystalManager, which is
        /// scene-level. The preview mints them the way the Wanderway conveyor does: the omni
        /// prefab already carries its impactor + collider, and Crystal's manager-less guards make
        /// a local mint collectible with no manager. Nothing respawns - a preview is a taste, and
        /// its crystals are gone when they are gone.</para>
        ///
        /// <para>Placement mirrors the mode: a waypoint-track mode gets one crystal per sampled
        /// waypoint (the track IS the crystal line); any other crystal-scored mode gets a scatter
        /// drawn volume-uniformly inside the nucleus - the platform's own rule that the omni
        /// respawn volume IS the nucleus (Docs/ECOSYSTEM.md §27). Registered with the satellite's
        /// runtime data so the AI hunts them before the player takes over.</para>
        /// </summary>
        void SpawnPreviewCrystals(ModePreviewDefinitionSO definition, int intensity)
        {
            if (!definition || !CrystalScored(definition.ObjectiveMetric)) return;

            var library = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
            var prefab = library ? library.OmniCrystalPrefab : null;
            if (!prefab)
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    "[ModePreview] No omni crystal wired on the preview library - no pickups.");
                return;
            }

            var positions = new List<Vector3>();

            if (definition.ResolveTrackSpawnable(intensity) is SpawnableWaypointTrack track)
            {
                var lays = ModePreviewTrackModel.BuildWaypointLays(track, intensity);
                int stride = Mathf.Max(1, lays.Count / MaxPreviewCrystals);
                for (int i = 0; i < lays.Count; i += stride)
                    positions.Add(lays[i].Point.Position);
            }
            else
            {
                float radius = Mathf.Max(Cell ? Cell.ExpectedNucleusWorldRadius : 0f, 60f);
                var rng = new System.Random(
                    ModePreviewPlantingModel.StableSeed(definition.Mode.ToString()));
                for (int i = 0; i < ScatterPreviewCrystals; i++)
                {
                    // Volume-uniform: cbrt of a uniform draw, the same reasoning as planting.
                    float r = radius * Mathf.Pow((float)rng.NextDouble(), 1f / 3f);
                    var dir = new Vector3((float)rng.NextDouble() * 2f - 1f,
                                          (float)rng.NextDouble() * 2f - 1f,
                                          (float)rng.NextDouble() * 2f - 1f);
                    positions.Add(dir.sqrMagnitude > 0.001f ? dir.normalized * r : Vector3.up * r);
                }
            }

            foreach (var local in positions)
            {
                var crystal = Object.Instantiate(prefab, _root.transform);
                crystal.transform.localPosition = local;
                crystal.enabled = true;
                crystal.gameObject.SetActive(true);

                // The AI's hunting list. The crystal's own serialized cellData still points at
                // the prefab's shared asset, so its self-removal on destroy may miss this list -
                // CellRuntimeDataSO.PruneDestroyed makes that self-healing.
                if (_runtimeInstance) _runtimeInstance.AddCrystalToList(crystal);
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] {positions.Count} preview crystal(s) minted for {definition.Mode}.");
        }

        /// <summary>
        /// A HANDFUL of creatures for the flight phase of a kill-scored mode.
        ///
        /// <para>The satellite's own life spawner is deliberately suppressed (a preview is
        /// structure, not ecology - the full seeded population was most of the preview lag),
        /// which left LifeformsKilled cards with nothing to hunt. The card authors a SMALL
        /// species and a tiny count (<see cref="ModePreviewDefinitionSO.PreviewFauna"/>),
        /// released through the canonical <see cref="CellLifeSpawnerBase"/> path on a runtime
        /// clone - the Lifeform Matrix bench's idiom - so each creature registers in the cell's
        /// lifeform book and the strike retires it with the world. Released in
        /// <see cref="Domains.Blue"/>: the neutral sentinel is hostile to every pilot, so
        /// anyone's rounds land. Ecology protocol: a bounded explicit release (production, which
        /// §0 permits), nothing culled, and a kill still drops the heart - the lifeform-crystal
        /// invariant is untouched.</para>
        /// </summary>
        void SpawnPreviewFauna(ModePreviewDefinitionSO definition)
        {
            if (!definition || !definition.PreviewFauna || !Cell) return;

            if (definition.ObjectiveMetric != ScoringMetric.LifeformsKilled)
            {
                CSDebug.LogWarning($"[ModePreview] {definition.Mode} authors PreviewFauna but " +
                                   "is not kill-scored - skipped (a preview is structure, not " +
                                   "ecology).");
                return;
            }

            // Runtime clone so the authored asset is never mutated; the clone IS the lineage
            // config. The card releases the exact species it authored - no element re-roll.
            var clone = Object.Instantiate(definition.PreviewFauna);
            clone.name = definition.PreviewFauna.name;
            clone.SpreadElements = false;

            float radius = Mathf.Max(Cell.ExpectedNucleusWorldRadius, 60f);
            var rng = new System.Random(
                ModePreviewPlantingModel.StableSeed(definition.Mode + ":fauna"));
            int count = Mathf.Max(1, definition.PreviewFaunaCount);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() * 2f - 1f,
                                      (float)rng.NextDouble() * 2f - 1f,
                                      (float)rng.NextDouble() * 2f - 1f);
                Vector3 local = (dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up)
                                * (radius * (0.6f + 0.6f * (float)rng.NextDouble()));

                var fauna = CellLifeSpawnerBase.SpawnFaunaWithDomain(
                    Cell, clone.FaunaPrefab, Origin, Domains.Blue, Origin + local);
                if (!fauna) continue;
                fauna.AssignLineage(Cell, clone);
                spawned++;
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] {spawned}/{count} preview {clone.name} released for " +
                $"{definition.Mode} (Blue - prey for every pilot).");
        }

        static bool CrystalScored(ScoringMetric metric) =>
            metric is ScoringMetric.Crystals or ScoringMetric.OmniCrystals
                   or ScoringMetric.ElementalCrystals;

        /// <summary>Crystal cap along a waypoint track - one per sampled waypoint.</summary>
        const int MaxPreviewCrystals = 24;

        /// <summary>How many crystals a non-track crystal mode scatters in its nucleus.</summary>
        const int ScatterPreviewCrystals = 6;

        /// <summary>
        /// Phase one of the teardown: retire the world POOL-SAFELY through
        /// <see cref="Cell.StrikeSatelliteWorld"/> — pooled prisms (the vessel's trail laid here)
        /// go back to their pool, never through <c>Destroy</c>, which corrupts the pool's
        /// accounting and with it every trail in the scene. Returns the retiring root holding the
        /// instantiated remainder for the CALLER to drain over frames (destroying a 10-20k-prism
        /// world in one frame is a multi-second freeze), because everything this class owns is
        /// about to be destroyed and cannot host the drain itself.
        ///
        /// <para>Explicit removal by a player action — the only thing allowed to remove this mass
        /// (Docs/ECOSYSTEM.md §19). Call <see cref="FinishStrike"/> once the drain completes.</para>
        /// </summary>
        public GameObject BeginStrike()
        {
            EndArenaCamera();
            _arenaCamera = null;      // destroyed with its host root below
            StrikeModel();

            GameObject retiring = null;
            if (Cell) retiring = Cell.StrikeSatelliteWorld();

            if (_structure)
            {
                Object.Destroy(_structure);
                _structure = null;
            }

            if (_trackSource)
            {
                Object.Destroy(_trackSource);
                _trackSource = null;
            }

            // The track's prisms ride the SAME gradual drain as the cell's world - destroying a
            // few thousand of them in one frame is the freeze the retiring root exists to avoid.
            // No retiring root (the cell never stood) means there is nothing big to drain and an
            // immediate destroy is fine.
            if (_trackStructure)
            {
                if (retiring) _trackStructure.transform.SetParent(retiring.transform, true);
                else Object.Destroy(_trackStructure);
                _trackStructure = null;
            }

            return retiring;
        }

        /// <summary>
        /// Phase two: destroy the cell, its root, and the runtime data instance. Runs after the
        /// drain so a prism destroyed mid-drain never dereferences a dead cell. Idempotent, and
        /// also the whole teardown for an arena that never finished standing.
        /// </summary>
        public void FinishStrike()
        {
            StrikeModel();

            // Normally handed to the retiring root by BeginStrike; on the abort path (an arena
            // that never finished standing) they are still under _root and die with it - these
            // just keep the fields honest.
            _trackStructure = null;
            _trackSource = null;

            if (_root)
            {
                Object.Destroy(_root);
                _root = null;
            }

            if (_runtimeInstance)
            {
                Object.Destroy(_runtimeInstance);
                _runtimeInstance = null;
            }

            Cell = null;
        }

        /// <summary>
        /// Show the arena on its own, with no vessel: a camera parented to the arena root, framing
        /// the cell and orbiting it slowly.
        ///
        /// <para>This is what a card shows the moment it opens. The vessel arrives only when the
        /// player taps in, and until then NOTHING outside this arena has been touched - not the
        /// hull, not its pose, not the gameplay camera - which is what makes backing out of a card
        /// you only looked at free, and what the messy teardown was paying for before.</para>
        ///
        /// <para>Settings are copied off the live camera rather than authored, so the preview is
        /// lit and cleared the way the game is. Its own near/far are local: the arena is 120k units
        /// out, but this camera is standing IN it.</para>
        /// </summary>
        public Camera BeginArenaCamera(RenderTexture texture)
        {
            // Either subject will do - the scale model a card opens on, or the real cell once the
            // player has tapped in.
            var host = _modelRoot ? _modelRoot : _root;
            if (!texture || !host) return null;

            if (_arenaCamera && _arenaCamera.transform.parent != host.transform)
            {
                Object.Destroy(_arenaCamera.gameObject);
                _arenaCamera = null;
            }

            if (!_arenaCamera)
            {
                var go = new GameObject("ModePreviewArenaCamera");
                go.transform.SetParent(host.transform, false);
                _arenaCamera = go.AddComponent<Camera>();

                AdoptGameCameraSettings(_arenaCamera);
                _arenaCamera.nearClipPlane = 1f;
                _arenaCamera.farClipPlane = 20000f;
            }

            _arenaCamera.targetTexture = texture;
            _arenaCamera.enabled = true;
            _orbitAngle = 0f;
            FrameCell(0f);
            return _arenaCamera;
        }

        /// <summary>
        /// Make the arena camera draw the way the game's camera draws.
        ///
        /// <para>A bare <c>AddComponent&lt;Camera&gt;</c> comes up with URP's DEFAULTS, not the
        /// project's: no post-processing, no anti-aliasing, SDR. So the looking phase rendered a
        /// flat, aliased, bloom-free version of a world the tap-in phase then showed correctly -
        /// which reads as the preview being low quality rather than as two different cameras.
        /// The tap-in phase was always right because it borrows the real gameplay camera
        /// (<c>CameraManager.BeginWindowedPlayerCamera</c>); this makes the browsing phase borrow
        /// its SETTINGS.</para>
        ///
        /// <para>Copying the <see cref="UniversalAdditionalCameraData"/> is the load-bearing half -
        /// post-processing, anti-aliasing and the renderer index all live there, not on
        /// <see cref="Camera"/>. Copying the base Camera fields alone (which is what this did) gets
        /// the framing and the clear right and none of the image quality.</para>
        /// </summary>
        static void AdoptGameCameraSettings(Camera target)
        {
            var source = Camera.main;
            if (!source) return;

            target.clearFlags = source.clearFlags;
            target.backgroundColor = source.backgroundColor;
            target.fieldOfView = source.fieldOfView;
            target.cullingMask = source.cullingMask;
            target.allowHDR = source.allowHDR;
            target.allowMSAA = source.allowMSAA;

            if (!source.TryGetComponent(out UniversalAdditionalCameraData from)) return;

            var to = target.GetUniversalAdditionalCameraData();
            if (!to) return;

            to.renderPostProcessing = from.renderPostProcessing;
            to.antialiasing = from.antialiasing;
            to.antialiasingQuality = from.antialiasingQuality;
            to.renderShadows = from.renderShadows;
            to.volumeLayerMask = from.volumeLayerMask;

            // The scriptable RENDERER index is deliberately not copied: URP exposes SetRenderer
            // but no public getter for the index in this version, so there is nothing to copy it
            // FROM without reaching into internals. A camera in the same scene gets the pipeline's
            // default renderer, which is the one the game uses anyway.
        }

        /// <summary>
        /// Show the mode's world as a <b>SCALE MODEL</b> - the same thing the Cell Selector toy
        /// shows for a cell you have not chosen yet (<see cref="CellMiniatureBuilder"/>).
        ///
        /// <para><b>This is what a card opens on, and it is why browsing is cheap.</b> Standing the
        /// REAL cell to look at costs a full per-prism build - the Boneyard alone is ~69k prisms,
        /// on top of the menu world that is already live - which is a multi-second freeze per card
        /// and a permanent frame-rate collapse while it is up. The model reads the generator's
        /// point data and spawns NO PRISMS: generation is pure math, and the per-prism Instantiate
        /// that is ~97% of a real build simply never happens. One mesh, a submesh per domain, a
        /// few draw calls.</para>
        ///
        /// <para>The lays are RELEASED immediately after sampling. Retaining a 34k-entry list per
        /// card the player merely browsed past is the trade this whole path exists to refuse.</para>
        ///
        /// <para>A config with no authored <c>EnvironmentPrefab</c> has no structure to sample -
        /// fourteen of the arcade's seventeen preview cells are in that state, because their
        /// arenas are PLANTED once a match starts rather than laid. Those fall through to
        /// <see cref="ModePreviewPlantingModel"/>, which models the planting the profile
        /// describes; a cell that plants nothing (the Barren cell Joust, Scurry and Skim Race run
        /// on) correctly shows its shell alone, because that arena IS open water.</para>
        /// </summary>
        public bool StandModel(ModePreviewDefinitionSO definition, int intensity,
                               CellConfigDataSO config, GameDataSO gameData, Vector3 origin,
                               float radius, int pointBudget)
        {
            StrikeModel();
            if (!config) return false;

            Origin = origin;
            _modelRadius = Mathf.Max(1f, radius);
            _modelRoot = new GameObject($"ModePreviewModel ({config.CellName})");
            _modelRoot.transform.position = origin;

            // A cell whose world is GROWN (Rampage's cactus forest) or deliberately empty (the
            // Barren cell Joust, Scurry and Skim Race run on) authors no EnvironmentPrefab. It is
            // still a real place with a real shape - membrane and core - and showing that is a
            // truer answer than "preview not available", which is what those three cards said
            // when the model path only understood authored environments.
            bool shell = AddCellShell(config);

            // A mode whose environment is stood by its SCENE rather than by its cell (Joust,
            // Scurry and Skim Race each carry a SegmentSpawner) shows that structure here. It is
            // additive on purpose: nothing today authors both a track and an EnvironmentPrefab,
            // but a mode that did would genuinely have both in its arena.
            bool track = AddTrackModel(definition, intensity, gameData, pointBudget);

            if (!config.EnvironmentPrefab)
            {
                bool planted = AddPlantingModel(config, gameData, pointBudget);
                if (shell || planted || track) return true;
                StrikeModel();
                return false;
            }

            var miniature = CellMiniatureBuilder.Build(config.EnvironmentPrefab, _modelRadius,
                                                      Mathf.Max(64, pointBudget));

            // Whatever the outcome: a generated lay list is the expensive thing to keep, and the
            // mesh is the only part worth holding on to.
            if (config.EnvironmentPrefab is CellEnvironmentSpawnableBase cellEnvironment)
                cellEnvironment.ReleaseGeneratedData();

            if (!miniature.IsValid)
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    $"[ModePreview] '{config.CellName}' produced no scale model - its generator " +
                    "emitted nothing.");
                if (shell || track) return true;
                StrikeModel();
                return false;
            }

            var body = ToyFactory.AddMiniatureBody(_modelRoot.transform, miniature,
                                                   new ToyContext { GameData = gameData },
                                                   "ScaleModel");
            if (!body)
            {
                if (shell || track) return true;
                StrikeModel();
                return false;
            }

            _generatedMeshes.Add(miniature.Mesh);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] Scale model of '{config.CellName}' up at {origin} " +
                $"(radius {_modelRadius}, {miniature.SubmeshDomains.Length} domain submeshes).");
            return true;
        }

        /// <summary>
        /// The mode's SCENE-BUILT environment as a model - the track its own scene's
        /// <c>SegmentSpawner</c> stands at match start. The cell-only model path was structurally
        /// blind to these: Joust, Scurry and Skim Race all run cells that author no environment,
        /// yet none of those arenas is open water - the environment is simply built by the SCENE,
        /// which no <see cref="CellConfigDataSO"/> can say.
        ///
        /// <para>The spawnable is sampled exactly the way an authored environment is - generation
        /// is pure math and no prisms spawn. An intensity-aware spawnable (Skim Race's waypoint
        /// track) is walked through its own <see cref="SpawnableWaypointTrack.GetPreviewBlocks"/>,
        /// which mirrors what <c>Spawn</c> would lay at that intensity; everything else goes
        /// through <see cref="CellMiniatureBuilder.Build"/> like an
        /// <c>EnvironmentPrefab</c> does.</para>
        /// </summary>
        bool AddTrackModel(ModePreviewDefinitionSO definition, int intensity, GameDataSO gameData,
                           int pointBudget)
        {
            var spawnable = definition ? definition.ResolveTrackSpawnable(intensity) : null;
            if (!spawnable) return false;

            CellMiniatureBuilder.Miniature miniature;
            if (spawnable is SpawnableWaypointTrack waypointTrack)
            {
                var lays = ModePreviewTrackModel.BuildWaypointLays(waypointTrack, intensity);
                miniature = CellMiniatureBuilder.BuildFromLays(lays, _modelRadius,
                                                              Mathf.Max(64, pointBudget), 1f,
                                                              $"{spawnable.name} track");
            }
            else
            {
                miniature = CellMiniatureBuilder.Build(spawnable, _modelRadius,
                                                       Mathf.Max(64, pointBudget));

                // Same discipline as the environment path: the generated lay list is the
                // expensive thing to keep (Scurry's intensity-4 track is Atlantis, ~34k lays).
                if (spawnable is CellEnvironmentSpawnableBase cellEnvironment)
                    cellEnvironment.ReleaseGeneratedData();
            }

            if (!miniature.IsValid) return false;

            var body = ToyFactory.AddMiniatureBody(_modelRoot.transform, miniature,
                                                   new ToyContext { GameData = gameData },
                                                   "TrackModel");
            if (!body)
            {
                Object.Destroy(miniature.Mesh);
                return false;
            }

            _generatedMeshes.Add(miniature.Mesh);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] Track model '{spawnable.name}' up at intensity {intensity}.");
            return true;
        }

        /// <summary>
        /// The cell's PLANTING as a model, for a world that is grown rather than laid.
        ///
        /// <para>Fourteen of the arcade's preview cells author no environment at all, so at the
        /// instant a card is opened there is nothing built to sample - which is why those cards
        /// showed a bare shell. What does exist is the spawn profile: how many of each species,
        /// and which band of the cell each occupies. That is the memorable thing about several of
        /// these arenas (Rampage's cactus belt really is a ring at 0.76-0.94 of the membrane with
        /// the core left open), and it is true before a single prism grows.</para>
        ///
        /// <para>The markers are ordinary lays, so they go through the same
        /// <see cref="CellMiniatureBuilder"/> tail as an authored environment and cost the same
        /// nothing - no prisms, one mesh, a submesh per domain.</para>
        /// </summary>
        bool AddPlantingModel(CellConfigDataSO config, GameDataSO gameData, int pointBudget)
        {
            // Bands are FRACTIONS of the membrane and the builder normalises to its own bounds, so
            // the model is scale-free: handing it the framing radius produces exactly the shape the
            // real membrane radius would.
            var lays = ModePreviewPlantingModel.Build(config, _modelRadius,
                                                      ModePreviewPlantingModel.StableSeed(config.CellName));
            if (lays.Count == 0) return false;

            var miniature = CellMiniatureBuilder.BuildFromLays(lays, _modelRadius,
                                                               Mathf.Max(64, pointBudget),
                                                               1f, $"{config.CellName} planting");
            if (!miniature.IsValid) return false;

            var body = ToyFactory.AddMiniatureBody(_modelRoot.transform, miniature,
                                                   new ToyContext { GameData = gameData },
                                                   "PlantingModel");
            if (!body)
            {
                Object.Destroy(miniature.Mesh);
                return false;
            }

            _generatedMeshes.Add(miniature.Mesh);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] Planting model of '{config.CellName}' up ({lays.Count} plants).");
            return true;
        }

        /// <summary>
        /// The cell's own SHAPE - its membrane and its core - as authored prefabs, scaled into the
        /// model's radius. Two objects, so it is free next to the environment model, and it is what
        /// a cell with no authored environment actually looks like at the start of a match.
        ///
        /// <para>These are display copies under the model root, never the Cell's own instances:
        /// nothing here is a Cell, so none of the bookkeeping the platform-wide "never hand-place a
        /// membrane" rule protects is in play (Docs/ECOSYSTEM.md - that rule is about a live
        /// <c>Cell</c> whose tracked instance a scene copy would shadow).</para>
        /// </summary>
        bool AddCellShell(CellConfigDataSO config)
        {
            bool any = false;

            if (config.MembranePrefab)
            {
                var membrane = Object.Instantiate(config.MembranePrefab, _modelRoot.transform);
                membrane.name = "MembraneModel";
                membrane.transform.localPosition = Vector3.zero;
                membrane.transform.localScale = Vector3.one * (_modelRadius * MembraneShellScale);
                StripInteractivity(membrane);
                any = true;
            }

            if (config.NucleusPrefab)
            {
                var nucleus = Object.Instantiate(config.NucleusPrefab, _modelRoot.transform);
                nucleus.name = "NucleusModel";
                nucleus.transform.localPosition = Vector3.zero;
                nucleus.transform.localScale = Vector3.one * (_modelRadius * NucleusShellScale);
                StripInteractivity(nucleus);
                any = true;
            }

            return any;
        }

        /// <summary>
        /// A display copy must not act. Colliders would take part in physics queries the menu world
        /// runs, and any behaviour on these prefabs would tick against a Cell that does not exist.
        /// </summary>
        static void StripInteractivity(GameObject go)
        {
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour) behaviour.enabled = false;
        }

        /// <summary>Membrane drawn a touch inside the framing radius so it stays in shot.</summary>
        const float MembraneShellScale = 0.9f;

        /// <summary>Core at roughly the fraction of a cell a real nucleus occupies.</summary>
        const float NucleusShellScale = 0.3f;

        /// <summary>Take the scale model down. Safe when none is up.</summary>
        public void StrikeModel()
        {
            foreach (var mesh in _generatedMeshes)
                if (mesh) Object.Destroy(mesh);
            _generatedMeshes.Clear();

            if (!_modelRoot) return;

            Object.Destroy(_modelRoot);
            _modelRoot = null;
        }

        /// <summary>True while a scale model - rather than a live cell - is what the camera sees.</summary>
        public bool ModelStanding => _modelRoot;

        /// <summary>Advance the slow orbit. Driven by the session, so the arena owns no clock.</summary>
        public void TickArenaCamera(float deltaSeconds, float degreesPerSecond)
        {
            if (!_arenaCamera || !_arenaCamera.enabled) return;
            FrameCell(deltaSeconds * degreesPerSecond);
        }

        /// <summary>Stand the arena camera down - the gameplay camera is taking the window.</summary>
        public void EndArenaCamera()
        {
            if (!_arenaCamera) return;
            _arenaCamera.targetTexture = null;
            _arenaCamera.enabled = false;
        }

        /// <summary>True while the arena camera is the one drawing into the window.</summary>
        public bool ArenaCameraLive => _arenaCamera && _arenaCamera.enabled;

        void FrameCell(float advanceDegrees)
        {
            if (!_arenaCamera) return;

            _orbitAngle = Mathf.Repeat(_orbitAngle + advanceDegrees, 360f);

            float radius = _modelRoot ? _modelRadius : FramingRadius();

            var offset = Quaternion.Euler(0f, _orbitAngle, 0f) *
                         new Vector3(0f, radius * 0.35f, -radius * ArenaCameraFramingFactor);

            var t = _arenaCamera.transform;
            t.position = Origin + offset;
            t.rotation = Quaternion.LookRotation((Origin - t.position).normalized, Vector3.up);
        }

        /// <summary>
        /// How big the arena is, for framing. The membrane first - it is the playfield boundary, so
        /// it is what "the arena" means to somebody looking at the card.
        ///
        /// <para><b>`Cell.MembraneRadius` returns 0 until the membrane has actually spawned</b>, and
        /// the camera is placed the instant the cell reports its build finished - so a bare
        /// <c>Max(1, radius)</c> parked it 1.25 UNITS from the arena centre, where every mode looked
        /// identical because all any of them showed was the skybox and a few distant prisms. That is
        /// one bug reading as two: "the environment is not shown" and "the intensities do not
        /// change" were the same camera, in the same wrong place, whatever had been built around
        /// it.</para>
        ///
        /// <para>So it falls back - nucleus, then a default the size of the menu's own membrane -
        /// and it is re-read on every orbit tick rather than sampled once, so the framing corrects
        /// itself the moment the membrane appears.</para>
        /// </summary>
        float FramingRadius()
        {
            if (Cell)
            {
                float membrane = Cell.MembraneRadius;
                if (membrane > 1f) return membrane;

                // A cell with no membrane authored (or not spawned yet) still has a core.
                float nucleus = Cell.ExpectedNucleusWorldRadius;
                if (nucleus > 1f) return nucleus * NucleusFramingMultiple;
            }

            return DefaultFramingRadius;
        }

        /// <summary>The menu cell's own membrane radius - a sane arena size when nothing reports one.</summary>
        const float DefaultFramingRadius = 1200f;

        /// <summary>How much of a nucleus-only cell to take in: the core plus the room around it.</summary>
        const float NucleusFramingMultiple = 3f;

        /// <summary>
        /// How far back the arena camera sits, as a multiple of the membrane radius.
        ///
        /// <para>At 1.25 the camera sat close enough to the membrane that a card showed the inside
        /// of a wall rather than an arena. Well outside it, so the whole place is in frame with
        /// air around it - which is what makes two cards look like different worlds rather than
        /// two blue surfaces.</para>
        /// </summary>
        const float ArenaCameraFramingFactor = 1.95f;

        /// <summary>
        /// Where the vessel arrives on the tap: <b>the seat the real mode would give it</b>.
        ///
        /// <para>The resolution lives on the definition, which carries the mode's own scene data -
        /// the ring flag, radius, floor and formation for a mode that computes its ring, and the
        /// scene's hand-placed poses for a mode that does not.</para>
        /// </summary>
        public Pose SpawnPose(ModePreviewDefinitionSO definition, int seat = 0)
            => definition.ResolveSpawnPose(Origin, Cell ? Cell.ExpectedNucleusWorldRadius : 0f, seat);
    }
}

using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

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
                          GameObject cellPrefab, Vector3 origin)
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

            if (!Cell.InitializeSatellite(config))
            {
                FinishStrike();
                return false;
            }

            SpawnStructure(definition);

            CSDebug.Log($"[ModePreview] Arena standing for {definition.Mode} " +
                        $"({config.CellName}) at {origin}.");
            return true;
        }

        /// <summary>
        /// A local prop for a mode whose gameplay structure is built by its CONTROLLER rather than
        /// by its cell (Scarab's hoops, Astro League's goals, HexRace's track). Refused outright if
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

                var source = Camera.main;
                if (source)
                {
                    _arenaCamera.clearFlags = source.clearFlags;
                    _arenaCamera.backgroundColor = source.backgroundColor;
                    _arenaCamera.fieldOfView = source.fieldOfView;
                    _arenaCamera.cullingMask = source.cullingMask;
                }
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
        /// <para>A config with no authored <c>EnvironmentPrefab</c> - a grown world, a barren cell -
        /// has no structure to model, and says so by returning false rather than showing an empty
        /// frame.</para>
        /// </summary>
        public bool StandModel(CellConfigDataSO config, GameDataSO gameData, Vector3 origin,
                               float radius, int pointBudget)
        {
            StrikeModel();
            if (!config || !config.EnvironmentPrefab) return false;

            Origin = origin;
            _modelRadius = Mathf.Max(1f, radius);

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
                return false;
            }

            _modelRoot = new GameObject($"ModePreviewModel ({config.CellName})");
            _modelRoot.transform.position = origin;

            var body = ToyFactory.AddMiniatureBody(_modelRoot.transform, miniature,
                                                   new ToyContext { GameData = gameData },
                                                   "ScaleModel");
            if (!body)
            {
                StrikeModel();
                return false;
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] Scale model of '{config.CellName}' up at {origin} " +
                $"(radius {_modelRadius}, {miniature.SubmeshDomains.Length} domain submeshes).");
            return true;
        }

        /// <summary>Take the scale model down. Safe when none is up.</summary>
        public void StrikeModel()
        {
            if (!_modelRoot) return;

            // The mesh is built for this model alone and nothing else references it.
            foreach (var filter in _modelRoot.GetComponentsInChildren<MeshFilter>(true))
                if (filter && filter.sharedMesh) Object.Destroy(filter.sharedMesh);

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
        /// How far back the arena camera sits, as a multiple of the membrane radius. Just outside
        /// the membrane: far enough that the whole arena is in frame, close enough that its
        /// structure still reads at the size of a card's preview window.
        /// </summary>
        const float ArenaCameraFramingFactor = 1.25f;

        /// <summary>
        /// A pose looking into the arena from outside its nucleus - the framing the real mode
        /// opens on. A cell with no nucleus reports radius 0, which is why the definition's
        /// standoff carries the whole distance for those.
        /// </summary>
        public Pose SpawnPose(ModePreviewDefinitionSO definition)
        {
            float nucleus = Cell ? Cell.ExpectedNucleusWorldRadius : 0f;
            float radius = nucleus + Mathf.Max(0f, definition.SpawnDistanceOutsideNucleus);
            return CellSpawnFormation.Build(1, Origin, radius)[0];
        }
    }
}

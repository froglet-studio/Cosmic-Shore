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
            _arenaCamera = null;      // destroyed with the root in FinishStrike

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
            if (!texture || !_root) return null;

            if (!_arenaCamera)
            {
                var go = new GameObject("ModePreviewArenaCamera");
                go.transform.SetParent(_root.transform, false);
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

            // Frame the MEMBRANE, not the nucleus: the membrane is the playfield boundary, so it
            // is what "the arena" means to somebody looking at the card.
            float radius = Cell ? Mathf.Max(1f, Cell.MembraneRadius) : 1000f;

            var offset = Quaternion.Euler(0f, _orbitAngle, 0f) *
                         new Vector3(0f, radius * 0.35f, -radius * ArenaCameraFramingFactor);

            var t = _arenaCamera.transform;
            t.position = Origin + offset;
            t.rotation = Quaternion.LookRotation((Origin - t.position).normalized, Vector3.up);
        }

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

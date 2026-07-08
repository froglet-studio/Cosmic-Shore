using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Game;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
// The engine's UGS session placeholder also declares an IPlayer (Unity.Services.Multiplayer
// contract) — alias the game's player interface, which is what this harness means.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using EngineObject = CosmicShore.Engine.Object;

namespace CosmicShore.Client
{
    /// <summary>
    /// Optional per-run ecology tuning for <see cref="FreestyleFactory.Create"/>. The
    /// defaults are the playable client's; tests override them to isolate invariants
    /// (e.g. the conserved-mass soak zeroes starvation + fauna speed so NO active force
    /// exists — proving populations then never change at all).
    /// </summary>
    public sealed class FreestyleTuning
    {
        public int FloraInitialCount = 3;
        public int FloraMaxLivePrisms = 48;
        public int FaunaSeedFloor = 3;
        public int FaunaMaxPopulation = 6;
        public float FaunaStarvationSeconds = 60f;
        public float FaunaMinSpeed = 8f;
        public float FaunaMaxSpeed = 16f;
        public float FaunaConsumeRadius = 25f;
        public float InitialFaunaSpawnWaitTime = 2f;
        public float MembraneRadius = 420f;
    }

    /// <summary>
    /// The freestyle (lava-lamp) brain. Unlike <see cref="SkimRaceDirector"/> there is no
    /// track, scoring rule, turn monitor, or timer — freestyle has no score and no end
    /// condition. The director owns only:
    ///   • the autopilot ↔ player-control toggle (one system, two names: viewed from the
    ///     menu it is the lava lamp; under player control it is freestyle), raising the
    ///     REAL <see cref="MenuFreestyleEventsContainerSO"/> transition events the
    ///     <see cref="ToyboxController"/> gates its toys on;
    ///   • the SkimRace energy/boost feel (BoostActionSO shape gated on the real
    ///     ResourceSystem; energy raises top speed through the real
    ///     ThrottleScalerMultiplier hook; trail-skim charges through the real skimmer
    ///     pipeline — identical rules to the race so the vessel feels the same);
    ///   • scene upkeep the Unity scene gets for free: activating the initializer-chain
    ///     vessel clone (runtime-built "prefabs" are inactive by construction) and keeping
    ///     the rig's InputController paused so the window stays the single input driver;
    ///   • a cached ecology census (flora / fauna / lifeform-prisms) for the renderer +
    ///     the headless diag line;
    ///   • deterministic Shutdown (stop the prism spawn loop, the cell's spawner
    ///     coroutines, and the AI) so headless runs and tests wind down cleanly.
    /// Mass is conserved here exactly like everywhere else: the director has NO despawn,
    /// cap, or cull path — there is deliberately no RestartRace equivalent.
    /// </summary>
    public sealed class FreestyleDirector : MonoBehaviour
    {
        const float TopSpeedEnergyGain = 0.6f;   // SkimRace parity — full bar = +60% top speed
        const float BoostDrainPerSecond = 0.55f; // SkimRace parity
        const float CensusInterval = 0.5f;

        public GameDataSO GameData { get; private set; }
        public MenuFreestyleEventsContainerSO FreestyleEvents { get; private set; }
        public Cell Cell { get; private set; }
        public CellRuntimeDataSO Runtime { get; private set; }
        public SkimRacePrismFactory PrismFactory { get; private set; }
        public MenuServerPlayerVesselInitializer VesselInitializer { get; private set; }
        public ToyboxController Toybox { get; private set; }

        /// <summary>True while the player is flying (false = autopilot lava-lamp).</summary>
        public bool IsFreestyle { get; private set; }

        /// <summary>Boost intent (Button1Action edge state); gated on energy each frame.</summary>
        public bool BoostHeld;

        /// <summary>Live skim state mirrored from the vessel's SkimContactTracker (HUD/audio).</summary>
        public bool IsSkimming { get; private set; }
        public float SkimStrength { get; private set; }

        // census (renderer + diag) — rebuilt every CensusInterval
        public int FloraCount { get; private set; }
        public int FaunaCount { get; private set; }
        public int LifeFormPrismCount { get; private set; }
        public readonly List<HealthPrism> LifeFormPrisms = new();

        float _nextCensusAt;
        NetworkObject _initializerNetObj;
        IVesselStatus _lastVesselStatus;
        bool _shutdown;

        public IPlayer LocalPlayer => GameData.LocalPlayer;

        /// <summary>The live vessel status, null while a vessel swap is mid-flight.</summary>
        public IVesselStatus VesselStatus
        {
            get
            {
                var vessel = GameData.LocalPlayer?.Vessel;
                var status = vessel?.VesselStatus;
                if (status == null || (status is EngineObject o && !o)) return null;
                return status;
            }
        }

        public VesselClassType CurrentVesselClass =>
            VesselStatus?.VesselType ?? VesselClassType.Squirrel;

        public Domains CurrentDomain => GameData.LocalPlayer?.Domain ?? Domains.Jade;

        public bool AutopilotEnabled => VesselStatus?.AutoPilotEnabled ?? true;

        /// <summary>Trail prisms laid by every vessel this session (conserved — survives swaps).</summary>
        public int TrailPrismCount
        {
            get
            {
                int count = 0;
                var live = PrismFactory.Live;
                for (int i = 0; i < live.Count; i++)
                {
                    var go = live[i];
                    if (!go) continue;
                    var prism = go.GetComponent<Prism>();
                    if (prism && !prism.destroyed) count++;
                }
                return count;
            }
        }

        internal void InitializeFreestyle(GameDataSO gameData, MenuFreestyleEventsContainerSO freestyleEvents,
            Cell cell, CellRuntimeDataSO runtime, SkimRacePrismFactory prismFactory,
            MenuServerPlayerVesselInitializer vesselInitializer, NetworkObject initializerNetObj,
            ToyboxController toybox,
            ScriptableEventInputEvents onButtonPressed, ScriptableEventInputEvents onButtonReleased)
        {
            GameData = gameData;
            FreestyleEvents = freestyleEvents;
            Cell = cell;
            Runtime = runtime;
            PrismFactory = prismFactory;
            VesselInitializer = vesselInitializer;
            _initializerNetObj = initializerNetObj;
            Toybox = toybox;

            // The human's ported input strategy raises these SOAP channels (window-fed).
            onButtonPressed.OnRaised += HandleButtonPressed;
            onButtonReleased.OnRaised += HandleButtonReleased;
        }

        /// <summary>
        /// The lava-lamp ↔ freestyle toggle — the client's MenuCrystalClickHandler
        /// equivalent. Raises the REAL freestyle transition events (ToyboxController arms/
        /// disarms its toys off these), flips the REAL AIPilot, and leaves the rig's
        /// InputController paused: the window drives the ported input strategy itself.
        /// </summary>
        public void ToggleControl()
        {
            if (IsFreestyle) TransitionToMenu();
            else TransitionToFreestyle();
        }

        public void TransitionToFreestyle()
        {
            if (IsFreestyle) return;
            var status = VesselStatus;
            if (status == null) return; // vessel not spawned yet / mid-swap
            IsFreestyle = true;
            FreestyleEvents.OnGameStateTransitionStart.Raise();
            status.Vessel.ToggleAIPilot(false);
            FreestyleEvents.OnGameStateTransitionEnd.Raise();
        }

        public void TransitionToMenu()
        {
            if (!IsFreestyle) return;
            IsFreestyle = false;
            BoostHeld = false;
            FreestyleEvents.OnMenuStateTransitionStart.Raise();
            var status = VesselStatus;
            if (status != null)
            {
                status.IsBoosting = false;
                status.Vessel.ToggleAIPilot(true);
            }
            FreestyleEvents.OnMenuStateTransitionEnd.Raise();
        }

        // Real drift parameters — DriftActionSO class defaults (SkimRace parity).
        const float DriftRotationMultiplier = 1.5f;
        const float DriftDampingTarget = 0f;

        void HandleButtonPressed(InputEvents inputEvent)
        {
            switch (inputEvent)
            {
                case InputEvents.Button1Action:
                    BoostHeld = true;
                    break;
                case InputEvents.OnlyLeftStickAction:
                case InputEvents.OnlyRightStickAction:
                    BeginHumanDrift(isSharp: false);
                    break;
                case InputEvents.BothSticksAction:
                    BeginHumanDrift(isSharp: true);
                    break;
            }
        }

        void HandleButtonReleased(InputEvents inputEvent)
        {
            switch (inputEvent)
            {
                case InputEvents.Button1Action:
                    BoostHeld = false;
                    break;
                case InputEvents.OnlyLeftStickAction:
                case InputEvents.OnlyRightStickAction:
                    EndHumanDrift(isSharp: false);
                    break;
                case InputEvents.BothSticksAction:
                    EndHumanDrift(isSharp: true);
                    break;
            }
        }

        /// <summary>DriftActionSO.StartAction shape (SkimRace parity) — the real
        /// VesselTransformer's two-tier analog interpolation does the rest.</summary>
        void BeginHumanDrift(bool isSharp)
        {
            var status = VesselStatus;
            if (status == null || !IsFreestyle) return;
            status.VesselTransformer.BeginDrift(DriftRotationMultiplier, DriftDampingTarget, isSharp);
            status.IsDrifting = true;
        }

        void EndHumanDrift(bool isSharp)
        {
            var status = VesselStatus;
            if (status == null) return;
            status.VesselTransformer.EndDrift(isSharp);
            status.IsDrifting = status.VesselTransformer.IsDriftActive;
        }

        void Update()
        {
            if (_shutdown) return;

            var status = VesselStatus;
            if (status != null)
            {
                // Runtime-built prefabs are inactive by construction, so the initializer
                // chain's clone arrives inactive — the Unity scene's prefab assets are
                // active. Activate it into the scene (spawn AND every swap).
                var vesselGo = ((VesselController)status.Vessel).gameObject;
                if (!vesselGo.activeSelf) vesselGo.SetActive(true);

                // A fresh rig (initial spawn OR a vessel-changer swap) missed the cell
                // crystal's pre-spawn OnCellItemsUpdated raise — its AIPilot subscribes at
                // clone activation and would otherwise fly straight forever. Re-raise the
                // registry channel once per new rig so the autopilot orbits the cell (the
                // real Menu_Main gets this for free from ongoing crystal activity).
                if (!ReferenceEquals(status, _lastVesselStatus))
                {
                    _lastVesselStatus = status;
                    Runtime.OnCellItemsUpdated.Raise();
                }

                // The window is the single input driver (SkimRace precedent): the toy
                // swap-restore path unpauses the rig's InputController — re-pause so the
                // ported KeyboardInputStrategy doesn't zero the window's stick writes.
                var input = LocalPlayer?.InputController;
                if (input != null && input.InputStatus is { Paused: false })
                    input.SetPause(true);

                TickEnergyAndBoost(status);
            }

            if (Time.time >= _nextCensusAt)
            {
                _nextCensusAt = Time.time + CensusInterval;
                RebuildCensus();
            }
        }

        /// <summary>SkimRace-parity energy feel: boost drains the real bar; energy raises
        /// top speed through the real ThrottleScalerMultiplier ElementalFloat hook.</summary>
        void TickEnergyAndBoost(IVesselStatus status)
        {
            var resources = status.ResourceSystem;
            if (resources == null || resources.Resources == null || resources.Resources.Count == 0) return;

            bool boosting = IsFreestyle && BoostHeld && resources.Resources[0].CurrentAmount > 0.02f;
            status.IsBoosting = boosting;
            if (boosting)
                resources.ChangeResourceAmount(0, -BoostDrainPerSecond * Time.deltaTime);

            float energy = resources.Resources[0].CurrentAmount;
            status.VesselTransformer.ThrottleScalerMultiplier.Value = 1f + TopSpeedEnergyGain * energy;

            var tracker = ((VesselController)status.Vessel).gameObject
                .GetComponentInChildren<SkimContactTracker>(includeInactive: true);
            IsSkimming = tracker != null && tracker.IsSkimming;
            SkimStrength = tracker != null ? tracker.Strength : 0f;
        }

        /// <summary>
        /// Ecology census for the renderer + diag: walks the scene roots for lifeforms.
        /// Flora are LifeForm-derived; fauna bodies (LightFauna does NOT extend LifeForm)
        /// are found through the cell's live-fauna registry. Every body/leaf prism is a
        /// HealthPrism — the Prism family — so the renderer draws them with the same slab
        /// pass as vessel trails.
        /// </summary>
        void RebuildCensus()
        {
            LifeFormPrisms.Clear();
            int flora = 0;

            var roots = GameLoop.Current.Scene.GetRootGameObjects();
            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (!root || !root.activeSelf) continue;
                var lifeForm = root.GetComponent<LifeForm>();
                var fauna = root.GetComponent<Fauna>();
                if (lifeForm == null && fauna == null) continue;
                if (lifeForm is Flora) flora++;

                var prisms = root.GetComponentsInChildren<HealthPrism>(true);
                for (int p = 0; p < prisms.Length; p++)
                {
                    var hp = prisms[p];
                    if (hp && !hp.destroyed) LifeFormPrisms.Add(hp);
                }
            }

            FloraCount = flora;
            FaunaCount = Cell ? Cell.LiveFauna.Count : 0;
            LifeFormPrismCount = LifeFormPrisms.Count;
        }

        /// <summary>Number of live (armed or blooming) toy stations in the scene.</summary>
        public int CountToys()
        {
            int count = 0;
            var roots = GameLoop.Current.Scene.GetRootGameObjects();
            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (!root || !root.activeSelf) continue;
                count += root.GetComponentsInChildren<Toy>(true).Length;
            }
            return count;
        }

        /// <summary>
        /// Deterministic wind-down (SkimRaceDirector.Shutdown precedent): stops the rig's
        /// async prism spawn loop, hands the AI off, despawns the initializer (cancels its
        /// CTS), stops the cell's spawner/fauna coroutines by deactivating the ecology
        /// GameObjects, and retires any active shape painter. Idempotent; the window calls
        /// it on close and tests call it in teardown.
        /// </summary>
        public void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;

            var status = VesselStatus;
            if (status != null)
            {
                status.VesselPrismController.StopSpawn();
                LocalPlayer.ResetForPlay(); // AI path toggles the AIPilot OFF
            }

            if (_initializerNetObj && _initializerNetObj.IsSpawned)
                _initializerNetObj.Despawn(false); // OnNetworkDespawn cancels the spawn CTS

            var painter = EngineObject.FindFirstObjectByType<MenuShapePainter>();
            if (painter) painter.gameObject.SetActive(false);

            if (Cell)
            {
                Cell.SetLifeFormsActive(false);     // fauna behavior/goal coroutines stop
                Cell.gameObject.SetActive(false);   // spawner coroutines stop; ActiveCells unregisters
            }

            if (Toybox) Toybox.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Builds the freestyle (lava-lamp) scene on the REAL ported pipeline — the client
    /// counterpart of Menu_Main:
    ///   • the local human Player spawns its vessel through the REAL networked
    ///     initializer chain (Player.OnNetworkSpawn → NetcodeHooks →
    ///     ServerPlayerVesselInitializer.ProcessPreExistingPlayers → SpawnVesselForPlayer →
    ///     ClientPlayerVesselInitializer.InitializePair →
    ///     MenuServerPlayerVesselInitializer.ActivateAutopilot) — autopilot ON by default,
    ///     domain reset to Jade server-side, exactly the Menu_Main flow;
    ///   • ONE living Cell wired the standard way (CellConfigDataSO + SpawnProfileSO +
    ///     CapsuleMembrane membrane + SnowChanger cytoplasm), sized small for perf — the
    ///     Cell owns the environment, per the locked ecosystem design;
    ///   • the REAL ToyboxController places the three built-in toys (painting / vessel
    ///     changer / domain changer) on the membrane ring, gated on the freestyle events;
    ///   • trails are REAL prisms (SkimRacePrismFactory answers the spawn channel) and
    ///     trail-skim energy flows through the REAL skimmer pipeline — conserved mass, no
    ///     caps/TTLs/cullers anywhere.
    /// </summary>
    public static class FreestyleFactory
    {
        public static (GameLoop loop, FreestyleDirector director) Create(int seed, FreestyleTuning tuning = null)
        {
            tuning ??= new FreestyleTuning();
            var loop = new GameLoop("Freestyle");
            CosmicShore.Engine.Random.InitState(seed);

            // Project layers (same table the race registers). Idempotent.
            LayerMask.SetLayerName(7, "Skimmers");
            LayerMask.SetLayerName(8, "Ships");
            LayerMask.SetLayerName(9, "Crystals");
            LayerMask.SetLayerName(10, "Explosions");
            LayerMask.SetLayerName(11, "TrailBlocks");

            // ── theme: the REAL authored palette + ThemeManager single-writer flow ──
            var theme = SkimRaceTheme.CreateContainer();
            var themeManagerGo = new GameObject("ThemeManager");
            themeManagerGo.SetActive(false);
            var themeManager = themeManagerGo.AddComponent<ThemeManager>();
            SkimRaceFactory.SetPrivateField(themeManager, "_dataContainer", theme);
            themeManagerGo.SetActive(true);

            // ── the REAL prism performance managers (PrismGrowthDriver retired): every
            // PrismScaleAnimator/MaterialPropertyAnimator registers here at Initialize ──
            new GameObject("PrismScaleManager").AddComponent<PrismScaleManager>();
            new GameObject("MaterialStateManager").AddComponent<MaterialStateManager>();

            // ── host-mode NetworkManager (the initializer chain is networked) ──
            var nm = new GameObject("network-manager").AddComponent<NetworkManager>();
            NetworkManager.Singleton = nm;
            AuthenticationService.Instance.PlayerName = "Pilot#0001";

            // ── shared game data ──
            var gameData = ScriptableObject.CreateInstance<GameDataSO>();
            gameData.ThemeManagerData = theme;
            gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
            gameData.OnVesselNetworkSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.OnShowGameEndScreen = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
            gameData.SelectedIntensity.Value = 1;
            gameData.RequestedDomainCount = GameDataSO.ActiveDomains.Length; // all 3 pickable via the domain toy

            var freestyleEvents = ScriptableObject.CreateInstance<MenuFreestyleEventsContainerSO>();
            freestyleEvents.OnGameStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            freestyleEvents.OnGameStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            freestyleEvents.OnMenuStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            freestyleEvents.OnMenuStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

            // ── the cell's runtime registry (also every AIPilot's course — the autopilot
            // orbits the cell crystal, the lava-lamp motion) ──
            var runtime = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
            SkimRaceFactory.SetPrivateField(runtime, "gameData", gameData);
            runtime.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            runtime.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            runtime.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            runtime.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();

            // ── DI container (vessel clones + toybox + ecology prefabs inject from it) ──
            var container = new Container();
            container.RegisterValue(gameData);
            container.RegisterValue(freestyleEvents);
            container.RegisterValue(new GameObject("PlayerDataService").AddComponent<PlayerDataService>());
            container.RegisterValue(new GameObject("AudioSystem").AddComponent<AudioSystem>());

            // ── input channels (shared by the player's InputStatus + every vessel's
            // action handler, exactly like the real prefab assets) ──
            var onButtonPressed = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();
            var onButtonReleased = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();

            // ── the prism pipeline: one factory answers every rig's spawn channel; the
            // skim-energy rule rides the real skimmer impactor chain (SkimRace parity) ──
            var prismFactory = new SkimRacePrismFactory(theme);
            var skimEnergyEffect = ScriptableObject.CreateInstance<SkimRaceTrailSkimEnergyEffectSO>();
            var skimmerImpactorContainer = ScriptableObject.CreateInstance<SkimmerImpactorDataContainerSO>();
            SkimRaceFactory.SetPrivateField(skimmerImpactorContainer, "skimmerPrismEffectsSO",
                new SkimmerPrismEffectSO[] { skimEnergyEffect });

            // ── vessel prefabs: the full Squirrel rig per swappable class, each with the
            // authored NetworkObject the initializer chain looks up. Only the Squirrel has
            // an embedded hull mesh — the other classes render as the dart fallback (and
            // their toy mini-models as tinted spheres), which is fine and expected. ──
            var prefabShelf = new GameObject("PrefabShelf");
            prefabShelf.SetActive(false); // inactive holder: templates never run lifecycle

            var swapClasses = new[]
            {
                VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
                VesselClassType.Squirrel, VesselClassType.Serpent, VesselClassType.Sparrow,
            };
            var prefabTransforms = new Transform[swapClasses.Length];
            for (int i = 0; i < swapClasses.Length; i++)
            {
                var template = SkimRaceFactory.BuildVesselPrefab($"{swapClasses[i]}Prefab", swapClasses[i],
                    gameData, runtime, prismFactory, skimmerImpactorContainer, onButtonPressed, onButtonReleased);
                template.AddComponent<NetworkObject>();
                template.transform.SetParent(prefabShelf.transform, false);
                template.SetActive(true); // activeSelf true; inert under the inactive shelf
                prefabTransforms[i] = template.transform;
            }
            var prefabContainer = ScriptableObject.CreateInstance<VesselPrefabContainer>();
            SkimRaceFactory.SetPrivateField(prefabContainer, "_shipPrefabs", prefabTransforms);

            // ── the living Cell (CellConfigDataSO + SpawnProfileSO, the standard wiring) ──
            var cell = BuildCell(gameData, runtime, container, theme, prismFactory, tuning, prefabShelf);

            // ── the REAL initializer trio (the Menu_Main scene layout) ──
            var spawnerGo = new GameObject("vessel-initializer");
            spawnerGo.AddComponent<NetcodeHooks>();
            var clientInit = spawnerGo.AddComponent<ClientPlayerVesselInitializer>();
            var menuInit = spawnerGo.AddComponent<MenuServerPlayerVesselInitializer>();
            var spawnerNetObj = spawnerGo.AddComponent<NetworkObject>();
            SkimRaceFactory.SetPrivateField(menuInit, "clientPlayerVesselInitializer", clientInit);
            SkimRaceFactory.SetPrivateField(menuInit, "vesselPrefabContainer", prefabContainer);
            SkimRaceFactory.SetPrivateField(clientInit, "themeManagerData", theme);
            container.InjectGameObject(spawnerGo);

            // Spawn pose: inside the membrane, aimed PAST the cell crystal at an angle —
            // never dead-on. A spawn exactly on an axis aiming through the origin target
            // is a degenerate steering case: after the flythrough the target sits exactly
            // astern (zero lateral error) and the AIPilot holds a straight line forever.
            // The offset gives every pass a lateral component, so the autopilot curls
            // into the lava-lamp orbit instead.
            var spawnPoint = new GameObject("spawnPoint0");
            spawnPoint.transform.position = new Vector3(
                tuning.MembraneRadius * 0.4f, 30f, -tuning.MembraneRadius * 0.55f);
            var aimAt = new Vector3(-80f, 10f, 60f);
            spawnPoint.transform.rotation =
                Quaternion.LookRotation((aimAt - spawnPoint.transform.position).normalized, Vector3.up);
            gameData.SetSpawnPositions(new[] { spawnPoint.transform });

            // ── the REAL local human Player (host-mode owner) ──
            var playerGo = new GameObject("player");
            var player = playerGo.AddComponent<Player>();
            SkimRaceFactory.SetPrivateField(player, "gameData", gameData);
            var inputStatus = playerGo.AddComponent<InputStatus>();
            SkimRaceFactory.SetPrivateField(inputStatus, "_onButtonPressed", onButtonPressed);
            SkimRaceFactory.SetPrivateField(inputStatus, "_onButtonReleased", onButtonReleased);

            // ── the REAL toybox (self-wires its default painting/vessel/domain/conveyor toys
            // and places them on the membrane ring at OnClientReady). The zero-config default
            // is pre-built here so the Wanderway conveyor's prism prefab — an asset reference
            // the code-built fallback can't supply — can be wired from a client-built template
            // via the definition's SetRuntimePrismPrefab hook: the belt then transports REAL
            // conserved prisms instead of degrading to crystals + lifeforms only. ──
            var toyboxGo = new GameObject("toybox-controller");
            var toybox = toyboxGo.AddComponent<ToyboxController>();
            container.InjectGameObject(toyboxGo); // [Inject] GameDataSO + MenuFreestyleEventsContainerSO
            WireConveyorPrismPrefab(toybox, prismFactory, theme, prefabShelf);

            // ── the director ──
            var directorGo = new GameObject("FreestyleDirector");
            var director = directorGo.AddComponent<FreestyleDirector>();
            director.InitializeFreestyle(gameData, freestyleEvents, cell, runtime, prismFactory,
                menuInit, spawnerNetObj, toybox, onButtonPressed, onButtonReleased);

            // One settle frame so scene Start()s (toybox subscribe, cell init hooks) run
            // BEFORE the networked spawn chain raises its events.
            loop.Tick(1f / 60f);
            gameData.InitializeGame(); // → Cell.Initialize (config + membrane + grids)

            // Auth-scene order: the Player spawns first, then the initializer object —
            // ProcessPreExistingPlayers catches the already-spawned Player.
            player.Spawn();
            spawnerNetObj.Spawn();

            // The cell wakes on its first crystal (InitilizePostFirstCellItem → modifiers
            // + cytoplasm + the REAL spawner seeding flora and fauna).
            var cellCrystal = MakeCellCrystal(runtime);
            cellCrystal.transform.position = cell.transform.position;
            runtime.AddCrystalToList(cellCrystal);

            return (loop, director);
        }

        // ── the living cell ─────────────────────────────────────────────────

        static Cell BuildCell(GameDataSO gameData, CellRuntimeDataSO runtime,
            Container container, ThemeManagerDataContainerSO theme, SkimRacePrismFactory prismFactory,
            FreestyleTuning tuning, GameObject prefabShelf)
        {
            var config = ScriptableObject.CreateInstance<CellConfigDataSO>();
            config.CellName = "Freestyle Cell";
            // Phase ladder authored in VOLUME (the spine) — squirrel trail blocks here are
            // 5×1×6 slabs (~30 vol each), so the ladder is sized in the hundreds: Restless
            // around ~30 blocks of scored mass, Frenzy around ~60. Counts are the rare
            // perf backstop, sized far above the small freestyle populations.
            config.PhaseThresholds = new CellPhaseThresholds
            {
                RestlessEnter = 400, RestlessExit = 320,
                FrenzyEnter = 800, FrenzyExit = 640,
                RestlessEnterVolume = 900f, RestlessExitVolume = 700f,
                FrenzyEnterVolume = 1800f, FrenzyExitVolume = 1500f,
            };

            // Membrane: the playfield-boundary read (toy ring placement + flora dispersal).
            var membranePrefab = new GameObject("membrane-prefab");
            var membrane = membranePrefab.AddComponent<CapsuleMembrane>();
            SkimRaceFactory.SetPrivateField(membrane, "radius", tuning.MembraneRadius);
            membranePrefab.transform.SetParent(prefabShelf.transform, false);
            config.MembranePrefab = membranePrefab;

            // Cytoplasm: the drifting atmosphere/motes (SnowChanger shard field).
            var snowShard = new GameObject("snow-shard");
            snowShard.transform.SetParent(prefabShelf.transform, false);
            var cytoplasmPrefab = new GameObject("cytoplasm-prefab");
            cytoplasmPrefab.transform.SetParent(prefabShelf.transform, false);
            var snowChanger = cytoplasmPrefab.AddComponent<SnowChanger>();
            SkimRaceFactory.SetPrivateField(snowChanger, "cellData", runtime);
            SkimRaceFactory.SetPrivateField(snowChanger, "snow", snowShard);
            SkimRaceFactory.SetPrivateField(snowChanger, "shardDistance", 150); // perf: ~90 motes
            config.CytoplasmPrefab = snowChanger;

            // The population (small counts — perf is tuned HERE, never by culling).
            config.SpawnProfile = BuildSpawnProfile(runtime, container, theme, prismFactory, tuning, prefabShelf);

            var cellGo = new GameObject("cell-1");
            cellGo.SetActive(false);
            var cell = cellGo.AddComponent<Cell>();
            cell.ID = 1;
            SkimRaceFactory.SetPrivateField(cell, "runtime", runtime);
            SkimRaceFactory.SetPrivateField(cell, "gameData", gameData);
            SkimRaceFactory.SetPrivateField(cell, "CellConfigs", new List<CellConfigDataSO> { config });
            cellGo.SetActive(true);
            return cell;
        }

        static SpawnProfileSO BuildSpawnProfile(CellRuntimeDataSO runtime, Container container,
            ThemeManagerDataContainerSO theme, SkimRacePrismFactory prismFactory,
            FreestyleTuning tuning, GameObject prefabShelf)
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfileSO>();
            profile.SupportedFloras = new List<FloraConfigurationSO>();
            profile.SupportedFaunas = new List<FaunaConfigurationSO>();
            profile.FaunaFoodFloor = 0;                // seed against the thin freestyle mass
            profile.InitialFaunaSpawnWaitTime = tuning.InitialFaunaSpawnWaitTime;
            profile.BaseFaunaSpawnTime = 30f;          // seeder cadence (top-up only)
            profile.FloraSpawnIntervalSeconds = 0.2f;  // spread the initial batch across frames

            var floraCfg = ScriptableObject.CreateInstance<FloraConfigurationSO>();
            floraCfg.FloraPrefab = BuildFloraPrefab(runtime, container, theme, prismFactory, tuning, prefabShelf);
            floraCfg.SpawnProbability = 1f;
            floraCfg.InitialSpawnCount = tuning.FloraInitialCount;
            floraCfg.OverrideDefaultPlantPeriod = true;
            floraCfg.NewPlantPeriod = 90;              // slow continuous planting
            profile.SupportedFloras.Add(floraCfg);

            var faunaCfg = ScriptableObject.CreateInstance<FaunaConfigurationSO>();
            faunaCfg.FaunaPrefab = BuildFaunaPrefab(runtime, container, theme, prismFactory, tuning, prefabShelf);
            faunaCfg.SpawnProbability = 1f;
            faunaCfg.PopulationSize = tuning.FaunaSeedFloor;
            faunaCfg.MaxLivePopulation = tuning.FaunaMaxPopulation;
            faunaCfg.FeedsPerOffspring = 6;            // prey converts to population, slowly
            faunaCfg.ReproductionCooldownSeconds = 20f;
            profile.SupportedFaunas.Add(faunaCfg);

            return profile;
        }

        /// <summary>
        /// A full HealthPrism GameObject template — the V15 prism family with HealthPrism
        /// in the Prism slot, exactly what flora leaves and fauna bodies are made of.
        /// </summary>
        static HealthPrism BuildHealthPrismTemplate(string name, SkimRacePrismFactory factory,
            GameObject parentShelfOrBody, ThemeManagerDataContainerSO theme)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parentShelfOrBody.transform, false);
            return factory.AddHealthPrismComponents(go, theme);
        }

        /// <summary>
        /// Pre-resolves the controller's zero-config default toybox (the same code-built
        /// synthesis it would run at OnClientReady — invoked here via reflection so the
        /// verbatim ToyboxController stays untouched) and wires a client-built plain-Prism
        /// template into the Wanderway conveyor definition through its upstream
        /// SetRuntimePrismPrefab hook. Without this the fallback definition has no prism
        /// prefab and the belt's scenes degrade to crystals + lifeforms only.
        /// </summary>
        static void WireConveyorPrismPrefab(ToyboxController controller,
            SkimRacePrismFactory prismFactory, ThemeManagerDataContainerSO theme, GameObject prefabShelf)
        {
            var box = (ToyboxSO)typeof(ToyboxController)
                .GetMethod("BuildDefaultToybox", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, null);

            foreach (var def in box.Toys)
            {
                if (def is not ConveyorToyDefinitionSO conveyorDef) continue;
                var template = new GameObject("conveyor-prism-prefab");
                template.transform.SetParent(prefabShelf.transform, false);
                var prism = prismFactory.AddPrismComponents(template, theme);
                typeof(ConveyorToyDefinitionSO)
                    .GetMethod("SetRuntimePrismPrefab", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(conveyorDef, new object[] { prism });
                break;
            }

            SkimRaceFactory.SetPrivateField(controller, "toybox", box);
        }

        static Flora BuildFloraPrefab(CellRuntimeDataSO runtime, Container container,
            ThemeManagerDataContainerSO theme, SkimRacePrismFactory prismFactory,
            FreestyleTuning tuning, GameObject prefabShelf)
        {
            var go = new GameObject("flora-prefab");
            go.transform.SetParent(prefabShelf.transform, false);

            // Spindle prefab (branch bone) — carries the renderer ForceWither dims.
            var spindleGo = new GameObject("spindle-prefab");
            spindleGo.transform.SetParent(prefabShelf.transform, false);
            var spindleRenderer = spindleGo.AddComponent<MeshRenderer>();
            spindleRenderer.sharedMaterial = new Material((Shader)null) { name = "spindle" };
            var spindle = spindleGo.AddComponent<Spindle>();
            spindle.RenderedObject = spindleRenderer;

            var healthPrism = BuildHealthPrismTemplate("flora-leaf-prefab", prismFactory, prefabShelf, theme);

            var flora = go.AddComponent<BranchingFlora>();
            SkimRaceFactory.SetPrivateField(flora, "autoInitialize", false);
            SkimRaceFactory.SetPrivateField(flora, "cellData", runtime);
            SkimRaceFactory.SetPrivateField(flora, "healthPrism", healthPrism);
            SkimRaceFactory.SetPrivateField(flora, "spindle", spindle);
            SkimRaceFactory.SetPrivateField(flora, "maxTotalSpawnedObjects", tuning.FloraMaxLivePrisms);
            SkimRaceFactory.SetPrivateField(flora, "maxDepth", 4);
            SkimRaceFactory.SetPrivateField(flora, "growPeriod", 4f);
            SkimRaceFactory.SetPrivateField(flora, "leafChance", 0.35f);
            container.InjectGameObject(go); // [Inject] GameDataSO/AudioSystem onto the template → clones inherit
            return flora;
        }

        static Fauna BuildFaunaPrefab(CellRuntimeDataSO runtime, Container container,
            ThemeManagerDataContainerSO theme, SkimRacePrismFactory prismFactory,
            FreestyleTuning tuning, GameObject prefabShelf)
        {
            var data = ScriptableObject.CreateInstance<LightFaunaDataSO>();
            data.detectionRadius = 150f;
            data.separationRadius = 40f;
            data.consumeRadius = tuning.FaunaConsumeRadius;
            data.behaviorUpdateRate = 1.5f;
            data.separationWeight = 60f;
            data.goalWeight = 1.5f;
            data.minSpeed = tuning.FaunaMinSpeed;
            data.maxSpeed = tuning.FaunaMaxSpeed;
            data.witherRingInterval = 0.15f;

            var go = new GameObject("fauna-prefab");
            go.transform.SetParent(prefabShelf.transform, false);

            // Body: two nested HealthPrisms (core + tail) — "fauna bodies are HealthPrisms
            // on the move". LifeForm is intentionally not assigned (LightFauna does that
            // wiring itself and keeps the body out of the cell's consumable registry).
            var core = BuildHealthPrismTemplate("body-core", prismFactory, go, theme);
            core.TargetScale = new Vector3(3f, 3f, 6f);
            var tail = BuildHealthPrismTemplate("body-tail", prismFactory, go, theme);
            tail.transform.localPosition = new Vector3(0f, 0f, -5f);
            tail.TargetScale = new Vector3(2f, 2f, 4f);

            // Authored elemental crystal child (the LifeFormCrystal fast path — every
            // lifeform drops ONE elemental crystal on death, mass conserved).
            var crystalGo = new GameObject("elemental-crystal");
            crystalGo.transform.SetParent(go.transform, false);
            crystalGo.AddComponent<SphereCollider>();
            var crystal = crystalGo.AddComponent<Crystal>();
            crystal.Initialize(9001);
            crystal.ownDomain = Domains.Blue;
            var props = crystal.crystalProperties;
            props.Element = Element.Mass;
            crystal.crystalProperties = props;
            SkimRaceFactory.SetPrivateField(crystal, "cellData", runtime);

            var fauna = go.AddComponent<LightFauna>();
            SkimRaceFactory.SetPrivateField(fauna, "data", data);
            SkimRaceFactory.SetPrivateField(fauna, "cellData", runtime);
            SkimRaceFactory.SetPrivateField(fauna, "starvationSeconds", tuning.FaunaStarvationSeconds);
            container.InjectGameObject(go);
            return fauna;
        }

        static Crystal MakeCellCrystal(CellRuntimeDataSO runtime)
        {
            // The cell's anchor crystal (SnowChanger orientation target + AIPilot goal +
            // flora crystaltropism). No impactor rig — it is scenery, not a claimable.
            var go = new GameObject("cell-crystal");
            var crystal = go.AddComponent<Crystal>();
            crystal.Initialize(1);
            crystal.ownDomain = Domains.Blue;
            SkimRaceFactory.SetPrivateField(crystal, "cellData", runtime);
            return crystal;
        }
    }

}

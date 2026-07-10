using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
// The engine's UGS session placeholder also declares an IPlayer (Unity.Services.Multiplayer
// contract) — alias the game's player interface, which is what this harness means.
using IPlayer = CosmicShore.Gameplay.IPlayer;
using Object = CosmicShore.Engine.Object;
using Random = CosmicShore.Engine.Random;

namespace CosmicShore.Cli
{
    /// <summary>Knobs for one headless AI-vs-AI HexRace-style round.</summary>
    public sealed class HexRaceRoundOptions
    {
        public int PlayerCount = 4;
        public int Seed = 42;

        /// <summary>Per-domain crystal sum that ends the race (GameDataSO.CrystalTargetCount).</summary>
        public int CrystalTarget = 15;

        /// <summary>Fail-loud frame cap (default 10 simulated minutes @ 60 Hz).</summary>
        public int MaxFrames = 60 * 60 * 10;

        public float DeltaTime = 1f / 60f;

        /// <summary>
        /// Center-to-center distance (world units) at which a vessel claims the active
        /// crystal. Since the contact arc (CT1) this is realized through real trigger
        /// physics, not a proximity referee: the crystal carries a trigger SphereCollider
        /// of radius <c>ClaimRadius - VesselContactRadius</c> and each vessel a
        /// non-trigger SphereCollider of radius <see cref="VesselContactRadius"/>, so the
        /// engine's trigger pass fires OnTriggerEnter when the centers close to exactly
        /// this distance.
        /// </summary>
        public float ClaimRadius = 25f;

        /// <summary>Radius of the vessel's contact-bubble SphereCollider (world units).</summary>
        public float VesselContactRadius = 4f;
    }

    /// <summary>One row of the final standings (derived from GameDataSO.Results + RoundStats).</summary>
    public sealed class HexRaceStanding
    {
        public int Rank;
        public string Name;
        public Domains Domain;
        public int Crystals;
        public float Score;
        public string ScoreText;
        public string Secondary;
    }

    public sealed class HexRaceRoundResult
    {
        public bool Finished;
        public string WinnerName = "";
        public Domains WinnerDomain = Domains.Blue;
        public float FinishTime;
        public int FramesSimulated;
        public int TotalClaims;

        /// <summary>Deterministic line-by-line log: claims, finish, standings. Same seed → identical list.</summary>
        public List<string> Transcript = new();

        public List<HexRaceStanding> Standings = new();

        /// <summary>Error/Exception entries captured from the engine log during the round (expected empty).</summary>
        public List<string> EngineErrors = new();
    }

    /// <summary>No-op HUD controller for the prefab fixture's serialized slot (headless).</summary>
    sealed class RoundVesselHud : MonoBehaviour, IVesselHUDController
    {
        public void Initialize(IVesselStatus vesselStatus) { }
        public void SubscribeToEvents() { }
        public void UnsubscribeFromEvents() { }
        public void ShowHUD() { }
        public void HideHUD() { }
        public void SetBlockPrefab(GameObject prefab) { }
    }

    /// <summary>Concrete VesselAnimation (base is abstract) — no-op puppetry.</summary>
    sealed class RoundVesselAnimation : Gameplay.VesselAnimation
    {
        protected override void AssignTransforms() { }
        protected override void PerformShipPuppetry(float Pitch, float Yaw, float Roll, float Throttle) { }
    }

    /// <summary>
    /// The round's CrystalManager-family manager (rung 3): every Crystal routes its
    /// lifecycle through the real Crystal → CrystalManager chain, so the harness wires
    /// one in (Crystal.NotifyManagerToExplodeCrystal reaches it from inside
    /// OmniCrystalImpactor.ExecuteEffect for every non-Manta claim). The waypoint course
    /// never relocates a crystal in place — the harness stages the NEXT waypoint after a
    /// claim — so crystals carry allowRespawnOnImpact = false (Respawn → DestroyCrystal)
    /// and RespawnCrystal is unreachable.
    /// </summary>
    sealed class HexRaceCrystalManager : CrystalManager
    {
        public override void RespawnCrystal(int crystalId) { }

        public override void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams)
        {
            if (!cellData.TryGetCrystalById(crystalId, out var crystal)) return;
            if (crystal != null)
                crystal.Explode(explodeParams);
        }
    }

    /// <summary>
    /// A LIVE HexRace round the caller steps one engine frame at a time (Arc G: the
    /// windowed mode host drives this from its render loop; the CLI's blocking
    /// <see cref="HexRaceRound.Run"/> is the same handle stepped in a while-loop).
    /// Owns the round's GameLoop and every fixture <see cref="HexRaceRound.Setup"/>
    /// built; <see cref="Dispose"/> performs the exact teardown the CLI's finally
    /// block always did (AI/spawn wind-down, cell unregister, destroy flush, singleton
    /// resets, log-sink restore + EngineErrors flush) and then disposes the loop.
    /// </summary>
    public sealed class HexRaceRoundHandle : IRoundDriver
    {
        internal HexRaceRoundOptions options;
        internal Action<string> liveLog;
        internal HexRaceRoundResult result;

        internal GameLoop loop;
        internal CapturingLogSink capturedLog;
        internal ILogSink previousSink;

        internal GameObject cellHost;
        internal GameDataSO gameData;
        internal CellRuntimeDataSO courseData;
        internal CrystalManager crystalManager;
        internal ScoringRuleSO rule;
        internal List<IPlayer> players;

        internal Vector3[] coursePositions;
        internal Element[] courseElements;
        internal int courseIndex;
        internal Crystal activeCrystal;
        internal ScriptableEventCrystalStats onCrystalCollected;
        internal float crystalTriggerRadius;

        internal int target;
        internal float raceStart;
        internal int frames;
        internal bool objectiveReached;
        internal Domains objectiveDomain = Domains.Blue;

        bool _steppingCompleted;
        bool _finished;
        bool _disposed;

        // ── IRoundDriver (world view for a rendering host) ──────────────────
        public string GameLabel => "HEX RACE";
        public string ScoringLabel => "golf rules - lower is better";
        public HexRaceRoundOptions Options => options;
        public HexRaceRoundResult Result => result;
        public GameDataSO GameData => gameData;
        public IReadOnlyList<IPlayer> Players => players;
        public Crystal ActiveCrystal => activeCrystal;
        public Vector3[] Course => coursePositions;
        public Element[] CourseElements => courseElements;
        public int CourseIndex => courseIndex;
        public int Target => target;
        public float RaceStartTime => raceStart;
        public int FramesStepped => frames;
        public bool ObjectiveReached => objectiveReached;
        public Domains ObjectiveDomain => objectiveDomain;
        public int MaxFrames => options.MaxFrames;
        public bool Live => true;                 // the race clock runs from Setup
        public float ClockStart => raceStart;
        public bool Finished => result.Finished;
        public string WinnerName => result.WinnerName;
        public Domains WinnerDomain => result.WinnerDomain;
        public int TotalClaims => result.TotalClaims;
        public IEnumerable<(int Rank, string Name, Domains Domain, int Crystals, string ScoreText)> StandingRows
        {
            get
            {
                foreach (var s in result.Standings)
                    yield return (s.Rank, s.Name, s.Domain, s.Crystals, s.ScoreText);
            }
        }
        public int DomainScore(Domains domain) => ScoringMetrics.SumByDomain(gameData, rule.Metric, domain);
        public int PlayerScore(IPlayer player) => player.RoundStats.CrystalsCollected;
        public bool AutoReady { get; set; } = true; // HexRace has no ready flow — unused
        public bool ReadyPending => false;
        public void ClickReady() { }

        internal void Log(string line)
        {
            result.Transcript.Add(line);
            liveLog?.Invoke(line);
        }

        /// <summary>
        /// One engine frame: tick (claims happen inside — trigger pass → impactor
        /// dispatch), then the turn-monitor-shaped objective check. Returns true the
        /// frame some domain reaches the target.
        /// </summary>
        public bool StepFrame()
        {
            loop.Tick(options.DeltaTime);
            frames++;

            if (rule.IsObjectiveReached(gameData, out objectiveDomain))
            {
                objectiveReached = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ends the stepping phase (idempotent): detaches the claim observer and stamps
        /// FramesSimulated — exactly what the CLI loop did between its while-loop and
        /// the timeout/score branch.
        /// </summary>
        public void CompleteStepping()
        {
            if (_steppingCompleted) return;
            _steppingCompleted = true;
            onCrystalCollected.OnRaised -= HandleCrystalCollected;
            result.FramesSimulated = frames;
        }

        /// <summary>
        /// Finish: resolve, score (golf), publish results, log standings. No-op unless
        /// the objective was reached (a timed-out round has nothing to score).
        /// </summary>
        public void FinishAndScore()
        {
            CompleteStepping();
            if (_finished || !objectiveReached) return;
            _finished = true;

            float finishTime = Time.time - raceStart;
            var winnerDomain = rule.ResolveWinner(gameData);   // == objectiveDomain (highest sum, Jade→Ruby→Gold ties)
            rule.AssignScores(gameData, winnerDomain, finishTime);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeGameTurnConditionsMet();
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();

            result.Finished = true;
            result.FinishTime = finishTime;
            result.WinnerName = gameData.WinnerName;
            result.WinnerDomain = gameData.WinnerDomain;

            Log("");
            Log($"OBJECTIVE — {objectiveDomain} domain reached {target} crystals in {HexRaceRound.F(finishTime)}s ({frames} frames).");
            Log($"WINNER    — {gameData.WinnerName} ({gameData.WinnerDomain}), representative of the winning domain.");
            Log("");
            Log("STANDINGS (golf rules — lower score is better):");

            var crystalsByName = gameData.RoundStatsList.ToDictionary(s => (string)s.Name, s => s.CrystalsCollected);
            foreach (var row in gameData.Results)
            {
                var standing = new HexRaceStanding
                {
                    Rank = row.Rank,
                    Name = row.Name,
                    Domain = row.Domain,
                    Crystals = crystalsByName.TryGetValue(row.Name, out var c) ? c : 0,
                    Score = row.Score,
                    ScoreText = row.ScoreText,
                    Secondary = row.Secondary,
                };
                result.Standings.Add(standing);
                Log($"  #{standing.Rank} {standing.Name,-6} {standing.Domain,-5} {standing.Crystals,2} crystals  score={HexRaceRound.F(standing.Score),9}  {standing.ScoreText}");
            }
        }

        // Claim observer — fires from INSIDE the trigger pass, at the end of the
        // genuine OnTriggerEnter → ImpactorBase.AcceptImpactee → ExecuteEffect
        // chain on the crystal's OmniCrystalImpactor. The harness applies the
        // StatsManager-shaped bookkeeping (RoundStats + elemental progression),
        // logs the claim at the contact instant (authentic photo-finish gap), and
        // stages the next waypoint. The crystal then removes itself: AcceptImpactee
        // continues into Crystal.Respawn() → DestroyCrystal() →
        // courseData.TryRemoveItem (pilots retarget) → Destroy(gameObject).
        internal void HandleCrystalCollected(CrystalStats stats)
        {
            result.TotalClaims++;
            var claimant = players.First(p => p.Name == stats.PlayerName);
            HexRaceRound.ApplyCrystalPickup(claimant, stats.Element);

            // Closest rival's distance to the crystal at claim time — the "photo finish" gap.
            float rivalSqr = float.PositiveInfinity;
            foreach (var other in players)
            {
                if (ReferenceEquals(other, claimant)) continue;
                var d2 = (other.Vessel.Transform.position - activeCrystal.transform.position).sqrMagnitude;
                if (d2 < rivalSqr) rivalSqr = d2;
            }
            string gap = players.Count > 1 ? $" (rival {HexRaceRound.F(Mathf.Sqrt(rivalSqr))}u behind)" : "";

            Log($"[t={HexRaceRound.F(Time.time - raceStart),7}s] {claimant.Name} ({claimant.Domain}) claims crystal #{courseIndex + 1} [{stats.Element}]{gap} — " +
                $"Jade {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Jade)} · " +
                $"Ruby {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Ruby)} · " +
                $"Gold {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Gold)}");

            // Stage the next waypoint crystal unless the race just ended.
            if (!rule.IsObjectiveReached(gameData, out _) && courseIndex + 1 < coursePositions.Length)
            {
                courseIndex++;
                activeCrystal = HexRaceRound.SpawnCrystal(courseData, crystalManager, coursePositions, courseIndex,
                    courseElements[courseIndex], crystalTriggerRadius, onCrystalCollected);
            }
            else
            {
                activeCrystal = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Stop AI/spawn loops + flush destroys so the loop (and process) can wind down.
            if (gameData != null)
            {
                foreach (var p in gameData.Players.ToList())
                {
                    if (p?.Vessel is VesselController vc)
                        vc.VesselStatus.VesselPrismController.StopSpawn();
                    p?.ResetForPlay(); // AI path: toggles the AIPilot OFF
                }
            }
            if (cellHost != null)
                cellHost.SetActive(false); // unregisters Cell.ActiveCells + resets the course registry
            loop?.Tick(options.DeltaTime); // end-of-frame destroy flush

            typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
            typeof(AudioSystem).GetProperty("Instance")!.SetValue(null, null); // shell singleton (Awake-set)
            NetworkManager.Singleton = null;
            Debug.Sink = previousSink;

            foreach (var entry in capturedLog.Entries)
                if (entry.Type is LogType.Error or LogType.Exception)
                    result.EngineErrors.Add($"{entry.Type}: {entry.Message}");

            loop?.Dispose();
        }
    }

    /// <summary>
    /// Milestone port-m2 orchestration harness: the first full headless game-mode round.
    /// An AI field races to a crystal target through the verbatim ported systems —
    /// PlayerSpawner/VesselSpawner (C6) spawn the AI player+vessel pairs, AIPilot does the
    /// real crystal seeking (CellRuntimeDataSO.OnCellItemsUpdated → UpdateCellContent →
    /// IInputStatus writes → VesselTransformer flight), a Cell (V12) + CellRuntimeDataSO (V11)
    /// host the seeded crystal course, RoundStats/ResourceSystem record progression, and
    /// HexRaceScoringRuleSO ends the race (domain-aggregated), assigns golf scores and builds
    /// the ranked ScoreResults published via GameDataSO.SetResults.
    ///
    /// Since the contact arc (CT1), claims flow through the REAL impact pipeline: each
    /// crystal carries a trigger SphereCollider + OmniCrystalImpactor + ImpactCollider,
    /// each vessel a contact-bubble SphereCollider + VesselImpactor (+
    /// NetworkVesselImpactor) + ImpactCollider, and the engine's per-frame trigger pass
    /// drives OnTriggerEnter → ImpactorBase dispatch → OmniCrystalImpactor.AcceptImpactee,
    /// which raises the crystal-stats SOAP event and destroys/removes the crystal itself
    /// (Respawn → DestroyCrystal → CellRuntimeDataSO.TryRemoveItem). No game logic lives
    /// here — only construction, wiring, and a claim observer that applies RoundStats /
    /// elemental progression (the StatsManager role) and writes the transcript.
    ///
    /// Arc G split the harness into <see cref="Setup"/> (world construction) +
    /// <see cref="HexRaceRoundHandle.StepFrame"/> (one engine frame) +
    /// <see cref="HexRaceRoundHandle.FinishAndScore"/> + Dispose, so the windowed mode
    /// host can drive the SAME round from its render loop. <see cref="Run"/> is that
    /// handle stepped in a blocking while-loop — transcript and results are unchanged.
    /// </summary>
    public static class HexRaceRound
    {
        // ── reflection stand-in for inspector wiring of serialized fields ─────

        static void SetPrivateField(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) continue;
                f.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }

        internal static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        // ── round entry points ─────────────────────────────────────────────────

        /// <summary>The CLI's blocking round: Setup → step to objective/timeout → score.</summary>
        public static HexRaceRoundResult Run(HexRaceRoundOptions options, Action<string> liveLog = null)
        {
            using var handle = Setup(options, liveLog);

            while (handle.frames < handle.options.MaxFrames)
            {
                if (handle.StepFrame())
                    break;
            }

            handle.CompleteStepping();

            if (!handle.objectiveReached)
            {
                handle.Log($"TIMEOUT — no domain reached {handle.target} crystals within {handle.frames} frames.");
                return handle.result;
            }

            handle.FinishAndScore();
            return handle.result;
        }

        /// <summary>
        /// Builds the full round world (loop, cell, course, prefabs, AI field) and
        /// stages the first crystal — everything the CLI round did before its frame
        /// loop. The caller owns the returned handle: step it, finish it, dispose it.
        /// </summary>
        public static HexRaceRoundHandle Setup(HexRaceRoundOptions options, Action<string> liveLog = null)
        {
            options ??= new HexRaceRoundOptions();
            int playerCount = Mathf.Clamp(options.PlayerCount, 1, 12);
            int target = Mathf.Max(1, options.CrystalTarget);

            var handle = new HexRaceRoundHandle
            {
                options = options,
                liveLog = liveLog,
                result = new HexRaceRoundResult(),
                target = target,
            };

            // Engine log → capture (keeps the transcript deterministic and quiet);
            // Error/Exception entries are surfaced on the result at Dispose.
            handle.capturedLog = new CapturingLogSink();
            handle.previousSink = Debug.Sink;
            Debug.Sink = handle.capturedLog;

            handle.loop = new GameLoop("HexRaceRound");
            NetworkManager.Singleton = null;
            Random.InitState(options.Seed);

            try
            {
                // ── shared data: theme + GameDataSO + scoring rule ────────────
                var theme = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
                theme.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet>();
                foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold })
                {
                    var set = ScriptableObject.CreateInstance<SO_MaterialSet>();
                    set.ShipMaterial = new Material((Shader)null) { name = $"{domain}-ship" };
                    set.BlockSilhouettePrefab = new GameObject($"{domain}-silhouette");
                    theme.TeamMaterialSets[domain] = set;
                }

                var gameData = ScriptableObject.CreateInstance<GameDataSO>();
                handle.gameData = gameData;
                gameData.ThemeManagerData = theme;
                gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnPlayerNetworkSpawnedUlong = ScriptableObject.CreateInstance<ScriptableEventUlong>();
                gameData.OnVesselNetworkSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
                gameData.selectedVesselClass.Value = VesselClassType.Sparrow;
                gameData.GameMode = GameModes.HexRace;
                gameData.CrystalTargetCount = target;       // what NetworkCrystalCollisionTurnMonitor publishes
                gameData.RequestedDomainCount = Mathf.Min(3, playerCount);
                gameData.ScoringRule = ScriptableObject.CreateInstance<HexRaceScoringRuleSO>();

                // ── the cell + course registry (V11/V12) ──────────────────────
                var sharedCellItemsEvent = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                var courseData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
                handle.courseData = courseData;
                SetPrivateField(courseData, "gameData", gameData);
                courseData.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCellItemsUpdated = sharedCellItemsEvent;
                courseData.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();

                // Crystal lifecycle manager — the real Crystal → CrystalManager chain (rung 3).
                var crystalManagerGo = new GameObject("hexrace-crystal-manager");
                crystalManagerGo.SetActive(false); // configure-before-activation
                var crystalManager = crystalManagerGo.AddComponent<HexRaceCrystalManager>();
                handle.crystalManager = crystalManager;
                SetPrivateField(crystalManager, "cellData", courseData);
                crystalManagerGo.SetActive(true);

                var cellConfig = ScriptableObject.CreateInstance<CellConfigDataSO>();
                cellConfig.CellName = "HexRaceCourse";
                cellConfig.SenseRadiusOverride = 300f; // keeps the density grids small; the course registry is the SO, not ContainsPosition

                handle.cellHost = new GameObject("hexrace-cell");
                handle.cellHost.SetActive(false);
                var cell = handle.cellHost.AddComponent<Cell>();
                cell.ID = 1;
                SetPrivateField(cell, "runtime", courseData);
                SetPrivateField(cell, "gameData", gameData);
                SetPrivateField(cell, "CellConfigs", new List<CellConfigDataSO> { cellConfig });
                handle.cellHost.SetActive(true);

                gameData.InitializeGame(); // OnInitializeGame → Cell.Initialize binds courseData.Cell + builds grids

                // ── seeded crystal course ─────────────────────────────────────
                // Max claims before some domain reaches the target: (target-1)*3 + 1.
                int courseLength = target * 3;
                handle.coursePositions = GenerateCourse(courseLength);
                handle.courseElements = new Element[courseLength];
                for (int i = 0; i < courseLength; i++)
                    handle.courseElements[i] = RollElement(i);

                // ── prefabs + spawners (verbatim C6 pipeline) ─────────────────
                var vesselTemplate = BuildVesselPrefab("SparrowPrefab", VesselClassType.Sparrow, gameData,
                    courseData, sharedCellItemsEvent, options.VesselContactRadius);
                var prefabContainer = ScriptableObject.CreateInstance<VesselPrefabContainer>();
                SetPrivateField(prefabContainer, "_shipPrefabs", new[] { vesselTemplate.transform });

                var playerPrefabGo = new GameObject("PlayerPrefab");
                var playerPrefab = playerPrefabGo.AddComponent<Player>();
                SetPrivateField(playerPrefab, "gameData", gameData);

                var container = new Container();
                container.RegisterValue(gameData);
                var playerDataService = new GameObject("PlayerDataService").AddComponent<PlayerDataService>();
                container.RegisterValue(playerDataService);
                // VesselImpactor's [Inject] AudioSystem must resolve when VesselSpawner
                // DI-injects the cloned vessel. The rig wires the scene's full audio
                // singleton (GameSetting + mixer + sources) so the real Start runs clean.
                container.RegisterValue(AudioSystemRig.Create());

                var spawnerGo = new GameObject("Spawners");
                var vesselSpawner = spawnerGo.AddComponent<VesselSpawner>();
                SetPrivateField(vesselSpawner, "vesselPrefabContainer", prefabContainer);
                var playerSpawner = spawnerGo.AddComponent<PlayerSpawner>();
                SetPrivateField(playerSpawner, "_playerPrefab", playerPrefab);
                SetPrivateField(playerSpawner, "vesselSpawner", vesselSpawner);
                container.InjectGameObject(spawnerGo);

                // Flat spawn line at z=0 facing +Z (the course starts down +Z).
                var spawnTransforms = new Transform[playerCount];
                for (int i = 0; i < playerCount; i++)
                {
                    var point = new GameObject($"spawnPoint{i}");
                    point.transform.position = new Vector3((i - (playerCount - 1) * 0.5f) * 40f, 0f, 0f);
                    spawnTransforms[i] = point.transform;
                }
                gameData.SetSpawnPositions(spawnTransforms);

                // ── spawn the AI field through PlayerSpawner.SpawnPlayerAndShip ──
                handle.players = new List<IPlayer>(playerCount);
                for (int i = 0; i < playerCount; i++)
                {
                    var player = playerSpawner.SpawnPlayerAndShip(new IPlayer.InitializeData
                    {
                        vesselClass = VesselClassType.Sparrow,
                        PlayerName = $"AI-{i + 1}",
                        AvatarId = 0,
                        AllowSpawning = true,
                        IsAI = true,
                    });
                    if (player == null)
                        throw new InvalidOperationException($"PlayerSpawner failed to spawn AI player {i + 1}.");

                    // Balanced domains over GameDataSO.ActiveDomains — same Jade→Ruby→Gold
                    // tie-break order ServerPlayerVesselInitializerWithAI.GetBalancedDomain uses.
                    var domain = GameDataSO.ActiveDomains[i % GameDataSO.ActiveDomains.Length];
                    ((Player)player).SetDomain(domain);
                    player.RoundStats.Domain = domain;

                    gameData.AddPlayer(player); // registers RoundStats + assigns a seeded spawn pose

                    var vesselGo = ((VesselController)player.Vessel).gameObject;
                    vesselGo.SetActive(true);   // clones mirror the inactive prefab root — activate into the scene
                    player.StartPlayer();       // AI path: vessel un-stationed, AIPilot ON, input paused

                    // Known trap (C4/C6): end the async prism spawn loop so the process can exit.
                    ((VesselController)player.Vessel).VesselStatus.VesselPrismController.StopSpawn();

                    handle.players.Add(player);
                }

                // ── race start ────────────────────────────────────────────────
                gameData.StartTurn();
                handle.raceStart = gameData.TurnStartTime;
                handle.rule = gameData.ScoringRule;

                // Contact rig sizing: claim happens at center distance == ClaimRadius
                // (crystal trigger radius + vessel contact-bubble radius).
                handle.crystalTriggerRadius =
                    Mathf.Max(0.5f, options.ClaimRadius - options.VesselContactRadius);

                handle.onCrystalCollected = ScriptableObject.CreateInstance<ScriptableEventCrystalStats>();
                handle.onCrystalCollected.OnRaised += handle.HandleCrystalCollected;
                handle.activeCrystal = SpawnCrystal(courseData, crystalManager, handle.coursePositions,
                    handle.courseIndex, handle.courseElements[handle.courseIndex],
                    handle.crystalTriggerRadius, handle.onCrystalCollected);

                return handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        // ── course generation (seeded) ─────────────────────────────────────────

        static Vector3[] GenerateCourse(int count)
        {
            var positions = new Vector3[count];
            var position = new Vector3(0f, 0f, 220f);
            var direction = new Vector3(0f, 0f, 1f);
            for (int i = 0; i < count; i++)
            {
                positions[i] = position;
                float yaw = Random.Range(-35f, 35f);
                float pitch = Random.Range(-12f, 12f);
                direction = (Quaternion.Euler(pitch, yaw, 0f) * direction).normalized;
                position += direction * Random.Range(150f, 230f);
            }
            return positions;
        }

        /// <summary>Same elemental cadence the SkimRace sim uses: every 7th station is Omni.</summary>
        static Element RollElement(int index)
        {
            if (index % 7 == 6) return Element.Omni;
            float roll = Random.Range(0f, 1f);
            if (roll < 0.40f) return Element.Charge;
            if (roll < 0.65f) return Element.Mass;
            if (roll < 0.85f) return Element.Space;
            return Element.Time;
        }

        /// <summary>
        /// Waypoint crystal with the real contact rig: trigger SphereCollider (the claim
        /// surface) + OmniCrystalImpactor (any-domain collection) + ImpactCollider routing
        /// the engine trigger pass into the impactor dispatch. The crystal removes itself
        /// on collection (Respawn → DestroyCrystal → TryRemoveItem), so the harness only
        /// observes the OnCrystalCollected SOAP event.
        /// </summary>
        internal static Crystal SpawnCrystal(CellRuntimeDataSO courseData, CrystalManager crystalManager,
            Vector3[] course, int index,
            Element element, float triggerRadius, ScriptableEventCrystalStats onCrystalCollected)
        {
            var go = new GameObject($"crystal-{index + 1}");
            go.transform.position = course[index];

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = triggerRadius;

            var crystal = go.AddComponent<Crystal>();
            crystal.Initialize(index + 1);
            crystal.ownDomain = Domains.Blue;   // uncommitted — every domain's AI may seek it
            crystal.ItemType = ItemType.Buff;
            crystal.crystalProperties = new CrystalProperties
            {
                crystal = crystal,
                Element = element,
                crystalValue = 1f,
            };
            SetPrivateField(crystal, "cellData", courseData); // DestroyCrystal → TryRemoveItem
            crystal.InjectDependencies(crystalManager);        // NotifyManagerToExplodeCrystal → manager

            var impactor = go.AddComponent<OmniCrystalImpactor>(); // Awake binds impactor.Crystal
            SetPrivateField(impactor, "OnCrystalCollected", onCrystalCollected);

            var impactCollider = go.AddComponent<ImpactCollider>();
            SetPrivateField(impactCollider, "impactorObject", impactor);

            courseData.AddCrystalToList(crystal); // raises OnCellItemsUpdated → AIPilots retarget
            return crystal;
        }

        /// <summary>RoundStats + ResourceSystem elemental progression, mirroring the SkimRace sim.</summary>
        internal static void ApplyCrystalPickup(IPlayer claimant, Element element)
        {
            var stats = claimant.RoundStats;
            stats.CrystalsCollected++;

            var resources = ((VesselController)claimant.Vessel).VesselStatus.ResourceSystem;
            if (element == Element.Omni)
            {
                stats.OmniCrystalsCollected++;
                resources.IncrementLevel(Element.Charge);
                resources.IncrementLevel(Element.Mass);
                resources.IncrementLevel(Element.Space);
                resources.IncrementLevel(Element.Time);
            }
            else
            {
                stats.ElementalCrystalsCollected++;
                resources.IncrementLevel(element);
                resources.ChangeResourceAmount(0, element == Element.Charge ? 0.3f : 0.15f);
            }
        }

        // ── vessel prefab fixture (CLI section [6] builder + shared course wiring) ──

        /// <summary>
        /// Same programmatic vessel "prefab" as the CLI smoke section [6], with two
        /// round-specific differences: every AIPilot shares the round's
        /// <see cref="CellRuntimeDataSO"/> and its OnCellItemsUpdated channel, so a crystal
        /// registered in the course registry retargets the whole AI field; and the vessel
        /// carries the contact rig (CT1) — a non-trigger contact-bubble SphereCollider,
        /// VesselImpactor (+ empty effect container) with its NetworkVesselImpactor pair,
        /// and an ImpactCollider — so crystal trigger contacts dispatch through the real
        /// impact pipeline on both sides.
        /// </summary>
        static GameObject BuildVesselPrefab(string name, VesselClassType vesselType, GameDataSO gameData,
            CellRuntimeDataSO cellData, ScriptableEventNoParam onCellItemsUpdated, float contactRadius)
        {
            var go = new GameObject(name);
            go.SetActive(false);

            go.AddComponent<VesselPrismController>();

            var resources = go.AddComponent<Gameplay.ResourceSystem>();
            resources.Resources = new List<Gameplay.Resource> { new() { Name = "Energy" } };

            go.AddComponent<VesselTransformer>();

            var pilot = go.AddComponent<AIPilot>();
            SetPrivateField(pilot, "cellData", cellData);
            SetPrivateField(pilot, "OnCellItemsUpdated", onCellItemsUpdated);
            SetPrivateField(pilot, "abilities", new List<AIAbility>());

            go.AddComponent<SilhouetteController>();

            var cameraCustomizer = go.AddComponent<VesselCameraCustomizer>();
            SetPrivateField(cameraCustomizer, "OnInitializePlayerCamera",
                ScriptableObject.CreateInstance<ScriptableEventTransform>());

            go.AddComponent<RoundVesselAnimation>();

            var actionHandler = go.AddComponent<R_VesselActionHandler>();
            SetPrivateField(actionHandler, "_onButtonPressed", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_onButtonReleased", ScriptableObject.CreateInstance<ScriptableEventInputEvents>());
            SetPrivateField(actionHandler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());

            var geometry = new GameObject("geometry");
            geometry.transform.SetParent(go.transform);
            var customization = go.AddComponent<VesselCustomization>();
            SetPrivateField(customization, "_shipGeometries", new List<GameObject> { geometry });

            go.AddComponent<R_ShipElementStatsHandler>();

            var hud = go.AddComponent<RoundVesselHud>();
            var controller = go.AddComponent<VesselController>();
            SetPrivateField(controller, "gameData", gameData);
            var status = go.AddComponent<VesselStatus>();

            Skimmer NewChildSkimmer(string skimmerName)
            {
                var child = new GameObject(skimmerName);
                child.transform.SetParent(go.transform);
                var skimmer = child.AddComponent<Skimmer>();
                SetPrivateField(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
                return skimmer;
            }

            var orientationHandle = new GameObject("orientationHandle");
            orientationHandle.transform.SetParent(go.transform);

            SetPrivateField(status, "_shipInstance", controller);
            SetPrivateField(status, "vesselHUDController", hud);
            SetPrivateField(status, "orientationHandle", orientationHandle);
            SetPrivateField(status, "_name", name);
            SetPrivateField(status, "vesselType", vesselType);
            SetPrivateField(status, "_nearFieldSkimmer", NewChildSkimmer("nearFieldSkimmer"));
            SetPrivateField(status, "_farFieldSkimmer", NewChildSkimmer("farFieldSkimmer"));

            // Contact rig (CT1): non-trigger contact bubble + impactor routing. Crystal
            // triggers pair with this collider in the engine trigger pass; the crystal's
            // OmniCrystalImpactor resolves the vessel through this ImpactCollider, while
            // the vessel's own VesselImpactor runs the (empty here) vessel-side crystal
            // effects. The E16 clone remap rewrites the intra-prefab references per clone.
            var contactBubble = go.AddComponent<SphereCollider>();
            contactBubble.radius = contactRadius;

            var networkVesselImpactor = go.AddComponent<NetworkVesselImpactor>();
            var vesselImpactor = go.AddComponent<VesselImpactor>();
            SetPrivateField(vesselImpactor, "vesselImpactorDataContainerSO",
                ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>());
            SetPrivateField(vesselImpactor, "networkVesselImpactor", networkVesselImpactor);
            SetPrivateField(networkVesselImpactor, "vesselImpactor", vesselImpactor);

            var impactCollider = go.AddComponent<ImpactCollider>();
            SetPrivateField(impactCollider, "impactorObject", vesselImpactor);

            return go;
        }
    }
}

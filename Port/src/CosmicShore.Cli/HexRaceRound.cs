using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
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

        /// <summary>Proximity radius (world units) at which a vessel claims the active crystal.</summary>
        public float ClaimRadius = 25f;
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
    /// Milestone port-m2 orchestration harness: the first full headless game-mode round.
    /// An AI field races to a crystal target through the verbatim ported systems —
    /// PlayerSpawner/VesselSpawner (C6) spawn the AI player+vessel pairs, AIPilot does the
    /// real crystal seeking (CellRuntimeDataSO.OnCellItemsUpdated → UpdateCellContent →
    /// IInputStatus writes → VesselTransformer flight), a Cell (V12) + CellRuntimeDataSO (V11)
    /// host the seeded crystal course, RoundStats/ResourceSystem record progression, and
    /// HexRaceScoringRuleSO ends the race (domain-aggregated), assigns golf scores and builds
    /// the ranked ScoreResults published via GameDataSO.SetResults. No game logic lives here —
    /// only construction, wiring, and the per-frame proximity-claim referee that stands in
    /// for the un-ported collider/impactor pipeline.
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

        static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        // ── round entry point ──────────────────────────────────────────────────

        public static HexRaceRoundResult Run(HexRaceRoundOptions options, Action<string> liveLog = null)
        {
            options ??= new HexRaceRoundOptions();
            int playerCount = Mathf.Clamp(options.PlayerCount, 1, 12);
            int target = Mathf.Max(1, options.CrystalTarget);

            var result = new HexRaceRoundResult();
            void Log(string line)
            {
                result.Transcript.Add(line);
                liveLog?.Invoke(line);
            }

            // Engine log → capture (keeps the transcript deterministic and quiet);
            // Error/Exception entries are surfaced on the result afterwards.
            var capturedLog = new CapturingLogSink();
            var previousSink = Debug.Sink;
            Debug.Sink = capturedLog;

            using var loop = new GameLoop("HexRaceRound");
            NetworkManager.Singleton = null;
            Random.InitState(options.Seed);

            GameObject cellHost = null;
            GameDataSO gameData = null;
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

                gameData = ScriptableObject.CreateInstance<GameDataSO>();
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
                SetPrivateField(courseData, "gameData", gameData);
                courseData.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                courseData.OnCellItemsUpdated = sharedCellItemsEvent;
                courseData.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();

                var cellConfig = ScriptableObject.CreateInstance<CellConfigDataSO>();
                cellConfig.CellName = "HexRaceCourse";
                cellConfig.SenseRadiusOverride = 300f; // keeps the density grids small; the course registry is the SO, not ContainsPosition

                cellHost = new GameObject("hexrace-cell");
                cellHost.SetActive(false);
                var cell = cellHost.AddComponent<Cell>();
                cell.ID = 1;
                SetPrivateField(cell, "runtime", courseData);
                SetPrivateField(cell, "gameData", gameData);
                SetPrivateField(cell, "CellConfigs", new List<CellConfigDataSO> { cellConfig });
                cellHost.SetActive(true);

                gameData.InitializeGame(); // OnInitializeGame → Cell.Initialize binds courseData.Cell + builds grids

                // ── seeded crystal course ─────────────────────────────────────
                // Max claims before some domain reaches the target: (target-1)*3 + 1.
                int courseLength = target * 3;
                var coursePositions = GenerateCourse(courseLength);
                var courseElements = new Element[courseLength];
                for (int i = 0; i < courseLength; i++)
                    courseElements[i] = RollElement(i);

                // ── prefabs + spawners (verbatim C6 pipeline) ─────────────────
                var vesselTemplate = BuildVesselPrefab("SparrowPrefab", VesselClassType.Sparrow, gameData,
                    courseData, sharedCellItemsEvent);
                var prefabContainer = ScriptableObject.CreateInstance<VesselPrefabContainer>();
                SetPrivateField(prefabContainer, "_shipPrefabs", new[] { vesselTemplate.transform });

                var playerPrefabGo = new GameObject("PlayerPrefab");
                var playerPrefab = playerPrefabGo.AddComponent<Player>();
                SetPrivateField(playerPrefab, "gameData", gameData);

                var container = new Container();
                container.RegisterValue(gameData);
                var playerDataService = new GameObject("PlayerDataService").AddComponent<PlayerDataService>();
                container.RegisterValue(playerDataService);

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
                var players = new List<IPlayer>(playerCount);
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

                    players.Add(player);
                }

                // ── race ──────────────────────────────────────────────────────
                int courseIndex = 0;
                var activeCrystal = SpawnCrystal(courseData, coursePositions, courseIndex);

                gameData.StartTurn();
                float raceStart = gameData.TurnStartTime;
                float claimRadiusSqr = options.ClaimRadius * options.ClaimRadius;
                var rule = gameData.ScoringRule;
                int frames = 0;
                bool objectiveReached = false;
                Domains objectiveDomain = Domains.Blue;

                while (frames < options.MaxFrames)
                {
                    loop.Tick(options.DeltaTime);
                    frames++;

                    // Proximity referee: nearest vessel inside the claim radius collects.
                    if (activeCrystal != null)
                    {
                        IPlayer claimant = null;
                        float bestSqr = claimRadiusSqr;
                        foreach (var player in players)
                        {
                            var d2 = (player.Vessel.Transform.position - activeCrystal.transform.position).sqrMagnitude;
                            if (d2 < bestSqr)
                            {
                                bestSqr = d2;
                                claimant = player;
                            }
                        }

                        if (claimant != null)
                        {
                            result.TotalClaims++;
                            var element = courseElements[courseIndex];
                            ApplyCrystalPickup(claimant, element);

                            // Closest rival's distance to the crystal at claim time — the "photo finish" gap.
                            float rivalSqr = float.PositiveInfinity;
                            foreach (var other in players)
                            {
                                if (ReferenceEquals(other, claimant)) continue;
                                var d2 = (other.Vessel.Transform.position - activeCrystal.transform.position).sqrMagnitude;
                                if (d2 < rivalSqr) rivalSqr = d2;
                            }
                            string gap = players.Count > 1 ? $" (rival {F(Mathf.Sqrt(rivalSqr))}u behind)" : "";

                            Log($"[t={F(Time.time - raceStart),7}s] {claimant.Name} ({claimant.Domain}) claims crystal #{courseIndex + 1} [{element}]{gap} — " +
                                $"Jade {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Jade)} · " +
                                $"Ruby {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Ruby)} · " +
                                $"Gold {ScoringMetrics.SumByDomain(gameData, rule.Metric, Domains.Gold)}");

                            courseData.TryRemoveItem(activeCrystal); // raises OnCellItemsUpdated → pilots retarget
                            Object.Destroy(activeCrystal.gameObject);
                            activeCrystal = null;

                            // Respawn the next waypoint crystal unless the race just ended.
                            if (!rule.IsObjectiveReached(gameData, out _) && courseIndex + 1 < coursePositions.Length)
                            {
                                courseIndex++;
                                activeCrystal = SpawnCrystal(courseData, coursePositions, courseIndex);
                            }
                        }
                    }

                    // Turn-monitor shape: the server checks the scoring rule every frame.
                    if (rule.IsObjectiveReached(gameData, out objectiveDomain))
                    {
                        objectiveReached = true;
                        break;
                    }
                }

                result.FramesSimulated = frames;

                if (!objectiveReached)
                {
                    Log($"TIMEOUT — no domain reached {target} crystals within {frames} frames.");
                    return result;
                }

                // ── finish: resolve, score (golf), publish results ────────────
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
                Log($"OBJECTIVE — {objectiveDomain} domain reached {target} crystals in {F(finishTime)}s ({frames} frames).");
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
                    Log($"  #{standing.Rank} {standing.Name,-6} {standing.Domain,-5} {standing.Crystals,2} crystals  score={F(standing.Score),9}  {standing.ScoreText}");
                }

                return result;
            }
            finally
            {
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
                loop.Tick(options.DeltaTime);  // end-of-frame destroy flush

                typeof(PlayerDataService).GetProperty("Instance")!.SetValue(null, null);
                NetworkManager.Singleton = null;
                Debug.Sink = previousSink;

                foreach (var entry in capturedLog.Entries)
                    if (entry.Type is LogType.Error or LogType.Exception)
                        result.EngineErrors.Add($"{entry.Type}: {entry.Message}");
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

        static Crystal SpawnCrystal(CellRuntimeDataSO courseData, Vector3[] course, int index)
        {
            var go = new GameObject($"crystal-{index + 1}");
            go.transform.position = course[index];
            var crystal = go.AddComponent<Crystal>();
            crystal.Initialize(index + 1);
            crystal.ownDomain = Domains.Blue;   // uncommitted — every domain's AI may seek it
            crystal.ItemType = ItemType.Buff;
            courseData.AddCrystalToList(crystal); // raises OnCellItemsUpdated → AIPilots retarget
            return crystal;
        }

        /// <summary>RoundStats + ResourceSystem elemental progression, mirroring the SkimRace sim.</summary>
        static void ApplyCrystalPickup(IPlayer claimant, Element element)
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
        /// Same programmatic vessel "prefab" as the CLI smoke section [6], with one
        /// round-specific difference: every AIPilot shares the round's
        /// <see cref="CellRuntimeDataSO"/> and its OnCellItemsUpdated channel, so a crystal
        /// registered in the course registry retargets the whole AI field.
        /// </summary>
        static GameObject BuildVesselPrefab(string name, VesselClassType vesselType, GameDataSO gameData,
            CellRuntimeDataSO cellData, ScriptableEventNoParam onCellItemsUpdated)
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

            return go;
        }
    }
}

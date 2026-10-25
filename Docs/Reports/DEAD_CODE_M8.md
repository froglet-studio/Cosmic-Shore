# Dead-code sweep (REFACTOR_BACKLOG M8), measured 2026-10-25

**Verdict 2026-10-25 (Yash): all eight clusters deleted** - 125 scripts with their `.meta`s, plus three folders they emptied (`EffectsSO/Projectile Crystal Effects`, `System/Runtime/Controller`, `Utility/ChoppingBlock`). The 17 held files stay. This page is the record of what went and why; `git show` the commit to restore any one.

Every MonoBehaviour / ScriptableObject script under `Assets/_Scripts` and `Assets/FTUE` (Editor and Tests folders excluded)
whose `.meta` guid is on **no** scene, prefab, asset, controller, material, animation or preset, and none of whose
declared types (classes, enums, structs) is named by any other `.cs` file (comments stripped). 142 such files, 10,302 lines.

How each proposal was checked:

- **Compiles without them.** All 125 proposed files deleted together in a scratch worktree: refcompile player OK, editor
  at its 3-error baseline (`AuthenticationServiceFacade`). Because no other file names their types, each cluster also
  deletes on its own.
- **Nothing in flight uses them.** 54 branches with commits since 2026-10-11: no added line names a proposed type or guid,
  and none edits a proposed file.
- **Amoebius.** Its live build compiles `Assets/_Scripts`, so a deleted file simply leaves it; its hand port
  (`Port/src/CosmicShore.Game`) keeps its own copies. No live-side Port file names a proposed type (one coincidence:
  the engine's own `ScrollRect` enum member `AutoHide`, `Port/src/CosmicShore.Engine/UI/ScrollRect.cs`).
- **Dates.** The clone is shallow (graft 2026-10-02), so `2026-10-02` means "on or before then".

**Held, not proposed** (17 files, 2,521 lines): a script that starts itself (`[RuntimeInitializeOnLoadMethod]`), that a
`Tools/` generator names (it would put it on an asset), that a string names, that was added in the last 14 days, or that
an in-flight branch uses. Listed at the end.

## A MiniGameObjects (18 files, 1157 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `Controller/Environment/MiniGameObjects/ModeSelectTrigger.cs` | `ModeSelectTrigger` | 59 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableArrow.cs` | `SpawnableArrow` | 96 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableBaseballCurve.cs` | `SpawnableBaseballCurve` | 55 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableCardioidSmear.cs` | `SpawnableCardioidSmear` | 52 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableCircle.cs` | `SpawnableCircle` | 57 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableCylinder.cs` | `SpawnableCylinder` | 74 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableDiamond.cs` | `SpawnableDiamond` | 87 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableDriftCourse.cs` | `SpawnableDriftCourse` | 62 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableHeart.cs` | `SpawnableHeart` | 45 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableInfinity.cs` | `SpawnableInfinity` | 61 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableLightning.cs` | `SpawnableLightning` | 90 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableSmiley.cs` | `SpawnableSmiley` | 89 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableSpiral.cs` | `SpawnableSpiral` | 51 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableStar.cs` | `SpawnableStar` | 66 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableTube.cs` | `SpawnableTube` | 55 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableWave.cs` | `SpawnableWave` | 55 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/SpawnableZigzag.cs` | `SpawnableZigzag` | 54 | 2026-10-02 |
| `Controller/Environment/MiniGameObjects/ThreatSpawner.cs` | `ThreatSpawner` | 49 | 2026-10-02 |

## B Spawning generators (17 files, 881 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `Controller/Environment/Spawning/Generators/AtOriginGenerator.cs` | `AtOriginGenerator` | 46 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/BranchingLineGenerator.cs` | `BranchingLineGenerator` | 102 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/CubicGenerator.cs` | `CubicGenerator` | 38 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/CurvyTubeGenerator.cs` | `CurvyTubeGenerator` | 40 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/CylinderSurfaceGenerator.cs` | `CylinderSurfaceGenerator` | 42 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/HexRingGenerator.cs` | `HexRingGenerator` | 38 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/HilbertCurveGenerator.cs` | `HilbertCurveGenerator` | 122 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/HoneycombGridGenerator.cs` | `HoneycombGridGenerator` | 40 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/KinkyLineGenerator.cs` | `KinkyLineGenerator` | 40 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/KinkyTubeGenerator.cs` | `KinkyTubeGenerator` | 45 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/MazeGridGenerator.cs` | `MazeGridGenerator` | 38 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/SavedMazeGenerator.cs` | `SavedMazeGenerator` | 49 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/SphereSurfaceGenerator.cs` | `SphereSurfaceGenerator` | 42 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/SphereUniformGenerator.cs` | `SphereUniformGenerator` | 62 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/SpiralTowerGenerator.cs` | `SpiralTowerGenerator` | 45 | 2026-10-02 |
| `Controller/Environment/Spawning/Generators/StraightLineGenerator.cs` | `StraightLineGenerator` | 49 | 2026-10-17 |
| `Controller/Environment/Spawning/Generators/ToroidSurfaceGenerator.cs` | `ToroidSurfaceGenerator` | 43 | 2026-10-02 |

## C Legacy vessel actions and vessel parts (13 files, 669 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `Controller/Vessel/Animation/ParametricJetEffect.cs` | `ParametricJetEffect` | 82 | 2026-10-02 |
| `Controller/Vessel/ElementPipsView.cs` | `ElementPipsView` | 225 | 2026-10-02 |
| `Controller/Vessel/TrailScaleModulator.cs` | `TrailScaleModulator` | 62 | 2026-10-02 |
| `Controller/Vessel/VesselActions/ChangeRotationSpeedAction.cs` | `ChangeRotationSpeedAction` | 27 | 2026-10-02 |
| `Controller/Vessel/VesselActions/ChargedFireGunAction.cs` | `ChargedFireGunAction` | 89 | 2026-10-16 |
| `Controller/Vessel/VesselActions/DetonateProjectilesAction.cs` | `DetonateProjectilesAction` | 22 | 2026-10-02 |
| `Controller/Vessel/VesselActions/DisableTrailAction.cs` | `DisableTrailAction` | 17 | 2026-10-02 |
| `Controller/Vessel/VesselActions/SeedAssemblerMono.cs` | `SeedAssemblerMono` | 16 | 2026-10-02 |
| `Controller/Vessel/VesselActions/StopGunsAction.cs` | `StopGunsAction` | 16 | 2026-10-02 |
| `Controller/Vessel/VesselActions/SyncActionWrapper.cs` | `SyncActionWrapper` | 37 | 2026-10-02 |
| `Controller/Vessel/VesselActions/ToggleProjectileActionWrapper.cs` | `ToggleProjectileActionWrapper` | 23 | 2026-10-02 |
| `Controller/Vessel/VesselActions/ToggleTurretModeAction.cs` | `ToggleTurretModeAction` | 22 | 2026-10-02 |
| `Controller/Vessel/VesselActions/ZoomGrowRateDistributeAction.cs` | `ZoomGrowRateDistributeAction` | 31 | 2026-10-02 |

## D Arcade controllers and turn monitors (12 files, 590 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `Controller/Arcade/JoustStatsProvider.cs` | `JoustStatsProvider` | 80 | 2026-10-25 |
| `Controller/Arcade/OnlineDuelForTheCellController.cs` | `OnlineDuelForTheCellController` | 73 | 2026-10-08 |
| `Controller/Arcade/ScurryStatsProvider.cs` | `ScurryStatsProvider` | 84 | 2026-10-02 |
| `Controller/Arcade/SkimRaceStatsProvider.cs` | `SkimRaceStatsProvider` | 79 | 2026-10-02 |
| `Controller/Arcade/TurnMonitors/AllLifeFormsDestroyedTurnMonitor.cs` | `AllLifeFormsDestroyedTurnMonitor` | 46 | 2026-10-02 |
| `Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs` | `CellControlTurnMonitor` | 16 | 2026-10-25 |
| `Controller/Arcade/TurnMonitors/DistanceTurnMonitor.cs` | `DistanceTurnMonitor` | 41 | 2026-10-02 |
| `Controller/Arcade/TurnMonitors/ResourceAccumulationTurnMonitor.cs` | `ResourceAccumulationTurnMonitor` | 15 | 2026-10-02 |
| `Controller/Arcade/TurnMonitors/VesselCollisionTurnMonitor.cs` | `ShipCollisionTurnMonitor` | 41 | 2026-10-02 |
| `Controller/Arcade/TurnMonitors/VolumeCreatedTurnMonitor.cs` | `VolumeCreatedTurnMonitor` | 61 | 2026-10-02 |
| `Controller/Arcade/TurnMonitors/VolumeDestroyedTurnMonitor.cs` | `VolumeDestroyedTurnMonitor` | 45 | 2026-10-02 |
| `Controller/Arcade/VolumeTestController.cs` | `VolumeTestController` | 9 | 2026-10-02 |

## E UI leftovers (19 files, 1197 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `UI/Animations/EllipsisTextLooper.cs` | `EllipsisTextLooper` | 78 | 2026-10-02 |
| `UI/BenchmarkSceneHud.cs` | `BenchmarkSceneHud` | 206 | 2026-10-02 |
| `UI/CoOpScoreBoard.cs` | `CoOpScoreBoard` | 10 | 2026-10-02 |
| `UI/Controller/MinigameHUDContainer.cs` | `MinigameHUDContainer` | 19 | 2026-10-02 |
| `UI/CurrentScore.cs` | `CurrentScore` | 39 | 2026-10-02 |
| `UI/Elements/DomainSelectionPanel.cs` | `DomainSelectionPanel` | 71 | 2026-10-02 |
| `UI/Elements/Hangar/HangarAbilityCard.cs` | `HangarAbilityCard` | 30 | 2026-10-02 |
| `UI/Elements/ProfileDisplayWidget.cs` | `ProfileDisplayWidget` | 52 | 2026-10-02 |
| `UI/Elements/QuickPlayButton.cs` | `QuickPlayButton` | 105 | 2026-10-02 |
| `UI/Elements/SwitchToggle.cs` | `SwitchToggle` | 34 | 2026-10-02 |
| `UI/Elements/TeamScorecard.cs` | `TeamScorecard` | 76 | 2026-10-02 |
| `UI/MaelstromDomainScoreView.cs` | `MaelstromDomainScoreView` | 37 | 2026-10-02 |
| `UI/Minimap.cs` | `Minimap` | 29 | 2026-10-02 |
| `UI/Privacy/AnalyticsPrivacySettingsController.cs` | `AnalyticsPrivacySettingsController` | 85 | 2026-10-02 |
| `UI/SkimRaceHUDView.cs` | `SkimRaceHUDView` | 11 | 2026-10-02 |
| `UI/VesselSelection.cs` | `ShipSelection` | 26 | 2026-10-02 |
| `UI/Views/ArcadeProfileWidget.cs` | `ArcadeProfileWidget` | 191 | 2026-10-02 |
| `UI/Views/AvatarButtonView.cs` | `AvatarButtonView` | 38 | 2026-10-02 |
| `UI/WidescreenLayoutAdapter.cs` | `WidescreenLayoutAdapter` | 60 | 2026-10-02 |

## F System leftovers (8 files, 1112 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `System/Monetization/EpisodeTokenController.cs` | `EpisodeTokenController` | 187 | 2026-10-08 |
| `System/RewindSystem/GenericRewind.cs` | `GenericRewind` | 31 | 2026-10-02 |
| `System/RewindSystem/RewindSystem.cs` | `RewindSystem` | 164 | 2026-10-02 |
| `System/Runtime/Controller/DialogueManager.cs` | `DialogueManager` | 123 | 2026-10-02 |
| `System/Runtime/View/DialogueUIController.cs` | `DialogueUIController` | 266 | 2026-10-02 |
| `System/SnsShare.cs` | `SnsShare` | 75 | 2026-10-02 |
| `System/SplashToAuthFlow.cs` | `SplashToAuthFlow` | 126 | 2026-10-02 |
| `System/Systems/Authentication/AuthenticationController.cs` | `AuthenticationController` | 140 | 2026-10-02 |

## G FTUE leftovers (5 files, 253 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `Assets/FTUE/Scripts/Graph/Nodes/QuestLockModesNode.cs` | `QuestLockModesNode` | 45 | 2026-10-06 |
| `Assets/FTUE/Scripts/Graph/Nodes/QuestUnlockModeNode.cs` | `QuestUnlockModeNode` | 33 | 2026-10-02 |
| `Assets/FTUE/Scripts/Graph/Nodes/QuestWaitForDriftNode.cs` | `QuestWaitForDriftNode` | 113 | 2026-10-02 |
| `Assets/FTUE/Scripts/Graph/Nodes/QuestWaitForGameLaunchNode.cs` | `QuestWaitForGameLaunchNode` | 34 | 2026-10-02 |
| `Assets/FTUE/Scripts/Graph/Nodes/QuestWaitNode.cs` | `QuestWaitNode` | 28 | 2026-10-02 |

## H Utility, FX, camera, animation and small SOs (33 files, 1922 lines)

| File | Types | Lines | Last commit |
|---|---|---|---|
| `Controller/Animation/DolphinAnimation.cs` | `DolphinAnimation` | 54 | 2026-10-16 |
| `Controller/Animation/SingleStickAnimationController.cs` | `SingleStickAnimationController` | 44 | 2026-10-02 |
| `Controller/Camera/CameraSettingsApplier.cs` | `CameraSettingsApplier` | 65 | 2026-10-02 |
| `Controller/Environment/FlowField/FlowFieldView.cs` | `FlowFieldView` | 134 | 2026-10-02 |
| `Controller/Environment/WarpField/WarpFieldView.cs` | `WarpFieldView` | 117 | 2026-10-02 |
| `Controller/Environment/WarpField/ZeroWarp.cs` | `ZeroWarp` (CreateAssetMenu) | 22 | 2026-10-02 |
| `Controller/FX/FloraAmbientAudioController.cs` | `FloraAmbientAudioController` | 134 | 2026-10-02 |
| `Controller/FX/ParticleEffectModifier.cs` | `ParticleEffectModifier` | 54 | 2026-10-02 |
| `Controller/FX/VFXPropertyModifier.cs` | `VFXPropertyModifier` | 49 | 2026-10-02 |
| `Controller/IO/ControllerDropdown.cs` | `ControllerDropdown` | 37 | 2026-10-02 |
| `Controller/ImpactEffects/EffectsSO/Abstract Effect Types/SkimmerProjectileEffectSO.cs` | `SkimmerProjectileEffectSO` | 6 | 2026-10-02 |
| `Controller/ImpactEffects/EffectsSO/Projectile Crystal Effects/ProjectileExplosionByCrystalEffectSO.cs` | `ProjectileExplosionByCrystalEffectSO` (CreateAssetMenu) | 49 | 2026-10-02 |
| `Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerSFXByCrystalEffectSO.cs` | `SkimmerSFXByCrystalEffectSO` (CreateAssetMenu) | 22 | 2026-10-02 |
| `Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerScaleTrailAndCameraPrismEffectSO.cs` | `SkimmerScaleTrailAndCameraPrismEffectSO` (CreateAssetMenu) | 23 | 2026-10-02 |
| `Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/VesselSetShieldByCrystalEffectSO.cs` | `VesselSetShieldByCrystalEffectSO` (CreateAssetMenu) | 18 | 2026-10-02 |
| `ScriptableObjects/SOAP/ScriptableCellPhase/CellPhaseVariable.cs` | `CellPhaseVariable` (CreateAssetMenu) | 13 | 2026-10-02 |
| `ScriptableObjects/SOAP/ScriptableClassType/ScriptableEventSkimmerDebuffApplied.cs` | `ScriptableEventSkimmerDebuffApplied` (CreateAssetMenu) | 27 | 2026-10-02 |
| `Utility/AutoHide.cs` | `AutoHide` | 23 | 2026-10-02 |
| `Utility/ChoppingBlock/TestHarnessOctreeDensitySearch.cs` | `TestHarnessOctreeDensitySearch` | 40 | 2026-10-02 |
| `Utility/DataContainers/SpriteDataSO.cs` | `SpriteDataSO` (CreateAssetMenu) | 10 | 2026-10-02 |
| `Utility/DataContainers/TagDataSO.cs` | `TagDataSO` (CreateAssetMenu) | 8 | 2026-10-02 |
| `Utility/DisableDebugger.cs` | `DisableDebugger` | 18 | 2026-10-02 |
| `Utility/Effects/FlickerUIEffect.cs` | `FlickerUIEffect` | 40 | 2026-10-02 |
| `Utility/ImportVertices.cs` | `ImportVertices` | 35 | 2026-10-02 |
| `Utility/Interactive/InteractiveSquishSSU.cs` | `InteractiveSquishSSU` | 67 | 2026-10-02 |
| `Utility/Network/NetworkObjectSpawner.cs` | `NetworkObjectSpawner` | 50 | 2026-10-02 |
| `Utility/Network/ServerAdditiveSceneLoader.cs` | `ServerAdditiveSceneLoader` | 158 | 2026-10-17 |
| `Utility/PopupManager.cs` | `PopupManager` | 93 | 2026-10-02 |
| `Utility/PrismShieldPreview.cs` | `PrismShieldPreview` | 153 | 2026-10-17 |
| `Utility/ScreenShots/CaptureScreenShot.cs` | `CaptureScreenShot` | 48 | 2026-10-02 |
| `Utility/SegmentSpawnerPreview.cs` | `SegmentSpawnerPreview` | 278 | 2026-10-17 |
| `Utility/TagContainerSO.cs` | `TagContainerSO` (CreateAssetMenu) | 9 | 2026-10-02 |
| `Utility/Tools/AudioTester.cs` | `AudioTester` | 24 | 2026-10-02 |

## Held (not proposed)

| File | Why held |
|---|---|
| `Assets/FTUE/Scripts/Graph/Nodes/QuestNavigateNode.cs` | a Tools/ generator names it |
| `Assets/FTUE/Scripts/Graph/Nodes/QuestWaitForLessonNode.cs` | a Tools/ generator names it |
| `Controller/ECS/Rendering/PrismStressInjector.cs` | starts itself |
| `Controller/Environment/EcosystemPerfProbe.cs` | starts itself; a Tools/ generator names it |
| `Controller/Environment/MiniGameObjects/FreestyleSign.cs` | starts itself |
| `Controller/Environment/MiniGameObjects/SpawnableRaceTrack.cs` | in flight: cece/nice-brahmagupta-ojclij |
| `Controller/Environment/MiniGameObjects/SpawnableShapeSign.cs` | a Tools/ generator names it |
| `Controller/FX/SilenceScreenFeedback.cs` | starts itself; added in the last 14 days |
| `Controller/Vessel/PrismStellatedOctahedronShieldTester.cs` | a Tools/ generator names it |
| `Controller/Vessel/VesselActions/GrowSkimmerAction.cs` | a Tools/ generator names it |
| `Controller/Vessel/VesselActions/GrowTrailAction.cs` | a Tools/ generator names it |
| `Controller/Vessel/VesselActions/SpinAroundAction.cs` | a Tools/ generator names it |
| `Controller/Vessel/VesselCollider.cs` | a Tools/ generator names it |
| `ScriptableObjects/SnowStressTestSpawner.cs` | a Tools/ generator names it |
| `UI/Elements/IconRotator.cs` | a Tools/ generator names it |
| `Utility/PerformanceBenchmark/BenchmarkBuildAutoRunner.cs` | starts itself; in flight: cece/nice-brahmagupta-ojclij |
| `Utility/ScreenShots/ScreenshotDirector.cs` | starts itself; in flight: cece/nice-brahmagupta-ojclij, claude/match-replays-5t3dbr |

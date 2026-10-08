# Branch archive: `claude/explosive-joust-scripts-xLazv`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-19 by Shombith03
- **Unmerged commits:** 3
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/425
- **Forked from:** `b3c73ffe1` (2026-03-19, Add Drag Scouting End Game Cinematic Defination)
- **Tip:** `43c064bc4`
- **Files touched (15):**
  - `Assets/_Prefabs/Projectile/AOEExplosion.prefab`
  - `Assets/_SO_Assets/Cinematics/MinigameExplosiveJoustCinematicDefinition_Multiplayer.asset`
  - `Assets/_SO_Assets/Cinematics/MinigameExplosiveJoustCinematicDefinition_Multiplayer.asset.meta`
  - `Assets/_SO_Assets/Cinematics/SceneCinematicLibrary.asset`
  - `Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/ExplosiveJoustExplosionImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/ExplosiveJoustExplosionImpactorDataContainer.asset.meta`
  - `Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselExplosiveJoustByExplosionEffect.asset`
  - `Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselExplosiveJoustByExplosionEffect.asset.meta`
  - `Assets/_SO_Assets/Games/ArcadeGameExplosiveJoust.asset`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameExplosiveJoust.unity`
  - `Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Vessel Explosion Effects/VesselExplosiveJoustByExplosionEffectSO.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Vessel Explosion Effects/VesselExplosiveJoustByExplosionEffectSO.cs.meta`
  - `Assets/_Scripts/Models/Enums/GameModes.cs`
  - `ProjectSettings/EditorBuildSettings.asset`

### `e82831231` — Add Explosive Joust game mode scripts and configuration

_Claude, 2026-03-18 20:42:12 +0000_

```text
- Create VesselExplosiveJoustByExplosionEffectSO: scores a joust when
  a Manta's crystal explosion catches an enemy vessel
- Add MultiplayerExplosiveJoust (36) to GameModes enum
- Add PlayGameMultiplayerExplosiveJoust (432) to CallToActionTargetType
- Create ExplosiveJoustExplosionImpactorDataContainer SO with the new effect
- Update ArcadeGameExplosiveJoust SO: correct Mode, DisplayName, SceneName, CTA
- Wire AOEExplosion prefab's explosionImpactorDataContainer to the new container
- Update scene JoustStatsReporter gameMode to MultiplayerExplosiveJoust
- Add MinigameExplosiveJoust scene to EditorBuildSettings
```

```text
 Assets/_Prefabs/Projectile/AOEExplosion.prefab                        |  8 +++----
 .../ExplosiveJoustExplosionImpactorDataContainer.asset                | 17 ++++++++++++++
 .../ExplosiveJoustExplosionImpactorDataContainer.asset.meta           |  8 +++++++
 .../VesselExplosiveJoustByExplosionEffect.asset                       | 15 ++++++++++++
 .../VesselExplosiveJoustByExplosionEffect.asset.meta                  |  8 +++++++
 Assets/_SO_Assets/Games/ArcadeGameExplosiveJoust.asset                | 11 +++++----
 Assets/_Scenes/Multiplayer Scenes/MinigameExplosiveJoust.unity        |  2 +-
 Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs    |  2 ++
 .../VesselExplosiveJoustByExplosionEffectSO.cs                        | 41 +++++++++++++++++++++++++++++++++
 .../VesselExplosiveJoustByExplosionEffectSO.cs.meta                   |  2 ++
 Assets/_Scripts/Models/Enums/GameModes.cs                             |  1 +
 ProjectSettings/EditorBuildSettings.asset                             |  3 +++
 12 files changed, 108 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs b/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs
index f0634e3e6..90d265910 100644
--- a/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs
+++ b/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs
@@ -55,6 +55,8 @@ namespace CosmicShore.App.Systems.CTA
         PlayGameMultiplayerWildlifeBlitzGame = 430,
         PlayGameHexRace = 431,
 
+        PlayGameMultiplayerExplosiveJoust = 432,
+
         /*********** ADDED BY WILL *************/
 
     }
diff --git a/Assets/_Scripts/Models/Enums/GameModes.cs b/Assets/_Scripts/Models/Enums/GameModes.cs
index f566e2384..2db388dde 100644
--- a/Assets/_Scripts/Models/Enums/GameModes.cs
+++ b/Assets/_Scripts/Models/Enums/GameModes.cs
@@ -37,4 +37,5 @@ public enum GameModes
     HexRace = 33,
     MultiplayerJoust = 34,
     MultiplayerCrystalCapture = 35,
+    MultiplayerExplosiveJoust = 36,
 }
\ No newline at end of file
```

</details>

### `43c064bc4` — Add Cinematic Defination

_Shombith03, 2026-03-19 03:36:30 +0530_

```text
 .../MinigameExplosiveJoustCinematicDefinition_Multiplayer.asset       | 65 +++++++++++++++++++++++++++++++++
 .../MinigameExplosiveJoustCinematicDefinition_Multiplayer.asset.meta  |  8 ++++
 Assets/_SO_Assets/Cinematics/SceneCinematicLibrary.asset              |  2 +
 3 files changed, 75 insertions(+)
```

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._

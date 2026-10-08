# Branch archive: `claude/sparrow-crystal-sound-bug-bnocj6`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-26 by Claude
- **Unmerged commits:** 1
- **Forked from:** `def29f5e1` (2026-08-25, Merge remote-tracking branch 'origin/bleeding-edge' into seven-sixteen)
- **Tip:** `c30040ec9`
- **Files touched (2):**
  - `Assets/_Prefabs/Environment/CrystalTime.prefab`
  - `CLAUDE.md`

### `c30040ec9` — fix(audio): stop the Time crystal sounding through a muted SFX setting

_Claude, 2026-08-26 01:51:24 +0000_

```text
CrystalTime.prefab carried two FMODUnity.StudioEventEmitter components, both
wired to the borrowed `event:/SFX/Oneshots/Gameplay sfx/Creature colide`:

  - EventPlayTrigger 3 (TriggerEnter) on the crystal's own pickup trigger, so
    ANYTHING entering it sounded - a Sparrow round, a hull, a skimmer.
  - EventPlayTrigger 2 (ObjectDestroy), duplicating the real pickup one-shot
    Crystal.PlayExplosionAudio already fires.

Neither could be silenced. This project has no SFX bus or VCA, so all SFX
volume is applied per-instance via setVolume() (the whole reason
FMODOneShotVolumeHelper exists), and StudioEventEmitter never calls it - the
string "volume" does not appear anywhere in StudioEventEmitter.cs. So the
event played at full volume with sound effects turned off, and no code path
could stop it.

It read as a feature rather than a defect because it was the only one of the
four elemental crystals with an emitter, and because the crystal's correct,
mutable pickup sound (GameplaySFXCategory.CrystalCollect, through
AudioSystem -> FMODOneShotVolumeHelper) still worked alongside it.

Removing both emitters leaves the crystal with exactly that one mutable sound.
Deletion-only edit: the two component entries plus the two MonoBehaviour
documents, nothing else touched. Verified no other asset in the project
referenced either fileID.

Same defect still outstanding, deliberately left alone as it is pre-existing
and beyond this report: TeamCrystal.prefab and BigCrystalVariant.prefab start
`event:/SFX/Loops/Goal` on ObjectStart, and the fauna emitters
Fauna.AssignLineage retargets are unmutable for the same reason. Recorded in
CLAUDE.md's audio anti-patterns with the general rule - a component that
starts an FMOD event without owning the instance can never honour a
per-instance volume setting.
```

```text
 Assets/_Prefabs/Environment/CrystalTime.prefab | 64 --------------------------------------------------------
 CLAUDE.md                                      | 19 +++++++++++++++++
 2 files changed, 19 insertions(+), 64 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 843b18dd6..0b8c026ab 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -1647,6 +1647,25 @@ onto the emitter at spawn (`FaunaConfigurationSO.OverrideAudio` + `AudioLoopEven
   (music, plus stragglers like `IconEmitter` and `AudioSystem.PlaySFXClip`) and is being retired;
   new SFX is FMOD
 - Adding an if-null-guard *fallback to another event*. Guard for silence, never for substitution
+- **A bare `FMODUnity.StudioEventEmitter` dropped on a prefab.** This project has no SFX bus or
+  VCA, so *all* SFX volume is applied per-instance via `setVolume()` (that is the entire reason
+  `FMODOneShotVolumeHelper` exists). `StudioEventEmitter` never calls `setVolume()`, so a sound
+  fired from one **cannot be muted or attenuated by the SFX setting** — it plays at full volume
+  with sound effects turned off, and no code path can stop it. `CrystalTime.prefab` shipped two
+  of them, both on the borrowed `event:/SFX/Oneshots/Gameplay sfx/Creature colide`, one on
+  `TriggerEnter` (so *anything* entering the crystal's pickup trigger — a Sparrow round, a hull,
+  a skimmer — sounded) and one on `ObjectDestroy` (duplicating the real, mutable
+  `GameplaySFXCategory.CrystalCollect` one-shot `Crystal.PlayExplosionAudio` already fires). It
+  was invisible for as long as it shipped because the other three elemental crystals have no
+  emitter, so it read as "the Time crystal has a sound" rather than as a defect. Fire a one-shot
+  through `AudioSystem` / `FMODOneShotVolumeHelper`; for a **continuous** emitter, own the
+  instance and tie its volume to `GameSetting.SFXLevel`/`SFXEnabled` the way
+  `FloraAmbientAudioController` and `ShipAudioController` do. **Still outstanding** (pre-existing,
+  same defect): `TeamCrystal.prefab` + `BigCrystalVariant.prefab` start `event:/SFX/Loops/Goal`
+  on `ObjectStart`, and the fauna emitters `Fauna.AssignLineage` retargets
+  (`FaunaConfigurationSO.OverrideAudio`) are unmutable for the same reason.
+  **General rule: a component that starts an FMOD event without owning the instance can never
+  honour a per-instance volume setting.**
 
 ### Multiplayer / Netcode
 
```

</details>

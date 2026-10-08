# Branch archive: `claude/crystal-destruction-audio-bug-xvqhek`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-13 by Claude
- **Unmerged commits:** 3
- **Forked from:** `d32f26839` (2026-08-12, Merge pull request #710 from froglet-studio/claude/charge-crystal-shader-9u2ik)
- **Tip:** `0fec0f60c`
- **Files touched (13):**
  - `Assets/Resources/GameplaySFXPolicy.asset`
  - `Assets/Resources/GameplaySFXPolicy.asset.meta`
  - `Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs`
  - `Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs`
  - `Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs`
  - `Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs.meta`
  - `Assets/_Scripts/System/Audio/AudioSystem.cs`
  - `Assets/_Scripts/System/Audio/GameplaySFXBurstLimiter.cs`
  - `Assets/_Scripts/System/Audio/GameplaySFXBurstLimiter.cs.meta`
  - `Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs`
  - `Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs.meta`
  - `CLAUDE.md`
  - `Docs/AUDIO.md`

### `617775c2c` — fix(audio): gate every gameplay SFX one-shot behind a per-category burst policy

_Claude, 2026-08-13 16:41:25 +0000_

```text
Destroying many crystals at once started one FMOD one-shot per crystal, on the
same frame, from the same asset. That is two distinct problems:

  - Voices. Each one-shot is its own CreateInstance/start/release, so a frame of
    crystal deaths is a voice storm and a frame-cost spike.
  - Sound. The copies are correlated, so they sum COHERENTLY: N identical signals
    is +20*log10(N) dB (ten crystals = +20 dB, thirty = +29.5 dB), and the
    sub-millisecond offsets between them comb-filter into harsh metallic phasing.

The second is the one that reads as "it tried to combine far too many sounds",
and it is why attenuating each voice was never going to be enough: the phasing is
a function of SIMULTANEITY, not level. AudioSystem only had a volume scale plus a
hand-rolled sliding window, and both were wired to BlockDestroy alone - every
crystal category (CrystalCollect, CrystalSkim, and the four Element*Received
stingers, which are 2D and therefore sum perfectly) was completely ungoverned.

Every gameplay one-shot now passes GameplaySFXBurstLimiter in the dispatch, so
there is no unthrottled entry point to author around. Three levers per category,
all in GameplaySFXPolicySO (the only tuning surface, per config separation):

  - minRetriggerSeconds - the decoherence lever, and the one that fixes the sound.
    Two voices of a category can never start within a few ms of each other, so a
    burst becomes a legible rattle of distinct hits instead of one phased wall.
  - maxVoicesPerWindow / windowSeconds - the FMOD-voice budget.
  - maxPendingVoices - blocked events are not thrown away. They fold into pending
    aggregates and replay spaced out, quieter, at the CENTROID of the events they
    represent, so a big burst still sounds big. 0 = the old pure-drop behaviour.

30 crystals on one frame now resolves to 3 voices at t=0/0.046/0.092 with
decaying volume, instead of 30 at t=0. The leading edge is never delayed.

The limiter is pure C# (takes "now" as a parameter, touches no Unity object), so
spacing, budget, window roll, falloff floor, coalescing, centroid and per-category
isolation are all covered by edit-mode tests - the bug only reproduces under a
burst that is awkward to stage by hand in play mode. Unlisted categories get an
Unlimited() default, so anything not identified as bursty behaves exactly as
before. BlockDestroy's legacy tuning (0.35 volume, 4 per 0.1s) is carried over
verbatim and asserted by a test.

Also corrects the tech-stack entry: the project runs FMOD, not Wwise (a Wwise
folder exists but no first-party script references it).

Verified out of editor: all four changed/added C# files compile clean under mcs
with every stub type in its real namespace, and a behavioural harness runs the
shipped limiter and policy through the burst, spacing, centroid, volume-decay,
category-isolation and shipped-defaults scenarios (all pass). The hand-authored
Resources asset was machine-validated key-by-key against the C# serialized fields
of both classes. conditional-compilation check: OK.
```

```text
 Assets/Resources/GameplaySFXPolicy.asset                          | 136 +++++++++++++
 Assets/Resources/GameplaySFXPolicy.asset.meta                     |   8 +
 Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs          | 386 ++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs.meta     |  11 +
 Assets/_Scripts/System/Audio/AudioSystem.cs                       | 156 ++++++++-------
 Assets/_Scripts/System/Audio/GameplaySFXBurstLimiter.cs           | 292 +++++++++++++++++++++++++++
 Assets/_Scripts/System/Audio/GameplaySFXBurstLimiter.cs.meta      |  11 +
 Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs      | 382 +++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs.meta |  11 +
 CLAUDE.md                                                         |   3 +-
 Docs/AUDIO.md                                                     | 168 ++++++++++++++++
 11 files changed, 1487 insertions(+), 77 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1490 lines)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs b/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
new file mode 100644
index 000000000..efe4512a8
--- /dev/null
+++ b/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
@@ -0,0 +1,386 @@
+using System;
+using System.Collections.Generic;
+using CosmicShore.Core;
+using UnityEngine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    /// <summary>
+    /// Per-category burst policy for gameplay SFX one-shots — the single source of truth for
+    /// "how many of this sound may play at once, how close together, and how loud".
+    ///
+    /// Why this exists: every gameplay SFX is fired per-EVENT, and several event classes are
+    /// inherently bursty — a vessel flying through a crystal shower, an AOE blast destroying a
+    /// field of crystals, a fauna die-off dropping its hearts, a trail collapsing. Each one-shot
+    /// is a separate FMOD instance (see <c>FMODOneShotVolumeHelper</c>: CreateInstance / setVolume /
+    /// set3DAttributes / start / release), so N events in one frame means N voices — and because
+    /// they are the SAME asset started within the same frame, they sum COHERENTLY: N identical
+    /// copies is +20·log10(N) dB (10 crystals = +20 dB), and the sub-millisecond offsets between
+    /// them comb-filter into the harsh metallic phasing that reads as "the game tried to combine
+    /// far too many sounds". Attenuating each voice does not fix that — the phasing is a function
+    /// of SIMULTANEITY, not level.
+    ///
+    /// So the policy has three distinct levers, and they do three different jobs:
+    ///   • <see cref="GameplaySFXCategoryPolicy.minRetriggerSeconds"/> — the DECOHERENCE lever.
+    ///     Guarantees two voices of a category never start within the same few milliseconds, which
+    ///     is what actually removes the comb filtering. A burst becomes a legible rattle of
+    ///     distinct hits instead of one phased blast.
+    ///   • <see cref="GameplaySFXCategoryPolicy.maxVoicesPerWindow"/> — the BUDGET lever (CPU and
+    ///     FMOD voices). Hard ceiling on how many voices a category may start per window.
+    ///   • <see cref="GameplaySFXCategoryPolicy.maxPendingVoices"/> — the MAGNITUDE lever. Events
+    ///     blocked by the two above are not simply thrown away: up to this many are folded into
+    ///     pending aggregates and replayed, spaced out and progressively quieter, at the centroid
+    ///     of the events they stand for. A 30-crystal burst still SOUNDS like many crystals; it
+    ///     just arrives as a short spaced rattle rather than a wall.
+    ///
+    /// Set <c>maxPendingVoices = 0</c> for the older pure-drop behaviour.
+    ///
+    /// Per CLAUDE.md config separation, all tuning lives here rather than as serialized fields on
+    /// <see cref="AudioSystem"/>. The shared asset is loaded from
+    /// <c>Resources/<see cref="ResourcePath"/></c> so a scene needs no wiring.
+    /// </summary>
+    [CreateAssetMenu(
+        fileName = "GameplaySFXPolicy",
+        menuName = "ScriptableObjects/Audio/Gameplay SFX Policy")]
+    public class GameplaySFXPolicySO : ScriptableObject
+    {
+        /// <summary>Resources path of the shared asset (zero-wire default).</summary>
+        public const string ResourcePath = "GameplaySFXPolicy";
+
+        [Header("Default (categories with no explicit entry below)")]
+        [Tooltip(
+            "Applied to any GameplaySFXCategory not listed in Category Policies. Deliberately " +
+            "permissive: a category nobody has identified as bursty should behave exactly as it " +
+            "did before this policy existed.")]
+        [SerializeField] GameplaySFXCategoryPolicy defaultPolicy = GameplaySFXCategoryPolicy.Unlimited();
+
+        [Header("Per-category overrides")]
+        [Tooltip(
+            "One entry per category that can fire in bursts. First matching entry wins; a " +
+            "category listed twice logs a warning on load.")]
+        [SerializeField] List<GameplaySFXCategoryPolicy> categoryPolicies = new()
+        {
+            // ── Crystals ────────────────────────────────────────────────────────────────────
+            // The reported bug. A vessel can sweep a crystal shower, an AOE blast can destroy a
+            // whole field at once, and every lifeform drops a heart on death — so a fauna die-off
+            // or a mass-kill mode (Wildlife Liberation, Rampage) detonates dozens in one frame,
+            // all at nearly the same world position.
+            new()
+            {
+                category            = GameplaySFXCategory.CrystalCollect,
+                volumeScale         = 0.8f,
+                maxVoicesPerWindow  = 3,
+                windowSeconds       = 0.12f,
+                minRetriggerSeconds = 0.045f,
+                burstVolumeFalloff  = 0.6f,
+                minBurstVolume      = 0.3f,
+                maxPendingVoices    = 2,
+            },
+            new()
+            {
+                category            = GameplaySFXCategory.CrystalSkim,
+                volumeScale         = 0.8f,
+                maxVoicesPerWindow  = 3,
+                windowSeconds       = 0.1f,
+                minRetriggerSeconds = 0.04f,
+                burstVolumeFalloff  = 0.65f,
+                minBurstVolume      = 0.3f,
+                maxPendingVoices    = 1,
+            },
+
+            // ── Elemental receive stingers ──────────────────────────────────────────────────
+            // The worst case in the whole table, because these are played 2D
+            // (AudioSystem.PlayGameplaySFX(category) with no position), so unlike the 3D
+            // categories they get ZERO spatial decorrelation — every copy lands at the listener
+            // dead centre and sums perfectly. One crystal pickup fires one of these ON TOP of a
+            // CrystalCollect, so a shower stacks two coherent families at once. Held to a
+            // near-single voice: this is a reward stinger, not a physical event, and it reads
+            // fine as one.
+            new()
+            {
+                category            = GameplaySFXCategory.ElementChargeReceived,
+                volumeScale         = 1f,
+                maxVoicesPerWindow  = 2,
+                windowSeconds       = 0.15f,
+                minRetriggerSeconds = 0.07f,
+                burstVolumeFalloff  = 0.5f,
+                minBurstVolume      = 0.35f,
+                maxPendingVoices    = 1,
+            },
+            new()
+            {
+                category            = GameplaySFXCategory.ElementMassReceived,
+                volumeScale         = 1f,
+                maxVoicesPerWindow  = 2,
+                windowSeconds       = 0.15f,
+                minRetriggerSeconds = 0.07f,
+                burstVolumeFalloff  = 0.5f,
+                minBurstVolume      = 0.35f,
+                maxPendingVoices    = 1,
+            },
+            new()
+            {
+                category            = GameplaySFXCategory.ElementSpaceReceived,
+                volumeScale         = 1f,
+                maxVoicesPerWindow  = 2,
+                windowSeconds       = 0.15f,
+                minRetriggerSeconds = 0.07f,
+                burstVolumeFalloff  = 0.5f,
+                minBurstVolume      = 0.35f,
+                maxPendingVoices    = 1,
+            },
+            new()
+            {
+                category            = GameplaySFXCategory.ElementTimeReceived,
+                volumeScale         = 1f,
+                maxVoicesPerWindow  = 2,
+                windowSeconds       = 0.15f,
+                minRetriggerSeconds = 0.07f,
+                burstVolumeFalloff  = 0.5f,
+                minBurstVolume      = 0.35f,
+                maxPendingVoices    = 1,
+            },
+
+            // ── Prisms / mass ───────────────────────────────────────────────────────────────
```

</details>

### `be0b96c85` — fix(audio): stop the duplicate crystal-collect one-shot; govern the shield bursts

_Claude, 2026-08-13 16:57:10 +0000_

```text
Follow-up from an exhaustive audit of every burst source that reaches AudioSystem.
Two real findings, both the same bug class as the crystal report.

1. DOUBLE-FIRE. A single vessel<->omni-crystal contact played CrystalCollect
   TWICE, in the same frame, at the same position: Unity raises OnTriggerEnter on
   both colliders of a contact pair, so VesselImpactor.AcceptImpactee (the
   `case OmniCrystalImpactor` branch) and OmniCrystalImpactor.AcceptImpactee ->
   ExecuteEffect -> Crystal.Explode -> PlayExplosionAudio each ran their own accept
   path with their own latch, and neither latch could see the other.

   Crystal's copy is removed. VesselImpactor is the one that survives because it is
   strictly the more reliable of the two: no owner/network gate and no vessel-type
   exclusion, so it fires on every peer for every vessel — whereas the crystal-side
   path early-returns on network clients (OmniCrystalImpactor.AcceptImpactee) and
   skips Explode entirely for the Manta. It could only ever add a duplicate, never
   be the only voice. Crystal's now-unused [Inject] AudioSystem goes with it.

   This mattered more after the burst limiter, not less: the limiter does not drop
   a duplicate, it HOLDS it and replays it ~45ms later as a soft echo — so the
   redundancy would have become an audible artifact on every single pickup, in the
   ordinary one-crystal-at-a-time case.

2. THE SHIELD PAIR was ungoverned, and one of them is the worst case in the table.
   ShieldActivate/ShieldDeactivate are played 2D (PrismStateManager passes no
   position), so unlike the spatialized categories they get zero decorrelation —
   every copy lands dead centre on the listener and sums perfectly. ShieldActivate
   is one voice per prism, and an AOE blast shields a whole neighbourhood in a
   frame. ShieldDeactivate is synchronized BY CONSTRUCTION: PrismTimerManager
   drains every expired shield timer in one Update, so prisms shielded together
   expire together. Both now carry the tightest spacing in the table, asserted by a
   test that no 2D category may be spaced looser than the spatialized CrystalCollect.

Also records in Docs/AUDIO.md the burst sources the audit traced (Wildlife
Liberation's ~1,409 live hearts as the measured worst case, the Wanderway belt's
~120 crystals) and — equally useful — the paths it CLEARED so nobody re-checks
them: AOE/explosion never touches crystals, lifeform death itself is silent, cell
drain/swap and turn-end teardown destroy crystals without audio.

Verified: all changed C# compiles clean under mcs with stubs in their real
namespaces; the behavioural harness runs the shipped limiter and policy green
including the two new categories; the regenerated Resources asset matches the C#
defaults entry-for-entry in order and validates key-by-key against the serialized
fields of both classes; Crystal.cs is brace-balance-neutral vs HEAD; no stale
references to the removed method or the retired throttle fields survive;
conditional-compilation check OK.
```

```text
 Assets/Resources/GameplaySFXPolicy.asset                     | 16 +++++++++++
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs  | 31 +++++++++++++++------
 Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs        |  9 ++++---
 Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs     | 31 +++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs | 61 +++++++++++++++++++++++++++++++++++-------
 Docs/AUDIO.md                                                | 37 ++++++++++++++++++++++++-
 6 files changed, 163 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 260 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index a05beb9d1..be56a14fa 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -4,7 +4,6 @@ using System.Collections;
 using System.Collections.Generic;
 using CosmicShore.Utility;
 using Cysharp.Threading.Tasks;
-using Reflex.Attributes;
 using Unity.Collections;
 using UnityEngine;
 using CosmicShore.Data;
@@ -22,7 +21,9 @@ namespace CosmicShore.Gameplay
 
     public class Crystal : CellItem
     {
-        [Inject] AudioSystem audioSystem;
+        // No [Inject] AudioSystem here: the crystal itself plays no audio. Its collect
+        // one-shot is owned by VesselImpactor (see the note in Explode), and injecting an
+        // unused service back in would invite a second emitter to grow on this class again.
 
         #region Inspector Fields
         [SerializeField]
@@ -307,14 +308,28 @@ namespace CosmicShore.Gameplay
                     explodeParams.Course * explodeParams.Speed, modelData.explodingMaterial, playerName);
             }
 
-            PlayExplosionAudio();
         }
 
-        void PlayExplosionAudio()
-        {
-            if (audioSystem != null)
-                audioSystem.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
-        }
+        // NOTE: this used to end with a PlayExplosionAudio() that fired
+        // GameplaySFXCategory.CrystalCollect at transform.position — a DUPLICATE of the one
+        // VesselImpactor already fires for the same pickup. Unity raises OnTriggerEnter on both
+        // colliders of a contact pair, so a single vessel↔crystal touch runs two independent
+        // accept paths with two independent latches: VesselImpactor.AcceptImpactee (the
+        // `case OmniCrystalImpactor` branch) AND OmniCrystalImpactor.AcceptImpactee →
+        // ExecuteEffect → here. Neither latch can see the other, so every pickup played the
+        // same one-shot twice, at the same position, in the same frame.
+        //
+        // VesselImpactor is the one that survives: it has no owner/network gate and no vessel-
+        // type exclusion, so it fires on every peer for every vessel. This path is a strict
+        // subset — OmniCrystalImpactor.AcceptImpactee early-returns on network clients, and
+        // ExecuteEffect skips Explode entirely for the Manta — so it could only ever add a
+        // duplicate, never the only voice.
+        //
+        // This matters more than "one redundant voice": two identical one-shots in one frame
+        // is the exact pathology Docs/AUDIO.md exists to prevent, and under the burst limiter
+        // the second one would be held back and replayed as a soft echo ~45 ms later on EVERY
+        // single pickup. Do not reinstate it — if a crystal needs its own destruction sound,
+        // give it its own category rather than a second CrystalCollect.
 
         /// <summary>
         /// Moves this heart out of its dying owner and onto the cell WITHOUT freeing it: it stays
diff --git a/Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs b/Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs
index 9ff0ace13..e93d63462 100644
--- a/Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs
+++ b/Assets/_Scripts/Editor/StripCrystalAudioSourceTool.cs
@@ -9,10 +9,11 @@ namespace CosmicShore.Editor
     /// One-shot editor tool: removes AudioSource components from all crystal
     /// prefabs in Assets/_Prefabs/Environment/.
     ///
-    /// Crystal.PlayExplosionAudio() no longer uses per-prefab AudioSources -
-    /// it fires an FMOD one-shot via AudioSystem.PlayGameplaySFX(category, position)
-    /// instead. Run this tool once after pulling this change to clean up the
-    /// dead components from all prefabs.
+    /// Crystals no longer use per-prefab AudioSources: the collect one-shot is an FMOD
+    /// event fired by VesselImpactor via AudioSystem.PlayGameplaySFX(category, position).
+    /// (Crystal.PlayExplosionAudio, named here previously, was itself removed — it was a
+    /// duplicate of that same VesselImpactor call; see the note in Crystal.Explode.)
+    /// Run this tool once after pulling to clean the dead components off the prefabs.
     ///
     /// Menu: FrogletTools > Ecology > Strip Crystal AudioSources
     /// </summary>
diff --git a/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs b/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
index efe4512a8..09c8640eb 100644
--- a/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
+++ b/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
@@ -215,6 +215,37 @@ namespace CosmicShore.ScriptableObjects
                 maxPendingVoices    = 2,
             },
 
+            // ── Shields ─────────────────────────────────────────────────────────────────────
+            // Both are 2D (PrismStateManager plays them with no position), so like the
+            // elemental stingers they get no spatial decorrelation at all. ShieldActivate is
+            // one voice PER PRISM — an AOE blast shields a whole neighbourhood in one frame.
+            // ShieldDeactivate is worse, and worse BY CONSTRUCTION: PrismTimerManager drains
+            // every expired shield timer in a single Update, so the prisms shielded together
+            // at t all expire together at t + duration. That is a perfectly synchronised burst
+            // of an identical 2D one-shot — the theoretical worst case for coherent summation.
+            new()
+            {
+                category            = GameplaySFXCategory.ShieldActivate,
+                volumeScale         = 0.6f,
+                maxVoicesPerWindow  = 2,
+                windowSeconds       = 0.12f,
+                minRetriggerSeconds = 0.06f,
+                burstVolumeFalloff  = 0.55f,
+                minBurstVolume      = 0.35f,
+                maxPendingVoices    = 1,
+            },
+            new()
+            {
+                category            = GameplaySFXCategory.ShieldDeactivate,
+                volumeScale         = 0.6f,
+                maxVoicesPerWindow  = 2,
+                windowSeconds       = 0.12f,
+                minRetriggerSeconds = 0.06f,
+                burstVolumeFalloff  = 0.55f,
+                minBurstVolume      = 0.35f,
+                maxPendingVoices    = 1,
+            },
+
             // ── Vessel contact ──────────────────────────────────────────────────────────────
             // A vessel grazing a trail generates one of these per prism contact per frame.
             new()
diff --git a/Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs b/Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs
index 9c42a6083..8fccc6c24 100644
--- a/Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs
+++ b/Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs
@@ -320,27 +320,70 @@ namespace CosmicShore.Tests
 
         // ── Policy asset ─────────────────────────────────────────────────────────────────────
 
+        // Every category a burst source is known to drive. Each entry is a real, traced burst
+        // path - see Docs/AUDIO.md §1 for the sources.
+        static readonly GameplaySFXCategory[] KnownBurstCategories =
+        {
+            GameplaySFXCategory.CrystalCollect,
+            GameplaySFXCategory.CrystalSkim,
+            GameplaySFXCategory.ElementChargeReceived,
+            GameplaySFXCategory.ElementMassReceived,
+            GameplaySFXCategory.ElementSpaceReceived,
+            GameplaySFXCategory.ElementTimeReceived,
+            GameplaySFXCategory.BlockDestroy,
+            GameplaySFXCategory.FloraCollision,
+            GameplaySFXCategory.CreatureBlockHit,
+            GameplaySFXCategory.CreatureDeath,
+            GameplaySFXCategory.Explosion,
+            GameplaySFXCategory.MineExplode,
+            GameplaySFXCategory.ShieldActivate,
+            GameplaySFXCategory.ShieldDeactivate,
+            GameplaySFXCategory.VesselImpact,
+            GameplaySFXCategory.TrackImpact,
+        };
+
```

</details>

### `0fec0f60c` — fix(audio): make the burst gate carry blast magnitude, and fix two ordering flaws

_Claude, 2026-08-13 17:43:11 +0000_

```text
The reported case is the Dolphin's AOE blast, not crystals. PrismSpatialIndex
caps damage at MAX_NEW_HITS_PER_FRAME = 48 and backlogs the rest, so a blast
destroys up to 48 prisms PER FRAME for many frames, each calling
Prism.PlayDestructionSFX -> BlockDestroy.

BlockDestroy was already "throttled", which is exactly why this was confusing:
the old gate capped the COUNT (4 per 100ms) and did nothing about SIMULTANEITY,
so its 4 admitted voices all started on the same frame and still summed
coherently at +12 dB and still comb-filtered. A voice budget alone is not a fix.
Simulated against the shipped policy, worst same-frame voices per blast:

  ungoverned   48 prisms -> 48 voices, 48 same-frame (+33.6 dB)
  old throttle 48 prisms ->  4 voices,  4 same-frame (+12.0 dB)
  shipped      48 prisms ->  3 voices,  1 same-frame ( +0.0 dB)

Three changes on top of the gate that landed in 617775c2:

1. MAGNITUDE. Killing the stacking removed the thing that made a big blast loud -
   accidentally and coherently, but loud. BlockDestroy's 0.35 attenuation existed
   solely to compensate for that stacking ("dozens of prisms can break in a single
   frame"), so with stacking gone it would simply have made the blast quiet.
   burstMagnitudeGain puts loudness back deliberately: a replayed voice is scaled
   by 1 + gain*log2(represents), clamped, so the ONE voice speaking for 70 prisms
   is louder than one speaking for 1. The old throttle gave a 48-prism blast and a
   300-prism blast identical energy (1.40 both); they now scale 1.54 / 2.83.

2. A PENDING BACKLOG OUTRANKS A FRESH ARRIVAL. Found by simulating a sustained
   blast rather than a single-frame burst. Events arrive every frame before every
   Drain, so letting the newest take the voice slot starved the queue for the whole
   blast: every voice represented exactly one prism, none carried the crowd, and
   the aggregates grew until the blast ended and released as a late thump. An
   isolated prism break still plays instantly - nothing is pending in that case.

3. OVERFLOW FOLDS INTO THE LIGHTEST AGGREGATE, not the last. Drain pops FIFO, so
   appending overflow to the last aggregate parked the entire crowd behind a
   single-event aggregate and the first replay carried no magnitude at all.

After 2 and 3, a sustained blast releases voices representing 72-96 prisms at
0.43-0.84 volume instead of 1 prism at 0.18-0.35.

Perf: suppressed events never reach FMODOneShotVolumeHelper, so a blast costs
3/5/21 CreateInstance-start-release calls instead of 48/300/2000 - 93.8% / 98.3%
/ 99.0% avoided.

Verified by executing the suite, not just compiling it: a reflection runner over
all 27 [Test] methods with throwing assertions passes 27/27 under mcs, including
four new regressions covering the two ordering flaws and the magnitude clamp. The
Dolphin blast simulation drives the shipped limiter and the shipped policy asset.
Asset revalidated key-by-key against both classes (16 entries x 10 fields, order
identical to the C# defaults). conditional-compilation check OK.
```

```text
 Assets/Resources/GameplaySFXPolicy.asset                     |  34 +++++++++++
 Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs     |  69 +++++++++++++++++++---
 Assets/_Scripts/System/Audio/GameplaySFXBurstLimiter.cs      |  63 ++++++++++++++++----
 Assets/_Scripts/Tests/Editor/GameplaySFXBurstLimiterTests.cs | 128 ++++++++++++++++++++++++++++++++++++++++-
 CLAUDE.md                                                    |   2 +-
 Docs/AUDIO.md                                                |  58 +++++++++++++++----
 6 files changed, 320 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 560 lines)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs b/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
index 09c8640eb..57a535688 100644
--- a/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
+++ b/Assets/_Scripts/ScriptableObjects/GameplaySFXPolicySO.cs
@@ -27,11 +27,21 @@ namespace CosmicShore.ScriptableObjects
     ///     distinct hits instead of one phased blast.
     ///   • <see cref="GameplaySFXCategoryPolicy.maxVoicesPerWindow"/> — the BUDGET lever (CPU and
     ///     FMOD voices). Hard ceiling on how many voices a category may start per window.
-    ///   • <see cref="GameplaySFXCategoryPolicy.maxPendingVoices"/> — the MAGNITUDE lever. Events
-    ///     blocked by the two above are not simply thrown away: up to this many are folded into
-    ///     pending aggregates and replayed, spaced out and progressively quieter, at the centroid
-    ///     of the events they stand for. A 30-crystal burst still SOUNDS like many crystals; it
-    ///     just arrives as a short spaced rattle rather than a wall.
+    ///   • <see cref="GameplaySFXCategoryPolicy.maxPendingVoices"/> +
+    ///     <see cref="GameplaySFXCategoryPolicy.burstMagnitudeGain"/> — the MAGNITUDE levers.
+    ///     Events blocked by the two above are not thrown away: up to <c>maxPendingVoices</c> of
+    ///     them are folded into pending aggregates and replayed, spaced out, at the centroid of
+    ///     the events they stand for — and scaled UP by how many that is
+    ///     (<c>1 + gain · log2(represents)</c>, clamped). A 300-prism Dolphin blast still SOUNDS
+    ///     like 300 prisms; it just arrives as a short spaced crunch rather than a wall.
+    ///
+    /// <para><b>The magnitude lever is not optional garnish — it replaces something the fix
+    /// removes.</b> Before the limiter, a big burst was loud precisely BECAUSE its voices stacked,
+    /// which is the defect; that is why <c>BlockDestroy</c> was attenuated to 0.35 in the first
+    /// place ("dozens of prisms can break in a single frame"). Kill the stacking and that
+    /// attenuation, left alone, would just make the blast quiet. So loudness is restored
+    /// deliberately and logarithmically on the ONE voice that speaks for the crowd, instead of
+    /// accidentally and coherently across N voices.</para>
     ///
     /// Set <c>maxPendingVoices = 0</c> for the older pure-drop behaviour.
     ///
@@ -74,6 +84,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.045f,
                 burstVolumeFalloff  = 0.6f,
                 minBurstVolume      = 0.3f,
+                burstMagnitudeGain  = 0.15f,
+                maxBurstMagnitude   = 1.6f,
                 maxPendingVoices    = 2,
             },
             new()
@@ -85,6 +97,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.04f,
                 burstVolumeFalloff  = 0.65f,
                 minBurstVolume      = 0.3f,
+                burstMagnitudeGain  = 0.15f,
+                maxBurstMagnitude   = 1.6f,
                 maxPendingVoices    = 1,
             },
 
@@ -105,6 +119,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.07f,
                 burstVolumeFalloff  = 0.5f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.0f,
+                maxBurstMagnitude   = 1.0f,
                 maxPendingVoices    = 1,
             },
             new()
@@ -116,6 +132,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.07f,
                 burstVolumeFalloff  = 0.5f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.0f,
+                maxBurstMagnitude   = 1.0f,
                 maxPendingVoices    = 1,
             },
             new()
@@ -127,6 +145,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.07f,
                 burstVolumeFalloff  = 0.5f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.0f,
+                maxBurstMagnitude   = 1.0f,
                 maxPendingVoices    = 1,
             },
             new()
@@ -138,6 +158,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.07f,
                 burstVolumeFalloff  = 0.5f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.0f,
+                maxBurstMagnitude   = 1.0f,
                 maxPendingVoices    = 1,
             },
 
@@ -154,6 +176,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.02f,
                 burstVolumeFalloff  = 0.8f,
                 minBurstVolume      = 0.4f,
+                burstMagnitudeGain  = 0.3f,
+                maxBurstMagnitude   = 2.4f,
                 maxPendingVoices    = 2,
             },
             new()
@@ -165,6 +189,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.03f,
                 burstVolumeFalloff  = 0.7f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.3f,
+                maxBurstMagnitude   = 2.4f,
                 maxPendingVoices    = 1,
             },
             new()
@@ -176,6 +202,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.03f,
                 burstVolumeFalloff  = 0.7f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.3f,
+                maxBurstMagnitude   = 2.4f,
                 maxPendingVoices    = 1,
             },
 
@@ -189,6 +217,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.03f,
                 burstVolumeFalloff  = 0.7f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.25f,
+                maxBurstMagnitude   = 2.0f,
                 maxPendingVoices    = 1,
             },
             new()
@@ -200,6 +230,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.035f,
                 burstVolumeFalloff  = 0.7f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.25f,
+                maxBurstMagnitude   = 2.0f,
                 maxPendingVoices    = 1,
             },
             new()
@@ -212,6 +244,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.05f,
                 burstVolumeFalloff  = 0.65f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.25f,
+                maxBurstMagnitude   = 2.0f,
                 maxPendingVoices    = 2,
             },
 
@@ -232,6 +266,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.06f,
                 burstVolumeFalloff  = 0.55f,
                 minBurstVolume      = 0.35f,
+                burstMagnitudeGain  = 0.2f,
+                maxBurstMagnitude   = 1.8f,
                 maxPendingVoices    = 1,
             },
             new()
@@ -243,6 +279,8 @@ namespace CosmicShore.ScriptableObjects
                 minRetriggerSeconds = 0.06f,
```

</details>

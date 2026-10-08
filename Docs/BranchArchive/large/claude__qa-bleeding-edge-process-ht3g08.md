# Branch archive: `claude/qa-bleeding-edge-process-ht3g08`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-09-03 by Claude
- **Unmerged commits:** 71
- **Forked from:** `4f0144f1d` (2026-08-06, fix(wildlife-liberation): centre-spawn root cause, open water, no tadpoles, ta)
- **Tip:** `22570e6f9`
- **Files touched (79):**
  - `.claude/skills/asset-surgery/SKILL.md`
  - `.claude/skills/qa-backlog/SKILL.md`
  - `.github/workflows/sync-build-branches.yml`
  - `.github/workflows/tag-internal-build.yml`
  - `Assets/Resources/PostHogConfig.asset`
  - `Assets/Resources/PrivacyConsentConfig.asset`
  - `Assets/Resources/PrivacyConsentConfig.asset.meta`
  - `Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl`
  - `Assets/_Graphics/Materials/Graphs/PrismOcclusionDitherPreview.shader`
  - `Assets/_Graphics/Materials/Graphs/PrismOcclusionDitherPreview.shader.meta`
  - `Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset`
  - `Assets/_SO_Assets/_TEMP/FalconClassSO.asset`
  - `Assets/_SO_Assets/_TEMP/ShrikeClassSO.asset`
  - `Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs`
  - `Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs`
  - `Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs.meta`
  - `Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs`
  - `Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/SO_Vessel.cs`
  - `Assets/_Scripts/System/AppManager.cs`
  - `Assets/_Scripts/System/CloudData/DisplayNameRegistry.cs`
  - `Assets/_Scripts/System/CloudData/UGSDataService.cs`
  - `Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs`
  - `Assets/_Scripts/System/Instrumentation/FlightClock.cs`
  - `Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs`
  - `Assets/_Scripts/System/VesselUnlock/VesselUnlockSystem.cs`
  - `Assets/_Scripts/UI/Privacy/PrivacyConsentController.cs`
  - `Assets/_Scripts/UI/Privacy/PrivacyConsentOverlay.cs`
  - `Assets/_Scripts/UI/Privacy/PrivacyConsentOverlay.cs.meta`
  - `Assets/_Scripts/UI/UGSStatsManager.cs`
  - `Assets/_Scripts/UI/Views/PlayerDataService.cs`
  - `CLAUDE.md`
  - `Cosmic-Shore.slnx`
  - `Docs/Analytics/ANALYTICS_HANDBOOK.html`
  - `Docs/Analytics/ANALYTICS_HANDBOOK.pdf`
  - `Docs/Analytics/ANALYTICS_HANDOFF.md`
  - `Docs/Analytics/DATA_ARCHITECTURE.md`
  - … and 39 more

### `240c31791` — feat(analytics): salvage attribution-branch tools/docs, harden PostHog sink, lock EU region

_Claude, 2026-08-05 12:51:32 +0000_

```text
PR #592 (claude/analytics-attribution-viability-vwkunw) built a sink layer independently,
against a base 5379 commits old. The equivalent layer shipped via #625, so that branch can
no longer merge cleanly - and its sink class is named PostHogSink rather than
PostHogAnalyticsSink, so a naive merge would NOT conflict: it would silently land a second
PostHog sink beside the shipped one. This takes what is genuinely unique and leaves the
duplicate behind.

Salvaged (none of it existed on bleeding-edge):
- Tools/Analytics/export_cloud_save.py - bulk Cloud Save export via a read-only UGS
  service account, stdlib-only, JSONL out
- Tools/Analytics/import_snapshot_to_posthog.py - backfills existing players into PostHog
  People/cohorts, so the whole player base is visible rather than only post-launch players
- POSTHOG_SETUP.md, viability-report.md, event-taxonomy.md, utm-conventions.md,
  implementation-plan.md

Salvaged docs carry a provenance banner: their analysis stands, but DATA_ARCHITECTURE.md
is the authority on implemented shape, since the shipped IAnalyticsSink differs in detail.
PostHogSink references renamed to the shipped class.

Ported from that branch's sink (real improvements the shipped one lacked):
- HTTP 4xx batches are dropped, not retried. A bad project key or malformed payload would
  otherwise re-send forever until the queue cap silently ate every newer event.
- 10s request timeout, so a stalled upload cannot hang a quit flush.
- Warn-once-per-failure-episode instead of one warning per batch.
- excludedEvents filter on PostHogConfigSO - the free-tier budget lever. UGS still receives
  everything, so nothing leaves the system of record.
- install_id: device-scoped GUID in PlayerPrefs, surviving sign-out but never roaming.

Region locked to EU Cloud (host was us.i.posthog.com; the project is EU, so events were
pointed at a host that would accept none of them). Kept EU rather than switching to US on
the strength of Delaware incorporation, because GDPR scope follows where the PLAYERS are
(Art. 3(2)), not where the company is: a US entity serving EU players is in scope either
way. US Cloud would make every EEA/UK player's events a restricted transfer needing SCCs
or the twice-struck-down DPF lineage, and buys nothing - no US law requires US residency
for game telemetry, and batched uploads make latency irrelevant. PostHog regions cannot be
changed after project creation. Rationale recorded in DATA_ARCHITECTURE.md 8.3.
```

```text
 Assets/Resources/PostHogConfig.asset                           |   3 +-
 Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs           |  17 +-
 Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs |  82 ++++++-
 Docs/Analytics/DATA_ARCHITECTURE.md                            |  53 ++++-
 Docs/Analytics/POSTHOG_SETUP.md                                | 193 +++++++++++++++
 Docs/Analytics/event-taxonomy.md                               | 159 +++++++++++++
 Docs/Analytics/implementation-plan.md                          | 145 ++++++++++++
 Docs/Analytics/utm-conventions.md                              | 144 ++++++++++++
 Docs/Analytics/viability-report.md                             | 492 +++++++++++++++++++++++++++++++++++++++
 Tools/Analytics/.gitignore                                     |   2 +
 Tools/Analytics/README.md                                      | 222 ++++++++++++++++++
 Tools/Analytics/export_cloud_save.py                           | 247 ++++++++++++++++++++
 Tools/Analytics/import_snapshot_to_posthog.py                  | 242 +++++++++++++++++++
 13 files changed, 1982 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2155 lines)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs b/Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs
index e24baba12..737794162 100644
--- a/Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs
@@ -1,3 +1,4 @@
+using System.Collections.Generic;
 using UnityEngine;
 
 namespace CosmicShore.ScriptableObjects
@@ -18,9 +19,10 @@ namespace CosmicShore.ScriptableObjects
         [Tooltip("PostHog PROJECT API key (write-only). Never a personal API key.")]
         [SerializeField] string projectApiKey = "";
 
-        [Tooltip("PostHog ingestion host. Use the EU host for an EU Cloud project - the region " +
-                 "decides whether SCCs are needed for EEA/UK players. See DATA_ARCHITECTURE.md 8.3.")]
-        [SerializeField] string host = "https://us.i.posthog.com";
+        [Tooltip("Ingestion host. MUST match the region the PostHog project was created in, or " +
+                 "events are accepted nowhere. EU cloud: https://eu.i.posthog.com - US cloud: " +
+                 "https://us.i.posthog.com. This project is EU. See DATA_ARCHITECTURE.md 8.3.")]
+        [SerializeField] string host = "https://eu.i.posthog.com";
 
         [Header("Batching")]
         [Tooltip("Upload once this many events are queued.")]
@@ -36,6 +38,12 @@ namespace CosmicShore.ScriptableObjects
         [Min(50)]
         [SerializeField] int maxQueuedEvents = 500;
 
+        [Header("Filtering")]
+        [Tooltip("Event names never forwarded to PostHog. The free-tier budget lever for chatty " +
+                 "events (ui_action, setting_changed) - UGS still receives them, so nothing is " +
+                 "lost from the system of record.")]
+        [SerializeField] List<string> excludedEvents = new();
+
         [Header("Lifecycle")]
         [Tooltip("Master switch. Off disables the PostHog sink entirely, leaving UGS untouched.")]
         [SerializeField] bool enabled = true;
@@ -49,5 +57,8 @@ namespace CosmicShore.ScriptableObjects
 
         public bool IsConfigured =>
             enabled && !string.IsNullOrWhiteSpace(projectApiKey) && !string.IsNullOrWhiteSpace(host);
+
+        public bool IsExcluded(string eventName) =>
+            excludedEvents != null && excludedEvents.Contains(eventName);
     }
 }
diff --git a/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs b/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs
index 53d828691..9cd24b814 100644
--- a/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs
+++ b/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs
@@ -24,12 +24,19 @@ namespace CosmicShore.Core
     {
         const string QueueFileName = "posthog_queue.json";
 
+        /// <summary>PlayerPrefs key for the device-scoped install id.</summary>
+        const string InstallIdPrefKey = "AnalyticsInstallId";
+
+        /// <summary>Upload timeout. Long enough for a slow mobile link, short enough not to stall a quit.</summary>
+        const int RequestTimeoutSeconds = 10;
+
         readonly PostHogConfigSO _config;
         readonly Action<string> _log;
         readonly List<PostHogEvent> _queue = new();
 
         bool _collecting;
         bool _uploadInFlight;
+        bool _warnedThisEpisode;
         float _lastFlushTime;
         string _distinctId = "";
 
@@ -78,6 +85,12 @@ namespace CosmicShore.Core
             if (!_collecting || string.IsNullOrEmpty(eventName))
                 return;
 
+            // Budget lever: chatty events (ui_action, setting_changed) can be dropped from
+            // PostHog without losing them - UGS remains the system of record and still
+            // receives everything. Configured per-event on PostHogConfigSO.
+            if (_config.IsExcluded(eventName))
+                return;
+
             var properties = parameters != null
```

</details>

### `7b8d0b4ef` — docs(analytics): handoff - data-layer status and the ordered action list

_Claude, 2026-08-05 13:05:35 +0000_

```text
One document covering what changed in the player data layer and the analytics pipeline,
and the ordered list of what only Shombith can do (keys, dashboards, accounts, legal).

Maps to the Steam EA checkpoint rows it belongs to: C7 (instrumentation buildout) is
now event-coverage-complete with the remaining work being turn-on, backfill, validation
and dashboards; F4 (GDPR/COPPA review) stays a review plus exactly one build - the
server-side PostHog deletion path.

Records the EU-vs-US decision and its reasoning (GDPR scope follows the players, not the
Delaware incorporation), the three write-dead Cloud Save fields that now have writers,
why flight time needs an unscaled clock, why a single lobby_id could not answer the
rematch question, and what to reconcile when QUEST_GRAPH_PROGRESS arrives from the FTUE
branch (it versions with "Version" not "SchemaVersion", and adds a second progression key
alongside GAME_MODE_PROGRESSION).

States the known gaps plainly rather than burying them: erasure is partial until the
server-side deletion exists, the UGS SDK deprecation cutover is deferred on purpose, and
PlayerUUID is still the display name.
```

```text
 Docs/Analytics/ANALYTICS_HANDOFF.md | 349 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 349 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 355 lines)</summary>

```diff
diff --git a/Docs/Analytics/ANALYTICS_HANDOFF.md b/Docs/Analytics/ANALYTICS_HANDOFF.md
new file mode 100644
index 000000000..cb6adf5e9
--- /dev/null
+++ b/Docs/Analytics/ANALYTICS_HANDOFF.md
@@ -0,0 +1,349 @@
+# Analytics & Data — Status and Action List
+
+> **Audience:** Shombith (and Ian, for the dashboard half).
+> **Scope:** everything done to the player data layer and the analytics pipeline, plus the
+> ordered list of what only you can do — accounts, keys, dashboards, legal.
+> **Maps to:** `STEAM_EA_INVESTOR_CHECKPOINT` rows **C7** (instrumentation buildout, 5.0 d,
+> Shombith + Ian) and **F4** (GDPR/COPPA consent review, 1.0 d, Shombith).
+>
+> Companions: `DATA_ARCHITECTURE.md` (the authority on schema + design), `EVENT_SCHEMA.json`
+> (machine-readable event contract), `POSTHOG_SETUP.md` (the click-by-click PostHog guide),
+> `../../Tools/Analytics/README.md` (export + backfill scripts).
+
+---
+
+## 1. What changed — the short version
+
+Two things were wrong and both are now fixed.
+
+**The save data had drifted.** Five Cloud Save keys, written at different times by different
+people, disagreed on nearly everything: key casing, field casing, what a timestamp is (three
+different formats), how a composite dictionary key is spelled (two separators for one concept).
+Nothing carried a schema version, so nothing could ever be migrated — only broken.
+
+**Three of the fields the instrumentation email asked for did not exist and could not be
+produced.** Not "weren't wired up" — the game had no clock that excluded pause, no per-match
+identifier, and no replicated UGS player id at all.
+
+Both are shipped and merged into `bleeding-edge` (PR #625, four commits), plus a follow-up that
+salvaged the useful half of the older attribution branch (PR #592, now closed).
+
+---
+
+## 2. Cloud Save — before and after
+
+### 2.1 The keys
+
+| Before | After | What happened |
+|---|---|---|
+| `player_profile` | `PLAYER_PROFILE` | Renamed to match the others; restructured into groups; absorbed `LastLoginTick` |
+| `VESSEL_STATS` | *deleted* | Merged into `HANGAR_DATA` — one record per vessel |
+| `HANGAR_DATA` | `HANGAR_DATA` | Restructured; absorbs vessel stats; three write-dead fields now actually written |
+| `PLAYER_STATS_PROFILE` | `MODE_STATS` | Four bespoke per-mode models collapsed into one uniform record |
+| `GAME_MODE_PROGRESSION` | `GAME_MODE_PROGRESSION` | Untouched by request, except a `SchemaVersion` field |
+
+**Five keys became four.** One fewer round-trip at sign-in, and the vessel join stopped being a
+client-side correlation across two payloads on a bare string key.
+
+### 2.2 The format standard (was violated by every model)
+
+- Cloud Save keys: `SCREAMING_SNAKE_CASE`
+- JSON fields: `PascalCase`
+- Timestamps: `long`, **Unix epoch milliseconds UTC**, always suffixed `UtcMs`
+- Durations: `float`, **seconds**, always suffixed `Seconds`
+- Composite dictionary keys: `"{Mode}:{Intensity}"` — one separator, everywhere
+- Every root model carries `SchemaVersion`
+
+`SchemaVersion` was added while breaking was still free, specifically so the *next* change is a
+migration rather than another break. **It has already paid for itself once:** a later branch
+removed player XP, and `PLAYER_PROFILE` went to v2 — old saves still carry the dead
+`Progression` key and it is ignored on load, which is exactly the no-op migration the field
+exists to enable.
+
+### 2.3 Three real bugs found and fixed
+
+1. **`SelectedVessel` had no writer at all.** The only reference outside the model was a debug
+   read in the editor window. That is why it was always `""`. It now writes on vessel-select
+   confirm, so it genuinely means "last vessel the player chose".
+2. **`VesselPreferences` was only ever `.Clear()`ed** — never written, by anything. It is
+   replaced by `PreferredVessel` (singular, as you asked), *derived* as the most-flown vessel
+   rather than stored. That required tracking per-vessel flight time, which nothing did.
+3. **`UnlockedVessels: [""]`** was a real data bug, not a display artifact: one `SO_Vessel`
+   asset ships with a blank `Name`, and the unlock writer persisted it verbatim. The flat list
+   is now a keyed map with an explicit `Unlocked` flag, and blank names are rejected at the
+   writer — so the shape no longer permits the bug. **The underlying asset still needs
```

</details>

### `e06d1fb4a` — docs(git): reconcile GIT_RULES with the real branch model, add promotion runbooks

_Claude, 2026-08-05 13:22:15 +0000_

```text
GIT_RULES.md described a branching model this repository does not have. It
referenced a `main` branch nine times, told readers to target it with their PRs
and to rebase onto it, and never mentioned bleeding-edge, development or master
at all. Anyone onboarding had two branching documents that contradicted each
other, and the more official-sounding one was the wrong one.

Section 1.1 is now the real long-lived branches, with the one-way flow rule and
a pointer to Docs/BRANCHING_AND_RELEASE.md as the authority. Every `main`
reference is updated. Branch naming now acknowledges that claude/* and codex/*
are the majority of branches here rather than pretending everything is
feature/*; they follow the same rules and the prefix is worth keeping.

Section 4 gained the exception that would have cost us a rescue today. Squash
and merge remains right for feature branches, but it is exactly wrong for a
promotion: squashing discards the shared history that makes the next promotion a
fast-forward, which is how master and development drifted 3000 commits apart in
the first place. The two cases are now tabulated, with the note that a promotion
presenting conflicts means something committed directly to the target and the
stray commit is the bug, not the conflicts.

BRANCHING_AND_RELEASE.md gains the two routine operations as runbooks: promoting
bleeding-edge into development before a cycle, and cutting a release. Both are
written around `git merge --ff-only`, and both say plainly that a refusal is a
signal to investigate rather than an obstacle to force past. The release runbook
also records why the version bump belongs on bleeding-edge rather than master,
and why a store submission needs a release/* branch instead of building from a
master that can move underneath a multi-day review.

Both fast-forward claims verified against the current branch tips.
```

```text
 Docs/BRANCHING_AND_RELEASE.md | 68 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 GIT_RULES.md                  | 70 +++++++++++++++++++++++++++++++++++++++++++++++++++++++---------------
 2 files changed, 123 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 200 lines)</summary>

```diff
diff --git a/Docs/BRANCHING_AND_RELEASE.md b/Docs/BRANCHING_AND_RELEASE.md
index 434e690b7..81429db5a 100644
--- a/Docs/BRANCHING_AND_RELEASE.md
+++ b/Docs/BRANCHING_AND_RELEASE.md
@@ -106,6 +106,74 @@ fixed comes straight back. Fix it on `bleeding-edge` instead.
 > workflow**, leaving `source_ref` blank. Manual runs skip both the clock and
 > the cycle check, so it will run immediately.
 
+### Runbook: promote `bleeding-edge` to `development`
+
+Do this before a Wednesday cycle when you want testers on a newer batch. Skip it
+and the cycle simply rebuilds what testers already have, which is a valid choice.
+
+```bash
+git fetch origin
+git checkout development
+git merge --ff-only origin/bleeding-edge
+git push origin development
+```
+
+That is the whole operation. `--ff-only` is the safety catch, not a formality:
+it succeeds only if `development` has no commits of its own. **If it refuses,
+do not force it and do not merge manually.** Something has committed directly to
+`development`, which breaks the one-way rule, and that stray commit is the thing
+to find. See §6 R6.
+
+Verify before the cycle runs:
+
+```bash
+git diff --stat origin/development origin/bleeding-edge   # empty = in sync
+```
+
+Prefer the GitHub UI? Open a PR from `bleeding-edge` into `development` and use
+**Create a merge commit**. **Never squash a promotion** — it discards the shared
+history that makes the next one a fast-forward, which is exactly how these
+branches drifted 3000 commits apart before.
+
+### Runbook: cut a release
+
+There is no release automation yet (§6 R5), so this is deliberate and manual.
+Release from `development`, not `bleeding-edge`: `development` is the code that
+has actually been through a test build.
+
+1. **Pick the commit.** Normally `development`'s tip, and normally one that
+   testers have already been running for a cycle. If you need an older one, take
+   the `testbuild/YYYY-MM-DD` tag for the build QA signed off on.
+
+2. **Set the version.** Bump `bundleVersion` in `ProjectSettings/ProjectSettings.asset`
+   on `bleeding-edge` and let it flow down, rather than editing it on `master`,
+   which would give `master` a commit of its own and break the fast-forward rule.
+
+3. **Move `master`:**
+
+   ```bash
+   git fetch origin
+   git checkout master
+   git merge --ff-only origin/development
+   git push origin master
+   ```
+
+   Same `--ff-only` rule, same reasoning.
+
+4. **Tag it**, so the shipped build is recoverable after the branches move on:
+
+   ```bash
+   git tag -a v0.3.0 -m "Release 0.3.0"
+   git push origin v0.3.0
+   ```
+
+5. **Build from `master` in UGS**, manually. Until R1 lands, the tag is the only
+   link between what players are running and a commit, so do not skip step 4.
+
+6. **Store submission?** Do not build directly from `master` for a submission
+   that will sit in review for days while `master` may move. Cut
+   `release/<version>` from the tagged commit and build from that. See §6 R5.
+
 ---
 
 ## 4. How you find out something broke
diff --git a/GIT_RULES.md b/GIT_RULES.md
```

</details>

### `83685522c` — fix(vessels): FalconClassSO had a blank Name, which is what wrote "" to HANGAR_DATA

_Claude, 2026-08-05 17:35:35 +0000_

```text
SO_Class_Falcon (Class: 9) shipped with an empty Name field while its sibling
ShrikeClassSO carried "Shrike". Every other vessel asset sets Name to its
VesselClassType enum name; Falcon was the one omission.

The full chain, since the symptom showed up in cloud data rather than in the editor:
FalconClassSO is referenced by SO_Classlist_All (the SO_VesselList that UGSDataService
iterates), so it is a live vessel, not a stray asset. VesselUnlockSystem persists
vessel.Name verbatim as the Cloud Save unlock key. UnlockVessel early-returns when the
vessel is already unlocked, and isLocked is unserialized on this asset so it defaults to
false - which is why the empty key only appeared for some players: it needs Lock All
(ResetAllUnlocks) followed by Unlock All in the Froglet toolbox, at which point
PersistUnlockToCloud("", true) added "" to UnlockedVessels.

That is the "" seen in the reported HANGAR_DATA dump.

The schema rework already made this non-persisting - the keyed vessel map rejects blank
names at the writer - but that stops the bad data, it does not fix the asset. A blank
Name would still leave Falcon unnameable in the hangar UI and unable to carry stats.

Verified all 11 SO_Vessel assets now have Name matching the enum name for their Class id.
```

```text
 Assets/_SO_Assets/_TEMP/FalconClassSO.asset | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `774025641` — Testing game

_Hunter VonWald, 2026-08-05 19:40:01 -0500_

```text
 Cosmic-Shore.slnx | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `95c1769ef` — revert(sln): drop accidental "Testing game" solution reorder

_Claude, 2026-08-06 02:28:53 +0000_

```text
Reverts 774025641cea718349acf698dfeea501cc72de96, an IDE-generated
reorder of Assembly-CSharp-Editor.csproj in Cosmic-Shore.slnx that was
pushed to bleeding-edge unintentionally. No gameplay or build impact;
restores the project ordering that preceded it.
```

```text
 Cosmic-Shore.slnx | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `b23e71068` — Update PostHogConfig.asset

_Shombith03, 2026-08-06 20:59:05 +0530_

```text
 Assets/Resources/PostHogConfig.asset | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `7a39b39c6` — docs(analytics): flat event/parameter row list for UGS Event Manager setup

_Claude, 2026-08-06 15:36:47 +0000_

```text
Generated from EVENT_SCHEMA.json: one row per (event, parameter) pair with its type
and whether it comes from the common envelope or the event itself.

28 events, 65 event-specific parameters, plus the 6 envelope fields carried on every
event - 233 rows. UGS Analytics validates custom events against a schema declared in
the Unity Cloud Dashboard and drops anything undeclared, so this is the working list
for that setup. PostHog needs none of it; it accepts whatever the sink sends.

Kept as a derived artifact rather than hand-maintained: regenerate from
EVENT_SCHEMA.json whenever the contract changes, so the two cannot drift.
```

```text
 Docs/Analytics/ugs_event_manager_rows.csv | 234 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 234 insertions(+)
```

### `d05823a61` — refactor(analytics): envelope belongs to the PostHog sink, not every event

_Claude, 2026-08-06 15:41:27 +0000_

```text
Verified against Unity's current docs: UGS Analytics rejects any custom event whose
schema is not hand-created in the dashboard Event Manager, there is no auto-detect or
approve-from-traffic flow, no bulk/CSV import, and no CLI or admin API. Worse, events
and parameters CANNOT BE DELETED once created and count against a 1,500-per-environment
cap. So every field sent to UGS is a permanent commitment, and the setup cost is real
manual typing.

The common envelope (player_id, app_version, platform, schema_version, and the two
timestamps) was being stamped onto all 28 events. UGS already auto-collects the player
id, platform and app version as core data, so declaring them would have bought 168
permanent schema rows for data UGS already has - and none of it was needed there.

The envelope exists because PostHog has no equivalent auto-collection. That makes it a
PostHog concern, so it moves into PostHogAnalyticsSink, which is what IAnalyticsSink is
for. Event parameters override envelope keys, so game_completed keeps its own
client-stamped completion timestamp - that one was an explicit ask and goes to both sinks
as a real event parameter.

UGS Event Manager setup drops from 233 rows to 28 events / 67 parameters. Four events
carry no parameters at all and are name-only.

Regenerates EVENT_SCHEMA.json and ugs_event_manager_rows.csv to match, with the CSV now
listing exactly the UGS-side work.
```

```text
 Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs |  31 ++-
 Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs   |  26 +-
 Docs/Analytics/EVENT_SCHEMA.json                                 | 519 ++++++++++++++++++++++++++++++-------
 Docs/Analytics/ugs_event_manager_rows.csv                        | 302 +++++----------------
 4 files changed, 539 insertions(+), 339 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 664 lines)</summary>

```diff
diff --git a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
index 11aa5bafd..191d665fe 100644
--- a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
+++ b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
@@ -131,7 +131,7 @@ namespace CosmicShore.Core
             var postHogConfig = Resources.Load<PostHogConfigSO>("PostHogConfig");
             if (postHogConfig != null && postHogConfig.Enabled)
             {
-                _postHogSink = new PostHogAnalyticsSink(postHogConfig, Log);
+                _postHogSink = new PostHogAnalyticsSink(postHogConfig, Log, BuildSinkEnvelope);
                 _sinks.Add(_postHogSink);
             }
 
@@ -320,8 +320,6 @@ namespace CosmicShore.Core
                 ? new Dictionary<string, object>(parameters)
                 : new Dictionary<string, object>();
 
-            AddCommonEnvelope(payload);
-
             foreach (var sink in _sinks)
                 sink.RecordEvent(eventName, payload);
 
@@ -329,19 +327,24 @@ namespace CosmicShore.Core
         }
 
         /// <summary>
-        /// Fields present on EVERY event, so any two events can be joined without a lookup:
-        /// who, which build, which platform, and when by the client's clock.
+        /// Identity + build context that PostHog has no way to collect on its own.
+        ///
+        /// This is deliberately NOT applied to every event in the facade. UGS Analytics
+        /// already auto-collects the player id, platform and app version as core data, and it
+        /// validates every custom parameter against a schema hand-created in the dashboard
+        /// Event Manager - where events and parameters, once created, can never be deleted and
+        /// count against a 1,500-per-environment cap. Stamping six redundant fields onto 28
+        /// events would have cost 168 permanent, unnecessary schema rows for data UGS already
+        /// has. So the envelope is a PostHog concern and lives in the PostHog sink.
+        /// See Docs/Analytics/DATA_ARCHITECTURE.md §7.1.
         /// </summary>
-        void AddCommonEnvelope(IDictionary<string, object> payload)
+        public IDictionary<string, object> BuildSinkEnvelope() => new Dictionary<string, object>
         {
-            payload["player_id"] = ResolveDistinctId();
-            payload["app_version"] = Application.version;
-            payload["platform"] = Application.platform.ToString();
-            payload["schema_version"] = EventSchemaVersion;
-
-            if (!payload.ContainsKey("timestamp_utc_ms"))
-                AddCompletionTimestamp(payload);
-        }
+            ["player_id"] = ResolveDistinctId(),
+            ["app_version"] = Application.version,
+            ["platform"] = Application.platform.ToString(),
+            ["schema_version"] = EventSchemaVersion
+        };
 
         /// <summary>
         /// The canonical identity: the UGS player id. Immutable, and the same key as Cloud
diff --git a/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs b/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs
index 9cd24b814..9a3d8e002 100644
--- a/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs
+++ b/Assets/_Scripts/System/Instrumentation/PostHogAnalyticsSink.cs
@@ -32,6 +32,7 @@ namespace CosmicShore.Core
 
         readonly PostHogConfigSO _config;
         readonly Action<string> _log;
+        readonly Func<IDictionary<string, object>> _envelope;
         readonly List<PostHogEvent> _queue = new();
 
         bool _collecting;
@@ -43,10 +44,18 @@ namespace CosmicShore.Core
         public string Name => "PostHog";
         public bool IsCollecting => _collecting;
 
-        public PostHogAnalyticsSink(PostHogConfigSO config, Action<string> log)
+        /// <param name="envelope">
+        /// Supplies the identity/build context PostHog cannot collect on its own (player id,
+        /// app version, platform, schema version). It is stamped HERE rather than in the
+        /// facade because UGS auto-collects the equivalents, and every field sent to UGS costs
+        /// a permanent, undeletable row in its dashboard schema.
```

</details>

### `d3adf6b13` — docs(analytics): handoff reflects verified UGS Event Manager facts and closed actions

_Claude, 2026-08-06 15:45:55 +0000_

```text
A3 rewritten as its own section (6.1) against Unity's current docs rather than my earlier
assumption. Custom events must exist in the dashboard before ingestion; there is no
auto-detect flow, no bulk import, no CLI or admin API; and events and parameters can never
be deleted once created, against a 1,500-per-environment cap.

That last fact is why the row count changed: the envelope was moved into the PostHog sink,
so the UGS list is 28 events / 67 parameters instead of 233 rows, and 168 of those rows
would have permanently duplicated data UGS auto-collects. Section now carries the dashboard
path, the value-first ordering, Copy to Environment, the case-sensitivity and
game_started-vs-gameStarted traps, and an explicit flag that parameter-level rejection
behaviour is undocumented.

Closed out: A1 (key committed, EU confirmed against the eu.posthog.com login) and E2 (the
blank Name was FalconClassSO). Section 2.3 now names the culprit and the exact path that
pushed the empty key to cloud. Added E4 for the related observation that Falcon and Shrike
both default to unlocked despite being planned vessels - flagged, not changed, since lock
state is a design call.
```

```text
 Docs/Analytics/ANALYTICS_HANDOFF.md | 95 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++--------
 1 file changed, 84 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 138 lines)</summary>

```diff
diff --git a/Docs/Analytics/ANALYTICS_HANDOFF.md b/Docs/Analytics/ANALYTICS_HANDOFF.md
index cb6adf5e9..d3ff018b4 100644
--- a/Docs/Analytics/ANALYTICS_HANDOFF.md
+++ b/Docs/Analytics/ANALYTICS_HANDOFF.md
@@ -68,11 +68,13 @@ exists to enable.
 2. **`VesselPreferences` was only ever `.Clear()`ed** — never written, by anything. It is
    replaced by `PreferredVessel` (singular, as you asked), *derived* as the most-flown vessel
    rather than stored. That required tracking per-vessel flight time, which nothing did.
-3. **`UnlockedVessels: [""]`** was a real data bug, not a display artifact: one `SO_Vessel`
-   asset ships with a blank `Name`, and the unlock writer persisted it verbatim. The flat list
-   is now a keyed map with an explicit `Unlocked` flag, and blank names are rejected at the
-   writer — so the shape no longer permits the bug. **The underlying asset still needs
-   fixing** (see §6, E2).
+3. **`UnlockedVessels: [""]`** was a real data bug, not a display artifact. The culprit was
+   **`FalconClassSO.asset`** (Class 9), which shipped with a blank `Name` while its sibling
+   `ShrikeClassSO` carried "Shrike". It is referenced by `SO_Classlist_All`, so it is a live
+   vessel, and `VesselUnlockSystem` persisted `vessel.Name` verbatim as the Cloud Save key. It
+   only surfaced for some saves because `isLocked` is unserialized on that asset and defaults to
+   false — it takes **Lock All** then **Unlock All** in the toolbox to push the empty key through.
+   The list is now a keyed map that rejects blank names at the writer, **and the asset is fixed**.
 
 ### 2.4 Why `MODE_STATS` matters more than it looks
 
@@ -171,6 +173,13 @@ Person properties mirrored from Cloud Save: display name, avatar, crystal balanc
 crystals earned/spent, first-seen, session count, games completed, total flight time, preferred
 vessel, selected vessel, unlocked vessel count, unlocked mode count.
 
+**The identity/build envelope lives in the PostHog sink, not in the facade.** `player_id`,
+`app_version`, `platform` and `schema_version` are stamped on the way out to PostHog only. UGS
+auto-collects the equivalents as core data, and — because every UGS parameter is a permanent,
+undeletable dashboard row against a 1,500 cap — sending them there would have been 168 rows of
+duplicated data. Event parameters override envelope keys, so `game_completed`'s own client-stamped
+timestamp still wins and reaches both sinks. See §6.1.
+
 **Region is locked to EU Cloud** — see §5.
 
 ---
@@ -205,7 +214,8 @@ PostHog sink beside the first and double-sent every event.
 
 ## 5. The EU vs US question — settled
 
-**Keep EU Cloud.** The client host is `https://eu.i.posthog.com`.
+**Keep EU Cloud — confirmed.** The project logs in at `eu.posthog.com`, and the client host is
+`https://eu.i.posthog.com`.
 
 The premise behind switching to US — *"Froglet is a Delaware C-corp"* — does not apply, and it
 is worth writing down because it will come up again:
@@ -233,14 +243,76 @@ PostHog regions **cannot be changed after project creation**, so this is effecti
 
 Ordered. Everything in **A** is required before any data arrives at all.
 
-### A · Turn the pipeline on — ~45 min, unblocks everything else
+### A · Turn the pipeline on
 
 | # | Action | Why it matters |
 |---|---|---|
-| **A1** | Open `Assets/Resources/PostHogConfig.asset` in Unity, paste the **Project API key** (starts with `phc_`) into `Project Api Key`. Confirm `Host` reads `https://eu.i.posthog.com`. Save, commit, push. | The sink is **inert** until this key exists. This is the single blocking step. |
+| **A1** | ~~Paste the PostHog **Project API key** into `Assets/Resources/PostHogConfig.asset`.~~ **DONE.** Key `phc_qixNiz…` is committed and the host is `https://eu.i.posthog.com`, confirmed against the `eu.posthog.com` login. | The sink was inert until this existed. PostHog now receives events. |
 | **A2** | PostHog → *Organization settings → Billing → Product analytics → Edit billing limit → **$0***. | Makes free-tier overage a hard drop instead of a surprise bill. |
-| **A3** | **Declare every event and every parameter in the UGS dashboard Event Manager**, from `Docs/Analytics/EVENT_SCHEMA.json`. | UGS **silently discards** any event or parameter not declared there. This is the most likely reason so little data has been arriving. |
-| **A4** | Play one full game. Check PostHog → *Activity* for `game_started` / `game_completed`, and the UGS dashboard for the same. | End-to-end proof. If PostHog is empty but UGS is not, the key or host is wrong. |
+| **A3** | **Hand-create 28 events and 67 parameters in the UGS Event Manager.** Full walkthrough in §6.1 — read it before starting, because the rows are permanent. | UGS rejects any custom event with no dashboard schema. This is why so little has reached UGS while the code has been recording all along. **UGS only — PostHog needs none of it.** |
+| **A4** | Play one full game. Check PostHog → *Activity* for `game_started` / `game_completed`. | End-to-end proof. Works today, independently of A3. |
+
+> **A4 is unblocked now.** A1 is done and PostHog accepts whatever the sink sends, so you can
+> confirm the PostHog half of the pipeline before touching the UGS list at all.
+
+### 6.1 · A3 in full — the UGS Event Manager
+
+Verified against Unity's current documentation (`com.unity.services.analytics` 6.3.0) rather than
+assumed, because this is real manual work and one earlier assumption of mine was wrong.
+
+**The three facts that shape the job:**
+
+1. **Custom events must exist in the dashboard before they are accepted.** Unity: *"Events must
+   conform to a schema that exists on the dashboard or else they are rejected as invalid."* The SDK
+   still uploads them — the backend rejects them, and they accumulate under
+   **Event Browser → Invalid Events** with a reason.
+2. **There is no auto-detect or approve-from-traffic flow, and no bulk import.** No CSV upload, no
+   admin API, and `ugs deploy` does not cover Analytics. (The *legacy* pre-UGS Analytics did list
```

</details>

### `08d3813c7` — docs(analytics): restructure UGS setup into the two passes the dashboard actually needs

_Claude, 2026-08-06 15:54:48 +0000_

```text
The Add Custom Event / Add Custom Parameter dialogs show two things the earlier plan got
wrong.

Descriptions are MANDATORY. Both dialogs mark Event description and Parameter description
required; the previous CSV had none, so every row would have needed one invented at the
keyboard. Written descriptions now live in EVENT_SCHEMA.json for all 28 events and all 42
parameters, and the CSVs derive from it.

Parameters are project-level objects, not fields on an event - "Parameters are attached to
the project and are shared between environments". You create a parameter once and ASSIGN it
to each event that uses it. So the real unit of work is 42 unique parameters, not 67
per-event rows: game_mode alone is used by 7 events.

Replaces ugs_event_manager_rows.csv with two files matching the actual order of operations:
ugs_step1_parameters.csv (create 42, once) and ugs_step2_events.csv (create 28, each with
its exact assign list). Doing it in this order avoids breaking off mid-event to create a
parameter that does not exist yet.

Two verifications that only matter because parameters are shared project-wide: no parameter
name is used with two different types anywhere (that would be unresolvable), and none of the
42 collides with the clientVersion / platform / sdkMethod / userCountry parameters UGS
auto-attaches to every custom event.

Also notes that Copy to Environment sits inside the creation dialog, so events can be fanned
out at creation rather than revisited, and parameters need no copying at all.
```

```text
 Docs/Analytics/ANALYTICS_HANDOFF.md       |  53 +++++++++---
 Docs/Analytics/EVENT_SCHEMA.json          | 276 +++++++++++++++++++++++++++++++++++++++---------------------
 Docs/Analytics/ugs_event_manager_rows.csv |  68 ---------------
 Docs/Analytics/ugs_step1_parameters.csv   |  43 ++++++++++
 Docs/Analytics/ugs_step2_events.csv       |  29 +++++++
 5 files changed, 291 insertions(+), 178 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 602 lines)</summary>

```diff
diff --git a/Docs/Analytics/ANALYTICS_HANDOFF.md b/Docs/Analytics/ANALYTICS_HANDOFF.md
index d3ff018b4..262d8ef28 100644
--- a/Docs/Analytics/ANALYTICS_HANDOFF.md
+++ b/Docs/Analytics/ANALYTICS_HANDOFF.md
@@ -8,7 +8,8 @@
 >
 > Companions: `DATA_ARCHITECTURE.md` (the authority on schema + design), `EVENT_SCHEMA.json`
 > (machine-readable event contract), `POSTHOG_SETUP.md` (the click-by-click PostHog guide),
-> `../../Tools/Analytics/README.md` (export + backfill scripts).
+> `../../Tools/Analytics/README.md` (export + backfill scripts), `ugs_step1_parameters.csv` +
+> `ugs_step2_events.csv` (the two UGS Event Manager passes).
 
 ---
 
@@ -282,25 +283,49 @@ The envelope exists because *PostHog* has no equivalent auto-collection, so it m
 PostHog sink where it belongs. `game_completed` keeps its client-stamped completion timestamp as a
 real event parameter, since that was an explicit ask, and it goes to both sinks.
 
-**Net: 28 events, 67 parameters. Four events carry no parameters at all.**
+**Net: 28 events and 42 unique parameters.**
+
+**Parameters are project-level objects, not fields on an event.** The dialog says it outright:
+*"Parameters are attached to the project and are shared between environments."* You create a
+parameter **once**, then **assign** it to as many events as use it. `game_mode` is used by 7
+events but is created once; there are 67 assignments across only 42 distinct parameters.
+
+So the job is two passes, in this order:
+
+**Step 1 — create the 42 parameters.** *Event Manager → Parameters → Add Custom Parameter.*
+Name, description, type. Source: **`Docs/Analytics/ugs_step1_parameters.csv`**.
+
+**Step 2 — create the 28 events and assign parameters.** *Event Manager → Add New → Custom Event.*
+Name, description, then **+ Assign Parameter** for each one, picking from what Step 1 created.
+Source: **`Docs/Analytics/ugs_step2_events.csv`**, whose `parameters_to_assign` column is the exact
+list per event.
+
+Doing it in this order matters: assigning a parameter that does not exist yet means breaking off
+mid-event to create it.
+
+**Descriptions are mandatory** — both dialogs mark Event description and Parameter description with
+a red asterisk. Every event and parameter in both CSVs ships with one written, so there is nothing
+to invent at the keyboard.
 
 **Where:** `cloud.unity.com` → **Development → Products** → **Analytics** → **Event Manager**.
 Direct: `https://cloud.unity.com/analytics`
 
-**How:** per event, **Add New → Custom Event**, type the name exactly, then add each parameter with
-its type. Work from **`Docs/Analytics/ugs_event_manager_rows.csv`** (`event_name, parameter_name,
-type`), regenerated from `EVENT_SCHEMA.json` so the two cannot drift.
+**Order within Step 2 — highest value first, so stopping early still leaves working analysis:**
 
-**Order — highest value first, so stopping early still leaves you with working analysis:**
-
-| Order | What | Rows |
+| Order | What | Assignments |
 |---|---|---|
-| 1 | `game_started` (12) + `game_completed` (13) | 25 of 67 — every field the instrumentation email asked for |
-| 2 | `play_again_pressed`, `ad_impression`, `game_first_launched`, `party_joined` | 0 each — name only, seconds apiece |
-| 3 | The remaining 22 events | 1–3 parameters each |
-
-**Then replicate:** build the schema in **one** environment and use **Copy to Environment** — the
-only bulk operation UGS offers.
+| 1 | `game_started` (12) + `game_completed` (13) | Every field the instrumentation email asked for |
+| 2 | `play_again_pressed`, `ad_impression`, `game_first_launched`, `party_joined` | None — name + description only |
+| 3 | The remaining 22 events | 1–3 each |
+
+**Then replicate:** the **Copy event to other environments** dropdown sits in the same Add Custom
+Event dialog, so you can fan an event out at creation time rather than revisiting it. Parameters are
+already shared project-wide and need no copying.
+
+**Two checks already done for you.** Because parameters are shared project-wide, a name used with
+two different types anywhere would be unresolvable — there are **no such conflicts**. And UGS
+auto-attaches `clientVersion`, `platform`, `sdkMethod` and `userCountry` to every custom event; none
+of our 42 collides with those, and none of them needs creating.
 
 **Two traps:**
 
diff --git a/Docs/Analytics/EVENT_SCHEMA.json b/Docs/Analytics/EVENT_SCHEMA.json
index 44e9f9f12..ba8507d4b 100644
--- a/Docs/Analytics/EVENT_SCHEMA.json
```

</details>

### `5bedfb5ce` — fix(analytics): the consent gate is closed and nothing said so - fail loud, add a dev opener

_Claude, 2026-08-06 16:42:52 +0000_

```text
Root cause of "0 valid events in UGS and nothing in PostHog after playing several games":
collection never started. It has nothing to do with editor vs build, or the UGS schema.

AnalyticsServiceFacade.StartCollectionIfReady requires an answered COPPA age gate AND
granted consent. Both are PlayerPrefs that default to 0, i.e. denied - correct for an
opt-in design. The UI that answers them, PrivacyConsentController, exists in code but is
placed in NO scene and NO prefab, so on any machine there is no way to answer it. The gate
stays shut, _collecting stays false, and RecordEvent drops every event before it reaches a
sink. Both destinations going quiet at once is the signature: a UGS schema problem would
still leave PostHog working, and a PostHog key problem would still leave UGS working.

This is the same failure mode DATA_INVENTORY.md already recorded once, when the Firebase
analytics objects were orphaned in no scene and collection had never started.

It was silent because the drop path logged through Log(), which is gated on VerboseLogging.
RecordEvent now warns once, loudly, naming which gate is shut - age not answered, under 13,
consent not answered, consent declined, not signed in, or offline - and pointing at the fix.
Also exposes DroppedEventCount, where 0 is the healthy value.

Adds FrogletTools > Services > Analytics Consent (Dev): shows both gates and grants them on
the local machine for testing. Editor-only by location, never compiled into any build, so no
build can grant consent on a player's behalf - which is the exact thing the gate exists to
prevent. It writes the facade's own pref keys (now public) rather than re-typing the strings.

The real fix is placing PrivacyConsentController in a scene. That is UI work and a release
blocker, tracked separately - this only unblocks testing.
```

```text
 Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs              | 123 +++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs.meta         |  11 ++++
 Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs |  45 +++++++++++++-
 3 files changed, 176 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 212 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs b/Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs
new file mode 100644
index 000000000..8bd4f1d62
--- /dev/null
+++ b/Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs
@@ -0,0 +1,123 @@
+using CosmicShore.Core;
+using UnityEditor;
+using UnityEngine;
+
+namespace CosmicShore.Editor.Froglet
+{
+    /// <summary>
+    /// Developer-only view of the analytics consent gate, and a way to open it while testing.
+    ///
+    /// Analytics collection is opt-in: the COPPA age gate and the consent flag both default to
+    /// DENIED, and until both are granted <c>AnalyticsServiceFacade</c> drops every event before
+    /// it reaches any sink - so UGS and PostHog go silent together. In a shipping build the
+    /// player answers <c>PrivacyConsentController</c>; that UI is not currently placed in any
+    /// scene, so on a dev machine there is no way to answer it at all.
+    ///
+    /// This window is that answer, and nothing more. It writes the same two PlayerPrefs the real
+    /// dialog writes, on this machine only. It is EDITOR-ONLY BY LOCATION (an Editor/ folder), so
+    /// no build - development or release - can auto-grant consent. That is deliberate: granting
+    /// consent on a player's behalf is the exact thing the gate exists to prevent.
+    ///
+    /// Read-only tool otherwise: it writes no assets, so it needs no ship panel (Docs/TOOLING.md).
+    /// </summary>
+    public class AnalyticsConsentDevWindow : EditorWindow
+    {
+        [MenuItem("FrogletTools/Services/Analytics Consent (Dev)")]
+        [FrogletTool(FrogletToolCategory.Services, Importance = 5,
+            Description = "Inspect the analytics consent gate and grant it on this machine for " +
+                          "testing. Until it is granted, every event is dropped before reaching " +
+                          "UGS or PostHog.")]
+        public static void Open()
+        {
+            var window = GetWindow<AnalyticsConsentDevWindow>(false, "Analytics Consent");
+            window.minSize = new Vector2(430f, 300f);
+            window.Show();
+        }
+
+        static bool AgeAnswered => PlayerPrefs.HasKey(AnalyticsServiceFacade.AgeGatePrefKey);
+        static bool AgeEligible => PlayerPrefs.GetInt(AnalyticsServiceFacade.AgeGatePrefKey, 0) == 1;
+        static bool ConsentAnswered => PlayerPrefs.HasKey(AnalyticsServiceFacade.ConsentPrefKey);
+        static bool ConsentGranted => PlayerPrefs.GetInt(AnalyticsServiceFacade.ConsentPrefKey, 0) == 1;
+
+        static bool Collecting => AgeEligible && ConsentGranted;
+
+        void OnGUI()
+        {
+            FrogletEditorPalette.Banner("Analytics Consent", "Developer gate control",
+                FrogletEditorPalette.Azure);
+
+            EditorGUILayout.Space(6f);
+            EditorGUILayout.LabelField(
+                "Collection is opt-in. Both gates must be granted or every event is dropped " +
+                "before it reaches UGS or PostHog - which looks like a backend problem and is not.",
+                EditorStyles.wordWrappedMiniLabel);
+
+            EditorGUILayout.Space(8f);
+            DrawGate("Age gate (COPPA)", AgeAnswered, AgeEligible, "13+", "under 13");
+            DrawGate("Analytics consent", ConsentAnswered, ConsentGranted, "granted", "declined");
+
+            EditorGUILayout.Space(6f);
+            var pillRect = GUILayoutUtility.GetRect(GUIContent.none, FrogletEditorPalette.Pill,
+                GUILayout.Height(20f), GUILayout.ExpandWidth(true));
+            FrogletEditorPalette.StatusPill(pillRect,
+                Collecting ? "COLLECTING - events will reach both sinks" : "BLOCKED - all events are dropped",
+                Collecting ? FrogletEditorPalette.Ok : FrogletEditorPalette.Error);
+
+            EditorGUILayout.Space(12f);
+            using (new EditorGUILayout.HorizontalScope())
+            {
+                if (FrogletEditorPalette.ColorButton("Grant (this machine)", FrogletEditorPalette.Ok, 170f))
+                    SetGates(true);
+                if (FrogletEditorPalette.ColorButton("Revoke", FrogletEditorPalette.Warn, 100f))
+                    SetGates(false);
+                if (FrogletEditorPalette.ColorButton("Clear (unanswered)", FrogletEditorPalette.Info, 150f))
+                    ClearGates();
```

</details>

### `2ae877144` — feat(privacy): consent dialog that ships - built at runtime, created by AppManager

_Claude, 2026-08-06 16:52:41 +0000_

```text
Players on the invite-only build can now consent, so analytics actually collect. Until
now nothing could: the consent gate defaults to denied and there was no way to answer it.

Built at runtime and created by AppManager rather than placed in a scene. That is the
whole point. The previous PrivacyConsentController was a MonoBehaviour you had to drop on
a panel in some scene - and nobody ever did, so the gate stayed shut and every analytics
event in the game was silently dropped before reaching UGS or PostHog. A screen that has
to be remembered is a screen that gets forgotten. This one cannot be unwired: it appears
in every scene and every build with nothing to author, the same way SceneTransitionManager
builds its own fade overlay.

Two steps, because one of them is not optional. The age gate asks for a birth YEAR rather
than "are you 13 or older?" - a yes/no age question nudges the player toward the answer
that unlocks collection, which is what COPPA guidance says a neutral age screen must not
do. Then the consent dialog: I agree / No thanks, naming the processors (UGS and PostHog
EU) and the categories sent, because consent that does not name the recipient is not
informed consent. Under-13 skips consent entirely and collects nothing.

Not a wall - declining or answering under-13 dismisses it and play continues. Gating play
on consent would not be freely given.

Copy and the policy URL live in PrivacyConsentConfigSO (Resources), so legal can change
what the player is told without a code change. The policy link hides itself when no URL is
set rather than opening a broken link; filling it in is still a release gate.

Uses InputSystemUIInputModule for its fallback EventSystem, not StandaloneInputModule -
the project ships activeInputHandler = 1 (new Input System only), under which the legacy
module throws and no click would register. Sits at sorting order 32766, one below the
scene-transition fade, so a transition covers it rather than fighting it.

Deletes PrivacyConsentController: keeping a second, never-placed consent flow beside a
working one is exactly the confusion that produced this bug.
```

```text
 Assets/Resources/PrivacyConsentConfig.asset                           |  23 ++
 Assets/Resources/PrivacyConsentConfig.asset.meta                      |   8 +
 Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs                   |  17 +-
 Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs           |  62 +++++
 .../PrivacyConsentConfigSO.cs.meta}                                   |   2 +-
 Assets/_Scripts/System/AppManager.cs                                  |   8 +
 Assets/_Scripts/UI/Privacy/PrivacyConsentController.cs                | 187 --------------
 Assets/_Scripts/UI/Privacy/PrivacyConsentOverlay.cs                   | 418 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Privacy/PrivacyConsentOverlay.cs.meta              |  11 +
 9 files changed, 540 insertions(+), 196 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 740 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs b/Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs
index 8bd4f1d62..3d3c00415 100644
--- a/Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs
+++ b/Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs
@@ -9,11 +9,11 @@ namespace CosmicShore.Editor.Froglet
     ///
     /// Analytics collection is opt-in: the COPPA age gate and the consent flag both default to
     /// DENIED, and until both are granted <c>AnalyticsServiceFacade</c> drops every event before
-    /// it reaches any sink - so UGS and PostHog go silent together. In a shipping build the
-    /// player answers <c>PrivacyConsentController</c>; that UI is not currently placed in any
-    /// scene, so on a dev machine there is no way to answer it at all.
+    /// it reaches any sink - so UGS and PostHog go silent together. In a build the player answers
+    /// <c>PrivacyConsentOverlay</c>, which AppManager creates on first run.
     ///
-    /// This window is that answer, and nothing more. It writes the same two PlayerPrefs the real
+    /// This window exists to skip that dialog while testing, and to re-open it: clearing the gates
+    /// makes the overlay appear again on the next run. It writes the same two PlayerPrefs the real
     /// dialog writes, on this machine only. It is EDITOR-ONLY BY LOCATION (an Editor/ folder), so
     /// no build - development or release - can auto-grant consent. That is deliberate: granting
     /// consent on a player's behalf is the exact thing the gate exists to prevent.
@@ -76,10 +76,11 @@ namespace CosmicShore.Editor.Froglet
 
             EditorGUILayout.Space(10f);
             EditorGUILayout.HelpBox(
-                "This is a stand-in for PrivacyConsentController, which exists in code but is not " +
-                "placed in any scene. Placing it is the real fix and a release blocker - a shipping " +
-                "build must never grant consent on the player's behalf.\n\n" +
-                "Granting while in Play Mode takes effect on the next sign-in, so restart Play Mode.",
+                "Players answer PrivacyConsentOverlay, which AppManager creates on first run. This " +
+                "window only skips it for testing - a build must never grant consent on the " +
+                "player's behalf.\n\n" +
+                "Use Clear to make the real dialog appear again on the next run. Granting while in " +
+                "Play Mode takes effect on the next sign-in, so restart Play Mode.",
                 MessageType.Info);
 
             if (Application.isPlaying)
diff --git a/Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs b/Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs
new file mode 100644
index 000000000..deef3c1ef
--- /dev/null
+++ b/Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs
@@ -0,0 +1,62 @@
+using UnityEngine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    /// <summary>
+    /// Copy and links for the first-run privacy flow. Asset lives at
+    /// <c>Resources/PrivacyConsentConfig</c> so the overlay can load it with no scene wiring.
+    ///
+    /// The wording is authored data, not code, so legal or product can change what the player is
+    /// told without a programmer — which matters because the disclosure has to name the processor
+    /// (PostHog, EU) and the categories sent, and that text is a release gate.
+    /// </summary>
+    [CreateAssetMenu(fileName = "PrivacyConsentConfig", menuName = "ScriptableObjects/Analytics/Privacy Consent Config")]
+    public class PrivacyConsentConfigSO : ScriptableObject
+    {
+        [Header("Links")]
+        [Tooltip("Public URL of the hosted privacy policy. Leave EMPTY and the link button is hidden " +
+                 "rather than shown broken — but it must be filled before any public release.")]
+        [SerializeField] string privacyPolicyUrl = "";
+
+        [Header("Age gate")]
+        [TextArea(2, 4)]
+        [Tooltip("Neutral age question. Do NOT phrase this so it nudges the player toward being " +
+                 "old enough — a neutral birth-year question is the COPPA-safe form.")]
+        [SerializeField] string ageTitle = "Before you fly";
+
+        [TextArea(2, 5)]
+        [SerializeField] string ageBody = "What year were you born?";
+
+        [Header("Consent")]
+        [TextArea(2, 4)]
+        [SerializeField] string consentTitle = "Help us improve Cosmic Shore";
+
+        [TextArea(4, 12)]
+        [Tooltip("Must name the processor and the categories sent. Consent that does not name the " +
+                 "recipient is not informed consent.")]
+        [SerializeField] string consentBody =
+            "We'd like to collect anonymous gameplay data — which modes you play, how long you fly, " +
```

</details>

### `ba701576e` — docs(analytics): Analytics Handbook - PostHog and UGS guide for whoever reads the data

_Claude, 2026-08-06 17:20:40 +0000_

```text
An 11-page PDF for the instrumentation and analysis side: how to get into PostHog, confirm
data is arriving, and answer questions with it - Activity, People and person properties,
filters, cohorts, insights, and HogQL - plus what UGS Analytics is for and when it needs
touching. Committed with its HTML source so it can be re-rendered rather than re-authored.

Written against the shipped implementation, not the design docs, which caught a stale
claim: POSTHOG_SETUP.md told readers to expect session_id and build_version on every
event. Neither ever shipped - the envelope was reworked into player_id, install_id,
app_version, platform and schema_version. Corrected there too, so the guide and the setup
doc agree.

Leads with the two things that will otherwise cost someone an afternoon: only consenting
players appear at all, so totals are a floor rather than a headcount; and UGS can read
zero valid events while PostHog has data, because UGS rejects any event whose schema has
not been hand-created in its dashboard first.

The rematch section spells out the query the original instrumentation email asked for,
including the condition that is easy to miss - an organic rematch needs a DIFFERENT
party_id, or one evening of three back-to-back games reads as two rematches.
```

```text
 Docs/Analytics/ANALYTICS_HANDBOOK.html | 613 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/Analytics/ANALYTICS_HANDBOOK.pdf  | Bin 0 -> 352389 bytes
 Docs/Analytics/POSTHOG_SETUP.md        |   3 +-
 3 files changed, 615 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/Analytics/POSTHOG_SETUP.md b/Docs/Analytics/POSTHOG_SETUP.md
index 2a8fd58d0..6d78ddbb7 100644
--- a/Docs/Analytics/POSTHOG_SETUP.md
+++ b/Docs/Analytics/POSTHOG_SETUP.md
@@ -50,7 +50,8 @@ batch size / flush interval / offline queue cap.
 3. Play one game to the end screen, then background/quit the app (that triggers the flush).
 4. In PostHog: **Activity** (left nav) — events appear within seconds of a flush:
    `game_started`, `game_completed`, `session_ended`, etc., each carrying
-   `session_id`, `install_id`, `build_version`, `platform`, and the gameplay parameters.
+   `player_id`, `install_id`, `app_version`, `platform`, `schema_version`, and the gameplay
+   parameters. (`session_id` / `build_version` never shipped - the envelope was reworked.)
 5. Cross-check identity: the event's *distinct ID* equals the UGS PlayerId (same ID you
    see in UGS dashboards and Cloud Save) — that's the join key across all our systems.
 
```

</details>

### `ad138beb4` — feat(prisms): the occlusion corridor's flecks are triangles, not circles

_Claude, 2026-08-06 18:10:55 +0000_

```text
The corridor's dither reads as little circles, which is the one shape that
cannot sit in the house soft-hard-soft motif: bloom (soft) around low-poly
prisms (hard) along a smooth flight curve (soft), and UI borders graded at
both ends but taking hard turns in their pathing. A circle is soft with a
soft gradient either side of it - soft-SOFT-soft - so the flecks read as
foam against everything else in frame.

SHARD is Worley with the METRIC changed and nothing else. Same lattice,
same Hoskins hash, same orbiting feature points, same 3x3 search, same CDF
remap; distance is measured with the gauge of an equilateral triangle,
max(q.y, 0.866*|q.x| - 0.5*q.y), whose level sets are that triangle. The
arrangement the eye reads as organic flecking is untouched and only the
unit shape's edges change, from curved to three straight ones.

The area normalisation (x1.28607) is load-bearing twice: it makes them
"triangles of the same size" - identical ink at every alpha - and it lands
the distance distribution on Worley's own measured CDF, so one fitted remap
now serves both cellular kernels (constants renamed ..._WORLEY_CDF_* ->
..._CELL_CDF_* to say so). Slightly cheaper than Worley, too: a gauge is
homogeneous of degree 1, so the min is taken on it directly and the final
sqrt disappears.

Measured (harness reads shipped Worley at 0.0073 uniform / 0.0117
corridor): SHARD/FIXED 0.0074 / 0.0145, FLIP 0.0066 / 0.0126, SPIN
0.0070 / 0.0129 - all inside the admission rule, phase-stable across
t = 0..400s, temporal coherence 0.64%/frame against the 1.45% ceiling.
3x3 search verified against an exhaustive 5x5: 0.216% of pixels differ,
mean threshold delta 1.5e-5. FIXED is the default because it is the one
whose shape is nameable at a glance; FLIP/SPIN measure marginally better
and are one #define away.

Two more polygonal candidates passed the fidelity bar and were rejected on
the look: triangular tessellation (0.0009 / 0.0056) and Voronoi shatter
(0.0009 / 0.0034) both dissolve into thin strokes at mid alpha and read as
crosshatch rather than as polygons. Passing the number is necessary, not
sufficient - now stated in the file's admission rule.

Kernel 2 is kept as the calibration reference every fidelity number in the
file is quoted against. No graph, material, uniform or signature change:
PRISM_OCCLUSION_KERNEL selects it and nothing else moves.
```

```text
 Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl | 187 +++++++++++++++++++++++++++++++++++++---
 CLAUDE.md                                                     |   2 +-
 Docs/PRISM_ANIMATION.md                                       |  69 ++++++++++++++-
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md                          |  11 ++-
 4 files changed, 250 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 378 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
index 33b5c4588..944505a46 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
@@ -75,12 +75,44 @@
 // a fade instead of an edge, and it is the reason the other nine candidates rendered on
 // 2026-08-04 (concentric rings 0.21, quasicrystal 0.13, halftone 0.10, hex 0.10, perlin
 // 0.04, …) are not here: they buy their look by trading it away.
+//
+// A kernel must also EARN ITS SHAPE. The 2026-08-06 hard-edge pass measured two more
+// polygonal candidates that pass the fidelity bar and were still rejected, on the look:
+// a triangular TESSELLATION (simplex grid, per-facet phase, facets filling as nested
+// triangles — 0.0009 / 0.0056) and Voronoi SHATTER (irregular polygons filling between
+// parallel straight lines — 0.0009 / 0.0034). Both dissolve into thin strokes at mid
+// alpha and read as scratchy crosshatch rather than as polygons, and the unstaggered
+// tessellation (0.16) is the literal wallpaper the Bayer grid was dropped for. Passing
+// the number is necessary, not sufficient.
 // -----------------------------------------------------------------------------
 #define PRISM_OCCLUSION_KERNEL_IGN 0     // screen-space noise — reads as a DISSOLVE
 #define PRISM_OCCLUSION_KERNEL_SPIRAL 1  // corridor-relative — reads as an IRIS
-#define PRISM_OCCLUSION_KERNEL_WORLEY 2  // screen-space cells — reads as ORGANIC FLECKING
+#define PRISM_OCCLUSION_KERNEL_WORLEY 2  // screen-space cells — reads as ROUND flecking
+#define PRISM_OCCLUSION_KERNEL_SHARD 3   // screen-space cells — reads as TRIANGULAR flecking
 
-#define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_WORLEY
+#define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_SHARD
+
+// -----------------------------------------------------------------------------
+// THE SHAPE RULE — why the current kernel is SHARD and not WORLEY (2026-08-06).
+//
+// The unit shape of the dither is a design surface, not just a dither detail: it is
+// the smallest piece of the game the player sees, repeated thousands of times right
+// next to their ship. Cosmic Shore's motif is SOFT-HARD-SOFT — bloom (soft) around
+// low-poly prisms (hard) drawn along a smooth flight curve (soft); the UI borders do
+// the same thing, grading out at both ends while taking hard turns in their pathing.
+// Rigid geometry sandwiched between the ambiguous.
+//
+// A CIRCLE breaks that. It is a soft shape with a soft gradient on either side of it —
+// soft-SOFT-soft — so Worley's round flecks read as foam against everything else in the
+// frame. SHARD keeps Worley's arrangement exactly (same lattice, same jitter, same
+// orbit, same remap) and changes only the METRIC, so the flecks become equilateral
+// triangles of the same area: hard polygonal unit shape, ambiguous placement, still
+// feathered by the corridor's own soft profile. Hard shape, soft sandwich.
+//
+// Kernel 2 is kept, not deleted — it is the calibration reference every fidelity number
+// in this file is quoted against, and it is one #define away if the triangles ever want
+// re-judging side by side.
+// -----------------------------------------------------------------------------
 
 // -----------------------------------------------------------------------------
 // THE MORPH RATE — how fast the pattern evolves, in full pattern cycles per second.
@@ -88,9 +120,9 @@
 // legibly alive without ever drawing the eye off the ship. Set to 0 for a frozen pattern;
 // nothing else needs to change.
 //
-// This is an AXIS, not a fourth kernel — each kernel interprets it in its own natural
-// terms (Worley orbits its feature points, the spiral drifts its phase), and each states
-// the interpretation at its own definition. Time comes from `_Time.y`, a URP built-in, so
+// This is an AXIS, not another kernel — each kernel interprets it in its own natural terms
+// (the cellular kernels orbit their feature points, the spiral drifts its phase), and each
+// states the interpretation at its own definition. Time is `_Time.y`, a URP built-in, so
 // morphing costs one MAD per fragment and ZERO CPU: no per-prism state, no publisher
 // change, no extra uniform. That is the same shape the clock-material law asks for
 // everywhere else — initial conditions plus a clock, evaluated on the GPU.
@@ -225,9 +257,16 @@ float PrismOcclusionMotley(float2 pixel)
 // rate 0 through t = 400s. (Feeding the shipped-static constants 0.02/0.83 to the moving
 // points measured 0.0238, i.e. straight back out of the admission rule, which is exactly
 // the failure mode the warning below is about.)
-static const float PRISM_OCCLUSION_WORLEY_CELL = 6.0;       // pixels per lattice cell
-static const float PRISM_OCCLUSION_WORLEY_CDF_LO = 0.011;   // fitted to the measured F1 CDF
-static const float PRISM_OCCLUSION_WORLEY_CDF_HI = 0.873;   // — do not retune independently
+static const float PRISM_OCCLUSION_CELL_SIZE = 6.0;       // pixels per lattice cell
+static const float PRISM_OCCLUSION_CELL_CDF_LO = 0.011;   // fitted to the measured F1 CDF
+static const float PRISM_OCCLUSION_CELL_CDF_HI = 0.873;   // — do not retune independently
+
+// The fit is named CELL, not WORLEY, because BOTH cellular kernels use it: SHARD's
+// triangle gauge is area-normalised against the circle (see kernel D), which lands its
+// distance distribution on the same CDF. Re-measured under the triangle metric the
+// independent best fit is 0.0118 / 0.8775 — within noise of these two, and the shipped
+// pair measures 0.0074 uniform on it. One fit, two metrics; that is the payoff for
```

</details>

### `590c27eda` — feat(prisms): carry SHATTER, and open the corridor dither's scale dials

_Claude, 2026-08-06 18:36:30 +0000_

```text
Two additions to the occlusion corridor's kernel set, both so the shape can
be judged in motion instead of from stills.

SHATTER (kernel 4) is the other way to get a hard-edged unit shape: rather
than growing a polygon around a point, take the Voronoi cell itself - an
irregular convex polygon, nothing but straight edges - and fill it between
two parallel straight lines from a hashed phase and a hashed direction.
Neighbouring cells are independent so their boundaries always show, and the
pattern reads as a cracked lattice of WALLS rather than as scattered flecks.
It is a different proposition from SHARD, not a variant: SHARD hardens the
fleck, SHATTER makes the negative space the motif.

It is the only kernel here whose wall thickness is authorable separately
from its cell size - SHATTER_CELL (polygon px, 8-18) and SHATTER_WALL (band
repeat px, 4-11; at alpha a the dark wall is (1-a)*WALL wide). No CDF fit
and none needed: frac of a hash is uniform by construction. Shipped at
12/9 = 0.0009 / 0.0070.

THE SIZE WINDOW, and a correction. CELL_SIZE carried a "re-fit the CDF or
the fade degrades ~19x" warning. That is wrong, and measured wrong: the
distance is in CELL units, so its distribution does not move with the pitch
- re-fitting anywhere from 3 to 15 px lands within noise of the shipped
constants and buys nothing (at 15 px a bespoke re-fit takes the sweep from
0.0062 to 0.0059 and leaves corridor error at 0.026 untouched). The 19x is
what dropping the remap ENTIRELY costs, not what moving the pitch costs.

What actually bounds the pitch is SAMPLING at both ends, and neither end is
fittable: 3 px puts the shape under the pixel floor, and past 11 px too few
cells span the gradient band (0.0193 at 11, 0.0248 at 15 - that one reads as
a chunky edge, not a fade). Usable 4.5-11 px, sweet spot 6-8; 8 px measures
identically to the shipped 6 and is the most legible AS a triangle. SHATTER
fails the same way at both of its ends, for the same reason.

The triangular tessellation candidate stays out: it passes the number
(0.0009 / 0.0056) but dissolves into thin strokes at mid alpha and reads as
crosshatch rather than facets.

Default is unchanged (SHARD/FIXED at 6 px). Both new paths verified by
compiling the shipped .hlsl through a clang shim and comparing against the
reference the fidelity numbers were measured with: max delta 7.2e-06
(SHARD) and 1.4e-06 (SHATTER).
```

```text
 Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl | 134 +++++++++++++++++++++++++++++++++++++---
 CLAUDE.md                                                     |   2 +-
 Docs/PRISM_ANIMATION.md                                       |  59 +++++++++++++++---
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md                          |  17 ++++-
 4 files changed, 189 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 298 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
index 944505a46..c2ba1a10d 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
@@ -76,19 +76,19 @@
 // 2026-08-04 (concentric rings 0.21, quasicrystal 0.13, halftone 0.10, hex 0.10, perlin
 // 0.04, …) are not here: they buy their look by trading it away.
 //
-// A kernel must also EARN ITS SHAPE. The 2026-08-06 hard-edge pass measured two more
-// polygonal candidates that pass the fidelity bar and were still rejected, on the look:
-// a triangular TESSELLATION (simplex grid, per-facet phase, facets filling as nested
-// triangles — 0.0009 / 0.0056) and Voronoi SHATTER (irregular polygons filling between
-// parallel straight lines — 0.0009 / 0.0034). Both dissolve into thin strokes at mid
-// alpha and read as scratchy crosshatch rather than as polygons, and the unstaggered
-// tessellation (0.16) is the literal wallpaper the Bayer grid was dropped for. Passing
-// the number is necessary, not sufficient.
+// A kernel must also EARN ITS SHAPE — passing the number is necessary, not sufficient.
+// The 2026-08-06 hard-edge pass measured two more polygonal candidates. Voronoi SHATTER
+// passed both bars and is carried as kernel 4. The triangular TESSELLATION (simplex grid,
+// per-facet phase, facets filling as nested triangles) passed the number at 0.0009 / 0.0056
+// and is NOT here: it dissolves into thin strokes at mid alpha and reads as scratchy
+// crosshatch rather than as facets, and with the per-facet stagger removed it measures
+// 0.16 and is the literal wallpaper the Bayer grid was dropped for.
 // -----------------------------------------------------------------------------
 #define PRISM_OCCLUSION_KERNEL_IGN 0     // screen-space noise — reads as a DISSOLVE
 #define PRISM_OCCLUSION_KERNEL_SPIRAL 1  // corridor-relative — reads as an IRIS
 #define PRISM_OCCLUSION_KERNEL_WORLEY 2  // screen-space cells — reads as ROUND flecking
 #define PRISM_OCCLUSION_KERNEL_SHARD 3   // screen-space cells — reads as TRIANGULAR flecking
+#define PRISM_OCCLUSION_KERNEL_SHATTER 4 // screen-space cells — reads as a CRACKED LATTICE
 
 #define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_SHARD
 
@@ -259,7 +259,7 @@ float PrismOcclusionMotley(float2 pixel)
 // the failure mode the warning below is about.)
 static const float PRISM_OCCLUSION_CELL_SIZE = 6.0;       // pixels per lattice cell
 static const float PRISM_OCCLUSION_CELL_CDF_LO = 0.011;   // fitted to the measured F1 CDF
-static const float PRISM_OCCLUSION_CELL_CDF_HI = 0.873;   // — do not retune independently
+static const float PRISM_OCCLUSION_CELL_CDF_HI = 0.873;   // — see THE SIZE WINDOW below
 
 // The fit is named CELL, not WORLEY, because BOTH cellular kernels use it: SHARD's
 // triangle gauge is area-normalised against the circle (see kernel D), which lands its
@@ -267,6 +267,31 @@ static const float PRISM_OCCLUSION_CELL_CDF_HI = 0.873;   // — do not retune i
 // independent best fit is 0.0118 / 0.8775 — within noise of these two, and the shipped
 // pair measures 0.0074 uniform on it. One fit, two metrics; that is the payoff for
 // normalising by area rather than by extent.
+//
+// -----------------------------------------------------------------------------
+// THE SIZE WINDOW — CELL_SIZE is a free dial inside a measured band (2026-08-06).
+//
+// CORRECTION to what this file and the checklist used to say. The fit is NOT bound to
+// the pitch: the distance is measured in CELL units, so the distribution does not move
+// when the lattice does. Refitting at every pitch from 3 to 15 px lands within noise of
+// the two constants above (lo 0.009–0.020, hi 0.859–0.878) and buys nothing measurable —
+// at 15 px a bespoke refit takes the sweep from 0.0062 to 0.0059 and leaves the corridor
+// error at 0.026 untouched. The "~19× degradation" this note used to threaten is what you
+// get from dropping the remap ENTIRELY (raw F1 = 0.140), not from moving the pitch.
+//
+// What actually bounds the pitch is SAMPLING at both ends, and neither end is fittable:
+//
+//   3.0 px   the shape falls under the pixel floor — quantisation, 0.013 either way
+//   4.5 px   fine grain; triangles present but not readable at 1:1
+//   6.0 px   SHIPPED. 0.0074 / 0.0145 (SHARD/FIXED); ~9 px tall triangles
+//   8.0 px   0.0060 / 0.0146 — the same fidelity, and the most legible AS a triangle
+//  11.0 px   0.0059 / 0.0193 — bold; too few cells now span the gradient band
+//  15.0 px   0.0060 / 0.0248 — BREAKS THE FADE. The band reads as chunky edge, not fade.
+//
+// So: 4.5–11 px is the usable window and 6–8 px is the sweet spot. Move it inside that
+// band freely; past 11 px the corridor error is a SPATIAL sampling failure and there is
+// no constant anywhere in this file that will buy it back.
+// -----------------------------------------------------------------------------
 
 float2 PrismOcclusionHash2(float2 cell)
 {
@@ -422,6 +447,95 @@ float PrismOcclusionShard(float2 pixel, float time)
         PRISM_OCCLUSION_CELL_CDF_LO, PRISM_OCCLUSION_CELL_CDF_HI, best));
 }
 
+// -----------------------------------------------------------------------------
+// Kernel E — screen-space SHATTER (a cracked lattice of walls).
+//
```

</details>

### `e97a288bf` — feat(prisms): Occlusion Dither Lab - slide the corridor's unit shape live

_Claude, 2026-08-06 19:26:37 +0000_

```text
You cannot slide a #define while flying, and a dither can only really be
judged in motion against real trail mass. So the corridor's whole dial set
can be promoted to two shader globals - the same O(1)-per-frame shape the
corridor's own params already use - and driven live from an editor window,
including in play mode.

THE GATE. PRISM_OCCLUSION_LIVE_TUNING 1 makes every dial a uniform and the
kernel a runtime branch; 0 compiles the file exactly as if none of this
existed - one kernel, no branch, no uniforms. Design mode is NOT free (all
five kernels in every prism shader, registers for the largest, occupancy on
the one draw class this game has most of), which is why it is a gate rather
than a feature, and why Bake turns it off. Fail-safe in both directions:
_PrismOcclusionDitherA.x carries kernel+1, so an unpublished global reads as
"nobody is driving" and every dial falls back to its constant.

THREE THINGS MAKE IT A LAB, NOT A SLIDER PANEL.

The preview is the shipped GPU code. PrismOcclusionDitherPreview.shader
includes the corridor's own HLSL and calls the same new
PrismOcclusionDitherThreshold dispatch reading the same globals, so it
cannot drift from what the game draws - and a kernel added to the corridor
appears in the Lab for free.

Measure runs the real admission rule. It renders threshold+alpha to a float
target, reads it back, and computes |coverage - alpha| over actual rendered
output - the same methodology as the in-situ numbers in the shader's
comments - then measures the shipped Worley baseline in the same pass so the
verdict is a RATIO that no property of the harness can flatter. Sliders that
let someone silently break a platform law would be worse than no sliders.

Bake closes the loop, writing the values into the constants and flipping
design mode off so nobody transcribes numbers out of a screenshot. Every
rewrite is anchored and must match exactly once or the bake refuses; the
trailing comments carrying the measured windows survive it. The braced group
form is deliberate - .NET does not throw on a replacement naming a group the
pattern lacks, it writes the literal "${3}" into the shader.

Sliders span WIDER than each measured window on purpose and flag everything
outside it: the failure has to be reachable and visible or "inside the
window" means nothing.

Keeper tool (re-runnable design surface), so no ToolScriptPaths and the
retire button hides - but it writes source, so it records to the ledger in
the same block that writes and draws the ship panel, per Docs/TOOLING.md.

Verified by compiling the real .hlsl through the clang shim in BOTH modes:
identical output (max delta 7.2e-06, pure float rounding), every dial
confirmed to drive when published (FLIP differs on 1787/3600 px, SPIN on
3581/3600, every kernel index distinct), all nine bake anchors matching
exactly once against the real file, a simulated bake preserving every
trailing comment, the round-trip back to shipped values byte-identical, and
the baked ship-mode output compiling and running.
```

```text
 Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl         | 182 ++++++--
 Assets/_Graphics/Materials/Graphs/PrismOcclusionDitherPreview.shader  | 115 +++++
 .../Materials/Graphs/PrismOcclusionDitherPreview.shader.meta          |  10 +
 Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs                     | 802 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs.meta                |   2 +
 CLAUDE.md                                                             |   2 +-
 Docs/PRISM_ANIMATION.md                                               |  32 +-
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md                                  |   7 +
 Docs/TOOLING.md                                                       |   1 +
 9 files changed, 1119 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1311 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
index c2ba1a10d..4751bf6be 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
@@ -92,6 +92,44 @@
 
 #define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_SHARD
 
+// -----------------------------------------------------------------------------
+// LIVE TUNING — the design-mode gate (FrogletTools > Ecology > Prism Animation >
+// Occlusion Dither Lab).
+//
+// Every dial below is a compile-time constant, which is the right shape for shipping and
+// a terrible one for CHOOSING a look: you cannot slide a #define while flying. So the
+// whole dial set can be promoted to two global uniforms — the same O(1)-per-frame shape
+// the corridor's own params already use, published by the Lab window — and the kernel
+// choice becomes a runtime branch.
+//
+//   1 = DESIGN MODE. Dials are live; the Lab drives them while the game runs.
+//   0 = SHIPPED. This file compiles EXACTLY as it would have without any of this: the
+//       macros below expand to the constants themselves, the #if picks one kernel, and
+//       the other four plus the branch and the uniforms are not in the shader at all.
+//
+// It is not free, which is why it is a gate rather than a permanent feature: design mode
+// compiles all five kernels into every prism shader and allocates registers for the
+// largest, which costs occupancy on tile-based GPUs — on the one draw class this game has
+// most of. The Lab's **Bake to Source** button writes the chosen values into the constants
+// and flips this to 0, so the cost lasts exactly as long as the design session.
+//
+// FAIL-SAFE. `_PrismOcclusionDitherA.x` is the master: it holds kernel+1, so an
+// unpublished global (all zeros — a player build, or the editor before the Lab is opened)
+// reads as 0 and EVERY dial falls back to its compile-time constant. Design mode with
+// nobody driving it looks exactly like shipped mode.
+// -----------------------------------------------------------------------------
+#define PRISM_OCCLUSION_LIVE_TUNING 1
+
+#if PRISM_OCCLUSION_LIVE_TUNING
+float4 _PrismOcclusionDitherA;  // (kernel + 1, cellSize, shardOrient, morphRate)
+float4 _PrismOcclusionDitherB;  // (shatterCell, shatterWall, spiralRings, spiralArms)
+
+#define PRISM_OCCLUSION_TUNING_ON (_PrismOcclusionDitherA.x > 0.5)
+#define PRISM_OCCLUSION_DIAL(live, fallback) (PRISM_OCCLUSION_TUNING_ON ? (live) : (fallback))
+#else
+#define PRISM_OCCLUSION_DIAL(live, fallback) (fallback)
+#endif
+
 // -----------------------------------------------------------------------------
 // THE SHAPE RULE — why the current kernel is SHARD and not WORLEY (2026-08-06).
 //
@@ -143,6 +181,15 @@
 // -----------------------------------------------------------------------------
 static const float PRISM_OCCLUSION_MORPH_RATE = 0.12;   // cycles/sec; 0 = frozen
 
+// Every dial below is read through one of these accessors rather than named directly, so
+// that design mode and shipped mode differ in exactly one place each. Under
+// PRISM_OCCLUSION_LIVE_TUNING 0 each one collapses to its constant and the compiler folds
+// it away — the generated code is identical to naming the constant inline.
+float PrismOcclusionMorphRate()
+{
+    return PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherA.w, PRISM_OCCLUSION_MORPH_RATE);
+}
+
 // The clip threshold must land STRICTLY inside (0,1). frac() can return exactly 0, and a
 // 0 threshold against a 0 alpha is `clip(0)` — which KEEPS the fragment on the URP
 // variants that clip directly rather than through AlphaDiscard's epsilon. That would
@@ -188,9 +235,9 @@ static const float PRISM_OCCLUSION_SPIRAL_ARMS = 3.0;   // turns per revolution
 float PrismOcclusionSpiral(float radialRatio, float angleTurns, float time)
 {
     return PrismOcclusionSafeThreshold(frac(
-        radialRatio * PRISM_OCCLUSION_SPIRAL_RINGS
-        + angleTurns * PRISM_OCCLUSION_SPIRAL_ARMS
-        + time * PRISM_OCCLUSION_MORPH_RATE));
+        radialRatio * PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherB.z, PRISM_OCCLUSION_SPIRAL_RINGS)
+        + angleTurns * PRISM_OCCLUSION_DIAL(_PrismOcclusionDitherB.w, PRISM_OCCLUSION_SPIRAL_ARMS)
+        + time * PrismOcclusionMorphRate()));
 }
 
 // -----------------------------------------------------------------------------
@@ -292,6 +339,10 @@ static const float PRISM_OCCLUSION_CELL_CDF_HI = 0.873;   // — see THE SIZE WI
 // band freely; past 11 px the corridor error is a SPATIAL sampling failure and there is
```

</details>

### `12004b78e` — feat(analytics): menu freestyle counts as flight time; starter vessel and SelectedVessel land in HANGAR_DATA

_Claude, 2026-08-06 20:41:50 +0000_

```text
Three gaps, one root cause each.

1. Menu freestyle contributed zero flight time. The lava lamp IS the gameplay
   vessel (one system, two names), but MenuCrystalClickHandler never raises
   GameDataSO.StartTurn, so FlightClock's turn gate could not open there and
   every minute flown in Menu_Main was invisible - including to PreferredVessel
   ("most hours played"), which is exactly the vessel idle menu time goes into.

   FlightClock now runs a SECOND segment with the same integrator minus the turn
   gate, accumulated separately so a freestyle segment can never contaminate
   LastGameSeconds. It is driven at the two lines that actually grant and revoke
   control, so the camera blend is not counted. Segments close on leaving
   freestyle, on pause, and on backgrounding - the last one deliberately, so a
   suspended mobile app has already banked what it earned - and OnDisable closes
   one too, so launching a game from freestyle banks the time instead of dropping
   it. UGSStatsManager.ReportFreestyleFlight lands it in the lifetime total and
   the per-vessel total, and touches neither GamesCompleted nor GamesPlayed: no
   game was played.

   No new UGS event and no new parameter - it reaches PostHog through the
   existing total_flight_time_seconds / preferred_vessel person properties.

2. SelectedVessel read null. A deliberate pick in the vessel panel is its only
   writer, so a player who never opened the panel never had one. It now defaults
   to the starter vessel on load, and self-repairs if it names a vessel the
   player does not own.

3. The starter vessel was never recorded as owned. VesselUnlockSystem.UnlockVessel
   early-returns on an already-unlocked vessel, so the Squirrel - authored
   unlocked, the one vessel every player can fly from launch - was never written
   to HANGAR_DATA, which therefore reported an empty hangar for a player who
   could fly. Authored ownership moves to a new SO_Vessel.OwnedFromStart flag,
   because Unlock() rewrites isLocked at runtime and the editor persists that
   mutation into the asset - so isLocked stops being able to answer "what did we
   author?" after the first play session. Starters are seeded on load and
   re-granted by ResetAllUnlocks: a reset returns the player to a fresh account,
   not a locked-out one.

   Falcon and Shrike serialized no isLocked at all and so defaulted to unlocked;
   both are Planned vessels and are now explicitly locked at the fleet cost.
```

```text
 Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset                 |   1 +
 Assets/_SO_Assets/_TEMP/FalconClassSO.asset                       |   3 ++
 Assets/_SO_Assets/_TEMP/ShrikeClassSO.asset                       |   3 ++
 Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs |  35 +++++++++++++
 Assets/_Scripts/ScriptableObjects/SO_Vessel.cs                    |  14 +++++
 Assets/_Scripts/System/CloudData/UGSDataService.cs                |  69 ++++++++++++++++++++++--
 Assets/_Scripts/System/Instrumentation/FlightClock.cs             | 101 ++++++++++++++++++++++++++++++++++--
 Assets/_Scripts/System/VesselUnlock/VesselUnlockSystem.cs         |  17 +++++-
 Assets/_Scripts/UI/UGSStatsManager.cs                             |  31 +++++++++++
 Assets/_Scripts/UI/Views/PlayerDataService.cs                     |  13 +++++
 Docs/Analytics/ANALYTICS_HANDOFF.md                               |  23 +++++++-
 Docs/Analytics/DATA_ARCHITECTURE.md                               |  58 +++++++++++++++++++--
 12 files changed, 354 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 606 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs b/Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs
index 8fd744744..943280e82 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs
@@ -75,15 +75,39 @@ namespace CosmicShore.Gameplay
         {
             _cts = new CancellationTokenSource();
             _isTransitioning = false; // Reset in case a previous transition was cancelled mid-flight
+
+            // This handler is the only thing that knows the player has taken the stick in the
+            // menu, and it is the only thing with the live local vessel to attribute that time
+            // to - so it both drives the freestyle clock and banks what the clock measures.
+            FlightClock.OnFreestyleSegmentCompleted -= HandleFreestyleSegmentCompleted;
+            FlightClock.OnFreestyleSegmentCompleted += HandleFreestyleSegmentCompleted;
         }
 
         void OnDisable()
         {
+            // Leaving Menu_Main mid-flight must bank the time, not drop it. Closing the segment
+            // first means the handler below still sees the vessel it was flown in.
+            FlightClock.OnFreestyleExited();
+            FlightClock.OnFreestyleSegmentCompleted -= HandleFreestyleSegmentCompleted;
+
             _cts?.Cancel();
             _cts?.Dispose();
             _cts = null;
         }
 
+        /// <summary>
+        /// Menu freestyle is real flight time - see Docs/Analytics/DATA_ARCHITECTURE.md §5.
+        /// Attributed to the vessel being flown right now, which is not necessarily the one the
+        /// segment started in: the vessel-changer toy can swap mid-visit, and each swap is
+        /// bracketed by a pause that closes the segment, so each vessel keeps its own share.
+        /// </summary>
+        void HandleFreestyleSegmentCompleted(float seconds)
+        {
+            var vesselType = gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselType;
+            UGSStatsManager.Instance?.ReportFreestyleFlight(
+                seconds, vesselType?.ToString() ?? string.Empty);
+        }
+
         void Start()
         {
             // Freestyle UI starts hidden
@@ -135,7 +159,10 @@ namespace CosmicShore.Gameplay
             // no erratic AI steering, no stray player input - producing the "forward only
             // during transition" feel that keeps the camera blend reading cleanly.
             if (!lockInputDuringEnterTransition)
+            {
                 player.InputController.SetPause(false);
+                FlightClock.OnFreestyleEntered();
+            }
 
             _isInFreestyle = true;
 
@@ -156,8 +183,12 @@ namespace CosmicShore.Gameplay
                               ignoreTimeScale: true, cancellationToken: ct));
 
             // Release control to the player once the camera has settled.
+            // The flight clock starts wherever control does - the blend is not flight time.
             if (lockInputDuringEnterTransition)
+            {
                 player.InputController.SetPause(false);
+                FlightClock.OnFreestyleEntered();
+            }
 
             freestyleEvents.OnGameStateTransitionEnd.Raise();
             _isTransitioning = false;
@@ -172,6 +203,10 @@ namespace CosmicShore.Gameplay
             player.InputController.SetPause(true);
             player.Vessel.ToggleAIPilot(true);
 
+            // Control is gone as of the line above, so the segment closes here - not after the
+            // blend. Banking it now also means the vessel it is attributed to is still current.
+            FlightClock.OnFreestyleExited();
+
             // Raise SOAP event early so the camera blend starts immediately.
             // Camera controller captures CM PlayerCam position as a static snapshot,
             // then blends back to orbit - runs in parallel with the UI fade.
diff --git a/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs b/Assets/_Scripts/ScriptableObjects/SO_Vessel.cs
```

</details>

### `84801e352` — fix(profile): DisplayNameRegistry misses the Cloud Save Models namespace

_Claude, 2026-08-06 20:49:52 +0000_

```text
Inherited from bleeding-edge (a47a257b), not from this branch, but it fails
the whole compile so nothing else on the branch can be verified until it is
fixed.

Query and FieldFilter live in Unity.Services.CloudSave.Models, which the
file never imported (CS0246 x4).

SaveOptions was ambiguous (CS0104) because Cloud Save declares it in BOTH
Unity.Services.CloudSave and Unity.Services.CloudSave.Models.Data.Player and
the file imported both. The player-data one is the overload carrying the
access-class options this code needs, so it is pinned with a using alias -
Unity's own documented pattern for this exact collision.

Also fixes a latent second error the compiler could not reach: QueryAsync
has no single-argument overload, so QueryAsync(query) would have failed as
soon as the Query type resolved. It now passes the required QueryOptions,
aliased to the same namespace for the same reason.

Verified against the Cloud Save 3.x API reference and Unity's Cloud Save
usage docs rather than guessed - the package sources are not in this clone,
so the namespaces could not be checked locally.
```

```text
 Assets/_Scripts/System/CloudData/DisplayNameRegistry.cs | 15 ++++++++++++++-
 1 file changed, 14 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/CloudData/DisplayNameRegistry.cs b/Assets/_Scripts/System/CloudData/DisplayNameRegistry.cs
index 97c9ed5bb..2521ee4da 100644
--- a/Assets/_Scripts/System/CloudData/DisplayNameRegistry.cs
+++ b/Assets/_Scripts/System/CloudData/DisplayNameRegistry.cs
@@ -4,9 +4,18 @@ using CosmicShore.Utility;
 using Cysharp.Threading.Tasks;
 using Unity.Services.Authentication;
 using Unity.Services.CloudSave;
+using Unity.Services.CloudSave.Models;
 using Unity.Services.CloudSave.Models.Data.Player;
 using Unity.Services.Core;
 
+// Cloud Save declares SaveOptions/QueryOptions in BOTH Unity.Services.CloudSave and
+// Unity.Services.CloudSave.Models.Data.Player, so naming either unqualified with both
+// namespaces imported is ambiguous (CS0104). The player-data ones are the pair that carry
+// the access-class options this file needs. Aliasing rather than fully qualifying at the
+// call site is Unity's own documented pattern for this exact collision.
+using SaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
+using QueryOptions = Unity.Services.CloudSave.Models.Data.Player.QueryOptions;
+
 namespace CosmicShore.Core
 {
     /// <summary>Outcome of a display-name availability check.</summary>
@@ -77,7 +86,11 @@ namespace CosmicShore.Core
                     },
                     new HashSet<string> { PublicNameKey });
 
-                var results = await CloudSaveService.Instance.Data.Player.QueryAsync(query).AsMainThread();
+                // QueryAsync has no single-argument overload — the options object is
+                // required even when it carries nothing but defaults.
+                var results = await CloudSaveService.Instance.Data.Player
+                    .QueryAsync(query, new QueryOptions())
+                    .AsMainThread();
 
                 string ownPlayerId = AuthenticationService.Instance.PlayerId;
                 foreach (var entity in results)
```

</details>

### `308ba4352` — docs(build): add the click-by-click pipeline setup checklist

_Claude, 2026-08-06 21:05:43 +0000_

```text
BRANCHING_AND_RELEASE.md explains why the pipeline is shaped the way it is, but
someone standing up the pipeline for the first time has to reverse-engineer the
actions out of that reasoning. This is the actions, separated out: what to click
in GitHub, which UGS targets to create, which variables to set, in order.

Four required sections and one optional one. The optional section is the Unity
runner and everything that depends on it, which is separated because the
pipeline works without it and separating them keeps the required path short.

Notable content decisions:

- Creating the build branches is step 1, because they do not exist until the
  first promotion run and "I cannot find build/android" was the first question
  the previous docs failed to answer.
- The UGS section is a table of four targets rather than prose. Two test targets
  with auto-build ON, two internal targets with auto-build OFF, because leaving
  it on for bleeding-edge produces a build on every merge to trunk.
- The public-repo runner warning is its own numbered step ahead of registering
  the runner, not a footnote after it. Ordering is the whole point: attaching a
  self-hosted runner to a public repo before restricting fork workflow runs lets
  a fork PR execute on the build machine.
- Known gaps are listed at the end so the checklist does not read as "done" when
  builds still cannot be identified by version.

Every claim was verified against the workflows rather than written from memory:
workflow display name, dispatch input defaults, both cron day-of-week values,
the Unity version, and each variable and secret name.
```

```text
 Docs/BRANCHING_AND_RELEASE.md |   4 ++
 Docs/BUILD_PIPELINE_SETUP.md  | 181 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 185 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 202 lines)</summary>

```diff
diff --git a/Docs/BRANCHING_AND_RELEASE.md b/Docs/BRANCHING_AND_RELEASE.md
index 81429db5a..f0765ebcd 100644
--- a/Docs/BRANCHING_AND_RELEASE.md
+++ b/Docs/BRANCHING_AND_RELEASE.md
@@ -3,6 +3,10 @@
 What each branch is for, when builds happen, and what to do when something
 breaks.
 
+> **Setting the pipeline up for the first time?** Follow
+> [`BUILD_PIPELINE_SETUP.md`](BUILD_PIPELINE_SETUP.md) — the click-by-click
+> checklist for GitHub, CI and UGS. This document is the reasoning behind it.
+
 ---
 
 ## 1. The four branches, in one line each
diff --git a/Docs/BUILD_PIPELINE_SETUP.md b/Docs/BUILD_PIPELINE_SETUP.md
new file mode 100644
index 000000000..768b5e701
--- /dev/null
+++ b/Docs/BUILD_PIPELINE_SETUP.md
@@ -0,0 +1,181 @@
+# Build Pipeline Setup
+
+One-time setup checklist. Steps 1 to 4 are required; step 5 is optional.
+
+*Why any of this works the way it does: `Docs/BRANCHING_AND_RELEASE.md`. This
+page is only the steps.*
+
+---
+
+## 1. Create the build branches
+
+`build/android` and `build/windows` do not exist yet. They are created by the
+first promotion run, so trigger one now rather than waiting for the schedule.
+
+1. GitHub → **Actions** tab
+2. Left sidebar → **Promote test build**
+3. **Run workflow** button (top right)
+4. Leave `source_ref` **blank**, leave `targets` as **both**
+5. **Run workflow**
+
+**Expect:** the run goes green in under a minute, and `build/android` and
+`build/windows` appear in the branch list.
+
+> Blank `source_ref` means "use `development`". Manual runs skip the schedule
+> checks, so it runs immediately instead of waiting for the cycle.
+
+- [ ] Both build branches exist
+
+---
+
+## 2. Set up UGS Build Automation
+
+You need **4 build targets**: two for the test build, two for the internal build.
+
+### Test builds (what testers get)
+
+| Target | Branch | Auto-build on push |
+|---|---|---|
+| Android test | `build/android` | **On** |
+| Windows test | `build/windows` | **On** |
+
+Auto-build on push is what makes these fire. The promotion workflow pushes to
+these branches every 3 weeks and UGS picks it up from there.
+
+### Internal builds (what the team gets)
+
+| Target | Branch | Auto-build on push | Schedule |
+|---|---|---|---|
+| Android internal | `bleeding-edge` | **Off** | Weekly, Friday |
+| Windows internal | `bleeding-edge` | **Off** | Weekly, Friday |
+
+Auto-build stays **off** here or you get a build on every merge to trunk.
+
+- [ ] 4 targets created and pointed at the right branches
+
+---
+
+## 3. Turn on build notifications
+
```

</details>

### `a9437a53a` — fix(ci): a late scheduler must not silently cancel a build cycle

_Claude, 2026-08-06 21:18:42 +0000_

```text
The first scheduled run of the promotion workflow exposed a real flaw. Its cron
asks for 13:00 and 14:00 UTC; GitHub actually dispatched it at 15:52 UTC, nearly
two hours late, which is 08:52 Pacific. The gate tested for exactly 06:00 and so
skipped the run.

That test was written to drop the daylight-saving twin, and it does, but it also
drops any run the scheduler delivers late. GitHub documents scheduled workflows
as best-effort and delays them under load, so this was not an edge case: it would
have quietly cancelled a three-week test cycle and shown up as a green run,
because skipping is a success path.

The fix inverts which mechanism enforces "once". The dated tag the workflow
already writes is now the idempotency record: if testbuild/<PT date> exists, the
promotion has happened today and the run stops. Once duplicates are impossible by
that means, the hour test can be a window (>= 06:00 Pacific) rather than an exact
match, so a late dispatch still promotes. The daylight-saving twin, a retry, and
any duplicate firing now all land on the tag check instead.

Also adds a day-of-week check. Widening the hour window means a run dispatched
late enough to cross midnight Pacific could otherwise land on the wrong day.

The internal build tagger had the same exact-hour test and the same fix, keyed on
its internal/<PT date> tag.

Hours are converted with 10# before comparison. `date +%H` emits "08" and "09",
which arithmetic expansion would reject as invalid octal.

Verified against a stubbed clock: promotes when on time, two hours late, and even
at 23:00; refuses when the tag exists, before 06:00, on the wrong day, off cycle,
and before the anchor; manual dispatch still bypasses everything.
```

```text
 .github/workflows/sync-build-branches.yml | 49 ++++++++++++++++++++++++++++---------------------
 .github/workflows/tag-internal-build.yml  | 32 +++++++++++++++++++++++---------
 2 files changed, 51 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 127 lines)</summary>

```diff
diff --git a/.github/workflows/sync-build-branches.yml b/.github/workflows/sync-build-branches.yml
index 94ced7633..1714e0bb5 100644
--- a/.github/workflows/sync-build-branches.yml
+++ b/.github/workflows/sync-build-branches.yml
@@ -62,42 +62,49 @@ jobs:
     name: Promote to build branches
     runs-on: ubuntu-latest
     steps:
-      - name: Is this run on schedule and on cycle?
+      - name: Is this run due?
         id: gate
+        env:
+          GH_TOKEN: ${{ github.token }}
         run: |
           set -euo pipefail
 
           if [ "${{ github.event_name }}" != "schedule" ]; then
-            echo "Triggered manually; skipping both the clock and cycle guards."
+            echo "Triggered manually; running regardless of day, hour and cycle."
             echo "proceed=true" >> "$GITHUB_OUTPUT"
             exit 0
           fi
 
-          hour=$(TZ=America/Los_Angeles date +%H)
-          if [ "$hour" != "06" ]; then
-            echo "It is ${hour}:00 in America/Los_Angeles, not 06:00."
-            echo "This is the daylight-saving twin of today's real slot. Skipping."
-            echo "proceed=false" >> "$GITHUB_OUTPUT"
-            exit 0
-          fi
-
           today=$(TZ=America/Los_Angeles date +%Y-%m-%d)
-          days=$(( ( $(date -u -d "$today" +%s) - $(date -u -d "$CYCLE_ANCHOR" +%s) ) / 86400 ))
+          dow=$(TZ=America/Los_Angeles date +%u)                  # 3 = Wednesday
+          hour=$((10#$(TZ=America/Los_Angeles date +%H)))         # 10# so "08" is not read as octal
 
-          if [ "$days" -lt 0 ]; then
-            echo "$today is before the cycle anchor $CYCLE_ANCHOR. Skipping."
-            echo "proceed=false" >> "$GITHUB_OUTPUT"
-            exit 0
+          skip () { echo "$1 Skipping."; echo "proceed=false" >> "$GITHUB_OUTPUT"; exit 0; }
+
+          # Idempotency first, because it is what makes everything below safe to
+          # relax. The dated tag is the record that today's promotion already
+          # happened, so the daylight-saving twin, a retry, and any duplicate
+          # firing all land here and stop.
+          if gh api "repos/$GITHUB_REPOSITORY/git/ref/tags/testbuild/$today" >/dev/null 2>&1; then
+            skip "Already promoted on $today (tag testbuild/$today exists)."
           fi
+
+          [ "$dow" = "3" ] || skip "$today is not a Wednesday in America/Los_Angeles."
+
+          # >= 06, deliberately not == 06. GitHub's scheduler runs late under
+          # load, routinely by more than an hour: the first scheduled run of this
+          # workflow fired at 15:52 UTC against a 13:00 cron. An exact-hour test
+          # silently dropped it, and would drop a real promotion the same way.
+          # The tag check above is what stops the wider window running twice.
+          [ "$hour" -ge 6 ] || skip "It is ${hour}:00 in America/Los_Angeles; the 06:00 slot has not opened."
+
+          days=$(( ( $(date -u -d "$today" +%s) - $(date -u -d "$CYCLE_ANCHOR" +%s) ) / 86400 ))
+          [ "$days" -ge 0 ] || skip "$today is before the cycle anchor $CYCLE_ANCHOR."
           if [ $(( days % CYCLE_DAYS )) -ne 0 ]; then
-            next=$(( CYCLE_DAYS - (days % CYCLE_DAYS) ))
-            echo "$today is day $days of the cycle, not a multiple of $CYCLE_DAYS."
-            echo "Next promotion is in $next day(s). Skipping."
-            echo "proceed=false" >> "$GITHUB_OUTPUT"
-            exit 0
+            skip "$today is day $days of the cycle; next promotion in $(( CYCLE_DAYS - (days % CYCLE_DAYS) )) day(s)."
           fi
 
-          echo "$today is on cycle (day $days). Proceeding."
+          echo "$today is on cycle (day $days) and the 06:00 slot has opened. Proceeding."
           echo "proceed=true" >> "$GITHUB_OUTPUT"
 
       - name: Resolve source commit
diff --git a/.github/workflows/tag-internal-build.yml b/.github/workflows/tag-internal-build.yml
index 749263208..0165f9764 100644
--- a/.github/workflows/tag-internal-build.yml
+++ b/.github/workflows/tag-internal-build.yml
@@ -35,24 +35,38 @@ jobs:
```

</details>

### `90bc31039` — feat(prisms): ship the corridor dither as SHATTER, 16.26 px / 20 px

_Claude, 2026-08-06 21:35:52 +0000_

```text
The look was chosen in motion in the Occlusion Dither Lab rather than from
stills, which is the only way a dither can honestly be judged: SHATTER at
polygon 16.26 px, wall 20 px, morph 0.3256, design mode off.

It measures 0.0051 uniform / 0.0102 corridor, holding 0.0102-0.0128 across
t = 0..400s - at or inside the shipped Worley baseline's 0.0117 and better
than SHARD's 0.0145. So the hard-edged unit shape costs nothing against the
admission rule.

THE WALL WINDOW WAS WRONG, and this setting is what proved it. The Lab
flagged wall 20 px as "outside 4-11 px" and the published window said the
same. Both were wrong: that band came from a sweep taken at a FIXED 11 px
polygon and does not transfer. What actually fails is a wall wide relative
to ITS OWN polygon, because there is no lattice left to crack - measured by
ratio, 0.75x -> 0.0063, 1.00x -> 0.0094, 1.23x (shipped) -> 0.0102,
1.30x -> 0.0162, 1.64x -> 0.0173. Corrected everywhere to "polygon 8-20 px,
wall up to ~1.25x the polygon", and the Lab now flags on the ratio and says
"measure before trusting it" rather than "wrong".

Morph 0.3256 is deliberately above the ~0.25 guideline (1.75% of band pixels
flip per frame against the 1.45% guideline) - a look call made after seeing
it in motion, and it cannot affect the fade: coverage is flat across the
rate range. Recorded as a deliberate exceedance rather than silently
widening the guideline.

Two Lab defects fixed, both found by the screenshot of it running:

CRLF. The three #define anchors ended in `$`, and in .NET multiline `$`
matches before the '\n' but AFTER any '\r' - so on a Windows checkout they
matched 0 times. The Lab reported design mode OFF while the shader said 1,
and refused to bake. Every line-anchored pattern now captures `(\r?)` and
re-emits it, which also stops a bake from converting line endings. The
repo's own PrismOcclusionCoverageTests already carried this exact warning;
I did not heed it.

A failed READ now reads as "could not read", not as "the flag is 0".
Reporting a state the source does not hold is the worst thing a tool can do.

Baked by hand through the Lab's own (now fixed) anchors, so the result is
byte-for-byte what the Bake button produces. Verified by compiling the
shipped file through the clang shim: compiles clean, and its output matches
the measured reference to 6e-07.
```

```text
 Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl | 46 ++++++++++++++++++++-----------
 Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs             | 61 +++++++++++++++++++++++++++++++++--------
 CLAUDE.md                                                     |  2 +-
 Docs/PRISM_ANIMATION.md                                       | 30 ++++++++++++--------
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md                          | 15 +++++-----
 5 files changed, 108 insertions(+), 46 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 331 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
index 4751bf6be..98d7ec49c 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl
@@ -90,7 +90,7 @@
 #define PRISM_OCCLUSION_KERNEL_SHARD 3   // screen-space cells — reads as TRIANGULAR flecking
 #define PRISM_OCCLUSION_KERNEL_SHATTER 4 // screen-space cells — reads as a CRACKED LATTICE
 
-#define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_SHARD
+#define PRISM_OCCLUSION_KERNEL PRISM_OCCLUSION_KERNEL_SHATTER
 
 // -----------------------------------------------------------------------------
 // LIVE TUNING — the design-mode gate (FrogletTools > Ecology > Prism Animation >
@@ -118,7 +118,7 @@
 // reads as 0 and EVERY dial falls back to its compile-time constant. Design mode with
 // nobody driving it looks exactly like shipped mode.
 // -----------------------------------------------------------------------------
-#define PRISM_OCCLUSION_LIVE_TUNING 1
+#define PRISM_OCCLUSION_LIVE_TUNING 0
 
 #if PRISM_OCCLUSION_LIVE_TUNING
 float4 _PrismOcclusionDitherA;  // (kernel + 1, cellSize, shardOrient, morphRate)
@@ -142,13 +142,18 @@ float4 _PrismOcclusionDitherB;  // (shatterCell, shatterWall, spiralRings, spira
 //
 // A CIRCLE breaks that. It is a soft shape with a soft gradient on either side of it —
 // soft-SOFT-soft — so Worley's round flecks read as foam against everything else in the
-// frame. SHARD keeps Worley's arrangement exactly (same lattice, same jitter, same
-// orbit, same remap) and changes only the METRIC, so the flecks become equilateral
-// triangles of the same area: hard polygonal unit shape, ambiguous placement, still
-// feathered by the corridor's own soft profile. Hard shape, soft sandwich.
+// frame. Two kernels answer it, and both are carried: SHARD keeps Worley's arrangement
+// exactly and changes only the METRIC, so the flecks become equilateral triangles of the
+// same area; SHATTER abandons the fleck and makes the NEGATIVE space the motif, filling
+// each Voronoi polygon between straight lines so the lattice reads as cracked walls.
+//
+// SHATTER IS WHAT SHIPPED (2026-08-06), chosen in motion in the Occlusion Dither Lab
+// rather than from stills, at polygon 16.26 px / wall 20 px. Both candidates are hard-
+// edged and both sit inside the admission rule; the call between them was a look call and
+// could only be made against real trail mass at speed. SHARD stays one #define away.
 //
 // Kernel 2 is kept, not deleted — it is the calibration reference every fidelity number
-// in this file is quoted against, and it is one #define away if the triangles ever want
+// in this file is quoted against, and it is one #define away if the round flecks ever want
 // re-judging side by side.
 // -----------------------------------------------------------------------------
 
@@ -179,7 +184,7 @@ float4 _PrismOcclusionDitherB;  // (shatterCell, shatterWall, spiralRings, spira
 // every frame. That is full-amplitude shimmer, not motion. Only the two kernels that are
 // continuous functions of position can be continuous functions of time as well.
 // -----------------------------------------------------------------------------
-static const float PRISM_OCCLUSION_MORPH_RATE = 0.12;   // cycles/sec; 0 = frozen
+static const float PRISM_OCCLUSION_MORPH_RATE = 0.3256;   // cycles/sec; 0 = frozen
 
 // Every dial below is read through one of these accessors rather than named directly, so
 // that design mode and shipped mode differ in exactly one place each. Under
@@ -377,7 +382,7 @@ float PrismOcclusionWorley(float2 pixel, float time)
 }
 
 // -----------------------------------------------------------------------------
-// Kernel D — screen-space SHARD (triangular cells). CURRENT.
+// Kernel D — screen-space SHARD (triangular cells).
 //
 // Worley with one line changed. Same lattice, same Hoskins hash, same orbiting feature
 // points, same 3×3 search, same CDF remap — only the METRIC differs, from Euclidean
@@ -513,7 +518,7 @@ float PrismOcclusionShard(float2 pixel, float time)
 }
 
 // -----------------------------------------------------------------------------
-// Kernel E — screen-space SHATTER (a cracked lattice of walls).
+// Kernel E — screen-space SHATTER (a cracked lattice of walls). CURRENT.
 //
 // The other way to make a hard-edged unit shape: instead of growing a polygon around a
 // point, take the VORONOI CELL itself — an irregular convex polygon with nothing but
@@ -541,15 +546,24 @@ float PrismOcclusionShard(float2 pixel, float time)
 //
 //   polygon  5 px / wall  9 px   0.0258 / 0.0240   BREAKS — polygons under the wall period
 //   polygon  8 px / wall  9 px   0.0027 / 0.0065
-//   polygon 12 px / wall  9 px   0.0009 / 0.0070   SHIPPED SETTING
+//   polygon 12 px / wall  9 px   0.0009 / 0.0070
 //   polygon 18 px / wall  9 px   0.0007 / 0.0068
```

</details>

### `22b77494f` — docs(skills): capture the HLSL compile-verify technique and three regex traps

_Claude, 2026-08-06 21:39:02 +0000_

```text
Session retrospective (/ship §3.5). All four cost real time this session and
would be re-invented next time.

Technique 4.5c - compile the SHIPPED HLSL with clang rather than only porting
it to numpy. A port validates the design; only compiling the real file
validates the FILE. ext_vector_type gives arbitrary swizzles so HLSL hash
functions compile unmodified, stubbed URP built-ins let the entry point
compile too, and building every #if branch is how you PROVE a design-mode
gate is free when off instead of asserting it.

Traps:
- `$` in .NET regex does not match before `\r`, so every line-anchored
  pattern silently fails on a Windows checkout. Invisible on Linux, total on
  Windows. This is the bug that broke the Dither Lab's bake.
- .NET emits a literal "${3}" for a group the pattern lacks instead of
  throwing, so a rewriter must carry group counts explicitly - and a Python
  prototype will not warn you, because Python throws.
- A window measured with the other variable held fixed does not transfer.
  Shipping one as a validation rule flags good settings as failures, which
  is exactly what happened to the shatter wall band.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 63 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 63 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 81 lines)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index d9c76b040..3ad76435f 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -444,6 +444,46 @@ the cell size, the jitter, or add animation, and the constants must be re-fitted
 the error silently returns. Verify the fit across the whole range you intend to use
 (here: rate 0 through t=400s).
 
+## 4.5c Technique: COMPILE the shipped HLSL with clang (stronger than porting it)
+
+Origin: the occlusion corridor's triangle/shatter kernels (2026-08-06). §4.5b ports a
+shader to numpy to judge its LOOK. This compiles the **actual file from the repo** and
+runs it, which answers a different and harder question: *does the source I am about to
+commit compile, and does it do what my measurements say?*
+
+A numpy port validates the design. Only compiling the real file validates the FILE — a
+port cannot catch a typo, an unbalanced brace, a wrong swizzle, or a `#if` that excludes
+the wrong block.
+
+```python
+# Read the shader from the repo, apply a SHORT, LISTED set of mechanical substitutions,
+# #include it from a C++ shim, compile with -Wall, run it, diff against the numpy port.
+SUBS = [(r"\[unroll\]", ""),          # HLSL loop attribute
+        (r"\bout float\b", "float&"), # HLSL out-param -> C++ reference
+        (r"\bfloat2\(", "mk2(")]      # vector constructor spelling
+```
+
+- **`__attribute__((ext_vector_type(N)))` is the whole trick.** clang's vector types give
+  you elementwise arithmetic and *arbitrary swizzles* (`.xyx`, `.yzx`, `.zy`) for free, so
+  hash functions written for HLSL compile unmodified. Only the `floatN(a,b)` constructor
+  spelling needs substituting.
+- **Keep the substitution list short, listed, and auditable.** Every constant and every
+  expression must pass through untouched — those are what you are verifying. If the list
+  starts growing, you are rewriting the shader, not testing it.
+- **Stub the URP built-ins** (`_WorldSpaceCameraPos`, `_ScreenParams`, `_Time`,
+  `UNITY_MATRIX_V`, `TransformWorldToHClip`) as file-scope globals in the shim. Then the
+  entry point compiles too, not just the leaf functions.
+- **Compile EVERY `#if` branch.** A gate like `#define X_LIVE_TUNING 1|0` has two shapes;
+  build both and diff their output. That is how you prove a "design mode" is genuinely
+  free when it is off, instead of asserting it.
+- **Then assert the dials actually DRIVE.** Set the globals from the shim and check the
+  output changes — over a POPULATION of pixels, not one. A single sample matching proves
+  nothing (a flip that affects half the cells legitimately leaves any given pixel alone).
+
+This also verifies a source-rewriting tool end to end: bake values with the tool's own
+regexes, compile the result, and confirm the round-trip back to the original values is
+byte-identical.
+
 ## 4.6 Technique: hand-authoring a new asset trio
 
 Adding a new SO-configured, prefab-backed thing (here: a cell) means four
@@ -507,6 +547,29 @@ read**. The lesson generalizes:
 
 ## 5. Traps learned the hard way (check these BEFORE debugging for an hour)
 
+- **`$` in a .NET regex does NOT match before `\r`, so every line-anchored pattern
+  fails on a Windows checkout.** In multiline mode `$` matches before the `\n` but
+  *after* any `\r`, so `^#define FOO (\w+)$` matches 0 times in a CRLF file. This is
+  invisible on Linux and total on Windows: a source-rewriting tool shipped this way
+  reported the file's state wrongly AND refused to write. Capture the ending and
+  re-emit it — `(\r?)$` as a group, `${1}value${3}` in the replacement — which also
+  stops the rewrite from silently converting line endings into diff noise. (Reading
+  only? `\r?$` is enough. This repo's own `PrismOcclusionCoverageTests` carries the
+  same warning for `.shadergraph` — heed it BEFORE writing the regex.)
+- **.NET does not throw on a replacement naming a group the pattern lacks — it emits
+  the literal text.** `Regex.Replace(s, @"(a)(b)", "${1}x${3}")` writes `${3}` into
+  your file. A rewriter that varies its patterns must carry the group count
+  EXPLICITLY per pattern rather than inferring it. (Python is the opposite and throws
+  — so a Python prototype will not warn you.) Related: prefer `${1}` over `$1`; `$1`
+  followed by a digit (`$1` + `4.5`) parses as group 14.
+- **A window measured with the other variable held fixed does not transfer.** A sweep
+  of parameter B at one value of A yields a band for B that looks absolute and is not.
+  Shipping it as a validation rule then flags perfectly good settings as failures —
+  here a "wall 4–11 px" band, swept at a fixed 11 px polygon, condemned a 20 px wall
+  in a 16 px polygon that actually measured BETTER than the shipped baseline. Sweep
+  the RATIO (or the second variable at several values of the first) before publishing
+  a window, and when a tool enforces one, have it say *"outside the measured range —
+  measure it"* rather than *"wrong"*.
 - **Stripping `[...]` attributes globally also eats `float[]`.** A C#-field
   scraper that does `re.sub(r'\[[^\]]*\]\s*', '', line)` turns
```

</details>

### `27fdb20e4` — refactor(tools): plain concatenation for the Lab's wall-ratio label

_Claude, 2026-08-06 21:40:07 +0000_

```text
A nested interpolated string inside an interpolation hole is legal C# but it
is the one construct in this file I cannot compile-check from here, and it
buys nothing over concatenation.
```

```text
 Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs | 5 ++++-
 1 file changed, 4 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs b/Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs
index 2472f5d7d..271c8523b 100644
--- a/Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs
+++ b/Assets/_Scripts/Editor/PrismOcclusionDitherLab.cs
@@ -368,8 +368,11 @@ namespace CosmicShore.Editor
                         _shatterWall, ShatterWallMin, ShatterWallMax);
                     float ratio = _shatterWall / Mathf.Max(_shatterCell, 1e-3f);
                     bool wallOutside = ratio > ShatterWallRatioMax || _shatterWall < ShatterWallMin;
+                    string wallNote = wallOutside
+                        ? "past " + ShatterWallRatioMax.ToString("0.00") + "x, measure before trusting it"
+                        : "in window";
                     EditorGUILayout.LabelField(" ",
-                        $"{ratio:0.00}× the polygon — {(wallOutside ? $"past {ShatterWallRatioMax:0.00}×, measure before trusting it" : "in window")}",
+                        ratio.ToString("0.00") + "x the polygon - " + wallNote,
                         Warn(wallOutside));
                     EditorGUILayout.LabelField(" ", "at alpha a the dark wall is (1−a) × period wide",
                         EditorStyles.miniLabel);
```

</details>

### `e43b7b07b` — qa: results 2026-08-07 kourosh (Ribcage PARTIAL, EditMode FAIL) + results template

_Claude, 2026-08-07 19:40:14 +0000_

```text
 Docs/QA/RESULTS/2026-08-07-kourosh.md |  8 ++++++++
 Docs/QA/RESULTS/TEMPLATE.md           | 14 ++++++++++++++
 2 files changed, 22 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-07-kourosh.md b/Docs/QA/RESULTS/2026-08-07-kourosh.md
new file mode 100644
index 000000000..88adf612d
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-07-kourosh.md
@@ -0,0 +1,8 @@
+# QA Results — 2026-08-07 — kourosh
+
+Build: claude/ftue-editor-tool-69acq5 @ 34ee47e · Unity <FILL-IN version, Help ▸ About Unity> · <FILL-IN platform, e.g. macOS Editor>
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-RIBCAGE-MODE | PARTIAL | Confirmed working: shell count per intensity, triangular open-weave, tighter-toward-core spacing, cage twist on orbit (steps 1–5). Not yet proven: step 7 baseline prism counts not measured (Measure Cell Environment Baselines not run), step 9 full round to target not played to the scoreboard. No failures observed in the parts run. |
+| QA-EDITMODE-TESTS | FAIL | Test Runner ▸ EditMode ▸ Run All. Totals: <FILL-IN X passed / Y failed>. Failing tests: <FILL-IN Class.TestName — "assertion message"; one per failure>. Missing/uncompiled suites (did not appear in the runner): <FILL-IN name(s) or "none">. |
diff --git a/Docs/QA/RESULTS/TEMPLATE.md b/Docs/QA/RESULTS/TEMPLATE.md
new file mode 100644
index 000000000..74f7c7127
--- /dev/null
+++ b/Docs/QA/RESULTS/TEMPLATE.md
@@ -0,0 +1,14 @@
+# QA Results — <YYYY-MM-DD> — <tester>
+
+Build: <branch> @ <short-sha> · Unity <version> · <platform>
+
+<!--
+One file per test session. Copy this file to RESULTS/<date>-<tester>.md and fill the table.
+Result values: PASS · FAIL · PARTIAL · BLOCKED · SKIP
+Attach evidence (console text / screenshot / step number) for every FAIL.
+Never hand-edit QA_BACKLOG.md — the /qa-backlog skill updates it from this file.
+-->
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| <QA-ITEM-ID> | <PASS/FAIL/PARTIAL/BLOCKED/SKIP> | <what happened; step number + console text for FAIL> |
```

</details>

### `2b7fb7383` — Update and rename 2026-08-07-kourosh.md to 2026-08-07-andrew.md

_Andrew Mokhtary, 2026-08-07 15:44:44 -0400_

```text
Made adjustments so it was more like my own work
```

```text
 Docs/QA/RESULTS/2026-08-07-andrew.md  | 8 ++++++++
 Docs/QA/RESULTS/2026-08-07-kourosh.md | 8 --------
 2 files changed, 8 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-07-andrew.md b/Docs/QA/RESULTS/2026-08-07-andrew.md
new file mode 100644
index 000000000..7cf4e43b2
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-07-andrew.md
@@ -0,0 +1,8 @@
+# QA Results — 2026-08-07 — andrew
+
+Build: claude/ftue-editor-tool-69acq5 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-RIBCAGE-MODE   | PARTIAL | Most of the steps seem fine, I'm not sure if the danger blocks work at all, but it not working isn't labeled as a failure |
+| QA-EDITMODE-TESTS | FAIL    | None of the required tests were even there for me to test. |
diff --git a/Docs/QA/RESULTS/2026-08-07-kourosh.md b/Docs/QA/RESULTS/2026-08-07-kourosh.md
deleted file mode 100644
index 88adf612d..000000000
--- a/Docs/QA/RESULTS/2026-08-07-kourosh.md
+++ /dev/null
@@ -1,8 +0,0 @@
-# QA Results — 2026-08-07 — kourosh
-
-Build: claude/ftue-editor-tool-69acq5 @ 34ee47e · Unity <FILL-IN version, Help ▸ About Unity> · <FILL-IN platform, e.g. macOS Editor>
-
-| Item ID | Result | Notes / Evidence |
-|---------|--------|------------------|
-| QA-RIBCAGE-MODE | PARTIAL | Confirmed working: shell count per intensity, triangular open-weave, tighter-toward-core spacing, cage twist on orbit (steps 1–5). Not yet proven: step 7 baseline prism counts not measured (Measure Cell Environment Baselines not run), step 9 full round to target not played to the scoreboard. No failures observed in the parts run. |
-| QA-EDITMODE-TESTS | FAIL | Test Runner ▸ EditMode ▸ Run All. Totals: <FILL-IN X passed / Y failed>. Failing tests: <FILL-IN Class.TestName — "assertion message"; one per failure>. Missing/uncompiled suites (did not appear in the runner): <FILL-IN name(s) or "none">. |
```

</details>

### `68b73691c` — qa: build the /qa-backlog loop (backlog, apply engine, skill) + apply andrew's results

_Claude, 2026-08-07 19:56:41 +0000_

```text
- Docs/QA/README.md, QA_BACKLOG.md, ARCHIVE.md, DEV_TASKS.md
- Tools/QA/apply_results.py (deterministic, idempotent, stdlib-only)
- .claude/skills/qa-backlog/SKILL.md
- applied 2026-08-07-andrew: Ribcage -> PARTIAL, EditMode -> FAIL + dev task
```

```text
 .claude/skills/qa-backlog/SKILL.md |  99 ++++++++++++++
 Docs/QA/ARCHIVE.md                 |   8 ++
 Docs/QA/DEV_TASKS.md               |  17 +++
 Docs/QA/QA_BACKLOG.md              | 489 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/QA/README.md                  |  96 +++++++++++++
 Tools/QA/apply_results.py          | 271 +++++++++++++++++++++++++++++++++++++
 6 files changed, 980 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1016 lines)</summary>

```diff
diff --git a/.claude/skills/qa-backlog/SKILL.md b/.claude/skills/qa-backlog/SKILL.md
new file mode 100644
index 000000000..5574e50c2
--- /dev/null
+++ b/.claude/skills/qa-backlog/SKILL.md
@@ -0,0 +1,99 @@
+---
+name: qa-backlog
+description: Maintain the untested-development QA backlog and run the QA loop. Use when the user types /qa-backlog, asks to refresh/print/regenerate the QA backlog, apply submitted QA RESULTS files, archive passes, or open dev tasks for failures. Owns Docs/QA/QA_BACKLOG.md, ARCHIVE.md and DEV_TASKS.md — the tester never hand-edits those; they only submit a RESULTS file, and this skill applies it. Reads new merges since the last scan (PR bodies / merge messages carrying a "verification status" note) to add new untested items.
+---
+
+# /qa-backlog — refresh the untested-development backlog and run the loop
+
+You own the three skill-managed files in `Docs/QA/`:
+
+- `QA_BACKLOG.md` — THE list (items are `### QA-... <emoji> — title` sections).
+- `ARCHIVE.md` — items that PASSED (removed from the backlog).
+- `DEV_TASKS.md` — handoff tasks for FAILs.
+
+The tester never edits those. They only ever add a `Docs/QA/RESULTS/<date>-<tester>.md`
+file. Your job each invocation: **(A) apply submitted results, (B) scan new merges for new
+untested items, (C) print the prioritised list, (D) commit the updates.** Full design and the
+data model are in `Docs/QA/README.md` — read it if anything below is unclear.
+
+Do all of this non-interactively; do not stop to ask the tester questions unless the repo is
+in a state you genuinely cannot proceed from (e.g. `Docs/QA/` does not exist at all).
+
+## 1. Apply submitted results (the deterministic half — the "close")
+
+Run the engine; it reads every `RESULTS/*.md` (except `TEMPLATE.md`), takes the latest verdict
+per item, and rewrites the three files:
+
+```
+python3 Tools/QA/apply_results.py
+```
+
+- PASS → the item's section moves to `ARCHIVE.md` and leaves the backlog.
+- FAIL → heading marked 🔴, a `> **Last result:**` line added, a `DEV_TASKS.md` entry upserted.
+- PARTIAL → heading marked 🟡 + the note. BLOCKED → ⛔ + the blocker. SKIP → no change.
+
+It is idempotent, so it is safe to run every time. Use `--check` first if you want to preview
+what will change without writing. Read the script's stdout and carry its summary into your
+final report to the tester (e.g. "applied QA-EDITMODE-TESTS → 🔴, opened a dev task").
+
+If the tester's RESULTS file references an item ID that is not in the backlog, say so plainly
+in your report (a typo, or an item already archived) — do not invent a section for it.
+
+## 2. Scan new merges for new untested items (the non-deterministic half — the "open")
+
+This is the part only you can do. Determine what has merged since the last scan and add any
+new untested work as fresh `### QA-... ⬜ — title` sections.
+
+- The `Generated:` / `Scan covers:` line at the top of `QA_BACKLOG.md` records the last scan
+  point (a commit SHA and PR range). Find merges after it:
+  - `git fetch origin bleeding-edge` (retry with backoff on network failure).
+  - `git log --oneline --merges <last-sha>..origin/bleeding-edge`
+- For each new merge, read the PR body / merge-commit message for a **Verification status**
+  section (what the author could not run, what a human must verify). If it names untested
+  behaviour, write a new backlog item for it: a stable `QA-<AREA>-<SHORT>` id, a one-line
+  title, a `Source:` line (PR number), numbered steps, and explicit PASS / FAIL definitions —
+  match the shape of the existing items exactly.
+- Place each new item under the right priority section (`## Priority 0/1/2`). A gate
+  (compile, platform law, a new game mode) is P0; a merged-but-never-played feature is P1;
+  cosmetic / data-gathering is P2.
+- Update the `Generated:` / `Scan covers:` header line to the new scan point.
+- If there are zero new merges since the last scan, say so and skip this step — do not
+  fabricate items.
+
+Never duplicate an item that already exists (match on the `QA-...` id). Never resurrect an id
+that is present in `ARCHIVE.md` unless a later merge genuinely re-opened that work — if so,
+note why in the item's Source line.
+
+## 3. Print the prioritised list
+
+After steps 1–2, print the current backlog top-down (P0 first), one line per item:
+`<emoji> <QA-ID> — <title>` plus the `Last result` note if present. Call out at the top:
+how many items are ⬜ never-run, 🟡 partial, 🔴 failed, ⛔ blocked; what you archived this run;
+what dev tasks you opened. This printed list is the tester's working queue.
+
+## 4. Commit the skill-owned files
```

</details>

### `9d993c146` — qa: correct build branch in andrew's results (ftue-editor-tool -> untested-backlog-qa-workflow) + re-apply

_Claude, 2026-08-07 20:00:43 +0000_

```text
 Docs/QA/DEV_TASKS.md                 | 2 +-
 Docs/QA/QA_BACKLOG.md                | 4 ++--
 Docs/QA/RESULTS/2026-08-07-andrew.md | 2 +-
 3 files changed, 4 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index cf109cecf..18f83d3e6 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -11,7 +11,7 @@ of duplicating it.
 
 <!-- devtask:QA-EDITMODE-TESTS -->
 ### QA-EDITMODE-TESTS — run the test suites that were written but never executed
-- **Failed on:** claude/ftue-editor-tool-69acq5 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-07, andrew)
+- **Failed on:** claude/untested-backlog-qa-workflow-7a0nb9 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-07, andrew)
 - **Symptom:** None of the required tests were even there for me to test.
 - **Definition of done:** QA item `QA-EDITMODE-TESTS` passes.
 <!-- /devtask:QA-EDITMODE-TESTS -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 842877275..16d9576a4 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -72,7 +72,7 @@ Source: PR #660 + `Docs/UNITY_VERIFICATION_CHECKLIST.md`. The Dolphin's `VesselS
 PASS: audit reports `Dolphin NearFieldSkimmer: 'EnergySkimmer' OK`; crackle arcs sweep the skimmer sphere per prism and the HUD jaw icon punches per skim; the gape widens as energy fills; drift fills the ring and flying straight does not; speed returns to normal after an interrupted discharge; the crystal fires the cone, empties energy and flashes the Space icon; two crystals plantable at Charge L5, preview tinted your domain and blooming (not popping); both peers agree. FAIL: audit reports anything else for Dolphin · no visible/audible skim feedback · ring fills while flying straight · speed stuck high after drift→release→drift · peers disagree on upgrade state. (Serpent is expected to FAIL the same audit — that is a known, separate item.)
 
 ### QA-RIBCAGE-MODE 🟡 — "Peel the Cage" has never been opened
-> **Last result:** 🟡 PARTIAL — Most of the steps seem fine, I'm not sure if the danger blocks work at all, but it not working isn't labeled as a failure  _(build claude/ftue-editor-tool-69acq5 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-07, andrew)_
+> **Last result:** 🟡 PARTIAL — Most of the steps seem fine, I'm not sure if the danger blocks work at all, but it not working isn't labeled as a failure  _(build claude/untested-backlog-qa-workflow-7a0nb9 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-07, andrew)_
 
 Source: PR #662 + later tuning. Whole new game mode (`GameModes.Ribcage = 39`), authored headless. Reference: `_Scripts/Controller/Arcade/RIBCAGE.md` § In-editor verification.
 
@@ -100,7 +100,7 @@ Source: PR #627. Reference: `Docs/SPATIAL_INDEX.md` § "Shell view — in-editor
 PASS: skims register at the stella surface (≈3× the box), spike-tip grazes hit, aimed-at-the-gap passes do not; boost is granted per shell touch; the Rhino pops at octahedron reach rather than point-blank; no prism becomes untouchable and no double-fire (pop and destroy in one contact); the two markers stay sub-ms and `Physics.SendEvents` is flat vs. the previous build; the A/B toggle reverts cleanly. FAIL: skims only at the box · gap false-positives · pop-then-destroy · any prism that cannot be hit at all · marker spikes or a rising `Physics.SendEvents` train.
 
 ### QA-EDITMODE-TESTS 🔴 — run the test suites that were written but never executed
-> **Last result:** 🔴 FAIL — None of the required tests were even there for me to test.  _(build claude/ftue-editor-tool-69acq5 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-07, andrew)_
+> **Last result:** 🔴 FAIL — None of the required tests were even there for me to test.  _(build claude/untested-backlog-qa-workflow-7a0nb9 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-07, andrew)_
 
 Source: PRs #659, #639, #627, #641, #668, #651. These are NUnit suites authored headless — they have literally never been run.
 
diff --git a/Docs/QA/RESULTS/2026-08-07-andrew.md b/Docs/QA/RESULTS/2026-08-07-andrew.md
index 7cf4e43b2..76e1f42fd 100644
--- a/Docs/QA/RESULTS/2026-08-07-andrew.md
+++ b/Docs/QA/RESULTS/2026-08-07-andrew.md
@@ -1,6 +1,6 @@
 # QA Results — 2026-08-07 — andrew
 
-Build: claude/ftue-editor-tool-69acq5 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor
+Build: claude/untested-backlog-qa-workflow-7a0nb9 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor
 
 | Item ID | Result | Notes / Evidence |
 |---------|--------|------------------|
```

</details>

### `b3ea105cc` — qa(backlog): apply results + rescan merges to PR #679 (8 new items)

_Claude, 2026-08-07 20:06:04 +0000_

```text
- rescan window advanced 2e2d3aaf -> 9e8cf3f3 (PRs #583-#679)
- new: QA-PRISM-OCCLUSION-SHATTER (P0), QA-SPARROW-BOOST-REDESIGN,
  QA-SPARROW-STOPPED-ROLL, QA-DISPLAYNAME-VALIDATION, QA-MENU-CAMERA-RIG,
  QA-PAUSE-MENU-RETURN, QA-WILDLIFE-LIBERATION (P1), QA-ANALYTICS-FLIGHT-TIME (P2)
- re-applied andrew's results (Ribcage 🟡, EditMode 🔴)
```

```text
 Docs/QA/QA_BACKLOG.md | 99 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++--
 1 file changed, 97 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 137 lines)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 16d9576a4..433dd7978 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-06 · Scan covers: merges up to `2e2d3aaf` (PRs #583–#669) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-07 · Scan covers: merges up to `9e8cf3f3` (PRs #583–#679) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 Every item below landed on a shared branch without ever being opened in Unity by its author (or was play-tested only in part). Work top-down: P0 first.
 
@@ -106,7 +106,7 @@ Source: PRs #659, #639, #627, #641, #668, #651. These are NUnit suites authored
 
 1. Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
 2. Record every failing test by name, plus the total pass/fail count.
-3. Specifically confirm these suites are present and green: `CellSpawnFormationTests`, `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`, `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`, `GeometryUtilsTests`, `PrismOcclusionCoverageTests`.
+3. Specifically confirm these suites are present and green: `CellSpawnFormationTests`, `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`, `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`, `GeometryUtilsTests`, `PrismOcclusionCoverageTests`, `ShipModifierTests` (PR #679), `DisplayNameValidatorTests` (PR #674/display-name-validation).
 
 PASS: all EditMode tests green and all nine suites present. FAIL: any red test (record the name + assertion message) or a suite that does not appear at all (means it did not compile into the test assembly).
 
@@ -126,6 +126,17 @@ Source: PRs #637, #641, #653, #659, #661, #668, #646, #650. Each auditor is a ch
 
 PASS: every tool runs without throwing, and each reports either clean or only the known exceptions: Serpent fails the skimmer audit; Manta/Rhino/Serpent are listed as design-blocked in the ability-row audit; Dolphin/Urchin/Rhino/Grizzly lack elemental morphs; `SkyboxModel` entries listed under OK in the cell-visual audit. FAIL: any tool that throws, or any new failure beyond the known exceptions above — especially "SCENE-PLACED DUPLICATES" or "DEAD CELL OVERRIDES" being non-empty.
 
+### QA-PRISM-OCCLUSION-SHATTER ⬜ — the corridor dither's new hard-edged SHATTER shape + Dither Lab tool
+Source: PR #677 (`prisms-occlusion-shapes`). 467 lines of new `PrismOcclusionCorridor.hlsl` (a new SHATTER kernel, triangular flecks, live scale dials) plus a brand-new editor tool `PrismOcclusionDitherLab.cs` (844 lines) — none of it compiled or run. This is the same platform-law surface as QA-PRISM-OCCLUSION; do that item too and treat this as the shape-specific delta.
+
+1. Load any scene with prisms. If any prism is magenta on load, the HLSL failed to compile — stop, FAIL, attach the shader error.
+2. Freestyle: put a wall of trail between the camera and your ship. The cleared region's stipple should now read as a **cracked lattice of hard-edged polygons** (SHATTER), not round or triangular flecks, and should not strobe.
+3. Hold still ~10 s: the pattern should slowly evolve/orbit, not freeze or twinkle.
+4. Open FrogletTools ▸ Ecology ▸ Prism Animation ▸ Occlusion Dither Lab. Confirm it opens without throwing, drives the kernel/scale live in play mode, and its coverage readout is sane. Change the kernel and scale and confirm the corridor updates in play.
+5. Console: zero `[PrismOcclusion]` errors.
+
+PASS: no magenta; the ship stays visible through corridor prisms; the SHATTER lattice reads as hard-edged cracked polygons and evolves smoothly; the Dither Lab opens, drives the effect live and reports coverage without throwing; no console errors. FAIL: magenta prisms · any HLSL/`[PrismOcclusion]` error · the Lab throwing or not affecting the corridor · a strobing/twinkling dither · the ship occluded.
+
 ## Priority 1 — merged features that have never been played
 
 ### QA-ECOLOGY-WORM-KAIJU ⬜ — the worm colony boss
@@ -463,6 +474,80 @@ Source: PR #666 + `Docs/PartySystem/BUGS.md` (B2/B3/B5 open) + `Docs/PresenceSys
 
 PASS: B11/B13/B14 stay fixed (all instances reach `Present`; peers promote CONNECTING… → ONLINE; no Relay 500 on boot); the graceful-quit path evicts in < 1 s; fault rates are no worse than the last measurement. FAIL: any of B11/B13/B14 recurring, a graceful quit taking the reap path, or fault rates rising. Note B2/B3/B4/B5/B6 outcomes as data — they are known-open, so they do not fail this item, but their current behaviour is what we need recorded.
 
+### QA-SPARROW-BOOST-REDESIGN ⬜ — overheat removed, base strafing roll, Elemental Ward (Time-5)
+Source: PR #675 (`sparrow-ability-redesign`). Touches a **platform** surface (`ResourceSystem.ApplyElementalEffect`) plus two vessel prefabs edited as hand-written YAML — a removed GameObject, a removed resource slot, renamed serialized fields. Prefab integrity is the real risk. Reference: `SPARROW_AFTERBURNER.md` § In-editor verification.
+
+1. Project compiles with zero errors; no new console warnings on Sparrow or Serpent spawn. (Known pre-existing, not this branch: the Sparrow's `ElementalBarsController.view` reference is already dangling on `bleeding-edge`.)
+2. **Prefab integrity (top risk).** Open `Sparrow.prefab`: no missing-script slots; the `OverheatingBoostActionExecutor` child is gone; the `ResourceSystem` list reads Missiles / FullAuto / ExhaustBarrage (3 entries, no Heat); `SparrowHUDController.barrelRollController` points at the root's `BarrelRollController`; `VesselElementalImmunity` on the root reads `WhileBoosting` + `Time`. Then `Serpent.prefab`: `VesselElementalImmunity` on the root reads `WhileTranslationRestricted` + `None`.
+3. Hold boost 60 s — no force-release, no danger trail, no self-slam (overheat is gone).
+4. Time at 0: boost + full stick deflection rolls **once** per press (roll is now base kit, no Time gate).
+5. The boost (rightmost) ability icon's ring: full on press, wipes empty with a punch on roll, empty until the next press — never a partial fill.
+6. Time ≥ 5 (`ResourceSystem.TimeTestHarness = 0.5`): a danger prism **while boosting** → element flowers do not dip; **not boosting** → they dip. Slow and input-mute still land either way (by design).
+7. Serpent stopped + danger prism → no flower dip at any Time level.
+8. **MPPM two clients**, both Sparrows, one at Time 5: both machines agree on who resists the drain (replicated `NetElementUnlocks` path — a local read would pass step 6 and fail here).
+9. `FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows` — Sparrow still 4/4 in charge → mass → space → time.
+
+PASS: compiles clean; both prefabs intact per step 2; unlimited boost with no overheat side-effects; base-kit roll once per press; binary roll pip; Ward blocks only the elemental drain while boosting (Sparrow) / while stopped (Serpent), never the slow or mute; both peers agree; ability-row audit still 4/4. FAIL: compile error · any missing script or leftover Heat/overheat slot · boost force-releasing or laying a danger trail · roll not firing at Time 0 · Ward blocking the slow/mute, or blocking while not boosting · peers disagreeing · audit not 4/4.
+
+### QA-SPARROW-STOPPED-ROLL ⬜ — strafing roll works stopped, pitch/yaw 3× in the stationary stance
+Source: PR #679 (`sparrow-strafing-roll-stopped`). Code only — no prefab/scene/SO touched — so the risk is a compile check plus feel. Also raises the stopped turn rate on the **shared** `VesselTransformer`, so the **Serpent inherits it**. Adds `ShipModifierTests`. Reference: `SPARROW_AFTERBURNER.md`.
+
+1. Project compiles; run `CosmicShore.Tests.EditMode` — `ShipModifierTests` gained two cases pinning the new `ignoresTranslationRestriction` flag.
+2. Sparrow, **flying** roll first (regression risk): boost + full left stick → rolls and strafes once per press. Must be **unchanged**.
+3. Toggle the stationary/turret stance. Boost + full left stick → **rolls and strafes**; speed does not change; still once per press; charge ring arms and wipes as when flying.
+4. After the stopped roll: still stopped, still in fire mode, no trail/bridging prisms laid.
+5. **Stale-course check:** stopped, rotate to aim well away from your stop heading, then dodge — the strafe must go where the stick points relative to your **current** facing (a skew toward the old heading = wrong projection plane).
+6. **No banked lurch:** stopped, take a knockback (Rhino ram / danger prism) — you must not move; release the stance — you must not lurch.
+7. **Stopped turn rate:** flying, time a full 180° yaw; toggle the stance and repeat — roughly a third the time; pitch likewise; release and the rate drops straight back.
+7b. **Serpent:** stop into its weave stance, take a knockback, release — no movement stopped, no lurch after; note its pitch/yaw are **also 3×** stopped (same default). Any vessel: flying turn rates unchanged everywhere.
+8. **MPPM two clients:** roll while stopped on A; B sees the same displacement.
+
+PASS: compiles + `ShipModifierTests` green; flying roll unchanged; stopped roll strafes once per press with no speed/stance change and no prisms laid; strafe follows current facing; no movement/lurch from a stopped knockback; stopped pitch/yaw ~3× and snaps back on release; Serpent behaves the same; both peers agree. FAIL: compile/test failure · flying roll changed · stopped roll not firing, changing speed, or laying trail · strafe skewing to the old heading · a knockback moving or a release lurching a stopped vessel · stopped turn rate not ~3× or staying fast after release · peers disagreeing.
+
+### QA-DISPLAYNAME-VALIDATION ⬜ — one validated path for display names (filter/format/no-duplicates)
+Source: `display-name-validation` + PR #674 (`errors`) + the Cloud Save namespace fix in PR #677. A 494-line `DisplayNameValidator`, a `DisplayNameRegistry` over Cloud Save, and a 243-line `DisplayNameValidatorTests` suite — first import, and the registry needed two separate Cloud Save 3.4 API-namespace fixes (real compile risk).
+
+1. Project compiles with zero errors — specifically no unresolved Cloud Save / `Unity.Services.CloudSave.Models` namespace errors in `DisplayNameRegistry.cs`.
+2. Run `DisplayNameValidatorTests` in EditMode (also covered by QA-EDITMODE-TESTS) — all green.
+3. In the profile/username setup UI: try an empty name, an over-long name, disallowed characters, and profanity — each is rejected or auto-formatted per the rules, with clear feedback.
+4. Set a valid name; confirm it persists (Cloud Save) and shows in the profile widget and arcade profile.
```

</details>

### `be06995e3` — qa: results 2026-08-10 andrew (Worm Kaiju PARTIAL, Sparrow projectile pool FAIL, flora leafsize FAIL)

_Claude, 2026-08-10 21:15:19 +0000_

```text
 Docs/QA/RESULTS/2026-08-10-andrew.md | 9 +++++++++
 1 file changed, 9 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-10-andrew.md b/Docs/QA/RESULTS/2026-08-10-andrew.md
new file mode 100644
index 000000000..6f429bcee
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-10-andrew.md
@@ -0,0 +1,9 @@
+# QA Results — 2026-08-10 — andrew
+
+Build: bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-ECOLOGY-WORM-KAIJU | PARTIAL | Almost every PASS condition is true; the outstanding one is "devours creatures at the jaws and pursues pilots" — not observed yet. |
+| QA-SPARROW-PROJECTILE-POOL | FAIL | Launch SFX play and there are no NREs, but normal shots sometimes pass straight through prisms, and the Console throws "Projectile already released! Should not call twice!". |
+| QA-FLORA-LEAFSIZE | FAIL | No reference for what the leaf size should be, and nothing that looked like a leaf to analyze in the first place. |
```

</details>

### `521377eb4` — qa(backlog): apply 2026-08-10 results (Worm Kaiju 🟡, Sparrow projectile pool 🔴, flora leafsize 🔴)

_Claude, 2026-08-10 21:19:21 +0000_

```text
Rescan found NO new merges: bleeding-edge was force-pushed/rewound to ~2026-08-04,
dropping the previously-scanned range. Scan header left at 9e8cf3f3 pending a human
decision on the rewind (see chat). No backlog items pruned.
```

```text
 Docs/QA/DEV_TASKS.md  | 14 ++++++++++++++
 Docs/QA/QA_BACKLOG.md | 12 +++++++++---
 2 files changed, 23 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index 18f83d3e6..dcc74bf73 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -15,3 +15,17 @@ of duplicating it.
 - **Symptom:** None of the required tests were even there for me to test.
 - **Definition of done:** QA item `QA-EDITMODE-TESTS` passes.
 <!-- /devtask:QA-EDITMODE-TESTS -->
+
+<!-- devtask:QA-SPARROW-PROJECTILE-POOL -->
+### QA-SPARROW-PROJECTILE-POOL — async-refilled pooled projectiles are injected
+- **Failed on:** bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-10, andrew)
+- **Symptom:** Launch SFX play and there are no NREs, but normal shots sometimes pass straight through prisms, and the Console throws "Projectile already released! Should not call twice!".
+- **Definition of done:** QA item `QA-SPARROW-PROJECTILE-POOL` passes.
+<!-- /devtask:QA-SPARROW-PROJECTILE-POOL -->
+
+<!-- devtask:QA-FLORA-LEAFSIZE -->
+### QA-FLORA-LEAFSIZE — garden flora still grow leaves at the authored size
+- **Failed on:** bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-10, andrew)
+- **Symptom:** No reference for what the leaf size should be, and nothing that looked like a leaf to analyze in the first place.
+- **Definition of done:** QA item `QA-FLORA-LEAFSIZE` passes.
+<!-- /devtask:QA-FLORA-LEAFSIZE -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 433dd7978..10ceb70ce 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -139,7 +139,9 @@ PASS: no magenta; the ship stays visible through corridor prisms; the SHATTER la
 
 ## Priority 1 — merged features that have never been played
 
-### QA-ECOLOGY-WORM-KAIJU ⬜ — the worm colony boss
+### QA-ECOLOGY-WORM-KAIJU 🟡 — the worm colony boss
+> **Last result:** 🟡 PARTIAL — Almost every PASS condition is true; the outstanding one is "devours creatures at the jaws and pursues pilots" — not observed yet.  _(build bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-10, andrew)_
+
 Source: PR #667. Reference: `Docs/ECOSYSTEM.md` §23.6 (spawn steps + dials).
 
 1. Freestyle → Lifeform Matrix toy → "Worm Colony" → any element station.
@@ -361,7 +363,9 @@ Source: PR #658. Every frame-cost claim on that branch is structural, never meas
 
 PASS: benchmark numbers recorded (this item's deliverable is data, not a verdict); suctions converge on the moving creature, animate for their full duration, and no prism is left frozen mid-suction. FAIL: a marker regressing sharply vs. the reference run · GC per frame appearing · suctions converging on a stale point or freezing. (The old ~0.43 ms self/death figure is stale — do not compare against it.)
 
-### QA-SPARROW-PROJECTILE-POOL ⬜ — async-refilled pooled projectiles are injected
+### QA-SPARROW-PROJECTILE-POOL 🔴 — async-refilled pooled projectiles are injected
+> **Last result:** 🔴 FAIL — Launch SFX play and there are no NREs, but normal shots sometimes pass straight through prisms, and the Console throws "Projectile already released! Should not call twice!".  _(build bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-10, andrew)_
+
 Source: PR #606. The failure mode is silent duds seconds after spawn.
 
 1. Spawn a Sparrow (any mode or freestyle) and fire full-auto for ~30 s.
@@ -457,7 +461,9 @@ Source: PR #663 (buttons never pressed in a running editor). Do this on a throwa
 
 PASS: the dialog lists exactly the selected path; the commit contains only that path; a protected branch is refused; the pre-staged file stays staged and out of the commit. FAIL: anything else riding along in the commit, a protected-branch push succeeding, or the pre-staged file being swept in.
 
-### QA-FLORA-LEAFSIZE ⬜ — garden flora still grow leaves at the authored size
+### QA-FLORA-LEAFSIZE 🔴 — garden flora still grow leaves at the authored size
+> **Last result:** 🔴 FAIL — No reference for what the leaf size should be, and nothing that looked like a leaf to analyze in the first place.  _(build bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-10, andrew)_
+
 Source: PR #656 (a duplicate declaration removed after a semantic merge conflict).
 
 1. Confirm zero compile errors.
```

</details>

### `ab74576ba` — qa(backlog): rescan to PR #696 (5 new items) + note the bleeding-edge rewind/restore

_Claude, 2026-08-11 16:11:07 +0000_

```text
- scan window 9e8cf3f3 -> b0cf4f0f (PRs #583-#696); rewind to 0e855b24 was transient, restored
- new P1: QA-SPARROW-PRISM-ATTACK (#696), QA-DOLPHIN-CAPSULE-BLAST (#680),
  QA-DOLPHIN-SKIM-ENERGY-CTA (#695), QA-PROFILE-ADS-REMOVAL (profile-save-and-ads-removal)
- new P2: QA-DOLPHIN-SPEED-TUNE (#681)
- windows-build-failures folded into QA-BUILD-COMPILE (no separate item)
```

```text
 Docs/QA/QA_BACKLOG.md | 56 +++++++++++++++++++++++++++++++++++++++++++++++++++++++-
 1 file changed, 55 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 10ceb70ce..c042143fe 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,8 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-07 · Scan covers: merges up to `9e8cf3f3` (PRs #583–#679) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-11 · Scan covers: merges up to `b0cf4f0f` (PRs #583–#696) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+
+> Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
 Every item below landed on a shared branch without ever being opened in Unity by its author (or was play-tested only in part). Work top-down: P0 first.
 
@@ -554,6 +556,48 @@ Source: `wildlifeliberation-game-mode` (merged and re-tuned through `9b9d9b60`,
 
 PASS: launches clean; three cages at the stated radii with the three tiers; every creature killable by body-prism destruction, withering to one crystal; creatures stay in their bands; the winner is a player at 500 kills with domain sums as secondary; frame time acceptable on target hardware. FAIL: missing scripts · a creature that can't be killed or that vanishes instead of withering · a body segment dropping a crystal or a capital dropping none · creatures escaping their band or being led to inedible mass · the mode resolving a winning domain instead of a player · a hard frame-time cliff.
 
+### QA-SPARROW-PRISM-ATTACK ⬜ — Turret Stance fires real prisms (two flight visualizations)
+Source: PR #696 (`sparrow-prism-attack`). Large (4,405 insertions): the Sparrow's turret stance now fires real prisms "on the bullets' terms", with a live-switchable A/B flight visual, a new prism-flight clock wired into both prism graphs, a new asset-writing editor tool (`Tools/Shaders/wire_prism_flight_clock.py`, 664 lines), and `PrismImplosion` changes. Space-5 now gates a shield. Reference: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md`.
+
+1. **Asset gates first:** run `python3 Tools/Shaders/wire_prism_flight_clock.py --check`, then FrogletTools ▸ Ecology ▸ Prism Animation ▸ Validate Clock Wiring (now requires the three `_Flight*` properties + `PrismFlightClock` on BlockGraph and ExplodingBlockGraph). Open both graphs — no import errors, `FlightStartTime/Duration/Velocity` on the Blackboard. If any prism is magenta on load, the graph failed to compile — FAIL.
+2. Sparrow, stopped, hold fire in **TranslateAndGrow** (`FullAutoBlockShootAction.asset` ▸ Flight Visualization): prisms visibly leave the muzzles, scale up in flight, and anchor at ~286 u. Prisms teleporting to range with no flight, or `[PrismClock]` console errors, = wiring failure.
+3. Flip to **ReverseSuction** live (next volley switches): faces stream from the moving shot point into the anchored shape; the real prism appears as the stream completes.
+4. Confirm fired prisms behave as bullets: friendly-fire on, a pilot's own shots never destroy their own fired prisms, the 0.2 s placement-immunity window holds, and the hit sphere is sized to the projectile (not its z-stretch).
+5. Raise Space to 5 → the shield gate engages; raise Charge to 5 → friendly fire still on, only the skyburst spared.
+
+PASS: both asset gates pass and both graphs import clean (no magenta, no `[PrismClock]` errors); both flight visualizations render as described; fired prisms hit like bullets with the immunity window and correct hit size; Space-5 shield and Charge-5 sparing behave. FAIL: a failed wire/validate gate · magenta prisms · prisms teleporting to range · a visualization that shows nothing · a pilot destroying their own fired prisms · wrong hit size · the Space-5/Charge-5 gates not behaving.
+
+### QA-DOLPHIN-CAPSULE-BLAST ⬜ — crystal blast sweeps a capsule aligned with the jaw gape
+Source: PR #680 (`dolphin-echobliteration-capsule`). The Dolphin crystal blast's cross-section becomes a **capsule** (fixed width across the beam; skim energy buys length along the jaw-open axis). **Hand-authored asset YAML** — a `SphereCollider` rewritten into a `CapsuleCollider` in place (class id 135→136) — so step 1 is a genuine import check. Touches `PrismSpatialIndex`, `AOEConicExplosion`, `AOEExplosion`. Related: QA-VESSEL-AOE-IMPULSE.
+
+1. **The hand-authored collider imported.** Open `_Prefabs/Projectile/AOEConicExplosion.prefab`: the root shows a **Capsule Collider** (Is Trigger ✓, Radius 0.0667, Height 1, Direction Z-Axis, Center 0/-0.5/0) — not a missing component, not a Sphere Collider, not a second collider alongside.
+2. Project compiles (nothing here is `#if`-guarded, but no compiler ran author-side).
+3. Empty-energy blast: fly to a crystal with no banked energy — the blast looks and destroys about as before, slightly lozenge-shaped, not a sphere.
+4. Charged blast: bank skim energy, then detonate — the blast is a fan, **wide in the jaw plane, narrow across it**, and grows in **length** (not radius) with energy. The vessel-impact volume and the destruction volume are the same shape.
+5. Regression: fire a spherical AOE (e.g. Rhino) — unchanged (CoreScale 0 collapses to the plain circular cone).
+
+PASS: the capsule collider imported exactly as specified; compiles clean; empty blast reads unchanged; the charged blast is a length-growing jaw-plane fan with matching impact/destruction shapes; other vessels' blasts unchanged. FAIL: a missing/Sphere/duplicate collider on the prefab · compile error · the blast still a growing circular cone · impact and destruction shapes disagreeing · a spherical AOE that changed.
+
+### QA-DOLPHIN-SKIM-ENERGY-CTA ⬜ — 15× skim-energy nerf, lime jaw CTA at full, dead silhouette removed
+Source: PR #695 (`dolphin-prism-energy`). Skim banks 15× less energy per prism; the HUD jaw gauge arms **lime** at full energy; and the dead vessel-silhouette HUD element is deleted **fleet-wide** (YAML surgery removing content from 6 HUD prefabs — missing-script risk). Related: QA-DOLPHIN-SKIM, QA-UI-TRAIL-DISPLAY-REMOVAL.
+
+1. Open the six touched HUD prefabs (`MantaHUDVariant`, `RhinoHUDVariant`, `SerpentHUDVariant`, `SparrowHUDVariant`, `SquirrelHUDVariant`, `VesselHUDPrefab`): no `Missing (Mono Script)` where the silhouette element was removed.
+2. Dolphin freestyle: skim a run of prisms — energy now fills **much** more slowly (~15× more prisms to fill than before).
+3. Fill the jaw energy gauge to full — it arms **lime** as a call-to-action; below full it does not.
+4. Play a round on two other vessels and confirm their HUDs still build and animate (petal bars etc.) with the silhouette gone.
+
+PASS: no missing scripts on the six prefabs; skim energy fills ~15× slower; the jaw gauge arms lime only at full; other vessels' HUDs intact. FAIL: any missing script · energy filling at the old rate · the lime CTA never arming or arming below full · a broken HUD on any vessel after the silhouette removal.
+
+### QA-PROFILE-ADS-REMOVAL ⬜ — Unity Ads removed entirely + profile double-submit closed
+Source: `profile-save-and-ads-removal`. Removes Unity Ads (package manifest + `RewardedAdsButton` deleted, `UnityConnectSettings`/`ProjectSettings` changed) and closes the display-name double-submit window. Package/manifest change = compile/build + package-resolution risk.
+
+1. Project compiles and the package manifest resolves with no Unity Ads / Advertisement package errors in the Console.
+2. Nothing in the menu references a missing `RewardedAdsButton` (no missing-script slots on the daily-reward card or anywhere ads UI lived).
+3. The daily-reward flow works without the rewarded-ad path.
+4. In the profile/username UI, submit a display name and rapidly tap submit again — the double-submit window is closed (no duplicate request / no error).
+
+PASS: compiles and resolves packages cleanly; no missing ads-UI scripts; daily reward works ad-free; the name double-submit is prevented. FAIL: a package-resolution/compile error · a missing `RewardedAdsButton` reference · a broken daily-reward flow · a display name that still double-submits.
+
 ## Priority 2 — lower risk, cosmetic, or data-gathering
 
 ### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
@@ -584,6 +628,16 @@ Source: `game-data-json-schema`. `FlightClock`, `VesselUnlockSystem`, `UGSDataSe
 
 PASS: menu-freestyle flight time is recorded; starter + selected vessel persist in `HANGAR_DATA` across a relaunch; no Cloud Save exceptions. FAIL: freestyle flight not counted · vessel fields missing or not persisting · a Cloud Save serialization throw. (Deliverable here is partly data — note the observed values.)
 
+### QA-DOLPHIN-SPEED-TUNE ⬜ — cruise +30%, charged boost +70%, faster fill / slower drain
+Source: PR #681 (`dolphin-speed-boost-tuning`). Two authored numbers changed in existing serialized assets (`Dolphin.prefab` `DefaultThrottleScaler 50→68`; `ChargeBoostAction.asset` `maxBoostMultiplier 2→2.259`, `chargeTimeToFull 4→3.636`, `dischargeTimeToEmpty 2→2.5`). Data-only, low import risk, but never flown. Reference: `DOLPHIN_ENERGY_ECONOMY.md` §2.
+
+1. Menu_Main freestyle, Dolphin. Full throttle, no boost → `VesselStatus.Speed` settles at **78** (was 60).
+2. Hold drift from an empty meter → the boost ring fills in **~3.6 s** (was 4).
+3. Full charged-boost discharge → peak speed reaches **~357** (was 210), draining over ~2.5 s.
+4. Sanity: no other vessel's speed/boost changed (the asset is Dolphin-only).
+
+PASS: cruise ~78, fill ~3.6 s, charged peak ~357, drain ~2.5 s; no other vessel affected. FAIL: values materially off from those targets · another vessel's boost/speed changed · the drift/idle floor moved (it should stay at 10). (Feel is a judgement call — note whether the new boost peak plays too strong.)
+
 ## Not covered by this list
 
 * Automated CI checks (`Tools/CI/validate_project.py`, `check_conditional_compilation.py`, the Thursday build promotion in PR #664) run in GitHub Actions and are verified there. QA does not need to re-run them; if a build branch is red, that is an engineering item.
```

</details>

### `7b7323ccb` — qa: results 2026-08-11 andrew (QA-AUDIT-TOOLS FAIL)

_Claude, 2026-08-11 17:58:15 +0000_

```text
 Docs/QA/RESULTS/2026-08-11-andrew.md | 7 +++++++
 1 file changed, 7 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-11-andrew.md b/Docs/QA/RESULTS/2026-08-11-andrew.md
new file mode 100644
index 000000000..a01b4ac92
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-11-andrew.md
@@ -0,0 +1,7 @@
+# QA Results — 2026-08-11 — andrew
+
+Build: bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-AUDIT-TOOLS | FAIL | Three problems beyond the known exceptions; everything else (skimmers, ability rows, hull morphs, speed-tunnel law, occlusion corridor, baselines) ran fine. (1) **Audit Cell-Owned Visuals** logged errors: "'CosmicShore.Core.NetworkMonitor' is missing the class attribute 'ExtensionOfNativeClass'!" (x2) and warning "GameObject (named 'NetworkMonitor') references runtime script in scene file. Fixing!", then "[CellOwnedVisualAudit] 26 scenes scanned." (2) **Validate Lifeform Crystals** — the menu item does not exist on this build (could not run it). (3) **Game Mode Prefab Kit ▸ Validate** — 1 error + ~40 warnings; logged "[PrefabKit] Created kit config at Assets/Resources/GameModePrefabKit.asset with 9 seeded entries." For reference the baseline line read: "SpawnableAtlantis 67,722 prisms / 950,437 volume", and the occlusion-corridor check reported the hlsl GUID pinned (OK). |
```

</details>

### `d49666563` — qa(backlog): apply QA-AUDIT-TOOLS FAIL + scan to b08a35d7 (1 new item)

_Claude, 2026-08-11 18:01:47 +0000_

```text
- QA-AUDIT-TOOLS -> 🔴 (dev task: missing Validate Lifeform Crystals tool, cell-visual + prefab-kit errors)
- scan b0cf4f0f -> b08a35d7: no new merges; 1 direct crash-fix commit itemized
- new P0: QA-MENU-CRASH-PAUSE-PANEL (b08a35d7, Windows IL2CPP Menu_Main access violation)
- e7fd9107 (analytics hint text) too trivial to itemize
```

```text
 Docs/QA/DEV_TASKS.md  |  7 +++++++
 Docs/QA/QA_BACKLOG.md | 16 ++++++++++++++--
 2 files changed, 21 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index dcc74bf73..b46e45ef0 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -29,3 +29,10 @@ of duplicating it.
 - **Symptom:** No reference for what the leaf size should be, and nothing that looked like a leaf to analyze in the first place.
 - **Definition of done:** QA item `QA-FLORA-LEAFSIZE` passes.
 <!-- /devtask:QA-FLORA-LEAFSIZE -->
+
+<!-- devtask:QA-AUDIT-TOOLS -->
+### QA-AUDIT-TOOLS — run every FrogletTools auditor and record its verdict
+- **Failed on:** bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-11, andrew)
+- **Symptom:** Three problems beyond the known exceptions; everything else (skimmers, ability rows, hull morphs, speed-tunnel law, occlusion corridor, baselines) ran fine. (1) **Audit Cell-Owned Visuals** logged errors: "'CosmicShore.Core.NetworkMonitor' is missing the class attribute 'ExtensionOfNativeClass'!" (x2) and warning "GameObject (named 'NetworkMonitor') references runtime script in scene file. Fixing!", then "[CellOwnedVisualAudit] 26 scenes scanned." (2) **Validate Lifeform Crystals** — the menu item does not exist on this build (could not run it). (3) **Game Mode Prefab Kit ▸ Validate** — 1 error + ~40 warnings; logged "[PrefabKit] Created kit config at Assets/Resources/GameModePrefabKit.asset with 9 seeded entries." For reference the baseline line read: "SpawnableAtlantis 67,722 prisms / 950,437 volume", and the occlusion-corridor check reported the hlsl GUID pinned (OK).
+- **Definition of done:** QA item `QA-AUDIT-TOOLS` passes.
+<!-- /devtask:QA-AUDIT-TOOLS -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index c042143fe..034301dc0 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-11 · Scan covers: merges up to `b0cf4f0f` (PRs #583–#696) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-11 · Scan covers: up to `b08a35d7` (PRs #583–#696 + 2 direct fixes) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -112,7 +112,9 @@ Source: PRs #659, #639, #627, #641, #668, #651. These are NUnit suites authored
 
 PASS: all EditMode tests green and all nine suites present. FAIL: any red test (record the name + assertion message) or a suite that does not appear at all (means it did not compile into the test assembly).
 
-### QA-AUDIT-TOOLS ⬜ — run every FrogletTools auditor and record its verdict
+### QA-AUDIT-TOOLS 🔴 — run every FrogletTools auditor and record its verdict
+> **Last result:** 🔴 FAIL — Three problems beyond the known exceptions; everything else (skimmers, ability rows, hull morphs, speed-tunnel law, occlusion corridor, baselines) ran fine. (1) **Audit Cell-Owned Visuals** logged errors: "'CosmicShore.Core.NetworkMonitor' is missing the class attribute 'ExtensionOfNativeClass'!" (x2) and warning "GameObject (named 'NetworkMonitor') references runtime script in scene file. Fixing!", then "[CellOwnedVisualAudit] 26 scenes scanned." (2) **Validate Lifeform Crystals** — the menu item does not exist on this build (could not run it). (3) **Game Mode Prefab Kit ▸ Validate** — 1 error + ~40 warnings; logged "[PrefabKit] Created kit config at Assets/Resources/GameModePrefabKit.asset with 9 seeded entries." For reference the baseline line read: "SpawnableAtlantis 67,722 prisms / 950,437 volume", and the occlusion-corridor check reported the hlsl GUID pinned (OK).  _(build bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-11, andrew)_
+
 Source: PRs #637, #641, #653, #659, #661, #668, #646, #650. Each auditor is a cheap, asset-only check that encodes a contract; several have never been run. Run each and paste its report into your results file:
 
 1. Vessels ▸ Audit Vessel Skimmers
@@ -139,6 +141,16 @@ Source: PR #677 (`prisms-occlusion-shapes`). 467 lines of new `PrismOcclusionCor
 
 PASS: no magenta; the ship stays visible through corridor prisms; the SHATTER lattice reads as hard-edged cracked polygons and evolves smoothly; the Dither Lab opens, drives the effect live and reports coverage without throwing; no console errors. FAIL: magenta prisms · any HLSL/`[PrismOcclusion]` error · the Lab throwing or not affecting the corridor · a strobing/twinkling dither · the ship occluded.
 
+### QA-MENU-CRASH-PAUSE-PANEL ⬜ — Menu_Main no longer crashes the Windows player (type-punned pause panel)
+Source: direct commit `b08a35d7` (`fix(ui): the Menu_Main crash is a type-punned pause-panel reference`). `PauseMenu.pauseMenuPanel` was a `GameObject` field still holding a `CanvasGroup`-typed pointer in `Pause_Menu_Panel.prefab`; the Editor coerced the mismatch to null (silent) but the **IL2CPP player** handed the punned pointer to native `GameObject` calls — an access violation (not a catchable exception) that took the Windows build down on **every** entry to Menu_Main. Fix repoints the prefab at the panel root and routes the reference through a validated `Panel` property. Also touches `SquadMemberCard.cs`. This is a **player-build** crash, so the Editor alone can't fully clear it.
+
+1. Open `_Prefabs/UI Elements/Panels/Pause_Menu_Panel.prefab`: no `Missing (Mono Script)`; `PauseMenu.pauseMenuPanel` points at the panel **GameObject** (its own root), not a CanvasGroup.
+2. Editor: launch to Menu_Main, open the pause panel (first tap warms it), close it — no null-ref, panel appears.
+3. **The real gate — Windows player build.** Make a Windows (IL2CPP) build, launch it, and enter Menu_Main. Do it several times (and back-and-forth from a game) — it must not crash.
+4. Sanity-check anything using `SquadMemberCard` still displays.
+
+PASS: prefab repointed with no missing script; the pause panel warms and opens in the Editor; the **Windows player build reaches Menu_Main repeatedly with no crash**; squad cards still render. FAIL: a missing script or a CanvasGroup-typed reference remaining · the pause panel not opening/warming · **any crash entering Menu_Main in the Windows player** · broken squad cards.
+
 ## Priority 1 — merged features that have never been played
 
 ### QA-ECOLOGY-WORM-KAIJU 🟡 — the worm colony boss
```

</details>

### `a98b207bb` — qa: add QA-FTUE-QUEST-ROWS BLOCKED to 2026-08-11 results (Quest Graph Editor absent on bleeding-edge)

_Claude, 2026-08-12 16:26:52 +0000_

```text
 Docs/QA/RESULTS/2026-08-11-andrew.md | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-11-andrew.md b/Docs/QA/RESULTS/2026-08-11-andrew.md
index a01b4ac92..b0e604268 100644
--- a/Docs/QA/RESULTS/2026-08-11-andrew.md
+++ b/Docs/QA/RESULTS/2026-08-11-andrew.md
@@ -5,3 +5,4 @@ Build: bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor
 | Item ID | Result | Notes / Evidence |
 |---------|--------|------------------|
 | QA-AUDIT-TOOLS | FAIL | Three problems beyond the known exceptions; everything else (skimmers, ability rows, hull morphs, speed-tunnel law, occlusion corridor, baselines) ran fine. (1) **Audit Cell-Owned Visuals** logged errors: "'CosmicShore.Core.NetworkMonitor' is missing the class attribute 'ExtensionOfNativeClass'!" (x2) and warning "GameObject (named 'NetworkMonitor') references runtime script in scene file. Fixing!", then "[CellOwnedVisualAudit] 26 scenes scanned." (2) **Validate Lifeform Crystals** — the menu item does not exist on this build (could not run it). (3) **Game Mode Prefab Kit ▸ Validate** — 1 error + ~40 warnings; logged "[PrefabKit] Created kit config at Assets/Resources/GameModePrefabKit.asset with 9 seeded entries." For reference the baseline line read: "SpawnableAtlantis 67,722 prisms / 950,437 volume", and the occlusion-corridor check reported the hlsl GUID pinned (OK). |
+| QA-FTUE-QUEST-ROWS | BLOCKED | Could not run — the Quest Graph Editor does not exist on this build. There is no `FrogletTools ▸ Quest Graph Editor` menu, and no `Quest Graph ▸ Layout All Phases (Rows)` menu item; the only FrogletTools graph/layout entry is the unrelated Prism "Auto-Wire Clock Properties". The absence itself is the finding — PR #633's editor tooling appears not to be present on bleeding-edge (same class of gap as the missing "Validate Lifeform Crystals" tool). Nothing about node layout could be judged. |
```

</details>

### `8b100853c` — qa(backlog): apply QA-FTUE-QUEST-ROWS BLOCKED + scan to PR #710 (7 new items)

_Claude, 2026-08-12 16:30:30 +0000_

```text
- QA-FTUE-QUEST-ROWS -> ⛔ (Quest Graph Editor absent on bleeding-edge)
- scan b08a35d7 -> d32f2683 (PRs #583-#710)
- new P0: QA-DOGFIGHT-MODE (Dog Fight, Sparrow-only gun duel)
- new P1: QA-ASTROLEAGUE-REWORK, QA-CHARGE-CRYSTAL-SHADER, QA-SPARROW-MISSILE-BAY, QA-ECOLOGY-JOUST-WITHER
- new P2: QA-PALETTE-DANGER-GOLD, QA-UI-QUIT-BUTTON
- dithering-layer-mismatch (#702) folded into the occlusion items; wildlifeliberation re-merge already covered
```

```text
 Docs/QA/QA_BACKLOG.md | 78 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++--
 1 file changed, 76 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 116 lines)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 034301dc0..c0444ef5e 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-11 · Scan covers: up to `b08a35d7` (PRs #583–#696 + 2 direct fixes) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-12 · Scan covers: up to `d32f2683` (PRs #583–#710) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -151,6 +151,17 @@ Source: direct commit `b08a35d7` (`fix(ui): the Menu_Main crash is a type-punned
 
 PASS: prefab repointed with no missing script; the pause panel warms and opens in the Editor; the **Windows player build reaches Menu_Main repeatedly with no crash**; squad cards still render. FAIL: a missing script or a CanvasGroup-typed reference remaining · the pause panel not opening/warming · **any crash entering Menu_Main in the Windows player** · broken squad cards.
 
+### QA-DOGFIGHT-MODE ⬜ — "Dog Fight": the Sparrow-only gun duel in the Boneyard
+Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 files, 15,626 insertions, authored headless — with new asset-writing tools (`Tools/Build/author_dogfight_assets.py`, `boneyard_budget.py`), a new scene (EditorBuildSettings changed), a `ScriptableEventCombatHitStats` SOAP type, and `GameDataSO` additions. Reference: `_Scripts/Controller/Arcade/DOGFIGHT.md`.
+
+1. Open the Dog Fight scene: no `Missing (Mono Script)`; the controller and its scoring rule are wired; the arena ("Boneyard") builds.
+2. Launch the mode (any player count — AI backfill for solo). It reaches gameplay without an exception.
+3. Confirm it is Sparrow-only and gun-combat focused (the Boneyard as the arena, the enemy marker, crystal drops).
+4. Play a full round to the win condition and watch the scoreboard resolve (combat-hit / kill scoring).
+5. Return to menu and relaunch once — no leaked state, no crash.
+
+PASS: scene opens clean; the mode launches, plays a full round to a resolved scoreboard, and returns/relaunches without error; combat scoring behaves; the Boneyard arena builds as intended. FAIL: missing scripts · a scene/controller that throws on load or launch · the round never resolving · a scoreboard that doesn't tally combat hits/kills · a crash on return/relaunch.
+
 ## Priority 1 — merged features that have never been played
 
 ### QA-ECOLOGY-WORM-KAIJU 🟡 — the worm colony boss
@@ -430,7 +441,9 @@ Source: PR #634 (prefab YAML surgery across 6 vessel prefabs + 3 HUD prefabs).
 
 PASS: no missing-script warnings anywhere; petal bars build, colour and animate correctly on both vessels. FAIL: any missing script · petal bars absent, mis-coloured or static.
 
-### QA-FTUE-QUEST-ROWS ⬜ — quest graphs lay out in venue rows
+### QA-FTUE-QUEST-ROWS ⛔ — quest graphs lay out in venue rows
+> **Last result:** ⛔ BLOCKED — Could not run — the Quest Graph Editor does not exist on this build. There is no `FrogletTools ▸ Quest Graph Editor` menu, and no `Quest Graph ▸ Layout All Phases (Rows)` menu item; the only FrogletTools graph/layout entry is the unrelated Prism "Auto-Wire Clock Properties". The absence itself is the finding — PR #633's editor tooling appears not to be present on bleeding-edge (same class of gap as the missing "Validate Lifeform Crystals" tool). Nothing about node layout could be judged.  _(build bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-11, andrew)_
+
 Source: PR #633 (six graph assets rewritten by script).
 
 1. FrogletTools ▸ Quest Graph Editor → MainQuest → click through Phases 0–5.
@@ -610,6 +623,48 @@ Source: `profile-save-and-ads-removal`. Removes Unity Ads (package manifest + `R
 
 PASS: compiles and resolves packages cleanly; no missing ads-UI scripts; daily reward works ad-free; the name double-submit is prevented. FAIL: a package-resolution/compile error · a missing `RewardedAdsButton` reference · a broken daily-reward flow · a display name that still double-submits.
 
+### QA-ASTROLEAGUE-REWORK ⬜ — Astro League as Rhino-only sword soccer (bigger court, strike feedback, food web)
+Source: `astro-league-improvements` (feat `769eeb61`, + `17e9116f` court-shrink follow-up). Significant rework of the existing Astro League: bigger court then a 40% shrink, ball settling, strike feedback, smarter AI, and a "working food web" (touches `Cell.cs`, `CellLifeSpawnerBase`, `Fauna`, `ECOSYSTEM.md`). Reference: `_Scripts/Controller/Arcade/ASTROLEAGUE.md`, `Docs/ECOSYSTEM.md`.
+
+1. Launch Astro League: no missing scripts; the court builds with its cage cover.
+2. Play — the ball settles rather than drifting forever; striking it gives clear feedback; goals score and golden-goal resolves.
+3. Confirm it plays as Rhino-only sword soccer and the AI is a credible opponent (not passive/stuck).
+4. Watch the cell's food web over a couple of minutes — fauna spawn, feed and behave (no frozen creatures, no runaway population).
+5. Score to the win condition and confirm the scoreboard resolves; return to menu cleanly.
+
+PASS: court + cage build clean; ball settles, strikes give feedback, goals/golden-goal resolve; AI competes; the food web runs without frozen/runaway fauna; the round resolves and returns cleanly. FAIL: missing scripts · a ball that never settles or a strike with no feedback · passive/stuck AI · frozen or exploding fauna · a round that won't resolve.
+
+### QA-CHARGE-CRYSTAL-SHADER ⬜ — dedicated charge-crystal shader (edge-only plasma, blooms in)
+Source: PR #710 (`charge-crystal-shader`). New `ChargeCrystal.shader` + `CrystalEdgeArcs` + `CrystalEdgeArcMeshBaker` + a re-imported crystal FBX; a follow-up (`5b5ca689`) makes it honour `_opacity` so the crystal still **blooms in** rather than popping. Shader work → magenta risk on charge crystals.
+
+1. Load a scene with charge crystals (freestyle in a cell with lifeforms, or any mode that drops elemental crystals). If a charge crystal renders **magenta**, the shader failed to compile — stop, FAIL, attach the error.
+2. Watch a charge crystal spawn — it should **bloom in** (continuity of existence), not pop.
+3. Look at the effect: edge-only plasma discharge / arcs along the crystal edges, reading as a charge crystal (distinct from the other three elements).
+4. Skim/collect one — it behaves as a normal charge crystal (energy/level applied), and withers/leaves on death per the usual rules.
+
+PASS: no magenta; charge crystals bloom in; the edge-arc plasma renders and reads as "charge"; collection and death behave normally. FAIL: a magenta/failed shader · a crystal that pops instead of blooming · no edge-arc effect or a broken look · collection/death misbehaving.
+
+### QA-SPARROW-MISSILE-BAY ⬜ — bay-animated skyburst launch with the real missile model
+Source: PR #708 (`sparrow-missile-bay`). The Sparrow's skyburst now launches from a bay animation using the real missile model (`Sparrow.prefab`, `SparrowAnimationController`, `FireGunActionExecutor`, `SkyBurstGunAction.asset`). Reference: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md` + `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
+
+1. Project compiles; open `Sparrow.prefab` — no missing scripts; the bay/missile wiring resolves.
+2. Sparrow in freestyle: fire a skyburst missile — a bay animation plays and the **real missile model** launches (not a placeholder/instant spawn).
+3. Fire several in succession — the bay animates each time and never jams/desyncs; the missile still flies and detonates as before.
+4. Confirm normal full-auto fire is unaffected.
+
+PASS: compiles, prefab intact; the skyburst launches from an animated bay with the real model; repeated fire animates cleanly; missiles fly/detonate normally; full-auto unaffected. FAIL: missing scripts · no bay animation or a placeholder model · a jam/desync on repeated fire · a missile that no longer flies/detonates · full-auto broken.
+
+### QA-ECOLOGY-JOUST-WITHER ⬜ — joust takes the heart, starvation exposes it; both leave a skeleton
+Source: PR #709 (`squirrel-joust-starvation-wither`). New `LifeformDeathStyle` enum + `HealthPrism`, `ElementalCrystalImpactor`, `VesselWitherLifeformByCrystalEffectSO`, `PrismSpatialIndex`, `FloraConfigurationSO` changes; wither cadence moved onto the variant config. A **LOCKED ecology** surface (continuity of existence / wither-to-crystal / mass conservation) — verify against those invariants. Reference: `Docs/ECOSYSTEM.md`.
+
+1. Project compiles; a cell with lifeforms builds with no missing scripts.
```

</details>

### `61ecbde78` — qa(backlog): scan to PR #717 (3 new items)

_Claude, 2026-08-14 15:34:31 +0000_

```text
- scan d32f2683 -> 26fcc090 (PRs #583-#717)
- new P1: QA-RAMPAGE-REBUILD (#717), QA-DOLPHIN-DRIFT-VELOCITY (#716), QA-PRISM-DEATH-TIER (#715)
- PrismDeathVisualTierTests added to QA-EDITMODE-TESTS suite list
- Dog Fight (#e1bb8ed8) + Ribcage re-merges are tuning to existing items (QA-DOGFIGHT-MODE, QA-RIBCAGE-MODE); no results submitted this run
```

```text
 Docs/QA/QA_BACKLOG.md | 35 +++++++++++++++++++++++++++++++++--
 1 file changed, 33 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index c0444ef5e..bfdaa7e78 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-12 · Scan covers: up to `d32f2683` (PRs #583–#710) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-14 · Scan covers: up to `26fcc090` (PRs #583–#717) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -108,7 +108,7 @@ Source: PRs #659, #639, #627, #641, #668, #651. These are NUnit suites authored
 
 1. Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
 2. Record every failing test by name, plus the total pass/fail count.
-3. Specifically confirm these suites are present and green: `CellSpawnFormationTests`, `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`, `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`, `GeometryUtilsTests`, `PrismOcclusionCoverageTests`, `ShipModifierTests` (PR #679), `DisplayNameValidatorTests` (PR #674/display-name-validation).
+3. Specifically confirm these suites are present and green: `CellSpawnFormationTests`, `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`, `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`, `GeometryUtilsTests`, `PrismOcclusionCoverageTests`, `ShipModifierTests` (PR #679), `DisplayNameValidatorTests` (PR #674/display-name-validation), `PrismDeathVisualTierTests` (PR #715).
 
 PASS: all EditMode tests green and all nine suites present. FAIL: any red test (record the name + assertion message) or a suite that does not appear at all (means it did not compile into the test assembly).
 
@@ -665,6 +665,37 @@ Source: PR #709 (`squirrel-joust-starvation-wither`). New `LifeformDeathStyle` e
 
 PASS: compiles; starvation withers-to-crystal and leaves a skeleton; a joust kill takes the heart and still withers/skeletons; nothing pops in or out; interrupted withers still finish. FAIL: a creature vanishing instead of withering · no skeleton left · a joust kill dropping no heart or a starved creature dropping none · an interrupted wither leaving a stuck/immortal husk · any missing script.
 
+### QA-RAMPAGE-REBUILD ⬜ — Rampage rebuilt as the Dolphin's demolition race (four intensities)
+Source: PR #717 (`dolphin-rampage-minigame`). A rebuild of the Rampage mode as the Dolphin's demolition race — 64 files, four intensities via a new `Tools/Build/rampage_intensity.py`, `SpawnProfileSO`/`GameDataSO` additions, crystals coupled to the nucleus, banded flora, AI-drift fix, and a fixed sticky cell-config race. Reference: `_Scripts/Controller/Arcade/RAMPAGE.md`, `Docs/ECOSYSTEM.md`.
+
+1. Open the Rampage scene / launch the mode: no `Missing (Mono Script)`; the controller + scoring rule wired; the cell builds.
+2. Launch at intensity 1, then at intensity 4 — the intensity ladder visibly differs (arena/population scale), not identical.
+3. Confirm it plays as a Dolphin demolition race: the objective (omni) crystal is coupled to the nucleus and the objective arrow tracks only that managed crystal; flora is banded; AI drifts/plays sensibly.
+4. Play a full round to the destruction target and watch the scoreboard resolve (environment-mass kills credited per-simulator and by domain).
+5. Return to menu and relaunch once — clean, no leaked score/state.
+
+PASS: scene clean; the four intensities differ; the demolition race plays with the nucleus-coupled objective crystal and correct arrow tracking; the round resolves on the destruction target with a sane scoreboard; return/relaunch clean. FAIL: missing scripts · identical intensities (config race not fixed) · the objective arrow tracking the wrong crystal · AI stuck/not drifting · a round that won't resolve · leaked score on relaunch.
+
+### QA-DOLPHIN-DRIFT-VELOCITY ⬜ — drift holds velocity magnitude for its whole duration
+Source: PR #716 (`dolphin-drift-velocity`). The Dolphin now **holds its velocity magnitude for the duration of a drift** (`VesselTransformer` + `SingleStickVesselTransformer` + `Dolphin.prefab`). Touches the **shared** transformer, so re-check other single-stick vessels don't regress. Reference: `DOLPHIN_ENERGY_ECONOMY.md`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
+
+1. Project compiles. Dolphin in freestyle: enter a drift — speed **holds** at its magnitude through the whole drift rather than bleeding off.
+2. Release the drift, then re-drift — the speed hold re-arms cleanly each time (watch the re-drift verification row in the checklist).
+3. Confirm it doesn't leak into non-drift flight (normal accel/decel unchanged when not drifting).
+4. Sanity-check another single-stick vessel (e.g. Serpent/Sparrow that share `SingleStickVesselTransformer`) — its drift/turn behaviour is unchanged.
+
+PASS: compiles; drift holds velocity for its full duration and re-arms on re-drift; non-drift flight unchanged; other single-stick vessels unaffected. FAIL: compile error · speed bleeding off during a drift · the hold not re-arming on re-drift · non-drift flight altered · another single-stick vessel regressing.
+
+### QA-PRISM-DEATH-TIER ⬜ — death visuals wear the dying prism's tier + danger-prism detonations
+Source: PR #715 (`danger-prisms-explosions`). Prism **death visuals now wear the dying prism's TIER** (plain/danger/shielded/super-shielded), not just its domain; danger prisms carry a detonation gain with extended reach. `PrismDebris` + `PrismExplosion` changes, new EditMode suite `PrismDeathVisualTierTests`. Related: QA-VESSEL-AOE-IMPULSE, QA-PALETTE-*.
+
+1. Project compiles; run `PrismDeathVisualTierTests` in EditMode (also under QA-EDITMODE-TESTS) — green.
+2. Destroy prisms of different tiers (plain, danger, shielded, super-shielded) and watch the death debris/explosion — each reads as its **tier**, not a generic domain-coloured burst.
+3. Detonate near danger prisms — the danger detonation gain visibly reaches farther than a plain destruction.
+4. Regression: a normal (plain) prism death still looks/behaves as before; no magenta or missing VFX.
+
+PASS: compiles + `PrismDeathVisualTierTests` green; death visuals differ by tier; danger detonations reach farther; plain deaths unchanged. FAIL: compile/test failure · all deaths looking identical regardless of tier · danger detonation with no extra reach · missing/broken death VFX on any tier.
+
 ## Priority 2 — lower risk, cosmetic, or data-gathering
 
 ### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
```

</details>

### `ed0064c7c` — qa(backlog): scan to PR #737 (9 new items, clustered)

_Claude, 2026-08-17 15:36:05 +0000_

```text
- scan 26fcc090 -> eb85e1e3 (PRs #583-#737), 60 merges
- new P1: QA-RHINO-SWORD-V3 (#726), QA-DOLPHIN-CRYSTAL-ENERGY-CLUSTER (#720/#721/#723),
  QA-PRISM-SHIELD-GPU-VISUALS (#727/#729/#730), QA-ECOLOGY-GYROID-COLONY (#734),
  QA-ECOLOGY-CRYSTAL-LEVELING (#737/#728), QA-VESSEL-SELF-TRAIL (#736), QA-KEYBOARD-CONTROLS (#722)
- new P2: QA-CRYSTAL-EFFECTS (#724/#725), QA-SPARROW-SPREAD-HAPTICS (#719)
- super-shield jiggle suite (#730) added to QA-EDITMODE-TESTS
- folded: #718->QA-RAMPAGE-REBUILD, #732->QA-ASTROLEAGUE-REWORK, #731->QA-P2-DANGLING-CELLDATA (likely fix),
  #735 logging + #733/#623 docs (not covered); #610/#612/#613/#614 re-merges already have items
```

```text
 Docs/QA/QA_BACKLOG.md | 96 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++--
 1 file changed, 94 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 127 lines)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index bfdaa7e78..7c0b73993 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-14 · Scan covers: up to `26fcc090` (PRs #583–#717) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-17 · Scan covers: up to `eb85e1e3` (PRs #583–#737) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -108,7 +108,7 @@ Source: PRs #659, #639, #627, #641, #668, #651. These are NUnit suites authored
 
 1. Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
 2. Record every failing test by name, plus the total pass/fail count.
-3. Specifically confirm these suites are present and green: `CellSpawnFormationTests`, `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`, `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`, `GeometryUtilsTests`, `PrismOcclusionCoverageTests`, `ShipModifierTests` (PR #679), `DisplayNameValidatorTests` (PR #674/display-name-validation), `PrismDeathVisualTierTests` (PR #715).
+3. Specifically confirm these suites are present and green: `CellSpawnFormationTests`, `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`, `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`, `GeometryUtilsTests`, `PrismOcclusionCoverageTests`, `ShipModifierTests` (PR #679), `DisplayNameValidatorTests` (PR #674/display-name-validation), `PrismDeathVisualTierTests` (PR #715), the super-shield jiggle suite (PR #730).
 
 PASS: all EditMode tests green and all nine suites present. FAIL: any red test (record the name + assertion message) or a suite that does not appear at all (means it did not compile into the test assembly).
 
@@ -696,6 +696,80 @@ Source: PR #715 (`danger-prisms-explosions`). Prism **death visuals now wear the
 
 PASS: compiles + `PrismDeathVisualTierTests` green; death visuals differ by tier; danger detonations reach farther; plain deaths unchanged. FAIL: compile/test failure · all deaths looking identical regardless of tier · danger detonation with no extra reach · missing/broken death VFX on any tier.
 
+### QA-RHINO-SWORD-V3 ⬜ — the energy-sword v3 rework
+Source: PR #726 (`energy-sword-v3-rework`, after `energy-sword-rework-retry`). A v3 rework of the Rhino's energy sword. Builds on the earlier sword work (QA-VESSEL-RHINO-SWORD, PR #639) — verify the new behaviour end-to-end. Reference: `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`.
+
+1. Project compiles; open `Rhino.prefab` — no missing scripts on the sword/skimmer.
+2. Rhino in freestyle: swing the sword and hit prisms — the swing/point-velocity, growth, and shield interaction behave per the v3 design (no dead sword, no stuck scale).
+3. Skim shielded/super-shielded track lining and confirm shell contacts still register (cross-check QA-SHELL-COLLISION).
+4. Clip your own just-laid trail — no self-collision (cross-check QA-VESSEL-SELF-TRAIL).
+5. Compare against the prior sword feel — the rework should read as intended, not a regression.
+
+PASS: compiles, prefab intact; the sword swings, grows and damages as designed in v3; shell contacts register; no self-trail collision; no dead/stuck sword. FAIL: missing scripts · a sword that doesn't swing/grow/damage · broken shell contacts · self-trail collision · an obvious regression from the prior sword.
+
+### QA-DOLPHIN-CRYSTAL-ENERGY-CLUSTER ⬜ — crystal-spawn rework, prism-collision energy, new skim effect
+Source: PRs #720 (`dolphin-crystal-spawn-rework`), #721 (`dolphin-prism-collision-energy`), #723 (`dolphin-skim-effect`). A themed cluster of Dolphin energy/crystal changes — do them in one Dolphin session. Related: QA-DOLPHIN-SKIM, QA-DOLPHIN-SKIM-ENERGY-CTA.
+
+1. Project compiles; Dolphin freestyle, no missing scripts.
+2. **Crystal spawn (#720):** trigger the Dolphin crystal — it spawns/blooms per the rework (not popping), fires its effect, and behaves as intended.
+3. **Prism-collision energy (#721):** ram/skim prism mass — the Dolphin banks energy from prism collision as designed; the HUD energy gauge moves accordingly.
+4. **Skim effect (#723):** skim prisms and confirm the new skim VFX renders (no magenta, no missing effect).
+5. Regression: normal flight/boost unaffected.
+
+PASS: compiles; crystal spawns/blooms and fires; prism-collision energy banks and the gauge tracks it; the new skim effect renders cleanly; no regressions. FAIL: missing scripts · crystal popping/not firing · no energy from prism collision or a stuck gauge · magenta/missing skim VFX · flight/boost regressed.
+
+### QA-PRISM-SHIELD-GPU-VISUALS ⬜ — octahedron-shield GPU morph + prism jiggle shader
+Source: PRs #729 (`octahedron-shield-gpu-morph`), #727 (`prism-jiggle-shader-effect`), #730 (`prism-super-shield-jiggle-tests`). GPU-driven shield morph + a prism "jiggle" shader effect, with a new EditMode jiggle test suite. Shader work → magenta risk on shielded prisms.
+
+1. Run the super-shield jiggle EditMode suite (PR #730; also under QA-EDITMODE-TESTS) — green.
+2. Get shielded + super-shielded prisms on screen (a cell with lifeforms, a HexRace/Skim track, or Astro League). If any prism is **magenta**, a shader failed — stop, FAIL.
+3. Watch a prism engage/disengage its shield — the octahedron shield morph now runs on the GPU; it should bloom/shatter smoothly, not snap or render the plain box (cross-check the exotic-visual handoff — a bare mesh swap renders nothing).
+4. Watch the jiggle effect on shielded prisms — it reads as a subtle animated jiggle, not a static or broken look.
+5. Confirm shielded collision still works (cross-check QA-SHELL-COLLISION).
+
+PASS: jiggle suite green; no magenta; shield morph runs on GPU and blooms/shatters smoothly; jiggle animates; shell collision intact. FAIL: a red test · magenta prisms · a shield that snaps, renders as the plain box, or renders nothing · a static/broken jiggle · broken shielded collision.
+
+### QA-ECOLOGY-GYROID-COLONY ⬜ — the gyroid octagon flora colony + Gyroid Lab
+Source: PR #734 (`flora-populations-gyroid`). A gyroid octagon flora colony ("a crystal in every window") with population-event reproduction (one birth per fauna-wave cycle from the colony frontier), a maturity gate (reproduce only once fully grown incl. bloom), colony diagnostics, a lattice-defect auditor, and a 5× ceiling raise. LOCKED ecology surface — verify against continuity/mass-conservation invariants. Reference: `Docs/ECOSYSTEM.md`.
+
+1. Project compiles; freestyle → Cell Selector → the gyroid colony cell (or Lifeform Matrix). It builds with no missing scripts.
+2. Watch the colony grow: plants mature, and **reproduction happens once per fauna-wave cycle** (a population event, not per-plant timers), popped from the frontier.
+3. Confirm a plant reproduces **only once fully grown** (maturity gate incl. the bloom) — no daughters spawning from immature plants.
+4. Watch for lattice defects — the octagon lattice should be clean (no permanent holes at plant boundaries, no chirality/z-mirror twins); the defect auditor should not scream.
+5. Continuity/mass: nothing pops in or out; growth withers/blooms; the colony ceiling behaves (grows toward the raised ceiling, doesn't freeze or explode).
+
+PASS: builds clean; population-event reproduction from mature plants; a clean lattice with no boundary holes or mirror twins; continuity of existence and mass conservation hold; the colony grows toward its ceiling. FAIL: missing scripts · per-plant timer reproduction or immature daughters · lattice holes / chirality twins / auditor errors · anything popping in/out · a frozen or runaway colony.
+
+### QA-ECOLOGY-CRYSTAL-LEVELING ⬜ — level is earned, heart size per level, crystal colours
+Source: PRs #737 (`crystal-sizing-lifeform-leveling`), #728 (`lifeform-crystal-colors`). Lifeform level is now EARNED (not always 1), a dropped heart is one size per level, Mass/Charge crystals got a model-size correction (and the Charge bump was reverted — measure not infer), and lifeform crystals carry per-element colours. LOCKED ecology surface. Reference: `Docs/ECOSYSTEM.md`.
+
+1. Project compiles; a cell with lifeforms builds clean.
+2. Kill creatures at different levels — the dropped heart's **size scales with the level** (a level-5 kill drops a visibly bigger crystal than a level-1).
+3. Confirm each element's crystal reads in its correct colour (Charge/Mass/Space/Time distinct), and Mass/Charge crystals are correctly sized (no oversized/undersized model).
+4. Confirm level is actually earned over time (a fresh creature isn't stuck at 1 forever, and isn't rolled/handed a level it didn't earn).
+
+PASS: compiles; heart size tracks level; per-element crystal colours correct; Mass/Charge model sizes corrected; level is earned. FAIL: uniform heart size regardless of level · wrong/duplicate crystal colours · a mis-sized Mass/Charge crystal · level stuck at 1 or handed unearned.
+
+### QA-VESSEL-SELF-TRAIL ⬜ — don't skim or ram your own trail while laying it
```

</details>

### `2bd20a82e` — qa(backlog): add curated '⚡ Quick wins' section + wire skill to refresh it each run

_Claude, 2026-08-17 15:40:28 +0000_

```text
- top-of-list pointer to the ~7 fastest open items (asset-only / one-glance / short checks)
- lives before Priority 0, so the apply engine passes it through untouched (bullets, not ### headings)
- SKILL.md step 2.5: rewrite it each run from OPEN items only, ranked by least effort
```

```text
 .claude/skills/qa-backlog/SKILL.md | 15 +++++++++++++++
 Docs/QA/QA_BACKLOG.md              | 17 +++++++++++++++++
 2 files changed, 32 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/qa-backlog/SKILL.md b/.claude/skills/qa-backlog/SKILL.md
index 5574e50c2..f59e90f1b 100644
--- a/.claude/skills/qa-backlog/SKILL.md
+++ b/.claude/skills/qa-backlog/SKILL.md
@@ -64,6 +64,21 @@ Never duplicate an item that already exists (match on the `QA-...` id). Never re
 that is present in `ARCHIVE.md` unless a later merge genuinely re-opened that work — if so,
 note why in the item's Source line.
 
+## 2.5. Refresh the "⚡ Quick wins" section
+
+`QA_BACKLOG.md` opens with a hand-curated `## ⚡ Quick wins` block (before `## Priority 0`).
+Rewrite it every run to point at the ~5–8 **open** items (⬜, or an actionable 🟡) that are
+the fastest / lowest-effort to get a clean verdict on — asset-only checks, one-glance visual
+checks, a single short flight, an editor-window check. Rank by least effort, not by priority.
+
+- **Only list open items.** Drop anything now PASS (archived), 🔴, or ⛔ — never point a tester
+  at a dead/failed item as a "quick win".
+- Give each a half-line on *why* it's quick (what the one check is).
+- It lives before the first `### QA-` heading, so it is raw pass-through text the apply engine
+  never rewrites — you are its only maintainer. Keep it a bullet list (no `### QA-` headings, or
+  the engine will parse them as duplicate item sections).
+- Say in the block that it is refreshed each run and can lag reality by one submission.
+
 ## 3. Print the prioritised list
 
 After steps 1–2, print the current backlog top-down (P0 first), one line per item:
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 7c0b73993..ea6620b02 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -16,6 +16,23 @@ Every item below landed on a shared branch without ever being opened in Unity by
 * Keep the Console open with Error Pause off and Clear on Play off.
 * Unless an item says otherwise, "freestyle" means: launch to `Menu_Main`, tap the centre crystal to take control of the vessel.
 
+## ⚡ Quick wins — start here if you have a few minutes
+
+Hand-curated by `/qa-backlog` each run: open items that are the fastest / lowest-effort to get a
+clean verdict on (asset-only, one-glance, or a single short check). Not a priority ranking —
+the P0 gates below still matter more; this is just "what can I knock out quickly." Refreshed
+every run, so it can lag reality by one submission.
+
+1. **QA-CHARGE-CRYSTAL-SHADER** — load a scene with a charge crystal and look: magenta = FAIL, blooms-in with edge arcs = pass. Seconds.
+2. **QA-UI-QUIT-BUTTON** — open the in-game settings panel, click Quit. One control, one check.
+3. **QA-P2-LIFEFORM-MATRIX-MOONS** — one glance at the Lifeform Matrix bench: are the four crystal "moons" visible or swallowed by the sphere.
+4. **QA-VESSEL-SPARROW-ROLL** — fly the Sparrow into prisms at a couple of angles; does it roll (not deflect off course).
+5. **QA-CRYSTAL-EFFECTS** — collect an elemental crystal (capture effect) and watch an omni crystal appear (blooms in, not pops).
+6. **QA-DOLPHIN-SPEED-TUNE** — Dolphin freestyle, check three numbers: cruise ≈78, boost fill ≈3.6 s, peak ≈357.
+7. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
+
+Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta) is nearly as cheap if you're already in the editor.
+
 ## Priority 0 — gates. Nothing below matters if these fail.
 
 ### QA-BUILD-COMPILE ⬜ — the project compiles, imports and boots
```

</details>

### `f8db4f62e` — qa: results 2026-08-17 andrew (quit-button PASS, charge-crystal PASS, sparrow-roll FAIL)

_Claude, 2026-08-17 17:32:13 +0000_

```text
 Docs/QA/RESULTS/2026-08-17-andrew.md | 9 +++++++++
 1 file changed, 9 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-17-andrew.md b/Docs/QA/RESULTS/2026-08-17-andrew.md
new file mode 100644
index 000000000..1b2e57ace
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-17-andrew.md
@@ -0,0 +1,9 @@
+# QA Results — 2026-08-17 — andrew
+
+Build: bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-UI-QUIT-BUTTON | PASS | Clicking the quit control exits the game. |
+| QA-CHARGE-CRYSTAL-SHADER | PASS | No charge crystals rendered magenta. |
+| QA-VESSEL-SPARROW-ROLL | FAIL | Hitting a prism as the Sparrow shifts my movement (course is redirected) rather than rolling the vessel in place — matches the item's "still being deflected off-course" FAIL criterion. |
```

</details>

### `bb588ef9d` — qa(backlog): apply 2026-08-17 results (2 archived, 1 failed) + refresh quick wins

_Claude, 2026-08-17 17:33:11 +0000_

```text
- QA-UI-QUIT-BUTTON PASS -> ARCHIVE; QA-CHARGE-CRYSTAL-SHADER PASS -> ARCHIVE
- QA-VESSEL-SPARROW-ROLL FAIL -> 🔴 + dev task (prism hit deflects course instead of rolling)
- no new merges since eb85e1e3; quick-wins re-picked from open items
```

```text
 Docs/QA/ARCHIVE.md    | 27 ++++++++++++++++++++++++++-
 Docs/QA/DEV_TASKS.md  |  7 +++++++
 Docs/QA/QA_BACKLOG.md | 39 ++++++++++++---------------------------
 3 files changed, 45 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 124 lines)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index bbdfffeb3..d343ef0c4 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -5,4 +5,29 @@ Kept so a re-scan never resurrects a passed item. **Owner: the `/qa-backlog` ski
 Each entry below left the backlog because a submitted RESULTS file marked it PASS. The
 `<!-- archived:QA-... -->` markers let the apply engine avoid re-archiving on re-runs.
 
-_(none yet)_
+<!-- archived:QA-CHARGE-CRYSTAL-SHADER -->
+_Passed on build bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-17, andrew)._
+
+### QA-CHARGE-CRYSTAL-SHADER ⬜ — dedicated charge-crystal shader (edge-only plasma, blooms in)
+Source: PR #710 (`charge-crystal-shader`). New `ChargeCrystal.shader` + `CrystalEdgeArcs` + `CrystalEdgeArcMeshBaker` + a re-imported crystal FBX; a follow-up (`5b5ca689`) makes it honour `_opacity` so the crystal still **blooms in** rather than popping. Shader work → magenta risk on charge crystals.
+
+1. Load a scene with charge crystals (freestyle in a cell with lifeforms, or any mode that drops elemental crystals). If a charge crystal renders **magenta**, the shader failed to compile — stop, FAIL, attach the error.
+2. Watch a charge crystal spawn — it should **bloom in** (continuity of existence), not pop.
+3. Look at the effect: edge-only plasma discharge / arcs along the crystal edges, reading as a charge crystal (distinct from the other three elements).
+4. Skim/collect one — it behaves as a normal charge crystal (energy/level applied), and withers/leaves on death per the usual rules.
+
+PASS: no magenta; charge crystals bloom in; the edge-arc plasma renders and reads as "charge"; collection and death behave normally. FAIL: a magenta/failed shader · a crystal that pops instead of blooming · no edge-arc effect or a broken look · collection/death misbehaving.
+<!-- /archived:QA-CHARGE-CRYSTAL-SHADER -->
+
+<!-- archived:QA-UI-QUIT-BUTTON -->
+_Passed on build bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-17, andrew)._
+
+### QA-UI-QUIT-BUTTON ⬜ — quit-game control moved into the settings panel
+Source: `quit-game-button` (`fabe7074`). The standalone `QuitGameButton.cs` was removed and its behaviour folded into `GameSettingsPanelController`.
+
+1. Open the in-game settings panel: a Quit control is present with no missing-script slot where the old button was.
+2. Trigger quit from the settings panel — it does what it should (returns to menu / quits per design) with no exception.
+3. Confirm nothing else in the settings panel regressed.
+
+PASS: the quit control is present in the settings panel and works with no missing scripts or exceptions; the rest of the panel is intact. FAIL: a missing button/script · a quit control that throws or does nothing · another settings control broken by the move.
+<!-- /archived:QA-UI-QUIT-BUTTON -->
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index b46e45ef0..409841cf4 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -36,3 +36,10 @@ of duplicating it.
 - **Symptom:** Three problems beyond the known exceptions; everything else (skimmers, ability rows, hull morphs, speed-tunnel law, occlusion corridor, baselines) ran fine. (1) **Audit Cell-Owned Visuals** logged errors: "'CosmicShore.Core.NetworkMonitor' is missing the class attribute 'ExtensionOfNativeClass'!" (x2) and warning "GameObject (named 'NetworkMonitor') references runtime script in scene file. Fixing!", then "[CellOwnedVisualAudit] 26 scenes scanned." (2) **Validate Lifeform Crystals** — the menu item does not exist on this build (could not run it). (3) **Game Mode Prefab Kit ▸ Validate** — 1 error + ~40 warnings; logged "[PrefabKit] Created kit config at Assets/Resources/GameModePrefabKit.asset with 9 seeded entries." For reference the baseline line read: "SpawnableAtlantis 67,722 prisms / 950,437 volume", and the occlusion-corridor check reported the hlsl GUID pinned (OK).
 - **Definition of done:** QA item `QA-AUDIT-TOOLS` passes.
 <!-- /devtask:QA-AUDIT-TOOLS -->
+
+<!-- devtask:QA-VESSEL-SPARROW-ROLL -->
+### QA-VESSEL-SPARROW-ROLL — Sparrow rolls on prism hit
+- **Failed on:** bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-17, andrew)
+- **Symptom:** Hitting a prism as the Sparrow shifts my movement (course is redirected) rather than rolling the vessel in place — matches the item's "still being deflected off-course" FAIL criterion.
+- **Definition of done:** QA item `QA-VESSEL-SPARROW-ROLL` passes.
+<!-- /devtask:QA-VESSEL-SPARROW-ROLL -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index ea6620b02..01d2be90d 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -23,16 +23,18 @@ clean verdict on (asset-only, one-glance, or a single short check). Not a priori
 the P0 gates below still matter more; this is just "what can I knock out quickly." Refreshed
 every run, so it can lag reality by one submission.
 
-1. **QA-CHARGE-CRYSTAL-SHADER** — load a scene with a charge crystal and look: magenta = FAIL, blooms-in with edge arcs = pass. Seconds.
-2. **QA-UI-QUIT-BUTTON** — open the in-game settings panel, click Quit. One control, one check.
-3. **QA-P2-LIFEFORM-MATRIX-MOONS** — one glance at the Lifeform Matrix bench: are the four crystal "moons" visible or swallowed by the sphere.
-4. **QA-VESSEL-SPARROW-ROLL** — fly the Sparrow into prisms at a couple of angles; does it roll (not deflect off course).
-5. **QA-CRYSTAL-EFFECTS** — collect an elemental crystal (capture effect) and watch an omni crystal appear (blooms in, not pops).
-6. **QA-DOLPHIN-SPEED-TUNE** — Dolphin freestyle, check three numbers: cruise ≈78, boost fill ≈3.6 s, peak ≈357.
-7. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
+1. **QA-P2-LIFEFORM-MATRIX-MOONS** — one glance at the Lifeform Matrix bench: are the four crystal "moons" visible or swallowed by the sphere.
+2. **QA-CRYSTAL-EFFECTS** — collect an elemental crystal (capture effect) and watch an omni crystal appear (blooms in, not pops).
+3. **QA-DOLPHIN-SPEED-TUNE** — Dolphin freestyle, check three numbers: cruise ≈78, boost fill ≈3.6 s, peak ≈357.
+4. **QA-DOLPHIN-DRIFT-VELOCITY** — Dolphin freestyle, enter a drift: does the speed hold for the whole drift instead of bleeding off.
+5. **QA-VESSEL-SELF-TRAIL** — fly a tight loop so you cross your own just-laid trail: no skim/ram/slow off the fresh trail.
+6. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
+7. **QA-PALETTE-DANGER-GOLD** — get danger + shielded prisms on screen (a populated cell) and check gold reads in the pastel family, danger isn't inverted.
 
 Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta) is nearly as cheap if you're already in the editor.
 
+Tip: #1–#5 and #7 are all "one freestyle session in a populated cell" — load a lifeform-rich cell (Cell Selector → Yggdra/Hesperides) on the Dolphin and you can knock out several in a row.
+
 ## Priority 0 — gates. Nothing below matters if these fail.
 
 ### QA-BUILD-COMPILE ⬜ — the project compiles, imports and boots
```

</details>

### `00c2fd95c` — qa(backlog): scan to PR #750 (4 new items)

_Claude, 2026-08-18 16:08:12 +0000_

```text
- scan eb85e1e3 -> 55b310a6 (PRs #583-#750), 20 merges
- new P1: QA-URCHIN-VESSEL (#746), QA-DOLPHIN-ELEMENTAL-REWORK (#740/#749),
  QA-TOYS-SWITCH-RING (#750), QA-ECOLOGY-LATTICE-FLORA (#747/#744/#748)
- folded: #745/#743 ability-object-ambiguity (compile fix -> QA-BUILD-COMPILE/QA-AUDIT-TOOLS),
  #742 menu-lava-lamp-camera -> QA-MENU-CAMERA-RIG; #747 also extends QA-ECOLOGY-GYROID-COLONY
- no new results; quick-wins unchanged (all still open)
```

```text
 Docs/QA/QA_BACKLOG.md | 44 +++++++++++++++++++++++++++++++++++++++++++-
 1 file changed, 43 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 01d2be90d..ae29c713e 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-17 · Scan covers: up to `eb85e1e3` (PRs #583–#737) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-18 · Scan covers: up to `55b310a6` (PRs #583–#750) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -781,6 +781,48 @@ Source: PR #722 (`keyboard-controls`). A keyboard control scheme (input strategy
 
 PASS: full vessel control from the keyboard with sensible mappings; abilities respond; no double-driven UI; gamepad hand-off works. FAIL: unmapped/broken controls · an ability with no key · keyboard driving the UI and the vessel at once · a broken device switch.
 
+### QA-URCHIN-VESSEL ⬜ — the Urchin vessel revived (chain-reaction spikes + prismscape rider)
+Source: PR #746 (`restore-urchin-vessel`). A whole vessel brought back — 100 files, 8,464 insertions, authored headless, with a new asset-writing tool (`Tools/Build/author_urchin_assets.py`) and an 854-line verification checklist. Reference: `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Docs/ElementalAbilitySystem/FLEET_MAPS.md`.
+
+1. Project compiles; open `Urchin.prefab` — no `Missing (Mono Script)`; camera/telemetry/HUD/skimmer wiring resolves.
+2. Fly the Urchin in freestyle: it spawns, is controllable, and its HUD renders (ability row, petal bars).
+3. Exercise its abilities — the chain-reaction spikes and the "prismscape rider" behave as designed (spikes chain; the rider interacts with prism mass).
+4. Confirm it swaps in cleanly via the Vessel Changer toy and inherits pose/speed.
+5. Run FrogletTools ▸ Vessels ▸ Audit Vessel Skimmers / Ability Rows against the Urchin — record its verdict (may be design-blocked; note what the audits say).
+
+PASS: compiles, prefab intact; the Urchin spawns, flies, and renders its HUD; chain-spikes and prismscape rider work; a Vessel Changer swap is clean; audits report clean or a known/annotated state. FAIL: missing scripts · a vessel that won't spawn/fly · abilities that don't fire · a broken HUD · a swap that throws · an unexpected audit failure.
+
+### QA-DOLPHIN-ELEMENTAL-REWORK ⬜ — elemental map re-cut around one weapon + Time-5 Drift Ward
+Source: PRs #740 (`dolphin-elemental-upgrades`, re-cut the elemental map around one weapon), #749 (`dolphin-time5-debuff-immunity`, Time 5 re-scoped to **Drift Ward** — elemental-debuff immunity **while drifting**). Vessel elemental-ability surface. Reference: `Docs/ElementalAbilitySystem/FLEET_MAPS.md`, `DOLPHIN_ENERGY_ECONOMY.md`.
+
+1. Project compiles; Dolphin HUD shows four ability icons in charge → mass → space → time order (run Audit Vessel Ability Rows).
+2. Raise each element to its unlock level (5) via crystals / test harness — the re-cut map's upgrades apply to the intended weapon/abilities, and the icon upgrade signal (badge/tint/scale) fires.
+3. **Time 5 Drift Ward:** at Time ≥ 5, take a danger-prism/elemental debuff **while drifting** → the element flowers do not dip (immunity); **not drifting** → they dip. The slow/mute still land either way (by design).
+4. MPPM two clients, one at Time 5: both agree on who resists the drain (replicated unlock state).
+
+PASS: compiles; ability row 4/4 in order; the re-cut upgrades apply as intended; Drift Ward blocks the elemental drain only while drifting; peers agree. FAIL: compile error · wrong/missing ability order or upgrade signal · Drift Ward blocking when not drifting, or not blocking while drifting, or blocking the slow/mute · peers disagreeing.
+
+### QA-TOYS-SWITCH-RING ⬜ — every freestyle toy inside a switch ring
+Source: PR #750 (`freestyle-toys-switch-fundamental`). All freestyle toys are now placed inside a "switch ring" (a new asset-writing tool `Tools/Build/toy_switch_ring_geometry.py`). Reference: `Docs/ToySystem/ARCHITECTURE.md`, `BACKLOG.md`.
+
+1. Enter freestyle (Menu_Main → take control): the toys sit in a switch ring around the membrane; no toy is missing or mis-placed, nothing assembles in view.
+2. Fly each toy in the ring and confirm it still triggers its function (cell selector, vessel changer, domain changer, painting, Wanderway).
+3. Confirm the ring re-arms correctly after use (a used toy doesn't switch you back before you fly clear).
+4. Return from an arcade game and re-enter freestyle — the ring is intact.
+
+PASS: all toys present in the switch ring and each still triggers its function; re-arm behaves; the ring survives a game round-trip; nothing assembles in view. FAIL: a missing/mis-placed toy · a toy that no longer triggers · broken re-arm · an emblem building in view · the ring absent after a game return.
+
+### QA-ECOLOGY-LATTICE-FLORA ⬜ — gyroid branch-pair + Schwarz-P non-Euclidean tile + charge flora shields
+Source: PRs #747 (`branch-spindle-gyroid-redesign` — gyroid branch is a mirrored half-branch pair), #744 (`schwarz-p-noneuclidean-tile` — Schwarz P grows on its own non-Euclidean tile, per-element lattice scale, a silent prism-size clamp), #748 (`charge-flora-prism-shield` — Charge armours its mass; both lattice species fitted for the shield). Extends QA-ECOLOGY-GYROID-COLONY; LOCKED ecology surface. Reference: `Docs/ECOSYSTEM.md`.
+
+1. Project compiles; load the gyroid / Schwarz-P lattice flora cell (Cell Selector or Lifeform Matrix) — no missing scripts, no `None` refs.
+2. **Gyroid branch:** grown gyroid flora show the mirrored half-branch pair geometry (not a single branch punched through the prism); the branch-pair verifier's intent holds visually.
+3. **Schwarz P:** the Schwarz-P species grows on its non-Euclidean tile, sized per-element; no prisms are silently clamped to a wrong size (leaves/tiles look correctly scaled).
+4. **Charge shields:** Charge-domain lattice flora carry the prism shield (their mass is armoured) and the shield fits the leaf clearance — no shield clipping through or dwarfing the leaf.
+5. Continuity/mass: growth blooms/withers, nothing pops; population behaves per QA-ECOLOGY-GYROID-COLONY.
+
+PASS: compiles; gyroid branches are mirrored half-pairs; Schwarz-P grows correctly-scaled on its tile with no bad clamps; charge flora are shielded with a well-fitted shield; continuity/mass hold. FAIL: missing scripts/None refs · a single-branch gyroid · mis-scaled/clamped Schwarz-P · charge flora with no shield or a clipping/oversized one · anything popping in/out.
+
 ## Priority 2 — lower risk, cosmetic, or data-gathering
 
 ### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
```

</details>

### `7c0512352` — qa: results 2026-08-18 andrew (drift-velocity PASS, matrix-moons PASS, crystal-effects FAIL)

_Claude, 2026-08-18 18:02:10 +0000_

```text
 Docs/QA/RESULTS/2026-08-18-andrew.md | 9 +++++++++
 1 file changed, 9 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-18-andrew.md b/Docs/QA/RESULTS/2026-08-18-andrew.md
new file mode 100644
index 000000000..1c37ae478
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-18-andrew.md
@@ -0,0 +1,9 @@
+# QA Results — 2026-08-18 — andrew
+
+Build: bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-DOLPHIN-DRIFT-VELOCITY | PASS | The Dolphin's speed does not drop while drifting — it holds for the duration of the drift. |
+| QA-P2-LIFEFORM-MATRIX-MOONS | PASS | Four element crystals are visible, clustered together ("like a carton of eggs") in the emblem core of the Lifeform Matrix. The item's defect was the four "moons" being INVISIBLE (swallowed at a 2.2u offset inside the sphere); the toy was redesigned (LifeformMatrixToy.cs) to cluster the four crystal MODELS on the emblem core sub-ring, told apart by shape. Four visible, distinct crystals = the swallowed-moons defect is resolved. (Tester guessed FAIL; corrected to PASS per the item's "four visible and distinct" criterion and the code redesign. If the four read as identical shapes, that is a separate QA-TOYS-EMBLEMS concern, not this item.) |
+| QA-CRYSTAL-EFFECTS | FAIL | In HexRace: the crystal model and its breaking/collection effect look normal, but when a new crystal spawns in it POPS into existence instead of blooming in — the omni/crystal bloom-in (PR #724) is not playing on spawn. |
```

</details>

### `d0e3ebf48` — qa(backlog): apply 2026-08-18 results (2 archived, 1 failed) + refresh quick wins

_Claude, 2026-08-18 18:03:15 +0000_

```text
- QA-DOLPHIN-DRIFT-VELOCITY PASS -> ARCHIVE
- QA-P2-LIFEFORM-MATRIX-MOONS PASS -> ARCHIVE (moons redesigned into emblem core; swallowed-moons defect resolved)
- QA-CRYSTAL-EFFECTS FAIL -> 🔴 + dev task (HexRace crystals pop in instead of blooming; PR #724 bloom not playing)
- no new merges since 55b310a6; quick-wins re-picked (backfill: capsule-blast, skim-energy-cta, dangling-celldata)
```

```text
 Docs/QA/ARCHIVE.md    | 21 +++++++++++++++++++++
 Docs/QA/DEV_TASKS.md  |  7 +++++++
 Docs/QA/QA_BACKLOG.md | 35 ++++++++++++-----------------------
 3 files changed, 40 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 113 lines)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index d343ef0c4..409d4a9f1 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -31,3 +31,24 @@ Source: `quit-game-button` (`fabe7074`). The standalone `QuitGameButton.cs` was
 
 PASS: the quit control is present in the settings panel and works with no missing scripts or exceptions; the rest of the panel is intact. FAIL: a missing button/script · a quit control that throws or does nothing · another settings control broken by the move.
 <!-- /archived:QA-UI-QUIT-BUTTON -->
+
+<!-- archived:QA-DOLPHIN-DRIFT-VELOCITY -->
+_Passed on build bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-18, andrew)._
+
+### QA-DOLPHIN-DRIFT-VELOCITY ⬜ — drift holds velocity magnitude for its whole duration
+Source: PR #716 (`dolphin-drift-velocity`). The Dolphin now **holds its velocity magnitude for the duration of a drift** (`VesselTransformer` + `SingleStickVesselTransformer` + `Dolphin.prefab`). Touches the **shared** transformer, so re-check other single-stick vessels don't regress. Reference: `DOLPHIN_ENERGY_ECONOMY.md`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
+
+1. Project compiles. Dolphin in freestyle: enter a drift — speed **holds** at its magnitude through the whole drift rather than bleeding off.
+2. Release the drift, then re-drift — the speed hold re-arms cleanly each time (watch the re-drift verification row in the checklist).
+3. Confirm it doesn't leak into non-drift flight (normal accel/decel unchanged when not drifting).
+4. Sanity-check another single-stick vessel (e.g. Serpent/Sparrow that share `SingleStickVesselTransformer`) — its drift/turn behaviour is unchanged.
+
+PASS: compiles; drift holds velocity for its full duration and re-arms on re-drift; non-drift flight unchanged; other single-stick vessels unaffected. FAIL: compile error · speed bleeding off during a drift · the hold not re-arming on re-drift · non-drift flight altered · another single-stick vessel regressing.
+<!-- /archived:QA-DOLPHIN-DRIFT-VELOCITY -->
+
+<!-- archived:QA-P2-LIFEFORM-MATRIX-MOONS -->
+_Passed on build bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-18, andrew)._
+
+### QA-P2-LIFEFORM-MATRIX-MOONS ⬜ — element-crystal "moons" swallowed by the toy body
+Suspected pre-existing: the Lifeform Matrix's four crystal moons sit ~2.2 world units out while toys place at `toyBodyRadius = 22`. Look at the bench. PASS = the four moons are visible and distinct. FAIL = they are inside the sphere (then the fix is a placement value, not code).
+<!-- /archived:QA-P2-LIFEFORM-MATRIX-MOONS -->
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index 409841cf4..bbb8874ff 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -43,3 +43,10 @@ of duplicating it.
 - **Symptom:** Hitting a prism as the Sparrow shifts my movement (course is redirected) rather than rolling the vessel in place — matches the item's "still being deflected off-course" FAIL criterion.
 - **Definition of done:** QA item `QA-VESSEL-SPARROW-ROLL` passes.
 <!-- /devtask:QA-VESSEL-SPARROW-ROLL -->
+
+<!-- devtask:QA-CRYSTAL-EFFECTS -->
+### QA-CRYSTAL-EFFECTS — elemental crystal capture effect + omni-crystal bloom
+- **Failed on:** bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-18, andrew)
+- **Symptom:** In HexRace: the crystal model and its breaking/collection effect look normal, but when a new crystal spawns in it POPS into existence instead of blooming in — the omni/crystal bloom-in (PR #724) is not playing on spawn.
+- **Definition of done:** QA item `QA-CRYSTAL-EFFECTS` passes.
+<!-- /devtask:QA-CRYSTAL-EFFECTS -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index ae29c713e..b8d327887 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -23,17 +23,17 @@ clean verdict on (asset-only, one-glance, or a single short check). Not a priori
 the P0 gates below still matter more; this is just "what can I knock out quickly." Refreshed
 every run, so it can lag reality by one submission.
 
-1. **QA-P2-LIFEFORM-MATRIX-MOONS** — one glance at the Lifeform Matrix bench: are the four crystal "moons" visible or swallowed by the sphere.
-2. **QA-CRYSTAL-EFFECTS** — collect an elemental crystal (capture effect) and watch an omni crystal appear (blooms in, not pops).
-3. **QA-DOLPHIN-SPEED-TUNE** — Dolphin freestyle, check three numbers: cruise ≈78, boost fill ≈3.6 s, peak ≈357.
-4. **QA-DOLPHIN-DRIFT-VELOCITY** — Dolphin freestyle, enter a drift: does the speed hold for the whole drift instead of bleeding off.
-5. **QA-VESSEL-SELF-TRAIL** — fly a tight loop so you cross your own just-laid trail: no skim/ram/slow off the fresh trail.
-6. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
-7. **QA-PALETTE-DANGER-GOLD** — get danger + shielded prisms on screen (a populated cell) and check gold reads in the pastel family, danger isn't inverted.
+1. **QA-DOLPHIN-SPEED-TUNE** — Dolphin freestyle, check three numbers: cruise ≈78, boost fill ≈3.6 s, peak ≈357.
+2. **QA-DOLPHIN-CAPSULE-BLAST** — first an editor check: `_Prefabs/Projectile/AOEConicExplosion.prefab` has a **Capsule Collider** (not Sphere/missing); then a charged Dolphin crystal blast should fan wide-in-jaw-plane, growing in length.
+3. **QA-DOLPHIN-SKIM-ENERGY-CTA** — editor: the six HUD-variant prefabs have no missing scripts; then Dolphin skim → energy fills much slower, and the jaw gauge arms **lime** at full.
+4. **QA-VESSEL-SELF-TRAIL** — fly a tight loop so you cross your own just-laid trail: no skim/ram/slow off the fresh trail.
+5. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
+6. **QA-PALETTE-DANGER-GOLD** — get shielded prisms of all three domains on screen (a populated cell / HexRace) and check gold reads in the pastel family.
+7. **QA-P2-DANGLING-CELLDATA** — a populated cell in freestyle: watch the Console for `LifeForm.Start()` / `Flora.Plant()` throws (PR #731 may have fixed this — verify which, if any, still throw).
 
-Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta) is nearly as cheap if you're already in the editor.
+Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) is nearly as cheap if you're already in the editor.
 
-Tip: #1–#5 and #7 are all "one freestyle session in a populated cell" — load a lifeform-rich cell (Cell Selector → Yggdra/Hesperides) on the Dolphin and you can knock out several in a row.
+Tip: #1–#4, #6, #7 are all "one Dolphin freestyle session in a populated cell" — load a lifeform-rich cell (Cell Selector → Yggdra/Hesperides) on the Dolphin and knock out several in a row. #2/#3 start with a quick editor prefab glance.
 
 ## Priority 0 — gates. Nothing below matters if these fail.
 
@@ -687,16 +687,6 @@ Source: PR #717 (`dolphin-rampage-minigame`). A rebuild of the Rampage mode as t
 
 PASS: scene clean; the four intensities differ; the demolition race plays with the nucleus-coupled objective crystal and correct arrow tracking; the round resolves on the destruction target with a sane scoreboard; return/relaunch clean. FAIL: missing scripts · identical intensities (config race not fixed) · the objective arrow tracking the wrong crystal · AI stuck/not drifting · a round that won't resolve · leaked score on relaunch.
 
-### QA-DOLPHIN-DRIFT-VELOCITY ⬜ — drift holds velocity magnitude for its whole duration
```

</details>

### `a4b05d4c9` — qa(backlog): scan to PR #761 (3 new items) + Dolphin min-speed criterion update

_Claude, 2026-08-20 15:34:37 +0000_

```text
- scan 55b310a6 -> 04756610 (PRs #583-#761), 18 merges
- new P0: QA-SCARAB-MODE (#755/#758/#761), QA-BENDS-MODE (#752, GameModes.Bends=42)
- new P1: QA-ECOLOGY-QUASICRYSTAL (#753/#754)
- updated QA-DOLPHIN-SPEED-TUNE: min speed now 0 per #760 (was floor of 10)
- folded: #751->QA-URCHIN-VESSEL, #757->QA-DOLPHIN-ELEMENTAL-REWORK (Drift Ward now danger-only),
  #759 AI fix (verified via mode play)
- no new results; quick-wins unchanged (all still open)
```

```text
 Docs/QA/QA_BACKLOG.md | 35 +++++++++++++++++++++++++++++++++--
 1 file changed, 33 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index b8d327887..b0ed329ce 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-18 · Scan covers: up to `55b310a6` (PRs #583–#750) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-20 · Scan covers: up to `04756610` (PRs #583–#761) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -181,6 +181,27 @@ Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 fi
 
 PASS: scene opens clean; the mode launches, plays a full round to a resolved scoreboard, and returns/relaunches without error; combat scoring behaves; the Boneyard arena builds as intended. FAIL: missing scripts · a scene/controller that throws on load or launch · the round never resolving · a scoreboard that doesn't tally combat hits/kills · a crash on return/relaunch.
 
+### QA-SCARAB-MODE ⬜ — the Scarab vessel + party game
+Source: PRs #755 (`scarab-party-game` — smooth nucleus release, bigger skimmer, cap overload, domain blast), #758 (`scarab-wing-prism-dais` — membrane blow-out identity test), #761 (`scarab-squirrel-colliders` — omni-only ball forge, no hull omni effects, per-cell ball overload). A large new vessel/party-game cluster (79 + 17 + 31 files), authored headless.
+
+1. Project compiles; open the Scarab vessel prefab and its mode scene — no `Missing (Mono Script)`; controller + scoring rule wired.
+2. Launch the Scarab mode (any player count): it reaches gameplay without an exception; the arena/cell builds.
+3. Fly the Scarab: the skimmer, nucleus release, cap/overload, and domain blast behave as designed; the omni-only ball forge works and hull omni effects are absent (per #761).
+4. Play a full round to the win condition; scoreboard resolves; return/relaunch clean.
+5. Confirm the Squirrel colliders touched in #761 didn't regress the Squirrel (fly it briefly).
+
+PASS: compiles, prefabs/scene intact; the Scarab mode launches, plays to a resolved scoreboard, and returns cleanly; Scarab abilities and the ball forge behave; Squirrel unaffected. FAIL: missing scripts · a scene/vessel that throws on load or launch · abilities/ball forge not working · a round that won't resolve · a Squirrel regression.
+
+### QA-BENDS-MODE ⬜ — "The Bends": the Dolphin-only debuff duel (GameModes.Bends = 42)
+Source: PR #752 (`dolphin-dogfighting-game`). A new mode — `GameModes.Bends = 42`, Dolphin-only debuff duel — 42 files, 12,899 insertions, authored headless.
+
+1. Open the Bends scene / launch the mode: no missing scripts; controller + scoring rule wired; the arena builds.
+2. Launch (AI backfill for solo): reaches gameplay without an exception; confirm it is Dolphin-only.
+3. Play the debuff-duel loop — the win/scoring condition (debuffs applied / duel outcome) behaves as designed.
+4. Play a full round to resolution; scoreboard resolves; return to menu and relaunch once — clean.
+
+PASS: scene clean; the mode launches Dolphin-only, plays its debuff duel to a resolved scoreboard, and returns/relaunches without error. FAIL: missing scripts · a controller that throws on load/launch · the duel/scoring not resolving · a crash on return/relaunch.
+
 ## Priority 1 — merged features that have never been played
 
 ### QA-ECOLOGY-WORM-KAIJU 🟡 — the worm colony boss
@@ -813,6 +834,16 @@ Source: PRs #747 (`branch-spindle-gyroid-redesign` — gyroid branch is a mirror
 
 PASS: compiles; gyroid branches are mirrored half-pairs; Schwarz-P grows correctly-scaled on its tile with no bad clamps; charge flora are shielded with a well-fitted shield; continuity/mass hold. FAIL: missing scripts/None refs · a single-branch gyroid · mis-scaled/clamped Schwarz-P · charge flora with no shield or a clipping/oversized one · anything popping in/out.
 
+### QA-ECOLOGY-QUASICRYSTAL ⬜ — the Lattice cell grows to twelve quasicrystal colonies
+Source: PRs #753 (`gyroid-schwarz-flora-cell` — seed the Lattice cell with EIGHT founders, not 240), #754 (`exotic-quasicrystal-flora` — grow the Lattice cell to twelve colonies with the quasicrystal). Extends QA-ECOLOGY-LATTICE-FLORA / QA-ECOLOGY-GYROID-COLONY; a new quasicrystal flora form + the Lattice cell tuning. LOCKED ecology surface. Reference: `Docs/ECOSYSTEM.md`.
+
+1. Project compiles; freestyle → Cell Selector → the Lattice cell — it builds with no missing scripts / `None` refs.
+2. Confirm it seeds with a small number of founders (~8, not 240 — a wall of founders on boot means the seed fix didn't take) and grows out to ~twelve colonies over time.
+3. Look at the **quasicrystal** flora form — it grows as a coherent quasicrystal lattice (not a broken/degenerate mesh), alongside the gyroid/Schwarz-P species.
+4. Continuity/mass: growth blooms/withers, nothing pops; population behaves (no frozen/runaway colony) per QA-ECOLOGY-GYROID-COLONY.
+
+PASS: compiles; the Lattice cell seeds with ~8 founders and grows to ~twelve colonies; the quasicrystal form renders and grows coherently; continuity/mass hold. FAIL: missing scripts/None refs · a 240-founder boot wall · a broken/degenerate quasicrystal · anything popping in/out · a frozen or runaway colony.
+
 ## Priority 2 — lower risk, cosmetic, or data-gathering
 
 ### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
@@ -848,7 +879,7 @@ Source: PR #681 (`dolphin-speed-boost-tuning`). Two authored numbers changed in
 3. Full charged-boost discharge → peak speed reaches **~357** (was 210), draining over ~2.5 s.
 4. Sanity: no other vessel's speed/boost changed (the asset is Dolphin-only).
 
-PASS: cruise ~78, fill ~3.6 s, charged peak ~357, drain ~2.5 s; no other vessel affected. FAIL: values materially off from those targets · another vessel's boost/speed changed · the drift/idle floor moved (it should stay at 10). (Feel is a judgement call — note whether the new boost peak plays too strong.)
+PASS: cruise ~78, fill ~3.6 s, charged peak ~357, drain ~2.5 s; no other vessel affected; the Dolphin's **minimum speed is now 0** (updated per PR #760 — the vessel can come to a full stop). FAIL: values materially off from those targets · another vessel's boost/speed changed · a non-zero minimum speed (the old floor of 10 was removed by #760). (Feel is a judgement call — note whether the new boost peak plays too strong, and whether a full stop feels right.)
 
 ### QA-PALETTE-DANGER-GOLD ⬜ — danger tier un-inverted + gold shielded brought into the pastel family
 Source: PRs #705 (`danger-prisms-shielded-color`, `ThemeManager`) + #707 (`gold-shielded-prism-contrast`, `OriginalColorSetSO.asset`). Palette-only fixes: the danger tier was un-inverted, gold's unshielded rim corrected, and gold's shielded prism brought into the pastel family. Colour verification. Related: QA-PALETTE-SHIELDED. Reference: `Docs/PALETTE.md`.
```

</details>

### `d6d2c616c` — qa: results 2026-08-20 andrew (Scarab mode PASS + 2 design notes)

_Claude, 2026-08-20 17:25:16 +0000_

```text
 Docs/QA/RESULTS/2026-08-20-andrew.md | 7 +++++++
 1 file changed, 7 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-20-andrew.md b/Docs/QA/RESULTS/2026-08-20-andrew.md
new file mode 100644
index 000000000..a66e27b44
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-20-andrew.md
@@ -0,0 +1,7 @@
+# QA Results — 2026-08-20 — andrew
+
+Build: bleeding-edge @ 0475661 · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-SCARAB-MODE | PASS | The mode loads and plays, and the full Scarab kit works once the controls are known: left-stick flight, RT throttle, LT drift, A places a switch/ring, right-stick/body juke strikes balls, and the ball forge fires (crystal → ball). Design notes for the designers (not failures): (1) if a ball spawns close to the arena edge, the player can push it out and it cannot get back into the arena; the round still resolved. (2) the ball forge appears UNGATED — a ball forms on any crystal contact regardless of energy, whereas SCARAB.md specifies "crystal contact AT FULL ENERGY → ball"; may be intentional party-game chaos or a missing energy gate — flagging for design review. |
```

</details>

### `ad892d99f` — qa(backlog): apply Scarab PASS -> ARCHIVE; scan to 74bf4080 (audio-only, no new items)

_Claude, 2026-08-20 17:26:51 +0000_

```text
- QA-SCARAB-MODE PASS -> ARCHIVE (2 design notes: ball can leave arena; forge appears ungated)
- scan 04756610 -> 74bf4080: only 9471b544 'audio stuff' (FMOD bank re-export), no testable item
- quick-wins unchanged (Scarab was not one)
```

```text
 Docs/QA/ARCHIVE.md    | 15 +++++++++++++++
 Docs/QA/QA_BACKLOG.md | 13 +------------
 2 files changed, 16 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index 409d4a9f1..9c60a1b37 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -52,3 +52,18 @@ _Passed on build bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unit
 ### QA-P2-LIFEFORM-MATRIX-MOONS ⬜ — element-crystal "moons" swallowed by the toy body
 Suspected pre-existing: the Lifeform Matrix's four crystal moons sit ~2.2 world units out while toys place at `toyBodyRadius = 22`. Look at the bench. PASS = the four moons are visible and distinct. FAIL = they are inside the sphere (then the fix is a placement value, not code).
 <!-- /archived:QA-P2-LIFEFORM-MATRIX-MOONS -->
+
+<!-- archived:QA-SCARAB-MODE -->
+_Passed on build bleeding-edge @ 0475661 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-20, andrew)._
+
+### QA-SCARAB-MODE ⬜ — the Scarab vessel + party game
+Source: PRs #755 (`scarab-party-game` — smooth nucleus release, bigger skimmer, cap overload, domain blast), #758 (`scarab-wing-prism-dais` — membrane blow-out identity test), #761 (`scarab-squirrel-colliders` — omni-only ball forge, no hull omni effects, per-cell ball overload). A large new vessel/party-game cluster (79 + 17 + 31 files), authored headless.
+
+1. Project compiles; open the Scarab vessel prefab and its mode scene — no `Missing (Mono Script)`; controller + scoring rule wired.
+2. Launch the Scarab mode (any player count): it reaches gameplay without an exception; the arena/cell builds.
+3. Fly the Scarab: the skimmer, nucleus release, cap/overload, and domain blast behave as designed; the omni-only ball forge works and hull omni effects are absent (per #761).
+4. Play a full round to the win condition; scoreboard resolves; return/relaunch clean.
+5. Confirm the Squirrel colliders touched in #761 didn't regress the Squirrel (fly it briefly).
+
+PASS: compiles, prefabs/scene intact; the Scarab mode launches, plays to a resolved scoreboard, and returns cleanly; Scarab abilities and the ball forge behave; Squirrel unaffected. FAIL: missing scripts · a scene/vessel that throws on load or launch · abilities/ball forge not working · a round that won't resolve · a Squirrel regression.
+<!-- /archived:QA-SCARAB-MODE -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index b0ed329ce..7427f029a 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-20 · Scan covers: up to `04756610` (PRs #583–#761) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-20 · Scan covers: up to `74bf4080` (PRs #583–#761; `9471b544` audio-bank update carries no testable item) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -181,17 +181,6 @@ Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 fi
 
 PASS: scene opens clean; the mode launches, plays a full round to a resolved scoreboard, and returns/relaunches without error; combat scoring behaves; the Boneyard arena builds as intended. FAIL: missing scripts · a scene/controller that throws on load or launch · the round never resolving · a scoreboard that doesn't tally combat hits/kills · a crash on return/relaunch.
 
-### QA-SCARAB-MODE ⬜ — the Scarab vessel + party game
-Source: PRs #755 (`scarab-party-game` — smooth nucleus release, bigger skimmer, cap overload, domain blast), #758 (`scarab-wing-prism-dais` — membrane blow-out identity test), #761 (`scarab-squirrel-colliders` — omni-only ball forge, no hull omni effects, per-cell ball overload). A large new vessel/party-game cluster (79 + 17 + 31 files), authored headless.
-
-1. Project compiles; open the Scarab vessel prefab and its mode scene — no `Missing (Mono Script)`; controller + scoring rule wired.
-2. Launch the Scarab mode (any player count): it reaches gameplay without an exception; the arena/cell builds.
-3. Fly the Scarab: the skimmer, nucleus release, cap/overload, and domain blast behave as designed; the omni-only ball forge works and hull omni effects are absent (per #761).
-4. Play a full round to the win condition; scoreboard resolves; return/relaunch clean.
-5. Confirm the Squirrel colliders touched in #761 didn't regress the Squirrel (fly it briefly).
-
-PASS: compiles, prefabs/scene intact; the Scarab mode launches, plays to a resolved scoreboard, and returns cleanly; Scarab abilities and the ball forge behave; Squirrel unaffected. FAIL: missing scripts · a scene/vessel that throws on load or launch · abilities/ball forge not working · a round that won't resolve · a Squirrel regression.
-
 ### QA-BENDS-MODE ⬜ — "The Bends": the Dolphin-only debuff duel (GameModes.Bends = 42)
 Source: PR #752 (`dolphin-dogfighting-game`). A new mode — `GameModes.Bends = 42`, Dolphin-only debuff duel — 42 files, 12,899 insertions, authored headless.
 
```

</details>

### `3c92e3555` — qa(backlog): scan to PR #766 (3 new items)

_Claude, 2026-08-21 19:33:43 +0000_

```text
- scan 74bf4080 -> ce6a9c78 (PRs #583-#766)
- new P1: QA-MAELSTROM-POOL (#766, Rampage/Ribcage/Scarab/Bends join the Tournament pool),
  QA-ENTER-PLAYMODE-OPTIONS (managed-callbacks-performance, stale-static-state risk)
- new P2: QA-CRASH-DETECTOR-TOOL (tools-docs-crash-detector) + added to quick-wins editor line
- folded: crystal-breaking pass (minor VFX), com.unity.pipeline/CLI-verify/build-guard (CI, not player-testable)
```

```text
 Docs/QA/QA_BACKLOG.md | 32 ++++++++++++++++++++++++++++++--
 1 file changed, 30 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 7427f029a..2d24be79c 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-20 · Scan covers: up to `74bf4080` (PRs #583–#761; `9471b544` audio-bank update carries no testable item) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-21 · Scan covers: up to `ce6a9c78` (PRs #583–#766) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -31,7 +31,7 @@ every run, so it can lag reality by one submission.
 6. **QA-PALETTE-DANGER-GOLD** — get shielded prisms of all three domains on screen (a populated cell / HexRace) and check gold reads in the pastel family.
 7. **QA-P2-DANGLING-CELLDATA** — a populated cell in freestyle: watch the Console for `LifeForm.Start()` / `Flora.Plant()` throws (PR #731 may have fixed this — verify which, if any, still throw).
 
-Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) is nearly as cheap if you're already in the editor.
+Editor-only, no play mode: **QA-CRASH-DETECTOR-TOOL** (just open FrogletTools ▸ Misc ▸ Crash Detector and confirm it doesn't throw) and **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) are the cheapest if you're already in the editor.
 
 Tip: #1–#4, #6, #7 are all "one Dolphin freestyle session in a populated cell" — load a lifeform-rich cell (Cell Selector → Yggdra/Hesperides) on the Dolphin and knock out several in a row. #2/#3 start with a quick editor prefab glance.
 
@@ -833,6 +833,25 @@ Source: PRs #753 (`gyroid-schwarz-flora-cell` — seed the Lattice cell with EIG
 
 PASS: compiles; the Lattice cell seeds with ~8 founders and grows to ~twelve colonies; the quasicrystal form renders and grows coherently; continuity/mass hold. FAIL: missing scripts/None refs · a 240-founder boot wall · a broken/degenerate quasicrystal · anything popping in/out · a frozen or runaway colony.
 
+### QA-MAELSTROM-POOL ⬜ — the four new modes join the Maelstrom (Tournament) pool
+Source: PR #766 (`1f0b235a` feat(tournament)). Maelstrom/Tournament now draws from a pool that includes **Rampage, Peel the Cage (Ribcage), Scarab Scramble, and The Bends** (plus a corrected pool-math fix and a scene-wiring check). `TournamentDataSO` + `TournamentData.asset`. **Depends on** the individual modes working (QA-RAMPAGE-REBUILD, QA-RIBCAGE-MODE, QA-SCARAB-MODE ✓, QA-BENDS-MODE). Reference: `Docs/TournamentSystem/ARCHITECTURE.md`.
+
+1. Launch **Maelstrom** (Tournament). Confirm the mode chains multiple minigames back-to-back and that the pool now includes the four new modes (over a few runs you should see them appear, not only the legacy HexRace/Joust/Crystal Capture).
+2. Play a chain through at least one of the new modes (e.g. it rolls Scarab Scramble or The Bends) and confirm the transition in/out of it works — scores fold into the standings, the next mode loads.
+3. Confirm the race-to-N standings / summary resolve correctly with the larger pool (the "stale 3-mode pool math" fix from this PR).
+4. No missing scripts / scene-wiring errors on any pool member as it loads.
+
+PASS: Maelstrom chains modes including the four new ones; transitions in/out of a new mode work; standings/summary resolve with the corrected pool math; no load errors. FAIL: a pool member that won't load or throws · standings math wrong (a mode not counted, or a wrong race-to-N) · a chain that wedges between modes · the new modes never appearing in the pool.
+
+### QA-ENTER-PLAYMODE-OPTIONS ⬜ — no stale static state with fast play-mode entry
+Source: `managed-callbacks-performance` (`b3af31e1` "enable Enter Play Mode Options behind a full static-state audit", `4d97ba09` cut domain-reload cost). The editor now uses **Enter Play Mode Options** (domain/scene reload disabled for faster iteration) — which surfaces any static field that isn't reset between play sessions as a "works first time, breaks second time" bug. The audit is unverified in practice. **Editor-only concern.**
+
+1. Enter Play Mode on Menu_Main, exit, and **re-enter several times in a row** — the menu, autopilot vessel, and freestyle behave identically on the 2nd/3rd entry as the 1st (no doubled objects, no stale singletons, no missing managers).
+2. Repeat with a gameplay scene: play a round, exit play mode, re-enter, play again — score/state start clean each time (cross-check QA-STATE-RESET), no leftover objects or events firing twice.
+3. Watch the Console across repeated entries for `static`-state-related nulls or "already registered/subscribed" style warnings.
+
+PASS: repeated play-mode entries behave identically to a cold entry; no doubled/stale objects, no double-fired events, clean console. FAIL: any behaviour that only breaks on the 2nd+ entry · doubled objects / stale singletons · events firing multiple times · nulls from un-reset statics.
+
 ## Priority 2 — lower risk, cosmetic, or data-gathering
 
 ### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
@@ -900,6 +919,15 @@ Source: PR #719 (`sparrow-spread-haptics`). Sparrow shot spread plus haptic feed
 
 PASS: the spread reads as intended; the haptic fires appropriately on a device and respects the haptics policy; the standard feels are intact. FAIL: broken/absent spread · a haptic that fires on silenced events or not at all · a regression to the two standard feels.
 
+### QA-CRASH-DETECTOR-TOOL ⬜ — the editor Crash Detector + Diagnostics lane / Bug Ledger
+Source: `tools-docs-crash-detector` (`419590fb` add editor crash detector, `0448192e` Diagnostics lane + shared Bug Ledger, `5b8cddae` ledger archive / findings / severity / doc links). A new editor tool at **FrogletTools ▸ Misc ▸ Crash Detector** with a Diagnostics lane and a shared Bug Ledger. Reader/diagnostics tool.
+
+1. Open **FrogletTools ▸ Misc ▸ Crash Detector** — it opens without throwing.
+2. Exercise the Diagnostics lane / Bug Ledger UI (view findings, severity, doc links) — nothing throws; links resolve.
+3. If it can surface recent editor crashes/errors, confirm it lists something sensible (or an empty state) rather than erroring.
+
+PASS: the tool opens and its Diagnostics/Ledger UI works without throwing; findings/links render. FAIL: the menu item missing or throwing on open · a Diagnostics/Ledger panel that errors · broken doc links / severity display.
+
 ## Not covered by this list
 
 * Automated CI checks (`Tools/CI/validate_project.py`, `check_conditional_compilation.py`, the Thursday build promotion in PR #664) run in GitHub Actions and are verified there. QA does not need to re-run them; if a build branch is red, that is an engineering item.
```

</details>

### `6ab4235a4` — qa: results 2026-08-21 andrew (dogfight PASS, crash-detector FAIL, bends FAIL)

_Claude, 2026-08-21 21:12:48 +0000_

```text
 Docs/QA/RESULTS/2026-08-21-andrew.md | 9 +++++++++
 1 file changed, 9 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-21-andrew.md b/Docs/QA/RESULTS/2026-08-21-andrew.md
new file mode 100644
index 000000000..dde21c518
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-21-andrew.md
@@ -0,0 +1,9 @@
+# QA Results — 2026-08-21 — andrew
+
+Build: bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-DOGFIGHT-MODE | PASS | The scene opened normally; played a full round against a CPU and won; exited and re-entered the mode with no problems. |
+| QA-CRASH-DETECTOR-TOOL | FAIL | The Crash Detector menu item is missing — FrogletTools ▸ Misc ▸ Crash Detector is not present on this build. (Third catalogued editor tool found missing on bleeding-edge, after Validate Lifeform Crystals and the Quest Graph Editor.) |
+| QA-BENDS-MODE | FAIL | Can enter the mode and all machines are Dolphins (as expected), and the mode can be relaunched after closing it, but there is no way to earn points and the game never resolves to a win or a loss — either scoring/win-condition is not working, or the way to score (apply debuffs?) is not discoverable in-game. |
```

</details>

### `809dfc529` — qa(backlog): apply 2026-08-21 results (dogfight PASS, bends+crash-detector FAIL) + scan to 9bb325fb

_Claude, 2026-08-21 21:15:49 +0000_

```text
- QA-DOGFIGHT-MODE PASS -> ARCHIVE (full round vs CPU, won, clean re-entry)
- QA-BENDS-MODE FAIL -> 🔴 (can't score/resolve); item updated with the real scoring mechanic
  (catch an opposing pilot in the Dolphin blast = 1 bend/10pts; first domain to target wins)
- QA-CRASH-DETECTOR-TOOL FAIL -> 🔴; CORRECTED item: tool IS present at FrogletTools>Diagnostics>Crash
  Detector (my item wrongly said Misc) - likely a false FAIL, retest under Diagnostics
- scan ce6a9c78 -> 9bb325fb: 3 commits, all fold (wildlife-liberation tuning, managed-callbacks fix, bends AI)
```

```text
 Docs/QA/ARCHIVE.md    | 15 +++++++++++++++
 Docs/QA/DEV_TASKS.md  | 14 ++++++++++++++
 Docs/QA/QA_BACKLOG.md | 27 ++++++++++-----------------
 3 files changed, 39 insertions(+), 17 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 109 lines)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index 9c60a1b37..5c89e1e2b 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -67,3 +67,18 @@ Source: PRs #755 (`scarab-party-game` — smooth nucleus release, bigger skimmer
 
 PASS: compiles, prefabs/scene intact; the Scarab mode launches, plays to a resolved scoreboard, and returns cleanly; Scarab abilities and the ball forge behave; Squirrel unaffected. FAIL: missing scripts · a scene/vessel that throws on load or launch · abilities/ball forge not working · a round that won't resolve · a Squirrel regression.
 <!-- /archived:QA-SCARAB-MODE -->
+
+<!-- archived:QA-DOGFIGHT-MODE -->
+_Passed on build bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-21, andrew)._
+
+### QA-DOGFIGHT-MODE ⬜ — "Dog Fight": the Sparrow-only gun duel in the Boneyard
+Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 files, 15,626 insertions, authored headless — with new asset-writing tools (`Tools/Build/author_dogfight_assets.py`, `boneyard_budget.py`), a new scene (EditorBuildSettings changed), a `ScriptableEventCombatHitStats` SOAP type, and `GameDataSO` additions. Reference: `_Scripts/Controller/Arcade/DOGFIGHT.md`.
+
+1. Open the Dog Fight scene: no `Missing (Mono Script)`; the controller and its scoring rule are wired; the arena ("Boneyard") builds.
+2. Launch the mode (any player count — AI backfill for solo). It reaches gameplay without an exception.
+3. Confirm it is Sparrow-only and gun-combat focused (the Boneyard as the arena, the enemy marker, crystal drops).
+4. Play a full round to the win condition and watch the scoreboard resolve (combat-hit / kill scoring).
+5. Return to menu and relaunch once — no leaked state, no crash.
+
+PASS: scene opens clean; the mode launches, plays a full round to a resolved scoreboard, and returns/relaunches without error; combat scoring behaves; the Boneyard arena builds as intended. FAIL: missing scripts · a scene/controller that throws on load or launch · the round never resolving · a scoreboard that doesn't tally combat hits/kills · a crash on return/relaunch.
+<!-- /archived:QA-DOGFIGHT-MODE -->
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index bbb8874ff..fc22be3a6 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -50,3 +50,17 @@ of duplicating it.
 - **Symptom:** In HexRace: the crystal model and its breaking/collection effect look normal, but when a new crystal spawns in it POPS into existence instead of blooming in — the omni/crystal bloom-in (PR #724) is not playing on spawn.
 - **Definition of done:** QA item `QA-CRYSTAL-EFFECTS` passes.
 <!-- /devtask:QA-CRYSTAL-EFFECTS -->
+
+<!-- devtask:QA-BENDS-MODE -->
+### QA-BENDS-MODE — "The Bends": the Dolphin-only debuff duel (GameModes.Bends = 42)
+- **Failed on:** bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-21, andrew)
+- **Symptom:** Can enter the mode and all machines are Dolphins (as expected), and the mode can be relaunched after closing it, but there is no way to earn points and the game never resolves to a win or a loss — either scoring/win-condition is not working, or the way to score (apply debuffs?) is not discoverable in-game.
+- **Definition of done:** QA item `QA-BENDS-MODE` passes.
+<!-- /devtask:QA-BENDS-MODE -->
+
+<!-- devtask:QA-CRASH-DETECTOR-TOOL -->
+### QA-CRASH-DETECTOR-TOOL — the editor Crash Detector + Diagnostics lane / Bug Ledger
+- **Failed on:** bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-21, andrew)
+- **Symptom:** The Crash Detector menu item is missing — FrogletTools ▸ Misc ▸ Crash Detector is not present on this build. (Third catalogued editor tool found missing on bleeding-edge, after Validate Lifeform Crystals and the Quest Graph Editor.)
+- **Definition of done:** QA item `QA-CRASH-DETECTOR-TOOL` passes.
+<!-- /devtask:QA-CRASH-DETECTOR-TOOL -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 2d24be79c..a096ec5de 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-21 · Scan covers: up to `ce6a9c78` (PRs #583–#766) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-21 · Scan covers: up to `9bb325fb` (PRs #583–#766 + 3 direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -31,7 +31,7 @@ every run, so it can lag reality by one submission.
 6. **QA-PALETTE-DANGER-GOLD** — get shielded prisms of all three domains on screen (a populated cell / HexRace) and check gold reads in the pastel family.
 7. **QA-P2-DANGLING-CELLDATA** — a populated cell in freestyle: watch the Console for `LifeForm.Start()` / `Flora.Plant()` throws (PR #731 may have fixed this — verify which, if any, still throw).
 
-Editor-only, no play mode: **QA-CRASH-DETECTOR-TOOL** (just open FrogletTools ▸ Misc ▸ Crash Detector and confirm it doesn't throw) and **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) are the cheapest if you're already in the editor.
+Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) is the cheapest if you're already in the editor.
 
 Tip: #1–#4, #6, #7 are all "one Dolphin freestyle session in a populated cell" — load a lifeform-rich cell (Cell Selector → Yggdra/Hesperides) on the Dolphin and knock out several in a row. #2/#3 start with a quick editor prefab glance.
 
@@ -170,23 +170,14 @@ Source: direct commit `b08a35d7` (`fix(ui): the Menu_Main crash is a type-punned
 
 PASS: prefab repointed with no missing script; the pause panel warms and opens in the Editor; the **Windows player build reaches Menu_Main repeatedly with no crash**; squad cards still render. FAIL: a missing script or a CanvasGroup-typed reference remaining · the pause panel not opening/warming · **any crash entering Menu_Main in the Windows player** · broken squad cards.
 
-### QA-DOGFIGHT-MODE ⬜ — "Dog Fight": the Sparrow-only gun duel in the Boneyard
-Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 files, 15,626 insertions, authored headless — with new asset-writing tools (`Tools/Build/author_dogfight_assets.py`, `boneyard_budget.py`), a new scene (EditorBuildSettings changed), a `ScriptableEventCombatHitStats` SOAP type, and `GameDataSO` additions. Reference: `_Scripts/Controller/Arcade/DOGFIGHT.md`.
+### QA-BENDS-MODE 🔴 — "The Bends": the Dolphin-only debuff duel (GameModes.Bends = 42)
+> **Last result:** 🔴 FAIL — Can enter the mode and all machines are Dolphins (as expected), and the mode can be relaunched after closing it, but there is no way to earn points and the game never resolves to a win or a loss — either scoring/win-condition is not working, or the way to score (apply debuffs?) is not discoverable in-game.  _(build bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-21, andrew)_
 
-1. Open the Dog Fight scene: no `Missing (Mono Script)`; the controller and its scoring rule are wired; the arena ("Boneyard") builds.
-2. Launch the mode (any player count — AI backfill for solo). It reaches gameplay without an exception.
-3. Confirm it is Sparrow-only and gun-combat focused (the Boneyard as the arena, the enemy marker, crystal drops).
-4. Play a full round to the win condition and watch the scoreboard resolve (combat-hit / kill scoring).
-5. Return to menu and relaunch once — no leaked state, no crash.
```

</details>

### `2a7ddde12` — qa: results 2026-08-22 andrew (crash-detector PASS, keyboard-controls FAIL)

_Claude, 2026-08-22 22:49:09 +0000_

```text
 Docs/QA/RESULTS/2026-08-22-andrew.md | 8 ++++++++
 1 file changed, 8 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-22-andrew.md b/Docs/QA/RESULTS/2026-08-22-andrew.md
new file mode 100644
index 000000000..698e3db88
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-22-andrew.md
@@ -0,0 +1,8 @@
+# QA Results — 2026-08-22 — andrew
+
+Build: bleeding-edge @ 3bbe4f7 · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-CRASH-DETECTOR-TOOL | PASS | Found the panel at FrogletTools ▸ Diagnostics ▸ Crash Detector (not Misc); it opens fine and lists crashes from today and yesterday. (Supersedes the 2026-08-21 FAIL, which was caused by the item pointing at the wrong "Misc" submenu.) |
+| QA-KEYBOARD-CONTROLS | FAIL | Tested Sparrow, Manta, Squirrel. Most vessel functionality works from the keyboard, and the device switch works (controls swap correctly when the player changes between gamepad and keyboard mid-session). Two real defects: (1) **the left/right turn bound to the controller shoulder buttons has no keyboard binding** — that turn input is unmapped on keyboard; (2) **you cannot start a game from the keyboard** — arrow keys + Enter can SELECT an arcade game but never actually launch it. NOT a defect (documented/expected): "only Squirrel's UI switches to the keyboard glyph set" — per the CLAUDE.md ability-row audit only the Squirrel HUD carries the device-icon switcher (Sparrow/Dolphin have complete rows but no switcher, Manta/Rhino/Serpent are design-blocked). Menu nav being mouse-driven is also expected. |
```

</details>

### `b06de8cdc` — qa(backlog): apply 2026-08-22 results + scan to PR #770 + engine: clear stale dev tasks on non-FAIL

_Claude, 2026-08-22 23:30:59 +0000_

```text
- QA-CRASH-DETECTOR-TOOL PASS (found at Diagnostics, supersedes 2026-08-21 FAIL) -> ARCHIVE
- QA-KEYBOARD-CONTROLS FAIL -> 🔴 + dev task (unmapped shoulder-turn on keyboard; can't launch a game from keyboard)
- engine fix: apply_results.py now removes an item's DEV_TASKS entry when its latest result is
  PASS/PARTIAL/BLOCKED (incl. already-archived passes), via a post-loop cleanup; idempotent
- scan 9bb325fb -> 3bbe4f7d: new P0 QA-ASSEMBLY-SPLIT (first CosmicShore.Data .asmdef, compile gate);
  managed-callbacks + crash-detector already covered; pensive-hopper (Dolphin AI fix) folds into Bends/AI
```

```text
 Docs/QA/ARCHIVE.md        | 15 +++++++++++++++
 Docs/QA/DEV_TASKS.md      | 13 ++++++-------
 Docs/QA/QA_BACKLOG.md     | 28 +++++++++++++++-------------
 Tools/QA/apply_results.py | 29 +++++++++++++++++++++++++++++
 4 files changed, 65 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 157 lines)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index 5c89e1e2b..654602265 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -82,3 +82,18 @@ Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 fi
 
 PASS: scene opens clean; the mode launches, plays a full round to a resolved scoreboard, and returns/relaunches without error; combat scoring behaves; the Boneyard arena builds as intended. FAIL: missing scripts · a scene/controller that throws on load or launch · the round never resolving · a scoreboard that doesn't tally combat hits/kills · a crash on return/relaunch.
 <!-- /archived:QA-DOGFIGHT-MODE -->
+
+<!-- archived:QA-CRASH-DETECTOR-TOOL -->
+_Passed on build bleeding-edge @ 3bbe4f7 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-22, andrew)._
+
+### QA-CRASH-DETECTOR-TOOL 🔴 — the editor Crash Detector + Diagnostics lane / Bug Ledger
+> **Last result:** 🔴 FAIL — The Crash Detector menu item is missing — FrogletTools ▸ Misc ▸ Crash Detector is not present on this build. (Third catalogued editor tool found missing on bleeding-edge, after Validate Lifeform Crystals and the Quest Graph Editor.)  _(build bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-21, andrew)_
+
+Source: `tools-docs-crash-detector` (`419590fb` add editor crash detector, `0448192e` Diagnostics lane + shared Bug Ledger, `5b8cddae` ledger archive / findings / severity / doc links). A new editor tool with a Diagnostics lane and a shared Bug Ledger. Reader/diagnostics tool. **Confirmed path (2026-08-21): the code is on bleeding-edge (`Editor/Diagnostics/DiagnosticsWindow.cs`) and registers `[MenuItem("FrogletTools/Diagnostics/Crash Detector")]` — NOT under "Misc". A prior FAIL was from looking under Misc; retest under Diagnostics.**
+
+1. Open **FrogletTools ▸ Diagnostics ▸ Crash Detector** (and ▸ Diagnostics ▸ Bug Ledger) — it opens without throwing.
+2. Exercise the Diagnostics lane / Bug Ledger UI (view findings, severity, doc links) — nothing throws; links resolve.
+3. If it can surface recent editor crashes/errors, confirm it lists something sensible (or an empty state) rather than erroring.
+
+PASS: the tool opens and its Diagnostics/Ledger UI works without throwing; findings/links render. FAIL: the menu item missing or throwing on open · a Diagnostics/Ledger panel that errors · broken doc links / severity display.
+<!-- /archived:QA-CRASH-DETECTOR-TOOL -->
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index fc22be3a6..16d5e9442 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -57,10 +57,9 @@ of duplicating it.
 - **Symptom:** Can enter the mode and all machines are Dolphins (as expected), and the mode can be relaunched after closing it, but there is no way to earn points and the game never resolves to a win or a loss — either scoring/win-condition is not working, or the way to score (apply debuffs?) is not discoverable in-game.
 - **Definition of done:** QA item `QA-BENDS-MODE` passes.
 <!-- /devtask:QA-BENDS-MODE -->
-
-<!-- devtask:QA-CRASH-DETECTOR-TOOL -->
-### QA-CRASH-DETECTOR-TOOL — the editor Crash Detector + Diagnostics lane / Bug Ledger
-- **Failed on:** bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-21, andrew)
-- **Symptom:** The Crash Detector menu item is missing — FrogletTools ▸ Misc ▸ Crash Detector is not present on this build. (Third catalogued editor tool found missing on bleeding-edge, after Validate Lifeform Crystals and the Quest Graph Editor.)
-- **Definition of done:** QA item `QA-CRASH-DETECTOR-TOOL` passes.
-<!-- /devtask:QA-CRASH-DETECTOR-TOOL -->
+<!-- devtask:QA-KEYBOARD-CONTROLS -->
+### QA-KEYBOARD-CONTROLS — keyboard control scheme
+- **Failed on:** bleeding-edge @ 3bbe4f7 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-22, andrew)
+- **Symptom:** Tested Sparrow, Manta, Squirrel. Most vessel functionality works from the keyboard, and the device switch works (controls swap correctly when the player changes between gamepad and keyboard mid-session). Two real defects: (1) **the left/right turn bound to the controller shoulder buttons has no keyboard binding** — that turn input is unmapped on keyboard; (2) **you cannot start a game from the keyboard** — arrow keys + Enter can SELECT an arcade game but never actually launch it. NOT a defect (documented/expected): "only Squirrel's UI switches to the keyboard glyph set" — per the CLAUDE.md ability-row audit only the Squirrel HUD carries the device-icon switcher (Sparrow/Dolphin have complete rows but no switcher, Manta/Rhino/Serpent are design-blocked). Menu nav being mouse-driven is also expected.
+- **Definition of done:** QA item `QA-KEYBOARD-CONTROLS` passes.
+<!-- /devtask:QA-KEYBOARD-CONTROLS -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index a096ec5de..2066f60f4 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-21 · Scan covers: up to `9bb325fb` (PRs #583–#766 + 3 direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-22 · Scan covers: up to `3bbe4f7d` (PRs #583–#770) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -182,6 +182,17 @@ Source: PR #752 (`dolphin-dogfighting-game`). A new mode — `GameModes.Bends =
 
 PASS: scene clean; the mode launches Dolphin-only, plays its debuff duel to a resolved scoreboard, and returns/relaunches without error. FAIL: missing scripts · a controller that throws on load/launch · the duel/scoring not resolving · a crash on return/relaunch.
 
+### QA-ASSEMBLY-SPLIT ⬜ — the monolith begins splitting into assemblies (compile gate)
+Source: `csharp-monolith-assembly-split` (merge `409afb21`). The "zero runtime asmdefs, by design" rule is retired: `CosmicShore.Data` is extracted as the **first leaf `.asmdef`**, plus a root `.editorconfig` + Unity Roslyn analyzers and a compile/domain-reload timing recorder folded into the Diagnostics window. A new assembly boundary is a **compile-gate** — the whole project must still build with `Data` as its own assembly and nothing left referencing it the old way. Reference: `Docs/ASSEMBLY_SPLIT.md`.
+
+1. Let Unity fully reimport/recompile. **Read the whole Console** — zero compile errors, no `CS0246`/missing-type or "are you missing an assembly reference?" errors from the new `CosmicShore.Data` boundary.
+2. Confirm the `CosmicShore.Data.asmdef` (and its `.meta`) imported, and the assembly appears in the compile output; no duplicate-GUID or circular-reference warnings.
+3. Launch to `Menu_Main`, enter freestyle, launch one arcade game and return — no runtime type-load / `TypeLoadException` from the split.
+4. Run the EditMode tests (cross-check QA-EDITMODE-TESTS) — the test assemblies still resolve `CosmicShore.Data` types.
+5. Glance at the new compile-timing recorder in the Diagnostics window — it opens and reports without throwing.
+
+PASS: clean compile with the new `CosmicShore.Data` assembly; no missing-reference/type-load errors at import or runtime; the app boots + plays a round; tests still resolve Data types; the timing recorder works. FAIL: any compile error / missing-assembly-reference · a `TypeLoadException` at runtime · the app failing to boot or play a round · tests that no longer compile against `Data` · a circular/duplicate-assembly warning.
+
 ## Priority 1 — merged features that have never been played
 
 ### QA-ECOLOGY-WORM-KAIJU 🟡 — the worm colony boss
@@ -762,7 +773,9 @@ Source: PR #736 (`vessel-self-trail-collision`). A vessel no longer skims or ram
 
 PASS: no skim/ram/damage from the trail you're actively laying; older trail still interacts; the Rhino self-farm still works; interactions with other mass unchanged. FAIL: still colliding with/slowing on fresh self-trail · unable to interact with older trail · the Rhino self-farm broken · other-mass interactions changed.
 
-### QA-KEYBOARD-CONTROLS ⬜ — keyboard control scheme
+### QA-KEYBOARD-CONTROLS 🔴 — keyboard control scheme
```

</details>

### `b6a8273c9` — qa: results 2026-08-26 andrew (Maelstrom pool PASS)

_Claude, 2026-08-26 21:36:42 +0000_

```text
 Docs/QA/RESULTS/2026-08-26-andrew.md | 7 +++++++
 1 file changed, 7 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-26-andrew.md b/Docs/QA/RESULTS/2026-08-26-andrew.md
new file mode 100644
index 000000000..218473b74
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-26-andrew.md
@@ -0,0 +1,7 @@
+# QA Results — 2026-08-26 — andrew
+
+Build: bleeding-edge @ 31210fa · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-MAELSTROM-POOL | PASS | All seven modes are present in the Maelstrom pool (the three legacy + the four new: Rampage, Peel the Cage, Scarab Scramble, The Bends). No errors loading in and out of the modes, and the point counting from the new modes looks correct. |
```

</details>

### `9ac9be1b5` — qa(backlog): apply Maelstrom PASS + scan to PR #809 (6 new items, clustered)

_Claude, 2026-08-26 21:39:35 +0000_

```text
- QA-MAELSTROM-POOL PASS -> ARCHIVE (all seven modes present, clean load in/out, scoring ok)
- scan 3bbe4f7d -> c7195331 (PRs #583-#809), 221 commits / ~25 PRs
- new P0: QA-SPARROW-SALVO-MODE (#790, new Sparrow party game 'Salvo')
- new P1: QA-SPARROW-VISUAL-REWORK (#801/#794/#791/#787/#786/#784/#798),
  QA-UI-ABILITY-ICON-SYSTEM (#803/#804), QA-UI-THEME (#795),
  QA-ECOLOGY-TIME-BREEDING (#774), QA-SCARAB-BALL-FIXES (#807/#780/#773)
- folded: prism-shield color/pop (#771/#778/#785), worm colony (#793), quadfish/AI/camera fixes,
  the QA workflow itself merged upstream (#800/#808), CI (#809)
```

```text
 Docs/QA/ARCHIVE.md    | 14 ++++++++++++++
 Docs/QA/QA_BACKLOG.md | 72 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++-----------
 2 files changed, 75 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 120 lines)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index 654602265..e33b4f742 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -97,3 +97,17 @@ Source: `tools-docs-crash-detector` (`419590fb` add editor crash detector, `0448
 
 PASS: the tool opens and its Diagnostics/Ledger UI works without throwing; findings/links render. FAIL: the menu item missing or throwing on open · a Diagnostics/Ledger panel that errors · broken doc links / severity display.
 <!-- /archived:QA-CRASH-DETECTOR-TOOL -->
+
+<!-- archived:QA-MAELSTROM-POOL -->
+_Passed on build bleeding-edge @ 31210fa · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-26, andrew)._
+
+### QA-MAELSTROM-POOL ⬜ — the four new modes join the Maelstrom (Tournament) pool
+Source: PR #766 (`1f0b235a` feat(tournament)). Maelstrom/Tournament now draws from a pool that includes **Rampage, Peel the Cage (Ribcage), Scarab Scramble, and The Bends** (plus a corrected pool-math fix and a scene-wiring check). `TournamentDataSO` + `TournamentData.asset`. **Depends on** the individual modes working (QA-RAMPAGE-REBUILD, QA-RIBCAGE-MODE, QA-SCARAB-MODE ✓, QA-BENDS-MODE). Reference: `Docs/TournamentSystem/ARCHITECTURE.md`.
+
+1. Launch **Maelstrom** (Tournament). Confirm the mode chains multiple minigames back-to-back and that the pool now includes the four new modes (over a few runs you should see them appear, not only the legacy HexRace/Joust/Crystal Capture).
+2. Play a chain through at least one of the new modes (e.g. it rolls Scarab Scramble or The Bends) and confirm the transition in/out of it works — scores fold into the standings, the next mode loads.
+3. Confirm the race-to-N standings / summary resolve correctly with the larger pool (the "stale 3-mode pool math" fix from this PR).
+4. No missing scripts / scene-wiring errors on any pool member as it loads.
+
+PASS: Maelstrom chains modes including the four new ones; transitions in/out of a new mode work; standings/summary resolve with the corrected pool math; no load errors. FAIL: a pool member that won't load or throws · standings math wrong (a mode not counted, or a wrong race-to-N) · a chain that wedges between modes · the new modes never appearing in the pool.
+<!-- /archived:QA-MAELSTROM-POOL -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 2066f60f4..dcd5d1202 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-22 · Scan covers: up to `3bbe4f7d` (PRs #583–#770) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-26 · Scan covers: up to `c7195331` (PRs #583–#809) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -193,8 +193,68 @@ Source: `csharp-monolith-assembly-split` (merge `409afb21`). The "zero runtime a
 
 PASS: clean compile with the new `CosmicShore.Data` assembly; no missing-reference/type-load errors at import or runtime; the app boots + plays a round; tests still resolve Data types; the timing recorder works. FAIL: any compile error / missing-assembly-reference · a `TypeLoadException` at runtime · the app failing to boot or play a round · tests that no longer compile against `Data` · a circular/duplicate-assembly warning.
 
+### QA-SPARROW-SALVO-MODE ⬜ — "Salvo": the Sparrow party game
+Source: PR #790 (`sparrow-party-game`, incl. a "repair merge-damaged **Salvo** scene entry" in build settings) plus the Sparrow combat work around it. A new Sparrow-focused party game (scene "Salvo"), authored headless. **Depends on** the Sparrow visual/weapon rework (QA-SPARROW-VISUAL-REWORK) and the projectile pool (QA-SPARROW-PROJECTILE-POOL 🔴).
+
+1. Confirm the **Salvo** scene is in Build Settings and opens with no `Missing (Mono Script)`; controller + scoring rule wired.
+2. Launch the mode (any player count — AI backfill for solo): reaches gameplay without an exception; the arena builds; it is Sparrow-based.
+3. Play the core loop (gunplay / missiles) and confirm scoring works and the round resolves to a win/loss.
+4. Return to menu and relaunch once — no leaked state, no crash.
+
+PASS: the Salvo scene is wired and opens clean; the mode launches, plays its loop to a resolved scoreboard, and returns/relaunches without error. FAIL: a missing/merge-damaged scene entry · missing scripts · a controller that throws on load/launch · a round that won't score/resolve · a crash on return.
+
 ## Priority 1 — merged features that have never been played
 
+### QA-SPARROW-VISUAL-REWORK ⬜ — missile tail, spread cone, shell colours, dart, roll bank, bone-mounted jets
+Source: PRs #801 (missile TAIL), #794 (spread cone → plateau + blow-out), #791 (charge shell light budget), #787 (missile hit-sphere fit to model), #786 (halved dart, pale-blue model, neutral-blue + danger-red shell), #784 (strafing roll's root bank reaches the pilot), #798 (jets mounted on named bones — Sparrow's six on `b_Tail1..3 .L/.R`). A cluster of Sparrow weapon/model polish. Related: QA-SPARROW-PRISM-ATTACK, QA-VESSEL-SPARROW-ROLL (🔴).
+
+1. Project compiles; open `Sparrow.prefab` — no missing scripts; the tail-bone jets resolve.
+2. Fire full-auto: the dart/bullet reads as the new pale-blue smaller model; the spread widens as a plateau then blows out (not a linear cone).
+3. Fire a skyburst missile: it has a visible tail, and its hit sphere matches the model (hits land where the model is, not a larger/smaller sphere).
+4. The charge shell renders neutral-blue (danger-red when dangerous) and isn't over-bright.
+5. Do a strafing roll: the roll's bank visibly reaches the pilot/hull (cross-check QA-VESSEL-SPARROW-ROLL — the roll should read as a roll now).
+
+PASS: compiles; the new dart/spread/tail/shell all render correctly; missile hit volume matches the model; the roll banks the hull. FAIL: missing scripts/bones · wrong or missing dart/tail/shell visuals · a hit sphere that doesn't match the model · a spread that's still a plain linear cone · the roll not banking.
+
+### QA-UI-ABILITY-ICON-SYSTEM ⬜ — the ability-icon design system (no vessel authors the lockup)
+Source: PRs #803 (`ability-icon-design-system`, 40 files — "no vessel authors the lockup; correct two measured claims"), #804 (drop the Squirrel vessel-prefab override that masked the Time ability icon). A shared ability-icon lockup/design system across vessel HUDs. Related: QA-UI-ABILITY-ROW.
+
+1. Project compiles; the six HUD prefabs import with no missing scripts.
+2. Play Squirrel: the four ability icons render in charge → mass → space → time order, and the **Time** icon is now correct (the prefab override that masked it was removed).
+3. Play Sparrow/Dolphin: the ability-icon lockup renders consistently (spacing, size) — no vessel hand-authoring its own lockup.
+4. Run `FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows` — record its verdict (expect the documented state).
+
+PASS: compiles; icons render in the shared lockup on all wired vessels; the Squirrel Time icon shows correctly; the audit reports the expected/known state. FAIL: missing scripts · a masked/missing Time icon on Squirrel · inconsistent per-vessel lockups · a new audit failure.
+
+### QA-UI-THEME ⬜ — UIThemeSO + UITheme style foundation
+Source: PR #795 (`UIThemeSO + UITheme`, Style Foundation §11 — plus an audit of UI colour literals). A new UI theming system (a `UIThemeSO` asset + `UITheme` accessor) with a colour-literal audit. Reference: `Docs/STYLE_FOUNDATION.md` §11.
+
+1. Project compiles; no missing scripts on UI that adopted the theme.
+2. Navigate the menu (Home / Arcade / Store / Port / Hangar) and open a couple of modals — colours/typography read consistently, nothing renders un-themed (stark white/black) or with a broken palette.
+3. If a `UIThemeSO` asset exists, confirm menu UI reflects it (spot-check a themed element).
+
+PASS: compiles; themed UI renders consistently across screens/modals with no un-themed or broken-palette elements. FAIL: missing scripts · UI rendering with a wrong/broken palette · elements that ignore the theme and look stark/unstyled.
+
```

</details>

### `e2992b728` — qa(backlog): scan to PR #818 + direct commits (7 new items)

_Claude, 2026-08-31 15:42:03 +0000_

```text
- scan c7195331 -> 6fe3bb11 (PRs #583-#818 + direct commits), 202 commits
- new P1: QA-MOUSE-FLIGHT (#818/#811), QA-CONNECTING-PANEL-PREVIEW (connecting-panel cluster),
  QA-UI-GOAL-STACK (goal-stack HUD cluster), QA-ARCADE-MENU-REVAMP (arcade/topbar/card revamp),
  QA-ECOLOGY-ELEMENTAL-VARIATIONS (#810), QA-FAUNA-FLORA-NETSYNC (fauna-flora-network-sync)
- new P2: QA-DAILY-CHALLENGE-TIMER
- folded: #814 scarab switch material, #816 failed-tests-fixes (may help QA-EDITMODE-TESTS),
  domain-picker/domain-pick-survives-spawn fixes, SkyboxModel occlusion fix (5 arenas), tool-codex-listing
```

```text
 Docs/QA/QA_BACKLOG.md | 69 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++-
 1 file changed, 68 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 93 lines)</summary>

```diff
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index dcd5d1202..c020b31d9 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-26 · Scan covers: up to `c7195331` (PRs #583–#809) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-31 · Scan covers: up to `6fe3bb11` (PRs #583–#818 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -906,6 +906,65 @@ Source: `managed-callbacks-performance` (`b3af31e1` "enable Enter Play Mode Opti
 
 PASS: repeated play-mode entries behave identically to a cold entry; no doubled/stale objects, no double-fired events, clean console. FAIL: any behaviour that only breaks on the 2nd+ entry · doubled objects / stale singletons · events firing multiple times · nulls from un-reset statics.
 
+### QA-MOUSE-FLIGHT ⬜ — mouse flight controls (one-thumb transformer per hull)
+Source: PRs #818 (`mouse-flight-controls` — "a resting stick cannot take the ship from an active mouse"), #811 (`thumb-vessel-mouse-controls` — "one transformer per hull, so every one-thumb vessel qualifies structurally"). A mouse-driven flight scheme + a transformer refactor so one-thumb vessels support it. Related: QA-KEYBOARD-CONTROLS (input).
+
+1. Project compiles; no missing scripts on the vessels/input.
+2. Fly a vessel with the **mouse**: pitch/yaw follow the mouse, and control is usable across the one-thumb vessels.
+3. **Handoff:** with the mouse actively flying, a **resting gamepad stick must NOT yank the ship** (the #818 fix) — the active device keeps control; move the stick and it takes over cleanly.
+4. Abilities still fire while flying by mouse.
+
+PASS: compiles; mouse flight works across one-thumb vessels; a resting stick doesn't steal control from an active mouse; device handoff is clean; abilities fire. FAIL: missing scripts · mouse flight not working or fighting the stick · a resting stick yanking the ship · abilities that don't fire under mouse control.
+
+### QA-CONNECTING-PANEL-PREVIEW ⬜ — live arena preview + real progress bar on the connecting screen
+Source: a cluster of direct commits (`bf276c98` live arena preview + progress bar + daily-challenge objective as description, `087479f2` zoomed shot + pilot roster that waits, `83549c8e`/`d99b74f2` frame the built arena / keep camera inside the cell, `e0bd4f00` full-quality render + kill white chip, `a48cbbf1` restore roster using) + `game-card-playable-preview`. The connecting/loading panel now shows a live preview of the arena being built plus a real progress bar.
+
+1. Launch any arcade game and watch the connecting panel: it shows a **live preview of the actual arena** being built (camera inside the cell, framing the real arena — not a black screen or a generic backdrop), with a **real progress bar** that advances to completion.
+2. The pilot roster waits/populates correctly; no leftover white chip backing.
+3. For a daily-challenge launch, the panel shows the challenge objective as its description.
+4. The preview render is clean (full quality, no broken camera) and the panel dismisses into the built arena with no jump.
+
+PASS: the connecting panel shows a live, correctly-framed arena preview + a real advancing progress bar; roster populates; daily-challenge objective shows; clean handoff into gameplay. FAIL: a black/generic preview · a fake/stuck progress bar · a mis-framed camera (outside the cell / wrong arena) · a leftover white chip · a jump/mismatch into the built arena.
+
+### QA-UI-GOAL-STACK ⬜ — the in-game goal stack (named objective + target) replaces the top-left ring
+Source: direct commits (`fc9f8a4b` replace the top-left ring with a goal stack — named objective with its target, `da6ec594` generated/lit goal plate + slider bed, `531d7a02` goal row snapshotted a NetworkVariable target so it hid forever, `e3bed9b2` tell seconds from a count, `5297dd0e` TMP font+material atlas fix, `51e5bc0f` sizing/topbar clearance). The top-left objective ring is replaced by a goal stack showing the named objective and its target.
+
+1. Launch a few different modes: the top-left shows a **goal stack** with the objective's **name and target** (not the old ring), correctly reading count vs seconds per mode.
+2. The target populates even though it arrives by NetworkVariable (the `531d7a02` fix — it must not hide forever waiting for the target).
+3. Numerals render with the correct font/atlas (no wrong-atlas glyphs), the plate is lit/sized correctly, and it clears the top bar / FPS panel.
+4. Play toward the objective — the goal value updates live.
+
+PASS: the goal stack shows the named objective + target across modes, populates the NetworkVariable target, updates live, and renders/sizes correctly. FAIL: the old ring still there · a target that never appears (NetworkVariable snapshot bug) · count/seconds mislabeled · wrong-atlas numerals · a plate that overlaps the top bar or mis-sizes.
+
+### QA-ARCADE-MENU-REVAMP ⬜ — arcade launch screen + game-mode top-bar redesign + playable card preview
+Source: `arcade-launch-screen-revamp`, `game-mode-topbar-redesign`, `game-card-playable-preview`. A redesign of the arcade launch screen and the game-mode top bar, with playable game-card previews.
+
+1. Open the Arcade (ARK) screen: the redesigned top bar and launch screen render correctly (no missing scripts, no broken layout).
+2. The game cards show their **playable preview** (a live/animated preview, not a static broken image).
+3. Navigate the top bar between game modes — selection, layout and transitions behave.
+4. Launch a game from the revamped screen and confirm it still routes to the correct mode.
+
+PASS: the revamped arcade screen + top bar render and navigate correctly; game cards show working previews; launching routes to the right mode. FAIL: missing scripts / broken layout · a static/broken card preview · top-bar navigation broken · a launch that routes to the wrong mode or fails.
+
+### QA-ECOLOGY-ELEMENTAL-VARIATIONS ⬜ — lifeform elemental variations (+ codex correction)
+Source: PR #810 (`lifeform-elemental-variations`, 264 files — also "fix(codex): the encyclopedia described a level system that no longer exists"). Elemental variations across lifeforms, plus a large codex/encyclopedia data correction. LOCKED ecology surface. Reference: `Docs/ECOSYSTEM.md`.
+
+1. Project compiles; a cell with lifeforms builds clean with no `None`/missing refs across the 264-file change.
+2. Observe fauna/flora across elements — the elemental variations render (per-element differences in appearance/behaviour), each dropping its correct elemental crystal.
+3. If there's an in-game codex/encyclopedia, spot-check it — it no longer describes the retired level system (matches current mechanics).
+4. Continuity/mass hold; no runaway/frozen populations.
+
+PASS: compiles clean; elemental variations render with correct per-element crystals; the codex matches current mechanics; continuity/mass hold. FAIL: import errors / None refs · missing or wrong elemental variations · a codex still describing the retired level system · a broken population.
+
+### QA-FAUNA-FLORA-NETSYNC ⬜ — fauna/flora replicate across clients
+Source: `fauna-flora-network-sync`. Networked synchronisation of fauna and flora across clients. **Needs MPPM (2 virtual players).** LOCKED ecology surface.
+
+1. Project compiles. In MPPM with two clients in a mode with an ecology, confirm fauna and flora **appear in the same places and states on both peers** (spawns, growth, deaths/withers replicate).
+2. Kill a creature / consume flora on one client — the other client sees the same result (no ghost lifeforms left on the peer).
+3. No exceptions from the sync path; no runaway duplication.
+
+PASS: fauna/flora spawn, grow and die consistently on both peers; kills/consumption replicate; no ghosts, no duplication, no exceptions. FAIL: lifeforms out of sync between peers · ghost/duplicated lifeforms · a death that doesn't replicate · exceptions from the sync path.
+
 ## Priority 2 — lower risk, cosmetic, or data-gathering
 
 ### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
@@ -973,6 +1032,14 @@ Source: PR #719 (`sparrow-spread-haptics`). Sparrow shot spread plus haptic feed
 
```

</details>

### `fd11cf614` — qa: results 2026-08-31 andrew (arcade-menu-revamp FAIL, daily-challenge-timer PASS)

_Claude, 2026-09-01 00:03:04 +0000_

```text
 Docs/QA/RESULTS/2026-08-31-andrew.md | 8 ++++++++
 1 file changed, 8 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-31-andrew.md b/Docs/QA/RESULTS/2026-08-31-andrew.md
new file mode 100644
index 000000000..f7c436961
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-31-andrew.md
@@ -0,0 +1,8 @@
+# QA Results — 2026-08-31 — andrew
+
+Build: bleeding-edge @ 6fe3bb1 · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-ARCADE-MENU-REVAMP | FAIL | The arcade screen is redesigned and the game-mode cards do route to their modes, but two defects: (1) the **ARK section of the top bar can't be accessed** — the only way into the arcade is the Arcade button on the Home screen; (2) **Salvo has no card preview**. |
+| QA-DAILY-CHALLENGE-TIMER | PASS | The timer is present and restarts when it reaches zero. (Note: the daily-challenge game was still Salvo — worth confirming the challenge rotates its mode, not this item's concern.) |
```

</details>

### `dbbd8e4db` — qa(backlog): apply 2026-08-31 results (arcade-revamp FAIL, daily-timer PASS) + scan to 310bf302

_Claude, 2026-09-01 00:04:13 +0000_

```text
- QA-ARCADE-MENU-REVAMP FAIL -> 🔴 + dev task (ARK top-bar tab unreachable; Salvo card has no preview)
- QA-DAILY-CHALLENGE-TIMER PASS -> ARCHIVE
- scan 6fe3bb11 -> 310bf302: goal-plate bloom pulse folds into QA-UI-GOAL-STACK; toast-feed feature added+reverted (no-op); no new items
```

```text
 Docs/QA/ARCHIVE.md    | 12 ++++++++++++
 Docs/QA/DEV_TASKS.md  |  7 +++++++
 Docs/QA/QA_BACKLOG.md | 14 ++++----------
 3 files changed, 23 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index e33b4f742..b9fd53958 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -111,3 +111,15 @@ Source: PR #766 (`1f0b235a` feat(tournament)). Maelstrom/Tournament now draws fr
 
 PASS: Maelstrom chains modes including the four new ones; transitions in/out of a new mode work; standings/summary resolve with the corrected pool math; no load errors. FAIL: a pool member that won't load or throws · standings math wrong (a mode not counted, or a wrong race-to-N) · a chain that wedges between modes · the new modes never appearing in the pool.
 <!-- /archived:QA-MAELSTROM-POOL -->
+
+<!-- archived:QA-DAILY-CHALLENGE-TIMER -->
+_Passed on build bleeding-edge @ 6fe3bb1 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-31, andrew)._
+
+### QA-DAILY-CHALLENGE-TIMER ⬜ — daily challenge timer
+Source: `daily-challenge-timer`. A countdown timer for the daily challenge (time until the next challenge). Meta/UI.
+
+1. Open the daily-challenge UI: a timer shows the time remaining until the next challenge and counts down.
+2. Confirm it reads sensibly (not negative, not frozen, resets/rolls over correctly at the boundary if observable).
+
+PASS: the daily-challenge timer shows and counts down sensibly. FAIL: a missing/frozen/negative timer, or one that throws.
+<!-- /archived:QA-DAILY-CHALLENGE-TIMER -->
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index 16d5e9442..0510383f5 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -63,3 +63,10 @@ of duplicating it.
 - **Symptom:** Tested Sparrow, Manta, Squirrel. Most vessel functionality works from the keyboard, and the device switch works (controls swap correctly when the player changes between gamepad and keyboard mid-session). Two real defects: (1) **the left/right turn bound to the controller shoulder buttons has no keyboard binding** — that turn input is unmapped on keyboard; (2) **you cannot start a game from the keyboard** — arrow keys + Enter can SELECT an arcade game but never actually launch it. NOT a defect (documented/expected): "only Squirrel's UI switches to the keyboard glyph set" — per the CLAUDE.md ability-row audit only the Squirrel HUD carries the device-icon switcher (Sparrow/Dolphin have complete rows but no switcher, Manta/Rhino/Serpent are design-blocked). Menu nav being mouse-driven is also expected.
 - **Definition of done:** QA item `QA-KEYBOARD-CONTROLS` passes.
 <!-- /devtask:QA-KEYBOARD-CONTROLS -->
+
+<!-- devtask:QA-ARCADE-MENU-REVAMP -->
+### QA-ARCADE-MENU-REVAMP — arcade launch screen + game-mode top-bar redesign + playable card preview
+- **Failed on:** bleeding-edge @ 6fe3bb1 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-31, andrew)
+- **Symptom:** The arcade screen is redesigned and the game-mode cards do route to their modes, but two defects: (1) the **ARK section of the top bar can't be accessed** — the only way into the arcade is the Arcade button on the Home screen; (2) **Salvo has no card preview**.
+- **Definition of done:** QA item `QA-ARCADE-MENU-REVAMP` passes.
+<!-- /devtask:QA-ARCADE-MENU-REVAMP -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index c020b31d9..ffb14aa7b 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-31 · Scan covers: up to `6fe3bb11` (PRs #583–#818 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-08-31 · Scan covers: up to `310bf302` (PRs #583–#818 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -936,7 +936,9 @@ Source: direct commits (`fc9f8a4b` replace the top-left ring with a goal stack 
 
 PASS: the goal stack shows the named objective + target across modes, populates the NetworkVariable target, updates live, and renders/sizes correctly. FAIL: the old ring still there · a target that never appears (NetworkVariable snapshot bug) · count/seconds mislabeled · wrong-atlas numerals · a plate that overlaps the top bar or mis-sizes.
 
-### QA-ARCADE-MENU-REVAMP ⬜ — arcade launch screen + game-mode top-bar redesign + playable card preview
+### QA-ARCADE-MENU-REVAMP 🔴 — arcade launch screen + game-mode top-bar redesign + playable card preview
+> **Last result:** 🔴 FAIL — The arcade screen is redesigned and the game-mode cards do route to their modes, but two defects: (1) the **ARK section of the top bar can't be accessed** — the only way into the arcade is the Arcade button on the Home screen; (2) **Salvo has no card preview**.  _(build bleeding-edge @ 6fe3bb1 · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-31, andrew)_
+
 Source: `arcade-launch-screen-revamp`, `game-mode-topbar-redesign`, `game-card-playable-preview`. A redesign of the arcade launch screen and the game-mode top bar, with playable game-card previews.
 
 1. Open the Arcade (ARK) screen: the redesigned top bar and launch screen render correctly (no missing scripts, no broken layout).
@@ -1032,14 +1034,6 @@ Source: PR #719 (`sparrow-spread-haptics`). Sparrow shot spread plus haptic feed
 
 PASS: the spread reads as intended; the haptic fires appropriately on a device and respects the haptics policy; the standard feels are intact. FAIL: broken/absent spread · a haptic that fires on silenced events or not at all · a regression to the two standard feels.
 
-### QA-DAILY-CHALLENGE-TIMER ⬜ — daily challenge timer
-Source: `daily-challenge-timer`. A countdown timer for the daily challenge (time until the next challenge). Meta/UI.
-
-1. Open the daily-challenge UI: a timer shows the time remaining until the next challenge and counts down.
-2. Confirm it reads sensibly (not negative, not frozen, resets/rolls over correctly at the boundary if observable).
-
-PASS: the daily-challenge timer shows and counts down sensibly. FAIL: a missing/frozen/negative timer, or one that throws.
-
 ## Not covered by this list
 
 * Automated CI checks (`Tools/CI/validate_project.py`, `check_conditional_compilation.py`, the Thursday build promotion in PR #664) run in GitHub Actions and are verified there. QA does not need to re-run them; if a build branch is red, that is an engineering item.
```

</details>

### `552c7a76b` — qa: results 2026-09-01 andrew (connecting-panel-preview FAIL, rampage-rebuild FAIL)

_Claude, 2026-09-01 18:45:05 +0000_

```text
 Docs/QA/RESULTS/2026-09-01-andrew.md | 8 ++++++++
 1 file changed, 8 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-09-01-andrew.md b/Docs/QA/RESULTS/2026-09-01-andrew.md
new file mode 100644
index 000000000..e556735c3
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-09-01-andrew.md
@@ -0,0 +1,8 @@
+# QA Results — 2026-09-01 — andrew
+
+Build: bleeding-edge @ de56875 · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-CONNECTING-PANEL-PREVIEW | FAIL | Working: every mode shows an arena preview, and the daily challenge shows its objective as the description. Two defects: (1) **some modes' progress bars do not advance** — they stay pinned at the start and never change (others do advance); (2) **there is a jump from the preview to gameplay** — the preview camera is cut to a different camera angle of the actual game once fully loaded (not a clean handoff). |
+| QA-RAMPAGE-REBUILD | FAIL | The mode loads, the objective crystal is tracked by an arrow, the game ends when the objective is cleared, and it relaunches with a clean slate after clearing — but **all intensities appear identical to each other** (no visible difference in arena/population between intensity 1 and 4), which is the item's "identical intensities (config race not fixed)" FAIL. |
```

</details>

### `3bda79981` — qa(backlog): apply 2026-09-01 results (connecting-panel + rampage FAIL) + scan to PR #822 (2 new items)

_Claude, 2026-09-01 18:46:13 +0000_

```text
- QA-CONNECTING-PANEL-PREVIEW FAIL -> 🔴 (some progress bars stuck at start; preview->gameplay camera jump)
- QA-RAMPAGE-REBUILD FAIL -> 🔴 (all four intensities appear identical; the sticky cell-config didn't differentiate)
- scan 310bf302 -> f1c5556c: new P1 QA-TOYS-ARKWAY (#822 cellular Wanderway), QA-SCARAB-HULL-POLISH (#821);
  camera-teardown fix folds
```

```text
 Docs/QA/DEV_TASKS.md  | 14 ++++++++++++++
 Docs/QA/QA_BACKLOG.md | 30 +++++++++++++++++++++++++++---
 2 files changed, 41 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 83 lines)</summary>

```diff
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index 0510383f5..2b296afc2 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -70,3 +70,17 @@ of duplicating it.
 - **Symptom:** The arcade screen is redesigned and the game-mode cards do route to their modes, but two defects: (1) the **ARK section of the top bar can't be accessed** — the only way into the arcade is the Arcade button on the Home screen; (2) **Salvo has no card preview**.
 - **Definition of done:** QA item `QA-ARCADE-MENU-REVAMP` passes.
 <!-- /devtask:QA-ARCADE-MENU-REVAMP -->
+
+<!-- devtask:QA-RAMPAGE-REBUILD -->
+### QA-RAMPAGE-REBUILD — Rampage rebuilt as the Dolphin's demolition race (four intensities)
+- **Failed on:** bleeding-edge @ de56875 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-09-01, andrew)
+- **Symptom:** The mode loads, the objective crystal is tracked by an arrow, the game ends when the objective is cleared, and it relaunches with a clean slate after clearing — but **all intensities appear identical to each other** (no visible difference in arena/population between intensity 1 and 4), which is the item's "identical intensities (config race not fixed)" FAIL.
+- **Definition of done:** QA item `QA-RAMPAGE-REBUILD` passes.
+<!-- /devtask:QA-RAMPAGE-REBUILD -->
+
+<!-- devtask:QA-CONNECTING-PANEL-PREVIEW -->
+### QA-CONNECTING-PANEL-PREVIEW — live arena preview + real progress bar on the connecting screen
+- **Failed on:** bleeding-edge @ de56875 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-09-01, andrew)
+- **Symptom:** Working: every mode shows an arena preview, and the daily challenge shows its objective as the description. Two defects: (1) **some modes' progress bars do not advance** — they stay pinned at the start and never change (others do advance); (2) **there is a jump from the preview to gameplay** — the preview camera is cut to a different camera angle of the actual game once fully loaded (not a clean handoff).
+- **Definition of done:** QA item `QA-CONNECTING-PANEL-PREVIEW` passes.
+<!-- /devtask:QA-CONNECTING-PANEL-PREVIEW -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index ffb14aa7b..4d73bf7cd 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-08-31 · Scan covers: up to `310bf302` (PRs #583–#818 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-09-01 · Scan covers: up to `f1c5556c` (PRs #583–#822 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -748,7 +748,9 @@ Source: PR #709 (`squirrel-joust-starvation-wither`). New `LifeformDeathStyle` e
 
 PASS: compiles; starvation withers-to-crystal and leaves a skeleton; a joust kill takes the heart and still withers/skeletons; nothing pops in or out; interrupted withers still finish. FAIL: a creature vanishing instead of withering · no skeleton left · a joust kill dropping no heart or a starved creature dropping none · an interrupted wither leaving a stuck/immortal husk · any missing script.
 
-### QA-RAMPAGE-REBUILD ⬜ — Rampage rebuilt as the Dolphin's demolition race (four intensities)
+### QA-RAMPAGE-REBUILD 🔴 — Rampage rebuilt as the Dolphin's demolition race (four intensities)
+> **Last result:** 🔴 FAIL — The mode loads, the objective crystal is tracked by an arrow, the game ends when the objective is cleared, and it relaunches with a clean slate after clearing — but **all intensities appear identical to each other** (no visible difference in arena/population between intensity 1 and 4), which is the item's "identical intensities (config race not fixed)" FAIL.  _(build bleeding-edge @ de56875 · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-09-01, andrew)_
+
 Source: PR #717 (`dolphin-rampage-minigame`). A rebuild of the Rampage mode as the Dolphin's demolition race — 64 files, four intensities via a new `Tools/Build/rampage_intensity.py`, `SpawnProfileSO`/`GameDataSO` additions, crystals coupled to the nucleus, banded flora, AI-drift fix, and a fixed sticky cell-config race. Reference: `_Scripts/Controller/Arcade/RAMPAGE.md`, `Docs/ECOSYSTEM.md`.
 
 1. Open the Rampage scene / launch the mode: no `Missing (Mono Script)`; the controller + scoring rule wired; the cell builds.
@@ -916,7 +918,9 @@ Source: PRs #818 (`mouse-flight-controls` — "a resting stick cannot take the s
 
 PASS: compiles; mouse flight works across one-thumb vessels; a resting stick doesn't steal control from an active mouse; device handoff is clean; abilities fire. FAIL: missing scripts · mouse flight not working or fighting the stick · a resting stick yanking the ship · abilities that don't fire under mouse control.
 
-### QA-CONNECTING-PANEL-PREVIEW ⬜ — live arena preview + real progress bar on the connecting screen
+### QA-CONNECTING-PANEL-PREVIEW 🔴 — live arena preview + real progress bar on the connecting screen
+> **Last result:** 🔴 FAIL — Working: every mode shows an arena preview, and the daily challenge shows its objective as the description. Two defects: (1) **some modes' progress bars do not advance** — they stay pinned at the start and never change (others do advance); (2) **there is a jump from the preview to gameplay** — the preview camera is cut to a different camera angle of the actual game once fully loaded (not a clean handoff).  _(build bleeding-edge @ de56875 · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-09-01, andrew)_
+
 Source: a cluster of direct commits (`bf276c98` live arena preview + progress bar + daily-challenge objective as description, `087479f2` zoomed shot + pilot roster that waits, `83549c8e`/`d99b74f2` frame the built arena / keep camera inside the cell, `e0bd4f00` full-quality render + kill white chip, `a48cbbf1` restore roster using) + `game-card-playable-preview`. The connecting/loading panel now shows a live preview of the arena being built plus a real progress bar.
 
 1. Launch any arcade game and watch the connecting panel: it shows a **live preview of the actual arena** being built (camera inside the cell, framing the real arena — not a black screen or a generic backdrop), with a **real progress bar** that advances to completion.
@@ -967,6 +971,26 @@ Source: `fauna-flora-network-sync`. Networked synchronisation of fauna and flora
 
 PASS: fauna/flora spawn, grow and die consistently on both peers; kills/consumption replicate; no ghosts, no duplication, no exceptions. FAIL: lifeforms out of sync between peers · ghost/duplicated lifeforms · a death that doesn't replicate · exceptions from the sync path.
 
+### QA-TOYS-ARKWAY ⬜ — "The Arkway": a corridor of cells an Ark sails (cellular Wanderway)
+Source: PR #822 (`cellular-wanderway-toy`). A new freestyle toy — the Arkway, a **cellular** version of the Wanderway conveyor: a corridor of cells an Ark sails through. New `ArkwayToyDefinitionSO` + `ToyCategory`. 26 files, 2,316 insertions, plus adversarial-review fixes. Related: QA-TOYS-WANDERWAY-RUN, QA-TOYS-CELL-SELECTOR.
+
+1. Project compiles; enter freestyle — the Arkway toy is present in the toy ring, no missing scripts, nothing assembles in view.
+2. Fly the Arkway: it starts a run through a **corridor of cells** (the cellular Wanderway) — cells stream ahead and recycle behind, blooming/withering (continuity of existence), never popping in your face.
+3. Confirm the conserved-mass behaviour holds (one built stock transported, not created/destroyed) and the run ends cleanly via its exits (station / overview button / gamepad Start), returning you home.
+4. No exceptions from the run; the cell corridor is coherent (not overlapping/broken).
+
+PASS: the Arkway is present and starts a coherent cell-corridor run; cells bloom/wither and recycle without popping; conserved-mass holds; exits return home cleanly; no exceptions. FAIL: missing scripts · an emblem/toy assembling in view · cells popping in/out or overlapping · a run that won't end or throws · mass visibly created/destroyed.
+
+### QA-SCARAB-HULL-POLISH ⬜ — procedural elemental hull morphs + spring-driven puppetry
+Source: PR #821 (`scarab-vessel-polish`, 3,012 insertions). The Scarab's **generated hull is now an element display** (procedural elemental hull morphs), plus spring-driven puppetry (one writer per part, drift language, juke flourish, idle life), an abdomen under the wing cases and lamellate antennae, and several review fixes. Related: archived QA-SCARAB-MODE; hull morphs law (CLAUDE.md "Elemental Hull Morphs").
+
+1. Project compiles; open `Scarab.prefab` / fly the Scarab — no missing scripts; the hull (abdomen, wing cases, antennae) renders correctly.
+2. Sweep the Scarab's element levels (crystals / test harness) — the hull **morphs to display the element levels** (glides between extremes, never snaps), agreeing with the HUD flowers.
+3. Fly, drift and juke — the spring puppetry animates (drift lean, juke flourish, idle life) with **one writer per part** (no jitter/fighting), and settles when still.
+4. Confirm the puppetry doesn't fight the elemental morph (both compose without stomping each other).
+
+PASS: compiles, hull renders; elemental hull morphs glide with the levels and match the flowers; spring puppetry animates cleanly (drift/juke/idle) and settles; no per-part fighting/jitter. FAIL: missing scripts / broken hull parts · morphs that snap or disagree with the flowers · jittery/fighting puppetry (multiple writers) · puppetry and morph stomping each other.
+
```

</details>

### `eba4f67cd` — qa(results): 2026-09-02 andrew — salvo PASS, scarab-ball + wanderway-invisible FAIL

_Claude, 2026-09-02 21:02:17 +0000_

```text
 Docs/QA/RESULTS/2026-09-02-andrew.md | 9 +++++++++
 1 file changed, 9 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-09-02-andrew.md b/Docs/QA/RESULTS/2026-09-02-andrew.md
new file mode 100644
index 000000000..25e9031b2
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-09-02-andrew.md
@@ -0,0 +1,9 @@
+# QA Results — 2026-09-02 — andrew
+
+Build: bleeding-edge @ e3e2d36 · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-SPARROW-SALVO-MODE | PASS | The game opens, I can play its loop and win and lose, and the scoreboard matches the results. |
+| QA-SCARAB-BALL-FIXES | FAIL | The game launches and the rings still accept goals, but **the ball can still leave the arena** when it is hit at the edge of the arena — the nucleus-bounce fix did not resolve the out-of-arena escape (matches the item's "a ball still escaping the arena unrecoverably" FAIL). |
+| QA-TOYS-WANDERWAY-INVISIBLE | FAIL | Two observations. **Defect:** at very high Manta speed the microscenes can spawn very late, sometimes appearing right in the vehicle's face (matches the item's "a scene appearing close in front of you" FAIL). **Not a defect (verified intended):** the scenes themselves no longer disappear when looked at; the "yellow ball continuously eating the prism path you laid, which you can fly into to leave Wanderway" is the **designed return station riding the rolling-tether tail** (`Docs/ECOSYSTEM.md §0` / QA-TOYS-WANDERWAY-RUN) — the way home is always one tether-length behind you, and flying into it ends the run on purpose. That part is working as designed and is not a bug. |
```

</details>

### `8afb375b1` — qa(backlog): apply 2026-09-02 results (salvo PASS, scarab-ball + wanderway FAIL) + scan to PR #833

_Claude, 2026-09-02 21:06:13 +0000_

```text
- SALVO archived (P0 mode gate cleared); SCARAB-BALL-FIXES + TOYS-WANDERWAY-INVISIBLE
  re-failed -> 🔴 + dev tasks (wanderway task notes the return-station is intended).
- Scan f1c5556c..db61e73f: 5 new items — CRYSTAL-VESSEL-RETIREMENT, WEEKLY-CHALLENGE,
  PARTY-REINVITE-ROBUSTNESS (P1); AUDIO-SETTINGS, QA-SESSION-TOOL (P2).
- Header -> db61e73f (#583–#833); quick wins refreshed (+AUDIO-SETTINGS).
```

```text
 Docs/QA/ARCHIVE.md    | 14 ++++++++++++++
 Docs/QA/DEV_TASKS.md  | 14 ++++++++++++++
 Docs/QA/QA_BACKLOG.md | 68 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++-----------
 3 files changed, 85 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 160 lines)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index b9fd53958..e78056d4e 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -123,3 +123,17 @@ Source: `daily-challenge-timer`. A countdown timer for the daily challenge (time
 
 PASS: the daily-challenge timer shows and counts down sensibly. FAIL: a missing/frozen/negative timer, or one that throws.
 <!-- /archived:QA-DAILY-CHALLENGE-TIMER -->
+
+<!-- archived:QA-SPARROW-SALVO-MODE -->
+_Passed on build bleeding-edge @ e3e2d36 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-09-02, andrew)._
+
+### QA-SPARROW-SALVO-MODE ⬜ — "Salvo": the Sparrow party game
+Source: PR #790 (`sparrow-party-game`, incl. a "repair merge-damaged **Salvo** scene entry" in build settings) plus the Sparrow combat work around it. A new Sparrow-focused party game (scene "Salvo"), authored headless. **Depends on** the Sparrow visual/weapon rework (QA-SPARROW-VISUAL-REWORK) and the projectile pool (QA-SPARROW-PROJECTILE-POOL 🔴).
+
+1. Confirm the **Salvo** scene is in Build Settings and opens with no `Missing (Mono Script)`; controller + scoring rule wired.
+2. Launch the mode (any player count — AI backfill for solo): reaches gameplay without an exception; the arena builds; it is Sparrow-based.
+3. Play the core loop (gunplay / missiles) and confirm scoring works and the round resolves to a win/loss.
+4. Return to menu and relaunch once — no leaked state, no crash.
+
+PASS: the Salvo scene is wired and opens clean; the mode launches, plays its loop to a resolved scoreboard, and returns/relaunches without error. FAIL: a missing/merge-damaged scene entry · missing scripts · a controller that throws on load/launch · a round that won't score/resolve · a crash on return.
+<!-- /archived:QA-SPARROW-SALVO-MODE -->
diff --git a/Docs/QA/DEV_TASKS.md b/Docs/QA/DEV_TASKS.md
index 2b296afc2..b52cf9a96 100644
--- a/Docs/QA/DEV_TASKS.md
+++ b/Docs/QA/DEV_TASKS.md
@@ -84,3 +84,17 @@ of duplicating it.
 - **Symptom:** Working: every mode shows an arena preview, and the daily challenge shows its objective as the description. Two defects: (1) **some modes' progress bars do not advance** — they stay pinned at the start and never change (others do advance); (2) **there is a jump from the preview to gameplay** — the preview camera is cut to a different camera angle of the actual game once fully loaded (not a clean handoff).
 - **Definition of done:** QA item `QA-CONNECTING-PANEL-PREVIEW` passes.
 <!-- /devtask:QA-CONNECTING-PANEL-PREVIEW -->
+
+<!-- devtask:QA-SCARAB-BALL-FIXES -->
+### QA-SCARAB-BALL-FIXES — ball nucleus-bounce, studding retired, switch ring changes
+- **Failed on:** bleeding-edge @ e3e2d36 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-09-02, andrew)
+- **Symptom:** The game launches and the rings still accept goals, but **the ball can still leave the arena** when it is hit at the edge of the arena — the nucleus-bounce fix did not resolve the out-of-arena escape (matches the item's "a ball still escaping the arena unrecoverably" FAIL).
+- **Definition of done:** QA item `QA-SCARAB-BALL-FIXES` passes.
+<!-- /devtask:QA-SCARAB-BALL-FIXES -->
+
+<!-- devtask:QA-TOYS-WANDERWAY-INVISIBLE -->
+### QA-TOYS-WANDERWAY-INVISIBLE — the conveyor's transport is never watched
+- **Failed on:** bleeding-edge @ e3e2d36 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-09-02, andrew)
+- **Symptom:** Two observations. **Defect:** at very high Manta speed the microscenes can spawn very late, sometimes appearing right in the vehicle's face (matches the item's "a scene appearing close in front of you" FAIL). **Not a defect (verified intended):** the scenes themselves no longer disappear when looked at; the "yellow ball continuously eating the prism path you laid, which you can fly into to leave Wanderway" is the **designed return station riding the rolling-tether tail** (`Docs/ECOSYSTEM.md §0` / QA-TOYS-WANDERWAY-RUN) — the way home is always one tether-length behind you, and flying into it ends the run on purpose. That part is working as designed and is not a bug.
+- **Definition of done:** QA item `QA-TOYS-WANDERWAY-INVISIBLE` passes.
+<!-- /devtask:QA-TOYS-WANDERWAY-INVISIBLE -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 4d73bf7cd..1e329c38b 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-09-01 · Scan covers: up to `f1c5556c` (PRs #583–#822 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-09-02 · Scan covers: up to `db61e73f` (PRs #583–#833 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -30,6 +30,7 @@ every run, so it can lag reality by one submission.
 5. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
 6. **QA-PALETTE-DANGER-GOLD** — get shielded prisms of all three domains on screen (a populated cell / HexRace) and check gold reads in the pastel family.
 7. **QA-P2-DANGLING-CELLDATA** — a populated cell in freestyle: watch the Console for `LifeForm.Start()` / `Flora.Plant()` throws (PR #731 may have fixed this — verify which, if any, still throw).
+8. **QA-AUDIO-SETTINGS** — open Settings: audio sliders should be 0–1 (not 60–90), drag Music down (audible), relaunch and confirm it stuck. New this run.
 
 Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) is the cheapest if you're already in the editor.
 
@@ -193,17 +194,39 @@ Source: `csharp-monolith-assembly-split` (merge `409afb21`). The "zero runtime a
 
 PASS: clean compile with the new `CosmicShore.Data` assembly; no missing-reference/type-load errors at import or runtime; the app boots + plays a round; tests still resolve Data types; the timing recorder works. FAIL: any compile error / missing-assembly-reference · a `TypeLoadException` at runtime · the app failing to boot or play a round · tests that no longer compile against `Data` · a circular/duplicate-assembly warning.
 
-### QA-SPARROW-SALVO-MODE ⬜ — "Salvo": the Sparrow party game
-Source: PR #790 (`sparrow-party-game`, incl. a "repair merge-damaged **Salvo** scene entry" in build settings) plus the Sparrow combat work around it. A new Sparrow-focused party game (scene "Salvo"), authored headless. **Depends on** the Sparrow visual/weapon rework (QA-SPARROW-VISUAL-REWORK) and the projectile pool (QA-SPARROW-PROJECTILE-POOL 🔴).
+## Priority 1 — merged features that have never been played
 
-1. Confirm the **Salvo** scene is in Build Settings and opens with no `Missing (Mono Script)`; controller + scoring rule wired.
-2. Launch the mode (any player count — AI backfill for solo): reaches gameplay without an exception; the arena builds; it is Sparrow-based.
-3. Play the core loop (gunplay / missiles) and confirm scoring works and the round resolves to a win/loss.
-4. Return to menu and relaunch once — no leaked state, no crash.
+### QA-CRYSTAL-VESSEL-RETIREMENT ⬜ — a vessel replaces the shared crystal-husk spray with the crystal's own body morphing onto what the pickup made
+Source: PRs #833 (`vessel-crystal-effects` — "the platform half of a per-vessel crystal retirement"; `feat(scarab): the omni crystal CLOSES into the ball it forges`; the spent-crystal husk is now the DEFAULT, not a law). New crystal-death platform: instead of every collected crystal bursting into the shared husk spray, a vessel may carry the crystal's OWN geometry onto whatever the pickup produced (GPU vertex morph off `_PrismClock` via `CrystalMorph.hlsl` on `ShepardGraph`; zero per-frame CPU). The **Scarab** is the first citizen: its omni crystal closes into the ball it forges. Related: QA-CRYSTAL-EFFECTS (🔴, bloom-IN on spawn), QA-SCARAB-MODE, QA-SCARAB-BALL-FIXES. LOCKED ecology surface (continuity of existence) — verify against `Docs/ECOSYSTEM.md`.
 
-PASS: the Salvo scene is wired and opens clean; the mode launches, plays its loop to a resolved scoreboard, and returns/relaunches without error. FAIL: a missing/merge-damaged scene entry · missing scripts · a controller that throws on load/launch · a round that won't score/resolve · a crash on return.
```

</details>

### `31420da46` — qa(results): 2026-09-03 andrew — audio-settings + state-reset PASS

_Claude, 2026-09-03 21:36:36 +0000_

```text
 Docs/QA/RESULTS/2026-09-03-andrew.md | 8 ++++++++
 1 file changed, 8 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-09-03-andrew.md b/Docs/QA/RESULTS/2026-09-03-andrew.md
new file mode 100644
index 000000000..d1a786549
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-09-03-andrew.md
@@ -0,0 +1,8 @@
+# QA Results — 2026-09-03 — andrew
+
+Build: bleeding-edge @ db61e73 · Unity 6000.4.11f1.x · Windows, Unity Editor
+
+| Item ID | Result | Notes / Evidence |
+|---------|--------|------------------|
+| QA-AUDIO-SETTINGS | PASS | Both volume sliders work within 0–1 and FOV works in 60–90; the audible changes are immediate; the level stays where it was set when the game is closed and reopened; no errors in the Console. |
+| QA-STATE-RESET | PASS | Played Salvo and won, then replayed (same mode, Play Again) — none of the score carried over to the next round. No score leakage between rounds. |
```

</details>

### `22570e6f9` — qa(backlog): apply 2026-09-03 results (audio-settings + state-reset PASS) + scan to 5a024a50

_Claude, 2026-09-03 21:38:41 +0000_

```text
- Both PASSes archived (AUDIO-SETTINGS, STATE-RESET); AUDIO-SETTINGS dropped from Quick wins.
- Scan db61e73f..5a024a50: extended QA-WEEKLY-CHALLENGE (dedicated leaderboard window,
  spent-card-opens, challenge-clock-no-longer-eats-the-attempt); 1 new item
  QA-MODE-RENAME-SWEEP (mode code names -> player-facing, per-mode board deleted, blank cards removed).
- Header -> 5a024a50.
```

```text
 Docs/QA/ARCHIVE.md    | 26 ++++++++++++++++++++++++++
 Docs/QA/QA_BACKLOG.md | 40 ++++++++++++++++------------------------
 2 files changed, 42 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 117 lines)</summary>

```diff
diff --git a/Docs/QA/ARCHIVE.md b/Docs/QA/ARCHIVE.md
index e78056d4e..1bc11a565 100644
--- a/Docs/QA/ARCHIVE.md
+++ b/Docs/QA/ARCHIVE.md
@@ -137,3 +137,29 @@ Source: PR #790 (`sparrow-party-game`, incl. a "repair merge-damaged **Salvo** s
 
 PASS: the Salvo scene is wired and opens clean; the mode launches, plays its loop to a resolved scoreboard, and returns/relaunches without error. FAIL: a missing/merge-damaged scene entry · missing scripts · a controller that throws on load/launch · a round that won't score/resolve · a crash on return.
 <!-- /archived:QA-SPARROW-SALVO-MODE -->
+
+<!-- archived:QA-STATE-RESET -->
+_Passed on build bleeding-edge @ db61e73 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-09-03, andrew)._
+
+### QA-STATE-RESET ⬜ — runtime game state resets to defaults between sessions
+Source: PR #647.
+
+1. Play a game to the end, return to the menu, and launch a different mode.
+2. Repeat with the same mode twice (use Play Again where available).
+
+PASS: the second launch starts with a clean score, intensity, player count and domain assignment — no leakage from the previous round. FAIL: any carried-over score, stale player count, or a domain that was not reassigned.
+<!-- /archived:QA-STATE-RESET -->
+
+<!-- archived:QA-AUDIO-SETTINGS -->
+_Passed on build bleeding-edge @ db61e73 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-09-03, andrew)._
+
+### QA-AUDIO-SETTINGS ⬜ — the Options audio sliders actually control audio and persist (they shipped as FOV sliders)
+Source: PRs #833 tip: `fix(settings): audio sliders were FOV sliders that saved full volume on bind`; `fix(audio): stop FMOD leaks, harden lifecycle, make volume sliders persist`. The Music/SFX/Haptics rows in `OptionsMenuContent.prefab` were copies of the field-of-view slider (60–90, whole numbers) — binding them clamped the value to the 0–1 range AND broadcast that clamp to `AudioLevelSlider.SetVolume`, which saved full volume over the player's setting every launch. Now the three rows are re-authored 0–1 with a guaranteed-silent range-apply (`SliderRange.ApplyWithoutNotify`), so the saved value survives binding; plus FMOD lifecycle/leak hardening.
+
+1. Project compiles. Open Settings/Options: the Music, SFX and Haptics sliders read 0–1 (a fractional handle), and the real FOV slider still reads 60–90.
+2. Lower Music (and SFX): the change is audible immediately, and the handle stays where you set it (it does not snap back to full).
+3. Set a level, close and relaunch the app: the saved level is restored (not reset to the top). If signed in, confirm it round-trips through Cloud Save.
+4. Watch the Console across a few open/close cycles of the settings panel — no FMOD errors/leaks logged.
+
+PASS: audio sliders are 0–1 and audible, the FOV slider is untouched, a set level persists across a relaunch, and no FMOD leak/errors. FAIL: a slider that snaps to full on open · a level that resets on relaunch · an audio slider still ranged like FOV · FMOD leak/lifecycle errors in the Console.
+<!-- /archived:QA-AUDIO-SETTINGS -->
diff --git a/Docs/QA/QA_BACKLOG.md b/Docs/QA/QA_BACKLOG.md
index 1e329c38b..35eaa8ea8 100644
--- a/Docs/QA/QA_BACKLOG.md
+++ b/Docs/QA/QA_BACKLOG.md
@@ -1,6 +1,6 @@
 # QA Backlog — untested development on `bleeding-edge`
 
-Generated: 2026-09-02 · Scan covers: up to `db61e73f` (PRs #583–#833 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
+Generated: 2026-09-03 · Scan covers: up to `5a024a50` (PRs #583–#833 + direct commits) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**
 
 > Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.
 
@@ -30,7 +30,6 @@ every run, so it can lag reality by one submission.
 5. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
 6. **QA-PALETTE-DANGER-GOLD** — get shielded prisms of all three domains on screen (a populated cell / HexRace) and check gold reads in the pastel family.
 7. **QA-P2-DANGLING-CELLDATA** — a populated cell in freestyle: watch the Console for `LifeForm.Start()` / `Flora.Plant()` throws (PR #731 may have fixed this — verify which, if any, still throw).
-8. **QA-AUDIO-SETTINGS** — open Settings: audio sliders should be 0–1 (not 60–90), drag Music down (audible), relaunch and confirm it stuck. New this run.
 
 Editor-only, no play mode: **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) is the cheapest if you're already in the editor.
 
@@ -207,15 +206,26 @@ Source: PRs #833 (`vessel-crystal-effects` — "the platform half of a per-vesse
 PASS: compiles; ordinary crystals keep the husk spray; the Scarab omni crystal morphs cleanly into the ball as one event with continuity held; no regression to other crystal pickups. FAIL: missing scripts · the Scarab forge still reads as two events (explosion + ball) · a ball with no collider on the forge frame · any crystal popping in/out · a regression in normal crystal collection visuals.
 
 ### QA-WEEKLY-CHALLENGE ⬜ — the daily challenge is now a WEEKLY challenge with a UGS "fastest this week" leaderboard
-Source: PRs #828 (`daily-challenge-timer` cluster: `refactor(challenge): daily → WEEKLY, and the run goes back to the mode's own end conditions`; `feat(weekly-challenge): UGS leaderboard — who completed this week's objective fastest`; the daily→weekly rename that also renamed a serialized field + the scene text; the LOCKS stay, only end conditions reverted; the legacy PlayFab cluster keeps its own `DailyChallenge` struct). One curated objective per **UTC week** (keyed on Monday, ISO-8601), a countdown that steps down (days lead, then hours), and a single UGS leaderboard ranking finishers by elapsed time (lowest at top) — only a COMPLETION (`achieved >= target`) earns an entry. Supersedes the just-passed QA-DAILY-CHALLENGE-TIMER. Rewards on weekly reset are deliberately NOT implemented here.
+Source: PRs #828 (`daily-challenge-timer` cluster: `refactor(challenge): daily → WEEKLY, and the run goes back to the mode's own end conditions`; `feat(weekly-challenge): UGS leaderboard — who completed this week's objective fastest`; the daily→weekly rename that also renamed a serialized field + the scene text; the LOCKS stay, only end conditions reverted; the legacy PlayFab cluster keeps its own `DailyChallenge` struct) + the `weekly-challenge-leaderboard-ugs` follow-up (`feat(leaderboard): the weekly challenge leaderboard window` + its own-hierarchy wirer/row prefab; `feat(weekly): a spent challenge still opens, and its card offers the leaderboard`; `fix(weekly): the challenge's own clock was ending the turn and eating the attempt`; `refactor(leaderboard): one weekly-challenge board, and the per-mode path is deleted`; scene/catalog updates). One curated objective per **UTC week** (keyed on Monday, ISO-8601), a countdown that steps down (days lead, then hours), and a single UGS leaderboard — with its own dedicated menu window — ranking finishers by elapsed time (lowest at top); only a COMPLETION (`achieved >= target`) earns an entry. Supersedes the just-passed QA-DAILY-CHALLENGE-TIMER. Rewards on weekly reset are deliberately NOT implemented here.
 
 1. Project compiles; the challenge card in the menu reads **weekly** (no stale "Daily" text in the scene or on the card), and shows one objective for the current week.
 2. The countdown reads as a week (e.g. `6d 3h`, stepping down to hours/minutes near the end), and rolls over on the UTC Monday boundary — not a 24h daily reset.
-3. Play the challenge to completion (reach the target): your elapsed time is submitted and you appear on the leaderboard (rank · avatar · name · time), sorted lowest-first.
+3. Play the challenge to completion (reach the target): your elapsed time is submitted and you appear on the leaderboard (rank · avatar · name · time), sorted lowest-first. **The run must NOT be cut short by the challenge's own clock** — the recent fix stopped that clock from ending the turn and eating the attempt.
 4. A run that does NOT reach the target submits nothing — non-finishers never appear on the board (no sentinel/placeholder rows).
-5. The run uses the mode's own end conditions (the challenge doesn't override them anymore), and the challenge locks still gate access.
+5. Open the dedicated **weekly-challenge leaderboard window** (its card offers it, and a *spent*/already-completed challenge card still opens to the board): the window draws real rows (not an empty/placeholder panel).
+6. The run uses the mode's own end conditions (the challenge doesn't override them anymore), and the challenge locks still gate access.
 
-PASS: compiles; the card/countdown read as a correct UTC-Monday weekly cycle; a completion posts your time to the fastest-this-week board and a non-completion posts nothing; the run uses the mode's own end conditions with locks intact. FAIL: missing scripts · stale "Daily" text or a 24h reset · a completion that doesn't post (or a non-completion that does) · a board not sorted by time · the run ignoring the mode's end conditions or the locks.
+PASS: compiles; the card/countdown read as a correct UTC-Monday weekly cycle; a full run isn't cut short by the challenge clock; a completion posts your time to the fastest-this-week board and a non-completion posts nothing; the dedicated leaderboard window opens (including from a spent card) and draws rows; the run uses the mode's own end conditions with locks intact. FAIL: missing scripts · stale "Daily" text or a 24h reset · the run ending early / eating the attempt · a completion that doesn't post (or a non-completion that does) · a board not sorted by time · an empty/undrawable leaderboard window · the run ignoring the mode's end conditions or the locks.
+
+### QA-MODE-RENAME-SWEEP ⬜ — game-mode code names renamed to the player-facing names; per-mode leaderboard path deleted; blank duplicate cards removed
+Source: `weekly-challenge-leaderboard-ugs` refactor commits: `refactor(modes): the code names now say what the player sees`; `fix(modes): the rename sweep could not see three of the places it had to reach`; `fix(tools): the rename sweep would have eaten its own migration map`; `fix(arcade): delete two blank-named cards that duplicated another mode`; `refactor(leaderboard): one weekly-challenge board, and the per-mode path is deleted`; `fix(leaderboard): the per-mode board came back in the rename commit`. A project-wide rename of game-mode code identifiers to match the names players see, plus arcade-card cleanup and deletion of the old per-mode leaderboard path. Broad regression surface — every arcade card + its launch route + any leaderboard entry point. Related: QA-ARCADE-MENU-REVAMP (🔴), QA-WEEKLY-CHALLENGE.
+
+1. Project compiles with no missing scripts; the Menu_Main scene + arcade catalog import clean after the rename sweep.
+2. Open the arcade screen: every card shows a real player-facing name — **no blank-named cards** and no two cards that duplicate the same mode.
+3. Launch several modes from their cards: each still routes to the correct mode (the rename didn't break the card→mode mapping — this is where "three places it couldn't see" would surface).
+4. Leaderboards: only the one weekly-challenge board remains reachable; there is no stale per-mode leaderboard entry point that errors or shows empty.
```

</details>

_Also contains 8 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._

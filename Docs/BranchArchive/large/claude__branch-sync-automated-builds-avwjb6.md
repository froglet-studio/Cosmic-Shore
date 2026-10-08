# Branch archive: `claude/branch-sync-automated-builds-avwjb6`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Build pipeline and analytics consent**

Set up automated build-branch syncing and promotion through CI, with a click-by-click setup checklist. It also carries a batch of analytics and privacy work: a runtime privacy-consent dialog, a PostHog/UGS analytics handbook, event lists for the UGS dashboard, menu freestyle counted as flight time, and a fix for the Falcon's blank name in hangar data.

- **Status:** Redone elsewhere
- **Areas:** Build/CI, Analytics (PostHog/UGS), Privacy consent UI, Vessels (Falcon data)
- **Already in bleeding-edge:** bleeding-edge has .github/workflows/sync-build-branches.yml and build-branch-ci.yml, Docs/Analytics/ANALYTICS_HANDBOOK.html, Assets/_Scripts/UI/Privacy/PrivacyConsentOverlay.cs and Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs.
- **Risk if deleted:** low
- **Suggestion (2026-10-08):** can be deleted after archiving — The CI workflows, consent UI and analytics docs are all in bleeding-edge.

## Evidence

- **Last commit:** 2026-08-06 by Claude
- **Unmerged commits:** 20
- **Forked from:** `c85eba30e` (2026-08-06, Merge pull request #675 from froglet-studio/claude/sparrow-ability-redesign-no)
- **Tip:** `8bfe817f1`
- **Files touched (47):**
  - `.github/workflows/sync-build-branches.yml`
  - `.github/workflows/tag-internal-build.yml`
  - `Assets/Resources/PostHogConfig.asset`
  - `Assets/Resources/PrivacyConsentConfig.asset`
  - `Assets/Resources/PrivacyConsentConfig.asset.meta`
  - `Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset`
  - `Assets/_SO_Assets/_TEMP/FalconClassSO.asset`
  - `Assets/_SO_Assets/_TEMP/ShrikeClassSO.asset`
  - `Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs`
  - `Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs`
  - `Assets/_Scripts/Editor/AnalyticsConsentDevWindow.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/PostHogConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs`
  - `Assets/_Scripts/ScriptableObjects/PrivacyConsentConfigSO.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/SO_Vessel.cs`
  - `Assets/_Scripts/System/AppManager.cs`
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
  - `Cosmic-Shore.slnx`
  - `Docs/Analytics/ANALYTICS_HANDBOOK.html`
  - `Docs/Analytics/ANALYTICS_HANDBOOK.pdf`
  - `Docs/Analytics/ANALYTICS_HANDOFF.md`
  - `Docs/Analytics/DATA_ARCHITECTURE.md`
  - `Docs/Analytics/EVENT_SCHEMA.json`
  - `Docs/Analytics/POSTHOG_SETUP.md`
  - `Docs/Analytics/event-taxonomy.md`
  - `Docs/Analytics/implementation-plan.md`
  - `Docs/Analytics/ugs_event_manager_rows.csv`
  - `Docs/Analytics/ugs_step1_parameters.csv`
  - `Docs/Analytics/ugs_step2_events.csv`
  - `Docs/Analytics/utm-conventions.md`
  - `Docs/Analytics/viability-report.md`
  - … and 7 more

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

### `8bfe817f1` — docs(build): correct the build/* ruleset advice, which would have broken promotion

_Claude, 2026-08-06 21:31:41 +0000_

```text
Both documents told the reader to restrict pushes on build/* to the GitHub
Actions app and allow force pushes for that actor. Neither half is configurable.

GitHub's ruleset bypass list accepts repository admins, organization and
enterprise owners, maintain and write role users, teams, GitHub Apps, and
Dependabot. GitHub Actions is not an eligible bypass actor, so the workflow
cannot be exempted. And "Restrict updates" is documented as "only users with
bypass permissions can push", which means following the old advice would have
blocked the promotion workflow from updating the very branches it exists to move.

Worse, the ruleset form arrives with "Block force pushes" ALREADY TICKED, and
the promotion force-moves build/* on every cycle. Saving the form at its defaults
breaks the pipeline, so the correction has to say untick it explicitly rather
than just omitting it.

The working configuration is Restrict deletions on, Block force pushes off,
everything else off. That protects against accidental deletion but not against a
human pushing to a build branch. Rather than paper over the gap, both documents
now name it and explain why it is acceptable: such a push is destroyed by the
next promotion, which is already the documented behaviour of these branches.

Both also tell the reader to check the Add bypass dialog for a usable GitHub
Actions entry first, since plans differ, and to take the strict configuration if
one is available.
```

```text
 Docs/BRANCHING_AND_RELEASE.md | 25 ++++++++++++++++++++-----
 Docs/BUILD_PIPELINE_SETUP.md  | 27 +++++++++++++++++++++------
 2 files changed, 41 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/BRANCHING_AND_RELEASE.md b/Docs/BRANCHING_AND_RELEASE.md
index f0765ebcd..ffe70ec00 100644
--- a/Docs/BRANCHING_AND_RELEASE.md
+++ b/Docs/BRANCHING_AND_RELEASE.md
@@ -314,11 +314,26 @@ build" all depend on it.
 
 ### R3. Anyone can push to the build branches
 
-**The fix.** Settings → Branches → ruleset targeting `build/*`:
-
-- Restrict pushes to the GitHub Actions app only
-- Allow force pushes **for that actor** (promotion needs them)
-- Do not require PRs; the workflow pushes directly
+**The fix, and its ceiling.** Settings → Rules → Rulesets → new branch ruleset
+targeting `build/*`:
+
+- Tick **Restrict deletions**
+- **Untick Block force pushes** (on by default), because the promotion
+  force-moves these branches every cycle
+- Leave everything else off
+
+**"Restrict updates" is the rule you actually want, and you probably cannot use
+it.** It means "only users with bypass permissions can push", and GitHub Actions
+is not an eligible bypass actor: the list is repo admins, org and enterprise
+owners, maintain/write-role users, teams, GitHub Apps, and Dependabot. Ticking it
+blocks the promotion workflow itself. Check **Add bypass** for a usable GitHub
+Actions entry first; if one exists, tick **Restrict updates**, keep **Block force
+pushes** on, and the rule becomes airtight.
+
+So on most plans this protects against accidental *deletion* only. A human can
+still push to a build branch. That is an accepted residual risk rather than a
+gap: such a push is destroyed by the next promotion, which is exactly what §1
+says these branches do.
 
 **Effort:** ten minutes.
 
diff --git a/Docs/BUILD_PIPELINE_SETUP.md b/Docs/BUILD_PIPELINE_SETUP.md
index 768b5e701..01f954da0 100644
--- a/Docs/BUILD_PIPELINE_SETUP.md
+++ b/Docs/BUILD_PIPELINE_SETUP.md
@@ -78,12 +78,27 @@ broken.**
 
 **Settings → Rules → Rulesets → New branch ruleset**
 
-- Target branches: `build/*`
-- [ ] **Restrict updates** so only the GitHub Actions app can push
-- [ ] **Allow force pushes** for that actor (promotions require them)
-
-Without this, someone can commit to a build branch and lose the work on the next
-promotion.
+- Ruleset name: `Build branches`, enforcement **Active**
+- Target branches → **Add target → Include by pattern** → `build/*`
+- [ ] Tick **Restrict deletions**
+- [ ] **Untick Block force pushes** — it is on by default and **would break the
+      promotion**, which force-moves these branches every cycle
+- Leave every other rule off
+
+> **Do not tick "Restrict updates".** It means "only users with bypass
+> permissions can push", and GitHub Actions **cannot** be added to a bypass list
+> (eligible actors are repo admins, org/enterprise owners, maintain/write-role
+> users, teams, GitHub Apps and Dependabot). Ticking it stops your own promotion
+> workflow.
+>
+> Before settling for that, open **Add bypass** and search `actions`. If your
+> plan does surface a usable GitHub Actions entry, add it, and you can then tick
+> **Restrict updates** and leave **Block force pushes** on for the strong
+> version of this rule. If it is not there, use the settings above.
+
+This protects against accidental deletion, not against a human pushing to a
+build branch. That residual risk is accepted: such a push is overwritten by the
+next promotion, which is the documented behaviour of these branches.
 
 ---
 
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._

# Unified Systems — Garrett (owner: decisions, design nuance, in-editor verification)

**Companions:** `AUDIT.md` (evidence base — every § reference below points there) ·
`YASH.md` (systems engineering) · `SHOMBITH.md` (UI consolidation + tooling).

**How this works.** Section 1 is the decision sheet — mark it up like `FLEET_MAPS.md` (edit in
place: bold your pick, strike what's dead, add notes). Every gated item in YASH.md / SHOMBITH.md
cites a `D-number`. Each decision ships with a **recommended default**; items marked
`[default-ok]` in the other docs may proceed on the default if you haven't marked the sheet —
items marked `[hard-gate]` wait for your explicit markup. Section 2 is your own work list.
Section 3 is the starting prompt for your next Claude Code session.

**Already done (zero-risk wave, commits `3943e21d` + `16cc36e8`):** the audit itself; ~40
verified-dead files deleted (fully-commented corpses, zero-reference classes, throwing scoring
stubs + retired enum IDs 3-6, six stub turn monitors, dead per-mode stats providers, Hangar
singleton + prefab, ElementPips family, Wwise husk); stray `KeyboardInputStrategy.cs` moved into
`Controller/IO/`; CLAUDE.md corrected (FMOD ×3, `_Scripts/Game` note). Nothing with plausible
intent-to-use was touched.

---

## 1. Decision sheet

### Roadmap / content intent

**D1 — Unshipped vessels' legacy-only abilities.** Urchin (ghost/energize/detach/barrage),
Grizzly (charged-fire/spin/turret), Termite (drone swarms), Falcon/Shrike (gyro/seed-assembler)
exist ONLY as legacy `ShipAction` components that can never fire (dispatch only reaches R_
actions — AUDIT §1.1). Their support pieces (dead `SparrowAnimationController`, Termite's
`HUDContainer`/`ShipHUD` chain) are in the same state.
Options: (a) **keep in place until R_ ports exist** *(recommended — zero re-derivation risk;
costs only tree cleanliness)*; (b) delete now, git is the archive; (c) port designs to R_ now.
Gates: YASH Y7, SHOMBITH S2 (ShipHUD chain), S5 (dead animation deletions).

**D2 — Missions & training.** `ProtectMissionGame` (only mission gameplay, on the deprecated
base) is unreachable; `FactionMissionModal` + `HangarTrainingModal` are LIVE Menu_Main UI calling
the never-instantiated `Arcade.Instance` (those buttons NRE today — AUDIT §0.1). SlipnStride's
scene doesn't exist.
Options: (a) **training yes / missions parked** *(recommended — training feeds daily challenge +
Hangar; disable the mission modal entry, keep `ThreatSpawner` wave design parked)*; (b) both
returning — rebuild launch paths, delete nothing; (c) cut both.
Gates: YASH Y4.3 (Arcade singleton fate), SHOMBITH S6.2 (modal hiding).

**D3 — RESOLVED by owner 2026-07-20: delete outright (option b), superseding the archive
default — executed in the solo-retirement program (C6).** Original entry:

**D3 — Dead arcade content.** ~23 scene-less `SO_ArcadeGame` assets; 5 still render as playable
Arcade cards that fail at load (BlockBandit, Darts, MazeRunner, Rampage, SlipNStride);
`PreviousAllGames.asset` referenced by nothing; the mode-32 co-op blitz stack dead end-to-end.
Options: (a) **de-list the 5 cards now + move scene-less assets to an `_Archive` folder**
*(recommended — stops shipping broken buttons, preserves authored config)*; (b) delete outright;
(c) leave. Gate: SHOMBITH S6.1.

**D4 — Store / economy path.** Store screen + purchase/reward cards ride disabled PlayFab
(dead-render/NRE today). UGS Purchasing is already in the stack; `Docs/MENU_PROGRESSION_AND_IAP.md`
exists.
Options: (a) **port to UGS Economy per the IAP doc** *(recommended if monetization is near-term)*;
(b) hide Store + cards now, defer the UGS build to its own project *(recommended if not)*;
(c) delete the store UI. Gate: YASH Y4 (PlayFab excision scope). **[hard-gate]**

**D5 — Captain progression.** `CaptainProgressRepository`/`CloudData` finished but never
registered; dead PlayFab `XpHandler`/`CaptainManager` still DI-wired and consumed by live hangar
UI (AUDIT §1.9). Options: (a) **register the repo + port hangar UI, delete the PlayFab path**
*(recommended — the successor is already built)*; (b) shelve captains: delete both sides;
(c) leave. Gate: YASH Y4.2.

**D6 — Daily Challenge.** Third persistence pattern (10 PlayerPrefs keys) + dead PlayFab bridge +
NRE on play/claim; its purpose-built UGS repo loads unused every session (AUDIT §2.9).
Options: (a) **keep — port onto `DailyChallengeRepo` + `PlayerDataService`, fixing the NREs**
*(recommended)*; (b) cut the feature for now. Gate: YASH Y4.4.

**D7 — Cloud roaming.** Which domains should roam via Cloud Save? Loadout, Squads,
TrainingProgress have finished-but-unconsumed mirror repos while live systems write local files;
Favorites has no mirror at all (AUDIT §2.9). Mark each: **finish migration** / delete mirror,
stay local. *(Recommended: finish Loadout + TrainingProgress; decide Squads with the squads
feature; Favorites optional.)* Gate: YASH Y4.5.

**D8 — Dialogue runtime.** The whole `System/Runtime/` pipeline is built but mounted nowhere
(no channel asset, no raiser, FTUE doesn't use it — AUDIT §1.8). Options: (a) **archive the
runtime tree until a feature needs it; keep data assets + editor window** *(recommended)*;
(b) mount it now (say where — FTUE? menu?); (c) leave as is. Gate: SHOMBITH S6.3.

### Design nuances (don't let cleanup override intent)

**D9 — Touch invert.** Invert-Y / invert-throttle are user-facing + cloud-synced but silently
ignored on touch (the pipeline copy in `TouchInputStrategy` omits the inversion block — AUDIT
§3.5). Is that a bug to fix (**recommended**) or a deliberate touch exemption to document?
Gate: YASH Y0.3 / Y3.

**D10 — Squirrel skim FX.** The `[Obsolete]` legacy skim particle effect still runs on the
shipping Squirrel *alongside* its forcefield-crackle replacement in the same effect list (AUDIT
§1.5). Wants an in-editor look A/B from you: (a) **crackle only** *(recommended — honor the
[Obsolete] marker)*; (b) both is the intended look — drop `[Obsolete]`; (c) legacy only.
Gate: YASH Y6.3.

**D11 — Half-built abilities: finish or delete?** Four abilities exist in a wired-but-inert
state: Shard toggle (`ShardFieldBus` bodies commented, zero listeners — consumes input, does
nothing), Squirrel align toggle (dead in BOTH generations), FireTrailBlock (complete R_ pair,
never authored/wired), Manta mine-decoy (`Execute` empty, implementation commented, orphan
asset). Mark any to FINISH; unmarked ones get deleted. *(Recommended: delete all four — a
commented-out implementation is not a backlog; git preserves the designs.)* Gate: YASH Y6.4/Y7.

**D12 — Mouse-look flight (PC).** The deleted dead keyboard strategy was the only holder of
mouse-look flight + cursor-lock. For PC expansion: (a) **drop for now, note in the input backlog**
*(recommended)*; (b) port mouse-look into the live strategy as part of Y3. Gate: YASH Y3.

**D13 — Camera end state.** Shipped reality is permanent Cinemachine/custom coexistence with live
seam-bridging machinery and a bypassed `ICameraController` abstraction (AUDIT §2.10).
Options: (a) **declare coexistence final — rewrite `CameraMigrationReview.md`, widen or drop the
fiction interfaces (S)** *(recommended now)*; (b) commit to the full Cinemachine unification (L)
as a scheduled project. Gate: YASH Y8.2. **[hard-gate for option b]**

**D14 — Flow/Warp fields.** Two parallel copy-paste SO vector-field systems, BOTH runtime-dead
(AUDIT §1.9). If a "field" fundamental is on the roadmap it should go through the CLAUDE.md
fundamentals-curation process anyway. Options: (a) **delete both families** *(recommended)*;
(b) keep one generalized base parked; (c) planned soon — keep. Gate: YASH Y6.5.

**D15 — Notification banner system.** Complete receive-side pipeline with ZERO senders, nested
into 5 of 11 vessel prefabs; its role is served by `ToastNotificationAPI` (AUDIT §1.6).
(a) **delete the family + de-nest** *(recommended)*; (b) reserve it (still de-nest from vessels).
Gate: SHOMBITH S1.2.

**D16 — RESOLVED by owner 2026-07-20: convert to the networked single-host model —
executed (C3): scene rebuilt on the converted-blitz pattern, added to EditorBuildSettings,
`SandboxBenchmarkController` re-parented onto the MP spine + wired.** Original entry:

**D16 — Benchmark.** The Settings "Run Benchmark" button targets a scene that isn't in the build,
and the scene carries a different controller than the docs specify (AUDIT §1.7). (a) **fix it:
add `BenchmarkStressTest.unity` to EditorBuildSettings + wire `SandboxBenchmarkController` as
documented** *(recommended — Shombith built the scene)*; (b) remove the button for now.
Note: D19 (SP-path retirement) needs this resolved first — the benchmark scene currently runs on
the single-player controller. Gate: SHOMBITH S0.2.

### Architecture direction

**D17 — Music-on-FMOD.** The split is not load-bearing and the class header prescribes the
migration, but it needs FMOD Studio authoring (music events/bank). (a) **defer the full
migration; do the ~150-line dead-member cleanup now** *(recommended)*; (b) migrate now.
Gate: YASH Y8.1.

**D18 — Singleton vs DI.** Same services reachable both ways; consumers split (CameraManager: 48
`.Instance` / 0 `[Inject]` — AUDIT §3.7). (a) **pragmatic per-service** *(recommended:
CameraManager commits to `.Instance` + drops the dead DI registration; AudioSystem/GameSetting
funnel new code to `[Inject]`; prism-manager self-creating singletons whitelisted)*; (b) full
`[Inject]` everywhere; (c) defer. Gate: YASH Y5.4.

**D19 — RESOLVED by owner 2026-07-20: go — solo modes are retired as a concept
(solo = party-of-one host). Executed (C1-C4): duel + blitz consolidated onto their MP
modes, benchmark converted, SP spawn path deleted.** Original entry:

**D19 — Retire the single-player spawn path + controller branch.** Two scenes remain
(CellularDuel, WildlifeBlitz); the direction is already declared in three places ("solo = host +
AI"). (a) **go** *(recommended)*; (b) hold. Depends on D16 (benchmark scene rides the SP
controller today). Gate: YASH Y2. **[hard-gate]**

**D20 — Scoreboard stats framework winner.** `EventDrivenStatsProvider` ships;
`UniversalStatsProvider` (+ editor + interface + 3 authored assets) was a stillborn rival
(AUDIT §1.9). (a) **EventDriven wins — delete the Universal framework** *(recommended)*;
(b) Universal was the intended direction — wire it instead. Gate: SHOMBITH S4.2.

**D21 — RESOLVED by owner 2026-07-20: the flag is retired outright rather than replaced
(solo modes no longer exist, so there is nothing for a replacement signal to distinguish).
Executed (C5): both behavioral reads deleted — the `MultiplayerSetup` gate died with the whole
legacy matchmaking path (provably dead: `ResetAllData()` runs before sign-in), and presence now
advertises every in-game scene; analytics reads `ConnectedClientsIds.Count > 1` at report time.
See `Docs/ScoringSystem/ARCHITECTURE.md` §8 for the per-site resolution table.** Original entry:

**D21 — `IsMultiplayerMode` replacement signals.** Two behavioral reads remain
(`MultiplayerSetup.cs:84` session gate; `HostConnectionService.cs:1860` presence). REFACTOR.md Q1
requires sign-off on the replacement signals (party human count / requested-session semantics /
ApplicationState). Approve the concrete signals when Yash's Y1.4 design note lands. **[hard-gate]**
→ **The Y1.4 design note has landed:** `Docs/ScoringSystem/ARCHITECTURE.md` §8 now carries the
refreshed (measured 2026-07-20) fork map + the per-site replacement proposals — session gate →
`HostConnectionDataSO.PartyMembers.Count > 1`; presence → ApplicationState==InGame + the
party-session id already in `FriendPresenceActivity`. Mark this decision to unblock R1/Y1.4
execution.

---

## 2. Garrett's work items

| # | Item | Notes |
|---|---|---|
| G1 | Mark up §1 (this sheet) | D4, D13(b), D19, D21 are hard gates; everything else has a safe default |
| G2 | In-editor A/B for D10 (Squirrel skim: crackle vs legacy vs both) | 5 min in Menu_Main freestyle; it's a look call |
| G3 | Camera feel check for D13 | Fly menu→freestyle handoff; if the current seam feels fine, option (a) costs nothing |
| G4 | Review Wave-1 deletion PRs from Yash/Shombith | The risk lens: anything that smells like intent-to-use, bounce back to this sheet |
| G5 | `FLEET_MAPS.md` §2 markup (pre-existing) | The level-5 upgrade proposals are still waiting on you |
| G6 | After D3: eyeball the `_Archive` move | Confirm nothing archived is on your near-term design list |

## 3. Starting prompt (your next Claude Code session)

```
Read Docs/UnifiedSystems/GARRETT.md — I've marked up the Section 1 decision sheet.
Propagate my choices: update the D-gates in Docs/UnifiedSystems/YASH.md and
Docs/UnifiedSystems/SHOMBITH.md (unblock / re-scope / remove items per my markup),
execute any small decision-contingent items that are now unambiguous (e.g. the D3
card de-listing if I chose it), commit to a claude/unified-decisions-* branch, and
give me a summary of what each of Yash and Shombith is now cleared to start.
Evidence for every item is in Docs/UnifiedSystems/AUDIT.md — verify wiring claims
with guid greps at the moment of change, per that doc's method.
```

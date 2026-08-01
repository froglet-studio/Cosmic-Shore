# Unified Systems — Shombith (UI consolidation, screens/HUD, tooling & project hygiene)

**Companions:** `AUDIT.md` (evidence — § references point there) · `GARRETT.md` §1 (the D-gates).
**Gating:** `[default-ok Dx]` = proceed on the recommended default unless Garrett's markup says
otherwise. `[hard-gate Dx]` = wait for explicit markup.
**Discipline:** one item per commit; guid-grep verify wiring claims at the moment of change
(AUDIT method header); prefab edits through the Unity editor (not hand-edited YAML) wherever
possible; re-verify AUDIT line numbers before editing.

Why you: this doc is screens, HUD prefabs, panels, settings/benchmark, and editor-side
enforcement — the surface you already own (Maelstrom UI, Connecting Panel, player cards,
SettingsModal, the benchmark scene, canvas tooling). Note: S0.1 is literally your Maelstrom UI.

---

## S0 — Unblocked, start immediately

- **S0.1 Domain-color re-unification (AUDIT §2.1 bullet, §fresh-sweep).** R5 unified all domain
  UI color onto `ThemeManagerData.GetDomainUIColor` (`SO_ColorSet`), then the Tournament/
  Connecting UI shipped reading `DomainColorPaletteSO` again — and `TournamentSceneView.DomainColor`
  *prefers* the palette over the theme, so Maelstrom screens can disagree with the scoreboard for
  the same domain. Migrate the six consumers (`TournamentSceneView.cs:44`,
  `TournamentPlayerCard.cs:34`, `TournamentSummaryPlayerCard.cs:27`,
  `TournamentDomainScoreView.cs:23`, `TournamentRoundCard.cs:39`,
  `ConnectingPanelController.cs:29` — note its `Color.white` fallback) to
  `GameDataSO.ThemeManagerData.ColorSet.GetDomainUIColor`, then delete `DomainColorPaletteSO` +
  `DomainColorPalette.asset` (wired in 6 scenes + 4 prefabs — guid `d202844a…`), and re-save the
  5 `HUD/*SilhouetteConfig.asset` files carrying orphaned `domainPalette` refs. Also: add
  `goldGoalColor` to `AstroLeagueSettingsSO` and remove the inline palettes in
  `AstroLeagueBall`/`AstroLeagueArena`; keep `ToyFactory`'s literals only as the fallback behind
  its theme-first read. Update the stale R5 note in `Docs/ScoringSystem/REFACTOR.md`.
  *Verify: Jade/Ruby/Gold identical across Maelstrom cards, connecting panel, scoreboard, HUD.
  If Maelstrom intentionally uses distinct tints, STOP and fold them into `SO_ColorSet` as named
  roles instead — flag to Garrett.*
- **S0.2 Benchmark `[default-ok D16]`.** Add `BenchmarkStressTest.unity` to EditorBuildSettings
  and wire the controller the docs specify (`SandboxBenchmarkController` — the scene currently
  carries `SinglePlayerWildlifeBlitzController`; AUDIT §1.7), or remove the SettingsModal button
  if D16 says so. Update `Docs/SettingsSystem/ARCHITECTURE.md` + `Docs/SCENES.md` to match.
  This unblocks Yash's Y2 (the SP-path retirement).
- **S0.3 ResourceDisplay/ResourceButton strip (AUDIT §1.6).** Zero code callers, zero
  UnityEvent/anim-event callers, legacy branch commented. In-editor: remove the inert components
  from `SparrowHUDVariant` (×3), `SquirrelHUDVariant`, `SerpentHUDVariant` (incl. nested
  `Wall Button`), and the shared base `VesselHUDPrefab` (×4 — it propagates into the shipping
  variants); delete orphan prefabs `EnergyDisplay`/`BoostDisplay`/`ItemDisplay`; then delete
  `ResourceDisplay.cs` (+ inline editor) and `ResourceButton.cs`. Leave the
  `Panels/VesselHUD.prefab` → `HUDContainer.prefab` → Termite chain alone until **D1** resolves.
  *Verify: each HUD variant renders unchanged in a game scene + menu freestyle.*

## S1 — Toast/notification consolidation `[default-ok D15]`

- **S1.1** Keep the three live surfaces (in-HUD chat stack / app swipe toasts / domain event
  feed — they're genuinely distinct; AUDIT §2.4). Consolidate plumbing: one shared
  transient-message settings SO shape, one pooled item-view/DOTween core the three presenters
  compose, and one channel style — migrate `ToastChannel`'s plain C# event to a SOAP
  `ScriptableEvent` (the other two already conform).
- **S1.2** Delete the sender-less Notification System family (7 scripts, 2 prefabs, channel +
  settings assets) AND de-nest `NotificationPresenter.prefab` from the 5 vessel prefabs
  (Manta/Squirrel/Rhino/Dolphin/Serpent). If D15 says reserve it, still de-nest — a screen-space
  banner does not belong inside vessel prefabs.

## S2 — MiniGameHUD dedup `[default-ok; D1 caveat]`

Extract the three hand-duplicated behaviors (`onShipHUDInitialized` reparent loop,
`DomainVolumeIndicator` attach, local-HUD show/hide) into shared helpers or a slim base that both
`MiniGameHUD` and `MenuMiniGameHUD` consume — making CLAUDE.md's "full MiniGameHUD can replace
this later" path real (AUDIT §2.3). Then retire the dead reparent channel: per **D1**, either
delete `ShipHUD` + `HUDContainer` chain + the three subscriptions + the `ShipHUDData` SOAP family,
or (if Termite keeps it) leave the raiser and delete only the dead `GameCanvas.cs` copy
(coordinate — Yash deletes `GameCanvas.cs` in Y4.3).

## S3 — Fleet HUD contract + enforcement (the multiplier)

- **S3.1** `VesselStatus.vesselHUDController`: change the raw `MonoBehaviour` field to
  `[SerializeField, RequireInterface(typeof(IVesselHUDController))]` — the project's own
  attribute, already used 15 lines above (AUDIT §3.2).
- **S3.2** Move `SquirrelVesselHUDController.cs` + `DolphinVesselHUDController.cs` out of
  `R_VesselActions/Data Containers/` into `_Scripts/UI/Controller/` (guid-preserving file moves);
  normalize namespace (`CosmicShore.UI`) and Sparrow's naming (`SparrowHUDController` →
  `SparrowVesselHUDController`, rename-in-place so the guid survives). Same treatment for
  `CloakSeedWallActionSO.cs` (UI/View → Data Containers — it's a live gameplay SO misfiled in UI).
- **S3.3** **The fleet-contract edit-mode test** (AUDIT §3.1): iterate `VesselClassType` and
  assert per-tier obligations exist — playable vessels (flag on `SO_Vessel`): prefab in
  `VesselPrefabContainer`, HUD controller wired, camera SO, telemetry, class SO outside `_TEMP`;
  planned vessels: prefab + class SO only. Ability maps + elementBars stay opt-in (documented
  rollout tiers) — assert only that *existing* assets resolve. Follow the `EnumIntegrityTests`
  precedent. Relocate `FalconClassSO`/`ShrikeClassSO` out of `_SO_Assets/_TEMP/`.
- **S3.4** File==type edit-mode test + the Vessel/Ship renames (AUDIT §3.8): rename the 14
  remaining `Ship*` types in place (guid-safe — rename the class inside the existing file):
  `R_ShipElementStatsHandler`→`R_VesselElementStatsHandler`, `ShipHelper`→`VesselHelper`,
  `SlowShipViewer`→`SlowVesselViewer`, `ShipActionSO`/`ShipActionExecutorBase` etc. No
  missing-script risk on Unity 6 (verified), but grep-ability + doc consistency. Coordinate with
  Yash so it doesn't collide with Y1/Y3 branches — land as one dedicated rename PR.
- **S3.5** Animation hygiene (AUDIT §fresh-sweep): rename-in-place `BufoAnimation`→
  `GrizzlyAnimation`, `RiptideAnimation`→`DolphinAnimation`, fix the `MantaAnimationContoller`
  typo (wired in SIX prefabs). Delete the three dead classes (`DolphinAnimation` old,
  `SparrowAnimationController`, `SingleStickAnimationController`) only per **D1** — Sparrow's
  dedicated controller might be intended work.

## S4 — Stats/scoreboard UI consolidation

- **S4.1** Per-mode UGS stats reporting (AUDIT §2.7): one generic `StatsReporter` (metric +
  better-direction from the mode's `ScoringRuleSO`/golf flag), one
  `UGSStatsManager.ReportModeResult(mode, intensity, value)` API, one best-value dictionary keyed
  `(mode, intensity)` replacing the four clone `*PlayerStatsProfile` classes — with CloudSave
  payload migration on first load. Kills the `LogControlWindow` special-casing too. Coordinate
  with Yash's Y1 (same end-game path).
- **S4.2** `[default-ok D20]` Delete the stillborn `UniversalStatsProvider` framework (+ editor,
  `IStatExposable`, `StatModuleSO`, 3 orphaned assets, the `IStatExposable` impl on
  `HexRaceScoreTracker`); port `WildlifeBlitzStatsProvider` onto `VesselStatEventSO` assets so
  `EventDrivenStatsProvider` serves every mode.

## S5 — Project hygiene `[decision-gated]`

- **S5.1** `[default-ok D3]` De-list the 5 dead cards from `ArcadeGames.asset` (stops shipping
  broken buttons) and move the ~23 scene-less `SO_ArcadeGame` assets + `PreviousAllGames.asset`
  to `_SO_Assets/Games/_Archive/` (or delete, per markup). Delete the dead mode-32 stack
  (`MultiplayerWildlifeBlitzMiniGame.cs`, its orphaned asset, the mislabeled scene) unless D3
  says otherwise; annotate retired `GameModes` IDs in the enum.
- **S5.2** `[hard-gate D2/D4]` Hide/disable the mission modal + Store screen entries per markup
  (your lane: ScreenSwitcher/modal wiring). Yash owns the code-side ports/deletions.
- **S5.3** `[default-ok D8]` Archive the unmounted dialogue runtime (move `System/Runtime/` to an
  archive folder or delete per markup; keep `_Scripts/DialogueSystem/` data + editor window);
  update the CLAUDE.md Dialogue section to "authored, not yet mounted."
- **S5.4** Doc sweep: CLAUDE.md stale rows found by the audit — `DomainAssigner.cs` /
  `NetworkStatsManager.cs` (neither exists anymore), `HexRaceHUD.cs` row, the mode-32 scene
  table, "R_VesselElementStatsHandler" type-name drift (resolves with S3.4). Update
  `Docs/SCENES.md` for SlipnStride/benchmark per D2/D16.

## Starting prompt (new Claude Code session)

```
Read Docs/UnifiedSystems/AUDIT.md (method + evidence), Docs/UnifiedSystems/SHOMBITH.md
(my work list), and Docs/UnifiedSystems/GARRETT.md §1 (decision gates — unmarked
decisions mean the recommended default applies only to [default-ok] items; [hard-gate]
items wait). Start with S0.1 (domain-color re-unification — this is the Maelstrom UI I
built, so preserve its look: if the palette tints were intentional, fold them into
SO_ColorSet as named roles rather than flattening them, and flag it). One item per
commit on a claude/unified-shombith-<item> branch off bleeding-edge. Before every
deletion, re-verify the AUDIT claim with a guid grep (.cs.meta guid across
*.prefab/*.unity/*.asset) plus a repo-wide class-name grep. Prefab component removal
happens in the Unity editor, then verify each affected HUD/screen renders unchanged.
Renames are class-renames inside the existing file so the .meta guid survives.
```

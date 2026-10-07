# In-Game Toast System

The in-game toast feed (successor to `GameEventFeed`). Gameplay posts **situations** with
raw arguments; all copy lives in per-mode **`GameToastConfigSO`** assets; display goes
through an **editor-authored prefab** (nothing is generated from code). New lines appear
at the bottom of a scroll view and push older lines **up** — entries never disappear, they
dim with age and stay scrollable, bounded only by a retention cap.

## Data flow

```
Gameplay (any machine, local-only — nothing extra crosses the wire)
  └─ GameToastAPI.Post(situation, domain(s), args…)          [static, Resources channel]
      └─ Resources/Channels/GameToastChannel.asset            [ScriptableEventGameToastData]
          └─ GameToastController.HandleToast                  [on the toast panel prefab]
              ├─ GameToastLibrarySO.TryResolve(gameData.GameMode, situation)
              │    ├─ mode config first  (e.g. GameToastConfig_Joust)
              │    ├─ shared config second (GameToastConfig_Shared)
              │    └─ unresolved → NO toast (that's the per-mode opt-out; Scurry is empty)
              ├─ string.Format(template, enriched args)       [colored names, live joust pts]
              └─ GameToastView.Spawn(message, colors, alpha)
                  └─ Instantiate GameToastItemView prefab into the scroll content
```

`GameDataSO.OnPlayerAdded` is bridged by the controller (PlayerJoined), so joins need no
call site. `PlayerReady` / `PlayerDisconnected` post from `MultiplayerDomainGamesController`,
`Joust` from `VesselExplosionBySkimmerEffectSO.ExecuteConfirmed`, `BroodWaveScored` from
`BroodRushController`, `ComebackActivated` from `ElementalComebackSystem` (local player,
rising edge). `Overtake` / `NewRaceLeader` are produced by `RaceRankToastDriver`, the per-player STAT
toasts (`CrystalCollected`, `RocketHit`, `BendLanded`, `PrismsDestroyedMilestone`,
`LifeformKilled`) by `StatToastDriver`, and idle hints (e.g. the joust hint) by the
controller itself — all purely config-driven. **Both drivers live on `NotificationUI.prefab`
beside the controller** (2026-09-08: `RaceRankToastDriver` had been authored but placed on no
prefab, so Skim Race's overtake and leader toasts had never fired).

**Why the stat toasts are a POLL over `RoundStats` rather than a post at the credit site**: a
crystal collection resolves server-only and a combat hit is credited on the owner's machine
plus the server, so a post at either site reaches one or two peers. Every stat the driver reads
is a server-written NetworkVariable on the persistent Player object, so polling it fires the
same toast on every machine with nothing crossing the wire — the shape `RaceRankToastDriver`
already used. A player first seen mid-turn is seeded silently (no backlog announced), and a
config entry's `everyN` says how often the total must cross a multiple before the toast fires
(Skim Race announces every crystal; Scurry every tenth).

## Situations (`GameToastSituation`) and template placeholders

| Situation | Raised by | Placeholders |
|---|---|---|
| `PlayerJoined` (1) | controller ← `GameDataSO.OnPlayerAdded` | `{0}` name |
| `PlayerReady` (2) | `MultiplayerDomainGamesController` | `{0}` name |
| `PlayerDisconnected` (3) | `MultiplayerDomainGamesController` | `{0}` name |
| `Joust` (10) | `VesselExplosionBySkimmerEffectSO` | `{0}` scorer, `{1}` scorer pts, `{2}` target, `{3}` target pts |
| `JoustIdleHint` (11) | controller (idle hint) | — |
| `Overtake` (20) | `RaceRankToastDriver` | `{0}` overtaker, `{1}` overtaken |
| `NewRaceLeader` (21) | `RaceRankToastDriver` | `{0}` leader |
| `ComebackActivated` (30) | `ElementalComebackSystem` | `{0}` player |
| `BroodWaveScored` (40) | `BroodRushController` | `{0}` domain, `{1}` brood sum, `{2}` target |
| `CrystalCollected` (80) | `StatToastDriver` (CrystalsCollected rose) | `{0}` player, `{1}` new total, `{2}` increase, `{3}` objective target (0 if none) |
| `RocketHit` (81) | `StatToastDriver` (MissileHitsLanded rose) | same four |
| `BendLanded` (82) | `StatToastDriver` (DebuffHitsLanded rose) | same four |
| `PrismsDestroyedMilestone` (83) | `StatToastDriver` (HostilePrismsDestroyed crossed a multiple of `everyN`) | same four |
| `LifeformKilled` (84) | `StatToastDriver` (LifeformsKilled rose) | same four |
| `PrismsStolenMilestone` (85) | `StatToastDriver` (PrismStolen crossed a multiple of `everyN`) | same four |
| `WreckingBallLeadChanged` (92) | `WreckingBallController` | `{0}` domain, `{1}` prisms, `{2}` target |
| `WreckingBallForgeHint` (93) / `WreckingBallDashHint` (94) | controller config (idle hints) | — |
| `UndertowQuarter` (95) / `UndertowHalf` (96) / `UndertowLeadChanged` (97) | `UndertowController` | `{0}` domain, `{1}` points, `{2}` target |
| `UndertowDashHint` (98) | controller config (idle hint) | — |
| `BroadsideQuarter` (112) / `BroadsideHalf` (113) / `BroadsideLeadChanged` (114) | `BroadsideController` | `{0}` domain, `{1}` points, `{2}` target |
| `BroadsideVerbHint` (115) / `BroadsideCloseHint` (116) | controller config (idle hints) | — |
| `RegattaRailHint` (110) / `RegattaLaneHint` (111) | controller config (idle hints) | — |
| `WaystationRingHint` (117) / `WaystationFoldHint` (118) | controller config (idle hints) | — |
| `DustupQuarter` (119) / `DustupHalf` (120) / `DustupLeadChanged` (121) | `DustupController` | `{0}` domain, `{1}` points, `{2}` target |
| `DustupAboveHint` (122) | controller config (idle hint) | — |
| `TapestryLeadChanged` (123) | `TapestryController` (sampled after 20 s, 10 s minimum gap) | `{0}` domain, `{1}` volume standing |
| `TapestryPaintHint` (124) / `TapestryRaidHint` (125) | controller config (idle hints) | — |
| `SiroccoLeadChanged` (126) | `SiroccoController` (after 20% of the target) | `{0}` domain, `{1}` prisms, `{2}` target |
| `SiroccoDustHint` (127) | controller config (idle hint) | — |
| `DomainRaceQuarter` (128) / `DomainRaceHalf` (129) / `DomainRaceLeadChanged` (130) / `DomainRaceHomeStretch` (131) / `DomainRaceFinalLap` (132) | `DomainRaceToasts`, ticked by `GateRaceController` (every gate race) and by `RampageController` / `SalvoController` / `HijackController` | `{0}` leading domain, `{1}` its score, `{2}` target, `{3}` its best single pilot |
| `AstroLeagueGoal` (133) / `AstroLeagueMatchPoint` (134) | `AstroLeagueController.AnnounceGoal_ClientRpc` | goal: `{0}` scorer, `{1}` their domain's goals, `{2}` goal limit; match point: `{0}` domain, same `{1}` `{2}` |
| `AstroLeagueGoldenGoal` (135) | `AstroLeagueController.AnnounceOvertime_ClientRpc` | — |
| `SalvoWingReload` (136) | `SalvoController.RefuelDomainMissiles_ClientRpc`, only when the domain fields 2+ pilots | `{0}` collector, `{1}` domain |
| `WildlifeCoreBreached` (56) | `WildlifeLiberationController` (server samples vessel positions; first pilot inside the 200u core cage, once a match) | `{0}` pilot, `{1}` domain |

The Dog Fight (57-59), Bends (60-62), Cleave (50-52) and Wildlife Liberation (53-55)
milestone situations are posted by their controllers (`{0}` is the leading DOMAIN in all of them),
and every one of those four modes now authors them.

**`DomainRaceToasts` — the shared race beats.** A plain class a controller ticks every frame
(it throttles itself to 0.5 s). It folds the mode's own `ScoringRuleSO.DomainValue` per active
domain — the SUM in a summed race, the LEAD RUNNER in a gate race — against `TargetFor`, so it
needs no per-mode code. It is a LOCAL poll over replicated RoundStats on every peer (the
`StatToastDriver` shape), so it adds no RPC. The first poll of a turn seeds silently; a tie at
the top has no leader; a lead change is announced only once the leader is past the quarter beat
and at most every 8 s. `GateRaceController` passes a 3-gate home stretch and, on a lapped
course, the first gate of the last lap (`RaceLengthFor(rings, leadIn, laps - 1)`). This is the
gate-threaded hook Waystation's note below said the platform lacked — Regatta and Waystation
post these beats too, but their generator-owned configs do not author them yet.

Joust points are read from `RoundStatsList` at display time (StatsManager has already
recorded the joust locally when the post arrives, so the count includes the new point).

## Config assets (`_SO_Assets/Game Toasts/`)

| Asset | Mode | Authored situations |
|---|---|---|
| `GameToastConfig_Shared` | all | joined / Ready / disconnected (disconnect dimmed to 0.7) |
| `GameToastConfig_Joust` | Joust (34) | `{0}({1}) jousted {2}({3})` two-tone + 60s idle hint "Fly close to an opponent at high speed to joust them" (repeats while idle) |
| `GameToastConfig_SkimRace` | SkimRace (33) | `{0} overtook {1}`, `{0} is the race leader`, `Comeback system is on`, `{0} collected a crystal ({1}/{3})` (every crystal) |
| `GameToastConfig_Scurry` | Scurry (35) | `{0} has collected {1} crystals` (`everyN` 10) — the only mode-specific toast; shared toasts still show |
| `GameToastConfig_DogFight` | DogFight (41) | `{0} landed a rocket!` (every skyburst that reaches a pilot), the quarter / half / lead-change milestones the controller posts, `Comeback system is on` |
| `GameToastConfig_Bends` | Bends (42) | `{0} bent a rival! ({1}/{3})` (every debuff landed), the quarter / half / lead-change milestones, `Comeback system is on` |
| `GameToastConfig_BroodRush` | BroodRush (38) | `{0} brood hatched - {1}/{2}` |
| `GameToastConfig_WreckingBall` | WreckingBall (54) | `{0} has wrecked {1} prisms` (`everyN` 250), the lead-change beat, two idle hints (forge a ball / dash beside the forest), `Comeback system is on` — authored by `author_wrecking_ball_assets.py` |
| `GameToastConfig_Regatta` | Regatta (56) | two idle hints (the rail in your colour is the racing line; ride it / skim it / fly beside it), `Comeback system is on` — authored by `author_regatta_assets.py` |
| `GameToastConfig_Broadside` | Broadside (57) | `{0} landed a hit!` (every hit), the quarter / half / lead-change milestones, two idle hints that name the VERB rather than the hull (seven hulls share four verbs, and a hint per hull is seven hints nobody reads), `Comeback system is on` — authored by `author_broadside_assets.py` |
| `GameToastConfig_Waystation` | Waystation (58) | TWO IDLE HINTS AND NOTHING ELSE (thread every ring around you; hold the fold and aim at the next cluster). The absence is the decision: the gate-race platform has NO gate-threaded hook, so a milestone or lead-change situation would have no poster — and an enum member nothing raises reads exactly like a feature. An idle hint needs no poster at all, which is why Regatta authored only hints too. Authored by `author_waystation_assets.py` |
| `GameToastConfig_Dustup` | Dustup (59) | the quarter / half / lead-change milestones, one idle hint (get ABOVE a rival — the dust hangs below you) |
| `GameToastConfig_Tapestry` | Tapestry (60) | the lead-change beat, two idle hints (Mass mode paints a wide wake that is score; behind? Dust mode raids their painting) |
| `GameToastConfig_Sirocco` | Sirocco (61) | `{0} has eroded {1} prisms` (`everyN` 100), the lead-change beat, one idle hint (Dust mode, low and long over the forest) |
| `GameToastConfig_Undertow` | Undertow (55) | `{0} dragged a rival through the undertow!` (every bend), `{0} has drowned {1} creatures` (`everyN` 3), the quarter / half / lead-change milestones, an idle dash hint, `Comeback system is on` — authored by `author_undertow_assets.py` |
| `GameToastConfig_Rampage` | Rampage (2) | `{0} has smashed {1} prisms` (`everyN` 250), the quarter / half / lead-change race beats, `Comeback system is on` |
| `GameToastConfig_Cleave` | Cleave (39) | `{0} has cut {1} prisms` (`everyN` 200), the quarter / half / lead-change milestones the controller posts (50-52), `Comeback system is on` |
| `GameToastConfig_Salvo` | Salvo (44) | `{0} has levelled {1} prisms` (`everyN` 100), `{0} reloaded the wing` (the wingman reload, 2+ pilot domains only), the quarter / half / lead-change race beats, `Comeback system is on` |
| `GameToastConfig_Hijack` | Hijack (46) | `{0} has stolen {1} prisms` (`everyN` 100), the quarter / half / lead-change race beats, `Comeback system is on` |
| `GameToastConfig_WildlifeLiberation` | WildlifeLiberation (40) | `{0} has hunted {1} creatures` (`everyN` 5), the quarter / half / lead-change milestones (53-55), `{0} broke into the core!` (56), `Comeback system is on` |
| `GameToastConfig_Switchback` / `_Skein` | Switchback (45) / Skein (51) | halfway / lead change / home stretch (`gate` / `ring`), `Comeback system is on` — open chains, so no final lap |
| `GameToastConfig_Headlong` / `_Redline` / `_Breakwater` | Headlong (49) / Redline (53) / Breakwater (50) | halfway / lead change / home stretch (`gate` / `gate` / `station`) + `{0} is on the final lap`, `Comeback system is on` |
| `GameToastConfig_AstroLeague` | AstroLeague (37) | `{0} scores! {1}/{2}`, `MATCH POINT - {0} needs one more goal`, `Golden goal - the next one wins`, `Comeback system is on` |
| `GameToastLibrary` | — | shared + every mode config above |
| `GameToastSettings` | — | slide-in, age dim, retention cap, auto-scroll |

Adding a mode = create a `GameToastConfigSO` (menu: `ScriptableObjects/UI/Game Toast
Config`), set its `gameMode`, author entries, add it to `GameToastLibrary`. Adding copy to
an existing mode = add an entry; no code. **Idle hints** are entries with `isIdleHint` on:
they show after `idleSeconds` of an active turn without the `resetOnSituation` firing.

## Building the toast panel prefab (UI-side checklist)

Suggested hierarchy (author freely — only the wired references matter):

```
GameToastPanel                    [GameToastController, GameToastView, RaceRankToastDriver]
└── Scroll View                   [ScrollRect  — vertical only, Clamped]
    └── Viewport                  [RectMask2D or Mask+Image]
        └── Content               [VerticalLayoutGroup, ContentSizeFitter (vertical
                                   Preferred), anchors/pivot at the BOTTOM so lines grow up]
```

Wire on **GameToastController**: `toastChannel` → `Resources/Channels/GameToastChannel`,
`library` → `GameToastLibrary`, `view` → the `GameToastView` on the same object.
Wire on **GameToastView**: `settings` → `GameToastSettings`, `itemPrefab` → the toast item
prefab, `scrollRect` → the Scroll View, `contentContainer` → Content.
Wire on **RaceRankToastDriver** and **StatToastDriver**: `library` → `GameToastLibrary`.

Toast item prefab: root with `CanvasGroup` + `GameToastItemView` + `LayoutElement`, a
TMP text child wired to `messageText` (rich text on), optional `accentImage` (tinted with
the primary domain color) and `background`. Only X is tween-animated — the layout group
owns Y, so keep the root free of position-driving components.

Place the panel instance under each game scene's HUD (the old `NotificationUI` spot in
`GameCanvas.prefab` / `GameCanvas-SkimRace.prefab` — the `GameEventFeed` component was
removed from both; the `Player Vessel Selection` container objects there can be reused or
replaced). Scene `ContainerScope` is required for the `[Inject] GameDataSO` fields.

**`[GameToastView] Missing references` with a fully wired `NotificationUI.prefab`** means the
reference was nulled by an OVERRIDE on the prefab instance that nests it — the CORE canvas
shipped one on `itemPrefab` (`Docs/GAMECANVAS.md` §9.3). Look at the instance's
`m_Modifications` for `propertyPath: itemPrefab` / `settings` / `contentContainer` with
`objectReference: {fileID: 0}` and delete the override; never re-author the reference into the
outer prefab or the scene, which is how the override got there.

## Rules honored

- **Config separation**: every string, duration and threshold lives in SO assets.
- **SOAP**: posting goes through a `ScriptableEvent` channel; controller/view/driver are
  scene components with serialized references; `EventListenerGameToastData` exists for
  inspector-wired listeners.
- **Fail loud**: the controller subscribes to `toastChannel` without a null guard; a bad
  template logs an error naming the situation and template.
- **Single color source**: domain colors resolve through `GameDataSO.ThemeManagerData`
  (the same `SO_ColorSet` vessels and prisms use); `ThemeManager` hands the set to the
  static API at game start.
- **No per-frame allocation paths**: the rank driver polls at `pollInterval` over the
  existing `RoundStatsList`; toasts are event-driven.

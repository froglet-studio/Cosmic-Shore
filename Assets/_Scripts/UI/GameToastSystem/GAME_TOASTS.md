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
rising edge). `Overtake` / `NewRaceLeader` are produced by `RaceRankToastDriver`, and idle
hints (e.g. the joust hint) by the controller itself — both purely config-driven.

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

Joust points are read from `RoundStatsList` at display time (StatsManager has already
recorded the joust locally when the post arrives, so the count includes the new point).

## Config assets (`_SO_Assets/Game Toasts/`)

| Asset | Mode | Authored situations |
|---|---|---|
| `GameToastConfig_Shared` | all | joined / Ready / disconnected (disconnect dimmed to 0.7) |
| `GameToastConfig_Joust` | Joust (34) | `{0}({1}) jousted {2}({3})` two-tone + 60s idle hint "Fly close to an opponent at high speed to joust them" (repeats while idle) |
| `GameToastConfig_SkimRace` | SkimRace (33) | `{0} overtook {1}`, `{0} is the race leader`, `Comeback system is on` |
| `GameToastConfig_Scurry` | Scurry (35) | **empty for now** (by request — shared toasts still show) |
| `GameToastConfig_BroodRush` | BroodRush (38) | `{0} brood hatched - {1}/{2}` |
| `GameToastLibrary` | — | shared + the four mode configs |
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
Wire on **RaceRankToastDriver**: `library` → `GameToastLibrary`.

Toast item prefab: root with `CanvasGroup` + `GameToastItemView` + `LayoutElement`, a
TMP text child wired to `messageText` (rich text on), optional `accentImage` (tinted with
the primary domain color) and `background`. Only X is tween-animated — the layout group
owns Y, so keep the root free of position-driving components.

Place the panel instance under each game scene's HUD (the old `NotificationUI` spot in
`GameCanvas.prefab` / `GameCanvas-SkimRace.prefab` — the `GameEventFeed` component was
removed from both; the `Player Vessel Selection` container objects there can be reused or
replaced). Scene `ContainerScope` is required for the `[Inject] GameDataSO` fields.

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

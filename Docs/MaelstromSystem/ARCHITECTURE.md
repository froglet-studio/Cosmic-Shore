# Maelstrom System — Architecture

Canonical reference for **Maelstrom Mode** — the session-level meta (P3 / R2) that strings the
competitive domain minigames into one tournament with a per-DOMAIN leaderboard. The pool is the
sixteen modes that satisfy §1's admission criteria, laddered by pick-up difficulty (§1.1).

> **See also:** `MAELSTROM_UX_HANDOFF.md` (same folder) — session handoff for the between-game splash,
> readable dwell, the Shuffle→Maelstrom display rename, and the summary `(You)` owner tag, with sequence
> diagrams, the inspector wiring those features depend on, and recommended next tasks.

> **Status:** implemented end-to-end. Code, data assets, **and** the Unity-editor wiring (scene
> content, prefab buttons, inspector references) are all in place: the Maelstrom scene drives both
> its lobby and summary layouts, and every domain game's Scoreboard carries a host-only Continue
> button (see **§7**, now complete). Remaining work is the **§9 Deferred** backlog (rewards, share
> screen, instrumentation) only.

> **Player-facing name — "Maelstrom":** the Arcade card for this mode is shown to players as **Maelstrom**
> (`ArcadeGameMaelstrom.asset` `DisplayName = "Maelstrom"`, rendered by `GameCard`). The in-scene
> lobby/summary banner is **data-driven from that same field** — `MaelstromSceneView.ModeName` reads
> `MaelstromDataSO.ModeCard.DisplayName` (the `ModeCard` reference is wired to the card asset), so the
> card's `DisplayName` is the **single source** of the player-facing name. The code, data
> (`MaelstromDataSO`), enum (`GameModes.Maelstrom = 36`), controller, and this doc keep the
> **Maelstrom** name — *Shuffle and Maelstrom are the same meta-mode*. (The **scene file** was renamed
> to `Maelstrom.unity` in the v2 rework; only the file name changed — the classes/data/enum stay Maelstrom.)
> **To rename the mode, change
> only the card's `DisplayName`** — full guide + what NOT to touch is in `Docs/ShuffleSystem/ARCHITECTURE.md`
> ("Renaming the mode"). Planned Shuffle-specific *behavior* changes (randomized lineup, per-domain
> `{2,1,0}` scoring + crystal-wallet credit, race-to-6) are tracked in that same doc as future extensions
> of this meta — not a new mode.

---

## 1. What it is

One session plays a **randomized lineup** drawn from the competitive domain games — **sixteen** of
them, every arcade mode that satisfies the three admission criteria below. Each game the host draws a
random pool mode **from a bag** (no mode repeats until every drawable mode has been played — §1.2)
**and** a random intensity in `[1..X]` (X = the lobby-chosen
intensity ceiling), so a higher intensity widens the draw **twice over**: it raises each game's own
intensity and it unlocks more modes (§1.1). Drawable modes × intensities: L1 = 6×1 = 6 experiences,
L2 = 10×2 = 20, L3 = 13×3 = 39, L4 = 16×4 = **64**.

**The pool is authored, not coded** — it is `MaelstromData.asset`'s `GameQueue`, and every consumer
(`DrawNextRound`, `IndexOfSceneName`, the hub's pool string, `ConnectingPanelController`) is
length-agnostic, so adding a mode is one asset edit. Three things a candidate must satisfy:

1. **Domain-scored.** Standings fold through `ScoringRuleSO.ResolvePlacementOrder` (§3), so the mode
   must rank *domains* by team total. All seven are `MultiplayerDomainGamesController` subclasses.
2. **Scene in Build Settings.** `LaunchPendingRound` drives a `Single` load by scene name; a missing
   scene fails the round, not the draw.
3. **Player/domain range must contain the Maelstrom card's** (2–4 players, 2+ domains). The drawn
   mode's own card range is *not* re-checked at draw time — a mode capping at 3 players would break
   a 4-player lobby silently.
4. **Its scene must be able to hand back.** `Scoreboard.continueButton` is a per-scene
   `[SerializeField]`, and `if (continueButton)` is the only thing between the host and the
   Continue that calls `MaelstromController.AdvanceToNextGame()` — so a pool mode whose scene does
   not resolve that reference **stalls the tournament on that round**, with the scoreboard up and
   no way forward. It is satisfied structurally today rather than per scene: all 16 pool scenes
   instance `_Prefabs/CORE/GameCanvas.prefab`, which wires it, and none overrides it to null (the
   `GameCanvas-SkimRace` fork that used to break this class of inheritance is retired —
   `Docs/GAMECANVAS.md §9`). Verified for all 16 on 2026-09-10. **Check it for the next mode
   anyway**, because the thing that guarantees it is a prefab reference a scene is free to
   override, and an override to `{fileID: 0}` is exactly the silent-null shape
   `Docs/GAMECANVAS.md` warns about.

**Vessel-locked modes need no extra wiring.** Fifteen of the sixteen are single-hull (all but Scurry,
which offers Sparrow/Manta/Squirrel) (see the §1.1
table). `GameDataSO.SyncFromArcadeGame`
publishes the drawn card's `Vessels` list into `AllowedVesselClasses` and calls
`ClampSelectedVesselToGame`, so the round forces its own hull and the lobby's vessel pick applies
only to rounds that permit it. This is why the Maelstrom card's own `Vessels` list is a *lobby*
choice, not a session-wide lock.

**Known wrinkle — same-hull adjacency, and the sixteen-mode pool sharpened it.**
The draw excludes the previous *modes*, not the previous *vessel* or *arena* — and the bag (§1.2)
does **not** improve this: over a dealt bag the chance that two adjacent rounds share a hull is
`Σ n_g(n_g−1) / N(N−1)`, exactly what a uniform no-immediate-repeat roll gives, so the numbers below
stand unchanged.
It was already possible for Rampage and The Bends to come up back-to-back — they share both the
Dolphin and the cactus forest, so the pair reads as one mode played twice — and the wider pool makes
same-hull adjacency much likelier rather than rarer, because the added modes cluster on hulls:
**Sparrow ×5** (Wildlife Liberation, Salvo, Dog Fight, Breakwater, and Scurry by its first `Vessels`
entry), **Dolphin ×3** (Rampage, The Bends, Switchback), **Squirrel ×2**, **Rhino ×2**, **Scarab ×2**,
**Urchin ×2**. Measured chance that a draw repeats the previous round's hull, keying on each card's
first `Vessels` entry: **L1 26.7%** (Sparrow is 3 of 6), L2 20.0%, L3 16.7%, **L4 14.2%** — so it is
worst at the *most accessible* setting, which is also where a new player is least equipped to tell
two Sparrow modes apart. Two of those pairs also share an ARENA outright — Rampage/The
Bends (the cactus forest) and Dog Fight/Salvo (the Boneyard, reused verbatim, not forked) — so those
draws read as the same *place* as well as the same ship. The documented fix is unchanged and still
playtest-gated: widen the avoid-set to the previous mode's first `Vessels` entry. If it is taken,
it needs a guard for the case where every drawable mode shares one hull, or the draw starves.

### 1.2 The draw is a BAG, not a roll — no mode repeats inside a shuffle

`MaelstromDataSO.DrawnGames` is the bag: `DrawNextRound` draws uniformly from the drawable pool
**minus every mode already dealt this shuffle**, marks the winner, and only refills when the bag
empties. So a shuffle deals every drawable mode once before any mode comes round again — *no game
repeats itself in a shuffle*, as a property of the draw rather than of a lucky roll.

It matters most where the race is shortest against the widest pool: a race to 6 on `{2,1,0}` decides
in as few as three rounds, and the old immediate-repeat guard left a 16-mode pool free to deal the
same mode on rounds 1, 3 and 5 of a four-round match — the one thing a player reads as *the shuffle
is broken*.

Three details:

* **A shuffle CAN outlast its pool** (L1 draws from six modes; there is no cap on rounds), so the
  bag refills rather than starving. Across that seam the old rule still applies — the refilled bag
  avoids dealing the mode that just emptied it, so nothing is ever back-to-back.
* **The bag is host-only state**, because the draw is host-only: nothing replicates it and nothing
  reads it off a client. It is cleared by `ResetRuntime`, so Play Again starts a fresh bag.
* **Intensity still varies independently.** Two rounds of one mode at different intensities are
  still two different experiences, but they are not what the bag is preventing — a mode is dealt
  once per bag whatever intensity it draws.

### 1.3 The hub — the round is DRAWN early, PREVIEWED, and READIED into

The between-round screen used to be a standings board with one button on it. It is now the place a
round begins, and three things changed together to make that possible.

**The draw moved from launch to hub entry.** It used to happen at the moment of loading, on purpose:
the upcoming mode stayed hidden until its connecting panel, and a client never had to be told which
mode it was because the loaded scene told it. A hub that STANDS the upcoming arena and lets people
fly it needs the pick several seconds earlier, and needs it to be the same pick on every machine.
`MaelstromController.PrepareNextRound()` draws (idempotent — one draw per hub visit, so the arena a
player is looking at cannot change under them) and `BeginNextRound()` launches what is pending.

**The pick travels as replicated STATE, not an announcement.** `MaelstromRoundTicket`
(`GameIndex`, `Intensity`, `StartServerTime`) rides `Player.NetMaelstromRound` — server-write,
everyone-read, written identically onto every player. An RPC would reach exactly the peers that are
synchronized at the instant it is sent, and a peer still inside Netcode scene synchronization when
the host draws would sit in front of a hub with no arena in it (the same argument
`ArcadeConfigSyncManager.LobbySnapshot` already records for the arcade lobby). The index, not the
asset, is what travels: `SO_ArcadeGame` has no network identity, and `GameQueue` order is the one
ordering every peer shares.

**The countdown is ONE deadline with two values.** `MaelstromLobby` arms it at **30 s** on hub
entry; once every connected human has pressed READY it SNAPS to **3 s**. So "everybody readied" and
"nobody readied" end the same way — a 3-2-1 — and the view needs one rule to decide whether to show
it (`SecondsRemaining <= 3`). The snap is deliberately **one-way**: un-readying after the party has
been shown a 3-2-1 does not push the deadline back out, because that is a griefing lever on a screen
whose whole job is to get everyone into the next round.

**`MaelstromLobbyNetwork` is retired.** It was a `NetworkBehaviour` that had to be placed in the
Maelstrom scene to exist, and it never was — the scene shipped `lobbyNetwork: {fileID: 0}`, so the
ready-up had not run once and the hub fell through to a local fallback where the host's button
started the round immediately and nobody else's did anything. `MaelstromLobby` replaces it as a plain
MonoBehaviour the scene view ENSURES, with the state on `Player`. *A component that has to be placed
is a component that can be missing; the failure is silent, and it lasted as long as the feature had.*

### 1.4 The three screens

| Screen | Root | Button | Preview | When |
|---|---|---|---|---|
| **Hub** | `ArcadeGameConfigureModal` | READY (+ 3-2-1) | next round's arena, **flyable** | between every round |
| **Stats** | same root | NEXT | last round's arena, **look-only** | once the tournament is decided |
| **Summary** | `Summary Panel` | Play Again / Main Menu / **STATS** | — | after NEXT |

A decided tournament therefore lands on the **stats screen**, not on the trophy: NEXT is what asks
for the trophy, and the summary's STATS button goes back. The stats screen's preview has
`ModePreviewWindow.SetFocusEnabled(false)`, so "tap to play" is not offered for a round that will
never be played — an affordance the surface will not honour is worse than none.

### 1.5 The hub preview — the hub IS the arena

`MaelstromPreviewHost` fills `ConfigurationContent`. It does **not** stand a satellite cell the way
the arcade modal's preview does: that exists because Menu_Main has a live world to protect, and the
Maelstrom scene has no world at all. The hub swaps its own `Cell` onto the drawn mode's arena through
`Cell.RequestCellSwap` — the platform's one sanctioned runtime world-change — so the ecology, the
phase ladder and the spawners are the Cell's own rather than a parallel set this mode invented.

Two cameras share one surface and hand over **in order** (incoming takes the `RenderTexture` before
outgoing lets go, or the frame with nobody drawing into it is the white rectangle the window exists
to prevent): an orbit camera frames the whole arena while nobody is flying, and tapping in hands the
surface to the real gameplay camera behind the local pilot's vessel.

It degrades rather than failing. **Six of the sixteen pool modes author no `ModePreviewDefinitionSO`**
(Salvo, Switchback, Headlong, Breakwater, Hijack, Skein — their arenas are built by their own
controllers rather than by a cell config), and those rounds show the honest "preview not available"
with the mode's name and description still on screen. A scene with no vessel still gets the orbiting
look at the arena; only the tap-in is lost, and it says so once with the fix attached.

### 1.6 The AI roster is dealt ONCE — and the FIELD is fixed at four

**A Maelstrom seats `MaelstromDataSO.SeatCount` pilots every round — four by default — and the AI
that fill the empty seats are dealt at HUB ENTRY, before the first round.**

Two things follow from "a tournament is scored across sixteen matches, not one".

**The field size is not the launch modal's stepper.** That stepper is a preference for ONE match.
Here the placement table (`PointsByPlace`) is per DOMAIN, so the shape of the teams IS the shape of
the scoring, and a round played three-up is not comparable with a round played four-up. Four is the
Maelstrom card's own `MaxPlayersAllowed`, so **a full party of four brings no AI at all and a solo
player brings three**. `MaelstromController.ApplyRoster` writes it through
`GameDataSO.ConfigurePlayerCounts`, from the hub tick and again at launch — the second call is what
covers a degraded `BeginNextRound` that never went through a hub tick, rather than trusting that it
did. It runs AFTER `SyncFromArcadeGame`, which republishes the drawn card's own player range.

**The roster is dealt in the hub, not by the first round that happens to backfill.** It used to be
the latter, which made the intro hub honest about nothing: the party readied up against a field
that did not exist yet, and the bots they would race were decided by whichever mode loaded. A seat
(`MaelstromAISeat`) carries a NAME and a DOMAIN, and both are dealt once and replayed verbatim into
every later round — the domain because it used to be recomputed each round by the balanced
placement pick, so the moment a pilot changed domain the bots re-balanced around them and the
opponent you were racing became a team-mate, across a tournament scored per domain.

The deal uses the spawner's own algorithm — `ServerPlayerVesselInitializerWithAI.GetBalancedDomain`
against the same two count dictionaries — deliberately rather than a second copy, so **moving WHEN
the deal happens cannot change WHAT it deals**. `ServerPlayerVesselInitializerWithAI.SpawnAIs` keeps
its own fallback deal for the degraded case and now always finds the seats already dealt. Names come
from `MaelstromDataSO.AIProfileList`, held on the tournament asset rather than read off a game
scene's spawner, because the deal happens before any game scene exists and the summary resolves a
bot's face BY NAME.

**The roster has to TRAVEL, and it has to be drawn AFTER it lands.** Two separate misses, both of
which showed a solo player a field of one on the very screen where they decide whether to ready up
against three opponents:

* The hub spawns no AI (it is not a match — the bots get `Player` objects only when a round's scene
  loads), and `MaelstromAISeats` is per-peer runtime state the host alone deals, so a client had no
  way to know they existed. The seats now ride `Player.NetMaelstromRoster`
  (`MaelstromRosterTicket`) over the same channel and for the same reason as the round ticket —
  replicated STATE, not an announcement, because a peer still inside scene synchronization when the
  host deals would have an RPC deferred and dropped. It carries four fixed slots because a
  `NetworkVariable` takes an unmanaged struct and `SeatCount` is clamped to 4; a session always has
  the local human, so three is the true maximum. The roster deliberately survives
  `Player.PrepareForNewScene` (it belongs to the tournament, not the round) and is cleared with the
  tournament by `ResetRuntime`.
* `MaelstromSceneView.Start` builds the field list, and `MaelstromLobby` deals on its first
  `Update` — which is after every `Start` in the frame — so the list was built before the roster
  existed, **on the host as well**. `RefreshRoster` rebuilds it when the roster's rendered
  signature changes (a signature rather than a list reference, because both the deal and the
  client-side mirror mutate that list in place).

**The bots FLY the hub.** `MaelstromHubVesselInitializer.EnsureHubBots` gives every dealt seat a
body in the hub arena, in the drawn round's hull and its own team colour, on autopilot with
`shouldSeekPlayers: false` — so the field a player is about to race is on screen rather than only
in a list, and the bots do what a bot does in a cell: seek crystals and mass. It is deliberately
the SAME chain as the menu's AI companion and a game scene's backfill bot (spawn the Player
NetworkObject, claim it in the same frame, stamp its NetworkVariables, spawn its vessel, initialize
the pair, configure the pilot, `StartPlayer`), so a hub bot is an ordinary networked AI player and
not a third kind. Three details are load-bearing and each is a trap already recorded elsewhere:

* **`DespawnHubBots` runs immediately before the launch.** The round's own scene spawns these same
  seats from `RequestedAIBackfillCount`, so a hub body that survived the load would field every bot
  twice. It despawns and then prunes the roster BY NAME (`GameDataSO.RemovePlayerData`), because
  `Players` and `RoundStatsList` are name-keyed and a destroyed entry shadows the live one.
* **A bot is released UNDER WAY** (`botLaunchSpeed`, 60). The pair-init hands every vessel a dead
  stop, a vessel under `VesselPrismController`'s 3 u/s gate lays no trail, and the AI's own drift
  PINS cruise speed at whatever the vessel carried in — so a bot that drifts before it has
  accelerated stays pinned near zero and reads as broken rather than slow.
* **`StartPlayer`, never `StartPlayer` + `ActivateAutopilot`.** For a player whose `NetIsAI` is set,
  `StartPlayer` already takes the autopilot branch; doing both starts the AI pilot twice, which
  duplicates every ability coroutine and cannot be cleaned up.

The spawner is idempotent per seat NAME and is driven from the host tick, because the deal re-runs
whenever a player joins or leaves the hub and a re-deal must add a body without re-bodying the ones
already flying.

What deliberately does NOT persist is the bot's HULL: fifteen of the sixteen pool modes lock to one
vessel, so the ship has to change with the round. A bot is its name, its face and its colours,
exactly like a human pilot.

### 1.1 The intensity ladder — which modes a run can draw

`MaelstromDataSO.IntensityTiers` is **cumulative**: a run at intensity N draws from every rung up to
and including N, so a mode is authored once, at the rung it first appears on (a tier lists what it
ADDS). The rungs are ordered by **how quickly a new player can pick the mode up** — intensity is
therefore both "harder games" and "more games", and an intensity-1 Maelstrom is a legible party
lineup rather than a random sample of the whole roster.

| Rung | Adds | Hull | Why here |
|---|---|---|---|
| **1** | Skim Race | Squirrel | Fly through the crystals in order. |
| | Joust | Squirrel | Ram the other pilot. |
| | Scurry | Sparrow / Manta / Squirrel | Collect crystals. |
| | Wildlife Liberation | Sparrow | Shoot the animals. One verb, a target-rich arena, nothing to route. |
| | Salvo | Sparrow | Shoot the wreckage. The quarry is static and the guns are free; the reload economy is optional depth. |
| | Switchback | Dolphin | Follow the arrow through the rings — Skim Race's shape with rings for crystals. |
| **2** | Rampage | Dolphin | Skim → catch a crystal → fire the cone: a three-step chain. |
| | Peel the Cage | Rhino | Break inward through the shells. |
| | Dog Fight | Sparrow | Shoot the animals, except now they shoot back and evade. |
| | Headlong | Rhino | A lapped circuit — you must brake for corners *and* know you are running laps. |
| **3** | Scarab Scramble | Scarab | Forge a ball, then get it through a hoop. |
| | Breakwater | Sparrow | Each gate is a plugged door: fire, saw or thread it, then cross. Two verbs per station, ×2 laps. |
| | Hijack | Urchin | Grind the rails to STEAL mass — needs the ride mechanic and the domain-thirds speed cliff. |
| **4** | The Bends | Dolphin | Rampage's chain aimed at a *moving pilot*. The most indirect kill in the game. |
| | Skein | Urchin | Ride a knotted cable and change strands at aimed breaks — the hardest traversal on the platform. |
| | Tollway | Scarab | Place rings on living flora hearts and get paid when ANY ball threads one: indirect, economic, two-layer. |

**Not admitted — Astro League (37) and Brood Rush (38).** Both are domain-scored, both have their
scenes in Build Settings, and both would otherwise be strong pool modes — but both pin
`MaxDomainsAllowed = 2` because the mode has exactly two goals/claims (a *rule*, not the host's
preference — see `GameDataSO.MaxDomainsForGame`), and the Maelstrom card allows **3**. Criterion 3
above is that the candidate's range must *contain* the Maelstrom card's, and the drawn mode's range
is **not** re-checked at draw time, so a 3-domain Maelstrom that drew either one would hand the mode
a team shape it cannot express — and, worse, `NormalizeUnassignedHumans` would move the Gold pilot
off Gold for that round, so the standings would carry a domain that stopped being played for. To
admit them, the Maelstrom card would have to be capped at 2 domains (which narrows every other
round), or the draw would need a per-mode domain re-check — a real feature, not an asset edit. After each
game the active **domains** are ranked **by team total** (the mode rule's summed metric — see §3)
and earn **placement crystals** by domain place (1st = 2, 2nd = 1, 3rd = 0; `PointsByPlace`,
configurable — the **last**-placed domain always earns the table's last entry, 0, so a 2-domain
game pays `{2,0}`: losing never pays toward the race target). The cumulative **per-domain** total is
the leaderboard, and the session is a **race to `WinTarget` (6)** crystals — the first domain to
reach it wins, with a hard **`MaxGames` (7)** cap so a stalemate still ends. It appears as a normal
card in the Arcade panel (`GameModes.Maelstrom = 36`; the card's `DisplayName` is "Shuffle").
*(All Shuffle deltas shipped: per-domain `{2,1,0}` scoring, randomized lineup, race-to-6 / cap-7,
crystal-wallet credit, and the between-game loading-splash summary — see `Docs/ShuffleSystem/ARCHITECTURE.md`.)*

**Lobby minimum — 2 players, 2 domains.** Placement points are meaningless without opposing
teams (and the Joust leg is unplayable on a single domain — see JOUST.md Design Note 8). The Maelstrom
arcade card therefore sets `MinPlayersAllowed=2` and `MinDomainsAllowed=2` (`87658960`); the
configure modal floors both via `ArcadeGameConfigureModal.MinDomainsForGame`, so a solo host
always launches with at least one AI opponent on a second domain. No tournament-specific code is
needed — `MaelstromController` preserves the lobby's player/domain config across all three
`Single` loads (`SyncFromArcadeGame` only sets scene/mode/multiplayer, never the counts).

## 2. The load model — sequential `Single`, no additive

Every transition is a **host-driven `LoadSceneMode.Single` load** via the existing
`SceneLoader`/`NetworkManager.SceneManager`. The NetworkManager / UGS session / Relay and the
`Player` NetworkObjects already **persist across Single loads** (eager-Relay locked design), so
the tournament rides that proven path. **There is no additive scene loading** — it would collide
with per-scene systems (duplicate `ServerPlayerVesselInitializer`, ambiguous
`Scoreboard.gameController`, shared `gameData`/`CameraManager` singletons).

```
Menu_Main → [Arcade card → ArcadeGameConfigureModal ready-up]
  → Maelstrom (lobby) → ready-up → random game            (each transition a Single load)
       └─ game ends → Scoreboard.Continue (host) → Maelstrom (HUB: standings) → ready-up → random game → …
  → a domain hits 6 (or the 7-game cap) → Continue → Maelstrom (SUMMARY) → NEXT → results
       └─ Play Again (fresh shuffle) | Main Menu → Menu_Main (lava lamp)
```

The Maelstrom scene serves **three roles** — the intro lobby, the between-round **hub**, and the
end-of-tournament **summary** — and `MaelstromSceneView` picks the layout per load
(phase / `MaelstromController.IsShowingSummary`). Continue is shown on every game's scoreboard (host) and
always returns to the Maelstrom scene; once a domain reaches the target it loads in Summary phase, and
**Play Again / Main Menu live there** (behind the summary's NEXT step), not on the per-game scoreboard.

## 3. The brain — `MaelstromController` (persistent, network-free)

`MaelstromController` (`_Scripts/Controller/Arcade/Maelstrom/`) is a **pure-C# DI singleton**
created eagerly by `AppManager` (so it is alive from bootstrap and survives every Single load).
A static `Instance` lets scene MonoBehaviours reach it (mirrors `PartyInviteController.Instance`).

- **Standings are network-free.** On `gameData.OnMiniGameEnd`, **every peer** folds the
  already-synced `gameData.Results` (the ranked per-player `List<ScoreResult>`) into
  `MaelstromDataSO` via `RecordResults` — identical inputs → identical standings, no extra RPC.
  Domain placement is the mode rule's **team-total order**
  (`ScoringRuleSO.ResolvePlacementOrder` — domains by summed metric, ties → enum order
  Jade→Ruby→Gold; the same aggregation that ends the turn and picks `WinnerDomain`), computed from
  the still-synced `RoundStatsList` and passed in by the controller. The results-only reduction
  (each domain's place = its best player `Rank`) survives **only as a fallback** — it mis-placed
  teams whenever a losing team's player tied the top individual score (the 2v2 "Scurry" 17-vs-20
  regression). Places award `{2,1,0}` via `PointsForPlacement` (last place always earns the last
  entry, 0). Recording happens *before* the next load's `ResetRuntimeData` clears `Results`.
- **Only the host drives progression** (`BeginFirstGame` / `AdvanceToNextGame` /
  `RestartMaelstrom`): it draws a random `(mode, intensity ∈ [1..X])`, sets the per-game intensity
  on `gameData.SelectedIntensity`, then `SyncFromArcadeGame(mode) + InvokeGameLaunch()`. Clients
  follow the Single load (mode = loaded scene; intensity rides the existing config sync) — no shared
  RNG seed. Once `MaelstromDataSO.IsShuffleComplete`, `AdvanceToNextGame` loads the **summary** instead.
- **Phase is scene-load-driven** (deterministic on every peer): the **lobby scene** load runs
  `StartMaelstrom` (reset standings, capture the intensity ceiling, `IsActive=true`,
  `IsMaelstromMode=true`); each **pool game scene** load marks the loaded mode (`CurrentGameIndex`,
  used for repeat-avoidance) and goes `InGame`; **Menu_Main** load runs `EndMaelstrom` (clears the
  flags). `MaelstromStateMachine` tracks `Idle → Lobby → InGame → Complete → Summary` — `Complete`
  is the transient phase while the deciding game's scoreboard is up, `Summary` is the results scene
  itself (Play Again from `Summary` re-enters `InGame`; Main Menu returns to `Idle`).
- **The summary decision is authoritative, not phase-driven (race-to-6 fix).** At the Maelstrom scene
  load, `HandleSceneLoaded` shows the **Summary** when `IsActive && MaelstromDataSO.IsShuffleComplete`
  (deterministic on every peer) — **not** when the transient `Complete` phase happens to be set.
  `HandleMiniGameEnd` still sets `Complete` as a best-effort signal, but that transition only lands when
  the deciding game ends in the `InGame` phase; relying on it alone once let a domain hit `WinTarget` (6)
  yet route back to the hub for another game (the win silently swallowed). `EnterSummary` reaches
  `Summary` from `InGame`/`Lobby`/`Complete`, so the win always surfaces. Covered by
  `MaelstromStateMachineTests` + `MaelstromDataSOTests.IsShuffleComplete_*`.

Per-game stat reset is automatic (`SceneLoader` → `ResetRuntimeData` + each persistent
`Player.PrepareForNewScene` → `RoundStats.Cleanup`). Cumulative points live in `MaelstromDataSO`,
outside that reset, so they survive. AI backfill re-runs per scene; the **AI roster is dealt once**, in the HUB
before the first round, into `MaelstromDataSO.MaelstromAISeats` — a NAME and a DOMAIN per bot
(§1.6) — and replayed into every round, so bot identities stay stable across games (AI `Player`
objects are destroyed/recreated each scene). Standings are keyed by
**domain**, so per-game roster churn never affects the leaderboard.

## 4. End-of-game UI — the Scoreboard is the progression surface

The cinematic was removed (`Ys-bleeding-edge` `dbc7c703`); `EndGameSequencer` halts vessels,
plays the SFX, and raises `OnShowGameEndScreen` → `Scoreboard` (sole end-game UI). In tournament
mode `Scoreboard.ConfigureLobbyButtons` shows **only Continue, host-only**:

| Surface | Host sees | Hidden |
|---|---|---|
| Per-game scoreboard (after EVERY game) | **Continue** → `MaelstromController.AdvanceToNextGame()` | Play Again, Main Menu, Leave |
| Per-game scoreboard, on a client | — | all |
| **Maelstrom hub** (between rounds, shuffle not decided) | ready-up countdown → **START**, then auto-advances to the next random game | — |
| **Maelstrom summary** (shuffle decided) | **NEXT** → reveals results → host-only **Play Again** (→ `RestartMaelstrom()`) + **Main Menu** for everyone (host → `onClickToMainMenu` → Menu_Main for the whole party; client → `PartyInviteController.LeavePartyAndReturnToMenuAsync()` — leaves the party, returns solo) | — |
| Maelstrom hub / summary, on a client | — (follows the host's load; results + **Main Menu** on the summary) | Play Again |

`AdvanceToNextGame` **always** loads the Maelstrom scene (`LoadMaelstromScene`); the hub-vs-summary
choice is made on load from the authoritative, deterministic `MaelstromDataSO.IsShuffleComplete` (a
domain reached `WinTarget`, or `MaxGames` was hit) — **not** the transient `Complete` phase (see §3).
Mid-run it shows the standings **hub** (ready-up → next random game via `BeginNextRound`); once decided it
shows the results **summary**, whose active-panel button reads **NEXT** and reveals the end panel
(`MaelstromSceneView.OnPlayAgainPressed` / `OnMainMenuPressed`), not the Scoreboard. Play Again is
host-only; Main Menu shows on **every peer** — the host's press raises `onClickToMainMenu` (Netcode scene
load takes the whole party back over the live Relay), a client's press goes through
`PartyInviteController.LeavePartyAndReturnToMenuAsync()` (raising the SOAP event on a client would fade
and then defer to the server forever — `SceneLoader.ReturnToMainMenu` skips the load on connected clients).

**All Maelstrom-scene buttons are code-wired only** (`MaelstromSceneView.Awake` adds the listeners) — the
scene must NOT also add inspector `onClick` entries. Duplicate inspector wiring double-fired NEXT / Play
Again / Main Menu into `BeginNextRound`, launching a stray game off the summary (fixed; see
`MAELSTROM_REWORK_SPEC.md` v2.5). The summary cards and the round-card rows order by cumulative Total
Score (highest first), matching the leaderboard.

**Crystal reward (real wallet).** The placement crystals are also *real currency*: on each game's
Scoreboard, `AwardCrystalsToLocalPlayer` credits the **local** human's wallet
(`PlayerDataService.AddCrystals`, source `"shuffle_placement"`) with their domain's per-game `{2,1,0}`,
read from the injected `MaelstromDataSO.CrystalsForDomain(gameData.Results, localDomain,
placement)` — `placement` being the mode rule's team-total order (`ResolvePlacementOrder`) computed
once per show, so the badge/wallet match the standings fold exactly (a plain data-container read,
**not** a static `MaelstromController.Instance` reach-through). Each peer credits only its own local
player, once per game (AI have no wallet); the **last**-placed domain earns 0 (so the 2-domain loser
gets nothing, and 3rd of 3 gets nothing). The wallet write is wrapped in a try/catch: a
`PlayerDataService` hiccup degrades to a logged lost reward, never a missing end-game screen. The
per-player score cards show the same per-domain badge via `CardCrystalReward`. Gated on
`IsMaelstromMode` — outside a shuffle the Scoreboard keeps its original winner-only flat
`winnerCrystalReward`.

**Between-game summary overlay (SOAP — reuses the splash status surface).** `SceneTransitionManager`
owns **only** fades — it holds no UI text. The splash already has a SOLID/SOAP text view,
`BootStatusPanel`, fed by the `ScriptableEventBootStatusRequest` channel. On `OnLaunchGame` (fired on
host *and* clients, when the loading splash goes opaque), `BootStatusBroadcaster.HandleLaunchGame` — the
existing owner of "what the splash shows during a launch" — checks the shuffle state and, if mid-run
(`tournamentData.IsActive && !IsShuffleComplete && GamesPlayed > 0`), raises
`BootStatusRequest{Status, MaelstromStandingsFormatter.FormatRunning(tournamentData)}` instead of its
usual `Hide`. The standings (reduced from local standings on every peer — network-free) read on the
splash for the whole load, then the broadcaster's existing `HandleClientReady`→`Hide` clears them when
the new scene is ready. No new channel, view, or `TMP_Text` — and the controller no longer touches the
splash at all.

**Owner tag.** Scoring is per-DOMAIN (one row per team), so each peer passes its local player's domain
(`gameData.LocalPlayer.Domain`) into the formatter and the matching row is tagged ` <b>(You)</b>` — on
both the between-game splash (`FormatRunning`) and the final summary (`FormatFinal`) — so the owner can
read which team line is theirs. `Domains.Blue` (the no-team sentinel, never a standings row) tags nothing.

**Readable dwell.** A fast scene load would flash the standings by, so the load is held briefly behind
the opaque splash. `SceneLoader.LaunchGame` reads `MaelstromController.MinLoadSplashDwellSeconds`
(non-zero only under the *same* `IsActive && !IsShuffleComplete && GamesPlayed > 0` condition that shows
the standings — value `MaelstromDataSO.BetweenGameSummaryDwellSeconds`, default 2s) and `Max`es it with
the usual pre-load wait before `LoadScene`. Host-only: clients defer the load to the host at the
`LaunchGame` defer guard, so holding the host's `LoadScene` holds the whole party's splash. Zero outside
the window, so the first game, the load into the final summary, and Main-Menu returns are never delayed.

**Restart determinism:** Play Again calls `RestartMaelstrom()` → host loads the Maelstrom scene as a
fresh lobby; every peer resets its standings (keeping the intensity ceiling) when that scene loads while
still in phase `Summary` (`MaelstromController.HandleSceneLoaded` → `RestartFromSummary`), so the wipe is
consistent across the party without extra networking.

**Scoreboard position stability (`660e4d91`):** a tournament shows the Scoreboard once per leg, so
it re-shows the board up to three times in one session — exactly the case that exposed a drift
bug. `Scoreboard.PlayEntranceAnimation` slid the panel in by mutating its own `anchoredPosition`
and never restored it on hide; on a stretch-anchored panel each re-show captured the displaced
position as the new rest target, so the board crept off-base across the Joust / Crystal Capture
legs (SkimRace was immune — it shows the board once then full-scene-reloads). The entrance slide is
now disabled in favour of `Scoreboard.ShowScoreboardImmediate` (authored position, full alpha,
unit banner scale). See JOUST.md / SCURRY.md for the per-mode notes.

**Joust-leg AI hardening (`975271aa`):** because every tournament includes the Joust leg, the
player-seek AI was fixed so bots keep jousting instead of flying off when they lose their target
(empty opponent set / opponent mid-respawn): `AIPilot` now tracks the chosen opponent's live
transform every frame, falls back to the cell centre when no opponent qualifies, and re-acquires
on a faster cadence while unlocked. Full detail in JOUST.md Design Note 12.

## 5. Data — `MaelstromDataSO`

`_Scripts/Utility/DataContainers/Maelstrom/MaelstromDataSO.cs` (asset:
`_SO_Assets/Maelstrom/MaelstromData.asset`). Authored: `GameQueue` (the draw **pool** — the 3
`SO_ArcadeGame`s), `ModeCard` (the mode's own card — player-facing name), `PointsByPlace` (`{2,1,0}`),
`WinTarget` (6), `MaxGames` (7), `LobbySceneName`, `seatCount` (the FIELD — 4; §1.6),
`aiProfileList` (where AI seats get their names and faces, held here because the deal precedes
every game scene), four `ScriptableEventNoParam`s. Runtime
(non-serialized): `IsActive`, `CurrentGameIndex` (last loaded pool mode — repeat-avoidance),
`DrawnGames` (the draw bag, §1.2),
`PendingGameIndex`/`PendingIntensity` (the round the hub is previewing — §1.3; cleared at launch),
`GamesPlayed`, `IntensityCeiling` (X, captured at start; **survives `ResetRuntime`** so Play Again
keeps it), `MaelstromAISeats` (name + domain, dealt once — §1.6), `Standings` (a `List<MaelstromDomainStanding>` — **keyed by
`Domains`**, not player). Key methods: `RecordResults(results)` (per-domain fold + `GamesPlayed++`,
see §3), `IsShuffleComplete` (race target reached or game cap hit — drives summary vs next game),
`BuildSortedStandings()` (points desc, tiebreak best placement, then domain enum order Jade→Ruby→Gold),
`PointsForPlacement(place, count)` (last place always earns the table's last entry — 0),
`ResetRuntime()`. `RecordResults` takes an optional `domainPlacementOrder` — the mode rule's
team-total order from `ScoringRuleSO.ResolvePlacementOrder`, passed by the controller (rank-derived
fallback otherwise). Edit-mode coverage: `Assets/_Scripts/Tests/Editor/MaelstromDataSOTests.cs`.

## 6. File index

| Role | File |
|---|---|
| Mode enum | `_Scripts/Data/Enums/GameModes.cs` (`Maelstrom = 36`) |
| Config flag | `_Scripts/Utility/DataContainers/GameDataSO.cs` (`IsMaelstromMode`) |
| Data container (+ `ModeName`, `CrystalsForDomain`) | `_Scripts/Utility/DataContainers/Maelstrom/MaelstromDataSO.cs` |
| Standings text formatting (shared, DRY) | `_Scripts/Utility/DataContainers/Maelstrom/MaelstromStandingsFormatter.cs` |
| State machine | `_Scripts/Controller/Arcade/Maelstrom/MaelstromStateMachine.cs` |
| Controller (brain) | `_Scripts/Controller/Arcade/Maelstrom/MaelstromController.cs` |
| Hub / stats / summary scene view | `_Scripts/Controller/Arcade/Maelstrom/MaelstromSceneView.cs` |
| Ready-up + countdown (plain MonoBehaviour, ensured in code) | `_Scripts/Controller/Arcade/Maelstrom/MaelstromLobby.cs` |
| Replicated round pick | `_Scripts/Controller/Arcade/Maelstrom/MaelstromRoundTicket.cs` + `Player.NetMaelstromRound` / `NetMaelstromReady` |
| Hub preview (ConfigurationContent) | `_Scripts/Controller/Arcade/Maelstrom/MaelstromPreviewHost.cs` |
| Replicated AI roster | `_Scripts/Controller/Arcade/Maelstrom/MaelstromRosterTicket.cs` + `Player.NetMaelstromRoster` |
| Hub vessel spawner (autopilot, next round's hull, no domain reset) + the hub's BOTS (`EnsureHubBots` / `DespawnHubBots`) | `_Scripts/Controller/Arcade/Maelstrom/MaelstromHubVesselInitializer.cs` |
| Hub motion vocabulary | `_Scripts/Controller/Arcade/Maelstrom/MaelstromTransitions.cs` |
| End-game buttons + entrance + placement wallet credit (via injected `MaelstromDataSO`) | `_Scripts/UI/Scoreboard.cs` |
| Between-game summary text (SOAP, reuses the splash status surface) | `_Scripts/UI/Screens/BootStatusBroadcaster.cs` (shuffle branch) → `BootStatusPanel` via `Event_BootStatusRequest` |
| Lobby player/domain floor | `_Scripts/UI/Modals/ArcadeGameConfigureModal.cs` (`MinDomainsForGame`) |
| Per-game min domains field | `_Scripts/ScriptableObjects/SO_ArcadeGame.cs` (`MinDomainsAllowed`) |
| Joust-leg opponent-seek AI | `_Scripts/Controller/AI/AIPilot.cs` |
| Client flag sync | `_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` |
| Stable AI roster (seats replayed every round — §1.6) | `_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs` |
| DI registration | `_Scripts/System/AppManager.cs` |
| Card unlock | `_Scripts/System/Progression/GameModeProgressionService.cs` |
| Data asset | `_SO_Assets/Maelstrom/MaelstromData.asset` (+ 4 `Event_Maelstrom*.asset`) |
| Arcade card | `_SO_Assets/Games/ArcadeGameMaelstrom.asset` (in `GameLists/ArcadeGames.asset`) |
| Lobby / hub / summary scene | `_Scenes/Multiplayer Scenes/Maelstrom.unity` |
| Pool list row (the launch panel's ladder readout) | `_Scripts/UI/View/ArcadeLaunch/MaelstromPoolEntry.cs` + `_Prefabs/UI Elements/ArcadeLaunch/MaelstromPoolRow.prefab` |
| Pool row + grid generator (`--check`) | `Tools/Build/author_maelstrom_pool_cards.py` |

## 7. Editor wiring

The mode runs end-to-end; one **optional** wire remains for the §4 between-game summary overlay
(last bullet). Everything else below is in the scene as committed.

- **AppManager** — `tournamentData` assigned; `MaelstromController` registered and constructed with
  `gameData` + `tournamentData` + `sceneNames` + `sceneTransitionManager`, created eagerly at bootstrap
  so it survives every Single load.
- **`Maelstrom.unity`** — `MaelstromSceneView` drives all three screens of §1.4.
  - **It binds itself.** Every serialized field is looked up BY NAME under its own root when the
    inspector leaves it empty (`ArcadeGameConfigureModal` / `Summary Panel` → `Title Text (TMP)`,
    `PoolText`, `RoundStatusText`, `InfoText`, `LeadingDomainText`, `GameStartText`, `ReadyButton`,
    `NextButton`, `ConfigurationContent`, `MaelstormSummaryScrollView` — the scene's own
    misspelling is accepted alongside the correct one — and `RankText`, `StatsScreenButton`,
    `PlayAgain Button`, `Main Menu Button`, `Content`). An explicit reference always wins. This is a
    direct response to how the old ready-up died: a missing reference on this screen is silent.
  - **`MaelstromLobby` and `MaelstromPreviewHost` are ENSURED in code** — nothing to place.
  - **What the scene ALREADY carries, and is driven rather than duplicated.** `ConfigurationContent`
    holds a real **`MinigameLaunchPanel`** (the main menu's own launch panel) with its
    `ModePreviewWindow` on `Preview` and its `GameBriefingView` on `GameView` wired, so
    `MaelstromPreviewHost` adopts that panel and `Bind`s it — which is literally what makes "the name
    and description read like the main menu" true rather than re-implemented. **`Bind` is the only
    call it makes**: `ArcadeLaunchPanel.Show()`/`Hide()` toggle the panel's OWN GameObject, which is
    the object the host component lives on, so hiding it would stop the host that is meant to be
    driving the preview. The host takes the window down directly instead.
  - **`ArcadeGameConfigureModal` was REMOVED from `MaelstromConfigureModal`** (it had come with the
    copied layout). Only the layout was wanted: that component is the arcade's whole launch flow —
    a static `Instance`, an `ArcadeGameConfigSO` reset in `Start`, its own ready-up and its own
    launch — so leaving it live put a second authority in a scene where `MaelstromLobby` decides when
    a round starts. `MaelstromSceneView` still warns once if it comes back.
    **Its Animator went with it, and that is not cosmetic**: `Standard.controller`'s default state is
    `Standard Start.anim`, which keys the root's `CanvasGroup.m_Alpha` to **0**. `ModalWindowManager`
    is what normally drives that Animator out of Start; with the component gone and the Animator left
    enabled, the modal would have been held permanently invisible and would have overwritten
    `MaelstromTransitions.PanelIn`'s tween every LateUpdate. The Animator is disabled, not deleted.
    *General rule: removing the DRIVER of an Animator leaves the Animator playing its default state,
    and a modal's default state is usually "closed".*
  - **The scene carries a `Cell` and the standard spawn pair** (added 2026-09):
    1. **A `Cell`** (`_Prefabs/Environment/Cell.prefab`) at the scene root, `runtime` =
       `Runtime Cell Data.asset`, `CellConfigs[0]` = **`Barren Cell Config`** — the hub opens on an
       empty, instant world and `MaelstromPreviewHost.RequestCellSwap`s onto each drawn round's arena
       from there. Without it the preview reports the miss and shows "not available".
    2. **The standard spawn pair** on one root `Game` object — `NetworkObject` + `NetcodeHooks` +
       `ClientPlayerVesselInitializer` + `MaelstromHubVesselInitializer`, exactly as every other
       multiplayer scene carries one. It spawns no AI (the hub is not a match; the roster is dealt at
       the round's own scene) and it arranges its ring around the cell
       (`arrangeSpawnPointsAroundCell`, `spawnRingRadiusFloor 600`) rather than off authored points,
       because the hub's world CHANGES under it every round and a fixed point set would be authored
       against whichever arena happened to be standing.
  - **The hub raises `OnInitializeGame` itself** (`MaelstromSceneView.Start`). It has no
    `MiniGameController`, and that event is the only thing `Cell` subscribes `Initialize` to — so
    without the raise the scene's Cell never binds `runtime.Cell`, never assigns a config and never
    spawns its membrane, and `RequestCellSwap` would be building a world onto a cell that had not
    started. `MainMenuController` does exactly this in Menu_Main; the hub is the same shape — a scene
    with a live cell and no match running in it.
  - **The field renames carry their old wiring** via `[FormerlySerializedAs]`
    (`gameModesText`→`poolText`, `roundCounterText`→`roundStatusText`, `raceRuleText`→`infoText`,
    `countdownText`→`gameStartText`, `activeRoot`→`configureRoot`) — verified against the scene, where
    each old field already points at the renamed object. `summaryRoot`'s reference had gone dangling
    and is re-found by name.
  - **Buttons are code-wired only** — `MaelstromSceneView.Awake` adds `OnReadyPressed`,
    `OnNextPressed`, `OnStatsScreenPressed`, `OnPlayAgainPressed` and `OnMainMenuPressed`. Do **NOT**
    add inspector `onClick` entries: duplicate wiring double-fires the press. `onClickToMainMenu` is
    wired to `EventOnClickToMainMenuButton.asset` — the **same** main-menu `ScriptableEventNoParam`
    the Scoreboard's Main Menu raises and `SceneLoader` listens to.
  - **Main Menu must be reachable from the HUB**, not only the summary: if the scene parents it only
    under `Summary Panel`, the view says so once (a tournament you cannot leave until it finishes is
    not one anybody should have to finish).
- **Scoreboard Continue button** — present on the shared end-game canvas
  (`GameCanvas-SkimRace.prefab`, used by all three domain-game scenes — SkimRace, Joust, Crystal
  Capture — plus `EndGameStatsPanel.prefab`) and wired to `OnContinueButtonPressed()`. Host-only,
  shown on every game (see §4).
- **Arcade card + grid cell** — `ArcadeGameMaelstrom.asset` present in `GameLists/ArcadeGames.asset`,
  with `MinPlayersAllowed=2`, `MaxPlayersAllowed=4`, `MinDomainsAllowed=2`, `MinIntensity=1`,
  `MaxIntensity=4` (`87658960`) — so the configure modal floors both player and domain count to 2
  (see §1, *Lobby minimum*).
- **Between-game summary (SOAP — outstanding wires).** Wire `MaelstromData.asset` into
  `BootStatusBroadcaster.tournamentData` (on the splash canvas). The §4 running standings then show on
  the existing `BootStatusPanel.statusText` during shuffle inter-game loads — **no new object, and no
  `TMP_Text`/field on `SceneTransitionManager`** (it owns only fades now). Also wire `MaelstromData.asset`
  into each domain-game `Scoreboard.tournamentData` (`GameCanvas-SkimRace.prefab` + the scene-added
  Scoreboards in Joust / Crystal Capture) for the placement wallet credit + card badge. Unwired, both
  degrade gracefully (clean splash / flat winner reward).

No per-button visibility code lives in the scene — `MaelstromSceneView` and `Scoreboard`
drive it (phase-selected; host-only except the summary's Main Menu, which every peer gets).

## 8. Verification

See the plan's verification section: solo + bot-fill (Continue advances; final game shows Play
Again + Main Menu; bots stable across games), 2-4 players in MPPM (clients show no per-game buttons
and no Play Again, but DO get Main Menu on the final summary — pressing it leaves the party and lands
that client alone in Menu_Main while the rest stay on the summary; standings identical on every peer),
flag hygiene (a normal game after a tournament shows the standard buttons), and an edit-mode unit
test for `MaelstromDataSO.RecordResults`.

## 9. Deferred (later P3 plans)

Rewards (blocked on the P8 economy spec), post-tournament share screen, funnel instrumentation,
host-selected/randomized lineups, host-migration beyond existing disconnect handling, full QA
matrix.

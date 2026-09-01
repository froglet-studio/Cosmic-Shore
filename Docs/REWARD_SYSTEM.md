# The Reward System

Everything the game hands the player, the one door it comes through, and the two places it is
shown.

Owner: Shombith · Related: `Docs/ECONOMY_TABLES.md` (the numbers), `Docs/ECONOMY_AND_PRICING.md`
(why they are those numbers), `Docs/DAILY_CHALLENGE.md`, `Docs/MENU_PROGRESSION_AND_IAP.md`

---

## 0. The rule

> **Producers describe WHAT was earned. `RewardService` decides everything else.**

A producer builds a `RewardGrant` and calls `RewardService.Grant`. It does not touch the wallet,
does not know about cloud saves, does not implement its own once-only latch, and does not tell
the UI. That split is the whole system, and it exists because the alternative already shipped:
the only live earn path in the game wrote the wallet directly from a UI component, with the
payout table serialized on that component and duplicated across nine scenes — while nine other
designed reward paths were built and left unwired, three of which *consume a claim and grant
nothing*.

`PlayerDataService` remains the **sole writer** of `ProfileEconomy`. `RewardService` routes to
it; it is not a second wallet.

---

## 1. The pieces

| Piece | Location | Job |
|---|---|---|
| `RewardGrant` | `_Scripts/Data/Structs/` | What is being granted. Built through factories (`Crystals`, `CrystalsOnce`, `Entitlement`) — never by hand. |
| `RewardGranted` | `_Scripts/Data/Structs/` | The announcement. Carries the grant plus the balance **before and after**, so no display has to re-read the wallet. |
| `RewardKind` / `RewardDedupe` | `_Scripts/Data/Enums/` | What kind of thing, and how hard it refuses to happen twice. |
| `RewardTableSO` | `_Scripts/ScriptableObjects/`, asset at `Resources/RewardTable` | The payout numbers and the payout policy. |
| `RewardService` | `_Scripts/System/Rewards/` | The single grant door. Static, like `GameToastAPI`. |
| `ScriptableEventRewardGranted` | `_Scripts/ScriptableObjects/SOAP/ScriptableRewardGrant/`, asset at `Resources/Channels/RewardGrantedChannel` | The one channel every reward display listens on. |
| `RewardPayoutPanel` | `_Scripts/UI/Elements/` | The end-game payout moment. |
| `RewardToastDriver` | `_Scripts/UI/Elements/` | The menu toast. |
| `RewardDisplayWirer` | `_Scripts/Editor/Rewards/` | **FrogletTools > Interface > Wire Reward Displays** — binds the two displays into scenes. |
| `RewardTableTests` | `_Scripts/Tests/Editor/` | The payout policy, and that the shipped asset matches `ECONOMY_TABLES.md`. |

Assets are authored by `Tools/Build/author_reward_assets.py` (`--check`), never by hand.

---

## 2. Granting

```csharp
// A repeatable payout.
RewardService.GrantCrystals(200, "game_placement");

// A payout that may only ever land once for this account.
RewardService.GrantCrystalsOnce(500, "first_win", "first_win");

// A permanent unlock - a skin, a toy, a vessel.
RewardService.Grant(RewardGrant.Entitlement("skin.squirrel.aurora", "quest_reward"));
```

`Grant` returns **true only when the account actually changed**. It returns false without
logging an error for the three ordinary non-events: a payout of nothing (last place), a
once-ever reward already earned, and no profile service to write to.

### Dedupe, and why it needs no new cloud schema

`RewardDedupe.Account` deduplicates against `ProfileEconomy.UnlockedRewardIds` — the persisted,
cloud-synced list that already existed for entitlements and was wired to nothing. Once-ever
crystal grants namespace their key under `RewardService.OnceKeyPrefix` (`once:`) so they can
never collide with an entitlement id. An entitlement **is** its own dedupe key, so there is no
way to author one that grants twice.

The key is marked only *after* the payout succeeds, so a throw mid-write leaves the reward still
owed rather than silently consumed.

### Adding a new kind of reward

Skins and toys are `RewardKind.Entitlement` — they need no new plumbing, only a display branch
in the two UI components and an id convention. Add a `RewardKind` member only when the payout
target is genuinely not a currency and not an entitlement; that enum is the axis the whole
system dispatches on, and a parallel grant path beside `RewardService` is the mistake this
replaced.

---

## 3. The payout table

`Resources/RewardTable.asset`. One asset, every mode, tournament included.

| Place | Crystals |
|---|---|
| 1st | 200 |
| 2nd | 50 |
| Last | 0 (always, regardless of what the table would pay) |

`lastPlaceAlwaysEarnsNothing` is why a two-domain match has no silver medal: index 1 *is* last
place, so the runner-up is a loser rather than a medallist. That policy lives on the SO and not
at the call site, so a second producer cannot implement it differently.

**This table used to live in nine scenes.** Five further surfaces — Salvo, Brood Rush, Cellular
Duel, 2v2 Co-op and `GameCanvas-HexRace.prefab` — still carried the retired
`winnerCrystalReward` key and were therefore paying out of the C# field initializer while the
other nine paid out of their serialized copy. They agreed only by coincidence. All 14 now read
the asset.

---

## 4. The displays

Both listen on `RewardGrantedChannel` and read `RewardGranted` only, so what the player SEES and
what the wallet DID cannot drift apart.

### `RewardPayoutPanel` — the end-game moment

Replaces the bare `+N` badge on the winning score card, which stated an amount and nothing else.
Shows the payout, then counts the balance up from `PreviousCrystalBalance` to
`NewCrystalBalance`.

Two details are load-bearing:

- **It hides on a `CanvasGroup`, never `SetActive`.** It has to stay active to stay subscribed,
  and a reward popping into existence breaks the same continuity law a prism would.
- **It catches up on a grant it missed.** `Scoreboard` awards the payout while building its
  cards and activates its panel *afterwards*, so a listener parented under that panel has not
  had `OnEnable` when the raise happens. `RewardService.LatestGrant` / `GrantSequence` let a
  display compare against the last sequence it showed and replay. A *sequence* rather than a
  consumed flag, because two displays must be able to catch up independently — "consuming" it
  would let whichever woke first hide the reward from the other.

### `RewardToastDriver` — the menu

Posts through the menu's existing `ToastChannel`. It deliberately does **not** catch up: the
payout for a match the player just finished has already been shown on the end-game screen.

> **Status: correct and currently silent.** The only producer in the game today raises in a
> gameplay scene. This is the surface a daily-challenge, quest or milestone payout posts through
> the day one is wired (§6).

---

## 5. Wiring the displays

**FrogletTools > Interface > Wire Reward Displays.** Open the scenes you want and press *Wire
Open Scenes*, or *Wire All Build Scenes*. Idempotent; safe to re-run.

It is a tool rather than hand-authored prefab YAML for a specific reason: **the end-game
scoreboard's wiring is per-scene and the shared prefab is stale against it.**
`GameCanvas.prefab`'s `Scoreboard` leaves `playerCardContainer` unset and still serializes ten
keys the script no longer declares (`SingleplayerView`, `MultiplayerView`, the four `rematch*`
fields, …). Reading the prefab tells you almost nothing about what a given scene shows; only the
loaded, merged hierarchy does.

**It adopts before it creates.** The scoreboard already carries an authored `Goodies` cluster
with a `CrystalIcon` and a `CrystalsEarned` label — written since 2021 by the deprecated
`MiniGame` path with a hardcoded `0`. Where that art exists, the panel binds to it rather than
building a parallel display, so the payout lands in a slot a designer already placed.

The tool writes into the human's working tree, so it draws `FrogletToolShipPanel` — use
**Validate & Push**, per `Docs/TOOLING.md` § "Tool output is a deliverable".

---

## 6. Designed and NOT granting

These were built and left unwired. Each needs a payout number (`ECONOMY_TABLES.md` defers quest
payouts explicitly) and one `RewardService.Grant` call. **Three of them consume a claim while
granting nothing, which is worse than an honest gap** — a player spends the claim and receives
nothing, permanently.

| Path | State | What it needs |
|---|---|---|
| Training intensity tiers | `TrainingGameProgressSystem.ClaimIntensityTierReward` sets `Claimed = true` and grants nothing. The UI shows `IntensityNReward.Value`. | A `GrantCrystalsOnce` keyed on `(mode, tier)`. **Burns the claim today.** |
| Daily challenge tiers | `DailyChallengeSystem.ClaimReward` routes to the disabled PlayFab handler; claim state persists in `PlayerPrefs`. | Same. **Burns the claim today.** |
| Daily free reward | `DailyRewardCard` → `DailyRewardHandler.Claim()`; the CloudScript is disabled and the callback never fires, but the `PlayerPrefs` date is written first. | Same. **Burns the day today.** |
| Daily challenge completion (UGS-era) | `DailyChallengeService` has no reward code at all. | A payout on completion. |
| Quests | `Quest.CrystalRewards` is declared and read by nothing; `QuestSystem.CompleteQuest`'s grant is commented out but still sets `RewardGranted = true`. | A payout on `GameModeProgressionService.OnQuestCompleted`. |
| Tournament overall victory | `TournamentController` never grants; only per-game placement pays. | A decision on whether winning the meta pays extra. |
| Mission mode | `MiniGame.cs` (DEPRECATED) hard-codes `crystalsEarned = 0`, and reports a hard-coded `0` to `DailyChallengeSystem.ReportScore`, so the daily tiers can never be *satisfied* either. | Retirement, most likely. |
| New-player starting balance | `CrystalBalance` defaults to `0`; nothing seeds it. | A decision (`ECONOMY_AND_PRICING.md` §3 flags it). |

---

## 7. Reading a balance

**One source: `PlayerDataService`.** Subscribe to the static `OnCrystalBalanceChanged` and read
`GetCrystalBalance()`.

`CatalogManager.GetCrystalBalance()` reads the PlayFab inventory shelf, which has been disabled
for as long as PlayFab has — it returns `0` forever and its change events cannot fire. The Store
read it until this pass, so the Store and the Hangar showed different numbers for one wallet.
`PurchaseItemCard` still reads it and is referenced by no prefab or scene; repointing its balance
would imply a purchase flow that does not work.

`CrystalCurrencyDisplay` is a ready-made shared wallet display that **no prefab or scene
references.** It is correct (it routes through `VesselUnlockSystem`, which delegates to
`PlayerDataService`); it just needs wiring if a persistent wallet chip is wanted.

---

## 8. Verification status

Shipped without a Unity editor available, so:

- **Type-checked**, not merely inspected: every new and changed file compiled against a stub
  harness with signatures transcribed from the repo. The gate was proven to fire on an
  undeclared body identifier, a wrong member name, a wrong arity, a wrong palette arity and a
  wrong `SerializedObject` member before being trusted. It caught two real API mismatches in the
  wirer that a no-stub pass swallows entirely.
- **Tested**: `RewardTableTests`' eight assertions were executed headlessly against the shipped
  code and the shipped asset, and the runner was proven to fail when the asset drifts.
- **Scene edits validated**: the payout-override strip produced 14 deletions and 0 insertions,
  with YAML document count and trailing newline unchanged in every file.
- **NOT verified in the editor**: nothing here has been opened in Unity. In particular the two
  displays are **not yet placed in any scene** — that is what §5's wirer is for, and its output
  needs to land on the branch before the feature is visible to anyone.

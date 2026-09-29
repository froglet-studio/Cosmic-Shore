# PlayFab retirement — the phase-1 call-site table

The measured answer to `Docs/LAUNCH_BLOCKER_INDEX.md` §B2, and the record of what phase 2 did with
it. §B2 points here rather than carrying the table, because the table is about **code** and the
index is about **assets**.

> **Verdict: sever, do not amputate.** PlayFab goes — the SDK, the editor extensions, and every
> PlayFab-era backend class. The live screens that happened to compile against it stay, with their
> PlayFab data path removed. This follows the retirement prompt's own constraint: *"Do not delete a
> file because it compiles against PlayFab. The SDK is the dependency; the feature may still be
> wanted with a different backend."*

## 0 · What the scoping prompt got wrong, measured

Three corrections, each of which would have caused a defect if taken at face value.

1. **"Seventeen are inside `System/Playfab/` and go with the SDK" — no.** The folder holds **26**
   `.cs` files and only **15** use PlayFab. The other **11 are ordinary first-party code that
   happens to live there**, and one of them — `CaptainManager` — is **DI-registered in
   `AppManager` (`RegisterManagerSingleton<CaptainManager>`), instanced in `Bootstrap.unity`, and
   named by 18 call sites** across the Hangar, the Store and the purchase modals. Deleting the
   folder wholesale takes out a live manager. *A folder name is not a dependency.*

2. **"Three outliers are the whole job" — no.** Deleting the PlayFab-era classes cascades into
   **17 first-party files outside the folder**, four of them live Menu_Main screens. The three
   named outliers are the smallest part of it; `StoreScreen` alone has 15 call sites and
   `LeaderboardsMenu` 8.

3. **`Docs/WEEKLY_CHALLENGE.md` is wrong about the daily-challenge cluster.** It states
   *"`DailyChallengeSystem` is in no scene either"*. It is a real `PrefabInstance` in
   **`Bootstrap.unity`** (prefab guid `987fb44715d58f443bb86d97e9ed91a7`), so it is a
   `SingletonPersistent` that runs `Start()` — issuing daily tickets and selecting a daily game
   into `PlayerPrefs` — on every session, and `GameplayRewardButton` in Menu_Main can still reach
   `ClaimReward`. The cluster's **PlayFab coupling** is inert; the **cluster** is not.

## 1 · Two bugs the removal fixes — one reachable today, one latent

Both are UI waiting on a PlayFab backend that can never answer, and both are invisible to code
review because the code is correct for a PlayFab that is running. They differ in severity and the
difference is stated rather than flattened: only the second is reachable by a player on this
branch.

- **`ProfileModal` — the randomize-name button hangs forever** (real in the code; **not reachable on
  this branch**, see the caveat below).
  `GenerateRandomNameButton_OnClicked` → `AssignRandomNameCoroutine` calls
  `AuthenticationManager.Instance.LoadRandomNameList()` (a `PlayFabClientAPI.GetTitleData` call)
  and then `yield return new WaitUntil(() => AuthenticationManager.Adjectives != null)`.
  `AuthenticationManager.Awake()` early-returns — PlayFab never logs in, the callback never fires,
  `Adjectives` stays null. The coroutine never resumes, so the busy indicator never clears and the
  name field is never filled. Fixed here by generating the name from a local word list.

  **Caveat, measured:** an earlier commit on this same branch retired `ProfileModal` — it is
  unregistered from `ScreenSwitcher.Modals`, `ModalWindows.PROFILE` now opens
  `PlayerDataSelectModal`, and nothing else opens it — so **on this branch the player cannot reach
  the button**. The class is still instanced in `Menu_Main.unity` and two prefabs, still compiles
  into the build, and the hang is still live on `bleeding-edge`, which does not carry that
  retirement. So the fix is worth having and the severity claim is not: this is a latent hang in
  shipped code, not a bug a player hits today.

- **`LeaderboardsMenu` — reachable, and it threw on every open.** This one *is* live: the Records
  screen sits on `PortScreen`, is wired into `ScreenSwitcher`, and `OfflineMenuWirer` explicitly
  keeps it navigable. `FetchLeaderboard` called `LeaderboardManager.Instance`, whose prefab is in
  no scene, so `Instance` was null and `SelectShipType` threw a `NullReferenceException` every time
  the screen opened; `LeaderboardEntriesV2` was never initialised either. On top of that,
  `PopulateGameHighScores` compared `score.PlayerId == AuthenticationManager.PlayFabAccount.ID`.
  `PlayFabAccount` is initialised to `new()` so the `WaitUntil(... != null)` above it passed
  instantly, and `ID` is always empty, so the comparison was always false. All three are fixed
  here; the screen now renders its empty state instead of throwing.

## 2 · The decision table

`reach` = is it reachable from an enabled build scene (guid walk, transitively through prefabs)?
`UGS` = the live equivalent, or none.

### 2a · The live consumers — severed, kept

| Call site | reach | UGS equivalent | Decision |
|---|---|---|---|
| `UI/Modals/ProfileModal.cs` | **yes** — Menu_Main + 2 prefabs | `PlayerDataService` (profile), `AuthenticationData.PlayerId` | **Sever.** Both `using PlayFab` lines were already vestigial — no PlayFab type is used in the body. Random-name moved to a local list (fixes the hang); "stay logged in" keeps its `PlayerPrefs` behaviour via the rescued `PlayerSession`. |
| `UI/Screens/LeaderboardsMenu.cs` | **yes** — Menu_Main + Records Screen prefab | UGS Leaderboards exists (`WeeklyChallengeLeaderboardService`) but this screen is not on it | **Sever.** `LeaderboardEntry` becomes a nested struct of the menu (it holds no PlayFab type). Fetch path removed; the screen renders its empty state until someone ports it to UGS. Own-entry highlight moved to the UGS player id. |
| `System/DailyChallengeSystem.cs` | **yes** — `Bootstrap.unity` | none (weekly challenge is the successor, deliberately separate) | **Sever.** Drops `using PlayFab.ClientModels`, the `SaveToPref(GetUserDataResult)` method and its `PlayerDataController.OnGettingPlayerData` subscription — dead, because that publisher is in no scene. The `PlayerPrefs` ticket/reward logic is untouched. |
| `System/Xp/XpHandler.cs` | no (static, no instance) | `CaptainProgressCloudData` (UGS CloudSave) — its own docstring says *"Replaces the disabled PlayFab CaptainManager + XpHandler system"* | **Sever.** Drops the three `GetUserDataResult` parsers and the `PlayerDataController` calls. The XP surface `CaptainManager` actually uses (`GetCaptainXP`, `IssueXP`, `EncounterCaptain`, `EncounteredCaptainsData`) is kept. |
| `System/Playfab/Economy/CatalogManager.cs` | prefab in no scene | none — R4 de-scoped commerce | **Sever + move.** 38 live call sites, so it must keep compiling; 29 PlayFab-API lines removed. Already inert at runtime (`Instance` is null — its prefab is in no scene), so behaviour is unchanged. |
| `System/Playfab/Economy/DailyRewardHandler.cs` | prefab in no scene | none | **Sever + move.** Kept for `DailyRewardCard` and `DailyChallengeSystem`. |
| `Controller/Arcade/MiniGame.cs` | no (abstract, no subclass in a scene) | `PlayerDataService` | **Sever.** One live use: `PlayerDataController.PlayerProfile.DisplayName`. |

### 2b · Rescued — first-party code that only *lived* in the PlayFab folder

None of these reference PlayFab. Moved with `git mv` of the file **and** its `.meta`, so every guid
survives and no scene or prefab reference is disturbed.

| File | live refs | moved to |
|---|---|---|
| `Economy/CaptainManager.cs` | **18** (DI singleton, `Bootstrap.unity`) | `System/Economy/` |
| `Economy/VirtualItem.cs` | 8 files | `System/Economy/` |
| `Economy/Inventory.cs` | 5 files | `System/Economy/` |
| `Economy/StoreShelve.cs` | 1 file | `System/Economy/` |
| `Economy/ItemPrice.cs` | 0 outside, required by the above | `System/Economy/` |
| `PlayerData/PlayerProfile.cs` | 5 files | `System/PlayerData/` |
| `PlayerData/PlayerSession.cs` | 1 file (`ProfileModal`) | `System/PlayerData/` |

### 2c · Deleted — PlayFab-era, no live consumer once 2a is severed

| File | why |
|---|---|
| `Authentication/AuthenticationManager.cs` | `Awake()` already `base.Awake(); return;`. Its only consumers are severed in 2a. |
| `Authentication/AuthenticationView.cs`, `PlayFabAccount.cs`, `AuthMethods.cs` | zero references outside the folder. |
| `PlayerData/PlayerDataController.cs` | prefab in no scene, so `OnGettingPlayerData` / `OnProfileLoaded` never fire. |
| `PlayStream/LeaderboardManager.cs` | prefab in no scene; `LeaderboardEntry` preserved into `LeaderboardsMenu`. |
| `PlayStream/AnalyticsController.cs`, `EventsModel.cs`, `PlayerData/PlayerEvent.cs` | PlayStream-era. `Docs/Analytics/DATA_ARCHITECTURE.md` names `AnalyticsServiceFacade` (UGS Analytics) as the single writer; nothing references these. |
| `Economy/CatalogBundleHandler.cs`, `Utility/ModelConversionService.cs`, `Utility/PlayFabUtility.cs`, `CloudScripts/CloudScriptRunner.cs`, `Groups/GroupController.cs`, `Groups/GroupModel.cs`, `PlayerData/CaptainInstanceData.cs` | zero references outside the folder. |
| `PlayFabTests/PlayFabCatalogTests.cs` + its `.asmdef` | tests for the deleted SDK. |
| `Editor/PlayfabProductGenerator.cs` | editor tool that writes PlayFab catalog products. |
| `Utility/ChoppingBlock/AndroidIAPExample.cs` | PlayFab Economy sample, already on the chopping block. |
| `System/Architectures/EventBus/TestLoginUI.cs` | exists only to call `AuthenticationManager.AnonymousLogin()`; in no scene. |
| `_Prefabs/CORE/{AuthenticationManager,PlayerDataController,LeaderboardManager,PlayFabUtility}.prefab` | their scripts are gone. `AuthenticationManager.prefab` was instanced in `Authentication.unity`; that instance is removed with it. |

### 2d · Comment-only — untouched

`System/Quest/QuestSystem.cs`, `UI/Views/ArcadeExploreView.cs`, `UI/ScreenSwitcher.cs`,
`UI/Modals/AppInitializationModal.cs`, `Utility/CSDebug.cs`,
`ScriptableObjects/SO_CommerceAvailability.cs` name PlayFab only in comments that explain why
something is inert. They are left as-is — the comments stay true.

## 3 · The SDK

- **`Assets/PlayFabSDK` (4.7 MB) — deleted.** `PlayFab.asmdef` has `"includePlatforms": []`, so it
  compiled into the player, and `Shared/Public/Resources/` is a shipping `Resources` folder.
- **`Assets/PlayFabEditorExtensions` (4.9 MB) — deleted too.** It is editor-only
  (`includePlatforms: ["Editor"]`) and ships nothing, so this is repository weight, not build
  weight. It goes because its entire purpose is configuring the SDK that no longer exists; keeping
  an editor window that edits a deleted backend is how a retired system gets mistaken for a live
  one.

## 4 · What was NOT done

- **The Records screen is not ported to UGS Leaderboards.** It compiles and renders its empty
  state. That is the same thing the player saw before this branch (the PlayFab fetch could not
  return), so nothing regressed — but the screen is now honestly empty rather than accidentally
  empty, and porting it is a follow-up.
- **The commerce surfaces are unchanged.** R4 left them locked and fail-closed via
  `SO_CommerceAvailability`; this branch does not alter that posture, only the dead backend behind
  it.

## 5 · Measurement

`Tools/Build/measure_build_reachability.py`, before and after. It excludes `Plugins` and
`_Scripts` from its totals by design (code compiles regardless of references), so the SDK's *code*
weight is not in these numbers — the 4.7 MB / 4.9 MB on disk is.

The numbers below are the **A/B**: `bleeding-edge` and this branch, measured with the same tool on
the same day, after this branch merged `bleeding-edge` in. That matters — a parallel branch removed
Wwise, Parse, `SerializeInterface`, the TMP examples and the QuickScenePro `Resources/` folder while
this one was in flight, so a before-number taken at the merge base would attribute their deletions
to this branch.

| | assets indexed | reachable | reached MB | unreached MB | `Resources/` roots |
|---|---|---|---|---|---|
| `bleeding-edge` | 7,374 | 2,893 | 426.9 | 663.2 | 135 |
| this branch | **7,128** | **2,889** | 426.9 | **655.1** | **134** |

So the retirement takes **246 assets and 8.1 MB out of the project** and removes **one
unconditionally-packed `Resources/` root** — `PlayFabSDK/Shared/Public/Resources/`, which was
shipping into every player build. Reached MB is unchanged, which is the expected shape: nothing in
either folder was reachable to begin with (`PlayFabSDK` 3.8 MB / 102 assets and
`PlayFabEditorExtensions` 4.4 MB / 70, each 0.0 MB reached), so this is repository and
`Resources/`-pack weight rather than build weight.

**The four reachable assets lost are the deleted `CORE` prefabs** — `AuthenticationManager`,
`PlayerDataController`, `LeaderboardManager` and `PlayFabUtility`, reachable only from the
`Authentication` scene instance §2c removed.

## 6 · Verification status

**Not verified in the Unity editor.** There is no `unity` binary and no open Editor in this
session, so `/verify-unity` could not run: the project has **not** been compiled by Unity, Menu_Main
has not been opened, and the profile modal, the Records screen and the Hangar have not been
exercised. Treat that as the outstanding risk on this branch — on a deletion this size, the error
class that matters (a member that no longer exists, a signature that drifted) needs a symbol table,
which is editor-only.

What *was* run, and what each actually proves:

| check | result | what it covers |
|---|---|---|
| `check_enum_member_references.py` | OK (177 enums, 1,880 files) | no stale `CSLogChannel.LegacyPlayFab` |
| `check_switch_label_collisions.py` | OK | no duplicate enum labels |
| `check_conditional_compilation.py` | OK (1,932 files) | guards still cover self-consistent units |
| `check_using_directives.py` | 15, down from 18 | three pre-existing findings were in deleted files; **none introduced** |
| Roslyn parse of `Assets/_Scripts` | **0 syntax errors, 1,880 files** | every file still parses. Syntax only — it resolves no types |
| deleted-guid sweep | 0 dangling | no scene, prefab or asset references a deleted script |
| deleted-type sweep | 0 | the 21 types removed are named nowhere in `_Scripts` |
| deleted-member sweep | 0 | 28 removed members named nowhere |
| caller/callee cross-check | OK | `CaptainManager`'s five `XpHandler` needs, `CatalogManager`'s `EncounterCaptain(string)`, the new `PlayDailyChallenge(Action)` |

The Roslyn pass is a real gate but a narrow one, and it is narrow for the reason CLAUDE.md already
records: with every `MonoBehaviour` base type in the `Assembly-CSharp` monolith, an out-of-editor
compile cannot bind class bodies, so *what it proves shrinks silently as the code gets more
Unity-shaped*. It is evidence that nothing is malformed, not that everything resolves.

**What a human should check first, in this order:** the project compiles; Menu_Main opens; the
profile modal's randomize-name button now fills the field instead of spinning forever; the Records
screen opens without throwing; the Hangar's captain list still populates.

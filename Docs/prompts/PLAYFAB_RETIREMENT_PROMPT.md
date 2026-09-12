# Prompt — scope the PlayFab retirement (4.7 MB of dead backend compiled into every build)

Paste everything below into a fresh session. **This is a scoping task first and a deletion task
second — do not start by deleting anything.**

---

`Assets/PlayFabSDK` ships. `PlayFab.asmdef` has `"includePlatforms": []`, so the SDK compiles into
the player, and `Shared/Public/Resources/` is a `Resources` folder, so it is packed whole. CLAUDE.md
describes the backend as **"PlayFab SDK (legacy, inert)"** and the live backend as Unity Gaming
Services.

**It measures 0 inbound guid references — and that is the blind spot, not the answer.** `using
PlayFab;` creates no guid link. Read `Docs/LAUNCH_BLOCKER_INDEX.md` §B2 before anything else.

## The 20 first-party files that compile against it

```
Editor/PlayfabProductGenerator.cs
System/DailyChallengeSystem.cs
System/Playfab/Authentication/AuthenticationManager.cs
System/Playfab/Authentication/AuthenticationView.cs
System/Playfab/Authentication/PlayFabAccount.cs
System/Playfab/CloudScripts/CloudScriptRunner.cs
System/Playfab/Economy/CatalogBundleHandler.cs
System/Playfab/Economy/CatalogManager.cs
System/Playfab/Economy/DailyRewardHandler.cs
System/Playfab/Groups/GroupController.cs
System/Playfab/Groups/GroupModel.cs
System/Playfab/PlayStream/AnalyticsController.cs
System/Playfab/PlayStream/EventsModel.cs
System/Playfab/PlayStream/LeaderboardManager.cs
System/Playfab/PlayerData/PlayerDataController.cs
System/Playfab/Utility/ModelConversionService.cs
System/Playfab/Utility/PlayFabUtility.cs
System/Xp/XpHandler.cs
UI/Modals/ProfileModal.cs
Utility/ChoppingBlock/AndroidIAPExample.cs
```

Seventeen are inside `System/Playfab/` and go with the SDK. **Three are not, and they are the whole
job:** `DailyChallengeSystem`, `XpHandler`, `ProfileModal`.

## Phase 1 — answer these before writing code

For each of the three outliers, and for anything a scene or prefab still wires:

1. **Is it reachable from a build scene at all?** `ProfileModal` is UI — find whether Menu_Main still
   instances it. A component nothing instances is a different (easier) problem from one the menu
   opens.
2. **Does UGS already cover it?** CLAUDE.md is explicit that the live stack is UGS Analytics,
   CloudSave, Leaderboards, Multiplayer and Friends, and that `AuthenticationServiceFacade` is the
   sole writer of auth state with the PlayFab auth files *"deprecated and inert"*. For each PlayFab
   call site, name the UGS equivalent or say there is none.
3. **XP is the one to be careful about.** `XpHandler` is progression, `Docs/MENU_PROGRESSION_AND_IAP.md`
   is the live document for it, and progression that silently stops recording is a data-loss bug
   rather than a build-size one.
4. **What did R4 already de-scope?** The commerce surfaces were de-scoped, and the Hangar captain
   upgrade is PlayFab-catalog commerce — so `CatalogManager`, `CatalogBundleHandler` and
   `DailyRewardHandler` may already have no live entry point. **Confirm it; do not assume it.**
5. **Is `DailyChallengeSystem` the inert one?** `Docs/WEEKLY_CHALLENGE.md` records that the
   PlayFab-era `DailyChallengeSystem` / `DailyChallengeModal` / `DailyChallengeLeaderboardView`
   cluster **deliberately keeps its old name** so the dead feature is not confused with the live
   weekly challenge, and that the two were found to be sharing a value type. That doc is the
   authority; read it rather than re-deriving.

**Write the answers down before touching a file.** The output of phase 1 is a table — call site,
reachable yes/no, UGS equivalent or none, decision — and that table is what makes phase 2 reviewable.

## Phase 2 — the removal, if phase 1 says it is safe

In this order, each its own commit:

1. Delete the call sites that phase 1 proved dead, **innermost first** (a consumer before the thing
   it consumes), so the compiler tells you at each step whether the next layer is really unused.
2. Port anything phase 1 said UGS covers, one system at a time.
3. Only then remove `Assets/PlayFabSDK`, with a guid proof **and** a code proof (`grep -rl "using
   PlayFab"` returning nothing under `_Scripts`).
4. `Assets/PlayFabEditorExtensions` is **editor-only** (`includePlatforms: ["Editor"]`) and ships
   nothing — 4.9 MB of repository weight only. It can go with the SDK, or stay; either is defensible,
   but say which and why.

## Constraints

* **Do not delete a file because it compiles against PlayFab.** The SDK is the dependency; the
  feature may still be wanted with a different backend.
* **Auth is the live system and must not be disturbed.** `AuthenticationServiceFacade` is the sole
  writer of `AuthenticationDataVariable`; `System/Playfab/Authentication/*` is the deprecated path.
  Confirm nothing in the live chain reaches the PlayFab files before removing them.
* **`Docs/Analytics/DATA_ARCHITECTURE.md` is the authority on what analytics must keep working** —
  `AnalyticsController` and `EventsModel` are PlayStream-era; check them against it.
* **Run the out-of-editor gates after each commit** — `check_enum_member_references.py`,
  `check_switch_label_collisions.py`, `check_using_directives.py`,
  `check_conditional_compilation.py` — and remember what they cannot see: anything needing a symbol
  table is **editor-only**, which on a deletion of this size is most of the risk.

## Definition of done

1. The phase-1 table, committed as part of `Docs/LAUNCH_BLOCKER_INDEX.md` §B2 or a doc it points to.
2. Whatever phase 2 the table justified, in reviewable commits.
3. `python3 Tools/Build/measure_build_reachability.py` before/after.
4. Verified in the editor (`/verify-unity`): the project compiles, Menu_Main opens, the profile modal
   and the Hangar still work, and progression still records — or stated plainly that it was not.

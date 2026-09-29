# Prompt — organise `Assets/` after the third-party removals

Paste everything below into a fresh session — **but only after the removal and replacement prompts
have landed.** Reorganising while folders are still being deleted means doing it twice, and a move
touches paths that a deletion would have made moot.

---

`Assets/` currently interleaves **17 third-party folders** with the first-party `_`-prefixed ones, at
the same level, in no order. After the third-party pass there will be fewer of them — this is the job
of putting what remains somewhere a person can find it, and clearing the strays the audit turned up
on the way past.

Read `Docs/THIRD_PARTY_DECISIONS.md` §5 first. Measured on `claude/zealous-davinci-vjwev0`.

## The rule that makes every move safe

**A move is `git mv` of the file AND its `.meta`, never a rename.**

CLAUDE.md states it under the assembly-split rules and it applies to every asset in the project:
*"relocating a file that blocks a boundary is a `git mv` of the file **and** its `.meta`, never a
rename. Renaming is what breaks scene and prefab references."* A guid lives in the `.meta`; move both
together and every reference in every scene, prefab and SO survives untouched. Move the file alone and
Unity re-imports it with a **new** guid and every reference dangles.

**Three things change meaning when a path changes, and a guid does not protect any of them:**

1. **`Resources/`** — Unity packs *any* folder named `Resources/` into the player whole, and
   `Resources.Load("Name")` resolves by **path relative to that folder**. Moving a `Resources/` folder
   changes what ships; moving a file *within* one changes the load key. Both are invisible to a guid
   check. Grep for `Resources.Load` before moving anything under one.
2. **`Editor/`** — a folder literally named `Editor` is what puts code in `Assembly-CSharp-Editor` and
   keeps it out of the player. Moving a script out from under one **ships it**, and the failure is an
   IL2CPP linker error on the *release* build only (`Docs/CONDITIONAL_COMPILATION.md` —
   this project has already broken the Windows build exactly this way once, with NUnit).
3. **Generated settings assets referenced from `ProjectSettings/`** — see the hazard list below.

## Hazards, measured

**`Adaptive Performance` settings exist in two folders and both are referenced from `ProjectSettings/`.**

| Asset | Referenced from |
|---|---|
| `Assets/Adaptive Performance/Settings/Basic Provider Settings.asset` | `ProjectSettings/EditorBuildSettings.asset` |
| `Assets/Unity Assests/Adaptive Performance/AdaptivePerformanceGeneralSettings.asset` | `ProjectSettings/ProjectSettings.asset` **and** `EditorBuildSettings.asset` |

These are package-generated settings whose location the package itself chose, and they are wired into
preloaded assets / build settings. **Do not move them to tidy the tree.** Consolidating the two
locations is a legitimate goal but it is a *package configuration* change, not a file move — do it
through the package's own settings UI and let it rewrite the references, or leave it alone.

**`Assets/Unity Assests/` is a typo, and renaming it is the single riskiest move available.** It holds
`TextMesh Pro/` (two `Resources/` folders, fleet-wide), `Adaptive Performance/` (above) and
`Editor/com.unity.mobile.notifications/`. A rename is safe *for guid references* if the `.meta` files
move with it, and **unsafe for anything that resolves by path** — the two TMP `Resources/` folders
are exactly that. `Docs/LAUNCH_BLOCKER_INDEX.md` §C1 already records that `TMP Settings.asset`
measures **zero** guid references and every text component in the game needs it, because TMP loads it
by name. **Either leave the typo alone or treat the rename as its own commit with a full text-render
verification pass.** Leaving it is a defensible answer; renaming it blind is not.

## The strays this audit found

| Finding | Measured | Action |
|---|---|---|
| **`Assets/.DS_Store` and `Assets/_Scripts/Controller/.DS_Store` are tracked** | `.gitignore` already ignores `.DS_Store` at lines 108–109 — these predate it | `git rm --cached` both |
| **`Assets/ArcadeDPadNav.cs` sits loose at the `Assets/` root** | first-party `MonoBehaviour` (`CosmicShore.Core`), **is used** — `Menu_Main.unity` | `git mv` (+ `.meta`) to `_Scripts/UI/Elements/` or `_Scripts/Controller/IO/` |
| **`Assets/Environment/` is empty** | zero files | delete |
| **`Assets/Resolvers/ObjectResolver.cs`** | first-party, whole-file `#if UNITY_EDITOR`, uses `UnityEditor` — and is referenced by **nothing** | it is dead; delete it, or move it under an `Editor/` folder if it is wanted. Do **not** leave editor-only code outside an `Editor/` folder relying on a guard |

## A target shape

One decision to make first, and it is a real one: **a single `Assets/ThirdParty/` root, or leave each
pack where it is?**

* **For it:** the boundary becomes visible, the next audit is a directory listing, and "is this ours?"
  stops being a per-folder judgement call.
* **Against it:** every move is a chance to break a `Resources/` path, several vendors (FMOD, PlayFab,
  EDM4U) document their own install path and re-importing an update will recreate the original
  location, and `Assets/Plugins/` is already Unity's conventional third-party home for exactly this.

**The cheapest correct answer is probably: consolidate into `Assets/Plugins/`, which already holds
FMOD, Obvious (SOAP), Demigiant (DOTween), NativeShare and ParrelSync — and leave the vendors that
re-create their own path alone**, documenting why each exception exists. Pick one and write down the
reason; an undocumented convention is one the next importer will not follow.

Whatever is chosen, **the surviving third-party folders after the removal pass** are the ones to
place: FMOD, Obvious SOAP, DOTween, NativeShare, PlayFabSDK, PlayFabEditorExtensions, QuickScene Pro
(`YethGameDev`), EDM4U, `Analyzers`, `Samples`, and `Unity Assests` (TMP).

## Method

Do this in **small commits, one folder per commit**, each verified:

1. `git mv` the folder (file and `.meta` together — `git mv` on a directory does this).
2. Grep for anything that resolved by **path** rather than guid: `Resources.Load`, `AssetDatabase`
   path strings, `Application.dataPath`, hard-coded `"Assets/…"` literals (`PrimitivePlusConstants.cs`
   had one — check for its survivors), and `Tools/Build/*.py` scripts that walk a path.
3. Open the editor and let it re-import; **a clean re-import with no "missing script"/"missing
   reference" console entries is the check**. Nothing else proves a move.
4. `python3 Tools/Build/check_conditional_compilation.py` if any `.cs` moved.

## Definition of done

1. The `.DS_Store` files untracked, `Assets/Environment/` gone, `ArcadeDPadNav.cs` and the
   `ObjectResolver` question resolved.
2. Third-party folders consolidated per whichever convention was chosen, **with the convention and its
   exceptions written into `Docs/THIRD_PARTY_DECISIONS.md` §5.**
3. `Adaptive Performance` and `Unity Assests` either left alone with the reason recorded, or moved in
   their own commits with a text-render and build-settings verification.
4. Editor opens with zero missing references (`/verify-unity`), and `Menu_Main` + one gameplay scene
   render correctly — or stated plainly that it was not verified.

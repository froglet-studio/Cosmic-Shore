# Prompt — stop shipping three folders that only exist for demos and editor tools

Paste everything below into a fresh session.

---

Unity packs **any folder named `Resources/` into the player whole, referenced or not** (unless it
sits under an `Editor/` folder), and code with no `.asmdef` compiles into `Assembly-CSharp` whether
anything calls it or not. Three places in this project are on the wrong side of both rules.

Read `Docs/LAUNCH_BLOCKER_INDEX.md` §A1–§A3 and §1 of `Docs/THIRD_PARTY_REGISTER.md` first. Every
number below is measured on `claude/zealous-davinci-vjwev0`.

## 1. TextMesh Pro `Examples & Extras` — 7.6 MB, of which 3.5 MB ships unconditionally

`Assets/Unity Assests/TextMesh Pro/Examples & Extras` is TMP's demo content: 148 assets and **34
`.cs` files with no asmdef**, so the scripts compile into the player, and
`Examples & Extras/Resources/` (3.5 MB) is packed whole.

**It is not cleanly removable — two example fonts are wired into shipped content:**

| Example asset | Referenced by |
|---|---|
| `Resources/Fonts & Materials/Electronic Highway Sign SDF.asset` | **`_Prefabs/Spacevessels/Manta.prefab`** |
| `Resources/Fonts & Materials/Bangers SDF.asset` | `_Prefabs/UI Elements/Main Menu Screens/QuestItemPrefab.prefab` |

Both verified unique-guid-owner. **Deleting the folder wholesale breaks a shipped vessel prefab.**

**Do this:** `git mv` those two assets (file **and** `.meta`, so the guid travels and no reference
needs repointing) into the real `Assets/Unity Assests/TextMesh Pro/Resources/`, re-run the sweep for
anything else the first pass missed, then remove the rest.

**Do not touch `Assets/Unity Assests/TextMesh Pro/Resources/`** — index §C1. It holds
`TMP Settings.asset`, which TMP loads by **name**, so it measures zero guid references and every
piece of text in the game needs it.

## 2. QuickScene Pro's `Resources/` — 1.8 MB of editor-tool icons in the player

`Assets/YethGameDev/QuickScenePro` is an **editor tool** and its code is correctly under `Editor/`.
Its `Resources/` folder is not, so three editor icons (`QSP_Icon.png`, `icon_additive.png`,
`icon_single.png`) are packed into every build. They are referenced only by its own three demo
scenes, none of which is in `EditorBuildSettings`.

**Do this:** `git mv` `Resources/` to `Editor/Resources/`. `Resources.Load` still resolves for
editor code, so the tool keeps working and the player stops carrying it. `Demo/` can go the same
way. Licence is **MIT with the notice present**, so this is bloat, not entitlement.

## 3. NiceVibrations' Demo asmdef — 30 scripts compiling into the player

`Lofelt.NiceVibrations.Demo.asmdef` has `"includePlatforms": []`, so its **30 demo scripts compile
into the player**. The demo scene is not in the build list, so the scene itself does not ship.

**Do this and nothing more here:** set that asmdef to `"includePlatforms": ["Editor"]`. That is the
safe half and it is reversible.

**Do NOT delete `Assets/NiceVibrations/Demo`.** Its ART is wired into `Menu_Main.unity` — the
removal is [`NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md`](NICEVIBRATIONS_DEMO_ASSET_REPLACEMENT_PROMPT.md),
and the plugin itself is [`HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md`](HAPTICS_VENDOR_INDEPENDENCE_PROMPT.md).

## The rule that governs all three

**A move is `git mv` of the file AND its `.meta`, never a rename** — the guid lives in the `.meta`,
so a move preserves every reference and a rename breaks them. But **three things change meaning when
a path changes and a guid does not protect any of them**: a `Resources/` folder (what ships, and the
`Resources.Load` key), an `Editor/` folder (whether code reaches the player at all — and that
failure is an IL2CPP linker error on the *release* build only, which this project has already hit
once with NUnit), and package-generated settings wired into `ProjectSettings/`.

So after each move: grep for `Resources.Load`, `AssetDatabase` path strings and hard-coded
`"Assets/…"` literals; run `python3 Tools/Build/check_conditional_compilation.py` if any `.cs`
moved; and open the editor to confirm a clean re-import with **no missing script or missing
reference** entries — nothing else proves a move.

## Definition of done

1. The two TMP fonts relocated and `Examples & Extras` removed, in **that order**, in separate
   commits, each with a guid-count proof that nothing dangles.
2. QuickScene Pro's `Resources/` under `Editor/`; the tool still opens and draws its icons.
3. The NiceVibrations Demo asmdef editor-only.
4. `python3 Tools/Build/measure_build_reachability.py` re-run and the before/after quoted.
5. `Docs/LAUNCH_BLOCKER_INDEX.md` §A1–§A3 updated with what actually happened.
6. Verified in the editor (`/verify-unity`) — Menu_Main, the arcade screen and a gameplay scene all
   render text correctly — or stated plainly that it was not.

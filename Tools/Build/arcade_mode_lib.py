#!/usr/bin/env python3
"""
The shared half of every `author_<mode>_assets.py` - what a new arcade mode's generator would
otherwise copy from the previous one (and did, eleven times, drifting a little each time).

A mode generator is deterministic and idempotent: every minted GUID is md5("CosmicShore/<stable
name>"), the whole result is validated in memory, and only then is anything written. `--check`
validates AND diffs every generated file against disk. This module owns that shape so a mode's
script is only the numbers and the decisions that are genuinely its own. See
`.claude/skills/arcadegame/SKILL.md` for the recipe that uses it.

    import arcade_mode_lib as lib
    g = lib.Generator(mode_id=54, mode_name="WreckingBall")
    g.emit(path, content)           # queue a file
    g.script_meta(path, guid)       # queue a .cs.meta for a script this mode adds
    ...
    g.register_game_list(card_guid) # the party-games roster
    g.register_always_unlocked()    # ProgressionConfig
    g.register_build_scene("MinigameScarabScramble", "MinigameWreckingBall", scene_guid)
    g.set_end_condition("wreckingBallPrismTarget", after="tollwayTollTarget", value=1500)
    g.register_toast_config(toast_guid)
    g.register_preview(preview_guid)
    g.finish(errors, referenced=EXISTING, minted=[...])   # validate, then --check or write
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv

# ── Well-known asset paths every mode registers into ────────────────────────
GAME_LIST = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"   # the master roster
ARCADE_GRID = "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"         # what the Arcade screen draws
ARENA_GRID = "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset"           # what the Arena screen draws
PROGRESSION = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
BUILD_SETTINGS = "ProjectSettings/EditorBuildSettings.asset"
END_CONDITIONS = "Assets/Resources/EndConditionOverrides.asset"
TOAST_LIBRARY = "Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset"
PREVIEW_LIBRARY = "Assets/Resources/ModePreviewLibrary.asset"
SCENES_DIR = "Assets/_Scenes/Multiplayer Scenes"

# ── Script types every mode's assets point at (read from the repo, never invented) ──
# A mode script should still assert these resolve (Generator.finish does) - they are listed
# here so the eleventh generator does not carry the eleventh copy of the same table.
SCRIPT_GUIDS = {
    "SO_ArcadeGame":                       "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":                    "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":                      "e8d8aa5d835249798a256e18f2f7d912",
    "FloraConfigurationSO":                "a32a297a7606432885f4d3e1f83bea9a",
    "GameToastConfigSO":                   "86d1715b8f104fcc87cb60e015d4b563",
    "ModePreviewDefinitionSO":             "9f1780f039eb88d45a9cd7a4a75f9e13",
    "ExplosionImpactorDataContainerSO":    "841db4ce66da4384a711272307733e0f",
    "VesselCombatHitByExplosionEffectSO":  "22cfb97278f955c7cfdfaf0ec3e9fef2",
    "ExplosionWitherLifeformByCrystalEffectSO": "d454bc2ad0884c5c92e9787913057d19",
}

# ── Shared arena content every cell config references ───────────────────────
CELL_VISUALS = {
    "CellIcon":        "6aa1c06e11b265744a5f9fa8858ac72a",
    "MembranePrefab":  "6e330f85972faf843b8a128e7166f7b5",   # CapsuleMembrane, radius 1200
    "NucleusPrefab":   "b9cf1833fa2493d4b8724ccb6740fb3a",   # the standard nucleus (prefab scale 400)
    "CytoplasmPrefab": "9cacd903fcf4643459f5f14ac811bb20",
}
MEMBRANE_RADIUS = 1200.0

# ── Vessel data assets (SO_Class_*), by class - every PLAYABLE hull ─────────
# An ARENA card lists several of these in its Vessels; an arcade card exactly one. Grizzly,
# Termite, Falcon and Shrike are not here because they are not shipped playable kits
# (Grizzly's prefab carries no R_VesselActions and a disabled AI).
VESSELS = {
    "Manta":    "b0e6ec5495dbfb6419332830d585f364",
    "Dolphin":  "c0f30e9f09616874780edc0a375ce686",
    "Rhino":    "ec97e344adb08f847a8f7649ab79088e",
    "Urchin":   "bde48fa4833b6364b93111a55ba90958",
    "Squirrel": "6fbeef29c9430b94aabea2934640dc5a",
    "Serpent":  "8a288448ab55edc46ac841a5f2e53d83",
    "Sparrow":  "7b7053dd065edb54baa3b831b90f4985",
    "Scarab":   "b136d82d275e0f8ea1feef29f0d416a4",
    "Butterfly": "fe4abf38579d7f84baf16a532ed4a015",
}

# VesselClassType enum ids (Assets/_Scripts/Data/Enums/VesselClassType.cs) - a card's
# StartingElements rows and a scene's aiInitializeDatas name hulls by these.
VESSEL_CLASS_ID = {
    "Manta": 1, "Dolphin": 2, "Rhino": 3, "Urchin": 4, "Grizzly": 5, "Squirrel": 6,
    "Serpent": 7, "Termite": 8, "Falcon": 9, "Shrike": 10, "Sparrow": 11, "Scarab": 12,
    "Butterfly": 13,
}

# ── Arcade card art shared by the pure-aggression party games (Rampage, Dog Fight, Bends) ──
CARD_ART = {
    "IconActive":     "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":   "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground": "587d2203114c8004c9985d0112c89585",
}


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


def existing_guid(rel_path: str) -> str:
    """The guid an asset ALREADY carries, read off its .meta - for referencing a script or asset
    another generator (or a human) authored."""
    with open(os.path.join(ROOT, rel_path + ".meta"), encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
    assert m, f"no guid in {rel_path}.meta"
    return m.group(1)


_HEADER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def header_for(script_guid: str, name: str) -> str:
    """The MonoBehaviour-asset preamble every ScriptableObject asset starts with."""
    return _HEADER_TMPL.replace("__GUID__", script_guid).replace("__NAME__", name)


def script_meta_text(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta_text(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def prefab_meta_text(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta_text(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def num(v) -> str:
    """A YAML number the way Unity writes it: ints bare, floats trimmed."""
    if isinstance(v, int):
        return str(v)
    s = f"{v:.6f}".rstrip("0").rstrip(".")
    return s if s else "0"


def toast(situation: int, template: str, tint_domain: int = 0, domain_names: int = 1,
          every_n: int = 1, idle: int = 0, idle_seconds: int = 60, alpha=1) -> str:
    """One GameToastDefinition entry (the field set GameToastConfigSO serializes today)."""
    return (f"  - situation: {situation}\n"
            f"    messageTemplate: '{template}'\n"
            f"    tintWithDomainColor: {tint_domain}\n"
            f"    useDomainColoredNames: {domain_names}\n"
            f"    alpha: {num(alpha)}\n"
            f"    everyN: {every_n}\n"
            f"    isIdleHint: {idle}\n"
            f"    resetOnSituation: 0\n"
            f"    idleSeconds: {idle_seconds}\n"
            f"    repeatWhileIdle: 1\n")


def swap_guid(text: str, old: str, new: str, label: str, expected: int = 1) -> str:
    """Replace a donor's guid in a cloned scene, asserting it appeared exactly as often as the
    donor was expected to reference it - a swap that silently matched 0 or 3 times is how a
    clone ends up half-wired."""
    text, n = re.subn(old, new, text)
    assert n == expected, f"{label} guid appeared {n} times (expected {expected})"
    return text


def replace_block(text: str, old: str, new: str, label: str) -> str:
    assert text.count(old) == 1, f"{label}: block not found exactly once in the donor"
    return text.replace(old, new, 1)


class Generator:
    """The queue of files a mode authors, plus the registrations every mode makes."""

    def __init__(self, mode_id: int, mode_name: str):
        self.mode_id = mode_id
        self.mode_name = mode_name
        self.files: "dict[str, str]" = {}
        self.stale: "list[str]" = []

    # ── files ──
    def read(self, rel: str) -> str:
        with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
            return fh.read()

    def read_current(self, rel: str) -> str:
        """The file as this run has it - a queued rewrite if there is one, else disk. Use this
        for the shared registries so two registrations in one run compose."""
        return self.files[rel] if rel in self.files else self.read(rel)

    def emit(self, rel: str, content: str):
        self.files[rel] = content

    def script_meta(self, rel: str, g: str):
        assert os.path.exists(os.path.join(ROOT, rel)), f"script {rel} does not exist"
        self.emit(rel + ".meta", script_meta_text(g))

    def emit_asset(self, rel: str, g: str, content: str):
        self.emit(rel, content)
        self.emit(rel + ".meta", asset_meta_text(g))

    def folder_meta(self, rel_dir: str):
        """A .meta for a NEW folder, minted off the folder's path. Unity refuses to import a
        folder without one, and a generator that writes into a fresh directory but not its meta
        ships a folder the editor re-mints with a random guid on every machine."""
        self.emit(rel_dir.rstrip("/") + ".meta",
                  f"fileFormatVersion: 2\nguid: {guid('folder/' + rel_dir.rstrip('/'))}\n"
                  f"folderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
                  f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")

    def text_meta(self, rel: str):
        """A .meta for a hand-written text asset inside Assets/ (a mode's .md), minted off its
        path - the doc is the human's, the meta is the generator's."""
        assert os.path.exists(os.path.join(ROOT, rel)), f"text asset {rel} does not exist"
        self.emit(rel + ".meta",
                  f"fileFormatVersion: 2\nguid: {guid('text/' + rel)}\nTextScriptImporter:\n"
                  f"  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

    def emit_prefab(self, rel: str, g: str, content: str):
        """A generator-authored prefab (an arena's environment spawnable variant, one per
        intensity) plus its PrefabImporter meta."""
        self.emit(rel, content)
        self.emit(rel + ".meta", prefab_meta_text(g))

    def emit_scene(self, name: str, g: str, content: str):
        rel = f"{SCENES_DIR}/{name}.unity"
        self.emit(rel, content)
        self.emit(rel + ".meta", scene_meta_text(g))

    # ── registrations ──
    def register_game_list(self, card_guid: str, list_path: str = GAME_LIST):
        """One roster. A card needs the MASTER roster (OrganicRematchGames) to be a rematch
        candidate AND a GRID (ArcadeGames) to be reachable from a screen - check_gamelist_scenes
        reports a card on the master roster and neither grid as "launchable, reachable from no
        screen". Use register_arcade_card for the usual pair."""
        games = self.read_current(list_path)
        entry = f"  - {{fileID: 11400000, guid: {card_guid}, type: 2}}\n"
        if entry not in games:
            assert games.endswith("\n")
            games += entry
        self.emit(list_path, games)

    def register_arcade_card(self, card_guid: str):
        """The master roster plus the Arcade grid - where every shipped party game lives."""
        self.register_game_list(card_guid, GAME_LIST)
        self.register_game_list(card_guid, ARCADE_GRID)

    def register_arena_card(self, card_guid: str):
        """The master roster plus the ARENA grid - where a card that seats several hulls lives.
        Docs/HomeHub/ARCHITECTURE.md 3.1: ArcadeGames is "the master minus the arena cards", so
        an arena card goes in the master (the guest's by-mode lookup) and ArenaGames (the Arena
        screen's rosterOverride, which is also what routes it to the ArenaLaunchPanel and its
        vessel carousel) and NOT in ArcadeGames."""
        self.register_game_list(card_guid, GAME_LIST)
        self.register_game_list(card_guid, ARENA_GRID)

    def register_always_unlocked(self):
        prog = self.read_current(PROGRESSION)
        if re.search(rf"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - {self.mode_id}\n", prog, re.M) is None:
            prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)",
                              rf"\g<1>  - {self.mode_id}\n", prog, count=1)
            assert n == 1, "alwaysUnlockedModes block not found"
        self.emit(PROGRESSION, prog)

    def register_build_scene(self, after_scene: str, scene: str, scene_guid: str):
        """Add the mode's scene to `ProjectSettings/EditorBuildSettings.asset`.

        ⚠ THAT FILE IS NOT IN THE ASSETDATABASE, so a Unity Editor that is already open
        will never re-read it. The registration is correct on disk, `check_gamelist_scenes.py`
        passes, the card renders — and pressing it fails with *"has not been added to the build
        settings scenes in build list"*, about a list that on disk contains it. Worse, the next
        project-settings save writes the Editor's stale in-memory list back over this file and
        silently deletes the entry.

        `BuildSceneListReconciler` repairs this on every domain reload, so pulling the branch
        or touching a script is enough; **FrogletTools > Game Modes > Reconcile Build Scene
        List** is the same check run deliberately, with a report.
        """
        build = self.read_current(BUILD_SETTINGS)
        if f"{scene}.unity" not in build:
            anchor = re.search(
                rf"(  - enabled: 1\n    path: {re.escape(SCENES_DIR)}/{re.escape(after_scene)}\.unity\n"
                r"    guid: [0-9a-f]{32}\n)", build)
            assert anchor, f"{after_scene} entry not found in EditorBuildSettings"
            build = build.replace(anchor.group(1), anchor.group(1) +
                                  f"  - enabled: 1\n    path: {SCENES_DIR}/{scene}.unity\n"
                                  f"    guid: {scene_guid}\n")
        self.emit(BUILD_SETTINGS, build)

    def set_end_condition(self, key: str, after: str, value: int):
        """Author BOTH the live and the build-baseline value of a target. SET rather than
        insert-if-absent, so a re-run after a target change moves the number the game reads."""
        endcond = self.read_current(END_CONDITIONS)
        for live_key, new_key in ((after, key), (after + "Build", key + "Build")):
            existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
            if existing:
                endcond = endcond.replace(existing.group(0), f"  {new_key}: {value}\n", 1)
                continue
            m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
            assert m, f"{live_key} not found in {END_CONDITIONS}"
            endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {value}\n", 1)
        self.emit(END_CONDITIONS, endcond)

    def register_toast_config(self, toast_guid: str):
        lib_text = self.read_current(TOAST_LIBRARY)
        entry = f"  - {{fileID: 11400000, guid: {toast_guid}, type: 2}}\n"
        if entry not in lib_text:
            assert lib_text.endswith("\n")
            lib_text += entry
        self.emit(TOAST_LIBRARY, lib_text)

    def register_preview(self, preview_guid: str):
        plib = self.read_current(PREVIEW_LIBRARY)
        entry = f"  - {{fileID: 11400000, guid: {preview_guid}, type: 2}}\n"
        if entry not in plib:
            m = re.search(r"^  Definitions:\n((?:  - \{fileID[^\n]*\n)+)", plib, re.M)
            assert m, "ModePreviewLibrary Definitions block not found"
            plib = plib.replace(m.group(0), m.group(0) + entry, 1)
        self.emit(PREVIEW_LIBRARY, plib)

    # ── validation + output ──
    def finish(self, errors: "list[str]", referenced: "dict[str, str]", minted: "list[str]"):
        """Run the shared validation, then --check or write. `errors` is the mode's own list of
        failures (may be empty); `referenced` the guids it read from the repo; `minted` the guids
        it invented. Exits non-zero on any failure."""
        errors = list(errors)
        if len(set(minted)) != len(minted):
            errors.append("minted GUID collision within this script")

        owned_metas = {os.path.normpath(os.path.join(ROOT, rel))
                       for rel in self.files if rel.endswith(".meta")}
        existing = set()
        for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
            for fn in filenames:
                if not fn.endswith(".meta"):
                    continue
                full = os.path.normpath(os.path.join(dirpath, fn))
                if full in owned_metas:
                    continue
                try:
                    with open(full, encoding="utf-8", errors="ignore") as fh:
                        m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
                    if m:
                        existing.add(m.group(1))
                except OSError:
                    pass
        for g in minted:
            if g in existing:
                errors.append(f"minted GUID {g} collides with an asset this script does not own")
        for name, g in referenced.items():
            if g not in existing:
                errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")

        if errors:
            print("VALIDATION FAILED:")
            for e in errors:
                print("  -", e)
            sys.exit(1)

        if CHECK_ONLY:
            changed = []
            for rel, content in self.files.items():
                full = os.path.join(ROOT, rel)
                if not os.path.exists(full):
                    changed.append(f"{rel} (missing on disk)")
                    continue
                on_disk = self.read(rel)
                if on_disk != content:
                    changed.append(f"{rel} ({first_diff_line(on_disk, content)})")
            for rel in self.stale:
                if os.path.exists(os.path.join(ROOT, rel)):
                    changed.append(f"{rel} (stale - should be deleted)")
            if changed:
                print(f"--check: {len(changed)} file(s) differ from the authored output:")
                for c in sorted(changed):
                    print("  -", c)
                sys.exit(1)
            print(f"--check: OK, {len(self.files)} file(s) match.")
            sys.exit(0)

        for rel, content in self.files.items():
            full = os.path.join(ROOT, rel)
            os.makedirs(os.path.dirname(full), exist_ok=True)
            with open(full, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(content)
        for rel in self.stale:
            full = os.path.join(ROOT, rel)
            if os.path.exists(full):
                os.remove(full)
                print("  removed stale", rel)
        print(f"Wrote {len(self.files)} file(s).")
        for rel in sorted(self.files):
            print("  ", rel)


def first_diff_line(a: str, b: str) -> str:
    al, bl = a.splitlines(), b.splitlines()
    for i, (x, y) in enumerate(zip(al, bl), start=1):
        if x != y:
            return f"line {i}"
    return f"line {min(len(al), len(bl)) + 1} (length differs)"

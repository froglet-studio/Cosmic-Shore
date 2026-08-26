#!/usr/bin/env python3
"""Author the Bloomrush mode set (GameModes.Bloomrush = 45) — the Manta's party game.

ONE-SHOT donor-clone migration in the author_salvo_assets.py family. It clones
MinigameBends.unity (chosen for its ARENA: Bends already reuses Rampage's four
cactus-forest cell configs verbatim, which are exactly Bloomrush's reef — dense
forest, crystal-scarcity intensity ladder, wildlife climb) and swaps only the mode
identity: the controller, the turn monitor (point-race → 120 s NetworkTimeBased),
the scoring rule, and the crystal ladder. Then it registers the mode everywhere a
mode must be registered (card, live game list, progression unlock, build settings).

Like author_dogfight_assets.py, this generator asserts on the DONOR's exact field
blocks: the day someone reworks the Bends scene, this file becomes permanently
un-runnable — which is the correct end state for a one-shot migration. Do not
"fix" the asserts to chase the donor; the shipped Bloomrush assets are the record.

Deterministic guids (md5 of a stable name), idempotent, --check compares.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK = "--check" in sys.argv


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/Bloomrush/{name}".encode()).hexdigest()


SCENE_GUID = guid_for("MinigameBloomrush.unity")
CARD_GUID = guid_for("ArcadeGameBloomrush.asset")
RULE_GUID = guid_for("BloomrushScoringRule.asset")
TOAST_CONFIG_GUID = guid_for("GameToastConfig_Bloomrush.asset")

BLOOMRUSH_CONTROLLER_SCRIPT = "69368ea7f6e0e391ec445493af126df4"
BLOOMRUSH_RULE_SCRIPT = "a750b5dde5b98470d4e0cc9c58c16ae0"
BENDS_CONTROLLER_SCRIPT = "94ec6e4a35948f20e999eb581c8637e4"
BENDS_MONITOR_SCRIPT = "55e3116c4ada864354f5693a022364a9"
NETWORK_TIME_MONITOR_SCRIPT = "30f5bd573bfc4a29891fc9a175083c37"
SO_ARCADE_GAME_SCRIPT = "fe040efad3307fb449b6b72ad15362da"
GAME_TOAST_CONFIG_SCRIPT = "86d1715b8f104fcc87cb60e015d4b563"
MANTA_CLASS_GUID = "b0e6ec5495dbfb6419332830d585f364"


def sub(t: str, old: str, new: str, label: str) -> str:
    assert old in t, f"DONOR DRIFT ({label}): anchor missing — see the module docstring"
    assert t.count(old) == 1, f"DONOR DRIFT ({label}): anchor ambiguous"
    return t.replace(old, new)


def build_scene() -> str:
    t = open(os.path.join(ROOT, "Assets/_Scenes/Multiplayer Scenes/MinigameBends.unity")).read()

    # Controller identity: BendsController → BloomrushController, and its mode fields.
    t = sub(t,
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % BENDS_CONTROLLER_SCRIPT,
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % BLOOMRUSH_CONTROLLER_SCRIPT,
        "controller script")
    t = sub(t,
        """  rule: {fileID: 11400000, guid: d17aa51522bc1bedc765900d767f71b5, type: 2}
  arenaCell: {fileID: 1700000065}
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  aiAimRetargetSeconds: 1.25
  aiAimLeadSeconds: 0.35
  aiAimBlastReach: 2400
  aiAimBlastDuration: 2.7
  aiAimHumanFocus: 3
  aiAimMaxRange: 2400""",
        """  rule: {fileID: 11400000, guid: %s, type: 2}
  arenaCell: {fileID: 1700000065}
  fuseSecondsByIntensity:
  - 30
  - 25
  - 20
  - 20
  elementalCrystalCount: 16
  crystalScatterRadius: 850
  crystalScatterSeed: 45""" % RULE_GUID,
        "controller fields")

    # Turn monitor: the Bends point race becomes the 120-second clock. Same GameObject,
    # same fileID, new class + one new key — the change-type-in-place technique.
    t = sub(t,
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % BENDS_MONITOR_SCRIPT,
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % NETWORK_TIME_MONITOR_SCRIPT,
        "monitor script")
    monitor = re.search(
        r"(--- !u!114 &1628508337\nMonoBehaviour:\n.*?_updateInterval: 1\n)", t, re.S)
    assert monitor, "DONOR DRIFT: monitor doc 1628508337 moved"
    t = t[:monitor.end(1)] + "  duration: 120\n" + t[monitor.end(1):]

    # Crystal ladder: plentiful at intensity 1 (crystals close and constant), contested by
    # 4 — the Bloomrush spec's own axis, replacing Bends' inherited Rampage scarcity.
    t = sub(t,
        """  - CrystalsPerPlayer: 2
    ExtraCrystals: 0
  - CrystalsPerPlayer: 1
    ExtraCrystals: 0
  - CrystalsPerPlayer: 1
    ExtraCrystals: -1""",
        """  - CrystalsPerPlayer: 3
    ExtraCrystals: 2
  - CrystalsPerPlayer: 2
    ExtraCrystals: 1
  - CrystalsPerPlayer: 1
    ExtraCrystals: 0""",
        "crystal ladder")
    return t


SCENE_META = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""".format(guid=SCENE_GUID)

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

RULE_ASSET = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: BloomrushScoringRule
  m_EditorClassIdentifier:
  metric: 9
  golfRules: 0
""" % BLOOMRUSH_RULE_SCRIPT

CARD_ASSET = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %(script)s, type: 3}
  m_Name: ArcadeGameBloomrush
  m_EditorClassIdentifier:
  Mode: 45
  IsMultiplayer: 1
  DisplayName: Bloomrush
  Description: Mantas only, and nobody has to learn a button. Soar through the reef
    to arm your bombs, graze wildlife and rival rays to plant them - silently, one
    bomb per target, first tag takes it - then reach a crystal before the fuses burn
    down and set the whole board off at once. Biggest bloomed volume in 120 seconds
    wins it for your domain.
  IconActive: {fileID: 21300000, guid: 1dc25875d7cbd3e478fc5a133e65eedb, type: 3}
  IconInactive: {fileID: 21300000, guid: fa9b62abd1b217b4ba3d7c5a4a2c0916, type: 3}
  CardBackground: {fileID: 21300000, guid: 587d2203114c8004c9985d0112c89585, type: 3}
  PreviewClip: {fileID: 241334157148977051, guid: 4396864d799a6154bb82e5346ac0093b, type: 3}
  GolfScoring: 0
  SceneName: MinigameBloomrush
  Vessels:
  - {fileID: 11400000, guid: %(manta)s, type: 2}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 404
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: 0.027
""" % {"script": SO_ARCADE_GAME_SCRIPT, "manta": MANTA_CLASS_GUID}
# Comeback rate derivation: the comeback source is PrismsDestroyed (see
# ElementalComebackSystem.DefaultSourceFor). First-pass expected winning count over a
# 120 s round ~300 prisms; a quarter-of-expected deficit (75) x 0.027 = ~2.0 element
# levels — the Dog Fight curve, the nearest sibling by structure. Re-derive from the
# first playtest's real prism counts (the trap recorded on Dog Fight/Bends/Wildlife).


TOAST_CONFIG = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: GameToastConfig_Bloomrush
  m_EditorClassIdentifier:
  gameMode: 45
  toasts:
  - situation: 70
    messageTemplate: '<b>{0}</b> KABLOOM - {1} bombs'
    tintWithDomainColor: 1
    useDomainColoredNames: 0
    alpha: 1
    isIdleHint: 0
    resetOnSituation: 0
    idleSeconds: 60
    repeatWhileIdle: 1
  - situation: 71
    messageTemplate: 'Skim the reef to arm - graze anything to tag it - grab a crystal to set them all off'
    tintWithDomainColor: 0
    useDomainColoredNames: 0
    alpha: 0.85
    isIdleHint: 1
    resetOnSituation: 70
    idleSeconds: 25
    repeatWhileIdle: 1
""" % GAME_TOAST_CONFIG_SCRIPT
# The mode is BUTTONLESS, so a pilot who has not been told has nothing to press and no
# reason to guess. The hint resets on the cash-out toast: once you have Kabloomed, you know.


def main() -> int:
    writes = {
        "Assets/_SO_Assets/Game Toasts/GameToastConfig_Bloomrush.asset": TOAST_CONFIG,
        "Assets/_SO_Assets/Game Toasts/GameToastConfig_Bloomrush.asset.meta":
            ASSET_META.format(guid=TOAST_CONFIG_GUID),
        "Assets/_Scenes/Multiplayer Scenes/MinigameBloomrush.unity": build_scene(),
        "Assets/_Scenes/Multiplayer Scenes/MinigameBloomrush.unity.meta": SCENE_META,
        "Assets/_SO_Assets/Scoring Rules/BloomrushScoringRule.asset": RULE_ASSET,
        "Assets/_SO_Assets/Scoring Rules/BloomrushScoringRule.asset.meta": ASSET_META.format(guid=RULE_GUID),
        "Assets/_SO_Assets/Games/ArcadeGameBloomrush.asset": CARD_ASSET,
        "Assets/_SO_Assets/Games/ArcadeGameBloomrush.asset.meta": ASSET_META.format(guid=CARD_GUID),
    }

    # Registrations (append-if-absent, SET semantics on re-run).
    gamelist = os.path.join(ROOT, "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset")
    gl = open(gamelist).read()
    entry = "  - {fileID: 11400000, guid: %s, type: 2}\n" % CARD_GUID
    if entry not in gl:
        assert gl.endswith("type: 2}\n"), "game list tail moved"
        writes["Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"] = gl + entry

    # Toast library: without this row the mode's situations resolve to nothing and the
    # cash-out announces itself nowhere.
    lib = os.path.join(ROOT, "Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset")
    lb = open(lib).read()
    lib_entry = "  - {fileID: 11400000, guid: %s, type: 2}\n" % TOAST_CONFIG_GUID
    if TOAST_CONFIG_GUID not in lb:
        m = re.search(r"(  modeConfigs:\n(?:  - \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}\n)+)", lb)
        assert m, "GameToastLibrary.asset modeConfigs block moved"
        lb = lb[:m.end(1)] + lib_entry + lb[m.end(1):]
    writes["Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset"] = lb

    prog = os.path.join(ROOT, "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset")
    pg = open(prog).read()
    if re.search(r"^  - 45$", pg, re.M) is None:
        pg2 = sub(pg, "  - 44\n  firstQuestAlwaysUnlocked:", "  - 44\n  - 45\n  firstQuestAlwaysUnlocked:",
                  "progression unlock")
        writes["Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"] = pg2

    build = os.path.join(ROOT, "ProjectSettings/EditorBuildSettings.asset")
    bs = open(build).read()
    if "MinigameBloomrush.unity" not in bs:
        anchor = ("  - enabled: 1\n"
                  "    path: Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity\n"
                  "    guid: 313d5cf05253dc4d163a58dc415a07da\n")
        new = anchor + ("  - enabled: 1\n"
                        "    path: Assets/_Scenes/Multiplayer Scenes/MinigameBloomrush.unity\n"
                        "    guid: %s\n" % SCENE_GUID)
        bs2 = sub(bs, anchor, new, "build settings")
        writes["ProjectSettings/EditorBuildSettings.asset"] = bs2

    drift = []
    for rel, want in writes.items():
        p = os.path.join(ROOT, rel)
        have = open(p).read() if os.path.exists(p) else None
        if have != want:
            drift.append(rel)
            if not CHECK:
                open(p, "w").write(want)
    if CHECK:
        if drift:
            print("DRIFT:\n  " + "\n  ".join(drift))
            return 1
        print("check clean")
        return 0
    print(f"wrote {len(drift)} file(s)" if drift else "idempotent: nothing to write")
    return 0


if __name__ == "__main__":
    sys.exit(main())

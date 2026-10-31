#!/usr/bin/env python3
"""
Authors every serialized asset the Serpent Gulag game mode needs (GameModes.SerpentGulag = 77).

Serpent Gulag is the Serpent's CHARGE game - a SNIPER DUEL at range (Shombith, 2026-10-31: "a sniper duel and a
big enough cell for 4 players playing at a distance"). Pilots spawn far apart in a 3,600 u cell; a field of
elemental crystals floats in the middle at every intensity ("this field is common"). A snipe - a Sniper Shot that
strips a rival pilot, already a Debuff-class combat hit - is one point; first DOMAIN to the target wins.

THE LADDER (one cell config per intensity, Cleave's shape, Cleave's x3 membrane):
  I1 Open Ground  - no environment at all, the field alone ("No environment in the first intensity").
  I2 Watchtowers  - six sniper nests on a ring around the field.
  I3 Cell Blocks  - eight nests, and eight barred screens across the sight lines to the field.
  I4 The Gulag    - ten nests, twelve screens, danger plates on the nest frames and the bars.
The nests and screens are SpawnableSerpentGulag (one prefab per rung; the layout reads only its fields, so it bakes).

NOTHING ABOUT THE SERPENT CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the arena,
the field and the AI.

Beside Garrett's in-flight Serpent Charge (67, cece/vibrant-archimedes-n3xn3x), which this does not touch. The
scene is cloned from Dustup's (the CombatPoints duel whose controller declares settings / rule / arenaCell).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_serpent_gulag_assets.py [--check]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 77
g = lib.Generator(MODE_ID, "SerpentGulag")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "SerpentGulagController":       f"{AR}/SerpentGulag/SerpentGulagController.cs",
    "SerpentGulagSettingsSO":       f"{AR}/SerpentGulag/SerpentGulagSettingsSO.cs",
    "SerpentGulagScoringRuleSO":    f"{AR}/SerpentGulag/SerpentGulagScoringRuleSO.cs",
    "SerpentGulagMarksman":         f"{AR}/SerpentGulag/SerpentGulagMarksman.cs",
    "SerpentGulagSnipeTurnMonitor": f"{AR}/TurnMonitors/SerpentGulagSnipeTurnMonitor.cs",
    "SpawnableSerpentGulag":        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableSerpentGulag.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Serpent Gulag Cell"
G_ASSET = {
    "ArcadeGameSerpentGulag":      guid("asset/ArcadeGameSerpentGulag"),
    "SerpentGulagScoringRule":     guid("asset/SerpentGulagScoringRule"),
    "SerpentGulagSettings":        guid("asset/SerpentGulagSettings"),
    "GameToastConfigSerpentGulag": guid("asset/GameToastConfigSerpentGulag"),
    "ModePreviewSerpentGulag":     guid("asset/ModePreviewSerpentGulag"),
    "MinigameSerpentGulag.unity":  guid("asset/MinigameSerpentGulag.unity"),
    "SerpentGulagSpawnProfile":    guid("asset/SerpentGulagSpawnProfile"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"SerpentGulagCellConfig{i}"] = guid(f"asset/SerpentGulagCellConfig{i}")
for i in (2, 3, 4):
    G_ASSET[f"prefab/SpawnableSerpentGulag{i}"] = guid(f"asset/SpawnableSerpentGulag{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Serpent"] = lib.VESSELS["Serpent"]
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"           # SpawnablePrism.prefab (window 0.5-100)
EXISTING["CleaveMembrane_prefab"] = "6eb45784f9024cbb879f94c23ae46bd9"  # the x3 shell, radius 3,600
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
SNIPER_SHOT = "Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset"
EXISTING["SniperShotAction"] = lib.existing_guid(SNIPER_SHOT)
# The donor scene's identity, minted by author_dustup_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameDustup.unity"
DONOR = {
    "DustupController":       guid("script/DustupController"),
    "DustupPointTurnMonitor": guid("script/DustupPointTurnMonitor"),
    "DustupSettings":         guid("asset/DustupSettings"),
    "DustupScoringRule":      guid("asset/DustupScoringRule"),
}
BONEYARD = [lib.existing_guid(f"Assets/_SO_Assets/Cell Configs/Boneyard Cell/Boneyard Cell Config {i}.asset")
            for i in range(1, 5)]
TIGER_MOTH_ID = lib.VESSEL_CLASS_ID["TigerMoth"]
SERPENT_ID = lib.VESSEL_CLASS_ID["Serpent"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 7700000000000201, 7700000000000202, 7700000000000203

# ── The duel ─────────────────────────────────────────────────────────────────
SNIPE_TARGET = 6          # kept in sync with EndConditionOverridesSO.DefaultSerpentGulagSnipeTarget (checked)
SNIPE_POINTS = 1
DEBUFF_CLASS = 2          # CombatHitClass.Debuff - what SniperShotAction reports its pilot strip as
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (1.5 snipes) buys
# 1.2 element levels; on this hull a Charge level is a faster rifle.
COMEBACK_RATE = 0.8

# ── The arena (Cleave's x3 membrane at every rung) ───────────────────────────
MEMBRANE_RADIUS = 3600.0
CYTOPLASM_SHARD_DISTANCE = round(120 * MEMBRANE_RADIUS / 1200)   # keeps a standard cell's shard count
SPAWN_RING = 2200          # adjacent pilots ~3,100 apart (just past the rifle's 3,000), opposite 4,400
FIELD_RADIUS = 380.0       # the crystal field; the omni crystal spawns inside it too
FIELD_COUNT = 24
RIFLE_RANGE = 3000

RUNGS = [
    # intensity, label, blurb, seed, nests, nest ring, nest spread, screens, screen ring, screen spread, danger
    dict(i=1, label="Open Ground", blurb="no environment - open space, far spawns and the crystal field alone",
         prefab=None),
    dict(i=2, label="Watchtowers", blurb="six sniper nests on a ring around the field, each with a firing slit "
         "facing the middle and an entry at its back", seed=7702, nests=6, nestRing=1200, nestSpread=300,
         screens=0, screenRing=700, screenSpread=200, danger=0),
    dict(i=3, label="Cell Blocks", blurb="eight nests, and eight barred screens across the sight lines to the field",
         seed=7703, nests=8, nestRing=1300, nestSpread=350, screens=8, screenRing=700, screenSpread=200, danger=0),
    dict(i=4, label="The Gulag", blurb="ten nests, twelve barred screens, danger plates on the nest frames and the bars",
         seed=7704, nests=10, nestRing=1400, nestSpread=450, screens=12, screenRing=750, screenSpread=220, danger=6),
]
for r in RUNGS:
    if r["i"] > 1:
        r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableSerpentGulag{r['i']}.prefab"

# ── A Python twin of SpawnableSerpentGulag's counts and volumes (PhaseThresholds ride the baseline) ──
NEST_W, NEST_H, NEST_D, PLATE, TILE = 90.0, 60.0, 90.0, 3.0, 15.0
SLIT = (31.0, 9.0)
ENTRY = (16.0, 16.0)
SCREEN_W, SCREEN_H, BAR_STEP, BAR, BAR_SEG, RAIL_LEN, RAIL_CLEAR = 208.0, 120.0, 16.0, 3.0, 24.0, 16.0, 0.5
FLUSH = 0.995


def nest_plates():
    """[(w, h, d)] of every plate one nest lays, in the C#'s order."""
    plates = []
    cols, rows = round(NEST_W / TILE), round(NEST_H / TILE)
    tw, th = NEST_W / cols, NEST_H / rows
    for openw, openh in (SLIT, ENTRY):
        for c in range(cols):
            for r in range(rows):
                x, y = -NEST_W / 2 + tw * (c + 0.5), -NEST_H / 2 + th * (r + 0.5)
                if abs(x) < openw and abs(y) < openh:
                    continue
                plates.append((tw * FLUSH, th * FLUSH, PLATE * FLUSH))
    inner_d = NEST_D - 2 * PLATE
    sc, sr = max(1, round(inner_d / TILE)), max(1, round(NEST_H / TILE))
    for _ in range(2):
        plates += [(PLATE * FLUSH, NEST_H / sr * FLUSH, inner_d / sc * FLUSH)] * (sc * sr)
    inner_w = NEST_W - 2 * PLATE
    cc, cr = max(1, round(inner_w / TILE)), max(1, round(inner_d / TILE))
    for _ in range(2):
        plates += [(inner_w / cc * FLUSH, PLATE * FLUSH, inner_d / cr * FLUSH)] * (cc * cr)
    return plates


def screen_prisms():
    planks = max(1, round(SCREEN_W / RAIL_LEN))
    rl = SCREEN_W / planks
    out = [(rl * FLUSH, BAR * FLUSH, BAR * 1.4)] * (2 * planks)
    bars = max(1, math.floor(SCREEN_W / BAR_STEP))
    span = SCREEN_H - 2 * RAIL_CLEAR
    segs = max(1, round(span / BAR_SEG))
    out += [(BAR, span / segs * FLUSH, BAR)] * (bars * segs)
    return out


def rung_census(r):
    if r["prefab"] is None:
        return 0, 0.0
    prisms = nest_plates() * r["nests"] + screen_prisms() * r["screens"]
    return len(prisms), sum(a * b * c for a, b, c in prisms)


# Cleave's ladder deltas (cleave_budget.BLOB_DELTAS): a cell's phase rides ITS arena's own baseline. This mode has
# no fauna, so the phase only matters as the cell's own state, but it must not start a match in the wrong one.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameSerpentGulag.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/SerpentGulag")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/SERPENTGULAG.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/SerpentGulagScoringRule.asset", G_ASSET["SerpentGulagScoringRule"],
             lib.header_for(G_SCRIPT["SerpentGulagScoringRuleSO"], "SerpentGulagScoringRule") +
             f"""  metric: 8
  golfRules: 1
  snipePoints: {SNIPE_POINTS}
  otherHitPoints: 0
  snipesLeftTemplate: '{{count}} Snipes Left'
  snipesLandedTemplate: '{{count}} snipes'
  victoryHeader: VICTORY
  victoryLabel: GULAG TIME
  defeatHeader: DEFEAT
  defeatLabel: SNIPES LEFT
""")

# ── 3. Mode settings (the ladders: index = intensity - 1) ────────────────────
LADDERS = {
    # name: (values, "up" = harder means larger, "down" = harder means smaller)
    "aiRetargetSeconds":          ([1.2, 0.9, 0.6, 0.4], "down"),
    "aiAimErrorDegrees":          ([3.0, 2.0, 1.2, 0.5], "down"),
    "aiScopeSettleSeconds":       ([1.0, 0.6, 0.3, 0.15], "down"),
    "aiHuntRange":                ([1500, 2000, 2500, 3000], "up"),
    "aiForageMinCooldownSeconds": ([999, 7, 5, 3.5], "down"),
    "aiCloakAfterShotChance":     ([0, 0.25, 0.5, 0.75], "up"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/SerpentGulagSettings.asset", G_ASSET["SerpentGulagSettings"],
             lib.header_for(G_SCRIPT["SerpentGulagSettingsSO"], "SerpentGulagSettings") +
             f"""  progressSampleSeconds: 0.5
  fieldCrystalCount: {FIELD_COUNT}
  fieldRadius: {lib.num(FIELD_RADIUS)}
  fieldRegrowSeconds: 8
  fieldSeed: 77
""" + "".join(ladder_yaml(n) for n in LADDERS) + """  aiMinAimDistance: 250
  aiStandoffDistance: 650
  aiScopeOpenAngle: 15
  aiShotGiveUpSeconds: 3
  aiLeadSeconds: 0.25
  aiHumanFocus: 2
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: the rifle never strips a team-mate, so a solo lobby has nobody to score on.
DESCRIPTION = ("Serpents only - a sniper duel at range. You spawn far apart in a huge cell with a field of "
               "elemental crystals floating in the middle. Charge reloads your rifle faster, so the field is where "
               "you load up, in everyone's sights. Hold LT to scope and RT to fire - a round that strips a rival pilot "
               "is one snipe. Higher intensities add sniper nests and barred cell blocks. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameSerpentGulag"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameSerpentGulag") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Serpent Gulag
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameSerpentGulag
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Serpent']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_SerpentGulag.asset", G_ASSET["GameToastConfigSerpentGulag"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_SerpentGulag") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> sniped a rival!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} snipes", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} snipes", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(157, "Hold LT to scope, RT to fire - the crystals in the middle reload you faster",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigSerpentGulag"])

# ── 6. The arena: one environment prefab per rung (2-4), four cell configs, one spawn profile ──
for r in RUNGS:
    if r["prefab"] is None:
        continue
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableSerpentGulag{r['i']}"], f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{SPAWNABLE_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SPAWNABLE_TR}}}
  - component: {{fileID: {SPAWNABLE_MB}}}
  m_Layer: 0
  m_Name: SpawnableSerpentGulag{r['i']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{SPAWNABLE_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{SPAWNABLE_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableSerpentGulag']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {r['seed']}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: {r['i']}
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  nestCount: {r['nests']}
  nestRingRadius: {r['nestRing']}
  nestHeightSpread: {r['nestSpread']}
  screenCount: {r['screens']}
  screenRingRadius: {r['screenRing']}
  screenHeightSpread: {r['screenSpread']}
  dangerEveryNth: {r['danger']}
""")

for r in RUNGS:
    i = r["i"]
    count, volume = rung_census(r)
    th = thresholds(count, round(volume))
    r["count"] = count
    env = (f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableSerpentGulag{i}']}, type: 3}}"
           if r["prefab"] else "{fileID: 0}")
    desc = (f"{r['label']} - intensity {i} of Serpent Gulag: {r['blurb']}. {count} prisms, all neutral. "
            f"Membrane radius {int(MEMBRANE_RADIUS)} (Cleave's x3 shell), spawn ring {SPAWN_RING}, the crystal field "
            f"(radius {int(FIELD_RADIUS)}) laid by the controller at every rung. NO NUCLEUS, no flora, no fauna. "
            f"Authored by Tools/Build/author_serpent_gulag_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Serpent Gulag Cell Config {i}.asset", G_ASSET[f"SerpentGulagCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Serpent Gulag Cell Config {i}") + f"""  CellName: Serpent Gulag
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CleaveMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: {CYTOPLASM_SHARD_DISTANCE}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['SerpentGulagSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {env}
  EnvironmentIntensity: {i}
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {th['RestlessEnter']}
    RestlessExit: {th['RestlessExit']}
    FrenzyEnter: {th['FrenzyEnter']}
    FrenzyExit: {th['FrenzyExit']}
    RestlessEnterVolume: {th['RestlessEnterVolume']}
    RestlessExitVolume: {th['RestlessExitVolume']}
    FrenzyEnterVolume: {th['FrenzyEnterVolume']}
    FrenzyExitVolume: {th['FrenzyExitVolume']}
""")

# One spawn profile, shared by all four configs: it spawns nothing (no flora, no fauna).
g.emit_asset(f"{CELL_DIR}/Serpent Gulag Spawn Profile.asset", G_ASSET["SerpentGulagSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Serpent Gulag Spawn Profile") + """  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras: []
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 0
  InitialFaunaReleaseTier: 0
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 15
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 0
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0.5
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas: []
""")

CELLS = [G_ASSET[f"SerpentGulagCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 700\n  spawnFormation: 0\n")
NO_NUCLEUS = f"  noNucleusSpawnRadius: {int(FIELD_RADIUS)}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_SerpentGulag.asset", G_ASSET["ModePreviewSerpentGulag"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_SerpentGulag") + f"""  Mode: {MODE_ID}
  Notes: 'The Gulag cell at each intensity - open ground, then nests, then cell blocks. OPEN-ENDED -
    it scores on sniping rival pilots and a solo preview has none; what it teaches is the scope, the
    rifle and the crystal field. Vessel pinned to Serpent({SERPENT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SERPENT_ID}
  ObjectiveText: Scope with LT, fire with RT - snipe a rival pilot
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewSerpentGulag"])

# ── 8. Scene: Dustup's, with the identity, the cells, the AI hulls and the spawn ring swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["DustupPointTurnMonitor"], G_SCRIPT["SerpentGulagSnipeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["DustupController"], G_SCRIPT["SerpentGulagController"], "controller")
    scene = lib.swap_guid(scene, DONOR["DustupSettings"], G_ASSET["SerpentGulagSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["DustupScoringRule"], G_ASSET["SerpentGulagScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(BONEYARD) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {TIGER_MOTH_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SERPENT_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, "  noNucleusSpawnRadius: 420\n", NO_NUCLEUS, "crystal volume")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, NO_NUCLEUS]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameSerpentGulag.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameSerpentGulag", G_ASSET["MinigameSerpentGulag.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameSerpentGulag"])
g.register_always_unlocked()
g.register_build_scene("MinigameDustup", "MinigameSerpentGulag", G_ASSET["MinigameSerpentGulag.unity"])
g.set_end_condition("serpentGulagSnipeTarget", after="dustupPointTarget", value=SNIPE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * SNIPE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {SNIPE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * SNIPE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumption: the rifle still reports its pilot strip as Debuff, the class the rule pays.
shot = g.read(SNIPER_SHOT)
if f"  hitClass: {DEBUFF_CLASS}\n" not in shot:
    errors.append("SniperShotAction no longer reports a Debuff-class hit - SerpentGulagScoringRuleSO pays Debuff")
if f"  rangeUnits: {RIFLE_RANGE}\n" not in shot:
    errors.append(f"the rifle's range moved off {RIFLE_RANGE} - re-check the spawn ring and the AI's hunt ladder")
if "  coneHalfAngleDegrees: 1.5\n" not in shot:
    errors.append("the rifle's cone moved off 1.5 deg - re-check the aim-error ladder")

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultSerpentGulagSnipeTarget = {SNIPE_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultSerpentGulagSnipeTarget differs from SNIPE_TARGET")

# The ladders: each monotone toward harder; I4's aim inside the 1.5 deg cone, I1's outside; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["SerpentGulagSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + ("f" if True else "") for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
aim = LADDERS["aiAimErrorDegrees"][0]
if not (aim[3] < 1.5 < aim[0]):
    errors.append("aim error ladder must put I4 inside the 1.5 deg cone and I1 outside it")

# The geometry the duel promises.
if not (FIELD_RADIUS < min(r["screenRing"] - SCREEN_W / 2 for r in RUNGS if r["prefab"])):
    errors.append("a cell block stands inside the crystal field")
outer = max(math.hypot(r["nestRing"] + NEST_D, r["nestSpread"] + NEST_H) for r in RUNGS if r["prefab"])
if not (outer < SPAWN_RING - 300):
    errors.append(f"a nest stands within 300 u of the spawn ring ({outer:.0f} vs {SPAWN_RING})")
if not (SPAWN_RING + 600 < MEMBRANE_RADIUS):
    errors.append("the spawn ring is too close to the membrane")
adjacent = 2 * SPAWN_RING * math.sin(math.pi / 4)
if adjacent <= RIFLE_RANGE:
    errors.append(f"adjacent spawns ({adjacent:.0f} u) are inside the rifle's {RIFLE_RANGE} u - spawns must be FAR apart")
for r in RUNGS:
    if r["prefab"] and r["nests"] > 1:
        chord = 2 * r["nestRing"] * math.sin(math.pi / r["nests"])
        if chord < 3 * NEST_W:
            errors.append(f"I{r['i']}: nests crowd each other ({chord:.0f} u apart)")
    if r["prefab"] and r["screens"] > 1:
        chord = 2 * r["screenRing"] * math.sin(math.pi / r["screens"])
        if chord < SCREEN_W * 1.3:
            errors.append(f"I{r['i']}: cell blocks crowd each other ({chord:.0f} u apart)")
counts = [r["count"] for r in RUNGS]
if counts[0] != 0 or counts != sorted(counts):
    errors.append(f"the prism ladder must start at 0 (I1 has no environment) and rise ({counts})")
if counts[-1] > 6000:
    errors.append(f"I4 lays {counts[-1]} prisms - keep the duel arena light")

# The C# twin of this census stays the C#: its constants must still be the ones counted here.
spawnable = g.read(SCRIPT_PATHS["SpawnableSerpentGulag"])
for needle in ("NestWidth = 90f, NestHeight = 60f, NestDepth = 90f", "Plate = 3f, Tile = 15f",
               "SlitHalfWidth = 31f, SlitHalfHeight = 9f", "EntryHalfWidth = 16f, EntryHalfHeight = 16f",
               "ScreenWidth = 208f, ScreenHeight = 120f",
               "BarStep = 16f, BarSection = 3f, BarSegment = 24f, RailLength = 16f",
               "RailClearance = 0.5f", "Flush = 0.995f"):
    if needle not in spawnable:
        errors.append(f"SpawnableSerpentGulag no longer declares '{needle}' - update this census")

# The scene: every new identity present, every donor one gone, four Serpent AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("SerpentGulagController", "SerpentGulagSnipeTurnMonitor")},
                     "SerpentGulagSettings": G_ASSET["SerpentGulagSettings"],
                     "SerpentGulagScoringRule": G_ASSET["SerpentGulagScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Boneyard {i}": gd for i, gd in enumerate(BONEYARD, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {SERPENT_ID}\n") != 4 or f"  - vesselClass: {TIGER_MOTH_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Serpent AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/SerpentGulag"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/SERPENTGULAG.md")])

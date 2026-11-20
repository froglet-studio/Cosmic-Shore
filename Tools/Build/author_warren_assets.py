#!/usr/bin/env python3
"""
Authors every serialized asset the Warren game mode needs (GameModes.Warren = 106).

Warren is the Meerkat's MASS game (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 16, which reserved id 106 and hint toast
181): a TIMED burrow war scored on the prism volume a domain has STANDING in its own colour at the whistle
(ScoringMetric.VolumeRemaining, a live stock). The Meerkat's mass is its trail and the EXIT PORTAL every soliton rings
its way out with (six of its prisms on a hexagon, bigger with Mass; MEERKAT.md §10.4). The only other mass in the cell
is the rivals', and the Meerkat's own Space is the raid: a soliton formed beside a rival's mass swallows it and fires it
out of the way out, and a fired prism that runs into another breaks both (MEERKAT.md §8).

THE ARENA IS THE BARE BARREN CELL, referenced read-only (Tapestry, Switchback and Waystation run in it too): no
environment and no food web, on purpose - nothing to hide behind and nothing to score but pilots' mass. The ladder is
the crystals and the card's per-intensity STARTING ELEMENTS (SO_ArcadeGame.StartingElements, Brittle Star Time's lever):
  I1 Burrow          - six omni crystals.
  I2 Warren          - four omni crystals.
  I3 Danger Warren   - three crystals; everyone starts at Mass 5, so every exit portal is Danger Portal (L5).
  I4 Robbers' Warren - two crystals; Mass 5 and Space 5, so a swallowed prism is STOLEN (Steal, L5) as its run starts.

THE COMEBACK RATE IS SIZED AGAINST A VOLUME, and a volume is a function of the hull (Tapestry's reasoning). So the
model below reads the Meerkat's trail off the SHIPPED prefab (VesselPrismController's BaseScale, XScaler and
initialWavelength, its cruise speed) rather than typing a number.

NOTHING ABOUT THE MEERKAT CHANGES (the LOCKED vessel rule). The scene is cloned from Glasshouse's (the newest timed
VolumeRemaining game, whose controller declares settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_warren_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import meerkat_games_common as mk  # noqa: E402

MODE_ID = 106
TOAST_HINT = 181
g = lib.Generator(MODE_ID, "Warren")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "WarrenController":      f"{AR}/Warren/WarrenController.cs",
    "WarrenSettingsSO":      f"{AR}/Warren/WarrenSettingsSO.cs",
    "WarrenScoringRuleSO":   f"{AR}/Warren/WarrenScoringRuleSO.cs",
    "WarrenTimeTurnMonitor": f"{AR}/TurnMonitors/WarrenTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameWarren":      guid("asset/ArcadeGameWarren"),
    "WarrenScoringRule":     guid("asset/WarrenScoringRule"),
    "WarrenSettings":        guid("asset/WarrenSettings"),
    "GameToastConfigWarren": guid("asset/GameToastConfigWarren"),
    "ModePreviewWarren":     guid("asset/ModePreviewWarren"),
    "MinigameWarren.unity":  guid("asset/MinigameWarren.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Meerkat"] = lib.VESSELS["Meerkat"]
BARREN_REL = "Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset"
EXISTING["BarrenCellConfig"] = lib.existing_guid(BARREN_REL)
CELLS = [EXISTING["BarrenCellConfig"]] * 4   # one cell at every rung: the ladder is the crystals and the levels
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameGlasshouse.unity"
DONOR = {
    "GlasshouseController":      guid("script/GlasshouseController"),
    "GlasshouseTimeTurnMonitor": guid("script/GlasshouseTimeTurnMonitor"),
    "GlasshouseSettings":        guid("asset/GlasshouseSettings"),
    "GlasshouseScoringRule":     guid("asset/GlasshouseScoringRule"),
}
DONOR_CELLS = [guid(f"asset/GlasshouseCellConfig{i}") for i in (1, 2, 3, 4)]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
MEERKAT_ID = lib.VESSEL_CLASS_ID["Meerkat"]

# ── The round ────────────────────────────────────────────────────────────────
ROUND_SECONDS = 90            # kept in sync with EndConditionOverridesSO.DefaultWarrenRoundSeconds
RUNGS = [
    dict(i=1, label="Burrow", omni=6, mass=0, space=0, blurb="six omni crystals, nothing else"),
    dict(i=2, label="Warren", omni=4, mass=0, space=0, blurb="four omni crystals"),
    dict(i=3, label="Danger Warren", omni=3, mass=5, space=0,
         blurb="three crystals, and every pilot starts at Mass 5 - every exit portal is a Danger Portal"),
    dict(i=4, label="Robbers' Warren", omni=2, mass=5, space=5,
         blurb="two crystals, Mass 5 and Space 5 - a soliton STEALS what it swallows"),
]

# ── The trail model: what one Meerkat lays per second, read off the shipped hull ────────
PREFAB = g.read(mk.PREFAB)


def one(src, pattern, what):
    m = re.search(pattern, src, re.M)
    assert m, f"{what} not found in {mk.PREFAB}"
    return float(m.group(1))


BASE = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", PREFAB, re.M)
assert BASE, "Meerkat.prefab BaseScale not found"
base_x, base_y, base_z = (float(v) for v in BASE.groups())
x_scaler = one(PREFAB, r"^  XScaler: ([0-9.]+)$", "XScaler")
wavelength = one(PREFAB, r"^  initialWavelength: ([0-9.]+)$", "initialWavelength")
cruise = one(PREFAB, r"^\s*DefaultMinimumSpeed:\s*([0-9.]+)$", "DefaultMinimumSpeed") \
    + one(PREFAB, r"^\s*DefaultThrottleScaler:\s*([0-9.]+)$", "DefaultThrottleScaler")

KEY_VOLUME = (base_x * x_scaler / 2.0) * base_y * base_z     # VesselPrismController.CreateBlock, x halved
LAY_RATE = KEY_VOLUME * cruise / wavelength                    # volume / second, one pilot at cruise
# A domain of two pilots who lay about HALF the round (the other half is raiding or riding a tube, where no trail
# is laid) - Tapestry's reference. Conservative: a real lobby raids more and so stands less.
REFERENCE_DOMAIN_VOLUME = 2 * 0.5 * ROUND_SECONDS * LAY_RATE
# Comeback - a FUNCTION OF THE SCORE'S SCALE (bonusLevels = deficit x rate): a quarter of the reference behind buys 1.5
# levels. Rounded to the 6 decimals lib.num writes, so the assert below checks the number that SHIPS.
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameWarren.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Warren")
g.text_meta(f"{AR}/WARREN.md")
mk.shared_metas(g)

# ── 2. Scoring rule: metric 12 = VolumeRemaining, points mode ────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/WarrenScoringRule.asset", G_ASSET["WarrenScoringRule"],
             lib.header_for(G_SCRIPT["WarrenScoringRuleSO"], "WarrenScoringRule") + """  metric: 12
  golfRules: 0
  volumeTemplate: '{count} Volume'
  prismsTemplate: '{count} prisms'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME STANDING
""")

# ── 3. Settings (the ladders: index = AI difficulty - 1) ─────────────────────
LADDERS = {
    "aiRetargetSeconds":   ([4, 3, 2.2, 1.6], "down"),
    "aiRaidChance":        ([0.15, 0.3, 0.45, 0.6], "up"),
    "aiRaidRange":         ([300, 450, 600, 800], "up"),
    "aiRaidBehindSeconds": ([0.5, 1, 1.5, 2], "up"),
    "aiAimError":          ([80, 50, 25, 10], "down"),
}
SHARED = {"aiCrystalReach": 450, "aiLayClearance": 120, "aiLayOuterFraction": 0.75}
LEAD = {"leadSampleSeconds": 1, "leadAnnounceAfterSeconds": 15, "leadAnnounceMinGapSeconds": 8}

g.emit_asset("Assets/_SO_Assets/Games/WarrenSettings.asset", G_ASSET["WarrenSettings"],
             lib.header_for(G_SCRIPT["WarrenSettingsSO"], "WarrenSettings")
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in LEAD.items())
             + "".join(mk.ladder_yaml(n, LADDERS) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))


# ── 4. Arcade card ───────────────────────────────────────────────────────────
def starting_rows():
    rows = ""
    for r in RUNGS:
        if not (r["mass"] or r["space"]):
            continue
        rows += (f"  - Class: {MEERKAT_ID}\n    Intensity: {r['i']}\n    Levels:\n"
                 f"      Mass: {lib.num(r['mass'] / 10)}\n      Charge: 0\n"
                 f"      Space: {lib.num(r['space'] / 10)}\n      Time: 0\n")
    return rows


# MinPlayers 2 / MinDomains 2: a burrow war with one side is a screensaver, and the raid needs a rival.
# No colons in the description (the Serpentine trap).
DESCRIPTION = ("Meerkats only, in an empty cell. Everything you lay is yours - your trail, and the exit portal every "
               "soliton rings its way out with, bigger with Mass. Form a soliton beside a rival's mass and it swallows "
               "it and fires it back into the rest of it, breaking both. When the clock runs out, the team with the most "
               "standing wins. Higher intensities thin the crystals, start everyone at Mass 5 (danger portals) and then "
               "Space 5 (a soliton steals what it swallows).")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameWarren"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameWarren") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Warren
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameWarren
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Meerkat']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 9
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  StartingElements:
{starting_rows()}  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Warren.asset", G_ASSET["GameToastConfigWarren"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Warren") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(130, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Every soliton rings its exit with your prisms. Form one beside a rival's trail to "
                       "swallow it", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigWarren"])


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


def spawn_block(outside, rings):
    return (f"  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: {outside}\n"
            f"  spawnRingRadiusFloor: {rings[0]}\n  spawnRingRadiusFloorByIntensity:\n"
            + "".join(f"  - {v}\n" for v in rings) + "  spawnFormation: 0\n")


def omni_block(ball, counts):
    return (f"  noNucleusSpawnRadius: {ball}\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
            "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
            + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {n}\n" for n in counts))


SPAWN_OUTSIDE = 150           # Barren HAS a nucleus: 150 outside it, Tapestry's ring
RINGS = [0, 0, 0, 0]
SPAWN = spawn_block(SPAWN_OUTSIDE, RINGS)
DONOR_SPAWN = spawn_block(40, [450, 450, 450, 450])          # Glasshouse's ring
OMNI = omni_block(200, [r["omni"] for r in RUNGS])
DONOR_OMNI = omni_block(200, [4, 3, 3, 2])                    # Glasshouse's crystals
CELL_BLOCK = "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Warren.asset", G_ASSET["ModePreviewWarren"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Warren") + f"""  Mode: {MODE_ID}
  Notes: 'The bare Barren cell at every intensity - the ladder is the crystals and the starting levels, not the
    arena. OPEN-ENDED - it scores the mass you hold at the whistle; what a solo preview teaches is the trail and
    the exit portal every soliton lays. Vessel pinned to Meerkat({MEERKAT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {MEERKAT_ID}
  ObjectiveText: Lay your trail and form solitons - every one rings its exit with your prisms
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: {SPAWN_OUTSIDE}
  SpawnRingRadiusFloor: {RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewWarren"])

# ── 7. Scene: Glasshouse's, with the identity, the cells, the AI hulls, the ring and the crystals swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["GlasshouseTimeTurnMonitor"], G_SCRIPT["WarrenTimeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["WarrenController"], "controller")
    scene = lib.swap_guid(scene, DONOR["GlasshouseSettings"], G_ASSET["WarrenSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["GlasshouseScoringRule"], G_ASSET["WarrenScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              CELL_BLOCK, "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {MEERKAT_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "omni crystals")
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameWarren.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [CELL_BLOCK, SPAWN, OMNI])
errors += scene_errs
g.emit_scene("MinigameWarren", G_ASSET["MinigameWarren.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameWarren"])
g.register_always_unlocked()
g.register_build_scene("MinigameSentry", "MinigameWarren", G_ASSET["MinigameWarren.unity"])
g.set_end_condition("warrenRoundSeconds", after="sentryZapTarget", value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against a {REFERENCE_DOMAIN_VOLUME:.0f} u3 reference: a "
                  f"quarter-of-reference deficit buys {0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE:.2f} levels (< 1)")
if not (KEY_VOLUME > 0 and LAY_RATE > 0):
    errors.append("the trail model read a zero-volume trail off Meerkat.prefab")

# The load-bearing assumptions about the Meerkat, read from the shipped kit (never edited here): an exit portal is laid
# through the hull's own prism channel (so StatsManager counts it as the pilot's mass), a swallowed prism fired into
# another destroys both and credits the maker, and L5 Steal and Danger Portal are the Space and Mass gates.
portal = g.read(mk.PORTAL)
for needle in ("prisms.PrismSpawnChannel", "LayOne(channel, prisms.SpawnPrismType"):
    if needle not in portal:
        errors.append(f"MeerkatExitPortal no longer has '{needle}' - a portal would not be the pilot's counted mass")
swallow = g.read(mk.SWALLOW)
for needle in ("(t.Kit & MeerkatSolitonKit.KitSteal) != 0", "b.Prism.Steal(maker.PlayerName, maker.Domain);",
               "PrismMotion.Annihilate("):
    if needle not in swallow:
        errors.append(f"MeerkatSwallow no longer has '{needle}' - Warren's raid (and I4's steal) rests on it")
kit = g.read(mk.KIT)
for needle in ("KitSteal", "KitDanger"):
    if needle not in kit:
        errors.append(f"MeerkatSolitonKit no longer has '{needle}' - the I3/I4 starting levels would buy nothing")
errors += mk.enum_errors(g, "Warren", MODE_ID, "WarrenHint", TOAST_HINT)

eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
for needle in (f"DefaultWarrenRoundSeconds = {ROUND_SECONDS};", f"public int warrenRoundSeconds = {ROUND_SECONDS};",
               f"public int warrenRoundSecondsBuild = {ROUND_SECONDS};"):
    if needle not in eco:
        errors.append(f"EndConditionOverridesSO does not carry '{needle}' (ROUND_SECONDS)")

errors += mk.ladder_errors(g.read(SCRIPT_PATHS["WarrenSettingsSO"]), "WarrenSettingsSO", LADDERS, {**SHARED, **LEAD})
errors += mk.steer_errors(g, [SCRIPT_PATHS["WarrenController"]])

# The ladder: fewer crystals each rung, starting levels never falling, and a level that buys an L5 is exactly 5.
for name, values, direction in (("omni crystals", [r["omni"] for r in RUNGS], "down"),
                                ("starting Mass", [r["mass"] for r in RUNGS], "up"),
                                ("starting Space", [r["space"] for r in RUNGS], "up")):
    pairs = list(zip(values, values[1:]))
    if direction == "up" and any(b < a for a, b in pairs) or direction == "down" and any(b > a for a, b in pairs):
        errors.append(f"the {name} ladder must run {direction} with intensity ({values})")
for r in RUNGS:
    for el in ("mass", "space"):
        if r[el] not in (0, 5):
            errors.append(f"I{r['i']}: a starting {el} of {r[el]} buys no L5 and is not the ladder's point")
if "  NucleusPrefab: {fileID: 0}\n" in g.read(BARREN_REL):
    errors.append("the Barren cell lost its nucleus - SPAWN_OUTSIDE assumes one")

if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("WarrenController", "WarrenTimeTurnMonitor")},
                     "WarrenSettings": G_ASSET["WarrenSettings"],
                     "WarrenScoringRule": G_ASSET["WarrenScoringRule"],
                     "Barren cell": EXISTING["BarrenCellConfig"]}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Glasshouse cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {MEERKAT_ID}\n") != 4 or f"  - vesselClass: {ANGLER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Meerkat AI templates")
    if f"  duration: {ROUND_SECONDS}\n" not in scene:
        errors.append(f"scene's round monitor does not author duration {ROUND_SECONDS}")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Warren"), guid(f"text/{AR}/WARREN.md")] + mk.shared_minted())

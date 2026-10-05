#!/usr/bin/env python3
"""
Authors every serialized asset the Sirocco game mode needs (GameModes.Sirocco = 61).

Sirocco is the Butterfly's SPACE game - an erosion race through Rampage's cactus forest. Space is
the dust capsule's LENGTH (60 -> 150), and on opposing mass the dust destroys one prism in three it
touches; first DOMAIN to the hostile-prism target wins on ScoringMetric.PrismsDestroyed (Rampage's
metric and rule, inherited). See Assets/_Scripts/Controller/Arcade/SIROCCO.md.

THE ARENA IS RAMPAGE'S, referenced read-only - the four cactus-forest configs The Bends and
Bloomrush reference too (the cell is per-arena, not per-mode). Spawn ring and crystal volume are
Rampage's own, read off The Bends' scene (Rampage's cell has a nucleus, so no noNucleusSpawnRadius).

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_sirocco_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import butterfly_games_common as common    # noqa: E402

MODE_ID = 61
g = lib.Generator(MODE_ID, "Sirocco")
guid = lib.guid

SCRIPT_PATHS = {
    "SiroccoController":       "Assets/_Scripts/Controller/Arcade/Sirocco/SiroccoController.cs",
    "SiroccoSettingsSO":       "Assets/_Scripts/Controller/Arcade/Sirocco/SiroccoSettingsSO.cs",
    "SiroccoScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Sirocco/SiroccoScoringRuleSO.cs",
    "SiroccoPrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/SiroccoPrismTurnMonitor.cs",
    "ButterflyAutopilotModeDriver": "Assets/_Scripts/Controller/Arcade/ButterflyGames/ButterflyAutopilotModeDriver.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameSirocco":       guid("asset/ArcadeGameSirocco"),
    "SiroccoScoringRule":      guid("asset/SiroccoScoringRule"),
    "SiroccoSettings":         guid("asset/SiroccoSettings"),
    "GameToastConfigSirocco":  guid("asset/GameToastConfigSirocco"),
    "ModePreviewSirocco":      guid("asset/ModePreviewSirocco"),
    "MinigameSirocco.unity":   guid("asset/MinigameSirocco.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Butterfly"] = lib.VESSELS["Butterfly"]
RAMPAGE_DIR = "Assets/_SO_Assets/Cell Configs/Rampage Cell"
RAMPAGE = [lib.existing_guid(f"{RAMPAGE_DIR}/Rampage Cell Config {i}.asset") for i in range(1, 5)]
for i, gd in enumerate(RAMPAGE, 1):
    EXISTING[f"RampageCellConfig{i}"] = gd
# The dust's prism effect - read, never edited. The mode rests on its opposing-mass destroy weight.
DUST_PRISM = "Assets/_SO_Assets/Effects/Skimmer Prism Effects/ButterflyScaleDustPrismEffect.asset"
EXISTING["ButterflyScaleDustPrismEffect"] = lib.existing_guid(DUST_PRISM)

# ── The race ─────────────────────────────────────────────────────────────────
PRISM_TARGET = 600    # kept in sync with EndConditionOverridesSO.DefaultSiroccoPrismTarget
# Comeback - a FUNCTION OF THE TARGET. A quarter behind (150 prisms) buys 1.5 element levels, and
# on this mode an element level is the dust itself: Space is the swath, Charge the bite.
COMEBACK_RATE = 0.01
SPAWN = common.spawn_block(500, 0, 0)   # Rampage's own ring - 500 outside its nucleus

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameSirocco.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Sirocco")
g.folder_meta("Assets/_Scripts/Controller/Arcade/ButterflyGames")
g.text_meta("Assets/_Scripts/Controller/Arcade/SIROCCO.md")

# ── 2. Scoring rule: metric 5 = PrismsDestroyed (Rampage's rule, inherited) ──
g.emit_asset("Assets/_SO_Assets/Scoring Rules/SiroccoScoringRule.asset", G_ASSET["SiroccoScoringRule"],
             lib.header_for(G_SCRIPT["SiroccoScoringRuleSO"], "SiroccoScoringRule") +
             "  metric: 5\n  golfRules: 1\n")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/SiroccoSettings.asset", G_ASSET["SiroccoSettings"],
             lib.header_for(G_SCRIPT["SiroccoSettingsSO"], "SiroccoSettings") + """  leadAnnounceFraction: 0.2
  progressSampleSeconds: 0.5
  aiRetargetSeconds: 4
  aiDustHover: 30
  aiModeRetrySeconds: 1
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 2: a race against the ARENA - a solo pilot has the whole forest to
# erode against the AI (the Rampage shape).
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameSirocco"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameSirocco") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Sirocco
  Description: Butterflies only, over Rampage's cactus forest. Flip to Dust mode and fly low -
    your scale dust shreds, shrinks or steals the other colours' plants and fortifies your own.
    Space lengthens the dust, so every Space crystal is a wider swath. First team to erode the
    target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {common.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameSirocco
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Butterfly']}, type: 2}}
  MinPlayersAllowed: 1
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Sirocco.asset", G_ASSET["GameToastConfigSirocco"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Sirocco") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has eroded {1} prisms", tint_domain=1, domain_names=0, every_n=100) +
             lib.toast(126, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(127, "Pull the right trigger for Dust mode and skim LOW over the forest", idle=1, idle_seconds=25) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigSirocco"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Sirocco.asset", G_ASSET["ModePreviewSirocco"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Sirocco") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Rampage''s cactus forest outright - referenced, not forked, exactly as the mode
    does; intensity is the forest''s density. What the preview teaches is Dust mode over the
    forest. Vessel pinned to Butterfly(13).'
  PreviewCell: {{fileID: 11400000, guid: {RAMPAGE[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(RAMPAGE)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {common.BUTTERFLY_ID}
  ObjectiveText: Skim low over the forest in Dust mode
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 500
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewSirocco"])

# ── 7. Scene ─────────────────────────────────────────────────────────────────
errors = []
scene = common.clone_or_stand_down(
    g, "MinigameSirocco", G_ASSET["MinigameSirocco.unity"], errors,
    lambda: common.clone_scene(G_SCRIPT["SiroccoController"], G_SCRIPT["SiroccoPrismTurnMonitor"],
                               G_ASSET["SiroccoSettings"], G_ASSET["SiroccoScoringRule"],
                               RAMPAGE, 1, SPAWN, 0))

# ── 8. Registries ────────────────────────────────────────────────────────────
build = g.read_current(lib.BUILD_SETTINGS)
after = next((s for s in ("MinigameTapestry", "MinigameDustup") if f"{s}.unity" in build), "MinigameWaystation")
g.register_arcade_card(G_ASSET["ArcadeGameSirocco"])
g.register_always_unlocked()
g.register_build_scene(after, "MinigameSirocco", G_ASSET["MinigameSirocco.unity"])
end = g.read_current(lib.END_CONDITIONS)
after_key = next((k for k in ("tapestryRoundSeconds", "dustupPointTarget") if f"{k}:" in end), "undertowPointTarget")
g.set_end_condition("siroccoPrismTarget", after=after_key, value=PRISM_TARGET)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The dust must still DESTROY opposing mass - it is the only way this mode scores.
dust = g.read(DUST_PRISM)
import re  # noqa: E402
m = re.search(r"^  destroyWeight: ([0-9.]+)$", dust, re.M)
if not m or float(m.group(1)) <= 0:
    errors.append("the Butterfly's dust no longer destroys opposing mass (destroyWeight <= 0) - nothing would score")

# Rampage's own spawn ring, borrowed with its cell.
bends = g.read(f"{lib.SCENES_DIR}/MinigameBends.unity")
if SPAWN not in bends:
    errors.append("the Rampage-arena spawn ring (read off The Bends) moved - update SPAWN")
if common.cell_list(RAMPAGE) not in bends:
    errors.append("The Bends no longer runs the four Rampage configs in this order")

# The forest must be big enough that a match ENDS with forest standing: the target has to sit well
# under the smallest rung's forest (intensity 4, 9,830 prisms - CLAUDE.md, Rampage), of which a
# domain's hostile share is about two thirds.
if PRISM_TARGET > 0.25 * 9830:
    errors.append("the target would strip most of intensity 4's forest - lower it")

common.assert_scene(errors, scene,
                    must_have={"SiroccoController": G_SCRIPT["SiroccoController"],
                               "SiroccoPrismTurnMonitor": G_SCRIPT["SiroccoPrismTurnMonitor"],
                               "SiroccoSettings": G_ASSET["SiroccoSettings"],
                               "SiroccoScoringRule": G_ASSET["SiroccoScoringRule"],
                               **{f"Rampage {i}": gd for i, gd in enumerate(RAMPAGE, 1)}},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}})

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Sirocco"),
                   guid("folder/Assets/_Scripts/Controller/Arcade/ButterflyGames"),
                   guid("text/Assets/_Scripts/Controller/Arcade/SIROCCO.md")])

#!/usr/bin/env python3
"""
Authors every serialized asset the Undertow game mode needs (GameModes.Undertow = 55).

Undertow is the Scarab-only CAVITATION DUEL - The Bends for the hull whose blast is a sideways
PLATE rather than a cone. Two to four Scarabs hunt each other through Wildlife Liberation's caged
arena; the juke dash's cavitation plate is the only weapon, and two things it does score: an
opposing pilot caught in it (a BEND - every element stripped for four seconds - 3 points) and a
creature whose heart it reaches (a KILL - 1 point). First DOMAIN to the point target wins. See
Assets/_Scripts/Controller/Arcade/UNDERTOW.md.

THE ARENA IS WILDLIFE LIBERATION'S, ON PURPOSE AND READ-ONLY. The mode wants exactly what that
cell authors: a very heavy swarm of small creatures, bigger ones, and the biggest and toughest
roaming one arena-wide band through three concentric cages the plate tears through - and its
four-rung intensity ladder. Referencing its four per-intensity cell configs (rather than forking
them) is the CLAUDE.md rule "the cell is per-arena, not per-mode" (Salvo in the Boneyard).

THE ONE EDIT THAT REACHES OUTSIDE THE MODE - and it is the load-bearing wiring for the whole
feature - is section 3: the Scarab's cavitation container gets a SCORING report for the debuff it
already lands, and a LIFEFORM-CRYSTAL effect so a creature caught in the plate dies through its
heart (the Sparrow warhead's kill, on a plate). Both land platform-wide; only this mode's rule PAYS
for either (PointsForCombatHit is 0 in every other rule; LifeformsKilled is folded into a domain
score only here and in Wildlife Liberation).

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_undertow_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 55
g = lib.Generator(MODE_ID, "Undertow")
guid = lib.guid

# ── New script GUIDs ─────────────────────────────────────────────────────────
G_SCRIPT = {
    "UndertowController":       guid("script/UndertowController"),
    "UndertowSettingsSO":       guid("script/UndertowSettingsSO"),
    "UndertowScoringRuleSO":    guid("script/UndertowScoringRuleSO"),
    "UndertowPointTurnMonitor": guid("script/UndertowPointTurnMonitor"),
}
SCRIPT_PATHS = {
    "UndertowController":       "Assets/_Scripts/Controller/Arcade/Undertow/UndertowController.cs",
    "UndertowSettingsSO":       "Assets/_Scripts/Controller/Arcade/Undertow/UndertowSettingsSO.cs",
    "UndertowScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Undertow/UndertowScoringRuleSO.cs",
    "UndertowPointTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/UndertowPointTurnMonitor.cs",
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameUndertow":          guid("asset/ArcadeGameUndertow"),
    "UndertowScoringRule":         guid("asset/UndertowScoringRule"),
    "UndertowSettings":            guid("asset/UndertowSettings"),
    "VesselCombatHitByCavitation": guid("asset/VesselCombatHitByCavitation"),
    "ScarabCavitationWitherLifeformEffect": guid("asset/ScarabCavitationWitherLifeformEffect"),
    "GameToastConfigUndertow":     guid("asset/GameToastConfigUndertow"),
    "ModePreviewUndertow":         guid("asset/ModePreviewUndertow"),
    "MinigameUndertow.unity":      guid("asset/MinigameUndertow.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Scarab"] = lib.VESSELS["Scarab"]
# the donor's (The Bends') mode-specific wiring - minted by author_bends_assets.py, so the
# same guid function reproduces it; asserted to resolve below like every other reference
EXISTING["BendsController"] = guid("script/BendsController")
EXISTING["BendsPointTurnMonitor"] = guid("script/BendsPointTurnMonitor")
EXISTING["BendsScoringRule"] = guid("asset/BendsScoringRule")
# the Rampage cells the donor scene carries, replaced by Wildlife Liberation's four
RAMPAGE_DIR = "Assets/_SO_Assets/Cell Configs/Rampage Cell"
WL_DIR = "Assets/_SO_Assets/Cell Configs/Wildlife Liberation Cell"
for i in range(1, 5):
    EXISTING[f"RampageCellConfig{i}"] = lib.existing_guid(f"{RAMPAGE_DIR}/Rampage Cell Config {i}.asset")
    EXISTING[f"WildlifeCellConfig{i}"] = lib.existing_guid(f"{WL_DIR}/Wildlife Liberation Cell Config {i}.asset")
# the plate's container and what it already carries (edited in place, never minted)
CONTAINER_PATH = ("Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/"
                  "ScarabCavitationExplosionImpactorDataContainer.asset")
EXISTING["ScarabCavitationDebuffEffect"] = lib.existing_guid(
    "Assets/_SO_Assets/Effects/Vessel Explosion Effects/ScarabCavitationDebuffByExplosionEffect.asset")
EXISTING["ScarabBallForgeEffect"] = lib.existing_guid(
    "Assets/_SO_Assets/Effects/Explosion Crystal Effects/ScarabBallForgeByExplosionEffect.asset")
# the SOAP channel a landed vessel-vs-vessel hit travels on
EXISTING["Event_CombatHitStats"] = lib.existing_guid("Assets/_SO_Assets/Event Channels/Event_CombatHitStats.asset")

# ── The race ─────────────────────────────────────────────────────────────────
# A BEND is 3, a KILL is 1 (UndertowScoringRuleSO), so 12 is four clean bends, twelve kills, or
# any mix. Kept in sync with EndConditionOverridesSO.DefaultUndertowPointTarget.
POINT_TARGET = 12
BEND_POINTS = 3
KILL_POINTS = 1

# Comeback - a FUNCTION OF THE TARGET. The comeback system reads the SCORE deficit
# (UndertowScoringRuleSO.DomainValue: bends x 3 + kills x 1, since 2026-09 - it used to read bends
# alone), so a quarter of the race behind (3 points, e.g. one bend) buys 1.5 element levels. The assert below is the gate this family keeps re-learning.
COMEBACK_RATE = 0.5

# Anti-double-count window - MUST match the plate's debuff effect (cooldown 1): the plate
# resolves its vessel contacts over several frames, so both effects need one per-victim window.
SAME_VICTIM_COOLDOWN = 1

# The donor scene's spawn ring is Rampage's (500 outside a nucleus this cell does not have -
# Wildlife Liberation's cell is NUCLEUS-LESS, so that ring would collapse to 500u, inside the
# middle cage). Wildlife Liberation's own ring is the one authored for this arena.
SPAWN_RING = dict(distance=40, floor=1150, formation=1)
# ...and a nucleus-less cell must author the omni-crystal respawn volume (the Dog Fight rule),
# or every crystal falls through to the arena's exact centre.
NO_NUCLEUS_SPAWN_RADIUS = 480

# ── 1. .cs.meta for the scripts, and the folder this mode adds ──────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Undertow")
g.text_meta("Assets/_Scripts/Controller/Arcade/UNDERTOW.md")

# ── 2. The two effects the plate gains ──────────────────────────────────────
# 2a. "an opposing pilot was caught in this plate" - hitClass 2 = CombatHitClass.Debuff, the
# same script The Bends stamped for the Dolphin's cone. requireDebuffableVictim: the score must
# follow the effect (a warded pilot takes no drain, so pays no point). requireOwningMachine: the
# plate exists on exactly one machine today (the local pilot's, or the host's for an AI), so this
# is belt-and-braces rather than a live fix - but it is what stops a future replay of the blast
# onto a second machine from double-crediting, which is precisely how the Dolphin's cone bit.
g.emit_asset("Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByCavitation.asset",
             G_ASSET["VesselCombatHitByCavitation"],
             lib.header_for(EXISTING["VesselCombatHitByExplosionEffectSO"], "VesselCombatHitByCavitation") +
             f"""  hitClass: 2
  onCombatHitLanded: {{fileID: 11400000, guid: {EXISTING['Event_CombatHitStats']}, type: 2}}
  sameVictimCooldownSeconds: {SAME_VICTIM_COOLDOWN}
  requireDebuffableVictim: 1
  requireOwningMachine: 1
""")
# 2b. "a creature's heart was in this plate" - the Sparrow warhead's kill, authored the same way:
# fauna only (the plate's prism half already grazes flora as mass), wildlife is quarry whatever
# colour it wears (fauna spawn in ONE colour, so sparing your own would switch off half the mode
# for whoever shares the swarm's colour - the Wildlife Liberation finding).
g.emit_asset("Assets/_SO_Assets/Effects/Explosion Crystal Effects/ScarabCavitationWitherLifeformEffect.asset",
             G_ASSET["ScarabCavitationWitherLifeformEffect"],
             lib.header_for(EXISTING["ExplosionWitherLifeformByCrystalEffectSO"], "ScarabCavitationWitherLifeformEffect") +
             "  faunaOnly: 1\n  sparesOwnDomain: 0\n  onLifeformJousted: {fileID: 0}\n")

# ── 3. Wire the plate's container - THE load-bearing edit ───────────────────
# Everything it already carried is re-emitted verbatim (the debuff, the ball forge) with the two
# new effects beside them. SCOPED TO THE SCARAB'S OWN CONTAINER: the same edit on a shared AOE
# prefab would label every vessel's blast a bend.
g.emit(CONTAINER_PATH,
       lib.header_for(EXISTING["ExplosionImpactorDataContainerSO"], "ScarabCavitationExplosionImpactorDataContainer") +
       f"""  vesselExplosionEffects:
  - {{fileID: 11400000, guid: {EXISTING['ScarabCavitationDebuffEffect']}, type: 2}}
  - {{fileID: 11400000, guid: {G_ASSET['VesselCombatHitByCavitation']}, type: 2}}
  explosionPrismEffects: []
  explosionCrystalEffects:
  - {{fileID: 11400000, guid: {EXISTING['ScarabBallForgeEffect']}, type: 2}}
  explosionLifeformCrystalEffects:
  - {{fileID: 11400000, guid: {G_ASSET['ScarabCavitationWitherLifeformEffect']}, type: 2}}
""")

# ── 4. Scoring rule ──────────────────────────────────────────────────────────
# metric 8 = ScoringMetric.CombatPoints (bends); kills are folded in by the rule's DomainValue.
g.emit_asset("Assets/_SO_Assets/Scoring Rules/UndertowScoringRule.asset", G_ASSET["UndertowScoringRule"],
             lib.header_for(G_SCRIPT["UndertowScoringRuleSO"], "UndertowScoringRule") +
             f"  metric: 8\n  golfRules: 1\n  bendPoints: {BEND_POINTS}\n  killPoints: {KILL_POINTS}\n  gunneryPoints: 0\n")

# ── 5. Mode settings ─────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/UndertowSettings.asset", G_ASSET["UndertowSettings"],
             lib.header_for(G_SCRIPT["UndertowSettingsSO"], "UndertowSettings") + """  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  aiRetargetSeconds: 1.25
  aiInterceptLeadSeconds: 0.6
  aiHumanFocus: 3
  aiDashRange: 60
  aiWildlifeDashRange: 50
  aiDashSampleSeconds: 0.4
""")

# ── 6. Arcade game config ────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: you cannot bend a teammate, so a solo lobby would have
# only the wildlife to score on, which is Wildlife Liberation's game and not this one.
g.emit_asset("Assets/_SO_Assets/Games/ArcadeGameUndertow.asset", G_ASSET["ArcadeGameUndertow"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameUndertow") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Undertow
  Description: Scarabs only, loose in the wildlife cages, with no guns anywhere. Dash beside
    a rival and the cavitation plate drags them through the undertow - every element they
    have stripped for four seconds. Drag the swarm through it too and the creatures die.
    A pilot is three points, a creature one; first team to the target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameUndertow
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Scarab']}, type: 2}}
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

# ── 7. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Undertow.asset", G_ASSET["GameToastConfigUndertow"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Undertow") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> dragged a rival through the undertow!", tint_domain=1, domain_names=0) +
             lib.toast(84, "<b>{0}</b> has drowned {1} creatures", tint_domain=1, domain_names=0, every_n=3) +
             lib.toast(95, "{0} is a quarter of the way to {2}", tint_domain=1, domain_names=0) +
             lib.toast(96, "{0} is halfway to {2} - {1} points", tint_domain=1, domain_names=0) +
             lib.toast(97, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(98, "Flick the right stick beside a rival - the plate takes them, and everything behind you", idle=1, idle_seconds=30) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigUndertow"])

# ── 8. Mode preview ──────────────────────────────────────────────────────────
PREVIEW_CELLS = "".join(f"  - {{fileID: 11400000, guid: {EXISTING[f'WildlifeCellConfig{i}']}, type: 2}}\n" for i in range(1, 5))
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Undertow.asset", G_ASSET["ModePreviewUndertow"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Undertow") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Wildlife Liberation''s arena outright - referenced, not forked, exactly as
    the mode itself does. OPEN-ENDED: it scores on debuffing rival pilots, and a solo preview
    has none; what it teaches is the plate against the swarm, which is the other half of the
    score. Vessel pinned to Scarab(12).'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['WildlifeCellConfig1']}, type: 2}}
  PreviewCellsByIntensity:
{PREVIEW_CELLS}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: 12
  ObjectiveText: Dash beside a rival
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: {SPAWN_RING['distance']}
  SpawnRingRadiusFloor: {SPAWN_RING['floor']}
  SpawnFormation: {SPAWN_RING['formation']}
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewUndertow"])

# ── 9. Scene: clone MinigameBends, swap the mode-specific wiring ─────────────
# The Bends is the donor because its controller block is the one this controller inherits
# (rule + arena cell + milestone sampler); what changes is the identity, the CELL (Rampage's
# forest -> Wildlife Liberation's cages), the AI hull, the spawn ring and the crystal volume.
scene = g.read(f"{lib.SCENES_DIR}/MinigameBends.unity")
scene = lib.swap_guid(scene, EXISTING["BendsPointTurnMonitor"], G_SCRIPT["UndertowPointTurnMonitor"], "turn monitor")
scene = lib.swap_guid(scene, EXISTING["BendsController"], G_SCRIPT["UndertowController"], "controller")

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['BendsScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  aiAimRetargetSeconds: 1.25
  aiAimLeadSeconds: 0.35
  aiAimBlastReach: 2400
  aiAimBlastDuration: 2.7
  aiAimHumanFocus: 3
  aiAimMaxRange: 2400
"""
NEW_FIELDS = f"""  settings: {{fileID: 11400000, guid: {G_ASSET['UndertowSettings']}, type: 2}}
  rule: {{fileID: 11400000, guid: {G_ASSET['UndertowScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
"""
scene = lib.replace_block(scene, OLD_FIELDS, NEW_FIELDS, "controller field block")

# THE CELL: Rampage's four forest configs -> Wildlife Liberation's four caged ones.
scene = lib.replace_block(scene,
    "  CellConfigs:\n" + "".join(f"  - {{fileID: 11400000, guid: {EXISTING[f'RampageCellConfig{i}']}, type: 2}}\n" for i in range(1, 5)),
    "  CellConfigs:\n" + "".join(f"  - {{fileID: 11400000, guid: {EXISTING[f'WildlifeCellConfig{i}']}, type: 2}}\n" for i in range(1, 5)),
    "cell config list")

# THE AI HULL: the donor's four Dolphin templates become Scarabs (the clamp would do it anyway;
# the scene should say what it means).
scene = lib.swap_guid(scene, "  - vesselClass: 2\n    PlayerName: AI", "  - vesselClass: 12\n    PlayerName: AI", "AI templates", expected=4)

# THE SPAWN RING and the CRYSTAL VOLUME, both for a nucleus-less cell (see the constants).
scene = lib.replace_block(scene,
    "  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 500\n  spawnRingRadiusFloor: 0\n  spawnFormation: 0\n",
    f"  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: {SPAWN_RING['distance']}\n"
    f"  spawnRingRadiusFloor: {SPAWN_RING['floor']}\n  spawnFormation: {SPAWN_RING['formation']}\n",
    "spawn ring")
scene = lib.replace_block(scene, "  noNucleusSpawnRadius: 0\n  crystalCountMode: 2\n",
                          f"  noNucleusSpawnRadius: {NO_NUCLEUS_SPAWN_RADIUS}\n  crystalCountMode: 2\n", "crystal volume")
g.emit_scene("MinigameUndertow", G_ASSET["MinigameUndertow.unity"], scene)

# ── 10-13. The shared registries ─────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameUndertow"])
g.register_always_unlocked()
g.register_build_scene("MinigameBends", "MinigameUndertow", G_ASSET["MinigameUndertow.unity"])
# after Wrecking Ball's key when that generator has run, else after Tollway's - either way the
# asset ends up carrying both, in the order the C# declares them
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("undertowPointTarget",
                    after="wreckingBallPrismTarget" if "wreckingBallPrismTarget:" in end else "tollwayTollTarget",
                    value=POINT_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []
if 0.25 * POINT_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {POINT_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * POINT_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
if BEND_POINTS < 1 or KILL_POINTS < 1:
    errors.append("a bend or a kill is worth nothing - half the mode would score nothing")
if BEND_POINTS <= KILL_POINTS:
    errors.append("a pilot must be worth more than a creature, or the wildlife decides the duel")

sc = g.files[f"{lib.SCENES_DIR}/MinigameUndertow.unity"]
for name in ("BendsController", "BendsPointTurnMonitor", "BendsScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for i in range(1, 5):
    if EXISTING[f"RampageCellConfig{i}"] in sc:
        errors.append(f"cloned scene still references Rampage cell config {i}")
    if EXISTING[f"WildlifeCellConfig{i}"] not in sc:
        errors.append(f"cloned scene missing Wildlife Liberation cell config {i}")
for name in ("UndertowController", "UndertowPointTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if sc.count("  - vesselClass: 12\n") != 4 or "  - vesselClass: 2\n" in sc:
    errors.append("cloned scene does not carry exactly 4 Scarab AI templates")

cont = g.files[CONTAINER_PATH]
for name, gd in (("cavitation debuff", EXISTING["ScarabCavitationDebuffEffect"]),
                 ("combat-hit report", G_ASSET["VesselCombatHitByCavitation"]),
                 ("ball forge", EXISTING["ScarabBallForgeEffect"]),
                 ("wither-lifeform kill", G_ASSET["ScarabCavitationWitherLifeformEffect"])):
    if gd not in cont:
        errors.append(f"cavitation container missing the {name}")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Undertow"), guid("text/Assets/_Scripts/Controller/Arcade/UNDERTOW.md")])

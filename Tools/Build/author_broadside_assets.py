#!/usr/bin/env python3
"""
Author every serialized asset Broadside (GameModes.Broadside = 57) adds - the ARENA brawl.

The mode itself is small; what is NOT small is that it has to ARM three hulls before a
mixed-fleet brawl can be scored at all. Measured on the shipped containers, only four of the
eight playable hulls could land a scoreable hit (Sparrow, Manta, Dolphin, Scarab), so this
script's load-bearing edits are three CONTAINER wirings, each of which lands platform-wide and
is PAID only by this mode - the split Dog Fight established:

  * the Urchin's spike container had `projectileShipEffects: []` - a spike passed straight
    through a rival pilot and did nothing at all;
  * the Rhino's sword already DAMAGED and SPUN a rival and reported nothing;
  * the Squirrel's joust already exploded and scored a joust point and reported nothing.

The Serpent is deliberately absent from the card: it has no anti-vessel verb authored (0/4
abilities), so listing it would seat a pilot who cannot score.

The numbers come from Tools/Build/broadside_balance.py, which is IMPORTED rather than
transcribed - the price list and the per-asset latch windows are one system (a window is what
bounds a verb's rate against one victim), so a model that owned only half of it would be
describing a different mode.

Run:  python3 Tools/Build/author_broadside_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib          # noqa: E402
import broadside_balance as bal        # noqa: E402

MODE_ID = 57
g = lib.Generator(MODE_ID, "Broadside")
guid = lib.guid

# ── New script GUIDs ─────────────────────────────────────────────────────────
G_SCRIPT = {
    "BroadsideController":       guid("script/BroadsideController"),
    "BroadsideSettingsSO":       guid("script/BroadsideSettingsSO"),
    "BroadsideScoringRuleSO":    guid("script/BroadsideScoringRuleSO"),
    "BroadsidePointTurnMonitor": guid("script/BroadsidePointTurnMonitor"),
    "VesselCombatHitBySkimmerEffectSO": guid("script/VesselCombatHitBySkimmerEffectSO"),
}
SCRIPT_PATHS = {
    "BroadsideController":       "Assets/_Scripts/Controller/Arcade/Broadside/BroadsideController.cs",
    "BroadsideSettingsSO":       "Assets/_Scripts/Controller/Arcade/Broadside/BroadsideSettingsSO.cs",
    "BroadsideScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Broadside/BroadsideScoringRuleSO.cs",
    "BroadsidePointTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/BroadsidePointTurnMonitor.cs",
    "VesselCombatHitBySkimmerEffectSO":
        "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselCombatHitBySkimmerEffectSO.cs",
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameBroadside":        guid("asset/ArcadeGameBroadside"),
    "BroadsideScoringRule":       guid("asset/BroadsideScoringRule"),
    "BroadsideSettings":          guid("asset/BroadsideSettings"),
    "VesselCombatHitBySword":     guid("asset/VesselCombatHitBySword"),
    "VesselCombatHitByJoust":     guid("asset/VesselCombatHitByJoust"),
    "VesselCombatHitBySpike":     guid("asset/VesselCombatHitBySpike"),
    "GameToastConfigBroadside":   guid("asset/GameToastConfigBroadside"),
    "ModePreviewBroadside":       guid("asset/ModePreviewBroadside"),
    "MinigameBroadside.unity":    guid("asset/MinigameBroadside.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
for hull in ("Manta", "Dolphin", "Rhino", "Urchin", "Squirrel", "Sparrow", "Scarab"):
    EXISTING[f"Vessel_{hull}"] = lib.VESSELS[hull]

# The donor (Dog Fight) - its scene is the closest sibling: same metric, same arena, same
# HasEndGame shape. Read off disk rather than re-derived, because Dog Fight predates the lib.
EXISTING["DogFightController"] = lib.existing_guid(
    "Assets/_Scripts/Controller/Arcade/DogFightController.cs")
EXISTING["DogFightPointTurnMonitor"] = lib.existing_guid(
    "Assets/_Scripts/Controller/Arcade/TurnMonitors/DogFightPointTurnMonitor.cs")
EXISTING["DogFightScoringRule"] = lib.existing_guid(
    "Assets/_SO_Assets/Scoring Rules/DogFightScoringRule.asset")

# The effect SCRIPTS the new assets instance.
EXISTING["VesselCombatHitByProjectileEffectSO"] = lib.existing_guid(
    "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Projectile Effects/"
    "VesselCombatHitByProjectileEffectSO.cs")

# The SOAP channel a landed vessel-vs-vessel hit travels on.
EXISTING["Event_CombatHitStats"] = lib.existing_guid(
    "Assets/_SO_Assets/Event Channels/Event_CombatHitStats.asset")

# The three containers this mode edits IN PLACE, and everything they already carry - read, so a
# re-run can never drop an effect somebody else added.
CONTAINERS = {
    "urchin": "Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/"
              "UrchinSpikeProjectileImpactContainer.asset",
    "rhino":  "Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/"
              "RhinoForceFieldSkimmerImpactorDataContainer.asset",
    "squirrel": "Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/"
                "SquirrelSkimmerImpactorDataContainer.asset",
}

# The Boneyard, referenced not forked (the cell is per-ARENA, not per-mode - Salvo does the same).
BONEYARD = ["cc21823da49bd38535c54796c4a1c168", "64160342670283d2a0c5088f1951295c",
            "7dc0173b8f31a743221e8f0bd3ec2d40", "5511269da8da60affaf693e51b8a60a1"]

# The spawn ring is the Boneyard's, inherited from the donor scene unchanged. Named here so the
# PREVIEW can be authored with the same numbers - author_preview_spawns.py asserts the two
# agree, and a preview that spawns somewhere the scene does not is a preview of another mode.
SPAWN_RING = dict(distance=40, floor=700, formation=0)

# ── The race - every number from the model ───────────────────────────────────
POINT_TARGET = bal.read_target()
P = bal.POINTS
W = bal.NEW_WINDOWS
SHIPPED_W = bal.read_shipped_windows()

# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). This family has re-learned
# that seven times; the assert below is the gate, and the EIGHTH outing was this mode's own
# first playtest: the target went 600 -> 100 per pilot and 0.02 silently became worth half an
# element level at a quarter-of-target deficit. 0.12 restores the ~3 levels the 600 target
# bought. Sized against the SOLO target (the smallest the team-size rule can produce), so a
# fuller lobby races to a bigger number and the comeback only ever buys more, never less.
COMEBACK_RATE = 0.12

# The card's per-hull handicap, solved by the model. Time is the only element that reaches a
# speed on more than one hull, so it is the axis; three hulls it cannot reach get no row and the
# residual is REPORTED rather than faked (see BROADSIDE.md).
LEVELS = bal.solve_levels()

g.folder_meta("Assets/_Scripts/Controller/Arcade/Broadside")
g.text_meta("Assets/_Scripts/Controller/Arcade/BROADSIDE.md")
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])

# ── 1. The four new effect assets ───────────────────────────────────────────
def skimmer_hit(name, gd, window, require_faster):
    g.emit_asset(f"Assets/_SO_Assets/Effects/Vessel Skimmer Effects/{name}.asset", gd,
                 lib.header_for(G_SCRIPT["VesselCombatHitBySkimmerEffectSO"], name) +
                 f"""  hitClass: 5
  onCombatHitLanded: {{fileID: 11400000, guid: {EXISTING['Event_CombatHitStats']}, type: 2}}
  sameVictimCooldownSeconds: {lib.num(window)}
  requireOwningMachine: 1
  requireFasterThanVictim: {require_faster}
""")

# The Rhino's SWORD: a swung blade is in contact continuously while it is alongside, so it takes
# the longer window - AND, since the first playtest, it requires being FASTER than its victim,
# the Squirrel joust's own rule. It shipped at 0 on the reasoning that "a sword connects on its
# own terms", and what that actually bought was a mode that paid the Rhino to PARK: holding the
# blade alongside a rival paid 8 points every 1.4 s for nothing but station-keeping, while
# charging paid 8 points and left you 1200 units away. So the card was rewarding the exact
# opposite of the hull's identity, which is what came back as "I played rhino and was not
# charging full speed and straight to be a crazy fast and scary menace like he should have".
# Requiring speed does not make the Rhino fast; it makes being fast the only way it scores.
skimmer_hit("VesselCombatHitBySword", G_ASSET["VesselCombatHitBySword"], W["strike_sword"], 1)
# The Squirrel's JOUST: discrete by definition - it must re-earn a fresh overtake each time, so
# it takes the shorter window and DOES require being faster. That is the mode honouring what a
# joust already means rather than inventing a second meaning for the same contact.
skimmer_hit("VesselCombatHitByJoust", G_ASSET["VesselCombatHitByJoust"], W["strike_joust"], 1)

# The Urchin's SPIKE: a Bullet-class round with its own, longer window, because a volley is ~10
# projectiles arriving inside ~0.1 s and the Sparrow's 0.05 s would let one volley score ten times.
g.emit_asset("Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitBySpike.asset",
             G_ASSET["VesselCombatHitBySpike"],
             lib.header_for(EXISTING["VesselCombatHitByProjectileEffectSO"], "VesselCombatHitBySpike") +
             f"""  vesselTypesToImpact: []
  hitClass: 0
  onCombatHitLanded: {{fileID: 11400000, guid: {EXISTING['Event_CombatHitStats']}, type: 2}}
  sameVictimCooldownSeconds: {lib.num(W['spike'])}
""")

# A FELT effect used to ride along with the score: the spike SPUN its victim, on the reasoning
# that "a spike that pays a point and does nothing visible is a score with no cause on screen".
# That effect is REMOVED (Sep 2026) and must not come back from here — **A VESSEL MAY NOT MOVE AN
# OPPOSING VESSEL**: being shoved and re-aimed by somebody else's weapon is the one hit a pilot
# cannot answer with flying. What a hit may take is the victim's ELEMENTAL CRYSTALS, which the
# Urchin's spike does through `Docs/ELEMENTAL_ECONOMY.md`'s ejector — so the score still has a
# cause on screen, and it is crystals leaving the hull rather than the hull being thrown.

# ── 2. Wire the three containers - THE load-bearing edits ───────────────────
def add_to_list(path, list_key, next_key, new_guids, label):
    """Append entries to one serialized list, preserving everything already in it."""
    text = g.read_current(path)
    assert f"  {list_key}:" in text, f"{label}: '{list_key}' not found"
    start = text.index(f"  {list_key}:")
    end = text.index(f"  {next_key}:", start)
    block, tail = text[start:end], text[end:]
    block = block.replace(f"  {list_key}: []\n", f"  {list_key}:\n")
    for gd in new_guids:
        if gd not in block:
            block += f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n"
    g.emit(path, text[:start] + block + tail)

# The Urchin: projectileShipEffects was EMPTY - a spike did nothing to a pilot at all.
add_to_list(CONTAINERS["urchin"], "projectileShipEffects", "projectilePrismEffects",
            [G_ASSET["VesselCombatHitBySpike"]],
            "urchin spike container")
# The Rhino: already haptics + damage + spin + danger-block. Gains only the report.
add_to_list(CONTAINERS["rhino"], "vesselSkimmerEffectsSO", "skimmerPrismEffectsSO",
            [G_ASSET["VesselCombatHitBySword"]], "rhino sword container")
# The Squirrel: already the joust explosion + the teammate overtake buff. Gains only the report.
add_to_list(CONTAINERS["squirrel"], "vesselSkimmerEffectsSO", "skimmerPrismEffectsSO",
            [G_ASSET["VesselCombatHitByJoust"]], "squirrel joust container")

# ── 3. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/BroadsideScoringRule.asset",
             G_ASSET["BroadsideScoringRule"],
             lib.header_for(G_SCRIPT["BroadsideScoringRuleSO"], "BroadsideScoringRule") +
             f"""  metric: 8
  golfRules: 1
  bulletPoints: {P['bullet']}
  strikePoints: {P['strike']}
  debuffPoints: {P['debuff']}
  missileShockwavePoints: {P['missile_shockwave']}
  missileBlastPoints: {P['missile_blast']}
  missileDirectPoints: {P['missile_direct']}
""")

# ── 4. Mode settings ─────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/BroadsideSettings.asset", G_ASSET["BroadsideSettings"],
             lib.header_for(G_SCRIPT["BroadsideSettingsSO"], "BroadsideSettings") +
             """  progressSampleSeconds: 0.5
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  aiRetargetSeconds: 2
  aiHumanFocus: 3
  aiFireSampleSeconds: 0.6
  aiPlateRange: 160
  aiSpikeRange: 400
  aiSpikeTapSeconds: 0.08
""")

# ── 5. The ARENA card ────────────────────────────────────────────────────────
VESSEL_ROWS = "".join(
    f"  - {{fileID: 11400000, guid: {EXISTING[f'Vessel_{h}']}, type: 2}}\n"
    for h in ("Manta", "Dolphin", "Rhino", "Urchin", "Squirrel", "Sparrow", "Scarab"))

# A row per (hull, intensity) the model moves off rest. A hull at rest gets NO row - the platform
# default IS rest, and a row of zeros would claim the card had made a decision it did not make.
def starting_elements():
    rows = ""
    for hull, lvl in sorted(LEVELS.items()):
        if abs(lvl) < 1e-6:
            continue
        cid = lib.VESSEL_CLASS_ID[hull]
        for intensity in range(1, 5):
            rows += (f"  - Class: {cid}\n    Intensity: {intensity}\n    Levels:\n"
                     f"      Mass: 0\n      Charge: 0\n      Space: 0\n      Time: {lib.num(lvl)}\n")
    return rows

g.emit_asset("Assets/_SO_Assets/Games/ArcadeGameBroadside.asset", G_ASSET["ArcadeGameBroadside"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameBroadside") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Broadside
  Description: Every hull that can fight, loose in the Boneyard, each with the weapon it
    actually has - a Sparrow's guns and rockets, an Urchin's spikes, a Rhino's sword, a
    Squirrel's joust, a Dolphin's cone, a Scarab's plate, a Manta's bloom. A hit is worth
    what it cost you to land, not what you flew: a round is 1, a contact strike 8, a debuff
    12, a rocket up to 30. First team to the target wins. Pick the hull you fight best.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameBroadside
  Vessels:
{VESSEL_ROWS}  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  Tips:
  - Your hull already has a weapon. The card does not hand you one - it pays for the one you brought.
  - A contact strike is worth eight rounds. Closing is the expensive part, so it pays like it.
  - You cannot hit a teammate with anything here. The domains are the sides.
  - Slow hulls start with Time already high. Fast ones start with it low.
  ViewUserAction: 0
  PlayUserAction: 0
  StartingElements:
{starting_elements()}  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 6. Toasts - per VERB, never per hull ────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Broadside.asset",
             G_ASSET["GameToastConfigBroadside"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Broadside") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> landed a hit!", tint_domain=1, domain_names=0) +
             lib.toast(112, "{0} is a quarter of the way to {2}", tint_domain=1, domain_names=0) +
             lib.toast(113, "{0} is halfway to {2} - {1} points", tint_domain=1, domain_names=0) +
             lib.toast(114, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(115, "Use the weapon your hull already has - every one of them scores here",
                       idle=1, idle_seconds=30) +
             lib.toast(116, "A contact strike pays eight times a round. Get close.",
                       idle=1, idle_seconds=45) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigBroadside"])

# ── 7. Mode preview ──────────────────────────────────────────────────────────
PREVIEW_CELLS = "".join(f"  - {{fileID: 11400000, guid: {c}, type: 2}}\n" for c in BONEYARD)
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Broadside.asset",
             G_ASSET["ModePreviewBroadside"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Broadside") +
             f"""  Mode: {MODE_ID}
  Notes: 'Reuses Dog Fight''s Boneyard outright - referenced, not forked, exactly as the mode
    does. Vessel is -1 (ANY): this is an ARENA card, so the carousel''s pick flies the
    preview, which is the whole point of previewing a brawl you choose a hull for.'
  PreviewCell: {{fileID: 11400000, guid: {BONEYARD[0]}, type: 2}}
  PreviewCellsByIntensity:
{PREVIEW_CELLS}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: -1
  ObjectiveText: Fight with what you brought
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: {SPAWN_RING['distance']}
  SpawnRingRadiusFloor: {SPAWN_RING['floor']}
  SpawnFormation: {SPAWN_RING['formation']}
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewBroadside"])

# ── 8. Scene: clone MinigameDogFight ────────────────────────────────────────
scene = g.read(f"{lib.SCENES_DIR}/MinigameDogFight.unity")
scene = lib.swap_guid(scene, EXISTING["DogFightPointTurnMonitor"],
                      G_SCRIPT["BroadsidePointTurnMonitor"], "turn monitor")
scene = lib.swap_guid(scene, EXISTING["DogFightController"],
                      G_SCRIPT["BroadsideController"], "controller")

OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['DogFightScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  elementalCrystalCount: 14
  crystalScatterRadius: 400
  crystalScatterSeed: 41
  aiRetargetSeconds: 1.5
  aiLeadSeconds: 0.6
  aiBreakOffDistance: 120
  aiExtendDistanceMultiplier: 3
  aiMaxExtendSeconds: 4
"""
NEW_FIELDS = f"""  settings: {{fileID: 11400000, guid: {G_ASSET['BroadsideSettings']}, type: 2}}
  rule: {{fileID: 11400000, guid: {G_ASSET['BroadsideScoringRule']}, type: 2}}
  arenaCell: {{fileID: 1700000065}}
"""
scene = lib.replace_block(scene, OLD_FIELDS, NEW_FIELDS, "controller field block")

# THE AI HULL: Dog Fight's four Sparrow templates become RANDOM(0), so PickAIVesselType draws
# each bot's hull from the CARD and a bot grid is a mixed grid too. That is the arena rule -
# a brawl whose bots are all one hull is not the mode.
scene = lib.swap_guid(scene, "  - vesselClass: 11\n", "  - vesselClass: 0\n", "AI templates", expected=4)

g.emit_scene("MinigameBroadside", G_ASSET["MinigameBroadside.unity"], scene)

# ── 9. The shared registries ─────────────────────────────────────────────────
g.register_arena_card(G_ASSET["ArcadeGameBroadside"])      # master + ARENA grid, never Arcade
g.register_always_unlocked()
g.register_build_scene("MinigameDogFight", "MinigameBroadside", G_ASSET["MinigameBroadside.unity"])
end = g.read_current(lib.END_CONDITIONS)
# The key is PER PILOT since the playtest - the turn monitor multiplies it by the live team
# size. The value authored here is therefore what a SOLO domain races to, not a match total.
g.set_end_condition("broadsidePointsPerPilot",
                    after="undertowPointTarget" if "undertowPointTarget:" in end else "wreckingBallPrismTarget",
                    value=bal.read_target(1))

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

# The comeback trap, seven modes deep.
if 0.25 * POINT_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {POINT_TARGET}: a "
                  f"quarter-of-target deficit buys "
                  f"{0.25 * POINT_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The price list has to be ORDERED by what a verb costs to land, or the mode is saying something
# it does not mean.
if not (P["bullet"] < P["strike"] < P["debuff"] < P["missile_direct"]):
    errors.append("price list out of order: a round must be cheaper than a strike, a strike "
                  "than a debuff, and a debuff than a centre-punched rocket")

# EVERY hull on the card must be able to score. This is the arena rule, and it is the one that
# would have shipped a seat nobody can play.
CARD_HULLS = ("Manta", "Dolphin", "Rhino", "Urchin", "Squirrel", "Sparrow", "Scarab")
if "Serpent" in CARD_HULLS:
    errors.append("the Serpent has no anti-vessel verb authored - it cannot score here")
for h in CARD_HULLS:
    if h not in bal.HULLS:
        errors.append(f"{h} is on the card but the balance model does not price its verbs")

# The model's own claim, asserted rather than trusted.
r = bal.solve(LEVELS)
tuned_spread = bal.spread(r["tuned"])
if tuned_spread > 1.6:
    errors.append(f"tuned points/min spread {tuned_spread:.2f}x exceeds the 1.6x the doc claims")
# WHAT THIS GATE CAN AND CANNOT SAY, after the first playtest corrected it.
#
# The model's points/min is a rate against a victim you are ALREADY engaged with, thinned by a
# connect fraction that is an estimate. It does not model the time a brawl spends searching and
# repositioning BETWEEN engagements, so its minutes are a FLOOR on match length, not a
# prediction of one - and the playtest proved the gap is large, because a human who played a
# 600-point match came back asking for 100. The old two-sided 2-8 min gate was therefore
# asserting a number the model is not entitled to, and it is now a CEILING only: if even at full
# engagement a hull cannot reach the target, the target is unreachable and that IS decidable
# here. The floor is dropped with its reason stated rather than widened until it passes.
FULL_ENGAGEMENT_CEILING_MIN = 8.0
for h, ppm in r["tuned"].items():
    minutes = POINT_TARGET / ppm
    if minutes > FULL_ENGAGEMENT_CEILING_MIN:
        errors.append(f"{h} cannot reach the solo target ({POINT_TARGET}) inside "
                      f"{FULL_ENGAGEMENT_CEILING_MIN:.0f} min even at FULL engagement "
                      f"({minutes:.1f} min) - the target is out of reach for this hull")

# The windows must not have drifted from what the model priced against.
for key, shipped in (("bullet", SHIPPED_W["bullet"]), ("debuff", SHIPPED_W["debuff"])):
    if shipped <= 0:
        errors.append(f"shipped {key} latch window is {shipped} - the latch is disabled, so one "
                      f"weapon would score every frame it touched a victim")
if W["strike_sword"] <= W["strike_joust"]:
    errors.append("the sword's window must be LONGER than the joust's - a swept blade is in "
                  "contact continuously where a joust must re-earn a fresh overtake")

# The three container edits actually landed, and kept what was already there.
urchin = g.files[CONTAINERS["urchin"]]
if "projectileShipEffects: []" in urchin:
    errors.append("urchin spike container still has an EMPTY projectileShipEffects - a spike "
                  "would still pass through a pilot doing nothing")
if G_ASSET["VesselCombatHitBySpike"] not in urchin:
    errors.append("urchin spike container missing the spike combat-hit report")
rhino = g.files[CONTAINERS["rhino"]]
if G_ASSET["VesselCombatHitBySword"] not in rhino:
    errors.append("rhino sword container missing the combat-hit report")
for keep in ("02cd4a20ea91bbc4591c9ccbf9db91af", "c57e976ae0b3f0749895f17697cac036"):
    if keep not in rhino:
        errors.append("rhino sword container LOST an effect it already carried")
squirrel = g.files[CONTAINERS["squirrel"]]
if G_ASSET["VesselCombatHitByJoust"] not in squirrel:
    errors.append("squirrel container missing the combat-hit report")
if "c2570a8d81db3ab4eb0ba30f184e081f" not in squirrel:
    errors.append("squirrel container LOST the joust explosion effect")

# The scene really is Broadside's now.
sc = g.files[f"{lib.SCENES_DIR}/MinigameBroadside.unity"]
for name in ("DogFightController", "DogFightPointTurnMonitor", "DogFightScoringRule"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("BroadsideController", "BroadsidePointTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if sc.count("  - vesselClass: 0\n") != 4 or "  - vesselClass: 11\n" in sc:
    errors.append("cloned scene does not carry exactly 4 RANDOM-hull AI templates")
for c in BONEYARD:
    if c not in sc:
        errors.append("cloned scene lost a Boneyard cell config")

# The card is an ARENA card and lists every hull the model priced.
card = g.files["Assets/_SO_Assets/Games/ArcadeGameBroadside.asset"]
for h in CARD_HULLS:
    if EXISTING[f"Vessel_{h}"] not in card:
        errors.append(f"card missing {h}")
if lib.VESSELS["Serpent"] in card:
    errors.append("card lists the Serpent, which cannot score")
arena = g.files.get(lib.ARENA_GRID, "")
if G_ASSET["ArcadeGameBroadside"] not in arena:
    errors.append("card is not on the ARENA grid")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Broadside"),
                   guid("text/Assets/_Scripts/Controller/Arcade/BROADSIDE.md")])

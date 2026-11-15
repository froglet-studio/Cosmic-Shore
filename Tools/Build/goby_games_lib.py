#!/usr/bin/env python3
"""
The shared half of the Goby & Pistol's three element games (Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 24-26):
Snap (Charge, 114), Den Builder (Mass, 115) and Riptide (Space, 116). Each game's own generator
(author_snap_assets.py, author_den_builder_assets.py, author_riptide_assets.py) describes itself in a Spec and calls
author(spec); this file authors what every one of them needs the same way, through arcade_mode_lib:

  - the .cs.meta of its scripts and the folder / doc metas,
  - its scoring rule, arcade card (Goby only), toast config and mode preview,
  - its scene, CLONED from the nearest shipped sibling of its element (Kelpwood, Glasshouse, Shatterplane) with the
    sibling's own CELLS kept - the game references that arena rather than forking it (/arcadegame section 1) - and the
    controller, monitor and rule swapped, the donor's settings / arena-cell fields replaced, and the four AI templates
    turned into Gobies,
  - the registrations (master roster + Arcade grid, always-unlocked, build list, end condition, toast library, preview
    library),
  - and the checks that hold the premise: the pair's kit still does what the game scores (GobyPistolDuo), the ids are
    the roster's, the comeback is alive, the clone carries no donor identity.

NOTHING ABOUT THE PAIR CHANGES (the LOCKED vessel rule, Shombith 2026-10-31): no file here edits the Goby, the Pistol,
their config or prefab. No mode AI either: an AI pair is its seat's own crew (GobyPistolDuo's Goby AI and Pistol AI),
which already plays every one of these games.
"""
import os
import re
import sys
from dataclasses import dataclass, field

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

guid = lib.guid
AR = "Assets/_Scripts/Controller/Arcade"
DUO = "Assets/_Scripts/Controller/Vessel/Crew/GobyPistol/GobyPistolDuo.cs"
GOBY_CONFIG = "Assets/_SO_Assets/VesselActions/GobyPistol/GobyPistolConfig.asset"
GOBY_ID = lib.VESSEL_CLASS_ID["Goby"]
VESSEL_GOBY = "b569f215d5ca452d9e4ab28af3ee55ab"     # SO_Class_Goby.asset
ROSTER = "Docs/ElementalLoop/GAME_KIT_ROSTER.md"

# The shared race beats every game posts the same way (DomainRaceToasts, or the controller's own lead sampler).
RACE_BEATS = (128, 129, 130)


@dataclass
class Spec:
    mode_id: int
    name: str                 # the GameModes member and the asset stem: "Snap", "DenBuilder", "Riptide"
    display: str              # the card's DisplayName
    doc: str                  # the mode's doc, under Assets/_Scripts/Controller/Arcade/
    toast_hint: int
    element: str              # the roster row's element, for the checks
    donor: str                # the sibling whose scene (and cells) this game clones: "Kelpwood", "Glasshouse", ...
    donor_monitor: str        # the donor's turn monitor class
    donor_vessel_id: int      # the hull class the donor's four AI templates fly
    monitor: str              # this game's turn monitor class
    metric: int               # ScoringMetric
    golf: int
    rule_fields: str          # the rule asset's own fields (below metric / golfRules)
    end_key: str              # EndConditionOverridesSO field
    end_value: int
    comeback_rate: float
    comeback_reference: float  # a quarter of THIS behind must buy at least one element level
    min_players: int
    min_domains: int
    description: str
    toasts: str               # the toast entries (lib.toast lines)
    preview_notes: str
    objective_text: str
    controller_fields: str = ""   # serialized controller fields the scene authors below `rule:`
    monitor_lines: str = ""       # extra serialized monitor lines (a timed monitor's duration)
    donor_monitor_lines: str = ""
    kit_needles: list = field(default_factory=list)   # (needle, why) pairs GobyPistolDuo must still contain
    extra_errors: list = field(default_factory=list)


def author(spec: Spec):
    g = lib.Generator(spec.mode_id, spec.name)
    n = spec.name
    scripts = {
        f"{n}Controller":    f"{AR}/{n}/{n}Controller.cs",
        f"{n}ScoringRuleSO": f"{AR}/{n}/{n}ScoringRuleSO.cs",
        spec.monitor:        f"{AR}/TurnMonitors/{spec.monitor}.cs",
    }
    G_SCRIPT = {k: guid(f"script/{k}") for k in scripts}
    G_ASSET = {k: guid(f"asset/{k}") for k in (
        f"ArcadeGame{n}", f"{n}ScoringRule", f"GameToastConfig{n}", f"ModePreview{n}", f"Minigame{n}.unity")}

    EXISTING = dict(lib.SCRIPT_GUIDS)
    EXISTING.update(lib.CARD_ART)
    EXISTING["Vessel_Goby"] = VESSEL_GOBY

    d = spec.donor
    DONOR = {
        f"{d}Controller":  guid(f"script/{d}Controller"),
        spec.donor_monitor: guid(f"script/{spec.donor_monitor}"),
        f"{d}Settings":    guid(f"asset/{d}Settings"),
        f"{d}ScoringRule": guid(f"asset/{d}ScoringRule"),
    }
    DONOR_SCENE = f"{lib.SCENES_DIR}/Minigame{d}.unity"
    card_rel = f"Assets/_SO_Assets/Games/ArcadeGame{n}.asset"
    errors = list(spec.extra_errors)

    # ── 1. metas ──
    for k, p in scripts.items():
        g.script_meta(p, G_SCRIPT[k])
    g.folder_meta(f"{AR}/{n}")
    g.text_meta(f"{AR}/{spec.doc}")

    # ── 2. scoring rule ──
    g.emit_asset(f"Assets/_SO_Assets/Scoring Rules/{n}ScoringRule.asset", G_ASSET[f"{n}ScoringRule"],
                 lib.header_for(G_SCRIPT[f"{n}ScoringRuleSO"], f"{n}ScoringRule")
                 + f"  metric: {spec.metric}\n  golfRules: {spec.golf}\n" + spec.rule_fields)

    # ── 3. arcade card ──
    g.emit_asset(card_rel, G_ASSET[f"ArcadeGame{n}"],
                 lib.header_for(EXISTING["SO_ArcadeGame"], f"ArcadeGame{n}") + f"""  Mode: {spec.mode_id}
  IsMultiplayer: 1
  DisplayName: {spec.display}
  Description: {lib.wrap_yaml_scalar(spec.description)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(card_rel)}, type: 3}}
  GolfScoring: {spec.golf}
  SceneName: Minigame{n}
  Vessels:
  - {{fileID: 11400000, guid: {VESSEL_GOBY}, type: 2}}
  MinPlayersAllowed: {spec.min_players}
  MaxPlayersAllowed: 4
  MinDomainsAllowed: {spec.min_domains}
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(spec.comeback_rate)}
""")

    # ── 4. toasts ──
    g.emit_asset(f"Assets/_SO_Assets/Game Toasts/GameToastConfig_{n}.asset", G_ASSET[f"GameToastConfig{n}"],
                 lib.header_for(EXISTING["GameToastConfigSO"], f"GameToastConfig_{n}")
                 + f"  gameMode: {spec.mode_id}\n  toasts:\n" + spec.toasts
                 + lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
    g.register_toast_config(G_ASSET[f"GameToastConfig{n}"])

    # ── 5. preview: the donor's (its cells and spawn block ARE this scene's), re-labelled for the pair ──
    donor_preview = g.read(f"Assets/_SO_Assets/Mode Previews/ModePreview_{d}.asset")
    body = donor_preview.split("  m_EditorClassIdentifier:\n", 1)[1]
    body, k1 = re.subn(r"^  Mode: \d+\n", f"  Mode: {spec.mode_id}\n", body, flags=re.M)
    body, k2 = re.subn(r"^  Notes: '(?:[^']|'')*'\n", f"  Notes: '{spec.preview_notes.replace(chr(39), chr(39) * 2)}'\n",
                       body, flags=re.M | re.S)
    body, k3 = re.subn(r"^  Vessel: \d+\n", f"  Vessel: {GOBY_ID}\n", body, flags=re.M)
    body, k4 = re.subn(r"^  ObjectiveText: [^\n]*\n", f"  ObjectiveText: {spec.objective_text}\n", body, flags=re.M)
    body, k5 = re.subn(r"^  ObjectiveMetric: \d+\n", f"  ObjectiveMetric: {spec.metric}\n", body, flags=re.M)
    if (k1, k2, k3, k4, k5) != (1, 1, 1, 1, 1):
        errors.append(f"ModePreview_{d}.asset changed shape (Mode/Notes/Vessel/ObjectiveText/ObjectiveMetric "
                      f"matched {(k1, k2, k3, k4, k5)} times)")
    if ":" in spec.objective_text:
        errors.append("the preview's ObjectiveText is a plain YAML scalar: no colons")
    g.emit_asset(f"Assets/_SO_Assets/Mode Previews/ModePreview_{n}.asset", G_ASSET[f"ModePreview{n}"],
                 lib.header_for(EXISTING["ModePreviewDefinitionSO"], f"ModePreview_{n}") + body)
    g.register_preview(G_ASSET[f"ModePreview{n}"])

    # ── 6. scene: the donor's, its cells kept ──
    donor_ctrl_fields = (f"  settings: {{fileID: 11400000, guid: {DONOR[f'{d}Settings']}, type: 2}}\n"
                         f"  rule: {{fileID: 11400000, guid: {DONOR[f'{d}ScoringRule']}, type: 2}}\n"
                         "  arenaCell: {fileID: 1700000065}\n")
    ctrl_fields = (f"  rule: {{fileID: 11400000, guid: {G_ASSET[f'{n}ScoringRule']}, type: 2}}\n"
                   + spec.controller_fields)
    monitor_head = "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
    donor_monitor_block = (f"  m_Script: {{fileID: 11500000, guid: {DONOR[spec.donor_monitor]}, type: 3}}\n"
                           + monitor_head + spec.donor_monitor_lines)
    monitor_block = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT[spec.monitor]}, type: 3}}\n"
                     + monitor_head + spec.monitor_lines)

    def build_clone():
        scene = g.read(DONOR_SCENE)
        scene = lib.replace_block(scene, donor_monitor_block, monitor_block, "turn monitor")
        scene = lib.swap_guid(scene, DONOR[f"{d}Controller"], G_SCRIPT[f"{n}Controller"], "controller")
        scene = lib.replace_block(scene, donor_ctrl_fields, ctrl_fields, "controller fields")
        scene = lib.swap_guid(scene, f"  - vesselClass: {spec.donor_vessel_id}\n    PlayerName: AI",
                              f"  - vesselClass: {GOBY_ID}\n    PlayerName: AI", "AI templates", expected=4)
        return scene

    scene_rel = f"{lib.SCENES_DIR}/Minigame{n}.unity"
    scene, scene_errs = lib.committed_scene(scene_rel, build_clone, [ctrl_fields, monitor_block])
    errors += scene_errs
    g.emit_scene(f"Minigame{n}", G_ASSET[f"Minigame{n}.unity"], scene)

    # ── 7. registries ──
    g.register_arcade_card(G_ASSET[f"ArcadeGame{n}"])
    g.register_always_unlocked()
    g.register_build_scene(f"Minigame{d}", f"Minigame{n}", G_ASSET[f"Minigame{n}.unity"])
    g.set_end_condition(spec.end_key, after="glasshouseRoundSeconds", value=spec.end_value)

    # ══ VALIDATE ══
    quarter = 0.25 * spec.comeback_reference * spec.comeback_rate
    if quarter < 1.0:
        errors.append(f"comeback rate {spec.comeback_rate} is dead: a quarter of {spec.comeback_reference} behind buys "
                      f"{quarter:.2f} element levels (< 1)")

    # The ids are the roster's (parallel sessions build the other vessels' games on theirs).
    roster = g.read(ROSTER) if os.path.exists(os.path.join(lib.ROOT, ROSTER)) else ""
    if roster and not re.search(rf"\| Goby \| {spec.element} \| [^|]+ \| {spec.mode_id} \| {spec.toast_hint} \|", roster):
        errors.append(f"{ROSTER} does not reserve GameModes {spec.mode_id} / toast {spec.toast_hint} for Goby {spec.element}")
    if f"{n} = {spec.mode_id}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
        errors.append(f"GameModes.{n} is not {spec.mode_id}")
    if f"{n}Hint = {spec.toast_hint}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
        errors.append(f"GameToastSituation.{n}Hint is not {spec.toast_hint}")
    if f"GameToastSituation.{n}Hint" not in g.read(scripts[f"{n}Controller"]):
        errors.append(f"{n}Controller never posts its hint")

    # The C# default matches the authored value.
    eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
    default_name = "Default" + spec.end_key[0].upper() + spec.end_key[1:]
    if f"{default_name} = {spec.end_value};" not in eco:
        errors.append(f"EndConditionOverridesSO.{default_name} differs from {spec.end_value}")
    if f"public int {spec.end_key} = {spec.end_value};" not in eco:
        errors.append(f"EndConditionOverridesSO.{spec.end_key} initializer differs from {spec.end_value}")

    # The premise: the pair's kit still does what this game scores. A retune that breaks one names it here.
    duo = g.read(DUO)
    for needle, why in spec.kit_needles:
        if needle not in duo:
            errors.append(f"GobyPistolDuo no longer has '{needle}' - {why}")

    # No mode AI and no vessel edits: the controller arms nothing and presses nothing.
    ctrl = g.read(scripts[f"{n}Controller"])
    for bad in ("PerformShipControllerActions", "InputStatus", "RaceCoach", "SetExternalTargetProvider",
                "GobyPistolConfigSO"):
        if bad in ctrl:
            errors.append(f"{n}Controller touches '{bad}' - the pair's own crew AI plays this game; a mode never "
                          f"presses for it or changes the kit")

    # The clone: every new identity present, every donor one gone, four Goby AI templates.
    if scene is not None:
        for label, gd in ((f"{n}Controller", G_SCRIPT[f"{n}Controller"]), (spec.monitor, G_SCRIPT[spec.monitor]),
                          (f"{n}ScoringRule", G_ASSET[f"{n}ScoringRule"])):
            if gd not in scene:
                errors.append(f"scene missing {label}")
        for label, gd in DONOR.items():
            if gd in scene:
                errors.append(f"scene still references the donor's {label}")
        if scene.count(f"  - vesselClass: {GOBY_ID}\n") != 4 or f"  - vesselClass: {spec.donor_vessel_id}\n" in scene:
            errors.append("scene does not carry exactly 4 Goby AI templates")

    g.finish(errors, referenced=EXISTING,
             minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                    + [guid(f"folder/{AR}/{n}"), guid(f"text/{AR}/{spec.doc}")])

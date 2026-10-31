#!/usr/bin/env python3
"""
The shared half of the hull TIME races: every vessel's Time card is a gate race on the shared
GateRaceController platform, and once the course is chosen the rest of a hull's race is the same
assets with different numbers. This module authors them for one SPEC; each mode's own generator
(author_scarab_rally_assets.py, author_serpent_time_assets.py, author_brittle_star_time_assets.py)
is a spec plus a call.

What one spec authors (arcade_mode_lib's Generator does the writing, the guids and --check):
  - .cs.meta for the mode's scripts, its folder and its doc;
  - the scoring rule ASSET (metric 9 SwitchesThreaded, golf), on the shared GateRaceScoringRuleSO;
  - the arcade card, locked to the one hull;
  - the scene, CLONED once from a sibling race (`donor`) and adopted once committed
    (lib.committed_scene): controller script swapped, rule swapped, the controller field block
    replaced, fresh Netcode GlobalObjectIdHash values minted;
  - the preview definition, the toasts (the shared race beats), and the registrations (arcade
    roster + grid, always-unlocked, build scene, end-condition target).

It also VALIDATES what every hull race promises: the comeback rate buys a level at a quarter of the
target, the target agrees with EndConditionOverridesSO's three numbers, the HUD has a case for the
mode, the scene carries the mode's controller and none of the donor's, the scene's network ids are
unique in the project, and the hull's AIPilot is enabled so an AI can race.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT
guid = lib.guid
GATE_RACE_SCORING_RULE_SO = "349cc0c9402590262de23356775d43cc"
BARREN_RACE_CELL = "8d4e8398eedc76c4dadb8604f89b9e1b"
PREVIEW_CELL = "52de45c2fab54ea0a35bc37a311d80e6"
AI_PILOT_SCRIPT = "a58bf4fb65afa704194fe9e28e67d58d"


def mode_id(member: str) -> int:
    with open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8") as fh:
        m = re.search(rf"^\s*{member}\s*=\s*(\d+)\s*,", fh.read(), re.M)
    assert m, f"GameModes.{member} not found - has the member been renamed?"
    return int(m.group(1))


def read_or_empty(rel):
    full = os.path.join(ROOT, rel)
    return open(full, encoding="utf-8").read() if os.path.exists(full) else ""


def project_network_hashes(exclude: str) -> "set[int]":
    seen = set()
    for dirpath, _dirs, files in os.walk(os.path.join(ROOT, "Assets")):
        for fn in files:
            if not fn.endswith((".unity", ".prefab")):
                continue
            full = os.path.join(dirpath, fn)
            if os.path.normpath(full) == os.path.normpath(os.path.join(ROOT, exclude)):
                continue
            with open(full, encoding="utf-8", errors="ignore") as fh:
                seen.update(int(h) for h in re.findall(r"GlobalObjectIdHash: (\d+)", fh.read()))
    return seen


def author(spec: dict):
    """spec keys:
    member, display, hull, class_id, scripts {name: path}, doc, donor_scene, donor_controller_guid,
    donor_rule_guid, donor_fields, new_fields, target, laps, target_key, target_after,
    comeback, description, preview_objective, preview_notes, build_after, max_speed, card_extra_checks,
    new_field_lines (optional: serialized lines appended to the controller block, the mode's own fields),
    extra_toasts (optional: [(situation, template[, lib.toast kwargs])], the mode's own toasts after the race beats)
    cell_configs (optional: [cell config guid, ...] - the scene Cell's CellConfigs; more than one is IntensityWise,
                  list order = intensity 1..n. Omitted: the donor's barren race cell stays)
    """
    member = spec["member"]
    mid = mode_id(member)
    g = lib.Generator(mid, member)
    errors = []

    vessel_guid = lib.existing_guid(f"Assets/_SO_Assets/Classes/SO_Class_{spec['hull']}.asset")
    scripts = spec["scripts"]
    g_script = {k: guid(f"script/{k}") for k in scripts}
    mode_dir = os.path.dirname(scripts[f"{member}Controller"])
    card = f"Assets/_SO_Assets/Games/ArcadeGame{member}.asset"
    rule = f"Assets/_SO_Assets/Scoring Rules/{member}ScoringRule.asset"
    preview = f"Assets/_SO_Assets/Mode Previews/ModePreview_{member}.asset"
    toasts = f"Assets/_SO_Assets/Game Toasts/GameToastConfig_{member}.asset"
    scene_name = f"Minigame{member}"
    scene_rel = f"{lib.SCENES_DIR}/{scene_name}.unity"
    g_card, g_rule = guid(f"asset/ArcadeGame{member}"), guid(f"asset/{member}ScoringRule")
    g_preview, g_toasts = guid(f"asset/ModePreview_{member}"), guid(f"asset/GameToastConfig_{member}")
    g_scene = guid(f"asset/{scene_name}.unity")
    # A mode may author fields of its OWN controller after the shared block (Serpent Time's debris prefab).
    base_fields = spec["new_fields"].replace("__RULE__", g_rule)
    new_fields = base_fields + spec.get("new_field_lines", "")

    # 1. metas
    for k, p in scripts.items():
        g.script_meta(p, g_script[k])
    g.folder_meta(mode_dir)
    g.text_meta(spec["doc"])

    # 2. scoring rule: metric 9 = SwitchesThreaded, golf (finish time).
    g.emit_asset(rule, g_rule, lib.header_for(GATE_RACE_SCORING_RULE_SO, f"{member}ScoringRule") +
                 "  metric: 9\n  golfRules: 1\n")

    # 3. card - one hull in Vessels drives all three vessel clamps; 2/2 minimum: a race needs a rival.
    g.emit_asset(card, g_card, lib.header_for(lib.SCRIPT_GUIDS["SO_ArcadeGame"], f"ArcadeGame{member}") + f"""  Mode: {mid}
  IsMultiplayer: 1
  DisplayName: {spec['display']}
  Description: {spec['description']}
  IconActive: {{fileID: 21300000, guid: {lib.CARD_ART['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {lib.CARD_ART['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(member)}, type: 3}}
  GolfScoring: 1
  SceneName: {scene_name}
  Vessels:
  - {{fileID: 11400000, guid: {vessel_guid}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {spec['comeback']}
""")

    # 4. scene: clone the donor once, adopt it once committed.
    def clone_scene() -> str:
        scene = g.read(spec["donor_scene"])
        scene = lib.swap_guid(scene, spec["donor_controller_guid"], g_script[f"{member}Controller"], "controller script")
        scene = lib.replace_block(scene, spec["donor_fields"], new_fields, "controller field block")
        assert re.search(r"^  spawnFormation: 1$", scene, re.M), "donor no longer on the equatorial spawn ring"
        taken = project_network_hashes(scene_rel)
        donor_hashes = sorted({int(h) for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene)} - {0})
        assert donor_hashes, "donor scene has no in-scene NetworkObjects - has it changed shape?"
        for i, old in enumerate(donor_hashes):
            n = int(guid(f"{scene_name}/hash/{i}")[:8], 16)
            while n == 0 or n in taken:
                n = (n * 1103515245 + 12345) & 0xFFFFFFFF
            taken.add(n)
            scene = lib.swap_guid(scene, f"GlobalObjectIdHash: {old}\n", f"GlobalObjectIdHash: {n}\n",
                                  f"network hash {old}", expected=scene.count(f"GlobalObjectIdHash: {old}\n"))
        return scene

    cell_block = None
    if spec.get("cell_configs"):
        cells = spec["cell_configs"]
        cell_block = ("  CellConfigs:\n" + "".join(f"  - {{fileID: 11400000, guid: {c}, type: 2}}\n" for c in cells) +
                      f"  cellTypeChoiceOptions: {1 if len(cells) > 1 else 0}\n")
    authored_blocks = (new_fields, "  spawnFormation: 1\n") + ((cell_block,) if cell_block else ())
    scene, _ = lib.committed_scene(scene_rel, clone_scene, authored_blocks=authored_blocks)
    # The mode's own cells replace the donor's: one Cell in the scene, its config list rewritten in place.
    if cell_block and cell_block not in scene:
        found = re.findall(r"^  CellConfigs:\n(?:  - \{[^\n]*\}\n)+  cellTypeChoiceOptions: \d+\n", scene, re.M)
        if len(found) == 1:
            scene = scene.replace(found[0], cell_block)
        else:
            errors.append(f"{os.path.basename(scene_rel)}: expected one Cell CellConfigs block, found {len(found)}")
    # One-shot migration for a scene committed before the mode authored its own field lines: append them to the
    # controller block. Stands down once applied (the full block is then in the scene).
    if new_fields not in scene and new_fields != base_fields and scene.count(base_fields) == 1:
        scene = scene.replace(base_fields, new_fields)
    errors += [f"{os.path.basename(scene_rel)}: authored block no longer in the committed scene:\n{block}"
               for block in authored_blocks if block not in scene]
    g.emit_scene(scene_name, g_scene, scene)

    # 5. preview - a card without one offers no Test Flight.
    g.emit_asset(preview, g_preview, lib.header_for(lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"], f"ModePreview_{member}") + f"""  Mode: {mid}
  Notes: '{spec['preview_notes']}'
  PreviewCell: {{fileID: 11400000, guid: {PREVIEW_CELL}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {spec['class_id']}
  ObjectiveText: {spec['preview_objective']}
  ObjectiveMetric: 9
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 150
  SpawnRingRadiusFloor: 0
  SpawnFormation: 1
  SpawnPoints: []
""")
    g.register_preview(g_preview)

    # 5b. toasts - the shared race beats GateRaceController already ticks (DomainRaceToasts).
    g.emit_asset(toasts, g_toasts, lib.header_for(lib.SCRIPT_GUIDS["GameToastConfigSO"], f"GameToastConfig_{member}") +
                 f"  gameMode: {mid}\n  toasts:\n" +
                 lib.toast(129, "{0} is halfway home - gate {1}/{2}", tint_domain=1, domain_names=0) +
                 lib.toast(130, "{0} takes the lead - gate {1}/{2}", tint_domain=1, domain_names=0) +
                 lib.toast(131, "{0} is on the home stretch - gate {1}/{2}", tint_domain=1, domain_names=0) +
                 lib.toast(132, "{0} is on the final lap", tint_domain=1, domain_names=0) +
                 lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9) +
                 # A mode's own beats, which its controller posts (Serpent Time's rounds and walls).
                 "".join(lib.toast(*t[:2], **(t[2] if len(t) > 2 else {})) for t in spec.get("extra_toasts", [])))
    g.register_toast_config(g_toasts)

    # 6. registries
    g.register_arcade_card(g_card)
    g.register_always_unlocked()
    g.register_build_scene(spec["build_after"], scene_name, g_scene)
    g.set_end_condition(spec["target_key"], after=spec["target_after"], value=spec["target"])

    # ══ VALIDATE ═══════════════════════════════════════════════════════════
    target = spec["target"]
    if 0.25 * target * spec["comeback"] < 1.0:
        errors.append(f"comeback rate {spec['comeback']} is dead against target {target}: a quarter-of-target "
                      f"deficit buys {0.25 * target * spec['comeback']:.2f} element levels (< 1)")
    if target % spec["laps"] != 0:
        errors.append(f"race target {target} is not a whole number of {spec['laps']} laps")

    end = read_or_empty("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
    key = spec["target_key"]
    for pat, what in ((rf"Default{member}GateTarget\s*=\s*(\d+);", f"Default{member}GateTarget"),
                      (rf"public int {key}\s*=\s*(\d+);", f"{key} initializer"),
                      (rf"public int {key}Build\s*=\s*(\d+);", f"{key}Build initializer")):
        m = re.search(pat, end)
        if not m or int(m.group(1)) != target:
            errors.append(f"EndConditionOverridesSO.{what} is not {target}")
    if f"GameModes.{member}" not in end:
        errors.append(f"EndConditionOverridesSO.TryGetAuthoredTurnTarget has no {member} row")
    if f"case GameModes.{member}:" not in read_or_empty("Assets/_Scripts/UI/MiniGameHUD.cs"):
        errors.append(f"MiniGameHUD.CreateObjectiveProviderForGameMode has no {member} case")
    if f"{mid}: '{scene_name}'" not in read_or_empty("Tools/Build/author_preview_spawns.py"):
        errors.append(f"author_preview_spawns.py SCENE_FOR_MODE has no {mid}: '{scene_name}' row")

    if g_script[f"{member}Controller"] not in scene or spec["donor_controller_guid"] in scene:
        errors.append(f"scene does not carry exactly the {member}Controller script")
    if spec["donor_rule_guid"] in scene:
        errors.append("scene still references the donor's scoring rule")
    taken = project_network_hashes(scene_rel)
    for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene):
        if int(h) != 0 and int(h) in taken:
            errors.append(f"scene GlobalObjectIdHash {h} is also carried by another scene or prefab")

    hull = read_or_empty(f"Assets/_Prefabs/Spacevessels/{spec['hull']}.prefab")
    ai = re.search(r"  m_Enabled: (\d)\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, "
                   rf"guid: {AI_PILOT_SCRIPT}, type: 3\}}", hull)
    if not ai or ai.group(1) != "1":
        errors.append(f"{spec['hull']}.prefab's AIPilot is not enabled - an AI {spec['hull']} cannot race")
    m = re.search(r"^  maxPlausibleSpeed: (\d+)$", new_fields, re.M)
    if not m or int(m.group(1)) < 2 * spec["max_speed"]:
        errors.append(f"maxPlausibleSpeed must cover twice the hull's top speed ({spec['max_speed']} u/s)")
    if f"VesselClassType.{spec['hull']}" not in read_or_empty("Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs"):
        errors.append(f"ToyVesselRoster.Default does not offer the {spec['hull']}")
    for k, p in scripts.items():
        if not os.path.exists(os.path.join(ROOT, p)):
            errors.append(f"script {p} does not exist")
    errors += spec.get("extra_errors", [])

    minted = list(g_script.values()) + [g_card, g_rule, g_preview, g_scene, g_toasts,
                                        guid(f"folder/{mode_dir}"), guid(f"text/{spec['doc']}")]
    g.finish(errors, referenced={"Vessel": vessel_guid, "BarrenRaceCell": BARREN_RACE_CELL,
                                 "PreviewCell": PREVIEW_CELL, "GateRaceScoringRuleSO": GATE_RACE_SCORING_RULE_SO},
             minted=minted)

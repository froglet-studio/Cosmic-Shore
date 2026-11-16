#!/usr/bin/env python3
"""
What the Orca's three arcade generators share (Pod Hunt 99, Ice Shelf 100, Depth Charge 101; the game kit roster,
Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 9-11): the shared settings type and its ladders, the AI helm script, the
card shape, the load-bearing assumptions about the Orca's kit, and the scene clone's common swaps.

NOTHING ABOUT THE ORCA CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). Every mode here references an existing
arena, the platform's crystals and herds, and the crew's own gunner AI; what a mode adds is a scoring rule, a monitor,
a controller and the steering helm (OrcaArcadeHelm), which only ever sets a target for the platform's AIPilot.

Not a generator: imported by author_pod_hunt_assets.py, author_ice_shelf_assets.py and author_depth_charge_assets.py.
"""
import arcade_mode_lib as lib

AR = "Assets/_Scripts/Controller/Arcade"
ORCA_ID = lib.VESSEL_CLASS_ID["Orca"]
VESSEL_ORCA = "6984d323ceb54c5ab6944d3de86793d3"      # SO_Class_Orca.asset
ORCA_PREFAB = "Assets/_Prefabs/Spacevessels/Orca.prefab"
ORCA_CREW = "Assets/_Scripts/Controller/Vessel/Crew/OrcaCrew.cs"
ORCA_CREW_CONFIG = "Assets/_Scripts/Controller/Vessel/Crew/OrcaCrewConfigSO.cs"
ORCA_CREW_AI = "Assets/_Scripts/Controller/AI/Orca/OrcaCrewAI.cs"

SHARED_SCRIPTS = {
    "OrcaArcadeSettingsSO": f"{AR}/OrcaArcade/OrcaArcadeSettingsSO.cs",
    "OrcaArcadeHelm":       f"{AR}/OrcaArcade/OrcaArcadeHelm.cs",
}
SHARED_FOLDER = f"{AR}/OrcaArcade"

# ── The shared settings (index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds": ([2.5, 1.8, 1.2, 0.8], "down"),
    "aiScanRange":       ([500, 650, 800, 1000], "up"),
    "aiStandoff":        ([320, 260, 210, 170], "down"),
    "aiLeadSeconds":     ([0, 0.2, 0.4, 0.6], "up"),
}
SHARED = {"sampleSeconds": 0.5, "leadAnnounceAfterSeconds": 10, "leadAnnounceMinGapSeconds": 8}
SHARED_AI = {"aiRefuelBelowMissiles": 1, "aiHumanFocus": 1.5, "aiShelfLoopRadius": 160}
SHELF_LOOP_POINTS = 6


def script_guids():
    return {k: lib.guid(f"script/{k}") for k in SHARED_SCRIPTS}


def emit_shared_metas(g):
    """Every Orca generator writes the shared scripts' metas (byte-identical), so any one of them brings them up."""
    for k, p in SHARED_SCRIPTS.items():
        g.script_meta(p, lib.guid(f"script/{k}"))
    g.folder_meta(SHARED_FOLDER)
    return list(script_guids().values()) + [lib.guid(f"folder/{SHARED_FOLDER}")]


def settings_yaml(name):
    def ladder(n):
        return f"  {n}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[n][0])
    return (lib.header_for(lib.guid("script/OrcaArcadeSettingsSO"), name)
            + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items())
            + "".join(ladder(n) for n in LADDERS)
            + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED_AI.items())
            + f"  aiShelfLoopPoints: {SHELF_LOOP_POINTS}\n")


def card_yaml(existing, name, mode_id, display, description, card_rel, scene, golf, min_players, min_domains,
              comeback):
    return lib.header_for(existing["SO_ArcadeGame"], name) + f"""  Mode: {mode_id}
  IsMultiplayer: 1
  DisplayName: {display}
  Description: {lib.wrap_yaml_scalar(description)}
  IconActive: {{fileID: 21300000, guid: {existing['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {existing['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(card_rel)}, type: 3}}
  GolfScoring: {1 if golf else 0}
  SceneName: {scene}
  Vessels:
  - {{fileID: 11400000, guid: {VESSEL_ORCA}, type: 2}}
  MinPlayersAllowed: {min_players}
  MaxPlayersAllowed: 4
  MinDomainsAllowed: {min_domains}
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(comeback)}
"""


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


def swap_ai_templates(scene, donor_class):
    return lib.swap_guid(scene, f"  - vesselClass: {donor_class}\n    PlayerName: AI",
                         f"  - vesselClass: {ORCA_ID}\n    PlayerName: AI", "AI templates", expected=4)


def validate_shared(g, errors):
    """The shared settings' C# initializers, and the Orca kit facts every mode here leans on."""
    settings_cs = g.read(SHARED_SCRIPTS["OrcaArcadeSettingsSO"])
    for name, (values, harder) in LADDERS.items():
        pairs = list(zip(values, values[1:]))
        if len(values) != 4:
            errors.append(f"{name}: needs four rungs")
        if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
            errors.append(f"{name}: not monotone toward harder ({values})")
        init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
        if f"{name} = {init};" not in settings_cs:
            errors.append(f"OrcaArcadeSettingsSO.{name}: the C# initializer does not match {init}")
    for field, value in {**SHARED, **SHARED_AI}.items():
        if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
            errors.append(f"OrcaArcadeSettingsSO.{field} initializer differs from the authored {value}")
    if f"aiShelfLoopPoints = {SHELF_LOOP_POINTS};" not in settings_cs:
        errors.append("OrcaArcadeSettingsSO.aiShelfLoopPoints initializer differs from the authored value")

    # The helm only steers: it sets the platform AIPilot's target and never presses a control (the crew's own AI
    # fires; the LOCKED vessel rule keeps the mode out of the kit).
    helm = g.read(SHARED_SCRIPTS["OrcaArcadeHelm"])
    for needle in ("SetExternalTargetProvider", "ClearExternalTargetProvider", "AIDifficultyFor("):
        if needle not in helm:
            errors.append(f"OrcaArcadeHelm no longer calls '{needle}'")
    for banned in ("PerformShipControllerActions", "InputStatus", "TriggerAnalog", "SelectedIntensity"):
        if banned in helm:
            errors.append(f"OrcaArcadeHelm uses '{banned}' - the helm steers only, keyed on the seat's difficulty")

    crew = g.read(ORCA_CREW)
    for needle in ('const string ShieldTag = "::OrcaShield::"', "public const int Helm = 0, Seats = 4;",
                   "public int MissilesAt(int seat)", "public void OnOmniCrystal()",
                   "PrismKind.SuperShielded : PrismKind.Danger"):
        if needle not in crew:
            errors.append(f"OrcaCrew no longer has '{needle}' - the Orca games read the crew's shield tag, its seats "
                          f"and magazines, and the omni crystal's refill")
    cfg = g.read(ORCA_CREW_CONFIG)
    for needle in ("float shieldLinger = 20f;", "Vector3 shieldBlockScale = new(10f, 1f, 1f);"):
        if needle not in cfg:
            errors.append(f"OrcaCrewConfigSO no longer declares '{needle}' - Ice Shelf's reference volume reads it")
    if "spawnerEnabled: 1" not in g.read(ORCA_PREFAB):
        errors.append("Orca.prefab's trail spawner is off - the Orca would lay no mass")
    census = g.read(f"{AR}/IceShelf/IceShelfCensus.cs")
    if 'public const string ShieldTag = "::OrcaShield::";' not in census:
        errors.append("IceShelfCensus.ShieldTag differs from OrcaCrew's")


def validate_scene(errors, scene, present, gone, donor_class):
    if scene is None:
        return
    for name, gd in present.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in gone.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {ORCA_ID}\n") != 4 or f"  - vesselClass: {donor_class}\n" in scene:
        errors.append("scene does not carry exactly 4 Orca AI templates")


def validate_enums(g, errors, enum_name, mode_id, hint_name, hint_id):
    if f"{enum_name} = {mode_id}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
        errors.append(f"GameModes.{enum_name} is not {mode_id}")
    if f"{hint_name} = {hint_id}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
        errors.append(f"GameToastSituation.{hint_name} is not {hint_id}")

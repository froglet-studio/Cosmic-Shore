#!/usr/bin/env python3
"""Author the voyage CONTENT asset (Docs/Voyages/SLICE.md section 3, Docs/Voyages/DEPTH.md).

A generator in the author_*_assets.py family: deterministic guids, idempotent, --check reports drift and writes
nothing, --self-test proves the checks fire. It owns:

  * Assets/Resources/Voyages/                       folder (VoyageContentSO.ResourcePath: the voyage loads it at runtime)
  * Assets/Resources/Voyages/VoyageContent.asset    the map, crew, jobs, events, seed news, rivals, sagas, the world's
                                                    rates, the Kaiju, the rules' words and the HUD lines, from
                                                    Docs/Voyages/voyage_slice.json
  * Docs/Voyages/voyage_content.json                the SAME content as the C# classes read it (field names, enums as
                                                    numbers): the headless depth harness and the edit-mode depth tests load
                                                    it (Tools/Build/voyage_depth_harness), so the asset and the tests can
                                                    never disagree about what ships
  * Assets/Resources/Voyages/Voyage.asset           the voyage settings (the spec's "toy" block; VoyageToyDefinitionSO.Load)
  * Assets/Resources/Toybox.asset                   one line kept OFF: voyages have their own scene, freestyle stands no harbour

The content asset's `cellPool` (which worlds the locations and lanes are) and the settings' `arkEscort` (the escort asset)
are hand-authored: the generator keeps whatever the assets already hold there.

The model (`to_model`) is built once from the spec with the C# field names, and both the YAML and the JSON are
written from it, so the two outputs are one table.

Usage:
    python3 Tools/Build/author_voyage_assets.py             # write
    python3 Tools/Build/author_voyage_assets.py --check     # report drift, write nothing
    python3 Tools/Build/author_voyage_assets.py --self-test # negative controls
"""
import copy
import hashlib
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_pilot_assets import (  # noqa: E402  (shared YAML and enum helpers)
    ASSET_META, FOLDER_META, SO_HEADER, ROOT, ascii_ok, enum_values, fnum, read, script_guid, yaml_any,
)
from author_callsign_assets import CALLSIGNS  # noqa: E402

SPEC = "Docs/Voyages/voyage_slice.json"
PILOT_SPEC = "Docs/Pilots/pilots_and_artifacts.json"
OUT_DIR = "Assets/Resources/Voyages"
OUT = OUT_DIR + "/VoyageContent.asset"
TWIN = "Docs/Voyages/voyage_content.json"
SO_CS = "Assets/_Scripts/ScriptableObjects/Voyages/VoyageContentSO.cs"
TOY_CS = "Assets/_Scripts/ScriptableObjects/Voyages/VoyageToyDefinitionSO.cs"
TOY = "Assets/Resources/Voyages/Voyage.asset"
TOYBOX = "Assets/Resources/Toybox.asset"
ARKWAY_SETTINGS_META = "Assets/_SO_Assets/Toys/Wander_WithArk.asset.meta"
ARK_ESCORT_META = "Assets/_SO_Assets/Toys/ArkEscort.asset.meta"
ENUMS_CS = "Assets/_Scripts/Data/Voyages/VoyageEnums.cs"
DOMAINS_CS = "Assets/_Scripts/Data/Enums/Domains.cs"
ELEMENT_CS = "Assets/_Scripts/Data/Enums/Element.cs"

# Every line the voyage HUD and the Voyage window show (VoyageLines in VoyageContentSO.cs). A missing key is a typo.
LINES_FIELDS = [
    "VoyageName", "Depart", "Arrived", "Vaults", "Wreck", "Hold", "HoldProgress", "Done", "Failed", "Stowed", "Extracted",
    "ArkLost", "NoWork", "Following", "ChooseNext",
    "Lair", "Hoard", "Nursery", "Derelict", "Pen", "Shrine", "KaijuHere", "HostileWater", "RivalHere", "RivalOpened",
    "Charted", "Relic", "Delivered", "Convoy", "ProgressStow", "ProgressHold", "ProgressGather", "ProgressCull",
    "ProgressLay", "ProgressRaze", "ProgressObserve", "ProgressMeet",
    "CardRoute", "CardHomeRoute", "CardUnderway", "CardCourse",
    "SeaSpray", "SeaFroth", "SeaWater", "SeaThick", "SeaReef", "CardOpen", "CardClose", "CardDetails", "CardHomeDetails",
    "CardWarnings", "CardWarningSeparator", "CardSquadSeat", "CardSquadSeparator", "MapPlace", "EdgeCaption",
    "EdgeAbove", "EdgeBelow", "EdgeMidplane",
    "MapTierGalaxy", "MapTierArm", "MapTierSector", "MapTierRegion", "MapTierNeighbourhood", "MapTierCell", "MapTierSystem",
    "MapTierGalaxyWhat", "MapTierArmWhat", "MapTierSectorWhat", "MapTierRegionWhat", "MapTierNeighbourhoodWhat",
    "MapTierCellWhat", "MapTierSystemWhat", "MapDiveSol", "MapDiveTumult", "MapReadout", "MapReadoutSlab", "MapLy",
    "MapAbove", "MapBelow", "MapMidplane", "MapInspect", "MapWhere", "MapMinutes", "MapHours", "MapKindStar",
    "MapKindModelStar", "MapKindTumultStar", "MapKindGlobular", "MapKindOpen", "MapUnnamedStar", "MapUnchartedStar",
    "MapStarBody", "MapStarAlso", "MapLight", "MapTypeOB", "MapTypeB", "MapTypeA", "MapTypeF", "MapTypeG", "MapTypeK",
    "MapTypeM", "MapTypeRedSupergiant", "MapTypeGiant", "MapGlobularBody", "MapOpenBody", "MapOpenBodyYoung", "MapOpenAge",
    "MapAgeBillion", "MapAgeMillion", "MapSourceCatalog", "MapSourceModel", "MapSourceTumult", "MapSourceReal",
    "MapSourceProposed", "MapCellLabel", "MapKindCell", "MapCellName", "MapCellBody", "MapCellBerth", "MapSourceCell",
    "MapCellNucleus", "MapCellNucleusHeart", "MapCellNucleusBody", "MapCellNucleusAbove", "MapCellNucleusBelow", "MapCellCytoplasm", "MapCellCytoplasmBody", "MapCellMembrane", "MapCellMembraneHeart", "MapCellMembraneBody", "EdgeCaptionCells", "EdgeSlice",
    "CardBack", "CardPager", "CardPrevious", "CardNext", "CardLevelVoyage", "CardLevelCrew", "CardLevelStory",
    "CardCourseChip", "CardFoundBy", "CardKicker", "CardCourseLabel", "CardCourseValue", "CardSailLabel", "CardSailValue",
    "CardIntensityLabel", "CardIntensityValue", "CardCells", "CardYourArk", "CardDestination", "CardSquadLabel",
    "CardPresetsLabel", "CardNoSquad", "CardConfigure", "CardSetSail", "CardCrew", "CardStory", "CardVoyage", "CardCrewHead",
    "CardCrewNote", "CardCrewJoined", "CardSaveSquad", "CardSaved", "CardSeatLeader", "CardSeatRogueOne", "CardSeatRogueTwo",
    "CardSeatYou", "CardSeatAi", "CardSeatOpen", "CardSettingVessel", "CardSettingPilot", "CardSettingElement",
    "CardSettingArtifact", "CardNoArtifact", "ElementCharge", "ElementMass", "ElementSpace", "ElementTime", "CardStoryHead",
    "CardStoryOptional", "StoryNews", "StoryPlace", "StoryCrew", "StoryCrewBy", "StoryWater", "StoryWaterLine",
    "StoryWaterBy", "StoryWorld", "StoryWorldBy", "StoryLog", "StoryLogLine",
    "BoardRowTitle", "BoardRowBy", "BoardRowMeta", "BoardDistanceLabel", "BoardSailLabel", "BoardIntensityLabel", "BoardIntensityValue", "BoardSquadLabel", "BoardSquadJoined", "BoardSeatWho", "BoardSeatKit", "BoardSeatNotYours", "BoardEditorTitle", "BoardPilotLine", "BoardPilotSwaps", "BoardPilotFlying", "BoardElementLine", "BoardNoArtifactLine", "BoardDone", "BoardUnderway", "BoardSailKey", "BoardDoneKey",
    "BoardKicker", "BoardKickerAlone", "BoardStats", "BoardStepBack", "BoardStepOn", "BoardZoomIn", "BoardZoomOut", "BoardConfigure", "BoardConfigureKey", "BoardBack", "BoardBackKey", "BoardSavePreset", "BoardSavePresetKey", "BoardHangarNote", "BoardPresetWho", "BoardSwapsWith", "SlabChip", "SlabSagaPart", "SlabSagaKicker", "SlabSagaForm", "SlabQuestsKicker", "SlabQuestsName", "SlabQuestsProgress", "SlabQuestKicker", "SlabQuestClaimed", "SlabQuestReady", "SlabQuestNext", "SlabQuestLocked", "SlabQuestClaim", "SlabQuestPlay", "SlabEpisodesKicker", "SlabEpisodesName", "SlabEpisodesSoon", "SlabEpisodesLine", "SlabEpisodesFace", "SlabEpisodesBack", "SlabEpisodesBackKey", "SlabSagaLockedKicker", "SlabSagaLockedLine", "SlabSagaLockedFace",
    "QuestsTitle", "QuestsBack", "QuestsBackKey", "QuestsReady", "QuestsOpen", "QuestsTier3", "QuestsTier4", "QuestsTierFeature", "QuestsGoalDone", "QuestsGoalNext", "QuestsClaimStep", "QuestsClaimUnlocks", "QuestsClaimReady", "QuestsRewardKicker", "QuestsRewardKickerYours", "QuestsRewardKind", "QuestsRewardYours", "QuestsRewardReady", "QuestsRewardLater", "QuestsDomainJade", "QuestsDomainRuby", "QuestsDomainGold", "QuestsDomainBlue", "QuestsAlsoOpens", "QuestsOpened", "QuestsPlayNote",
    "StripSagaKicker", "StripSagaName", "StripEpisodesKicker", "StripEpisodesName", "StripSoon", "StripNotOpen",
    "StepSea", "StepVoyage", "StepArk", "PageBack", "PageBackKey", "SeaFullScreen", "SeaFullScreenBack", "SeaCrew", "SeaCrewHide", "SeaTierWidth", "SeaOpenVoyage", "SeaOfferKicker", "SeaOfferPay", "SeaCrewRecommends", "BriefKicker", "BriefStats", "BriefDifficulty", "BriefRecommended", "BriefRewards", "BriefCrystals", "BriefCrystalsBy", "BriefStanding", "BriefStandingBy", "BriefRole", "BriefStop", "BriefReady", "ArkLobby", "ArkLobbyWith", "ArkSquadHead", "ArkSquadLine", "ArkSquadLineLobby", "ArkYou", "ArkWingmate", "ArkInLobby", "ArkFriendLine", "ArkArtifact", "ArkFlySquad", "ArkFlySquadKey", "ArkBackToLobby", "ArkVoyageLine", "PickTitle", "PickTitleSeat", "PickKits", "PickKitVoyage", "PickKitLast", "PickKitWho", "PickSees", "PickSeesAll", "PickSeesCrystals", "PickSeesPrisms", "PickSeesLife", "PickSeesVessels", "PickSeesPaths", "PickCount", "PickForVoyage", "PickNote", "PickClose", "KindCourier", "KindHunt", "KindSalvage", "KindDefence", "KindSurvey", "KindHarvest", "KindStudy", "KindRescue", "KindLiberate", "KindBuild", "KindLift", "KindBounty", "KindRite", "KindParley", "KindRaid", "KindTend", "KindExhibition", "KindConvoy", "KindCull", "KindHome", "KindEscort",
]

# The world's rates (VoyageWorldRules in VoyageContent.cs): spec key -> (C# field, kind). Every one is written.
WORLD_FIELDS = [
    ("boardSize", "BoardSize", int), ("newsLifeDays", "NewsLifeDays", int), ("restockDays", "RestockDays", int),
    ("wreckDays", "WreckDays", int), ("bloomDays", "BloomDays", int), ("notorietyDecayDays", "NotorietyDecayDays", int),
    ("bountyAt", "BountyAt", int), ("ladenRumourAt", "LadenRumourAt", int), ("rivalVaultSeconds", "RivalVaultSeconds", float),
    ("legProfile", "LegProfile", list), ("tierThresholds", "TierThresholds", list), ("intensityPay", "IntensityPay", list),
    ("hopsPay", "HopsPay", float), ("tierPay", "TierPay", list), ("freshnessPerDay", "FreshnessPerDay", float),
    ("freshnessFloor", "FreshnessFloor", float), ("maxTicksAway", "MaxTicksAway", int), ("pressureDrift", "PressureDrift", float),
    ("rivalSuccessBase", "RivalSuccessBase", float), ("rivalStrengthWeight", "RivalStrengthWeight", float),
    ("rivalIntensityWeight", "RivalIntensityWeight", float), ("rivalPressureWeight", "RivalPressureWeight", float),
    ("rivalWreckChance", "RivalWreckChance", float), ("rivalRespawnDays", "RivalRespawnDays", int),
    ("lairPopulation", "LairPopulation", float), ("penPopulation", "PenPopulation", float),
    ("pressurePopulation", "PressurePopulation", float), ("bloomPopulation", "BloomPopulation", float),
    ("aidPressure", "AidPressure", float), ("breachPressure", "BreachPressure", float), ("failurePressure", "FailurePressure", float),
]

# The rules' own words (VoyageWords): spec key -> C# field.
WORDS_FIELDS = [
    ("restockNews", "RestockNews"), ("bountyOnYou", "BountyOnYou"), ("bountyOnRival", "BountyOnRival"),
    ("ladenRumour", "LadenRumour"), ("aidNews", "AidNews"), ("breachNews", "BreachNews"), ("harvestNews", "HarvestNews"),
    ("heldNews", "HeldNews"), ("rivalFellNews", "RivalFellNews"), ("rivalRace", "RivalRace"), ("nemesis", "Nemesis"),
    ("charted", "Charted"), ("relicFound", "RelicFound"), ("standingUp", "StandingUp"), ("wanted", "Wanted"),
    ("warningPressure", "WarningPressure"), ("warningRival", "WarningRival"), ("warningKaiju", "WarningKaiju"),
    ("warningBounty", "WarningBounty"), ("warningLaden", "WarningLaden"), ("warningHeld", "WarningHeld"),
    ("chronicleDelivered", "ChronicleDelivered"), ("roleAny", "RoleAny"), ("roleGuardian", "RoleGuardian"),
    ("roleDestroyer", "RoleDestroyer"), ("roleCultivator", "RoleCultivator"), ("tierStranger", "TierStranger"),
    ("tierKnown", "TierKnown"), ("tierTrusted", "TierTrusted"), ("relationCoOp", "RelationCoOp"),
    ("relationRace", "RelationRace"), ("relationHostile", "RelationHostile"),
]

KAIJU_FIELDS = [
    ("enabled", "Enabled", bool), ("name", "Name", str), ("huntJob", "HuntJob", str), ("respawnDays", "RespawnDays", int),
    ("moveDays", "MoveDays", int), ("cullGoal", "CullGoal", float), ("cullStep", "CullStep", float),
    ("populationPerTier", "PopulationPerTier", float), ("sighted", "SightedLine", str), ("moved", "MovedLine", str),
    ("felled", "FelledLine", str),
]

TOY_COLORS = ("routeColor", "hereColor", "portColor", "vaultColor", "coreColor", "lairColor", "nurseryColor", "shrineColor",
              "unchartedColor", "laneColor")
TOKENS = {"from", "to", "about", "pilot", "rival", "n", "kaiju", "relic", "saga", "faction", "tier", "holder", "home",
          "count", "seconds", "done", "goal", "percent"}
SEALED = ("VaultField", "Hoard")
PORTS = ("Port", "Harbour")


def variants(v):
    """A line in the spec is one string or a list of ways to say it; the model keeps the first as the line and all of them."""
    if v is None or v == "":
        return []
    return [v] if isinstance(v, str) else list(v)


def first(v):
    vs = variants(v)
    return vs[0] if vs else ""


def guid_for(name):
    return hashlib.md5(f"CosmicShore/Voyages/{name}".encode()).hexdigest()


def load(rel):
    return json.loads(read(rel))


def pilot_ids():
    return {f"pilot.callsign.{word.lower()}" for _, word, _ in CALLSIGNS}


def artifact_ids():
    return {a["id"] for a in load(PILOT_SPEC)["artifacts"]}


def enums():
    return {
        "exp": enum_values(ENUMS_CS, "VoyageExperience"),
        "fac": enum_values(ENUMS_CS, "FactionType"),
        "kinds": enum_values(ENUMS_CS, "JobKind"),
        "verbs": enum_values(ENUMS_CS, "WorkVerb"),
        "roles": enum_values(ENUMS_CS, "SquadRole"),
        "tags": enum_values(ENUMS_CS, "NewsTag"),
        "places": enum_values(ENUMS_CS, "NewsPlace"),
        "abouts": enum_values(ENUMS_CS, "NewsAbout"),
        "tempers": enum_values(ENUMS_CS, "RivalTemper"),
        "domains": enum_values(DOMAINS_CS, "Domains"),
        "elements": enum_values(ELEMENT_CS, "Element"),
    }


# ── Validation ───────────────────────────────────────────────────────────────

def validate(spec):
    """Problems an author must fix, one per line (the C# VoyageContent.Validate checks the structural ones at runtime)."""
    errors = []
    e = enums()
    pilots, artifacts = pilot_ids(), artifact_ids()

    def text(where, t, tokens=True):
        if not isinstance(t, str) or not ascii_ok(t):
            errors.append(f"{where}: text must be printable ASCII")
            return
        # The rules fill a fixed vocabulary; the Voyage window's own lines (hud.*) carry the window's tokens.
        if tokens:
            for tok in re.findall(r"\{(\w+)\}", t):
                if tok not in TOKENS:
                    errors.append(f"{where}: unknown token {{{tok}}}")

    locs = {}
    for l in spec["locations"]:
        if l["id"] in locs:
            errors.append(f"location id {l['id']} is used twice")
        locs[l["id"]] = l
        if l["experience"] not in e["exp"]:
            errors.append(f"{l['id']}: unknown experience {l['experience']}")
        text(l["id"], l["name"])
        text(l["id"], l.get("blurb", ""))
        if len(l.get("chart", [])) != 3:
            errors.append(f"{l['id']}: chart needs three numbers")
        g = l.get("galaxy")
        if g is not None:
            if len(g) != 3 or not all(isinstance(v, (int, float)) for v in g):
                errors.append(f"{l['id']}: galaxy needs three numbers (x, y, z in light years)")
            elif abs(g[2]) > 500:
                errors.append(f"{l['id']}: galaxy z {g[2]} is outside the disk (500 ly either side of the midplane)")
            elif (g[0] ** 2 + g[1] ** 2) ** 0.5 < 80:
                errors.append(f"{l['id']}: galaxy position is inside the Heart's closed well")
        if l["experience"] in SEALED:
            if l.get("vaults", 0) < 1 or not l.get("finds"):
                errors.append(f"{l['id']}: a vault field or hoard needs vaults and finds")
            for a in l.get("finds", []):
                if a not in artifacts:
                    errors.append(f"{l['id']}: find {a} is no artifact in {PILOT_SPEC}")
        if l["experience"] == "Harbour" and l.get("home") not in ("Jade", "Ruby", "Gold"):
            errors.append(f"{l['id']}: a harbour needs a home domain (Jade, Ruby or Gold)")
        if not 0 <= float(l.get("restPressure", 0)) <= 3:
            errors.append(f"{l['id']}: restPressure must be 0 to 3")
        if not 0 <= int(l.get("restBloom", 0)) <= 3:
            errors.append(f"{l['id']}: restBloom must be 0 to 3")
        if l.get("uncharted") and l["experience"] in PORTS:
            errors.append(f"{l['id']}: an uncharted port makes no sense (every port is known)")
    for d in ("Jade", "Ruby", "Gold"):
        if not any(l["experience"] == "Harbour" and l.get("home") == d for l in spec["locations"]):
            errors.append(f"no harbour for {d}")

    adj = {k: set() for k in locs}
    for lane in spec["lanes"]:
        for end in ("from", "to"):
            if lane[end] not in locs:
                errors.append(f"a lane names unknown location {lane[end]}")
        if lane["from"] in adj and lane["to"] in adj:
            adj[lane["from"]].add(lane["to"])
            adj[lane["to"]].add(lane["from"])
        if lane.get("cells", 1) < 0:
            errors.append(f"lane {lane['from']}-{lane['to']} has a negative length")
        profile = lane.get("profile", [])
        if profile and len(profile) != lane.get("cells", 1):
            errors.append(f"lane {lane['from']}-{lane['to']}: profile has {len(profile)} entries for {lane.get('cells', 1)} cells")
        if any(p <= 0 for p in profile):
            errors.append(f"lane {lane['from']}-{lane['to']}: a profile entry must be above 0")
    # Every location can be reached from every harbour: a job may point anywhere.
    if locs:
        start = next(iter(locs))
        seen, todo = {start}, [start]
        while todo:
            for n in adj.get(todo.pop(), ()):
                if n not in seen:
                    seen.add(n)
                    todo.append(n)
        for k in locs:
            if k not in seen:
                errors.append(f"{k} cannot be reached by any lane")
        # The charted sea holds together without the uncharted places: they are leaves found from it.
        charted = [k for k, l in locs.items() if not l.get("uncharted")]
        if charted:
            seen, todo = {charted[0]}, [charted[0]]
            while todo:
                for n in adj.get(todo.pop(), ()):
                    if n in seen or locs[n].get("uncharted"):
                        continue
                    seen.add(n)
                    todo.append(n)
            for k in charted:
                if k not in seen:
                    errors.append(f"{k} can only be reached through an uncharted place")
        for k, l in locs.items():
            if l.get("uncharted") and not any(not locs[n].get("uncharted") for n in adj.get(k, ())):
                errors.append(f"uncharted {k} has no lane to a charted place: nobody could ever find it")

    seen_pilots = set()
    crew_types = set()
    for p in spec["crew"]:
        if p["pilot"] not in pilots:
            errors.append(f"crew {p['name']}: {p['pilot']} is no callsign pilot")
        if p["pilot"] in seen_pilots:
            errors.append(f"two crew people on {p['pilot']} (one person per callsign per crew)")
        seen_pilots.add(p["pilot"])
        if p["faction"] not in e["fac"] or p["faction"] == "None":
            errors.append(f"crew {p['name']}: unknown faction type {p['faction']}")
        crew_types.add(p["faction"])
        text(p["name"], p["name"])
        text(p["name"], p.get("story", ""))
    for missing in sorted(pilots - seen_pilots):
        errors.append(f"no crew person on {missing}: every callsign pilot brings work")

    events = {ev["id"] for ev in spec["events"]}
    job_ids = set()
    jobs = {}
    for j in spec["jobs"]:
        if j["id"] in job_ids:
            errors.append(f"job id {j['id']} is used twice")
        job_ids.add(j["id"])
        jobs[j["id"]] = j
        if j["kind"] not in e["kinds"]:
            errors.append(f"{j['id']}: unknown kind {j['kind']}")
        if j["faction"] not in e["fac"] or (j["faction"] == "None" and j["id"] != "job.return"):
            errors.append(f"{j['id']}: unknown faction type {j['faction']}")
        if j["needs"] not in e["tags"]:
            errors.append(f"{j['id']}: unknown news tag {j['needs']}")
        if j["goes"] not in e["exp"]:
            errors.append(f"{j['id']}: unknown experience {j['goes']}")
        if j["element"] not in e["elements"]:
            errors.append(f"{j['id']}: unknown element {j['element']}")
        verb = j.get("verb", "Arrive")
        if verb not in e["verbs"]:
            errors.append(f"{j['id']}: unknown verb {verb}")
        if j.get("role", "Any") not in e["roles"]:
            errors.append(f"{j['id']}: unknown role {j.get('role')}")
        if verb not in ("Arrive", "Meet") and float(j.get("goal", 0)) <= 0 and not (verb == "Hold" and j.get("goal", 0) == 0):
            errors.append(f"{j['id']}: {verb} needs a goal above 0")
        if not 0 <= float(j.get("hullFloor", 0)) <= 1:
            errors.append(f"{j['id']}: hullFloor must be 0 to 1")
        if not 0 <= int(j.get("tier", 0)) <= 2:
            errors.append(f"{j['id']}: tier must be 0 to 2")
        for k in ("onSuccess", "onFail"):
            if j[k] and j[k] not in events:
                errors.append(f"{j['id']}: {k} names unknown event {j[k]}")
        for k in ("title", "summary"):
            text(j["id"], j[k])
        for k in ("success", "fail"):
            for v in variants(j[k]):
                text(j["id"], v)
        text(j["id"], j.get("chronicle", ""))
    if "job.return" not in job_ids:
        errors.append("no job.return (the way home every board offers)")
    # Every faction type on the crew brings some work (DEPTH.md section 4: every pilot card matters).
    for f in sorted(crew_types):
        if f != "None" and not any(j["faction"] == f and j["id"] != "job.return" for j in spec["jobs"]):
            errors.append(f"faction type {f} has no job template: its pilots would bring nothing anywhere")

    for ev in spec["events"]:
        for w in ev["writes"]:
            if w["tag"] not in e["tags"] or w["at"] not in e["places"] or w["about"] not in e["abouts"]:
                errors.append(f"{ev['id']}: a write names an unknown tag, place or subject")
            if w["about"] == "AnotherOf" and w.get("aboutExperience", "Harbour") not in e["exp"]:
                errors.append(f"{ev['id']}: a write names an unknown aboutExperience")
            if w["tag"] != "None" and not variants(w.get("line", "")):
                errors.append(f"{ev['id']}: a news write with no line")
            for v in variants(w.get("line", "")):
                text(ev["id"], v)
    for s in spec["seeds"]:
        if s["port"] not in locs or s["about"] not in locs:
            errors.append(f"seed news at {s['port']} about {s['about']} names an unknown location")
        elif locs[s["port"]]["experience"] not in PORTS:
            errors.append(f"seed news at {s['port']}: only ports and harbours keep news")
        if s["tag"] not in e["tags"]:
            errors.append(f"seed news: unknown tag {s['tag']}")
        text("seed", s["line"])
    for w in spec["seedWrecks"]:
        if w["at"] not in locs:
            errors.append(f"seed wreck at unknown location {w['at']}")
        for a in w["artifacts"]:
            if a not in artifacts:
                errors.append(f"seed wreck at {w['at']}: {a} is no artifact")
    for k, v in spec.get("lines", {}).items():
        text(f"lines.{k}", v)

    rival_ids = set()
    for r in spec.get("rivals", []):
        if r["id"] in rival_ids:
            errors.append(f"rival id {r['id']} is used twice")
        rival_ids.add(r["id"])
        if r["home"] not in locs:
            errors.append(f"rival {r['id']}: unknown home {r['home']}")
        if r["faction"] not in e["fac"] or r["faction"] == "None":
            errors.append(f"rival {r['id']}: unknown faction type {r['faction']}")
        if r["domain"] not in ("Jade", "Ruby", "Gold"):
            errors.append(f"rival {r['id']}: domain must be Jade, Ruby or Gold")
        if not 1 <= int(r.get("strength", 1)) <= 3:
            errors.append(f"rival {r['id']}: strength must be 1 to 3")
        if r.get("temper", "Careful") not in e["tempers"]:
            errors.append(f"rival {r['id']}: unknown temper {r.get('temper')}")
        if not r.get("hulls"):
            errors.append(f"rival {r['id']}: no hulls")
        for k in ("name", "story", "arrive", "fall", "win"):
            text(r["id"], r.get(k, ""))

    chapter_ids = set()
    for s in spec.get("sagas", []):
        if s.get("startPort") and s["startPort"] not in locs:
            errors.append(f"saga {s['id']}: unknown startPort {s['startPort']}")
        elif s.get("startPort") and locs[s["startPort"]]["experience"] not in PORTS:
            errors.append(f"saga {s['id']}: startPort must be a port or harbour")
        relic = s.get("relic", {})
        if relic.get("kind") not in artifacts:
            errors.append(f"saga {s['id']}: relic kind {relic.get('kind')} is no artifact")
        if not relic.get("name"):
            errors.append(f"saga {s['id']}: the relic has no name")
        text(s["id"], s.get("name", ""))
        text(s["id"], relic.get("name", ""))
        text(s["id"], relic.get("lore", ""))
        chapters = s.get("chapters", [])
        if not chapters:
            errors.append(f"saga {s['id']}: no chapters")
        ids_here = {c["id"] for c in chapters}
        for c in chapters:
            if c["id"] in chapter_ids:
                errors.append(f"chapter id {c['id']} is used twice")
            chapter_ids.add(c["id"])
            if c["faction"] not in e["fac"] or c["faction"] == "None":
                errors.append(f"chapter {c['id']}: unknown faction type {c['faction']}")
            if c["job"] not in jobs:
                errors.append(f"chapter {c['id']}: unknown job {c['job']}")
            elif jobs[c["job"]]["faction"] != c["faction"]:
                errors.append(f"chapter {c['id']}: asks a {c['faction']} but {c['job']} is {jobs[c['job']]['faction']} work")
            if c.get("at") and c["at"] not in locs:
                errors.append(f"chapter {c['id']}: unknown location {c['at']}")
            if not c.get("at") and c.get("atExperience", "Harbour") not in e["exp"]:
                errors.append(f"chapter {c['id']}: unknown atExperience")
            if c.get("opens") and c["opens"] not in ids_here:
                errors.append(f"chapter {c['id']}: opens unknown chapter {c['opens']}")
            if not 0 <= int(c.get("tier", 0)) <= 2:
                errors.append(f"chapter {c['id']}: tier must be 0 to 2")
            text(c["id"], c.get("line", ""))
            text(c["id"], c.get("done", ""))
            # A chapter's work must be possible where it goes.
            if c.get("at") and c["job"] in jobs:
                verb = jobs[c["job"]].get("verb", "Arrive")
                exp = locs[c["at"]]["experience"]
                if verb == "Stow" and exp not in SEALED:
                    errors.append(f"chapter {c['id']}: a Stow at {c['at']} ({exp}) has nothing sealed to open")
                if verb == "Hold" and exp not in ("Core", "Shrine", "Nursery", "Derelict"):
                    errors.append(f"chapter {c['id']}: a Hold at {c['at']} ({exp}) has nothing to hold")
                if jobs[c["job"]]["kind"] == "Lift" and exp not in PORTS:
                    errors.append(f"chapter {c['id']}: a lift at {c['at']} ({exp}) has no ledger to lift from")
        # The chain from the first chapter ends in a finale without looping.
        if chapters:
            seen, at = set(), chapters[0]
            by_id = {c["id"]: c for c in chapters}
            while at is not None:
                if at["id"] in seen:
                    errors.append(f"saga {s['id']}: the chapters loop at {at['id']}")
                    break
                seen.add(at["id"])
                at = by_id.get(at.get("opens")) if at.get("opens") else None

    world = spec.get("world", {})
    for key, _, kind in WORLD_FIELDS:
        if key not in world:
            errors.append(f"world.{key} is missing")
    if len(world.get("tierThresholds", [])) != 2 or world.get("tierThresholds", [0, 0])[0] >= world.get("tierThresholds", [0, 1])[1]:
        errors.append("world.tierThresholds must be two rising numbers")
    if len(world.get("intensityPay", [])) != 4:
        errors.append("world.intensityPay must have four multipliers")
    if len(world.get("tierPay", [])) != 3:
        errors.append("world.tierPay must have three multipliers")
    if world.get("boardSize", 0) < 1:
        errors.append("world.boardSize must be at least 1")

    kaiju = spec.get("kaiju", {})
    for key, _, kind in KAIJU_FIELDS:
        if key not in kaiju:
            errors.append(f"kaiju.{key} is missing")
    if kaiju.get("enabled") and kaiju.get("huntJob") not in jobs:
        errors.append(f"kaiju.huntJob {kaiju.get('huntJob')} is no job")
    for key in ("name", "sighted", "moved", "felled"):
        text(f"kaiju.{key}", kaiju.get(key, ""))

    words = spec.get("words", {})
    for key, _ in WORDS_FIELDS:
        if key not in words:
            errors.append(f"words.{key} is missing")
        else:
            text(f"words.{key}", words[key])

    for k in LINES_FIELDS:
        if k not in spec.get("hud", {}):
            errors.append(f"hud.{k} is missing (an empty string deletes a line; a missing key is a typo)")
        else:
            text(f"hud.{k}", spec["hud"][k], tokens=False)
    for extra in sorted(set(spec.get("hud", {})) - set(LINES_FIELDS)):
        errors.append(f"hud.{extra} is not a VoyageLines field (a typo, or add it to LINES_FIELDS and VoyageLines)")

    toy = spec.get("toy", {})
    for key in ("cullRadius", "observeRadius", "rivalAbeam") + TOY_COLORS:
        if key not in toy:
            errors.append(f"toy.{key} is missing")
    return errors


# ── The model: the content as the C# classes hold it ─────────────────────────

def galaxy(l):
    g = l.get("galaxy")
    if g is None:
        return {"Charted": False, "GalaxyX": 0, "GalaxyY": 0, "GalaxyZ": 0}
    return {"Charted": True, "GalaxyX": g[0], "GalaxyY": g[1], "GalaxyZ": g[2]}


def to_model(spec):
    """The VoyageContent as C# holds it: field names, enums as numbers, every field written."""
    e = enums()
    m = {"Locations": [], "Lanes": [], "Crew": [], "Jobs": [], "Events": [], "Seeds": [], "SeedWrecks": []}
    for l in spec["locations"]:
        x, y, z = l["chart"]
        loc = {"Id": l["id"], "Name": l["name"], "Blurb": l.get("blurb", ""), "Experience": e["exp"][l["experience"]],
               "Home": e["domains"][l.get("home", "Blue")], "ChartX": x, "ChartY": y, "ChartZ": z}
        loc.update(galaxy(l))
        loc.update({"Cell": l["cell"], "Vaults": l.get("vaults", 0), "Finds": list(l.get("finds", [])),
                    "HoldSeconds": l.get("hold", 60), "RestPressure": l.get("restPressure", 0),
                    "RestBloom": l.get("restBloom", 0), "Uncharted": bool(l.get("uncharted", False)),
                    "RestockDays": l.get("restockDays", 0)})
        m["Locations"].append(loc)
    for lane in spec["lanes"]:
        m["Lanes"].append({"From": lane["from"], "To": lane["to"], "Cells": lane.get("cells", 1), "Profile": list(lane.get("profile", []))})
    for p in spec["crew"]:
        m["Crew"].append({"PilotId": p["pilot"], "Name": p["name"], "Faction": e["fac"][p["faction"]], "Story": p.get("story", "")})
    for j in spec["jobs"]:
        m["Jobs"].append({
            "Id": j["id"], "Kind": e["kinds"][j["kind"]], "Faction": e["fac"][j["faction"]], "Needs": e["tags"][j["needs"]],
            "Goes": e["exp"][j["goes"]], "Title": j["title"], "Summary": j["summary"], "SuccessLine": first(j["success"]),
            "FailLine": first(j["fail"]), "SuccessLines": variants(j["success"]), "FailLines": variants(j["fail"]),
            "Reward": j["reward"], "RewardElement": e["elements"][j["element"]],
            "OnSuccess": j["onSuccess"], "OnFail": j["onFail"], "Verb": e["verbs"][j.get("verb", "Arrive")],
            "Goal": j.get("goal", 1), "HullFloor": j.get("hullFloor", 0), "NoKill": bool(j.get("noKill", False)),
            "Role": e["roles"][j.get("role", "Any")], "Tier": j.get("tier", 0), "StandingDelta": j.get("standing", 1),
            "NotorietyDelta": j.get("notoriety", 0), "GoesUncharted": bool(j.get("goesUncharted", False)),
            "AvoidsOwnDomain": bool(j.get("avoidsOwnDomain", False)), "RivalsMayTake": bool(j.get("rivalsMayTake", True)),
            "ChronicleLine": j.get("chronicle", ""),
        })
    for ev in spec["events"]:
        writes = []
        for w in ev["writes"]:
            writes.append({
                "Tag": e["tags"][w["tag"]], "At": e["places"][w["at"]], "About": e["abouts"][w["about"]],
                "AboutExperience": e["exp"][w.get("aboutExperience", "Harbour")], "Line": first(w.get("line", "")),
                "Lines": variants(w.get("line", "")),
                "PressureDelta": w.get("pressure", 0), "BloomDelta": w.get("bloom", 0),
                "HolderToSquad": bool(w.get("holderToSquad", False)), "ChartsDestination": bool(w.get("charts", False)),
                "RivalGrudgeDelta": w.get("grudge", 0),
            })
        m["Events"].append({"Id": ev["id"], "Writes": writes, "SpendsNews": bool(ev.get("spendsNews", False))})
    for s in spec["seeds"]:
        m["Seeds"].append({"Port": s["port"], "Tag": e["tags"][s["tag"]], "About": s["about"], "Line": s["line"]})
    for w in spec["seedWrecks"]:
        m["SeedWrecks"].append({"At": w["at"], "Artifacts": list(w["artifacts"]), "Line": w["line"]})
    lines = spec.get("lines", {})
    m["WreckLine"] = lines.get("wreck", "")
    m["WreckNewsLine"] = lines.get("wreckNews", "")
    m["Rivals"] = [{
        "Id": r["id"], "Name": r["name"], "Faction": e["fac"][r["faction"]], "Domain": e["domains"][r["domain"]],
        "Home": r["home"], "Strength": r.get("strength", 1), "Temper": e["tempers"][r.get("temper", "Careful")],
        "Hulls": list(r.get("hulls", [])), "Story": r.get("story", ""), "ArriveLine": r.get("arrive", ""),
        "FallLine": r.get("fall", ""), "WinLine": r.get("win", ""),
    } for r in spec.get("rivals", [])]
    m["Sagas"] = [{
        "Id": s["id"], "Name": s.get("name", ""), "StartPort": s.get("startPort", ""), "StartTier": s.get("startTier", 0),
        "StartDay": s.get("startDay", 0),
        "Relic": {"Kind": s["relic"]["kind"], "Name": s["relic"]["name"], "Lore": s["relic"].get("lore", ""), "Seed": s["relic"].get("seed", "")},
        "Chapters": [{
            "Id": c["id"], "Faction": e["fac"][c["faction"]], "Tier": c.get("tier", 0), "Job": c["job"], "At": c.get("at", ""),
            "AtExperience": e["exp"][c.get("atExperience", "Harbour")], "Line": c.get("line", ""), "Opens": c.get("opens", ""),
            "DoneLine": c.get("done", ""),
        } for c in s.get("chapters", [])],
    } for s in spec.get("sagas", [])]
    world = spec.get("world", {})
    m["World"] = {field: (list(world[key]) if kind is list else world[key]) for key, field, kind in WORLD_FIELDS}
    kaiju = spec.get("kaiju", {})
    m["Kaiju"] = {field: (bool(kaiju[key]) if kind is bool else kaiju[key]) for key, field, kind in KAIJU_FIELDS}
    words = spec.get("words", {})
    m["Words"] = {field: words.get(key, "") for key, field in WORDS_FIELDS}
    return m


# ── YAML from the model ──────────────────────────────────────────────────────

def scalar(v):
    if isinstance(v, bool):
        return "1" if v else "0"
    if isinstance(v, int):
        return str(v)
    if isinstance(v, float):
        return fnum(v)
    return yaml_any(v)


def emit_field(key, value, indent):
    """One serialized field the way Unity writes it: nested classes indented, lists as `- ` items."""
    pad = " " * indent
    if isinstance(value, dict):
        out = f"{pad}{key}:\n"
        for k, v in value.items():
            out += emit_field(k, v, indent + 2)
        return out
    if isinstance(value, list):
        if not value:
            return f"{pad}{key}: []\n"
        out = f"{pad}{key}:\n"
        for item in value:
            if isinstance(item, dict):
                first = True
                for k, v in item.items():
                    if first:
                        if isinstance(v, (dict, list)):
                            raise ValueError(f"{key}: a list item's first field must be a scalar ({k})")
                        out += f"{pad}- {k}: {scalar(v)}\n"
                        first = False
                    else:
                        out += emit_field(k, v, indent + 2)
            else:
                out += f"{pad}- {scalar(item)}\n"
        return out
    return f"{pad}{key}: {scalar(value)}\n"


def emit(spec, cell_pool_block):
    model = to_model(spec)
    o = SO_HEADER.format(script=script_guid(SO_CS), name="VoyageContent")
    o += "  content:\n"
    for k, v in model.items():
        o += emit_field(k, v, 4)
    o += cell_pool_block
    o += "  lines:\n"
    hud = spec.get("hud", {})
    for k in LINES_FIELDS:
        o += f"    {k}: {yaml_any(hud.get(k, ''))}\n"
    return o


def emit_twin(spec):
    return json.dumps(to_model(spec), indent=1, ensure_ascii=True) + "\n"


CELL_POOL = re.compile(r"^  cellPool:(?: \[\]\n|\n(?:  - .*\n(?:    .*\n)*)*)", re.M)


def color(c):
    return "{" + f"r: {fnum(c[0])}, g: {fnum(c[1])}, b: {fnum(c[2])}, a: {fnum(c[3])}" + "}"


def vec3(v):
    return "{" + f"x: {fnum(v[0])}, y: {fnum(v[1])}, z: {fnum(v[2])}" + "}"


def meta_guid(rel):
    return re.search(r"^guid: ([0-9a-f]{32})", read(rel), re.M).group(1)


def emit_toy(spec):
    t = spec["toy"]
    out = SO_HEADER.format(script=script_guid(TOY_CS), name="Voyage")
    out += (f"  displayName: {yaml_any(t['displayName'])}\n  description: {yaml_any(t['description'])}\n"
            f"  accentColor: {color(t['accent'])}\n"
            f"  withArk: {{fileID: 11400000, guid: {meta_guid(ARKWAY_SETTINGS_META)}, type: 2}}\n"
            f"  content: {{fileID: 11400000, guid: {guid_for('VoyageContent.asset')}, type: 2}}\n")
    out += current_ark_escort()
    out += f"  seedElementLevel: {fnum(t['seedElementLevel'])}\n  wingmateLeash: {fnum(t['wingmateLeash'])}\n"
    out += f"  findDistance: {fnum(t['findDistance'])}\n  vaultRadius: {fnum(t['vaultRadius'])}\n"
    out += f"  vaultPrisms: {int(t['vaultPrisms'])}\n  vaultPrismScale: {vec3(t['vaultPrismScale'])}\n"
    out += f"  vaultOpenShare: {fnum(t['vaultOpenShare'])}\n  holdRadius: {fnum(t['holdRadius'])}\n"
    out += f"  cullRadius: {fnum(t['cullRadius'])}\n  observeRadius: {fnum(t['observeRadius'])}\n  rivalAbeam: {fnum(t['rivalAbeam'])}\n"
    for k in TOY_COLORS:
        out += f"  {k}: {color(t[k])}\n"
    return out


def without_toy(toybox_text):
    """The toybox asset with the voyage settings OFF its list (freestyle stands no harbour); everything else unchanged.
    The guid is still the one the settings asset was first authored under, as Toy_Voyage."""
    entry = "  - {fileID: 11400000, guid: " + guid_for("Toy_Voyage.asset") + ", type: 2}\n"
    return toybox_text.replace(entry, "")


ARK_ESCORT = re.compile(r"^  arkEscort: .*\n", re.M)


def current_ark_escort():
    full = os.path.join(ROOT, TOY)
    if os.path.exists(full):
        m = ARK_ESCORT.search(read(TOY))
        if m:
            return m.group(0)
    return "  arkEscort: {fileID: 0}\n"


def current_cell_pool():
    full = os.path.join(ROOT, OUT)
    if os.path.exists(full):
        m = CELL_POOL.search(read(OUT))
        if m:
            return m.group(0)
    return "  cellPool: []\n"


def build(spec=None):
    spec = spec or load(SPEC)
    return {
        OUT_DIR + ".meta": FOLDER_META.format(guid=guid_for("folder")),
        OUT: emit(spec, current_cell_pool()),
        OUT + ".meta": ASSET_META.format(guid=guid_for("VoyageContent.asset")),
        TWIN: emit_twin(spec),
        TOY: emit_toy(spec),
        TOY + ".meta": ASSET_META.format(guid=guid_for("Toy_Voyage.asset")),
        TOYBOX: without_toy(read(TOYBOX)),
    }


def drift(files):
    return [rel for rel, text in files.items()
            if not os.path.exists(os.path.join(ROOT, rel)) or read(rel) != text]


def self_test():
    failures = []
    spec = load(SPEC)
    if validate(spec):
        failures.append("the shipped spec does not validate: " + "; ".join(validate(spec)))

    def vault_field(s):
        return next(l for l in s["locations"] if l["experience"] == "VaultField")

    def uncharted(s):
        return next(l for l in s["locations"] if l.get("uncharted"))

    plants = {
        "an unknown lane end": lambda s: s["lanes"].append({"from": "loc.nowhere", "to": "loc.tollmoor", "cells": 1}),
        "an unreachable location": lambda s: s["locations"].append(dict(s["locations"][3], id="loc.island")),
        "a missing harbour": lambda s: s["locations"].pop(0),
        "a vault field with nothing to find": lambda s: vault_field(s).update(finds=[]),
        "a find that is no artifact": lambda s: vault_field(s).update(finds=["artifact.nope"]),
        "two people on one callsign": lambda s: s["crew"].append(dict(s["crew"][0], name="Twin")),
        "a callsign with no person": lambda s: s["crew"].pop(),
        "an unknown event": lambda s: s["jobs"][1].update(onSuccess="ev.nope"),
        "a job with no faction": lambda s: s["jobs"][1].update(faction="None"),
        "no way home": lambda s: s["jobs"].pop(0),
        "text the font cannot draw": lambda s: s["jobs"][1].update(title="carry → home"),
        "an unknown token": lambda s: s["jobs"][1].update(title="carry {nowhere}"),
        "seed news at a vault field": lambda s: s["seeds"].append({"port": "loc.lantern", "tag": "Aid", "about": "loc.verdigris", "line": "x"}),
        "a galaxy position with two numbers": lambda s: s["locations"][3].update(galaxy=[1300, 2360]),
        "a galaxy position above the disk": lambda s: s["locations"][3].update(galaxy=[1300, 2360, 900]),
        "a galaxy position in the Heart": lambda s: s["locations"][3].update(galaxy=[10, 20, 0]),
        "a missing sea name": lambda s: s["hud"].pop("SeaReef"),
        "a hud key that is no field": lambda s: s["hud"].update(NoSuchLine="x"),
        "a lane profile of the wrong length": lambda s: s["lanes"][0].update(profile=[1.0, 1.0, 1.0]),
        "a rest pressure past 3": lambda s: s["locations"][3].update(restPressure=4),
        "an uncharted port": lambda s: s["locations"][3].update(uncharted=True),
        "an uncharted place no lane reaches from charted water": lambda s: [ln.update(cells=1) for ln in s["lanes"] if False] or s["lanes"].append({"from": uncharted(s)["id"], "to": "loc.harbour.jade", "cells": 1}) or [s["lanes"].remove(ln) for ln in list(s["lanes"]) if uncharted(s)["id"] in (ln["from"], ln["to"]) and not s["locations"][0]["id"] in (ln["from"], ln["to"])] and uncharted(s).update(dummy=1) if False else s["locations"].append({"id": "loc.lost", "name": "Lost", "experience": "Lair", "chart": [0, 0, 0], "galaxy": [2000, 2000, 0], "cell": 0, "uncharted": True, "blurb": "x"}),
        "a verb with no goal": lambda s: next(j for j in s["jobs"] if j.get("verb") == "Gather").update(goal=0),
        "a hull floor past 1": lambda s: s["jobs"][2].update(hullFloor=2),
        "a tier past 2": lambda s: s["jobs"][2].update(tier=5),
        "a crew type with no work": lambda s: [s["jobs"].remove(j) for j in list(s["jobs"]) if j["faction"] == "Sport"],
        "a news write with no line": lambda s: s["events"][0]["writes"][0].update(line=""),
        "a rival at an unknown home": lambda s: s["rivals"][0].update(home="loc.nowhere"),
        "a rival of strength 4": lambda s: s["rivals"][0].update(strength=4),
        "a rival with no hulls": lambda s: s["rivals"][0].update(hulls=[]),
        "a rival with an unknown temper": lambda s: s["rivals"][0].update(temper="Sleepy"),
        "a saga chapter whose job is another type's work": lambda s: s["sagas"][0]["chapters"][0].update(job="job.courier.dispatch"),
        "a saga chapter opening an unknown chapter": lambda s: s["sagas"][0]["chapters"][0].update(opens="ch.nope"),
        "a saga that loops": lambda s: s["sagas"][0]["chapters"][2].update(opens="ch.lantern.1"),
        "a saga whose relic is no artifact": lambda s: s["sagas"][0]["relic"].update(kind="artifact.nope"),
        "a saga whose relic has no name": lambda s: s["sagas"][0]["relic"].update(name=""),
        "a saga starting at a vault field": lambda s: s["sagas"][0].update(startPort="loc.lantern"),
        "a stow chapter where nothing is sealed": lambda s: s["sagas"][3]["chapters"][0].update(at="loc.tollmoor"),
        "a kaiju hunt naming an unknown job": lambda s: s["kaiju"].update(huntJob="job.nope"),
        "a missing world rate": lambda s: s["world"].pop("boardSize"),
        "three intensity multipliers": lambda s: s["world"].update(intensityPay=[1, 2, 3]),
        "a missing word": lambda s: s["words"].pop("nemesis"),
        "a missing toy colour": lambda s: s["toy"].pop("lairColor"),
        "a line variant the font cannot draw": lambda s: s["jobs"][2].update(success=["fine", "not \u2192 fine"]),
        "a news write whose variants are all empty": lambda s: s["events"][0]["writes"][0].update(line=[]),
    }
    for what, fn in plants.items():
        s = copy.deepcopy(spec)
        fn(s)
        if not validate(s):
            failures.append(f"validate missed: {what}")
    files = build(spec)
    tampered = dict(files)
    tampered[OUT] = files[OUT].replace("Cell: 0", "Cell: 9")
    if os.path.exists(os.path.join(ROOT, OUT)) and OUT not in drift(tampered):
        failures.append("drift() did not report a tampered asset")
    # The hand-authored cell pool survives a regeneration.
    pool = "  cellPool:\n  - {fileID: 11400000, guid: 0123456789abcdef0123456789abcdef, type: 2}\n"
    text = emit(spec, pool)
    m = CELL_POOL.search(text)
    if not m or m.group(0) != pool:
        failures.append("the cell pool block is not kept as authored")
    if without_toy(without_toy(read(TOYBOX))) != without_toy(read(TOYBOX)):
        failures.append("the toybox line is not idempotent")
    if without_toy(read(TOYBOX)).count(guid_for("Toy_Voyage.asset")) != 0:
        failures.append("the voyage settings are still on the toybox list")
    if emit(spec, current_cell_pool()) != emit(spec, current_cell_pool()):
        failures.append("emit is not deterministic")
    # The twin is the model: every top-level C# field, enums as numbers, and it round-trips.
    twin = json.loads(emit_twin(spec))
    for field in ("Locations", "Lanes", "Crew", "Jobs", "Events", "Seeds", "SeedWrecks", "WreckLine", "WreckNewsLine",
                  "Rivals", "Sagas", "World", "Kaiju", "Words"):
        if field not in twin:
            failures.append(f"the twin lacks {field}")
    if twin["Jobs"] and not isinstance(twin["Jobs"][0]["Verb"], int):
        failures.append("the twin writes enums as names, not numbers")
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else f"OK ({len(plants)} planted spec defects caught)")
    return 1 if failures else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()
    spec = load(SPEC)
    problems = validate(spec)
    if problems:
        for p in problems:
            print("SPEC:", p)
        return 1
    files = build(spec)
    changed = drift(files)
    if "--check" in sys.argv:
        for rel in changed:
            print("DRIFT:", rel)
        print(f"check: {len(files)} files, {len(changed)} drifted")
        return 1 if changed else 0
    for rel in changed:
        full = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="") as f:
            f.write(files[rel])
        print("wrote", rel)
    print(f"{len(changed)} written, {len(files) - len(changed)} already current")
    return 0


if __name__ == "__main__":
    sys.exit(main())

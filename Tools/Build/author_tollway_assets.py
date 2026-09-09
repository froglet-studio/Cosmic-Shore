#!/usr/bin/env python3
"""
Authors every serialized asset the Tollway game mode needs (GameModes.Tollway = 48).

Tollway is the Scarab-only RING RACE, and the mode built on the one Scarab idea no shipped mode
had ever used: a switch pays its PLACER when ANY ball threads it, friend or enemy
(R_VesselActions/SCARAB.md 5, which calls it "the design's best idea"). Graft rings onto the
living plants growing in the court; every ball that threads one pays the pilot who planted it and
raises a 255-prism scarab-wing monument on the spot. Rings are CONSUMED when they pay, so they must be replanted - which is
exactly why the switch's charge had to start recharging (SCARAB.md 5.2, the same branch).

What this script authors is only what is genuinely Tollway's. The ball, the switch, the dais and
their tuning belong to the VESSEL and are referenced, never forked. The fauna species and the
cleanup crew are the Scramble arena's and are carried across verbatim - the cell is per-arena,
not per-mode. Two things ARE forked. The CELL CONFIG, for its volume ladder: a toll IS a monument
here, so a Tollway match raises three to five times the mass a Scramble match does and Scramble's
ladder would be crossed at both gates before the race was half run (the same argument
SCARABSCRAMBLE.md makes for why it could not inherit Astro League's). And the SPAWN PROFILE, for
the anchors: a Scarab's switch grafts onto a living plant's heart (ScarabSwitchAnchors, a VESSEL
rule that holds in every arena), so this cell has to grow plants - and Scramble's profile authors
no flora at all.

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_tollway_assets.py [--check]

--check validates without writing (CI / pre-commit use).

See Assets/_Scripts/Controller/Arcade/TOLLWAY.md for what these numbers mean.
"""
import hashlib
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "TollwayController":       guid("script/TollwayController"),
    "TollwayTollTurnMonitor":  guid("script/TollwayTollTurnMonitor"),
    "TollwayScoringRuleSO":    guid("script/TollwayScoringRuleSO"),
    "TollwaySettingsSO":       guid("script/TollwaySettingsSO"),
    "TollwayObjectiveProvider": guid("script/TollwayObjectiveProvider"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameTollway":      guid("asset/ArcadeGameTollway"),
    "TollwayScoringRule":     guid("asset/TollwayScoringRule"),
    "TollwaySettings":        guid("asset/TollwaySettings"),
    # Per-intensity: one cell config, one spawn profile and one anchor species each
    # (CellTypeChoiceOptions.IntensityWise, list order = intensity - the Rampage/Peel the Cage
    # shape). Filled in below, once ANCHOR_SPECIES is known.
    "GameToastConfigTollway": guid("asset/GameToastConfig_Tollway"),
    "ModePreviewTollway":     guid("asset/ModePreview_Tollway"),
    "MinigameTollway.unity":  guid("asset/MinigameTollway.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":          "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":       "01f934d50526431a9392a6ceca1dc33d",
    "GameToastConfigSO":      "86d1715b8f104fcc87cb60e015d4b563",
    "ModePreviewDefinitionSO": "9f1780f039eb88d45a9cd7a4a75f9e13",
    "FloraConfigurationSO":   "a32a297a7606432885f4d3e1f83bea9a",
    "SpawnProfileSO":         "e8d8aa5d835249798a256e18f2f7d912",
    # the donor's mode-specific wiring, swapped out of the cloned scene
    "ScarabScrambleController":      "360704a9d9bc44b8b122828125121f1e",
    "ScarabScrambleGoalTurnMonitor": "19433ae0bbcf410e96cec59d70d1f769",
    "ScarabScrambleScoringRule":     "5e656e2b47044242a4c2700561ebe788",
    "ScarabScrambleSettings":        "9151506d4c52d18a288fd54a99ef64b0",
    "ScarabScrambleCellConfig":      "4c79db5a1791ff3060644ec7d2aee0db",
    # reused arena content (the cell is per-ARENA, not per-mode)
    "ScarabScrambleSpawnProfile": "01b40762f7abf41e7e4b1eb70273090d",
    # the donor profile's three-species cleanup crew, carried across verbatim
    "ScrambleBrittlestarFauna": "2d82ef8cbefd205db7a329bb37d2a79c",
    "ScramblePiranhaFauna":     "eceaaeea8d613d2053f8d39ea6d56e50",
    "ScrambleTadpoleFauna":     "508c59d3c640345f9eda3929935fbf66",
    # The four ANCHOR SPECIES, one per intensity - see ANCHOR_SPECIES for why they are four
    # different GROWTH FAMILIES rather than four variations on one.
    "SpireFloraPrefab":        "becb04107104ecb6768ae2d6766e681e",
    "SpireFloraCharge":        "25e619766581c4807de09c87fddf877c",
    "SpireFloraMass":          "10859a22ceb9349219eabe30541f5341",
    "SpireFloraSpace":         "f46966ea66e278b0ad293ccecc672c1d",
    "SpireFloraTime":          "6771e101fe9fa81ae0161ecbffc4c901",
    "GyroidFloraPrefab":       "a84d160ac0bcaf94da22c5368e4d3962",
    "GyroidFloraCharge":       "a343fb01cc5b439ca5fa66beacf1a817",
    "GyroidFloraMass":         "bcb09b7b37a24707a3934db2bf658d5f",
    "GyroidFloraSpace":        "016e18462b534f838d81604470e4962e",
    "GyroidFloraTime":         "f27c9fe8e43d4c8bb0c7ea84c9f8050d",
    "CactiFloraPrefab":        "82e04cfd7f5b93f478b5fbfe40c2a2d7",
    "CactiFloraCharge":        "58355313138d44a18893054ecabf494f",
    "CactiFloraMass":          "3c7234ddfb27413fa43e7640d54027c5",
    "CactiFloraSpace":         "e2677134f5ac45c5985e5c24cdf31a20",
    "CactiFloraTime":          "d182188565e8421bb0fa6a13dfebb0bb",
    "QuasicrystalFloraPrefab": "eff83db54b6d4f7d98bfc3b42b8d1487",
    "QuasicrystalFloraCharge": "fcf139eca19845ecbb5cea2d491214c6",
    "QuasicrystalFloraMass":   "47624b4efa994b9296f16723e5c87482",
    "QuasicrystalFloraSpace":  "f2677d56069e48aa85354b70616a650b",
    "QuasicrystalFloraTime":   "d8ebac2e7c7b4e64ad1e307c036b4a3f",
    "CellIcon":        "6aa1c06e11b265744a5f9fa8858ac72a",
    "MembranePrefab":  "6e330f85972faf843b8a128e7166f7b5",
    "NucleusPrefab":   "b9cf1833fa2493d4b8724ccb6740fb3a",
    "CytoplasmPrefab": "9cacd903fcf4643459f5f14ac811bb20",
    # shared content
    "Vessel_Scarab":   "b136d82d275e0f8ea1feef29f0d416a4",
}

# ── The race ─────────────────────────────────────────────────────────────────
# TOLLS a domain needs to win. RE-DERIVED TWICE. First when rings stopped being placeable
# anywhere and became ANCHORED (ScarabSwitchAnchors): a toll used to be "plant a ring in front of
# your ball and nudge", which is one move, and became "fly to a plant, plant facing the line you
# want, then drive a ball across the court and through a mouth two dozen units wide". Then again
# on 2026-09-06, when the switch went to ONE ring at a time on a 60-second recharge
# (PlaceSwitchActionSO): a pilot now has at most one scoring surface standing, so a toll is
# several times the work again and 8 of them is a grind rather than a race. Kept in sync with
# EndConditionOverridesSO.DefaultTollwayTollTarget.
TOLL_TARGET = 4

# The comeback strength - a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`), which is
# exactly why it has moved with the target every time. At the 8-toll race 0.75 bought 1.5 element
# levels for a quarter-of-target deficit; halving the target to 4 halves that deficit, so the rate
# doubles to hold the same felt comeback. The trap this guards has now bitten six modes; the
# assert below is the gate.
COMEBACK_RATE = 1.5

# ── The court, and the ANCHOR FLORA studding it ──────────────────────────────
# A ring may only be grafted onto a LIVING PLANT'S HEART - a vessel rule now
# (ScarabSwitchAnchors), not a mode one - so this arena's job is to grow the anchors. The
# mode-owned toll-post system this replaces built, replicated and drew a set of sockets that
# existed in exactly one mode; a flora crystal is the same affordance the ecology already
# produces everywhere, and it is also food, a joustable heart and an element reward.
COURT_RADII = [480, 560, 640, 720]

# The membrane the Scramble/Tollway cell wears (CapsuleMembrane.prefab, radius 1200). Planting
# fractions are of the MEMBRANE, so this is what turns a court fraction into an authored one.
MEMBRANE_RADIUS = 1200.0

# The anchor field, expressed against the SMALLEST court (intensity 1) so the plants are inside
# the arena at every intensity. Same band the retired toll posts used - 0.4..0.85 of that court - held
# off the middle because the core is where balls are forged and crystals respawn, and off the
# wall so a ball can come at a ring from behind. The consequence at higher intensities is
# deliberate and worth stating: the court grows and the anchor field does not, so a bigger court
# is more open water around the same contested middle, which is what "traffic goes up" means here.
ANCHOR_COURT_INNER, ANCHOR_COURT_OUTER = 0.4, 0.85
ANCHOR_INNER = round(ANCHOR_COURT_INNER * COURT_RADII[0] / MEMBRANE_RADIUS, 4)   # 0.16
ANCHOR_OUTER = round(ANCHOR_COURT_OUTER * COURT_RADII[0] / MEMBRANE_RADIUS, 4)   # 0.34

# ── ONE FLORA FAMILY PER INTENSITY ──────────────────────────────────────────
# Intensity here is TRAFFIC (court radius up, crystal count down), and the anchor field is the
# one thing a pilot reads the court by - so each of the four settings grows a COMPLETELY
# DIFFERENT KIND OF PLANT, not four variations on one. The project ships three growth families
# and all three are represented:
#
#   Spire         PhyllotacticFlora   a collared pillar, grown from a root
#   Gyroid        AssembledFlora      a triply-periodic minimal surface of plates
#   Cacti         BranchingFlora      a squat branching cactus of fat 5x5x3 pads
#   Quasicrystal  AssembledFlora      an aperiodic cage of long thin struts
#
# Gyroid and Quasicrystal share a growth COMPONENT and share nothing a player can see: one is a
# smooth surface of 7x4.5x3.5 plates, the other a needle cage whose struts run to 44 units. The
# family assert below is on the component (three families across four intensities is the most the
# project can offer); the LOOK is what the roster is actually chosen for.
#
# Ordered by STANDING VOLUME, which here is also roughly "how big it reads": the court grows
# 480 -> 720 and the marker grows with it.
#
# ── AND THE RING-MOUTH RULE THIS REPLACES IS RETIRED. ───────────────────────
# The previous pass picked its roster by proving each species' body rises OUT of the 24u ring
# planted at its heart, and rejected four phyllotactic forms for filling their own mouth. That
# rule was measuring something real and gating on something that does not exist:
#
#     "The ball NEVER physically collides with prisms - it passes through ALL of them and
#      resolves them by domain via a per-tick spatial scan"  - AstroLeagueBall
#
# A ball cannot be blocked by a plant, so a plant cannot block its own ring, so no flora geometry
# can make an anchor unthreadable. What a plant's mass in the mouth actually does is get RESOLVED
# BY DOMAIN as the ball passes: an opposing plant is destroyed, an own-domain one takes a shield.
# That is the food web and the scoring meeting each other, and it is good. The roster is
# therefore chosen for how the four LOOK, and the geometry that constrains it is the volume
# ladder below.
#
# General rule worth carrying: **before gating a design on a clearance, find out what actually
# has to pass through the gap** - the thing that threads a Tollway ring is the one object in the
# game with no prism collision at all.
ANCHOR_SPECIES = ["Spire", "Gyroid", "Cacti", "Quasicrystal"]

# One cell config, one spawn profile and one anchor-flora config per intensity. Named the way
# every other IntensityWise mode names them (`<Mode> Cell Config 1..4`), so the folder reads the
# same as Rampage's and Peel the Cage's. The flora config is keyed on the SPECIES rather than the
# index, so swapping the roster mints new guids for the new species and leaves the old ones to be
# retired by STALE_PATHS rather than silently rewriting a Reed asset into a Gyroid one.
for _i, _sp in enumerate(ANCHOR_SPECIES, start=1):
    G_ASSET[f"TollwayCellConfig{_i}"] = guid(f"asset/TollwayCellConfig{_i}")
    G_ASSET[f"TollwaySpawnProfile{_i}"] = guid(f"asset/TollwaySpawnProfile{_i}")
    G_ASSET[f"TollwayAnchorFlora{_sp}"] = guid(f"asset/TollwayAnchorFlora{_sp}")

# A per-plant prism budget the CELL imposes, or None to keep the species' own.
#
# A LATTICE species keeps its own: its budget is GEOMETRY (a gyroid octagon is 24 prisms around
# one crystal, a quasicrystal heart cell is one vertex's tree of struts), so a cell-imposed
# number does not thin the plant, it truncates a shape mid-figure - "plant COUNT is the only
# lever" (`Docs/ECOSYSTEM.md` 32.7/36). Spire and Cacti grow to whatever budget they are given,
# and 40 keeps them markers rather than scenery.
ANCHOR_PRISM_BUDGET = {"Spire": 40, "Gyroid": None, "Cacti": 40, "Quasicrystal": None}

LIFEFORM_DIR = "Assets/_SO_Assets/Lifeforms"
FLORA_PREFAB_DIR = "Assets/_Prefabs/FloraAndFauna"
SCRIPTS_DIR = "Assets/_Scripts"
ELEMENTS = ("Charge", "Mass", "Space", "Time")

# The AI's own aiming window - how close a free heart must be to its nose-line before it presses.
# Deliberately NOT the vessel's anchorReach: being wrong either way is free (see TollwaySettingsSO).
AI_ANCHOR_AIM_TOLERANCE = 70.0

# Seconds between an AI's switch placements. A FUNCTION of the vessel's own recharge (asserted
# below): pace it under that and two presses in three are refused, and an AI that cannot plant a
# ring cannot score at all in this mode.
AI_SWITCH_INTERVAL = 66.0

DAIS_VOLUME = 50773          # measured, SCARAB.md 5.1
DAIS_PRISMS = 255
TRAIL_BAND_RESTLESS = 12000  # Scramble's pre-dais trail-only estimate
TRAIL_BAND_FRENZY = 36000
COUNT_BAND_RESTLESS = 900
COUNT_BAND_FRENZY = 3000
COUNT_HEADROOM = 1.6         # the ~1.6x Scramble's own count backstops carry

# The monument budget, expressed as a fraction of a MAXIMUM-LENGTH match. A match raises at most
# `TOLL_TARGET + 2 * (TOLL_TARGET - 1)` daises - the winner's tolls plus two losing domains one
# short of the target - so both gates are stated as fractions of that rather than as constants
# that silently stop meaning anything when the target moves. (They did: an earlier pass hard-
# coded a literal 8 in one of the asserts below, which WAS the toll target at the time and became
# a meaningless number the moment the target halved. Same shape as the comeback-rate trap.)
MAX_MATCH_DAISES = TOLL_TARGET + 2 * (TOLL_TARGET - 1)
RESTLESS_DAISES = max(1, round(MAX_MATCH_DAISES * 0.27))   # the crew arrives about a quarter in
FRENZY_DAISES = max(RESTLESS_DAISES + 2, round(MAX_MATCH_DAISES * 0.73))


def _round_to(value: int, step: int) -> int:
    return int(round(value / step) * step)


def _num(value: float) -> str:
    """Unity serializes a whole float as `1`, not `1.0`; matching it keeps the next in-editor
    save from producing a spurious diff on a file this generator owns."""
    return str(int(value)) if float(value).is_integer() else str(value)


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def _leaf_volume(text: str) -> "float | None":
    """The authored leaf volume, or None when this asset does not author one.

    A ZERO vector is not a leaf, it is `FloraVariantTuning`'s documented "sentinel: keep" - so it
    reads as None and the caller falls through to the prefab, exactly as the runtime would. Every
    Cacti element but Charge simply omits the block, and Charge authors the sentinel; averaging
    that in as a real 0 priced the species at 56.25 volume against its true 75, which is the shape
    of bug this whole measurement layer exists to make impossible.
    """
    m = re.search(r"[Ll]eafSize: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", text)
    if not m:
        return None
    x, y, z = (float(g) for g in m.groups())
    v = x * y * z
    return v if v > 0 else None


# ── Everything below is READ from the shipped assets, never transcribed ─────
# Four families do not share a shape of authoring, so each fact is looked up where that family
# actually keeps it and the fallback order is explicit:
#
#   leaf volume   the element asset's variant tuning, else the PREFAB's own leafSize
#                 (BranchingFlora species author no per-element variant geometry at all, so a
#                  Cacti is 5x5x3 for every element while a Gyroid is four different plates)
#   prism budget  the cell's override when it imposes one, else the element variant's, else the
#                 prefab's
#   family        the prefab's own script guid, resolved to a class name
#
# A species whose leaf could not be found ANYWHERE would silently price its forest at zero, so
# every lookup below asserts rather than defaulting.
ANCHOR_LEAF_VOLUME = {}
ANCHOR_SITES = {}
ANCHOR_FAMILY = {}
ANCHOR_COMPONENT_FILEID = {}
ANCHOR_BUDGET = {}

_script_names = {}
for _dirpath, _dirnames, _filenames in os.walk(os.path.join(ROOT, SCRIPTS_DIR)):
    for _fn in _filenames:
        if not _fn.endswith(".cs.meta"):
            continue
        with open(os.path.join(_dirpath, _fn), encoding="utf-8", errors="ignore") as _fh:
            _m = re.search(r"^guid: ([0-9a-f]{32})", _fh.read(), re.M)
        if _m:
            _script_names[_m.group(1)] = _fn[:-len(".cs.meta")]

# Which of those classes are actually flora GROWTH RULES. Read from the declaration rather than
# assumed from the file name, because two prefabs in the flora folder carry components that are
# not Flora at all (SeaweedFlora's SegmentSpawner, oldWallFlora's GyroidAssembler) and counting
# them would inflate "how many families does the project ship" by two.
_flora_subclasses = set()
for _dirpath, _dirnames, _filenames in os.walk(os.path.join(ROOT, SCRIPTS_DIR)):
    for _fn in _filenames:
        if not _fn.endswith(".cs"):
            continue
        with open(os.path.join(_dirpath, _fn), encoding="utf-8", errors="ignore") as _fh:
            for _m in re.finditer(r"^\s*(?:public |internal |abstract |sealed )*class (\w+)\s*:\s*Flora\b",
                                  _fh.read(), re.M):
                _flora_subclasses.add(_m.group(1))
assert _flora_subclasses, "found no Flora subclasses - the family assert below would be vacuous"

for _sp in ANCHOR_SPECIES:
    _prefab = read(f"{FLORA_PREFAB_DIR}/{_sp}Flora.prefab")
    _prefab_leaf = _leaf_volume(_prefab)
    _prefab_budget = re.search(r"^  maxTotalSpawnedObjects: (\d+)", _prefab, re.M)

    _m = re.search(r"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})", _prefab, re.M)
    assert _m, f"{_sp}Flora.prefab has no growth component"
    ANCHOR_FAMILY[_sp] = _script_names.get(_m.group(1), _m.group(1))

    _vols, _sites, _budgets = [], set(), []
    for _e in ELEMENTS:
        _asset = read(f"{LIFEFORM_DIR}/{_sp} Flora {_e}.asset")
        # The component fileID of the FloraPrefab reference differs by family
        # (PhyllotacticFlora/BranchingFlora vs AssembledFlora), so it is copied from the shipped
        # element asset rather than assumed - a wrong fileID resolves to no component at all.
        _fid = re.search(r"FloraPrefab: \{fileID: (\d+),", _asset)
        assert _fid, f"{_sp} Flora {_e} has no FloraPrefab reference"
        ANCHOR_COMPONENT_FILEID.setdefault(_sp, _fid.group(1))
        assert ANCHOR_COMPONENT_FILEID[_sp] == _fid.group(1), \
            f"{_sp}'s four elements disagree about the flora component fileID"

        _v = _leaf_volume(_asset)
        _vols.append(_v if _v is not None else _prefab_leaf)
        # -1 is the same "sentinel: keep" as the zero leaf above, so it falls through to the
        # prefab BY RULE rather than by a `\d+` that happens not to match a minus sign.
        _b = re.search(r"^    MaxTotalSpawnedObjects: (-?\d+)", _asset, re.M)
        _authored = int(_b.group(1)) if _b else -1
        _budgets.append(_authored if _authored > 0
                        else (int(_prefab_budget.group(1)) if _prefab_budget else None))
        _s = re.search(r"^  PreferredSites: (\d+)", _asset, re.M)
        if _s:
            _sites.add(int(_s.group(1)))

    assert all(v is not None for v in _vols), \
        f"{_sp} authors no leaf size on its elements OR its prefab - its forest would price at 0"
    ANCHOR_LEAF_VOLUME[_sp] = round(sum(_vols) / len(_vols), 2)
    assert len(_sites) <= 1, f"{_sp}'s elements disagree about PreferredSites: {_sites}"
    ANCHOR_SITES[_sp] = _sites.pop() if _sites else None

    _override = ANCHOR_PRISM_BUDGET[_sp]
    if _override is None:
        assert len(set(_budgets)) == 1 and _budgets[0], \
            f"{_sp} keeps its own geometry budget but its elements disagree: {_budgets}"
        ANCHOR_BUDGET[_sp] = _budgets[0]
    else:
        ANCHOR_BUDGET[_sp] = _override

# How many anchors the court offers. ONE number for every intensity: intensity is court radius,
# crystal count and SPECIES, and a field that also thinned with intensity made another axis out
# of one. 14 sits in the middle of the 10..16 the retired toll posts used.
ANCHOR_PLANTS = 14

# Seconds between re-seed ticks. A grazed anchor is a removed scoring surface, so it has to come
# back briskly - but by SEEDING, never by a respawn timer on a specific plant.
ANCHOR_RESEED_SECONDS = 20

# Standing anchor mass, per intensity. BOTH the count and the volume vary now, because the four
# species differ in how many prisms they grow AND how big each one is - so both ladders below are
# per-intensity, where the single-species pass could share one count ladder across all four.
ANCHOR_PRISMS = {sp: ANCHOR_PLANTS * ANCHOR_BUDGET[sp] for sp in ANCHOR_SPECIES}
ANCHOR_VOLUME = {sp: int(round(ANCHOR_PRISMS[sp] * ANCHOR_LEAF_VOLUME[sp]))
                 for sp in ANCHOR_SPECIES}

# ── The volume ladder (the one thing the cell config is forked for) ──────────
# A spent switch raises a scarab-wing dais: 255 prisms, 50,773 box volume (SCARAB.md 5.1). In
# Scramble that is a rare event, so its ladder is "the trail band plus 3 and 7 spent switches".
# In Tollway a toll IS a dais, so the ladder has to be stated in the currency the match actually
# runs on. The trail band and the count headroom are Scramble's, unchanged - only the number of
# monuments and the standing anchor forest differ.
def restless_enter_volume(sp):
    return _round_to(TRAIL_BAND_RESTLESS + ANCHOR_VOLUME[sp] + RESTLESS_DAISES * DAIS_VOLUME, 1000)


def frenzy_enter_volume(sp):
    return _round_to(TRAIL_BAND_FRENZY + ANCHOR_VOLUME[sp] + FRENZY_DAISES * DAIS_VOLUME, 1000)



# The COUNT ladder is the rare frenzy/perf backstop, and it is per-species for the same reason:
# a Quasicrystal anchor field is 1,540 prisms against a Gyroid's 420, so one shared count would
# be four times too tight at one end and slack at the other.
def restless_enter_count(sp):
    return _round_to(
        int(COUNT_BAND_RESTLESS + ANCHOR_PRISMS[sp] + RESTLESS_DAISES * DAIS_PRISMS * COUNT_HEADROOM),
        10)


def frenzy_enter_count(sp):
    return _round_to(
        int(COUNT_BAND_FRENZY + ANCHOR_PRISMS[sp] + FRENZY_DAISES * DAIS_PRISMS * COUNT_HEADROOM),
        10)

# ── The crystal economy: INTENSITY IS TRAFFIC ────────────────────────────────
# Intensity here is how much is flying around, because traffic is what pays tolls. Low intensity
# is the party setting - a small court thick with balls, where a ring pays often and placement is
# forgiving. High intensity is the sweaty one - a big court and barely any balls, where every ring
# has to be aimed at a line somebody will actually fly. CrystalCountMode.IntensityScaled (2), the
# Rampage shape: max(1, round(players x CrystalsPerPlayer) + ExtraCrystals).
CRYSTALS_BY_INTENSITY = [
    (1.0, 3),   # I1 - 4 players -> 7
    (1.0, 2),   # I2 -> 6
    (0.75, 1),  # I3 -> 4
    (0.5, 0),   # I4 -> 2
]

_HEADER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def HEADER_FOR(script_guid: str, name: str) -> str:
    return _HEADER_TMPL.replace("__GUID__", script_guid).replace("__NAME__", name)


def meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


# ── 1. .cs.meta for the scripts assets/scenes point at ───────────────────────
SCRIPT_PATHS = {
    "TollwayController":        "Assets/_Scripts/Controller/Arcade/Tollway/TollwayController.cs",
    "TollwayScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Tollway/TollwayScoringRuleSO.cs",
    "TollwaySettingsSO":        "Assets/_Scripts/Controller/Arcade/Tollway/TollwaySettingsSO.cs",
    "TollwayObjectiveProvider": "Assets/_Scripts/Controller/Arcade/Tollway/TollwayObjectiveProvider.cs",
    "TollwayTollTurnMonitor":   "Assets/_Scripts/Controller/Arcade/TurnMonitors/TollwayTollTurnMonitor.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 4 = ScoringMetric.Goals, points not golf - the Astro League / Scramble race shape.
# Tollway reuses the metric because the SHAPE of the race is the same; what differs is only what
# a goal IS (a ball through a ring a PILOT planted, which pays that pilot and is consumed).
emit("Assets/_SO_Assets/Scoring Rules/TollwayScoringRule.asset",
     HEADER_FOR(G_SCRIPT["TollwayScoringRuleSO"], "TollwayScoringRule") +
     "  metric: 4\n  golfRules: 0\n")
emit("Assets/_SO_Assets/Scoring Rules/TollwayScoringRule.asset.meta",
     asset_meta(G_ASSET["TollwayScoringRule"]))


# ── 3. Mode settings ─────────────────────────────────────────────────────────
# Court radius climbs with intensity (a bigger court is harder to cover, and covering lines is
# the skill). Nothing about the SWITCH is here - that is PlaceSwitchAction.asset's, because a
# Scarab plants rings in freestyle and in Scramble too and a second author would drift. Nor is
# the ANCHOR RULE: where a ring may go is the vessel's (ScarabSwitchAnchors), and the anchors
# themselves are the cell's spawn profile. What is left here is the AI's aiming window.
emit("Assets/_SO_Assets/Games/TollwaySettings.asset",
     HEADER_FOR(G_SCRIPT["TollwaySettingsSO"], "TollwaySettings") + """  courtRadiusByIntensity: 0100000000000000
  aiAnchorAimTolerance: __AI_AIM__
  chainWindowSeconds: 4
  faunaWaitOutsideCourt: 1
  faunaExclusionCourtFraction: 1
  faunaExclusionSweepSeconds: 3
  aiRetargetSeconds: 1
  aiApproachLead: 45
  aiInterceptLeadSeconds: 0.5
  aiSwitchIntervalSeconds: __AI_INTERVAL__
  aiFirstSwitchDelaySeconds: 5
""".replace("  courtRadiusByIntensity: 0100000000000000\n",
            "  courtRadiusByIntensity:\n" + "".join(f"  - {_num(r)}\n" for r in COURT_RADII))
   .replace("__AI_AIM__", _num(AI_ANCHOR_AIM_TOLERANCE))
   .replace("__AI_INTERVAL__", _num(AI_SWITCH_INTERVAL)))
emit("Assets/_SO_Assets/Games/TollwaySettings.asset.meta",
     asset_meta(G_ASSET["TollwaySettings"]))



# ── 3b. The ANCHOR SPECIES, and the per-intensity cells that seed them ──────
# The mode needs points a ring can be planted on. It does NOT build them: a Scarab grafts its
# switch onto a living plant's heart in every arena (ScarabSwitchAnchors), so all this cell has
# to do is grow plants - which is the Cell's ordinary job, through the ordinary spawner, with
# the plants ordinary food-web citizens (grazeable, joustable, crystal-dropping) from the frame
# they exist. That is the whole reason the toll-post system was deleted rather than tuned.
#
# ONE SPECIES PER INTENSITY (2026-09-06), through CellTypeChoiceOptions.IntensityWise - the
# platform way, list order = intensity, the same shape Rampage and Peel the Cage use. Intensity
# in this mode is TRAFFIC, and the anchor field is what a pilot reads the court by, so the marker
# grows with the court (see ANCHOR_SPECIES). It costs three more cell configs and three more
# spawn profiles and buys an arena that is visibly a different place at each setting rather than
# the same court at four sizes.
#
# NetworkSynced is LOAD-BEARING on every one of them and is why the species are forked rather
# than referenced: a switch placement re-executes on every peer, so every peer has to agree about
# where the anchors are, and flora are per-peer by default (each machine runs its own spawner off
# its own UnityEngine.Random). FloraNetworkSync replicates the planting DECISION - species, root
# pose, domain and element - which is exactly the set the heart's world position is a function
# of. This is the first shipped user of that mechanism; the Tollway scene's cell already carries
# the component (it came across with the Scramble clone).
#
# SpreadElements over each species' four canonical assets rather than one element, because a
# heart IS an element reward: jousting an anchor kills the plant, frees its crystal and removes a
# scoring surface, which is real counter-play, and a court of four different hearts pays four
# different amounts for it. The cell-level overrides win over the rolled palette sibling
# (FloraConfigurationSO.TryBuildCellOverrideTuning), which is what lets these cells pick the band
# and the plant size without forking sixteen element assets.
ANCHOR_FLORA_DIR = "Assets/_SO_Assets/Cell Configs/Tollway Cell"

for _i, _sp in enumerate(ANCHOR_SPECIES, start=1):
    _flora_guid = G_ASSET[f"TollwayAnchorFlora{_sp}"]
    _palette = "".join(
        f"  - {{fileID: 11400000, guid: {EXISTING[f'{_sp}Flora{_e}']}, type: 2}}\n"
        for _e in ELEMENTS)
    emit(f"{ANCHOR_FLORA_DIR}/Tollway Anchor Flora {_sp}.asset",
         HEADER_FOR(EXISTING["FloraConfigurationSO"], f"Tollway Anchor Flora {_sp}") +
         f"""  FloraPrefab: {{fileID: {ANCHOR_COMPONENT_FILEID[_sp]}, guid: {EXISTING[f'{_sp}FloraPrefab']}, type: 3}}
  NetworkSynced: 1
  SpawnProbability: 1
  InitialSpawnCount: {ANCHOR_PLANTS}
  OverrideDefaultPlantPeriod: 1
  NewPlantPeriod: {ANCHOR_RESEED_SECONDS}
  PopulationSize: {ANCHOR_PLANTS}
  MaxLivePopulation: {ANCHOR_PLANTS}
  GrowthPerOffspring: 0
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 5
  MaturityFraction: 0
  OffspringSpread: 60
  PreferredSites: {ANCHOR_SITES[_sp] if ANCHOR_SITES[_sp] is not None else 31}
  Element: 0
  Variant:
    Enabled: 0
  SpreadElements: 1
  ElementPalette:
{_palette}  PlantRadiusCellFractionMaxOverride: {_num(ANCHOR_OUTER)}
  PlantRadiusCellFractionMinOverride: {_num(ANCHOR_INNER)}
  MaxTotalSpawnedObjectsOverride: {ANCHOR_PRISM_BUDGET[_sp] if ANCHOR_PRISM_BUDGET[_sp] is not None else -1}
""")
    emit(f"{ANCHOR_FLORA_DIR}/Tollway Anchor Flora {_sp}.asset.meta", asset_meta(_flora_guid))

    # The profile is a fork of Scramble's for ONE reason - it grows the anchors - so every fauna
    # number is carried across verbatim rather than re-derived. The cleanup crew is the same
    # three species at every intensity, waiting outside the court until the volume ladder leaves
    # Calm; only the flora entry differs.
    emit(f"{ANCHOR_FLORA_DIR}/Tollway Spawn Profile {_i}.asset",
         HEADER_FOR(EXISTING["SpawnProfileSO"], f"Tollway Spawn Profile {_i}") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras:
  - {{fileID: 11400000, guid: {_flora_guid}, type: 2}}
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 8
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 30
  FaunaFoodFloor: 5
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 2
  SupportedFaunas:
  - {{fileID: 11400000, guid: {EXISTING['ScrambleBrittlestarFauna']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['ScramblePiranhaFauna']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['ScrambleTadpoleFauna']}, type: 2}}
""")
    emit(f"{ANCHOR_FLORA_DIR}/Tollway Spawn Profile {_i}.asset.meta",
         asset_meta(G_ASSET[f"TollwaySpawnProfile{_i}"]))


# ── 4. Cell configs, one per intensity (forked from Scramble's for the LADDER) ─
CELL_DESC_TMPL = (
    "Ring-court cell for Tollway, intensity {i} of 4 (CellTypeChoiceOptions.IntensityWise, list "
    "order = intensity). Scarab Scramble's arena - the nucleus IS the sphere court (play "
    "geometry, not a claim; the controller clears NucleusIsControlZone) and the same three-species "
    "cleanup crew waits outside it until the volume ladder leaves Calm - forked for exactly TWO "
    "reasons. (1) ANCHORS: a Scarab's switch grafts onto a living plant's heart, so this cell "
    "grows {plants} NetworkSynced {species} plants ({family}) in a band inside the court and those "
    "hearts ARE the scoring sockets. Scramble authors no flora at all, so its profile could not "
    "be reused, and the SPECIES is what makes each intensity a different-looking place - the four "
    "are a DIFFERENT KIND of plant rather than four variants of one, and they grow with the court "
    "since a marker further away has to read larger. (2) The LADDER. In Scramble "
    "a switch dais is a rare event; here a TOLL IS A DAIS, so a match raises three to five times "
    "the mass and Scramble's gates (Restless 164,000 / Frenzy 391,000) would both be crossed "
    "before the race was half run, after which the ladder conveys nothing. Restated in the "
    "currency this mode runs on: the trail band plus the standing anchor forest ({volume} volume, "
    "{prisms} prisms) plus {restless} monuments for Restless and {frenzy} for Frenzy, at 50,773 "
    "volume and 255 prisms each (SCARAB.md 5.1), out of the {maxdaises} a maximum-length "
    "{target}-toll match can raise. ESTIMATE pending the in-editor baseline measure; see "
    "TOLLWAY.md."
)
for _i, _sp in enumerate(ANCHOR_SPECIES, start=1):
    emit(f"{ANCHOR_FLORA_DIR}/Tollway Cell Config {_i}.asset",
         HEADER_FOR(EXISTING["CellConfigDataSO"], f"Tollway Cell Config {_i}") + f"""  CellName: Tollway
  Description: {CELL_DESC_TMPL.format(i=_i, plants=ANCHOR_PLANTS, species=_sp,
                                      family=ANCHOR_FAMILY[_sp],
                                      volume=ANCHOR_VOLUME[_sp], prisms=ANCHOR_PRISMS[_sp],
                                      restless=RESTLESS_DAISES, frenzy=FRENZY_DAISES,
                                      maxdaises=MAX_MATCH_DAISES, target=TOLL_TARGET)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: 2
  CellEndGameScore: 0
  MembranePrefab: {{fileID: 346633111830028674, guid: {EXISTING['MembranePrefab']}, type: 3}}
  NucleusPrefab: {{fileID: 7555898194514117247, guid: {EXISTING['NucleusPrefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: 639495419069806261, guid: {EXISTING['CytoplasmPrefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'TollwaySpawnProfile{_i}']}, type: 2}}
  SenseRadiusOverride: 1300
  PhaseThresholds:
    RestlessEnter: {restless_enter_count(_sp)}
    RestlessExit: {restless_enter_count(_sp) - 100}
    FrenzyEnter: {frenzy_enter_count(_sp)}
    FrenzyExit: {frenzy_enter_count(_sp) - 210}
    RestlessEnterVolume: {restless_enter_volume(_sp)}
    RestlessExitVolume: {restless_enter_volume(_sp) - 4000}
    FrenzyEnterVolume: {frenzy_enter_volume(_sp)}
    FrenzyExitVolume: {frenzy_enter_volume(_sp) - 6000}
""")
    emit(f"{ANCHOR_FLORA_DIR}/Tollway Cell Config {_i}.asset.meta",
         asset_meta(G_ASSET[f"TollwayCellConfig{_i}"]))


# The single-config pass this replaced. The generator cannot leave them behind: a stray
# CellConfigDataSO with a live GUID is still a valid drop target and still resolves in any scene
# that happens to reference it, so a half-migrated folder is indistinguishable from a working one.
STALE_PATHS = [
    "Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Cell Config.asset",
    "Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Cell Config.asset.meta",
    "Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Spawn Profile.asset",
    "Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Spawn Profile.asset.meta",
    "Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Anchor Flora.asset",
    "Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Anchor Flora.asset.meta",
]
# ...plus every anchor-flora asset on disk that the CURRENT roster does not emit. Derived rather
# than listed, so swapping a species retires its predecessor without anyone remembering to: the
# 2026-09-06 roster (Reed/Spire/Lantern/Arbor) left four of these behind when it became
# Spire/Gyroid/Cacti/Quasicrystal, and a stale FloraConfigurationSO is a live asset the editor
# will happily keep resolving.
if os.path.isdir(os.path.join(ROOT, ANCHOR_FLORA_DIR)):
    _wanted = {f"Tollway Anchor Flora {sp}.asset" for sp in ANCHOR_SPECIES}
    for _fn in sorted(os.listdir(os.path.join(ROOT, ANCHOR_FLORA_DIR))):
        _base = _fn[:-len(".meta")] if _fn.endswith(".meta") else _fn
        if _base.startswith("Tollway Anchor Flora ") and _base not in _wanted:
            STALE_PATHS.append(f"{ANCHOR_FLORA_DIR}/{_fn}")


# ── 5. Arcade game config ────────────────────────────────────────────────────
# SCARAB ONLY: a single entry in Vessels drives all three enforcement layers (the launcher clamp,
# the server-side spawn clamp, and the AI clamp). MinDomainsAllowed 2 because a toll race needs a
# rival - a one-domain lobby is a building exercise with nobody to lose to.
emit("Assets/_SO_Assets/Games/ArcadeGameTollway.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameTollway") + f"""  Mode: 48
  IsMultiplayer: 1
  DisplayName: Tollway
  Description: Plants grow in the court, and a ring can only be grafted onto one. Fly
    at a plant, plant your ring at its foot, then drive a ball across the court and
    through the mouth - and every ball that threads it, yours or theirs or a stray off
    the wall, pays YOU and raises a monument on the spot, so the arena gets built out of
    the scoring. Rings are spent when they pay, so keep planting. Scarab only. First
    team to {TOLL_TARGET} tolls.
  IconActive: {{fileID: 0}}
  IconInactive: {{fileID: 0}}
  CardBackground: {{fileID: 0}}
  PreviewClip: {{fileID: 0}}
  GolfScoring: 0
  SceneName: MinigameTollway
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Scarab']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 0
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameTollway.asset.meta",
     asset_meta(G_ASSET["ArcadeGameTollway"]))


# ── 6. Toasts ────────────────────────────────────────────────────────────────
def toast(situation: int, template: str, tint_domain: int = 0, domain_names: int = 1,
          idle: int = 0, idle_seconds: int = 60) -> str:
    return (f"  - situation: {situation}\n"
            f"    messageTemplate: '{template}'\n"
            f"    tintWithDomainColor: {tint_domain}\n"
            f"    useDomainColoredNames: {domain_names}\n"
            f"    alpha: 1\n"
            f"    isIdleHint: {idle}\n"
            f"    resetOnSituation: 0\n"
            f"    idleSeconds: {idle_seconds}\n"
            f"    repeatWhileIdle: 1\n")


emit("Assets/_SO_Assets/Game Toasts/GameToastConfig_Tollway.asset",
     HEADER_FOR(EXISTING["GameToastConfigSO"], "GameToastConfig_Tollway") +
     "  gameMode: 48\n  toasts:\n" +
     toast(70, "{0} collects a toll - {1}/{2}") +
     toast(71, "CHAIN x{3}! {0} collects again - {1}/{2}") +
     toast(72, "MATCH POINT - {0} needs one more toll", tint_domain=1, domain_names=0) +
     toast(73, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
     toast(74, "Plant a ring on a plant - ANY ball through it pays YOU", idle=1, idle_seconds=25) +
     # Not an idle hint: this one fires ON A REFUSED PRESS, which is the moment the rule needs
     # stating. Before it, a press with no anchor on the line wrote one verbose log line and
     # nothing else - indistinguishable on screen from a dead button.
     toast(75, "No plant on this line - fly at one and plant your ring"))
emit("Assets/_SO_Assets/Game Toasts/GameToastConfig_Tollway.asset.meta",
     asset_meta(G_ASSET["GameToastConfigTollway"]))

TOAST_LIB = "Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset"
lib = read(TOAST_LIB)
lib_entry = f"  - {{fileID: 11400000, guid: {G_ASSET['GameToastConfigTollway']}, type: 2}}\n"
if lib_entry not in lib:
    assert lib.endswith("\n")
    lib = lib + lib_entry
emit(TOAST_LIB, lib)


# ── 7. Mode preview ──────────────────────────────────────────────────────────
# Every intensity is a genuinely different arena now - a different court radius AND a different
# anchor species - so the card's scale model has to be rebuilt when the intensity row moves
# (ModePreviewDefinitionSO.ResolveCell). Rampage authors none of these deliberately, because its
# forest is identical at all four; here it is not.
PREVIEW_CELLS = "".join(
    f"  - {{fileID: 11400000, guid: {G_ASSET[f'TollwayCellConfig{i}']}, type: 2}}\n"
    for i in range(1, len(ANCHOR_SPECIES) + 1))

emit("Assets/_SO_Assets/Mode Previews/ModePreview_Tollway.asset",
     HEADER_FOR(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Tollway") + f"""  Mode: 48
  Notes: 'OPEN-ENDED: the rings are PLACED BY PILOTS at runtime, so a preview arena has
    nothing to thread until somebody plants one - which is the mode being honest rather
    than a gap. The court sphere, the ball forge and the juke dash all work. If a
    StructurePrefab is ever added it should be a couple of standing rings, not hoops.'
  PreviewCell: {{fileID: 11400000, guid: {G_ASSET['TollwayCellConfig1']}, type: 2}}
  PreviewCellsByIntensity:
{PREVIEW_CELLS}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: 12
  ObjectiveText: Plant rings and run the traffic
  ObjectiveMetric: 4
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 760
  SpawnFormation: 0
  SpawnPoints: []
""")
emit("Assets/_SO_Assets/Mode Previews/ModePreview_Tollway.asset.meta",
     asset_meta(G_ASSET["ModePreviewTollway"]))

PREVIEW_LIB = "Assets/Resources/ModePreviewLibrary.asset"
plib = read(PREVIEW_LIB)
pentry = f"  - {{fileID: 11400000, guid: {G_ASSET['ModePreviewTollway']}, type: 2}}\n"
if pentry not in plib:
    m = re.search(r"^  Definitions:\n((?:  - \{fileID[^\n]*\n)+)", plib, re.M)
    assert m, "ModePreviewLibrary Definitions block not found"
    plib = plib.replace(m.group(0), m.group(0) + pentry, 1)
emit(PREVIEW_LIB, plib)


# ── 8. Scene: clone MinigameScarabScramble, swap the mode-specific wiring ────
# The donor already IS the court arena this mode wants - Scarab AI templates, the nucleus-as-court
# cell, the spawn ring, the crystal manager - so the clone swaps the mode identity (controller,
# turn monitor, rule, settings), the cell config (for the ladder) and the crystal economy.
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity")

for donor_key, new_guid, label in (
    ("ScarabScrambleController", G_SCRIPT["TollwayController"], "controller"),
    ("ScarabScrambleGoalTurnMonitor", G_SCRIPT["TollwayTollTurnMonitor"], "turn monitor"),
    ("ScarabScrambleScoringRule", G_ASSET["TollwayScoringRule"], "scoring rule"),
    ("ScarabScrambleSettings", G_ASSET["TollwaySettings"], "settings"),
):
    scene, n = re.subn(EXISTING[donor_key], new_guid, scene)
    assert n == 1, f"{label} guid appeared {n} times in the donor scene (expected exactly 1)"

# THE CELL becomes INTENSITY-WISE. The donor is a single-config cell (choice option 0 = Random
# over a one-entry list, i.e. always that one); Tollway authors four and selects by intensity.
# Cell.AssignConfig is STICKY and a client's intensity arrives only in the config ClientRpc, which
# the platform already handles (GameDataSO.GameConfigSynced gates the choice) - nothing mode-side
# is needed for it, but it is why the list order IS the intensity and must not be sorted.
OLD_CELL_BLOCK = (f"  CellConfigs:\n"
                  f"  - {{fileID: 11400000, guid: {EXISTING['ScarabScrambleCellConfig']}, type: 2}}\n"
                  f"  cellTypeChoiceOptions: 0\n")
NEW_CELL_BLOCK = ("  CellConfigs:\n"
                  + "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[f'TollwayCellConfig{i}']}, type: 2}}\n"
                            for i in range(1, len(ANCHOR_SPECIES) + 1))
                  + "  cellTypeChoiceOptions: 1\n")
assert OLD_CELL_BLOCK in scene, "donor cell-config block not found"
scene = scene.replace(OLD_CELL_BLOCK, NEW_CELL_BLOCK, 1)

# THE CRYSTAL ECONOMY - the one gameplay dial this mode moves on the donor. Scramble runs
# PlayerCountPlusExtra +2; Tollway runs IntensityScaled, because here the crystal count IS the
# intensity axis: crystals become balls and balls are the traffic that pays tolls.
OLD_CRYSTALS = ("  crystalCountMode: 1\n"
                "  fixedCrystalCount: 1\n"
                "  extraCrystalsToSpawnBeyondPlayerCount: 2\n"
                "  crystalCountByIntensity: []\n")
NEW_CRYSTALS = ("  crystalCountMode: 2\n"
                "  fixedCrystalCount: 1\n"
                "  extraCrystalsToSpawnBeyondPlayerCount: 0\n"
                "  crystalCountByIntensity:\n"
                + "".join(f"  - CrystalsPerPlayer: {_num(p)}\n    ExtraCrystals: {e}\n"
                          for p, e in CRYSTALS_BY_INTENSITY))
assert OLD_CRYSTALS in scene, "donor crystal-count block not found"
scene = scene.replace(OLD_CRYSTALS, NEW_CRYSTALS, 1)

emit("Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity.meta",
     scene_meta(G_ASSET["MinigameTollway.unity"]))


# ── 9. Register the card in the party-games list ────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameTollway']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 10. Always-unlocked so the card is clickable on a fresh account ─────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
# Mode 48 (see GameModes.Tollway). This said 45 until the renumber that moved Tollway off its
# collision with Hijack, and 45 was ALREADY in the list (Switchback) - so the check passed, the
# insert never ran, and Tollway shipped locked. A registration keyed on a number that another
# feature also holds cannot report its own failure.
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 48\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 48\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 11. Build settings ──────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameTollway.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Scarab Scramble scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity\n"
                          f"    guid: {G_ASSET['MinigameTollway.unity']}\n")
emit(BUILD_PATH, build)


# ── 12. End-game condition target ───────────────────────────────────────────
# SET semantics, not add-if-absent (the Dog Fight generator's lesson: an insert-only key left the
# asset on a stale number after a target retune).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("scarabScrambleGoalTarget", "tollwayTollTarget"),
                          ("scarabScrambleGoalTargetBuild", "tollwayTollTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {TOLL_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH}"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {TOLL_TARGET}\n", 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values())
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

# .meta files THIS script owns are excluded from the collision sweep - otherwise a second run
# flags its own (byte-identical) output as a collision and the script stops being idempotent.
owned_metas = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files if rel.endswith(".meta")}

existing_guids = set()
for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in filenames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.normpath(os.path.join(dirpath, fn))
        if full in owned_metas:
            continue
        try:
            with open(full, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
            if m:
                existing_guids.add(m.group(1))
        except OSError:
            pass
for g in all_new:
    if g in existing_guids:
        errors.append(f"minted GUID {g} collides with an asset this script does not own")

for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")

# the scene must no longer mention the donor's mode-specific guids
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity"]
for name in ("ScarabScrambleController", "ScarabScrambleGoalTurnMonitor",
             "ScarabScrambleScoringRule", "ScarabScrambleSettings", "ScarabScrambleCellConfig"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("TollwayController", "TollwayTollTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
for name in ("TollwayScoringRule", "TollwaySettings"):
    if G_ASSET[name] not in sc:
        errors.append(f"cloned scene missing the {name} reference")
# The cell must be INTENSITY-WISE over all four configs, in order. Checked as an ordered slice of
# the file rather than four independent "is this guid present" tests: list ORDER is the intensity
# (Cell.IntensityIndex), so a correct set in the wrong order is a wrong arena at three of four
# settings and every membership test would still pass.
_expected_cells = "".join(
    f"  - {{fileID: 11400000, guid: {G_ASSET[f'TollwayCellConfig{i}']}, type: 2}}\n"
    for i in range(1, len(ANCHOR_SPECIES) + 1))
if f"  CellConfigs:\n{_expected_cells}  cellTypeChoiceOptions: 1\n" not in sc:
    errors.append("the cloned scene's Cell is not IntensityWise over the four Tollway cell "
                  "configs in intensity order")
# The profiles ARE forked here (Scramble authors no flora and this mode's sockets are plants), so
# the check is that each cell points at its own forked profile, that every profile still carries
# the donor's whole cleanup crew - a fork that quietly loses a species is how an arena ends up
# with a ladder describing fauna it does not have - and that every anchor species is replicated.
for _i, _sp in enumerate(ANCHOR_SPECIES, start=1):
    _cell_asset = files[f"{ANCHOR_FLORA_DIR}/Tollway Cell Config {_i}.asset"]
    if G_ASSET[f"TollwaySpawnProfile{_i}"] not in _cell_asset:
        errors.append(f"Tollway cell config {_i} does not point at Tollway spawn profile {_i}")
    if EXISTING["ScarabScrambleSpawnProfile"] in _cell_asset:
        errors.append(f"Tollway cell config {_i} still points at Scramble's flora-less profile")

    _profile = files[f"{ANCHOR_FLORA_DIR}/Tollway Spawn Profile {_i}.asset"]
    for _k in ("ScrambleBrittlestarFauna", "ScramblePiranhaFauna", "ScrambleTadpoleFauna"):
        if EXISTING[_k] not in _profile:
            errors.append(f"spawn profile {_i} dropped {_k} from the cleanup crew")
    if G_ASSET[f"TollwayAnchorFlora{_sp}"] not in _profile:
        errors.append(f"spawn profile {_i} seeds no anchor flora - nothing in the court could "
                      f"carry a ring, so intensity {_i} would be unplayable")

    _flora = files[f"{ANCHOR_FLORA_DIR}/Tollway Anchor Flora {_sp}.asset"]
    if "  NetworkSynced: 1\n" not in _flora:
        errors.append(f"the {_sp} anchor species is not NetworkSynced - flora are per-peer by "
                      f"default, so two machines would snap a ring onto different plants and "
                      f"disagree about where it stands, permanently (nothing about a placed "
                      f"switch is replicated)")
    if _flora.count("- {fileID: 11400000, guid:") != len(ELEMENTS):
        errors.append(f"the {_sp} anchor species does not roll all four elements - a heart IS an "
                      f"element reward, so a one-element court pays the same for every anchor")

# Each intensity must be a DIFFERENT species, or IntensityWise is four copies of one arena and
# the three extra configs are cost with no product.
if len(set(ANCHOR_SPECIES)) != len(ANCHOR_SPECIES):
    errors.append(f"two intensities share an anchor species: {ANCHOR_SPECIES}")
if "  crystalCountMode: 2\n" not in sc:
    errors.append("scene is not on CrystalCountMode.IntensityScaled - the crystal count IS this "
                  "mode's intensity axis")
if sc.count("CrystalsPerPlayer:") != len(CRYSTALS_BY_INTENSITY):
    errors.append(f"scene must author exactly {len(CRYSTALS_BY_INTENSITY)} crystal intensity rows")
if sc.count("vesselClass: 12") != 4:
    errors.append("scene does not author 4 Scarab AI templates")
# Goals (2) is what ElementalComebackSystem.DefaultSourceFor returns for this mode; a scene-
# authored comeback instance is respected as-is and never reconfigured, so the two must agree.
if "  differenceSource: 2\n" not in sc:
    errors.append("scene's ElementalComebackSystem is not on ScoreDifferenceSource.Goals")

# Scarab-only must be a SINGLE entry, or the clamps let another hull through
arcade = files["Assets/_SO_Assets/Games/ArcadeGameTollway.asset"]
vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
if not vessels or vessels.group(1).count("- {fileID") != 1:
    errors.append("ArcadeGameTollway must author EXACTLY ONE vessel (Scarab)")
elif EXISTING["Vessel_Scarab"] not in vessels.group(1):
    errors.append("ArcadeGameTollway's single vessel is not Scarab")
if "MinDomainsAllowed: 2" not in arcade:
    errors.append("ArcadeGameTollway must require at least TWO domains - a one-domain lobby is a "
                  "toll race with nobody to lose to")

# ── ANCHORS: the band must actually be a band ────────────────────────────────
# The anchor field is the rule the mode turns on (TOLLWAY.md "Anchors"), and three of its
# properties are arithmetic rather than taste, so they are asserted here over the SHIPPED numbers
# rather than eyeballed in the editor. Note there is no layout to walk any more: the plants are
# placed by the CELL's ordinary volume-uniform band draw, which is why this replaced a 60-line
# re-derivation of a mode-owned Fibonacci walk with three inequalities.
_anchor_inner_world = ANCHOR_INNER * MEMBRANE_RADIUS
_anchor_outer_world = ANCHOR_OUTER * MEMBRANE_RADIUS

#   * every anchor must be INSIDE the smallest court, at every intensity. The band is authored in
#     membrane fractions while the court is resized per intensity, so this is the one place the
#     two coordinate systems are reconciled.
if _anchor_outer_world > min(COURT_RADII):
    errors.append(f"the anchor band reaches {_anchor_outer_world:.0f}u, outside the intensity-1 "
                  f"court ({min(COURT_RADII)}u) - plants would seed beyond the wall a ball can "
                  f"never cross, so a pilot could see anchors they can never reach")

#   * no anchor may sit in the MIDDLE. An anchor has to be a place you FLY TO; one near the centre
#     is claimed incidentally by anyone crossing the court, which is the effortless placement the
#     whole mechanism exists to remove, back by the front door. (The crystals are no help here:
#     the nucleus IS the court in this mode, so they respawn across the whole volume rather than
#     in a core - the band's inner edge is the only thing holding the middle open.)
_core_floor = AI_ANCHOR_AIM_TOLERANCE + 24.0     # one aim window plus one unupgraded ring mouth
if _anchor_inner_world <= _core_floor:
    errors.append(f"the anchor band starts {_anchor_inner_world:.0f}u from the court centre, "
                  f"inside the {_core_floor:.0f}u a pilot crossing the middle would claim "
                  f"incidentally")

#   * the band must have DEPTH. Collapsed to a shell, every anchor sits at one distance from the
#     middle and "which anchor" becomes a purely angular choice. This also fails loudly if the
#     nucleus-as-court clamp ever comes back: Flora.ResolvePlantRadius returns the OUTER edge when
#     inner >= outer, so a re-clamped band would degenerate to exactly that shell.
if _anchor_outer_world - _anchor_inner_world < 100.0:
    errors.append(f"the anchor band is only {_anchor_outer_world - _anchor_inner_world:.0f}u deep "
                  f"- with no depth, which anchor to claim is a purely angular choice")

# ── The four intensities must be four different KINDS of plant ──────────────
# Not four variations on one growth rule: the whole product of forking three extra cell configs
# is that each setting is visibly a different place. The bar is DERIVED from what the project
# actually ships rather than written as a literal - count the distinct growth components across
# the flora prefabs (a component that derives from Flora, which is what excludes SeaweedFlora's
# SegmentSpawner and oldWallFlora's GyroidAssembler), and require the roster to span as many of
# them as four intensities can.
_families_available = set()
for _fn in sorted(os.listdir(os.path.join(ROOT, FLORA_PREFAB_DIR))):
    if not _fn.endswith("Flora.prefab"):
        continue
    _m = re.search(r"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})",
                   read(f"{FLORA_PREFAB_DIR}/{_fn}"), re.M)
    _cls = _script_names.get(_m.group(1)) if _m else None
    if _cls and _cls in _flora_subclasses:
        _families_available.add(_cls)

_want_families = min(len(ANCHOR_SPECIES), len(_families_available))
_have_families = {ANCHOR_FAMILY[sp] for sp in ANCHOR_SPECIES}
if len(_have_families) < _want_families:
    errors.append(f"the four intensities span only {len(_have_families)} growth families "
                  f"({sorted(_have_families)}) of the {len(_families_available)} the project "
                  f"ships ({sorted(_families_available)}) - four variations on one growth rule is "
                  f"four cell configs' cost for one arena's product")
# ...and no family may take more than its share, or three of four settings look like siblings.
_share_cap = math.ceil(len(ANCHOR_SPECIES) / max(1, len(_families_available)))
for _fam in _have_families:
    _n = sum(1 for sp in ANCHOR_SPECIES if ANCHOR_FAMILY[sp] == _fam)
    if _n > _share_cap:
        errors.append(f"{_n} of {len(ANCHOR_SPECIES)} intensities grow {_fam}, over the {_share_cap} "
                      f"an even spread across {len(_families_available)} families allows")

# The marker must GROW with the court: intensity is traffic, the court widens 480 -> 720, and a
# plant two hundred units further away has to read larger. Measured as ONE PLANT's standing
# volume (budget x leaf), because that is the object a pilot actually reads - the forest follows
# it, since the plant COUNT is the same 14 at every intensity. Non-decreasing rather than
# strictly increasing, because two species can legitimately carry the same mass in different
# shapes.
_plant_volumes = [round(ANCHOR_BUDGET[sp] * ANCHOR_LEAF_VOLUME[sp]) for sp in ANCHOR_SPECIES]
if _plant_volumes != sorted(_plant_volumes):
    errors.append(f"the anchor species are not ordered by standing plant volume "
                  f"({_plant_volumes}) - the marker has to grow with the court, not shrink "
                  f"into it")

# The standing forest must not DOMINATE the ladder it is folded into. Restless is "the trail band
# plus the forest plus three monuments", so a forest big enough to be most of that describes the
# scenery rather than the match, and the phase the cell sits in stops being a statement about how
# the race is going. Same shape as the Lattice cell's "no single colony's own ceiling may reach
# FrenzyEnterVolume" (`Docs/ECOSYSTEM.md` 36) - the seeded world has to leave room for play.
for _sp in ANCHOR_SPECIES:
    _share = ANCHOR_VOLUME[_sp] / restless_enter_volume(_sp)
    if _share >= 0.5:
        errors.append(f"{_sp}'s standing forest is {_share:.0%} of its own RestlessEnterVolume - "
                      f"the ladder would describe the scenery rather than the match")

_switch_asset = read("Assets/_SO_Assets/VesselActions/Scarab/PlaceSwitchAction.asset")

# The AI's placement cooldown is a FUNCTION of the vessel's recharge, not a constant beside it:
# pace it under the recharge and most of its presses are refused for want of a charge, and an AI
# that cannot plant a ring cannot score at all in this mode.
_m = re.search(r"^  rechargeSecondsPerCharge: ([\d.]+)", _switch_asset, re.M)
if not _m:
    errors.append("PlaceSwitchAction.asset has no rechargeSecondsPerCharge")
elif AI_SWITCH_INTERVAL < float(_m.group(1)):
    errors.append(f"aiSwitchIntervalSeconds {AI_SWITCH_INTERVAL:.0f} is under the vessel's own "
                  f"{float(_m.group(1)):.0f}s recharge - the bots would press into a meter that "
                  f"is still filling and plant a fraction of the rings they should")

# The comeback rate only means anything relative to the TARGET. A quarter-of-target deficit must
# buy at least one whole element level (the trap that has now bitten six modes).
_quarter_deficit_levels = (TOLL_TARGET * 0.25) * COMEBACK_RATE
if _quarter_deficit_levels < 1.0:
    errors.append(f"ComebackRatePerScoreDeficit {COMEBACK_RATE} is too small for a "
                  f"{TOLL_TARGET}-toll target: a quarter-of-target deficit buys only "
                  f"{_quarter_deficit_levels:.2f} element levels, which is invisible")

# The ladder must be ORDERED and must sit above the mass a match actually makes, or it stops
# carrying information the moment the monuments start going up. Checked per intensity, because
# the standing anchor forest - and therefore the whole volume ladder - is per species now.
for _sp in ANCHOR_SPECIES:
    _re_v, _fe_v = restless_enter_volume(_sp), frenzy_enter_volume(_sp)
    if not (_re_v - 4000 < _re_v < _fe_v - 6000 < _fe_v):
        errors.append(f"{_sp}'s volume ladder is not strictly ordered exit<enter<exit<enter")
    _re_c, _fe_c = restless_enter_count(_sp), frenzy_enter_count(_sp)
    if not (_re_c - 100 < _re_c < _fe_c - 210 < _fe_c):
        errors.append(f"{_sp}'s count ladder is not strictly ordered exit<enter<exit<enter")

# BOTH gates are stated as fractions of a maximum-length match, and both must be checked against
# it - a threshold written as a bare number is a threshold that silently stops meaning anything
# the day the target moves. This assert previously compared against a literal 8, which WAS the
# toll target when it was written and became meaningless the moment the target halved: the same
# shape as the comeback-rate trap, sitting one assert away from it.
if not (0 < RESTLESS_DAISES < FRENZY_DAISES <= MAX_MATCH_DAISES):
    errors.append(f"monument budget is not ordered 0 < Restless {RESTLESS_DAISES} < Frenzy "
                  f"{FRENZY_DAISES} <= max match {MAX_MATCH_DAISES}")
# Frenzy must sit in the BACK HALF of the race: reachable before the midpoint and the ladder has
# stopped conveying anything while the match is still being played.
if FRENZY_DAISES <= MAX_MATCH_DAISES // 2:
    errors.append(f"FrenzyEnterVolume is reached by monument {FRENZY_DAISES} of "
                  f"{MAX_MATCH_DAISES} - the top of the ladder would arrive before the race is "
                  f"half run, which is exactly the defect this fork exists to avoid")


# serialized MonoBehaviour keys must exist on the C# class (asset-surgery)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    MODS = (r"(?:(?:public|protected|private|internal|static|const|readonly|new|virtual|"
            r"override|abstract|sealed|partial)\s+)+")
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>|\()", src):
        out.add(m.group(1))
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    for m in re.finditer(r"^\s{4,}" + TYPE + r"\s+(\w+)\s*\{\s*get;", src, re.M):
        out.add(m.group(1))
    return out


SO_BASE = {"Mode", "IsMultiplayer", "DisplayName", "Description", "IconActive", "IconInactive",
           "CardBackground", "PreviewClip", "GolfScoring", "SceneName"}
CHECKS = [
    ("Assets/_SO_Assets/Games/ArcadeGameTollway.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
    ("Assets/_SO_Assets/Scoring Rules/TollwayScoringRule.asset",
     "Assets/_Scripts/Controller/Arcade/Tollway/TollwayScoringRuleSO.cs"),
    ("Assets/_SO_Assets/Games/TollwaySettings.asset",
     "Assets/_Scripts/Controller/Arcade/Tollway/TollwaySettingsSO.cs"),
]
for asset_path, cs_path in CHECKS:
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path) | SO_BASE
    for extra in ("Assets/_Scripts/ScriptableObjects/SO_Game.cs",
                  "Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs",
                  "Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueScoringRuleSO.cs"):
        # Deliberately NOT an if-exists skip: a base class that moved would silently weaken this
        # check into passing everything, which is worse than the missing key it exists to catch.
        if not os.path.exists(os.path.join(ROOT, extra)):
            errors.append(f"base class for the serialized-key check is missing: {extra}")
            continue
        known |= cs_fields(extra)
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on "
                      f"{os.path.basename(cs_path)}: {sorted(unknown)}")

# every serialized key the scene's controller block authors must exist on TollwayController.cs
CONTROLLER_CS = "Assets/_Scripts/Controller/Arcade/Tollway/TollwayController.cs"
controller_keys = {"settings", "rule", "arenaCell", "cellData"}
missing = controller_keys - cs_fields(CONTROLLER_CS)
if missing:
    errors.append(f"TollwayController.cs is missing serialized fields the scene authors: "
                  f"{sorted(missing)}")

# the C# default target and this script's must agree, or the tool window's "(default)" lies
endcond_cs = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultTollwayTollTarget = (\d+);", endcond_cs)
if not m:
    errors.append("EndConditionOverridesSO.cs has no DefaultTollwayTollTarget")
elif int(m.group(1)) != TOLL_TARGET:
    errors.append(f"DefaultTollwayTollTarget ({m.group(1)}) != this script's "
                  f"TOLL_TARGET ({TOLL_TARGET}) - the two must move together")

# GameModes.Tollway must exist with the value this card authors
gamemodes_cs = read("Assets/_Scripts/Data/Enums/GameModes.cs")
if not re.search(r"^\s*Tollway = 48,", gamemodes_cs, re.M):
    errors.append("GameModes.cs has no 'Tollway = 48' - the card would launch nothing")

# the toast situations the controller posts must exist in the enum with these values
toast_cs = read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
for name, value in (("TollwayToll", 70), ("TollwayChain", 71), ("TollwayMatchPoint", 72),
                    ("TollwayLeadChanged", 73), ("TollwayRingHint", 74),
                    ("TollwayNoAnchor", 75)):
    if not re.search(rf"^\s*{name} = {value},", toast_cs, re.M):
        errors.append(f"GameToastSituation.{name} = {value} is missing")

if errors:
    print("VALIDATION FAILED - nothing written:")
    for e in errors:
        print("  x", e)
    sys.exit(1)

print(f"Validation passed ({len(files)} files).")
print(f"  toll target {TOLL_TARGET}, comeback {COMEBACK_RATE} "
      f"({_quarter_deficit_levels:.2f} levels at a quarter-target deficit)")
print(f"  monuments: Restless at {RESTLESS_DAISES}, Frenzy at {FRENZY_DAISES}, "
      f"max match {MAX_MATCH_DAISES}")
print(f"  families: {sorted(_have_families)} of {sorted(_families_available)} shipped")
for _i, _sp in enumerate(ANCHOR_SPECIES, start=1):
    print(f"  I{_i} {_sp:<13} {ANCHOR_FAMILY[_sp]:<18} court {COURT_RADII[_i - 1]}u  "
          f"{ANCHOR_BUDGET[_sp]:>4} prisms/plant x {ANCHOR_LEAF_VOLUME[_sp]:>6} vol = "
          f"{ANCHOR_BUDGET[_sp] * ANCHOR_LEAF_VOLUME[_sp]:>8.0f} vol/plant")
    print(f"       forest {ANCHOR_PRISMS[_sp]:>5} prisms / {ANCHOR_VOLUME[_sp]:>6} vol  ->  "
          f"volume {restless_enter_volume(_sp)}/{frenzy_enter_volume(_sp)}, "
          f"count {restless_enter_count(_sp)}/{frenzy_enter_count(_sp)}")
for rel in sorted(files):
    print("  ", rel)

_stale = [rel for rel in STALE_PATHS if os.path.exists(os.path.join(ROOT, rel))]
for rel in _stale:
    print("   retiring", rel)

if CHECK_ONLY:
    print("\n--check: no files written.")
    sys.exit(0)

for rel, content in files.items():
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)

# The single-config pass's assets. Left on disk they are live GUIDs that still resolve, so a
# half-migrated folder looks exactly like a working one - and a stale CellConfigDataSO is still a
# valid drop target for anyone wiring a scene by hand.
for rel in _stale:
    os.remove(os.path.join(ROOT, rel))

print(f"\nWrote {len(files)} files" +
      (f", retired {len(_stale)} stale." if _stale else "."))

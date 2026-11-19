#!/usr/bin/env python3
"""Train a vessel's Time AI in its OWN Time game, in the real game (Plan step: time-arcade).

Every candidate is raced by the shipped AIPilot in Amoebius, headless, through the race judge (regatta_judge.judge with
`mode`): the game's own course, rings, prisms, drift, boost and AI code. Nothing is modelled. A candidate is the AI's
numbers for that game, set IN MEMORY for its run (`overrides` on the drive assets, `controller_overrides` on the
mode's scene controller, `crystal_genes` for RaceAICrystalGenesSO), so candidates race side by side and nothing is
written until a winner is promoted.

    one AI per vessel per element (Docs/AI_SYSTEM/AI_PLAN.md): ONE drive, written to every intensity's file, scored on
    every CONDITION together. A condition is an intensity and a starting element table: the card's own (I1 at rest,
    I2-I4 at the game's element 5 since #2055) and the same rungs at Time 10, because a hull's Time sets its speed and
    an AI trained at one speed breaks at another (FINDINGS.md 13: the Scarab, 216 u/s in Scarab Rally, loops at 324 u/s
    in Regatta). W2's TrainingCourseOverlay genomes widen the courses (`--genome`, once it ships).

Two phases (Garrett, 2026-11-16: "constrain the space to find a good seed quickly then switch to a pareto scheme to
expand and contract complexity"):
    1 constrained  sep-CMA-ES over the drive fields and the nose lead, from every shipped genome; crystal gene at 0.
    2 pareto       a non-dominated archive over (loss, complexity). Complexity = genes moved off the phase-1 seed (a
                   crystal budget above 0 counts). A child EXPANDS (frees one held gene, the crystal gene included),
                   CONTRACTS (puts one moved gene back on the seed) or MUTATES its moved genes.

Loss (lower is better), per race:  finish / reference(condition)       when it finished
                                   2 + share of gates missed            when it did not (a DNF is worse than any finish)
    per condition  mean over races + 0.5 x the worst race
    total          mean over conditions + 0.5 x the worst condition  (no rung is given up for the others)
reference(condition) is the shipped AI's mean on the same courses, so 1.0 = today's AI.

    python3 Tools/Compute/evaluators/time_ai_search.py --hull Scarab --search --generations 4 --pareto 4
    python3 Tools/Compute/evaluators/time_ai_search.py --hull Scarab --confirm best.json --seeds 101,102,103

Resume: every finished race set goes to <out>/races.jsonl. Run the SAME command again after a restart (put --out on
/mnt/project-files so it survives the container) and it replays to where it stopped; --confirm resumes the same way.

A player killed for memory is raced again, never scored as a DNF. One player process takes ~2.5 GB: --workers 4 at
most on a 16 GB machine. Confirm a winner on fresh courses (24+ flights, Port/docs/AI_TRAINING.md) before promoting.
"""
import argparse
import concurrent.futures
import json
import os
import random
import re
import sys
import tempfile
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import regatta_judge as RJ  # noqa: E402
from optimizers import SepCMAES, Space  # noqa: E402

ROOT = RJ.ROOT
POL = "Assets/Resources/AIBoostPolicies/"
SO = "Assets/_SO_Assets/AI Boost Policies/"
CRYSTAL = "crystal"            # the RaceAICrystalGenesSO gene: detour budget, world units
CRYSTAL_RANGE = (0.0, 300.0)
# RaceAIRefuelGenesSO (a ramp that burns fuel earned by ploughing or slicing, #2233): turn toward a prism ahead when the
# tank is under refuelBelow, if the detour costs at most refuelSlack units. Searched with the drive ("refuel_row").
REFUEL = "Assets/Resources/RaceAIRefuelGenes.asset"
REFUEL_FIELDS = {"refuelSlack": ("PrismDetourSlack", (0.0, 300.0)), "refuelBelow": ("RefuelBelow", (0.0, 1.0))}

def _switchback_nose(p):
    return {
        "aiNoseFollowByIntensity": [
            {"easy": e, "medium": m, "hard": h, "legendary": float(p["noseLegendary"]) * k}
            for e, m, h, k in ((1.9, 2.2, 3, 1.0), (2, 2.4, 3, 1.0), (2.1, 2.2, 2.8, 1.2), (1.8, 2.2, 3.5, 1.2))],
        "aiNoseFollowGauntlet.legendary": float(p["noseLegendary"]) * 1.2,
        "aiNoDodgeNearRing": float(p["aiNoDodgeNearRing"])}


# SwitchbackController's AI knobs (Scarab Rally inherits them): the Legendary nose follow rate (every intensity's row,
# shipped 10.57/s at I1-I2 and 12.69/s at I3-I4 since the Dolphin step-5 run (2026-11-18), and the I4 gauntlet's) and how near a ring the AI stops dodging.
SWITCHBACK_NOSE = {
    "ctrl_fields": {"noseLegendary": (3.0, 24.0), "aiNoDodgeNearRing": (0.0, 600.0)},
    "ctrl_defaults": {"noseLegendary": 10.57, "aiNoDodgeNearRing": 277.67},   # Dolphin step-5 run (2026-11-18)
    "ctrl": _switchback_nose,
}

def _scarab_nose(p):
    return {
        "aiScarabNoseFollowByIntensity": [
            {"easy": e, "medium": m, "hard": h, "legendary": float(p["noseLegendary"]) * k}
            for e, m, h, k in ((1.9, 2.2, 3, 1.0), (2, 2.4, 3, 1.0), (2.1, 2.2, 2.8, 1.2), (1.8, 2.2, 3.5, 1.2))],
        "aiScarabNoDodgeNearRing": float(p["aiNoDodgeNearRing"])}


# ScarabRallyController's own copies of those knobs (2026-11-17: the Scarab no longer flies the Dolphin's table), shipped
# from Scarab run3: Legendary 15/s at I1-I2 (x1.2 at I3-I4), no dodge within 224 u of a ring.
SCARAB_NOSE = {
    "ctrl_fields": {"noseLegendary": (3.0, 24.0), "aiNoDodgeNearRing": (0.0, 600.0)},
    "ctrl_defaults": {"noseLegendary": 15.0, "aiNoDodgeNearRing": 224.0},
    "ctrl": _scarab_nose,
}

# Per vessel: its Time game, the drive assets per intensity (AIBoostPolicySO.ForMode: <name>_<Mode>_I<n>, else the mode
# file, else the hull's own), the searched drive fields with the asset's limits, and the nose lead: the mode's
# aiNoseLead table with this hull's row ("rows" are the other hulls' rows, kept as they ship).
GAMES = {
    "Dolphin": {
        "mode": "Switchback",
        "assets": {1: POL + "DolphinAIBoostPolicy_Switchback_I1.asset", 2: POL + "DolphinAIBoostPolicy_Switchback_I2.asset",
                   3: POL + "DolphinAIBoostPolicy_Switchback.asset", 4: POL + "DolphinAIBoostPolicy_Switchback_I4.asset"},
        # ChargeBoostAIPolicySO, bounds as Tools/Compute/evaluators/switchback_fit.py FIELDS (+ the stick band)
        "fields": {"alignDegrees": (0.0, 90.0), "stickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0),
                   "spendAtCharge01": (0.05, 1.0), "peakMarginUnits": (0.0, 30.0), "minDischargeSeconds": (0.0, 2.0),
                   "minPulseGainUnits": (0.0, 120.0), "maxHoldOffSeconds": (0.5, 8.0)},
        **SWITCHBACK_NOSE,
        "lead": 2.15, "rows": [],                      # SwitchbackController.aiNoseLead: Dolphin 2.15 (step-5 run)
    },
    "Serpent": {
        "mode": "SerpentTime",
        "assets": {i: POL + "SerpentAIBoostPolicy_SerpentTime.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 6.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "maxConcurrentBurns": (1, 6), "minSecondsBetweenPellets": (0.0, 4.0), "reservePellets": (0, 4),
                   # Refuel and pacing (FINDINGS 23: the tank runs dry by Regatta's third lap): lay a wall at or under
                   # this many pellets (negative = off, the cycler keeps the cloak), coast along it, pace a low tank.
                   "refuelBelowPellets": (-1.0, 4.0), "refuelCoastSeconds": (0.0, 6.0),
                   "lowTankSecondsBetweenPellets": (0.0, 6.0),
                   # Where to lay the scarce walls (one per ~42 s of Wall Seeds): at a corner, close to its gate, so
                   # the trail runs along the wall after the turn (regatta-trace FINDINGS). 0 = anywhere.
                   "refuelMinCornerDegrees": (0.0, 180.0), "refuelMaxRunwaySeconds": (0.0, 4.0)},
        "ints": ("maxConcurrentBurns", "reservePellets"),
        "lead": 2.09, "rows": [], "nose_gene": 10.42,  # SerpentTimeController.aiNoseLead: Serpent 2.09 at 10.42/s (run4)
        # --long: a Regatta-length race in the home game (24 gates as 3 laps of 8, 100-200 s), so pacing pays at home.
        "long": {"overrides": [{"asset": "Assets/Resources/EndConditionOverrides.asset",
                                "fields": {"serpentTimeGateTarget": 24}}],
                 "controller_overrides": {"laps": 3}},
    },
    "Sparrow": {
        "mode": "Breakwater",
        "assets": {i: POL + "SparrowAIBoostPolicy_Breakwater.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "minHoldSeconds": (0.0, 3.0), "minRestSeconds": (0.0, 3.0)},
        "lead": 1.29, "rows": [], "nose_gene": 13.9,   # BreakwaterController.aiNoseLead: Sparrow 1.29 at 13.9/s (run4)
    },
    "Scarab": {
        "mode": "ScarabRally",
        # The hull's own drive (no Scarab Rally variant): what it flies in Regatta too, so a win here carries over.
        "assets": {i: SO + "ScarabAIBoostPolicy.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "dashCooldownSeconds": (0.2, 6.0)},
        # Scarab Rally inherits Switchback's table, which has only the Dolphin's row: the Scarab flies with NO lead
        # there and 2.5 in Regatta (RaceLines/Scarab_Regatta). The search gives it a row.
        **SCARAB_NOSE,                                 # Scarab Rally's own nose knobs (ScarabRallyController)
        "lead": 0.0, "rows": [{"Hull": "Dolphin", "Gain": 2.5, "MaxExtraDegrees": 40}],
    },
    "Rhino": {
        "mode": "Headlong",
        # RampBoostAIPolicySO: the ramp boost's engage/release bands and the corner braking that keeps it.
        "assets": {i: POL + "RhinoAIBoostPolicy_Headlong.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "cornerSpeed": (60.0, 900.0), "minCornerSpeed": (30.0, 600.0), "brakeMarginUnits": (0.0, 300.0),
                   "brakeLeadSeconds": (0.0, 2.0), "brakeSteerFloor": (0.0, 1.0), "cornerReleaseDegrees": (0.0, 90.0)},
        # HeadlongController.aiNoseLead: Rhino 2.5. At 2.5 the AI orbits in place (~70 s a lap at 33 u/s, 2026-11-17);
        # 1.0 halved I1 and I2. Headlong has no nose knob: the fleet lag.
        "lead": 2.47, "rows": [], "nose_gene": 7.5,    # Rhino run1 (2026-11-17)
        "refuel_row": 0,                               # RaceAIRefuelGenes row: the ramp runs on plough fuel (#2233)
    },
    "Swordfish": {
        "mode": "Spearline",
        # Its own RampBoostAIPolicySO (it shared the Rhino's until 2026-11-18): the same ramp, run on slice fuel.
        "assets": {i: POL + "SwordfishAIBoostPolicy_Spearline.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "cornerSpeed": (60.0, 900.0), "minCornerSpeed": (30.0, 600.0), "brakeMarginUnits": (0.0, 300.0),
                   "brakeLeadSeconds": (0.0, 2.0), "brakeSteerFloor": (0.0, 1.0), "cornerReleaseDegrees": (0.0, 90.0)},
        # SpearlineController.aiNoseLead: Swordfish 2.5, at most 10 extra degrees. Spearline has no nose knob.
        "lead": 2.5, "lead_max": 10.0, "rows": [], "nose_gene": 1.5,
        "refuel_row": 1,
    },
    "Manta": {
        "mode": "Redline",
        # The Soar autopilot's bands (MantaSoarAITuning_Redline, written at the executor's own 0.35 / 0.75 so the shipped
        # AI is unchanged) and RedlineController's AI knobs: the Legendary nose follow rate (every intensity's row) and
        # its own crystal gene, aiTimeCrystalSlack. The card starts Time 10, so Soar (Time's level-5 upgrade) is lit.
        "assets": {i: POL + "MantaSoarAITuning_Redline.asset" for i in (1, 2, 3, 4)},
        "fields": {"BoostStickBand": (0.0, 1.0), "YawsteryStickBand": (0.0, 1.0)},
        # I1-I2 and I3-I4 each search their own Legendary rate (the split confirm below).
        "ctrl_fields": {"noseLegendary": (2.0, 24.0), "noseLegendaryI34": (2.0, 24.0)},
        "ctrl_defaults": {"noseLegendary": 15.0, "noseLegendaryI34": 9.0},   # shipped since the split confirms
        # noseLegendaryI34 (optional, not searched): I3-I4's own Legendary rate. The Manta split confirm (2026-11-17) found
        # nose 15 wins I1-I2 by 12-21% and loses I4 from Time 5 and 10 by 13-19% (Soar overshoot), so a confirm can race
        # a split table.
        "ctrl": lambda p: {"aiNoseFollowByIntensity": [
            {"easy": e, "medium": m, "hard": h,
             "legendary": float(p["noseLegendary"] if i < 2 else p.get("noseLegendaryI34", p["noseLegendary"]))}
            for i, (e, m, h) in enumerate(((2, 3, 6), (2, 3, 6), (2.5, 3, 6), (4, 5, 6)))]},
        "crystal_field": "aiTimeCrystalSlack",
        "lead": 0.0, "rows": [],                       # RedlineController.aiNoseLead: Manta 0
    },
    "Urchin": {
        "mode": "Skein",
        # UrchinAutopilotConfigSO (AIPilot's rail-riding autopilot, the Skein file): bounds are the SO's own Range/Min
        # limits, capped at a few times the shipped value where the SO has only a floor.
        "assets": {i: "Assets/Resources/UrchinAutopilotConfig_Skein.asset" for i in (1, 2, 3, 4)},
        "fields": {"RailLeadDistance": (10.0, 900.0), "AssessIntervalSeconds": (0.05, 1.5), "ScanArc": (50.0, 6000.0),
                   "CaptureFraction": (0.1, 1.0), "ClosingFraction": (0.1, 0.95), "LaunchPreferArc": (0.0, 1500.0),
                   "DryCrawlArc": (0.0, 400.0), "MaxDryCrawlSeconds": (0.5, 8.0), "ReverseLeadTurnRadii": (1.0, 6.0),
                   "ParkedSpeed": (0.5, 40.0), "ParkedSeconds": (0.5, 12.0), "MinSlipIntervalSeconds": (0.25, 6.0),
                   "SpikeIntervalSeconds": (0.1, 6.0), "SpikeMinAmmo": (0.0, 1.0), "TrackAimDegrees": (1.0, 45.0),
                   "TrackMinRangeInLengths": (1.0, 10.0), "TrackAxisAlignment": (0.0, 1.0)},
        # SkeinController.aiNoseLead: Urchin 1.39 at 14.41/s (run1, 2026-11-17).
        "lead": 1.39, "rows": [], "nose_gene": 14.41,
    },
    "Stoat": {
        "mode": "Slingshot",
        # StoatSlingConfigSO's AI sling rule (StoatSlingExecutor.AutopilotSling), the Slingshot file; only the ai* knobs
        # and the trampoline AI turn scale, never the sling's own physics (a mode never changes the vessel).
        "assets": {i: POL + "StoatSlingConfig_Slingshot.asset" for i in (1, 2, 3, 4)},
        "fields": {"aiSlingMinTurnDegrees": (0.0, 180.0), "aiReleaseConeDegrees": (1.0, 90.0),
                   "aiCatchGiveUpSeconds": (0.5, 10.0), "aiSlingIntervalSeconds": (0.5, 20.0),
                   "aiSlingMinDistance": (0.0, 400.0), "aiWarpMinDistance": (0.0, 1200.0),
                   "aiHoldForWindowDegrees": (0.0, 360.0), "trampolineAiTurnScale": (40.0, 300.0)},
        # SlingshotController.aiNoseLead: Stoat 2.62 at 10.43/s (run1, 2026-11-17).
        "lead": 2.62, "rows": [], "nose_gene": 10.43,
    },
    "Squirrel": {
        "mode": "SkimRace",
        # SkimRaceAIConfigSO (the Skim Race pilot, Docs/SKIM_RACE_AI.md): each intensity's own tuning file
        # (SkimRaceAIDeployment.PolicyFor: SkimRaceAIConfig_I<n>, the general SkimRaceAIConfig when there is none or its
        # map fingerprint does not fit). ONE genome is written on all four, so the pilot flies it whichever file it
        # resolves. Its steering look-ahead, skim height, boost spending, crossing pace, lead and recovery. The race needs
        # the judge's Skim Race path (a seat's progress is its crystals; it finishes at the mode's crystal count).
        "assets": {1: "Assets/Resources/SkimRaceAIConfig_I1.asset", 2: "Assets/Resources/SkimRaceAIConfig_I2.asset",
                   3: "Assets/Resources/SkimRaceAIConfig.asset", 4: "Assets/Resources/SkimRaceAIConfig_I4.asset"},
        "fields": {"LookaheadSeconds": (0.1, 3.0), "LookaheadMin": (5.0, 300.0), "LookaheadMax": (60.0, 1200.0),
                   "SkimHeight": (1.0, 20.0), "CrystalBumpHalfWidth": (10.0, 600.0),
                   "LowBoostApproachScale": (0.05, 1.0), "LowBoostFull": (1.01, 8.0), "DirectBoost": (0.0, 8.0),
                   "DirectBoostHysteresis": (0.0, 2.0), "TrackGuardMargin": (0.0, 10.0), "PassMargin": (0.0, 30.0),
                   "CrossingThrottle": (0.1, 1.0), "CrossingSlowDistance": (0.0, 600.0),
                   "CrystalDirectDistance": (0.0, 600.0), "LeadGain": (0.0, 6.0), "MaxLeadDegrees": (0.0, 90.0),
                   "StickGainPerDegree": (0.01, 0.5), "MinThrottle": (0.0, 1.0), "ReachabilityMargin": (0.0, 5.0),
                   "StallSeconds": (0.5, 10.0), "RecoveryThrottle": (0.0, 1.0)},
        # SkimRaceController has no aiNoseLead table (the pilot leads its own nose: LeadGain, MaxLeadDegrees): no lead
        # gene. No nose rate of its own either: the fleet lag, searched through the judge.
        "lead": None, "rows": [], "nose_gene": 1.5,
    },
    "Meerkat": {
        "mode": "BurrowRun",
        # The Meerkat flies the gate race's own steering (no drive asset, no ability autopilot in a Time race) and
        # its genes are the race controller's AI knobs (GateRaceController aiCommitDistance / aiApproachLead /
        # aiThroughDistance, as MinigameBurrowRun ships them; only the Meerkat races Burrow Run) and the nose rate
        # (BurrowRunController.aiNoseLead: Meerkat lead 0 at 7.97/s, run1 2026-11-17). No lead gene.
        "assets": {},
        "fields": {},
        "ctrl_fields": {"aiCommitDistance": (40.0, 600.0), "aiApproachLead": (40.0, 800.0),
                        "aiThroughDistance": (40.0, 600.0)},
        "ctrl_defaults": {"aiCommitDistance": 42.0, "aiApproachLead": 702.0, "aiThroughDistance": 74.0},
        "ctrl": lambda p: {k: float(p[k]) for k in ("aiCommitDistance", "aiApproachLead", "aiThroughDistance")},
        "lead": None, "rows": [], "nose_gene": 7.97,
    },
    # The Meerkat's portable Time AI (loop step 3, 2026-11-19): its OWN wormhole autopilot
    # (MeerkatSolitonExecutor.Autopilot, read off MeerkatSolitonConfig: when to form a straight or a bent tunnel, how
    # long to shape the reticle), trained in Burrow Run with the race controller's knobs left at what the scene ships
    # (run1's 42 / 702 / 74 and nose 7.97/s). The autopilot is the vessel's, so every race (Regatta included) flies what this learns.
    "MeerkatDrive": {
        "hull": "Meerkat",
        "mode": "BurrowRun",
        "assets": {i: "Assets/_SO_Assets/VesselActions/Meerkat/MeerkatSolitonConfig.asset" for i in (1, 2, 3, 4)},
        "fields": {"aiStraightMinDistance": (50.0, 2000.0), "aiBentMinDistance": (50.0, 2000.0),
                   "aiShapeSeconds": (0.05, 2.0)},
        "lead": None, "rows": [],
    },
    "Shrike": {
        "mode": "Thornline",
        # Thornline aims the AI itself (ThornlineAIPlan) and sets its skill by difficulty on every arming
        # (ThornlineController.aiNoseLeadByDifficulty / aiNoseFollowByDifficulty): the search moves the LEGENDARY row of
        # both, through controller_overrides, and nothing on the vessel. ShrikeConfig carries the sling AI's own knobs; it
        # is listed for the shipped-genome read only (no field of it is searched: it is the vessel's, every mode's).
        "assets": {i: "Assets/_SO_Assets/VesselActions/Shrike/ShrikeConfig.asset" for i in (1, 2, 3, 4)},
        "fields": {},
        "ctrl_fields": {"noseLeadLegendary": (1.0, 32.0), "noseFollowLegendary": (1.5, 24.0)},
        "ctrl_defaults": {"noseLeadLegendary": 9.2, "noseFollowLegendary": 1.6},    # Shrike run1 (was 16 and the fleet lag)
        "ctrl": lambda p: {
            "aiNoseLeadByDifficulty": [2.5, 3.0, 8.0, float(p["noseLeadLegendary"])],
            "aiNoseFollowByDifficulty": [-1.0, -1.0, -1.0,
                                         -1.0 if float(p["noseFollowLegendary"]) <= 1.5 else float(p["noseFollowLegendary"])]},
        "lead": None, "rows": [],                      # no aiNoseLead table: the row above is the lead
    },
    "Thresher": {
        "mode": "ThresherTime",
        # ThresherAIBoostPolicy's Thresher Time variant (the plant-and-sling drive) and ThresherTimeController.aiNoseLead
        # (Thresher 2.5). Thresher Time has no nose knob: the fleet lag, 1.5/s, set through the judge. In Regatta the hull
        # flies the base policy (no Regatta variant) and its race line's nose pair, so a winner reaches Regatta through a
        # ThresherAIBoostPolicy_Regatta variant and Resources/RaceLines/Thresher_Regatta.
        "assets": {i: POL + "ThresherAIBoostPolicy_ThresherTime.asset" for i in (1, 2, 3, 4)},
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "plantMinTurnDegrees": (0.0, 180.0), "plantMinDistance": (0.0, 400.0),
                   "slingIntervalSeconds": (0.1, 4.0), "releaseConeDegrees": (1.0, 60.0), "maxPlantSeconds": (0.2, 6.0),
                   "aimedHoldSeconds": (0.0, 4.0), "aimToleranceDegrees": (0.5, 45.0)},
        "lead": 2.77, "rows": [], "nose_gene": 6.37,    # ThresherTimeController.aiNoseLead: Thresher 2.77 at 6.37/s (run1)
    },
    "Wrasse": {
        "mode": "WrasseRun",
        # The Wrasse flies the gate race's own steering (no drive asset: its boosts are the kite's saved boosts, spent by
        # KiteNavigatorAI), so its genes are the race controller's AI knobs (as MinigameWrasseRun ships them), the nose
        # rate (set through the judge, fleet lag 1.5/s) and the nose lead, both promotable into WrasseRunController's
        # aiNoseLead table (empty = the fleet lag, no lead). In Regatta the nose pair reaches it through
        # Resources/RaceLines/Wrasse_Regatta (the controller knobs are Wrasse Run's).
        "assets": {},
        "fields": {},
        "ctrl_fields": {"aiCommitDistance": (20.0, 600.0), "aiApproachLead": (20.0, 800.0),
                        "aiThroughDistance": (20.0, 600.0)},
        "ctrl_defaults": {"aiCommitDistance": 433.0, "aiApproachLead": 423.0, "aiThroughDistance": 68.0},   # run1
        "ctrl": lambda p: {k: float(p[k]) for k in ("aiCommitDistance", "aiApproachLead", "aiThroughDistance")},
        "lead": 0.69, "rows": [], "nose_gene": 4.37,                                                       # run1
    },
    "Grizzly": {
        "mode": "GrizzlyTime",
        # As the Wrasse: the gate race's own steering, Grizzly Time's controller knobs and the nose rate (fleet lag).
        "assets": {},
        "fields": {},
        "ctrl_fields": {"aiCommitDistance": (40.0, 900.0), "aiApproachLead": (40.0, 1200.0),
                        "aiThroughDistance": (40.0, 900.0)},
        "ctrl_defaults": {"aiCommitDistance": 380.0, "aiApproachLead": 420.0, "aiThroughDistance": 285.0},
        "ctrl": lambda p: {k: float(p[k]) for k in ("aiCommitDistance", "aiApproachLead", "aiThroughDistance")},
        "lead": None, "rows": [], "nose_gene": 1.5,
    },
    "Honeyguide": {
        "mode": "SlipLine",
        # Slip Line coaches the AI itself (SlipLineController: the rail it rides and the race line between rides); the
        # search moves the coach's two leads through controller_overrides and the nose follow rate through the judge
        # (Slip Line has no nose knob: the fleet lag, 1.5/s). Nothing on the vessel.
        # HoneyguideConfig is listed for the shipped-genome read only: none of its fields is searched (it is the vessel's).
        "assets": {i: "Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguideConfig.asset" for i in (1, 2, 3, 4)},
        "fields": {},
        "ctrl_fields": {"aiRailLead": (5.0, 200.0), "aiRaceLineLead": (5.0, 300.0)},
        "ctrl_defaults": {"aiRailLead": 40.0, "aiRaceLineLead": 60.0},
        "ctrl": lambda p: {"aiRailLead": float(p["aiRailLead"]), "aiRaceLineLead": float(p["aiRaceLineLead"])},
        "lead": None, "rows": [], "nose_gene": 1.5,
    },
    "Angler": {
        "mode": "LanternRun",
        # AnglerLeapfrogAIPolicySO (the hull's own drive, AnglerAIBoostPolicy): when it paints and throws a group (lined up
        # within throwAlignDegrees, objective at least minThrowDistance away, trigger held holdSeconds, throwIntervalSeconds
        # between throws) and when it dashes to a group (at least minDashDistance, within dashAheadDegrees of the
        # objective). Every other mode reads the same policy, so a winner ships as <name>_LanternRun / _Regatta variants.
        # Lantern Run is a plain gate race (no aiNoseLead table) at the fleet lag, 1.5/s; the rate is searched through the
        # judge and reaches Regatta through Resources/RaceLines/Angler_Regatta. Shipped from run1 (2026-11-19):
        # AnglerAIBoostPolicy_LanternRun and LanternRunController.aiNoseLead (no lead, 8.25/s).
        "assets": {i: POL + "AnglerAIBoostPolicy_LanternRun.asset" for i in (1, 2, 3, 4)},
        "fields": {"throwAlignDegrees": (0.5, 30.0), "minThrowDistance": (0.0, 600.0), "holdSeconds": (0.05, 2.0),
                   "throwIntervalSeconds": (0.0, 3.0), "minDashDistance": (1.0, 300.0), "dashAheadDegrees": (0.0, 120.0)},
        "lead": None, "rows": [], "nose_gene": 8.25,
    },
    "TigerMoth": {
        "mode": "Waystation",
        # Waystation drives the AI itself (WaystationController: the Fold toward far rings, the belly-aimed drop, and
        # the tower hop over each ring under a plate). The genes are that controller's hop and drop gates, through
        # controller_overrides, and the nose follow rate through the judge (shipped in WaystationController.aiNoseLead).
        # TigerMothDropAction is listed for the shipped-genome read only: it is the vessel's, never searched.
        "assets": {i: "Assets/_SO_Assets/VesselActions/TigerMoth/TigerMothDropAction.asset" for i in (1, 2, 3, 4)},
        "fields": {},
        "ctrl_fields": {"aiHopLineShare": (0.3, 0.9), "aiHopMissMargin": (120.0, 800.0),
                        "aiHopLookahead": (60.0, 400.0), "aiDropAimDegrees": (3.0, 25.0)},
        "ctrl_defaults": {"aiHopLineShare": 0.85, "aiHopMissMargin": 600.0, "aiHopLookahead": 155.0,
                          "aiDropAimDegrees": 6.6},    # TigerMoth run1 (2026-11-17)
        "ctrl": lambda p: {k: float(p[k]) for k in ("aiHopLineShare", "aiHopMissMargin", "aiHopLookahead",
                                                    "aiDropAimDegrees")},
        "lead": None, "rows": [], "nose_gene": 6.41,   # WaystationController.aiNoseLead: TigerMoth at 6.41/s (run1)
    },
    "BrittleStar": {
        "mode": "BrittleStarTime",
        # The mode stands the hull's boost policy down (BrittleStarAIBoostPolicy_BrittleStarTime, engage runway 999 s):
        # BrittleStarTimeCoach is the only Speed Mode writer, and its corner-share and nose tables are static C#, not
        # serialized, so no drive field is searched here. The genes are the AI nose rate in two bands (I1-I2, I3-I4),
        # set through the judge over the coach's own per-intensity Legendary row (13.66, 13.66, 15.04, 15.04 since run1, 2026-11-17), and the crystal gene.
        "assets": {i: POL + "BrittleStarAIBoostPolicy_BrittleStarTime.asset" for i in (1, 2, 3, 4)},
        "fields": {},
        "lead": None, "rows": [],                      # BrittleStarTimeController has no nose lead table
        "nose_gene": 13.66, "nose_split": 15.04,       # seeds: the coach's Legendary I2 / I4 rate (run1)
        "nose_mode_sets": True,                        # the shipped AI (params None) keeps the coach's table
    },
    # The Brittle Star's portable Time AI (loop step 3, 2026-11-18): the hull's OWN Speed Mode policy
    # (BrittleStarSpeedModeAIPolicySO) trained in Brittle Star Time with the coach switched off (coachOn false: no
    # coached wish, no coached line, the gate way), so what it learns is the policy every other race flies too -
    # Regatta's BrittleStarAIBoostPolicy_Regatta copies it. The coach is static C# that reads this game's course and
    # cannot leave it. The reference (params None) is the shipped game, coach on, so a loss under 1 beats the coach.
    "BrittleStarDrive": {
        "hull": "BrittleStar",
        "mode": "BrittleStarTime",
        "assets": {i: POL + "BrittleStarAIBoostPolicy_BrittleStarTime.asset" for i in (1, 2, 3, 4)},
        # The mode file stands the policy down (engage runway 999 s), so seeds read the hull's own policy.
        "seed_assets": [SO + "BrittleStarAIBoostPolicy.asset"],
        "fields": {"alignDegrees": (0.0, 90.0), "releaseAlignDegrees": (0.0, 120.0), "stickBand": (0.0, 1.0),
                   "releaseStickBand": (0.0, 1.0), "engageRunwaySeconds": (0.0, 4.0), "releaseRunwaySeconds": (0.0, 3.0),
                   "minHoldSeconds": (0.0, 4.0), "minRestSeconds": (0.0, 4.0)},
        "ctrl": lambda p: {"coachOn": False},
        "lead": None, "rows": [],
        "nose_gene": 13.66, "nose_split": 15.04,       # the coach's trained Legendary rates (run1), as Regatta's line has
        "nose_mode_sets": True,
    },
}
LEAD_RANGE = (0.0, 3.0)
# The AI nose follow rate (VesselTransformer.AINoseFollowPerSecond). Games with no knob of their own fly the fleet lag,
# 1.5/s (LERP_AMOUNT); the judge sets the searched rate on the AI's transformer ("nose_gene": the shipped rate).
NOSE_RANGE = (1.5, 24.0)
LEAD = "noseLead"
NOSE34 = "noseFollowI34"       # with "nose_split": I3-I4's own nose rate; noseFollow then sets I1-I2


def wide_genome(hull, w, seed):
    """A curriculum course for this hull's game (W2's TrainingCourseOverlay): the game's own gates with genome placements
    laid round them; w=0 is the game's own course, w=1 the full genome."""
    import random
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import regatta_genome as RG
    mode = next(g["mode"] for k, g in GAMES.items() if g.get("hull", k) == hull)
    return RG.training_genome(random.Random(seed), w, for_mode=mode)


def conditions_for(a):
    """[(name, intensity, start, genome)]: the card's start at each rung; Time 10 at the --fast rungs; Time 0 (rest) at
    the --rest rungs; and --wide courses (W2's curriculum, I2) so the drive does not overfit the game's own track."""
    out = [(f"I{i}", i, None, None) for i in a.intensities]
    out += [(f"I{i}@T10", i, {a.hull: {"Time": 10}}, None) for i in a.fast]
    out += [(f"I{i}@T5", i, {a.hull: {"Time": 5}}, None) for i in a.l5]
    out += [(f"I{i}@T0", i, "rest", None) for i in a.rest]
    out += [(f"W{k}@I2", 2, None, wide_genome(a.hull, a.wide_w, 900 + k)) for k in range(a.wide)]
    out += [(f"L{i}", i, None, None) for i in a.long]
    return out


def condition_extra(g, name):
    """In-memory edits a condition adds on top of the candidate's (the --long rungs: the game's Regatta-length race)."""
    return g.get("long", {}) if name.startswith("L") else {}


def read_fields(rel, names):
    body = open(os.path.join(ROOT, rel), encoding="utf-8").read().split("m_EditorClassIdentifier:", 1)[-1]
    return {k: float(v) for k, v in re.findall(r"^  (\w+): (-?[0-9.]+(?:e-?\d+)?)\s*$", body, re.M) if k in names}


def read_refuel(row):
    """{refuelSlack, refuelBelow} as RaceAIRefuelGenes.asset ships them for this row."""
    rows = re.findall(r"PrismDetourSlack: (-?[0-9.]+)\s+RefuelBelow: (-?[0-9.]+)",
                      open(os.path.join(ROOT, REFUEL), encoding="utf-8").read())
    slack, below = rows[row]
    return {"refuelSlack": float(slack), "refuelBelow": float(below)}


def dims_for(g):
    ints = g.get("ints", ())
    dims = [{"name": k, "lo": lo, "hi": hi, **({"type": "int"} if k in ints else {})} for k, (lo, hi) in g["fields"].items()]
    dims += [{"name": k, "lo": lo, "hi": hi} for k, (lo, hi) in g.get("ctrl_fields", {}).items()]
    if "refuel_row" in g:
        dims += [{"name": k, "lo": lo, "hi": hi} for k, (_, (lo, hi)) in REFUEL_FIELDS.items()]
    if "nose_gene" in g:
        dims.append({"name": "noseFollow", "lo": NOSE_RANGE[0], "hi": NOSE_RANGE[1]})
    if "nose_split" in g:
        dims.append({"name": NOSE34, "lo": NOSE_RANGE[0], "hi": NOSE_RANGE[1]})
    if g["lead"] is not None:                          # None: the game has no aiNoseLead table (Skim Race)
        dims.append({"name": LEAD, "lo": LEAD_RANGE[0], "hi": LEAD_RANGE[1]})
    return dims


def shipped_genomes(g):
    """Every drive the game ships for this vessel and game, with today's nose lead and no crystal detour."""
    out = []
    # A game with no drive asset (the Meerkat: the gate race's own steering) seeds from its controller knobs alone.
    for rel in sorted(set(g.get("seed_assets") or g["assets"].values())) or [None]:
        p = read_fields(rel, g["fields"]) if rel else {}
        p.update({k: int(v) for k, v in p.items() if k in g.get("ints", ())})
        p.update(g.get("ctrl_defaults", {}))
        if "refuel_row" in g:
            p.update(read_refuel(g["refuel_row"]))
        if "nose_gene" in g:
            p["noseFollow"] = g["nose_gene"]
        if "nose_split" in g:
            p[NOSE34] = g["nose_split"]
        if g["lead"] is not None:
            p[LEAD] = g["lead"]
        p[CRYSTAL] = 0.0
        p["_from"] = os.path.basename(rel) if rel else "controller defaults"
        out.append(p)
    return out


def spec_for(hull, g, params, intensity=None):
    """None = the shipped AI. Otherwise ONE drive on every intensity's file, this hull's nose lead row and its crystal
    gene, all in memory."""
    if params is None:
        return {}
    fields = {k: params[k] for k in g["fields"] if k in params}
    ov = [{"asset": rel, "fields": fields} for rel in sorted(set(g["assets"].values()))]
    if "refuel_row" in g:
        ov.append({"asset": REFUEL, "fields": {f"Rows.{g['refuel_row']}.{f}": float(params[k])
                                               for k, (f, _) in REFUEL_FIELDS.items() if k in params}})
    # No mode knob: the judge sets the rate on the AI's transformer (left alone at the shipped fleet lag).
    nose = float(params.get("noseFollow", g.get("nose_gene", 0)))
    if "nose_split" in g and intensity is not None and intensity >= 3:   # I3-I4's own band
        nose = float(params.get(NOSE34, g["nose_split"]))
    # "nose_mode_sets": the mode sets its own per-intensity rate (Brittle Star Time's coach), so a candidate always
    # states its rate and only the shipped AI (params None, above) keeps the mode's table.
    out_nose = {hull: nose} if "nose_gene" in g and (g.get("nose_mode_sets") or nose != g["nose_gene"]) else None
    ctrl = {}
    if g["lead"] is not None:
        ctrl["aiNoseLead"] = list(g["rows"]) + [{"Hull": hull, "Gain": float(params.get(LEAD, g["lead"])),
                                                     "MaxExtraDegrees": g.get("lead_max", 40)}]
    if "ctrl" in g:
        ctrl.update(g["ctrl"](dict(g.get("ctrl_defaults", {}), **params)))
    out = {"overrides": ov}
    if ctrl:
        out["controller_overrides"] = ctrl
    if out_nose:
        out["ai_nose_follow"] = out_nose
    if params.get(CRYSTAL, 0) > 0:
        if "crystal_field" in g:                       # the mode's own crystal knob (Redline's aiTimeCrystalSlack)
            ctrl[g["crystal_field"]] = float(params[CRYSTAL])
        else:
            out["crystal_genes"] = {hull: float(params[CRYSTAL])}
    return out


def race_one(hull, g, params, cond, seeds, limit_s, tries=3):
    """One process: this candidate in one condition over the seeds. A player that died (killed for memory, a crash)
    is not the AI's fault and is raced again rather than scored as a DNF."""
    name, intensity, start, genome = cond
    extra = spec_for(hull, g, params, intensity)
    more = condition_extra(g, name)
    if more:
        extra = dict(extra)
        extra["overrides"] = list(extra.get("overrides", [])) + list(more.get("overrides", []))
        extra["controller_overrides"] = dict(extra.get("controller_overrides", {}), **more.get("controller_overrides", {}))
    for attempt in range(tries):
        wd = tempfile.mkdtemp(prefix=f"tas-{hull}-{name.replace('@', '-')}-")
        r = RJ.judge(genome, [hull], seeds, limit_s=limit_s, mode=g["mode"], intensity=intensity, workers=1, workdir=wd,
                     start=start, **extra)
        died = any("player exited" in n for n in r["notes"]) or len(r["races"]) < len(seeds)
        if not died:
            break
        print(f"retry {hull} {name} (attempt {attempt + 1}): {[n for n in r['notes'] if 'exited' in n]}", flush=True)
    rows = []
    for race in r["races"]:
        s = race["seats"][0] if race.get("seats") else {}
        rows.append({"seed": race.get("seed"), "course_seed": race.get("course_seed"), "finish_s": s.get("finish_s"),
                     "gates": s.get("gates", 0), "gates_total": race.get("gates_total") or 1, "outcome": race.get("outcome"),
                     "prism_hits": s.get("prism_hits"), "start_levels": s.get("start_levels")})
    if len(rows) < len(seeds):
        rows += [{"seed": sd, "finish_s": None, "gates": 0, "gates_total": 1, "outcome": "no-result"} for sd in seeds[len(rows):]]
    return {"condition": name, "races": rows, "notes": r["notes"]}


def loss(per_cond, ref):
    def race_loss(x, c):
        if x["finish_s"] is not None:
            return x["finish_s"] / ref[c]
        return 2.0 + (1.0 - x["gates"] / max(1, x["gates_total"]))
    lis = []
    for res in per_cond:
        ls = [race_loss(x, res["condition"]) for x in res["races"]]
        lis.append(sum(ls) / len(ls) + 0.5 * max(ls))
    return sum(lis) / len(lis) + 0.5 * max(lis), lis


class RaceCache:
    """Resume after a restart (cloud containers restart without warning): every finished (candidate, condition) race
    set is appended to <out>/races.jsonl, and an identical one asked again is answered from it. The search is
    deterministic (seeded sep-CMA-ES and Pareto rng), so a restarted run asks the same candidates in the same order and
    replays straight to the first race it never finished."""

    def __init__(self, path):
        self.path, self.mem, self.lock = path, {}, threading.Lock()
        if path and os.path.exists(path):
            for line in open(path, encoding="utf-8"):
                try:
                    rec = json.loads(line)
                    self.mem[rec["key"]] = rec["result"]
                except (ValueError, KeyError):
                    pass                                # a line cut off by the restart
        self.hits = 0

    @staticmethod
    def key(hull, g, params, cond, seeds, limit_s):
        name, intensity, start, genome = cond
        return json.dumps([hull, g["mode"], params, name, intensity, start, genome, list(seeds), limit_s], sort_keys=True)

    def get(self, k):
        r = self.mem.get(k)
        if r is not None:
            self.hits += 1
        return r

    def put(self, k, result):
        with self.lock:
            self.mem[k] = result
            if self.path:
                with open(self.path, "a", encoding="utf-8") as f:
                    f.write(json.dumps({"key": k, "result": result}) + "\n")


CACHE = RaceCache(None)


def evaluate_many(hull, g, cands, conds, seeds, limit_s, pool):
    """cands: [params|None]; every (candidate, condition) is one process, all in one pool; races already in the
    cache are not raced again."""
    out = [[None] * len(conds) for _ in cands]
    pending = {}                                       # key -> [(c, k)]: identical candidates (the shipped seeds) race once
    for c in range(len(cands)):
        for k in range(len(conds)):
            key = RaceCache.key(hull, g, cands[c], conds[k], seeds, limit_s)
            hit = CACHE.get(key)
            if hit is not None:
                out[c][k] = hit
            else:
                pending.setdefault(key, []).append((c, k))
    futs = {pool.submit(race_one, hull, g, cands[ck[0][0]], conds[ck[0][1]], seeds, limit_s): key
            for key, ck in pending.items()}
    for f in concurrent.futures.as_completed(futs):
        key = futs[f]
        res = f.result()
        # A broken build or a dead player is not the candidate's result: never cache it, so a resume races it again.
        if not any("judge failed" in n or "exited" in n or "build" in n.lower() for n in res["notes"]):
            CACHE.put(key, res)
        for c, k in pending[key]:
            out[c][k] = res
    return out


def summarise(per_cond):
    return {r["condition"]: [x["finish_s"] for x in r["races"]] for r in per_cond}


def complexity(p, seed, dims):
    """Genes moved off the seed by more than 5% of their range, plus the crystal gene when it is on."""
    moved = sum(1 for d in dims if abs(p[d["name"]] - seed[d["name"]]) > 0.05 * (d["hi"] - d["lo"]))
    return moved + (1 if p.get(CRYSTAL, 0) > 0 else 0)


def pareto_front(archive):
    """Non-dominated (loss, complexity) entries."""
    front = []
    for e in archive:
        if not any((o["loss"] <= e["loss"] and o["cx"] <= e["cx"]) and (o["loss"] < e["loss"] or o["cx"] < e["cx"])
                   for o in archive):
            front.append(e)
    return sorted(front, key=lambda e: e["cx"])


def child(parent, seed, dims, rng):
    """EXPAND a held gene, CONTRACT a moved one, or MUTATE the moved ones."""
    p = {k: v for k, v in parent.items() if not k.startswith("_")}
    names = [d["name"] for d in dims] + [CRYSTAL]
    rng_of = {d["name"]: (d["lo"], d["hi"]) for d in dims}
    rng_of[CRYSTAL] = CRYSTAL_RANGE
    moved = [n for n in names if abs(p[n] - seed[n]) > 0.05 * (rng_of[n][1] - rng_of[n][0]) or (n == CRYSTAL and p[n] > 0)]
    held = [n for n in names if n not in moved]
    op = rng.choice(["expand"] * 2 + ["contract"] * (1 if moved else 0) + ["mutate"] * (2 if moved else 0))
    if op == "expand" and held:
        n = rng.choice(held)
        lo, hi = rng_of[n]
        p[n] = min(hi, max(lo, p[n] + rng.gauss(0, 0.25) * (hi - lo))) if n != CRYSTAL else rng.uniform(40, hi)
    elif op == "contract" and moved:
        n = rng.choice(moved)
        p[n] = seed[n]
    else:
        for n in moved:
            lo, hi = rng_of[n]
            p[n] = min(hi, max(lo, p[n] + rng.gauss(0, 0.08) * (hi - lo)))
    for d in dims:
        if d.get("type") == "int":
            p[d["name"]] = int(round(p[d["name"]]))
    p["_op"] = op
    return p


def self_test():
    """Offline: every hull's table entry reads every searched field from every asset it names, each shipped value lies
    inside its bounds, the genome round-trips through the search space, and the race spec writes only the hull's own
    fields. A negative control (a field the asset does not carry) must fail. No player is started."""
    errs = []

    def check(hull, g):
        out = []
        for rel in sorted(set(g["assets"].values())):
            if not os.path.exists(os.path.join(ROOT, rel)):
                out.append(f"{hull}: missing asset {rel}")
        for rel in sorted(set(g.get("seed_assets") or g["assets"].values())):
            if not os.path.exists(os.path.join(ROOT, rel)):
                out.append(f"{hull}: missing asset {rel}")
                continue
            got = read_fields(rel, g["fields"])
            for k, (lo, hi) in g["fields"].items():
                if k not in got:
                    out.append(f"{hull}: {k} not a top-level number in {rel}")
                elif not lo <= got[k] <= hi:
                    out.append(f"{hull}: shipped {k}={got[k]} outside [{lo}, {hi}]")
        if not out:
            dims = dims_for(g)
            space = Space(dims)
            for p in shipped_genomes(g):
                q = space.decode(space.encode(p))
                for d in dims:
                    if abs(float(q[d["name"]]) - float(p[d["name"]])) > 1e-6 * max(1.0, abs(d["hi"])):
                        out.append(f"{hull}: {d['name']} does not round-trip ({p[d['name']]} -> {q[d['name']]})")
                spec = spec_for(hull, g, p)
                for ov in spec["overrides"]:
                    own = ({f"Rows.{g['refuel_row']}.{f}" for f, _ in REFUEL_FIELDS.values()}
                           if ov["asset"] == REFUEL and "refuel_row" in g else set(g["fields"]))
                    extra = set(ov["fields"]) - own
                    if extra:
                        out.append(f"{hull}: spec writes foreign fields {sorted(extra)}")
                lead_rows = spec.get("controller_overrides", {}).get("aiNoseLead")
                if g["lead"] is None and lead_rows is not None:
                    out.append(f"{hull}: spec writes an aiNoseLead table the game does not have")
                elif g["lead"] is not None and not any(r["Hull"] == hull for r in lead_rows or []):
                    out.append(f"{hull}: spec has no nose lead row")
                if "ai_nose_follow" in spec and not g.get("nose_mode_sets"):
                    out.append(f"{hull}: the shipped genome changes the nose rate")
                if "nose_split" in g:                  # each band reaches only its own rungs
                    q = dict(p, noseFollow=3.0, **{NOSE34: 11.0})
                    lo, hi = spec_for(hull, g, q, 2), spec_for(hull, g, q, 4)
                    if lo.get("ai_nose_follow") != {hull: 3.0} or hi.get("ai_nose_follow") != {hull: 11.0}:
                        out.append(f"{hull}: the nose bands do not split by intensity ({lo.get('ai_nose_follow')}, "
                                   f"{hi.get('ai_nose_follow')})")
        return out

    for key, g in sorted(GAMES.items()):
        hull = g.get("hull", key)
        e = check(hull, g)
        errs += e
        print(f"{hull:8s} {g['mode']:12s} {'ok' if not e else 'FAIL'}  {len(g['fields'])} fields")
    bogus = dict(GAMES["Urchin"], fields=dict(GAMES["Urchin"]["fields"], NotAField=(0.0, 1.0)))
    if not check("Urchin", bogus):
        errs.append("negative control: a field the asset does not carry passed")
    else:
        print("negative control: an unknown field fails (ok)")
    for e in errs:
        print("  " + e)
    print("self-test", "FAILED" if errs else "passed")
    return 1 if errs else 0


def main():
    if "--self-test" in sys.argv[1:]:
        sys.exit(self_test())
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--self-test", action="store_true", help="offline check of the hull table (no players)")
    ap.add_argument("--hull", required=True, choices=sorted(GAMES))
    ap.add_argument("--search", action="store_true")
    ap.add_argument("--confirm", help="JSON file of params (or a list of them) to race against the shipped AI")
    ap.add_argument("--generations", type=int, default=4, help="phase 1 (constrained) generations")
    ap.add_argument("--pareto", type=int, default=4, help="phase 2 (Pareto) generations")
    ap.add_argument("--popsize", type=int, default=6)
    ap.add_argument("--seeds", default="11,12")
    ap.add_argument("--intensities", default="1,2,3,4")
    ap.add_argument("--fast", default="1,4", help="rungs also raced at Time 10 (empty: none)")
    ap.add_argument("--l5", default="", help="rungs also raced from Time 5, the level-5 upgrade's threshold (empty: none)")
    ap.add_argument("--rest", default="", help="rungs also raced from Time 0 (empty: none)")
    ap.add_argument("--long", default="", help="rungs also raced at Regatta length (the game's 'long' edits; empty: none)")
    ap.add_argument("--wide", type=int, default=0, help="how many W2 curriculum courses (raced at I2)")
    ap.add_argument("--wide-w", type=float, default=0.5, help="curriculum weight of the wide courses (0-1)")
    ap.add_argument("--seed-from", action="append", default=[], help="a best.json (or list) to seed phase 1 with; repeatable")
    ap.add_argument("--limit", type=float, default=400)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--out", default=None)
    ap.add_argument("--hold", default="", help="drive fields held at their shipped value, not searched (comma list)")
    a = ap.parse_args()
    a.intensities = [int(s) for s in a.intensities.split(",") if s]
    a.fast = [int(s) for s in a.fast.split(",") if s]
    a.rest = [int(s) for s in a.rest.split(",") if s]
    a.l5 = [int(s) for s in a.l5.split(",") if s]
    a.long = [int(s) for s in a.long.split(",") if s]
    if a.long and "long" not in GAMES[a.hull]:
        ap.error(f"--long: {a.hull}'s game has no Regatta-length edits in GAMES")
    # A confirm always races I4 from Time 5: at level-5 speed a fleet-lag nose orbits a gate and never clears it
    # (the Rhino in Headlong, FINDINGS row 21), and a drive that only won from rest must not ship that back.
    if a.confirm and 4 not in a.l5:
        a.l5.append(4)
    g = GAMES[a.hull]
    a.hull = g.get("hull", a.hull)                     # a game entry may train another entry's hull (BrittleStarDrive)
    hold = [s for s in a.hold.split(",") if s]
    if hold:
        unknown = [k for k in hold if k not in g["fields"]]
        if unknown:
            ap.error(f"--hold: {unknown} are not {a.hull}'s drive fields {sorted(g['fields'])}")
        # A held field leaves the genome: no candidate overrides it, so every race flies the shipped value.
        g = dict(g, fields={k: v for k, v in g["fields"].items() if k not in hold})
    seeds = [int(s) for s in a.seeds.split(",") if s]
    conds = conditions_for(a)
    out = a.out or tempfile.mkdtemp(prefix=f"time-ai-{a.hull}-")
    os.makedirs(out, exist_ok=True)
    log = open(os.path.join(out, "log.jsonl"), "a")
    global CACHE
    CACHE = RaceCache(os.path.join(out, "races.jsonl"))
    if CACHE.mem:
        print(f"resume: {len(CACHE.mem)} finished race sets in {out}/races.jsonl", flush=True)
    RJ.build()
    pool = concurrent.futures.ThreadPoolExecutor(a.workers)
    t0 = time.time()

    def emit(rec):
        rec["t"] = round(time.time() - t0, 1)
        log.write(json.dumps(rec) + "\n")
        log.flush()
        print(json.dumps({k: v for k, v in rec.items() if k != "per_cond"}), flush=True)

    ref_path = os.path.join(out, f"reference_{'_'.join(map(str, seeds))}_{'_'.join(c[0] for c in conds)}.json")
    if not os.path.exists(ref_path):
        base = evaluate_many(a.hull, g, [None], conds, seeds, a.limit, pool)[0]
        ref = {}
        for r in base:
            done = [x["finish_s"] for x in r["races"] if x["finish_s"] is not None]
            ref[r["condition"]] = sum(done) / len(done) if done else a.limit
        json.dump({"ref": ref, "per_cond": base}, open(ref_path, "w"), indent=1)
        emit({"kind": "baseline", "seeds": seeds, "ref": ref, "times": summarise(base)})
    ref = json.load(open(ref_path))["ref"]

    def score(cands, kind, gen):
        res = evaluate_many(a.hull, g, cands, conds, seeds, a.limit, pool)
        recs = []
        for p, r in zip(cands, res):
            l, lis = loss(r, ref)
            rec = {"kind": kind, "gen": gen, "loss": round(l, 4), "per_cond": r,
                   "per_condition": dict(zip([c[0] for c in conds], [round(x, 3) for x in lis])),
                   "times": summarise(r), "params": p}
            recs.append(rec)
            emit(rec)
        return recs

    if a.confirm:
        cands = json.load(open(a.confirm))
        cands = cands if isinstance(cands, list) else [cands]
        score([None] + cands, "confirm", 0)
        return

    if not a.search:
        return
    dims = dims_for(g)
    space = Space(dims)
    es = SepCMAES(space, seed=1, popsize=a.popsize, sigma0=0.2)
    shipped = shipped_genomes(g)
    # --seed-from: earlier searches' best genomes join the shipped ones ("a new round grows from every genome that
    # exists", AI_PLAN.md); genes they lack (a gene added since) take the shipped value.
    for path in a.seed_from:
        extra = json.load(open(path))
        for e in (extra if isinstance(extra, list) else [extra]):
            shipped.append(dict(shipped[0], **{k: v for k, v in e.items() if not k.startswith("_")}))
    es.seed([space.encode(p) for p in shipped])
    archive = []
    for gen in range(a.generations):
        us = es.ask()
        cands = [dict(space.decode(u), **{CRYSTAL: 0.0}) for u in us]
        recs = score(cands, "phase1", gen)
        es.tell(us, [r["loss"] for r in recs])
        archive += recs
        best = min(archive, key=lambda r: r["loss"])
        emit({"kind": "gen", "phase": 1, "gen": gen, "best_loss": best["loss"], "best": best["params"]})

    # Phase 2: the best constrained genome is the seed; complexity is measured from it.
    seed = {k: v for k, v in min(archive, key=lambda r: r["loss"])["params"].items() if not k.startswith("_")}
    for r in archive:
        r["cx"] = complexity(r["params"], seed, dims)
    rng = random.Random(7)
    for gen in range(a.pareto):
        front = pareto_front(archive)
        cands = [child(rng.choice(front)["params"], seed, dims, rng) for _ in range(a.popsize)]
        recs = score(cands, "pareto", gen)
        for r in recs:
            r["cx"] = complexity(r["params"], seed, dims)
        archive += recs
        front = pareto_front(archive)
        emit({"kind": "front", "phase": 2, "gen": gen,
              "front": [{"loss": e["loss"], "cx": e["cx"], "params": e["params"]} for e in front]})
    front = pareto_front(archive)
    json.dump([{"loss": e["loss"], "cx": e["cx"], "params": e["params"]} for e in front],
              open(os.path.join(out, "front.json"), "w"), indent=1)
    best = min(archive, key=lambda r: r["loss"])
    json.dump(best["params"], open(os.path.join(out, "best.json"), "w"), indent=1)
    print("best", best["loss"], best["params"], "->", out)


if __name__ == "__main__":
    main()

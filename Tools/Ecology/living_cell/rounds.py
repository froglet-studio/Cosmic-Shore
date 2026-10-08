"""The composition / dial rounds of Part 1 (each round is a dict of named configs; run.py iterate runs them)."""
R1 = {
    "r1_default": {},                                   # iteration-1 rules (niches, hunger-gated packs, caps 40/40)
    "r1_flora_fast": dict(flora_r=0.06),
    "r1_pack_lean": dict(pack_n=16, pack_cap=30, lurker_n=12, lurker_cap=24),
    "r1_enriched": dict(plant_cap=60, flora_r=0.05),
}

# Round 2: caps were binding in R1 (CV ~ 0 at the cap) - make FOOD the limit. Caps become backstops (2-3x the
# expected equilibrium) and flora productivity drops to the order of herbivore demand (~70 vol/s).
HIGHCAP = dict(grazer_cap=6000, locust_cap=3000, pack_cap=120, thief_cap=400, lurker_cap=120,
               **{"thief.metab": 0.015, "thief.imax": 0.4})
R2 = {
    "r2_r004": dict(HIGHCAP, flora_r=0.004),
    "r2_r006": dict(HIGHCAP, flora_r=0.006),
    "r2_r010": dict(HIGHCAP, flora_r=0.010),
    "r2_r006_cap60": dict(HIGHCAP, flora_r=0.006, plant_cap=60),
}

# Round 3: territory. R2's lurkers (cap-bound, creeping from 600 u) and thieves (tailing forever) never left a
# pilot alone. Leashes + a lurker that has to eat to live; plus the sizes of the two populations.
BASE3 = dict(HIGHCAP, flora_r=0.006, lurker_cap=60, thief_cap=150, thief_n=45, **{"lurker.metab": 0.03})
R3 = {
    "r3_base": BASE3,
    "r3_lurk_lean": dict(BASE3, lurker_n=12, lurker_cap=40),
    "r3_thief_short": dict(BASE3, **{"thief.leash": 600.0}),
    "r3_r004": dict(BASE3, flora_r=0.004),
}

# Round 4: encounters are now ACTIVE-state events (metrics.Flight). Lurkers food-limited (R3 still capped).
R4 = {
    "r4_base": dict(BASE3),
    "r4_lurk06": dict(BASE3, **{"lurker.metab": 0.06}),
    "r4_lurk06_cap40": dict(BASE3, lurker_cap=40, **{"lurker.metab": 0.06}),
    "r4_lurk10": dict(BASE3, **{"lurker.metab": 0.10}),
}

# Round 5: cohorts of schooling species expand as clumps; packs go for whichever is nearer (prey or pilot).
BASE5 = dict(BASE3, **{"lurker.metab": 0.10})
R5 = {
    "r5_base": BASE5,
    "r5_locust_dense": dict(BASE5, locust_n=500),
    "r5_pack_lean": dict(BASE5, pack_n=16),
    "r5_locust_dense_r008": dict(BASE5, locust_n=500, flora_r=0.008),
}

# Long horizon: does the cell BREATHE (a flora recovery feeding a second herbivore / locust boom)?
LONG = {"long_r5d008": dict(BASE5, locust_n=500, flora_r=0.008)}
CONS = dict(BASE5, locust_n=500, flora_r=0.008)

# Round 6: the macro rates FITTED to the micro level (calibrate.py, results/calibrate.json) + the three fixes
# the 60-min run asked for. Every number below the CAL line comes from the fit, not from a guess.
CAL = {"grazer.F_half": 7.8125, "locust.F_half": 500.0, "thief.F_half": 1200.0, "pack.a_attack": 0.0006,
       "lurker.a_attack": 0.0384, "grazer.hop": 0.012, "locust.hop": 0.012, "pack.hop": 0.008}
BASE6 = dict(CONS, **CAL, **{"physarum.upkeep": 0.0015})
R6 = {
    "r6_cal": BASE6,
    "r6_cal_thief_free": dict(BASE6, **{"pack.prey_names": ("grazer", "locust")}),
    "r6_cal_pack06": dict(BASE6, pack_metab=0.06, **{"pack.prey_names": ("grazer", "locust")}),
    "r6_cal_lurk20": dict(BASE6, **{"pack.prey_names": ("grazer", "locust"), "lurker.metab": 0.2}),
}

# Round 7: thieves still starved in R6 even with no pack predation (r6_cal_thief_free) -> the fitted
# thief.F_half (x4) made the far-away macro thief a slow eater. Test two thief fixes on r6_cal_lurk20.
BASE7 = dict(BASE6, **{"pack.prey_names": ("grazer", "locust"), "lurker.metab": 0.2})
R7 = {
    "r7_base": BASE7,
    "r7_thief_fh600": dict(BASE7, **{"thief.F_half": 600.0}),
    "r7_thief_metab": dict(BASE7, **{"thief.metab": 0.015}),
    "r7_thief_both_pack90": dict(BASE7, pack_cap=90, **{"thief.F_half": 600.0, "thief.metab": 0.015}),
}
# NOTE r7_thief_metab duplicated r7_base: HIGHCAP already sets thief.metab 0.015 (kept as run, flagged in DISCOVERIES).

# The recommended cell (DISCOVERIES "Living cell"): R7's base. Thieves stay the fragile link (1 of 3 seeds lost them
# in R7); every thief fix tried moved the extinction onto locusts, which share the flora with them.
FINAL = dict(BASE7)   # superseded below by R8 (kept so R8 can be read as a delta)

# LOD radius sweep on FINAL: (expand_r, ahead_r, absorb_r) scaled together around the default 520/760/680.
LOD = {
    "lod_360": dict(expand_r=360.0, ahead_r=520.0, absorb_r=470.0),
    "lod_520": dict(expand_r=520.0, ahead_r=760.0, absorb_r=680.0),
    "lod_700": dict(expand_r=700.0, ahead_r=1000.0, absorb_r=900.0),
}

# Round 8 (after the 45-min final run lost thieves in 3/4 seeds to STARVATION - 54 of 58 losses, 181 vol eaten in
# 45 min against the grazers' 165k): off-screen thieves could only eat flora, where the calibrated grazers out-compete
# them, while their actual niche (stolen trail, hoards) existed only near pilots. Give the macro level each species'
# OWN diet. Masks: FLORA=1, TRAIL=2, SKEL=4, HOARD=8 bits (world.py kinds 0..3).
_F, _T, _S, _H = 1, 2, 4, 8
R8 = {
    "r8_thief_loose": dict(FINAL, **{"thief.macro_mask": _F | _T | _S | _H}),
    "r8_own_diets": dict(FINAL, **{"thief.macro_mask": _F | _T | _S | _H, "grazer.macro_mask": _F | _S,
                                   "locust.macro_mask": _F | _T}),
}

# The recommended cell after R8: per-species macro diets (= the micro diets). 0 extinctions in 3 seeds x 30 min.
FINAL = dict(R8["r8_own_diets"])

# ==========================================================================================================
# ROUND 2 OF DIRECTION G (2026-10-08): the four open items of the first pass.
#   1. thieves starve out (3/4 seeds by ~35 min): seed flora at its GRAZED level, not 60% of its cap
#   2. packs / lurkers end at their caps: is the cap doing the work?
#   3. soil N grows linearly with the pilots' trail: a sink that is not a timer (plant recruitment)
#   4. the macro rates were fitted before R8's diet fix: refit
# R9: the opening transient. Post-crash levels read off results/final.json (minutes 3-10): flora ~5-7k vol
# (13% of the 48k cap), grazers 300-500, locusts 150-300.
GRAZED = dict(flora_seed_frac=0.13, grazer_n=400, locust_n=250)
R9 = {
    "r9_grazed": dict(FINAL, **GRAZED),
    "r9_bloom": dict(FINAL, flora_seed_frac=0.13, grazer_n=150, locust_n=100),
    "r9_grazed_recruit": dict(FINAL, **GRAZED, flora_recruit=0.05),
    "r9_grazed_nocap": dict(FINAL, **GRAZED, pack_cap=300, lurker_cap=180),   # diagnostic: where food alone stops them
}

# R9 result: the opening crash is gone (flora 6k -> 2-3k -> 6k instead of 29k -> 4k) but thieves STILL fall
# 45 -> ~5 by minute 30 (births 28 vs 135 starved). The crash was not the cause. Their own food is stolen trail,
# ~3 steals/min x 3 vol = 0.15 vol/s for the whole population, against 45 x 0.025 = 1.1 vol/s of metabolism:
# the trail niche feeds ~6 thieves. Their fallback food (flora, skeletons) is the grazers' food, and the
# grazers out-eat them. And without caps (r9_grazed_nocap) packs overshoot to 230 and EAT THE PREY OUT
# (grazers + locusts extinct in seed 3): the pack cap was holding off a predator-prey collapse.
# R10: (i) partition the carrion - grazers eat plants only, thieves are the scavengers (skeletons + stolen trail
# + larder); (ii) a real brake on packs instead of the cap: higher metabolism and/or Holling III switching.
SCAV = dict(GRAZED, **{"grazer.diet": _F, "grazer.macro_mask": _F})
NOCAP = dict(pack_cap=300, lurker_cap=180)
R10 = {
    "r10_scav": dict(FINAL, **SCAV),
    "r10_scav_nocap_m08": dict(FINAL, **SCAV, **NOCAP, pack_metab=0.08),
    "r10_scav_nocap_h3": dict(FINAL, **SCAV, **NOCAP, **{"pack.switch_ref": 20.0, "lurker.switch_ref": 20.0}),
    "r10_scav_nocap_h3_m08": dict(FINAL, **SCAV, **NOCAP, pack_metab=0.08, **{"pack.switch_ref": 20.0, "lurker.switch_ref": 20.0}),
}

# R10 result: worse. Carrion partition starved grazers (190 vs 300) and with them the lurkers; thieves were not
# helped (31 births / 130 starved). Diagnosis (instrumented run, scratch): thieves live near pilots, as AGENTS,
# and a thief there tails the pilot until it starves - it only ate at its nest while under 40% of e_birth, so a
# fed thief never reached e_birth and NEVER BRED. Packs: 1326 of 1331 kills were MICRO (near pilots); packs
# drift to the pilots' prey crowd, convert every 3 kills into a pup and eat the prey out once uncapped.
# R11: thief.feed_fix (a starving thief goes home; the larder feeds it to e_max at imax) and a pack that eats
# only part of a kill (pack.eff): the rest stays as a carcass (SKEL, scavenger food) - lower conversion lifts
# the prey's equilibrium off the paradox-of-enrichment knife edge, in both LOD levels at once.
B11 = dict(FINAL, **GRAZED, **{"thief.feed_fix": True})
R11 = {
    "r11_base": B11,
    "r11_eff05_nocap": dict(B11, **NOCAP, **{"pack.eff": 0.5}),
    "r11_eff03_nocap": dict(B11, **NOCAP, **{"pack.eff": 0.3}),
    "r11_eff04_m06_nocap": dict(B11, **NOCAP, pack_metab=0.06, **{"pack.eff": 0.4}),
}

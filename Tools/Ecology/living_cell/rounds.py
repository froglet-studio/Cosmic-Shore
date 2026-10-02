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

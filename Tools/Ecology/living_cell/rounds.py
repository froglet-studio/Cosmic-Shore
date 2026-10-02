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

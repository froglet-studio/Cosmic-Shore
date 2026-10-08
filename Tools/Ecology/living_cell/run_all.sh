#!/bin/sh
# Re-run every Direction G study on the current code. From Tools/Ecology:  sh living_cell/run_all.sh
# Wall time on a 4-core container: ~3 h (the 30-60 sim-min cells dominate). Results land in living_cell/results/.
set -e
L=living_cell/results; mkdir -p $L
# Part 1 - the living cell
python3 -m living_cell.run baseline 30            > $L/baseline.log 2>&1      # the first composition (kept as the negative it is)
python3 -m living_cell.run iterate R1 20          > $L/iter_R1.log 2>&1       # rounds of composition / dials (rounds.py)
python3 -m living_cell.run iterate R2 15          > $L/iter_R2.log 2>&1
python3 -m living_cell.run iterate R3 15          > $L/iter_R3.log 2>&1
python3 -m living_cell.run iterate R4 15          > $L/iter_R4.log 2>&1
python3 -m living_cell.run iterate R5 15          > $L/iter_R5.log 2>&1
python3 -m living_cell.run iterate LONG 60 1,2 long > $L/iter_long.log 2>&1   # does it breathe over an hour?
python3 -m living_cell.run consistency CONS       > $L/consistency.log 2>&1   # all-micro vs all-macro, same cell
python3 -m living_cell.calibrate CONS             > $L/calibrate.log 2>&1     # fit macro rates to micro
python3 -m living_cell.run iterate R6 30 1,2,3          > $L/iter_R6.log 2>&1       # the calibrated cell
python3 -m living_cell.run iterate R7 30 1,2,3 > $L/iter_R7.log 2>&1       # thief-starvation fixes
python3 -m living_cell.run iterate R8 30 1,2,3 > $L/iter_R8.log 2>&1       # per-species macro diets (the thief fix)
python3 -m living_cell.run final                  > $L/final.log 2>&1         # recommended cell, 4 seeds x 45 min
python3 -m living_cell.run controls               > $L/controls.log 2>&1      # every metric's negative control
python3 -m living_cell.run lod                    > $L/lod.log 2>&1           # LOD radius sweep
# Part 2 - multi-domain swarms
python3 -m living_cell.domains 20                 > $L/domains.log 2>&1
python3 -m living_cell.domains 20 drift           > $L/domains_drift.log 2>&1
# the viewer (a 5-minute explorer flight through the recommended cell)
python3 -m living_cell.viewer 2                   > $L/viewer.log 2>&1
# ---- ROUND 2 (2026-10-08, branch cece/eco-living-cell-r2): the four open items. ~4 h more on 4 cores.
for R in R9 R10 R11 R12 R13 R14; do python3 -m living_cell.run iterate $R 30 1,2,3 r2_iterate > $L/r2_iter_$R.log 2>&1; done
python3 -m living_cell.micro_design M13b 34       > $L/r2_micro_design.txt 2>&1   # all-micro pack rule design
python3 -m living_cell.run consistency CAL2 15 r2_consistency > $L/r2_consistency.log 2>&1
python3 -m living_cell.calibrate CAL2 r2_consistency.json r2_calibrate.json > $L/r2_calibrate.log 2>&1
python3 -m living_cell.run iterate R15 45 1,2,3 r2_iterate > $L/r2_iter_R15.log 2>&1
python3 -m living_cell.run iterate R16 30 1,2,3 r2_iterate > $L/r2_iter_R16.log 2>&1
python3 -m living_cell.run final2                 > $L/r2_final.log 2>&1          # FINAL2, 4 seeds x 45 min
python3 -m living_cell.run controls2              > $L/r2_controls.log 2>&1
echo ALLDONE

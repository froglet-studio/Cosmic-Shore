#!/bin/sh
# Re-run every substrate study on the current defaults. Two lanes in parallel, then the benchmark ALONE
# (timings are only meaningful on an idle machine). From Tools/Ecology:  sh substrate/run_all.sh
set -e
L=substrate/results/logs; mkdir -p $L
( python3 -m substrate.laws > $L/laws.log 2>&1; python3 -m substrate.quorum_demo > $L/quorum.log 2>&1;
  python3 -m substrate.assembly_demo > $L/assembly.log 2>&1; python3 -m substrate.nbr_study > $L/nbr.log 2>&1 ) &
( python3 -m substrate.run_species > $L/scorecard.log 2>&1; python3 -m substrate.frac_study > $L/frac.log 2>&1;
  python3 -m substrate.blend_study > $L/blend.log 2>&1 ) &
wait
python3 -m substrate.bench > $L/bench.log 2>&1
python3 -m substrate.make_viewer > $L/viewer.log 2>&1
echo ALLDONE

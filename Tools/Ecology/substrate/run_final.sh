#!/bin/sh
# After a parameter change: rerun laws, quorum and scorecard in parallel, then the bench ALONE, then the viewer.
L=substrate/results/logs; mkdir -p $L
python3 -m substrate.laws > $L/laws.log 2>&1 &
python3 -m substrate.quorum_demo > $L/quorum.log 2>&1 &
python3 -m substrate.run_species > $L/scorecard.log 2>&1 &
wait
python3 -m substrate.bench > $L/bench.log 2>&1
python3 -m substrate.make_viewer > $L/viewer.log 2>&1
echo FINALDONE

#!/bin/bash
# After the search chain: snap-trap stage 2 on R + R_hard, then elements, re-route, recordings, sandbox, summary.
cd "$(dirname "$0")"
python3 search.py snaptrap 12 "$(python3 -c "import json;print(json.dumps(json.load(open('results/search_snaptrap_best.json'))['params']))")" > results/search_snaptrap_stage2.log 2>&1
python3 elements.py > results/elements.log 2>&1
python3 reroute.py > results/reroute.log 2>&1
python3 record.py best > results/record.log 2>&1
python3 sandbox.py > /dev/null 2>&1
python3 summary.py > results/summary.md 2>&1
echo FINISHED > results/finish.done

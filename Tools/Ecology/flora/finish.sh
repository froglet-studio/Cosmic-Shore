#!/bin/bash
# After the search chain: snap-trap stage 2 on R + R_hard, then elements, re-route, recordings, sandbox, summary.
cd "$(dirname "$0")"
python3 search.py coral 16 '{"Dv":0.0209,"Du":0.3,"rate":76.85,"k":0.01}' > results/search_coral.log 2>&1
python3 search.py snaptrap 12 "$(python3 -c "import json;print(json.dumps(json.load(open('results/search_snaptrap_best.json'))['params']))")" > results/search_snaptrap_stage2.log 2>&1
python3 search.py walker 12 "$(python3 -c "import json;p=json.load(open('results/search_walker_best.json'))['params'];p['guard']=0.4;print(json.dumps(p))")" > results/search_walker_stage2.log 2>&1
python3 search.py physarum 10 "$(python3 -c "import json;p=json.load(open('results/search_physarum_best.json'))['params'];p['heart_guard']=50;p['prism_vol']=18;print(json.dumps(p))")" > results/search_physarum_stage2.log 2>&1
python3 elements.py > results/elements.log 2>&1
python3 reroute.py > results/reroute.log 2>&1
python3 record.py best > results/record.log 2>&1
python3 sandbox.py > /dev/null 2>&1
python3 summary.py > results/summary.md 2>&1
echo FINISHED > results/finish.done

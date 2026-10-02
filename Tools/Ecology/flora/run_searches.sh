#!/bin/bash
# Overnight search chain (one species at a time; each scorecard already uses 4 processes).
cd "$(dirname "$0")"
python3 search.py snaptrap 20 '{}' > results/search_snaptrap.log 2>&1
python3 search.py spores 20 '{}' > results/search_spores.log 2>&1
python3 search.py walker 20 '{}' > results/search_walker.log 2>&1
python3 search.py coral 18 '{"Dv":0.02,"Du":0.3,"rate":40,"k":0.01}' > results/search_coral.log 2>&1
python3 search.py physarum 14 '{"wake_dep":3}' > results/search_physarum.log 2>&1

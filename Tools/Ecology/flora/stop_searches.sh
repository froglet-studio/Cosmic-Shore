#!/bin/bash
# Stop the search chain (kept as a script so the pattern never matches the caller's own command line).
for p in $(pgrep -f "run_searches.sh"); do kill $p; done
for p in $(pgrep -f "python3 search.py"); do kill $p; done

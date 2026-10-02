#!/bin/bash
cd "$(dirname "$0")"
until [ -f results/search_physarum_best.json ]; do sleep 30; done
./finish.sh

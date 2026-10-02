#!/bin/sh
# Run the JS half of the fidelity gate in 4 parallel groups and merge (node fidelity_js.js is single-threaded).
#   sh Tools/Ecology/flight/run_fidelity_all.sh            # dt 0.1  -> results/fidelity_js.json
#   sh Tools/Ecology/flight/run_fidelity_all.sh --dt30     # dt 1/30 -> results/fidelity_js_dt30.json
cd "$(dirname "$0")" || exit 1
EXTRA="$1"; TAG=""; [ "$EXTRA" = "--dt30" ] && TAG="_dt30"
node fidelity_js.js pack thief locust $EXTRA --out results/part1$TAG.json > results/part1$TAG.log 2>&1 &
node fidelity_js.js lurker stampede leviathan $EXTRA --out results/part2$TAG.json > results/part2$TAG.log 2>&1 &
node fidelity_js.js mobber grazer fortress $EXTRA --out results/part3$TAG.json > results/part3$TAG.log 2>&1 &
node fidelity_js.js snaptrap $EXTRA --out results/part4$TAG.json > results/part4$TAG.log 2>&1 &
wait
TAG="$TAG" node -e "
const fs=require('fs');const t=process.env.TAG;
const parts=[1,2,3,4].map(i=>JSON.parse(fs.readFileSync('results/part'+i+t+'.json')));
const o=Object.assign({},parts[0],{runs:[].concat(...parts.map(p=>p.runs))});
fs.writeFileSync('results/fidelity_js'+t+'.json',JSON.stringify(o));
for(const i of [1,2,3,4]) fs.unlinkSync('results/part'+i+t+'.json');"
cat results/part?$TAG.log > results/fidelity_js$TAG.log; rm -f results/part?$TAG.log

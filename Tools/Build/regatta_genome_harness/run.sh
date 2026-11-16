#!/usr/bin/env bash
# The Regatta course genome through the GAME'S OWN CODE, no Unity editor: builds Amoebius (Port/, which compiles
# Assets/_Scripts) once into $AMOEBIUS_OUT, builds this harness against it, and runs one job. Needs a .NET 10 SDK
# (`apt-get install -y dotnet-sdk-10.0` in a cloud session).
#
#   bash Tools/Build/regatta_genome_harness/run.sh measure          # rewrite measured.json (then regatta_gene_catalogue.py)
#   bash Tools/Build/regatta_genome_harness/run.sh lay G.json O.json  # lay one genome; O.json = rings, prisms, reports
#   bash Tools/Build/regatta_genome_harness/run.sh repair IN OUT     # RegattaCourseGenome.Repair per JSON line
#   bash Tools/Build/regatta_genome_harness/run.sh check GENOMES     # the invariants (empty genome = today's course,
#                                                                    # no placement prism breaks a carve rule)
#   bash Tools/Build/regatta_genome_harness/run.sh fixture           # rewrite repair_fixture.jsonl (the Python mirror's
#                                                                    # parity fixture: regatta_genome.py --self-test)
#   bash Tools/Build/regatta_genome_harness/run.sh gate              # fixture --check + check on 40 random genomes
# Set AMOEBIUS_REBUILD=1 to rebuild Amoebius after editing Assets/_Scripts.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
export AMOEBIUS_OUT="${AMOEBIUS_OUT:-${TMPDIR:-/tmp}/amoebius_regatta}"
export COSMIC_SHORE_PROJECT="$ROOT" COSMIC_SHORE_NET=off COSMIC_SHORE_AUDIO=off
if [ ! -f "$AMOEBIUS_OUT/CosmicShore.Live.dll" ] || [ "${AMOEBIUS_REBUILD:-0}" = "1" ]; then
  (cd "$ROOT/Port" && dotnet build src/CosmicShore.Player -c Release -o "$AMOEBIUS_OUT" -v q -nologo) >&2
fi
BIN="$AMOEBIUS_OUT/harness"
dotnet build "$HERE/Harness.csproj" -c Release -o "$BIN" -v q -nologo -p:AmoebiusOut="$AMOEBIUS_OUT" >&2
cp -n "$AMOEBIUS_OUT"/*.dll "$BIN"/ 2>/dev/null || true
JOB="${1:-}"; shift || true
case "$JOB" in
  measure)
    python3 "$ROOT/Tools/Build/regatta_gene_catalogue.py" --draft > "$BIN/draft.json"
    dotnet "$BIN/RegattaGenomeHarness.dll" measure "$BIN/draft.json" "$HERE/measured.json"
    echo "wrote $HERE/measured.json" >&2 ;;
  lay|repair|check)
    dotnet "$BIN/RegattaGenomeHarness.dll" "$JOB" "$@" ;;
  fixture|gate)
    EV="$ROOT/Tools/Compute/evaluators/regatta_genome.py"
    python3 "$EV" --parity-inputs > "$BIN/parity_in.jsonl"
    dotnet "$BIN/RegattaGenomeHarness.dll" repair "$BIN/parity_in.jsonl" "$BIN/parity_out.jsonl"
    python3 - "$BIN/parity_in.jsonl" "$BIN/parity_out.jsonl" > "$BIN/repair_fixture.jsonl" <<'PY'
import json, sys
ins = [l for l in open(sys.argv[1]) if l.strip()]
outs = [json.loads(l)["genome"] for l in open(sys.argv[2]) if l.strip()]
assert len(ins) == len(outs), (len(ins), len(outs))
for a, b in zip(ins, outs):
    print(json.dumps({"in": json.loads(a), "out": b}, sort_keys=True))
PY
    if [ "$JOB" = "fixture" ]; then
      cp "$BIN/repair_fixture.jsonl" "$HERE/repair_fixture.jsonl"; echo "wrote $HERE/repair_fixture.jsonl" >&2
      python3 "$EV" --self-test
    else
      cmp -s "$BIN/repair_fixture.jsonl" "$HERE/repair_fixture.jsonl" \
        && echo "repair_fixture.jsonl: up to date" || { echo "repair_fixture.jsonl: STALE (run.sh fixture)"; exit 1; }
      python3 "$EV" --random 40 --seed 20261116 --out "$BIN/gate" > /dev/null
      for f in "$BIN"/gate/genome_*.json; do python3 -c "import json,sys; print(json.dumps(json.load(open(sys.argv[1]))))" "$f"; done > "$BIN/gate.jsonl"
      dotnet "$BIN/RegattaGenomeHarness.dll" check "$BIN/gate.jsonl"
    fi ;;
  *) echo "usage: run.sh measure | lay G.json O.json | repair IN OUT | check GENOMES | fixture | gate" >&2; exit 2 ;;
esac

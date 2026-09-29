#!/usr/bin/env bash
# Compile the SHIPPED SpawnableGarland.cs against a UnityEngine + project shim and RUN it,
# writing Tools/Build/garland_measurements.json.
#
# Why run it rather than trust the offline model: author_garland_cell.py derives this cell's
# PhaseThresholds from its own transliteration of the generator, and two hand-written
# implementations that agree today are two that can drift tomorrow. This is what turns
# "I believe they agree" into a measurement, and --check is what stops the ladder shipping on
# a stale one.
#
# Needs a dotnet 8 SDK; a per-user install is fine and needs no root:
#   bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 --install-dir $HOME/.dotnet --no-path
#
# No .csproj on purpose - the repo gitignores *.csproj (Unity generates its own), so one here
# would be untracked and the harness would not survive a clone. Everything builds into $TMPDIR.
#
#   bash Tools/Build/garland_harness/run.sh            # measure and write the JSON
#   bash Tools/Build/garland_harness/run.sh --check    # fail if the committed JSON is stale
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/garland_harness"
SRC="$ROOT/Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableGarland.cs"
JSON="$ROOT/Tools/Build/garland_measurements.json"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '"%s"\n' "$HERE/UnityShim.cs" "$HERE/Program.cs" "$SRC" > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108 \
  -target:exe -main:Program -out:"$OUT/garland.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/garland.runtimeconfig.json"
"$DOTNET" "$OUT/garland.exe" > "$OUT/measured.json"

# Hash every source that can move a number, so a stale measurement fails loudly.
python3 - "$ROOT" "$OUT/measured.json" "$JSON" "${1:-}" <<'PY'
import hashlib, json, os, sys
root, measured_path, json_path, mode = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
sources = [
    "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableGarland.cs",
    "Tools/Build/garland_harness/UnityShim.cs",
    "Tools/Build/garland_harness/Program.cs",
]
doc = json.load(open(measured_path))
doc["sources"] = {s: hashlib.sha256(open(os.path.join(root, s), "rb").read()).hexdigest()
                  for s in sources}
text = json.dumps(doc, indent=2, sort_keys=True) + "\n"
if mode == "--check":
    if not os.path.exists(json_path):
        print("FAILED: no committed measurement."); sys.exit(1)
    if open(json_path).read() != text:
        print("FAILED: Tools/Build/garland_measurements.json is stale - re-run without --check.")
        sys.exit(1)
    print("--check OK: the committed measurement matches the shipped generator.")
else:
    open(json_path, "w").write(text)
    print(f"wrote {os.path.relpath(json_path, root)} - {doc['count']} prisms, "
          f"{doc['volume']:,.0f} volume.")
PY

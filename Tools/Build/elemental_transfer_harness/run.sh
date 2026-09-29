#!/usr/bin/env bash
# Compile the SHIPPED elemental-transfer files against a stub Unity + project surface and RUN the
# invariant driver. This is the only pre-editor proof available for this change: the transfer's
# arithmetic decides whether the economy conserves, and "it looked right" is not a measurement.
#
# What it proves: the files type-check (member names, arities, usings), and the take settles in
# whole petals, clamps to what a victim holds, is path-independent across cheap/dear hits, and
# survives float32 sums of authored magnitudes. Negative-controlled (T7).
#
# What it does NOT prove: anything that needs a MonoBehaviour base to bind - the four effect SOs
# and ResourceSystem itself compile only in the editor (Roslyn abandons class-body binding on an
# unresolved base type), so their call sites are checked by the grep audit in
# check_elemental_transfer.py instead.
#
# Needs a dotnet 8 SDK; a per-user install is fine:
#   bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 \
#        --install-dir $HOME/.dotnet --no-path
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"

# The real enums compile in rather than being stubbed: they live in the extracted CosmicShore.Data
# assembly and depend on no Unity type, so the harness runs the game's own values instead of a
# transcription that could drift.
SRCS=("$ROOT/Assets/_Scripts/Data/Enums/Element.cs" \
      "$ROOT/Assets/_Scripts/Data/Enums/Domains.cs" \
      "$ROOT/Assets/_Scripts/Data/Enums/CombatHitClass.cs" \
      "$ROOT/Assets/_Scripts/Data/Enums/ElementalDebuffSources.cs" \
      "$ROOT/Assets/_Scripts/Data/Enums/ElementalTransferForm.cs" \
      "$ROOT/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ElementalTransfer.cs" \
      "$ROOT/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/CombatHitDrain.cs" \
      "$ROOT/Assets/_Scripts/Controller/Environment/FlowField/ElementalCrystalEjector.cs" \
      "$ROOT/Assets/_Scripts/Controller/Environment/FlowField/EjectedCrystal.cs")

DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/elemental_transfer_harness"
mkdir -p "$OUT"

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$HERE/Stubs.cs" "$HERE/ProjectStubs.cs" "$HERE/Driver.cs" "${SRCS[@]}" \
  | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"

# csc writes diagnostics to STDOUT and this script's stdout is a DATA channel, so they go to
# stderr, where they stay visible and still fail the build through set -e.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS1574,CS1573,CS8632,CS0169,CS0649,CS0414,CS0067 \
  -target:exe -main:Driver -out:"$OUT/xfer.exe" "@$OUT/files.rsp" 1>&2

V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
  > "$OUT/xfer.runtimeconfig.json"

"$DOTNET" "$OUT/xfer.exe" "$@"

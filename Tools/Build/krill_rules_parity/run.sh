#!/usr/bin/env bash
# The Krill's rules gate: compile the SHIPPED KrillRules.cs, KrillCardDeckSO.cs and KrillConfigSO.cs with their SHIPPED
# edit-mode test (Assets/_Scripts/Tests/Editor/KrillRulesTests.cs, whose golden table is generated from cards.json)
# against a small UnityEngine stub and the shared NUnit shim (Tools/Build/brittlestar_harness), and RUN every [Test].
# Exit 0 = the C# answers every case the card list and the Krill Lab answer.
#
#   bash Tools/Build/krill_rules_parity/run.sh               # the shipped sources
#   bash Tools/Build/krill_rules_parity/run.sh --self-test   # + planted rule changes that must each FAIL
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else /usr/lib/dotnet, else ~/.dotnet.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
if [ -z "${DOTNET_ROOT:-}" ]; then
  if [ -x /usr/lib/dotnet/dotnet ]; then DOTNET_ROOT=/usr/lib/dotnet; else DOTNET_ROOT="$HOME/.dotnet"; fi
fi
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | head -1)
S="$ROOT/Assets/_Scripts"
RULES="$S/Controller/Vessel/KrillRules.cs"
DECK="$S/Controller/Vessel/R_VesselActions/Data Containers/KrillCardDeckSO.cs"
CONFIG="$S/Controller/Vessel/R_VesselActions/Data Containers/KrillConfigSO.cs"
ELEMENT="$S/Data/Enums/Element.cs"
TEST="$S/Tests/Editor/KrillRulesTests.cs"
OUT="${TMPDIR:-/tmp}/krill_rules_parity"
mkdir -p "$OUT"

build_and_run() {   # $1 = rules source, $2 = deck source, $3 = expected (pass|fail), $4 = label
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/../brittlestar_harness/NUnitShim.cs" "$HERE/../angler_command_parity/Runner.cs" \
    "$1" "$2" "$CONFIG" "$ELEMENT" "$TEST" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
    -target:exe -main:Runner -out:"$OUT/krp.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  TFM=$(basename "$REFDIR")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/krp.runtimeconfig.json"
  set +e; "$DOTNET" "$OUT/krp.exe" > "$OUT/result.txt"; local rc=$?; set -e
  if [ "$3" = pass ]; then cat "$OUT/result.txt"; return $rc; fi
  if [ $rc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $4"; return 1; fi
  echo "negative control fired: $4 ($(grep -c '^FAIL' "$OUT/result.txt") failing test(s))"
}

plant() {   # $1 = label, $2 = rules|deck, $3 = sed expression applied to a copy of that shipped source
  local src="$RULES"; [ "$2" = deck ] && src="$DECK"
  local copy="$OUT/planted.cs"
  sed "$3" "$src" > "$copy"
  if cmp -s "$copy" "$src"; then echo "PLANT DID NOT APPLY: $1"; return 1; fi
  if [ "$2" = deck ]; then build_and_run "$RULES" "$copy" fail "$1"; else build_and_run "$copy" "$DECK" fail "$1"; fi
}

build_and_run "$RULES" "$DECK" pass "shipped"
if [ "${1:-}" = "--self-test" ]; then
  plant "a count rounds to nearest, not down" deck 's/(float)Math.Floor(v + 1e-9)/(float)Math.Round(v)/'
  plant "geometric grows from level 0" deck 's/int n = Mathf.Max(0, level - 1);/int n = Mathf.Max(0, level);/'
  plant "the stack cap 15 -> 16" rules 's/public const int MaxLevel = 15;/public const int MaxLevel = 16;/'
  plant "the omni phase deals no hand" rules 's/if (ended == KrillPhase.Omnicrystal) DealHand();//'
  plant "Big Family's cap baseline 40 -> 46" rules 's/BigFamilyCapBase = 40f/BigFamilyCapBase = 46f/'
  plant "Hurry Up ignored" rules 's/return _config.PhaseTimer(phase) \* (1f - cut);/return _config.PhaseTimer(phase);/'
  plant "the vessel phase does not end on a debuff" rules 's/(Phase == KrillPhase.OpposingVessels \&\& DebuffedThisPhase)/false/'
  plant "the hand in the wrong order" rules 's/{ Element.Charge, Element.Mass, Element.Space, Element.Time }/{ Element.Mass, Element.Charge, Element.Space, Element.Time }/'
  echo "krill_rules_parity --self-test: every planted change failed the test"
fi

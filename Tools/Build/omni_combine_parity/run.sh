#!/usr/bin/env bash
# Proves OmniCombineCore (the game's port of the Omni Combine Lab) plays the lab's combine: same seed,
# same params, same camera -> every vertex of every crystal and of the omni within 2e-4.
#   bash Tools/Build/omni_combine_parity/run.sh
# Needs node + playwright (Chromium) and a .NET 8 SDK under DOTNET_ROOT (default ~/.dotnet).
# The SHIPPED Assets/_Scripts/Utility/OmniCombineCore.cs is compiled in place (never a copy).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
WORK="${TMPDIR:-/tmp}/omni_combine_parity"
mkdir -p "$WORK"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
python3 "$ROOT/Tools/Build/author_omni_combine_asset.py" --frame lab --out "$WORK/data_lab.json"
NODE_PATH="${NODE_PATH:-$(npm root -g)}" node "$HERE/lab_poses.cjs" "$ROOT/Docs/Studios/OmniCombineLab.html" "$WORK/lab_poses.json"
cat > "$WORK/OmniCombineParity.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$ROOT/Assets/_Scripts/Utility/OmniCombineCore.cs" />
    <Compile Include="$HERE/Driver.cs" />
  </ItemGroup>
</Project>
XML
rm -rf "$WORK/out" "$WORK/obj" "$WORK/bin"
dotnet build "$WORK/OmniCombineParity.csproj" -o "$WORK/out" -nologo -v q
dotnet "$WORK/out/OmniCombineParity.dll" "$WORK/data_lab.json" "$WORK/lab_poses.json" "$ROOT/Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json"

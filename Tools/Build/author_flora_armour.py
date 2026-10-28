#!/usr/bin/env python3
"""
Author THE ARMOUR LAW onto every flora asset (Docs/ECOSYSTEM.md §65): a MASS plant's leaves are armoured, a CHARGE
plant's never are.

    python3 Tools/Build/author_flora_armour.py            # write + print the table
    python3 Tools/Build/author_flora_armour.py --check    # verify, exit 1 on drift

WHAT THIS IS
------------
Mass is the element whose mass is SHIELDED (until 2026-10-28 it was Charge; Garrett moved the shield to Mass with the
"chunky" Mass plate, and gave Charge danger instead: every Charge prism is a danger prism). A shielded prism sheds its
shield instead of being eaten (Prism.Consume), so a herbivore has to strip a Mass plant before it can graze it, and the
plant re-armours one leaf every FloraVariantTuning.ShieldPeriod seconds. A shield CLEARS danger, so a Charge asset must
not author a cadence at all.

IT IS NOT THE ENFORCEMENT
-------------------------
ElementalArmour.ResolveShieldPeriod (through Flora.ResolveShieldPeriod) is - and it has to be, because a config with an
EMPTY element palette (FloraConfigurationSO.SpreadElements with no siblings) rolls an element and then applies its OWN
variant block to it, so the two Hesperides topiary configs would hand a Mass plant a cadence of 0 and no asset authoring
here could reach them. The law floors Mass at ElementalArmour.ShieldPeriod and zeroes Charge; this file is the data
saying the same thing, which is what a reader checks first. --check keeps the two from drifting apart.

An authored Mass cadence still WINS over the floor (armoured faster or slower than the fleet, never unarmoured): only
Mass assets sitting at "off" (0 or the -1 keep-the-prefab sentinel, where every prefab ships 0) are rewritten. A Charge
asset that authors a positive cadence is set to 0.
"""

from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"
VARIANT_CS = ROOT / "Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs"
ARMOUR_CS = ROOT / "Assets/_Scripts/Utility/DataContainers/ElementalArmour.cs"

CHARGE, MASS = 1, 2   # CosmicShore.Data.Element.Charge / .Mass


def armour_period():
    """ElementalArmour.ShieldPeriod - read from the C# so the data cannot drift from the law."""
    src = ARMOUR_CS.read_text(encoding="utf-8-sig")
    return float(re.search(r"ShieldPeriod\s*=\s*([\d.]+)f", src).group(1))


def variant_fields():
    """Every serialized field of FloraVariantTuning with its type, read from the C#.

    Mirrors the helper in fit_schwarz_p_leaf_sizes.py / fit_shield_clearance.py -
    deliberately a local copy rather than an import, so authoring an asset never depends on
    those tools' numpy."""
    src = VARIANT_CS.read_text(encoding="utf-8-sig")
    body = re.search(r"public class FloraVariantTuning\s*\{(.*?)\n    \}", src, re.S).group(1)
    body = re.sub(r"^(?:\s*\[[^\]]*\]\s*)+", "", body, flags=re.M)
    out = []
    for line in body.splitlines():
        m = re.match(r"\s*public ([\w<>]+) (\w+)\s*=\s*([^;]+);", line)
        if m:
            out.append((m.group(2), m.group(1)))
    return out


def variant_block(text):
    """(the block's raw lines, the parsed key->value map) for an asset's Variant block."""
    m = re.search(r"  Variant:\n((?:    [^\n]*\n)*)", text)
    if not m:
        return None, {}
    return m.group(0), dict(re.findall(r"^    (\w+): (.+)$", m.group(1), re.M))


def disarm(path, check):
    """A Charge asset authors no cadence: a positive ShieldPeriod becomes 0 (the law would zero it anyway)."""
    text = path.read_text()
    raw, fields = variant_block(text)
    if raw is None:
        return "no Variant block", False
    if float(fields.get("ShieldPeriod", -1)) <= 0:
        return f"ok (ShieldPeriod {fields.get('ShieldPeriod', 'unset')}, every prism danger)", False
    block = re.sub(r"^    ShieldPeriod: .+$", "    ShieldPeriod: 0", raw, count=1, flags=re.M)
    if not check:
        path.write_text(text.replace(raw, block, 1))
    return f"ShieldPeriod {fields['ShieldPeriod']} -> 0 (every prism danger)", True


def rewrite(path, period, check):
    """Ensure this asset's Variant block is enabled and armours at `period`.

    Two shapes to handle, and the first is why this cannot be a one-line sed: five of the
    six sit at a bare `Variant:\\n    Enabled: 0`, which the spawner SKIPS entirely
    (CellLifeSpawnerBase: `if (pick.Tuning is { Enabled: true })`). Enabling it means
    emitting the whole field set, and the sentinels are load-bearing and are NOT zero -
    a written `MaxTotalSpawnedObjects: 0` is a live-prism budget of zero, not "keep"."""
    text = path.read_text()
    raw, fields = variant_block(text)
    if raw is None:
        return "no Variant block", False

    already = fields.get("Enabled") == "1" and float(fields.get("ShieldPeriod", -1)) > 0
    if already:
        return f"ok (ShieldPeriod {fields['ShieldPeriod']})", False

    lines = ["  Variant:", "    Enabled: 1"]
    for name, kind in variant_fields():
        if name == "Enabled":
            continue
        if name == "ShieldPeriod":
            lines.append(f"    ShieldPeriod: {period:g}")
        elif name in fields:
            # PRESERVE every field this script does not own - author_flora_populations.py
            # writes the per-plant budget into this same block, and a blanket sentinel here
            # would silently revert it depending on which script ran last.
            lines.append(f"    {name}: {fields[name]}")
        elif kind == "Vector3":
            lines.append(f"    {name}: {{x: 0, y: 0, z: 0}}")
        else:
            lines.append(f"    {name}: -1")
    block = "\n".join(lines) + "\n"

    written = set(re.findall(r"^    (\w+):", block, re.M))
    declared = {n for n, _ in variant_fields()}
    if written != declared:
        raise SystemExit(f"{path.name}: variant key mismatch "
                         f"missing={declared - written} unknown={written - declared}")

    was = fields.get("ShieldPeriod", "unset")
    if not check:
        path.write_text(text.replace(raw, block, 1))
    return f"ShieldPeriod {was} -> {period:g}", True


def main():
    check = "--check" in sys.argv
    period = armour_period()

    print(f"Flora armour (ElementalArmour.ShieldPeriod = {period:g}s on Mass; Charge never shields)")
    print("-" * 70)

    drift = []
    for path in sorted(LIFEFORMS.glob("*Flora *.asset")):
        text = path.read_text()
        element = re.search(r"^  Element: (\d+)$", text, re.M)
        if not element or int(element.group(1)) not in (CHARGE, MASS):
            continue
        if int(element.group(1)) == MASS:
            note, changed = rewrite(path, period, check)
        else:
            note, changed = disarm(path, check)
        print(f"  {path.stem:<34} {note}")
        if changed:
            drift.append(path.stem)

    if check and drift:
        print(f"\nFAILED: {len(drift)} flora assets do not state the armour law: " + ", ".join(drift))
        return 1
    if check:
        print("\nevery Mass flora asset states the armour and no Charge one does")
    elif drift:
        print(f"\nwrote {len(drift)} asset(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

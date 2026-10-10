#!/usr/bin/env python3
"""Offline twin of FrogletTools > Validation > Validate Lifeform Crystals.

The editor validator (Assets/_Scripts/Editor/LifeFormCrystalValidator.cs) checks two halves of
the lifeform invariant and prints its findings to the console, where a session cannot read
them. QA's 2026-10-09 run reported "34 warnings" and nothing else (DT-002). This script runs the
same two checks from the YAML, so the 34 can be named and the next run compared line by line.

  1. Every flora / fauna PREFAB carries exactly one elemental crystal (Charge / Mass / Space /
     Time): a prefab whose component set (own components plus every nested prefab instance's,
     minus the ones an instance removed) contains a LifeForm subclass or LightFauna must contain
     exactly one Crystal whose crystalProperties.Element is elemental.
  2. Every lifeform CONFIG (FaunaConfigurationSO / FloraConfigurationSO) that names a prefab and
     does not draw its identity from a non-empty element palette states its own heart size:
     Variant.Enabled with HeartWorldScale > 0.

The prefab walk is the one Tools/Build/audit_vessel_skimmers.py uses (nested instances by the
fileID XOR rule, overrides outermost-first, m_RemovedComponents honoured), so a crystal nested
three prefabs deep with an element override on the outer instance reads as the editor reads it.

    python3 Tools/Build/audit_lifeform_crystals.py            # report
    python3 Tools/Build/audit_lifeform_crystals.py --check    # exit 1 on any finding

A READER tool: it never writes an asset.
"""
from __future__ import annotations

import argparse
import importlib.util
import os
import re
import sys
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("avs", Path(__file__).with_name("audit_vessel_skimmers.py"))
avs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(avs)

ELEMENTAL = {"1", "2", "3", "4"}   # Charge, Mass, Space, Time (Element.cs)


def script_guids(index: avs.AssetIndex, names: list[str]) -> dict[str, str]:
    out = {}
    for g, p in index.by_guid.items():
        if p.suffix == ".cs" and p.stem in names:
            out[g] = p.stem
    return out


def subclasses(base: str) -> set[str]:
    """Transitive subclasses of `base` by declaration, over Assets/_Scripts."""
    decl: dict[str, list[str]] = {}
    for dp, _, fns in os.walk(ROOT / "Assets/_Scripts"):
        for fn in fns:
            if not fn.endswith(".cs"):
                continue
            try:
                t = (Path(dp) / fn).read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            for m in re.finditer(r"class\s+(\w+)\s*(?:<[^>]*>)?\s*:\s*([\w\.]+)", t):
                decl.setdefault(m.group(1), []).append(m.group(2).split(".")[-1])
    out = {base}
    changed = True
    while changed:
        changed = False
        for cls, bases in decl.items():
            if cls not in out and any(b in out for b in bases):
                out.add(cls)
                changed = True
    return out


@lru_cache(maxsize=None)
def all_components(index: avs.AssetIndex, guid: str) -> tuple:
    """Every component Obj of the prefab with this guid, own and nested, removals honoured."""
    prefab = index.prefab(guid)
    if prefab is None:
        return ()
    out: list[avs.Obj] = []
    for fid, (cls, _body, stripped) in prefab.docs.items():
        if cls == avs.MONO and not stripped:
            out.append(avs.Obj([(prefab, fid, 0)]))
    for inst, (src_guid, _mods) in prefab.instances.items():
        for c in all_components(index, src_guid):
            top = (c.levels[0][1] ^ inst) & avs.MASK
            o = avs.Obj([(prefab, top, inst)] + list(c.levels))
            if not avs.removed_at_some_level(o):
                out.append(o)
    return tuple(out)


def crystal_element(obj: avs.Obj) -> str | None:
    """crystalProperties.Element as the editor would read it: an override wins, else the block."""
    v, _ = obj.value("crystalProperties.Element")
    if v is not None and v != "":
        return v.strip()
    p, f = obj.base
    body = p.body(f)
    m = re.search(r"^  crystalProperties:\n((?:    .*\n)*)", body, re.M)
    if not m:
        return None
    e = re.search(r"^    Element: (\S+)", m.group(1), re.M)
    return e.group(1) if e else None


def check_prefabs(index: avs.AssetIndex) -> tuple[int, int, list[str]]:
    lifeform_names = sorted(subclasses("LifeForm") | {"LightFauna"})
    lifeform_guids = script_guids(index, lifeform_names)
    crystal_guid = next(g for g, p in index.by_guid.items() if p.suffix == ".cs" and p.stem == "Crystal")
    lines: list[str] = []
    checked = issues = 0
    for guid, path in sorted(index.by_guid.items(), key=lambda kv: str(kv[1])):
        if path.suffix != ".prefab":
            continue
        comps = all_components(index, guid)
        is_lifeform = any(avs.guid_of(c.value("m_Script")[0]) in lifeform_guids for c in comps)
        if not is_lifeform:
            continue
        checked += 1
        rel = path.relative_to(ROOT)
        crystals = [c for c in comps if avs.guid_of(c.value("m_Script")[0]) == crystal_guid]
        if not crystals:
            lines.append(f"  {rel}: lifeform has NO crystal - must carry one elemental crystal to drop on death")
            issues += 1
            continue
        el = crystal_element(crystals[0])
        if el not in ELEMENTAL:
            lines.append(f"  {rel}: crystal element is '{el}' - must be one of Charge/Mass/Space/Time")
            issues += 1
        if len(crystals) > 1:
            lines.append(f"  {rel}: lifeform has {len(crystals)} crystals - a lifeform should carry exactly one")
            issues += 1
    return checked, issues, lines


def check_configs(index: avs.AssetIndex) -> tuple[int, int, list[str]]:
    kinds = {"FaunaConfigurationSO": ("FaunaPrefab", "creature"), "FloraConfigurationSO": ("FloraPrefab", "plant")}
    guids = {g: n for g, n in script_guids(index, list(kinds)).items()}
    lines: list[str] = []
    checked = issues = 0
    for guid, path in sorted(index.by_guid.items(), key=lambda kv: str(kv[1])):
        if path.suffix != ".asset":
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        kind = guids.get(avs.guid_of(avs.field(text, "m_Script")))
        if kind is None:
            continue
        prefab_key, noun = kinds[kind]
        if not avs.fileid(avs.field(text, prefab_key)):
            continue
        spread = (avs.field(text, "SpreadElements") or "0").strip() == "1"
        if spread:
            m = re.search(r"^  ElementPalette:\n((?:  - .*\n)*)", text, re.M)
            palette = re.findall(r"guid: ([0-9a-f]{32})", m.group(1)) if m else []
            has_element = False
            for pg in palette:
                pp = index.by_guid.get(pg)
                if pp and pp.exists():
                    pt = pp.read_text(encoding="utf-8", errors="replace")
                    if (avs.field(pt, "Element") or "0").strip() != "0":
                        has_element = True
            if has_element:
                continue
        checked += 1
        rel = path.relative_to(ROOT)
        vm = re.search(r"^  Variant:\n((?:    .*\n)*)", text, re.M)
        vblock = vm.group(1) if vm else ""
        enabled = re.search(r"^    Enabled: (\S+)", vblock, re.M)
        scale = re.search(r"^    HeartWorldScale: (\S+)", vblock, re.M)
        enabled_v = enabled.group(1).strip() == "1" if enabled else False
        scale_v = float(scale.group(1)) if scale else 0.0
        if not enabled_v:
            tail = (f"authors HeartWorldScale {scale_v} inside a DISABLED Variant block" if scale_v > 0
                    else "has no Variant tuning enabled")
            lines.append(f"  {rel}: {tail}, so this {noun}'s heart falls back to the platform default")
            issues += 1
        elif scale_v <= 0:
            lines.append(f"  {rel}: Variant authors no HeartWorldScale, so this {noun}'s heart falls back to the platform default")
            issues += 1
    return checked, issues, lines


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    index = avs.AssetIndex(ROOT)
    c1, i1, l1 = check_prefabs(index)
    c2, i2, l2 = check_configs(index)
    print("Lifeform crystal audit (offline)")
    print(f"1. prefabs: {c1} lifeform prefab(s), {i1} issue(s)")
    print("\n".join(l1))
    print(f"2. configs: {c2} lifeform config(s) checked, {i2} will render the platform default heart")
    print("\n".join(l2))
    total = i1 + i2
    print(f"{total} warning(s) (the editor validator prints one LogWarning per line above)")
    return 1 if (args.check and total) else 0


if __name__ == "__main__":
    sys.exit(main())

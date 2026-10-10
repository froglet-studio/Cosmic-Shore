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
        rel = path.relative_to(index.root)
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
        rel = path.relative_to(index.root)
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


def self_test() -> int:
    """Negative controls on a synthetic project: a lifeform prefab with no crystal, a config
    with its Variant block removed, and a config that passes. The script metas are copied so the
    guids resolve by file name exactly as they do on the live tree."""
    import shutil
    import tempfile
    ok = True
    live = avs.AssetIndex(ROOT)
    with tempfile.TemporaryDirectory() as td:
        root = Path(td)
        def put(rel: str) -> Path:
            dst = root / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy(ROOT / rel, dst)
            return dst
        # Scripts the walk resolves by name: every lifeform class, the crystal, the configs.
        names = subclasses("LifeForm") | {"LightFauna", "Crystal", "FaunaConfigurationSO", "FloraConfigurationSO"}
        for g, path in live.by_guid.items():
            if path.suffix == ".cs" and path.stem in names:
                put(str(path.relative_to(ROOT)) + ".meta")
        # A passing lifeform prefab: the first one on the live tree the prefab check passes.
        lifeform_guids = script_guids(live, sorted(names - {"Crystal", "FaunaConfigurationSO", "FloraConfigurationSO"}))
        crystal_guid = next(g for g, p in live.by_guid.items() if p.suffix == ".cs" and p.stem == "Crystal")
        shark = None
        for g, path in sorted(live.by_guid.items(), key=lambda kv: str(kv[1])):
            if path.suffix != ".prefab" or "Qfish" in path.name:
                continue
            comps = all_components(live, g)
            if not any(avs.guid_of(c.value("m_Script")[0]) in lifeform_guids for c in comps):
                continue
            crystals = [c for c in comps if avs.guid_of(c.value("m_Script")[0]) == crystal_guid]
            if len(crystals) == 1 and crystal_element(crystals[0]) in ELEMENTAL:
                shark = str(path.relative_to(ROOT))
                break
        put(shark); put(shark + ".meta")
        # The prefab's nested crystal source must come along, or the walk cannot see the crystal.
        import re
        pending = [ROOT / shark]
        seen = set()
        while pending:
            cur = pending.pop()
            for g in set(re.findall(r"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32})", cur.read_text(encoding="utf-8", errors="replace"))):
                src = live.by_guid.get(g)
                if src and g not in seen:
                    seen.add(g)
                    put(str(src.relative_to(ROOT))); put(str(src.relative_to(ROOT)) + ".meta")
                    pending.append(src)
        all_components.cache_clear()
        cfg = "Assets/_SO_Assets/Threat Flora/Swarm Physarum Flora Space Config Data.asset"
        put(cfg); put(cfg + ".meta")
        # Its prefab reference must resolve to SOMETHING for the config to count; it is a guid
        # reference, not a file read, so the meta alone is enough.
        idx = avs.AssetIndex(root)
        c1, i1, _ = check_prefabs(idx)
        c2, i2, l2 = check_configs(idx)
        ok &= _expect(f"a complete lifeform prefab passes ({shark.rsplit('/', 1)[-1]})", c1 >= 1 and i1 == 0)
        ok &= _expect("a config with an enabled Variant and a HeartWorldScale passes", c2 == 1 and i2 == 0)

        # Negative control 1: strip the Variant block from the config.
        text = (root / cfg).read_text(encoding="utf-8")
        stripped = re.sub(r"^  Variant:\n(?:    .*\n)*", "", text, flags=re.M)
        (root / cfg).write_text(stripped, encoding="utf-8")
        _c, i2b, l2b = check_configs(avs.AssetIndex(root))
        ok &= _expect("a config with no Variant block is a finding", i2b == 1 and any("no Variant tuning" in l for l in l2b))

        # Negative control 2: a lifeform prefab with every Crystal component removed by hand.
        ptext = (root / shark).read_text(encoding="utf-8", errors="replace")
        crystal_guid = next(g for g, p in live.by_guid.items() if p.suffix == ".cs" and p.stem == "Crystal")
        without = re.sub(r"--- !u!114 &-?\d+( stripped)?\nMonoBehaviour:\n(?:(?!--- !u!).*\n)*?  m_Script: \{fileID: 11500000, guid: " + crystal_guid + r", type: 3\}\n(?:(?!--- !u!).*\n)*", "", ptext)
        (root / shark).write_text(without, encoding="utf-8")
        # Also drop the nested crystal source prefabs so no nested crystal survives.
        for g in set(re.findall(r"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32})", ptext)):
            src = live.by_guid.get(g)
            if src and "rystal" in src.name:
                (root / src.relative_to(ROOT)).unlink(missing_ok=True)
                (root / (str(src.relative_to(ROOT)) + ".meta")).unlink(missing_ok=True)
        all_components.cache_clear()
        _c, i1b, l1b = check_prefabs(avs.AssetIndex(root))
        ok &= _expect("a lifeform prefab with no crystal is a finding", i1b >= 1 and any("NO crystal" in l for l in l1b))
    print("self-test:", "OK" if ok else "FAILED")
    return 0 if ok else 1


def _expect(label: str, cond: bool) -> bool:
    print(f"  {'ok  ' if cond else 'FAIL'} {label}")
    return cond


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
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

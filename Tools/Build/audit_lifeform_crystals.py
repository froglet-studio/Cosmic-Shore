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


# The self-test gates both static CI jobs, so its fixtures are SYNTHETIC: a Unity-shaped crystal
# prefab, a lifeform prefab that nests it, and a config, written into a temp project with their
# own script guids. An earlier version copied a named live config and the first passing live
# prefab, so renaming that config or adding a lifeform that sorted first could turn CI red.

SYN_SCRIPTS = {"LightFauna": avs._g(0x1F1), "Crystal": avs._g(0x1F2),
               "FaunaConfigurationSO": avs._g(0x1F3), "FloraConfigurationSO": avs._g(0x1F4)}
SYN_CRYSTAL, SYN_CREATURE, SYN_CONFIG = avs._g(0x2E1), avs._g(0x2E2), avs._g(0x2E3)
CR_GO, CR_T, CR_CRYSTAL = 10, 11, 12          # the crystal prefab
LF_GO, LF_T, LF_FAUNA = 1, 2, 3               # the lifeform prefab
CRYSTAL_INSTANCES = (900, 901)


def _mono(fid: int, go: int, script: str, fields: list[str]) -> str:
    return avs._doc(avs.MONO, fid, "MonoBehaviour", [
        f"  m_GameObject: {{fileID: {go}}}", "  m_Enabled: 1",
        f"  m_Script: {{fileID: 11500000, guid: {SYN_SCRIPTS[script]}, type: 3}}",
        "  m_Name: ", "  m_EditorClassIdentifier: "] + fields)


def _synthetic_project(root: Path, crystals: int = 1, element_override: str | None = "3",
                       removed: bool = False, variant: str | None = "    Enabled: 1\n    HeartWorldScale: 2.5\n"
                       ) -> tuple[str, str]:
    """Write the project; return (prefab rel path, config rel path). By default it passes: the
    crystal prefab's own Element is 0 (NOT elemental) and the lifeform's instance overrides it to
    3 (Space), so a pass proves the override is read through the nested instance."""
    w = avs._write
    for name, guid in SYN_SCRIPTS.items():
        w(root, f"Assets/Scripts/{name}.cs", "", guid)
    w(root, "Assets/Crystals/CrystalMass.prefab", avs.YAML_HEAD
      + avs._go(CR_GO, "CrystalMass", [CR_T, CR_CRYSTAL]) + avs._transform(CR_T, CR_GO, 0)
      + _mono(CR_CRYSTAL, CR_GO, "Crystal", ["  crystalProperties:", "    fuelAmount: 0.01",
                                             "    Element: 0", "    crystalValue: 0"]), SYN_CRYSTAL)
    mods = [(CR_CRYSTAL, "crystalProperties.Element", element_override, "{fileID: 0}")] \
        if element_override is not None else []
    text = (avs.YAML_HEAD + avs._go(LF_GO, "Creature", [LF_T, LF_FAUNA]) + avs._transform(LF_T, LF_GO, 0)
            + _mono(LF_FAUNA, LF_GO, "LightFauna", []))
    for inst in CRYSTAL_INSTANCES[:crystals]:
        text += avs._instance(inst, LF_T, SYN_CRYSTAL, mods, [CR_CRYSTAL] if removed else [])
    prefab = "Assets/Lifeforms/Creature.prefab"
    w(root, prefab, text, SYN_CREATURE)
    config = "Assets/Configs/Creature Fauna Config Data.asset"
    w(root, config, avs.YAML_HEAD + avs._doc(avs.MONO, 11400000, "MonoBehaviour", [
        "  m_GameObject: {fileID: 0}", "  m_Enabled: 1",
        f"  m_Script: {{fileID: 11500000, guid: {SYN_SCRIPTS['FaunaConfigurationSO']}, type: 3}}",
        "  m_Name: Creature Fauna Config Data", "  m_EditorClassIdentifier: ",
        f"  FaunaPrefab: {{fileID: {LF_GO}, guid: {SYN_CREATURE}, type: 3}}",
        "  SpreadElements: 0", "  Element: 3"]) + (f"  Variant:\n{variant}" if variant is not None else ""),
      SYN_CONFIG)
    return prefab, config


def self_test() -> int:
    """Negative controls on a synthetic project: a lifeform prefab whose crystal comes through a
    nested instance with an element override passes, and each half of the invariant is broken
    once (no crystal, a removed crystal, a non-elemental crystal, two crystals; no Variant block,
    a disabled one, one with no HeartWorldScale)."""
    import tempfile
    ok = True

    def run(**kw) -> tuple[tuple[int, int, list[str]], tuple[int, int, list[str]]]:
        all_components.cache_clear()
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            _synthetic_project(root, **kw)
            idx = avs.AssetIndex(root)
            return check_prefabs(idx), check_configs(idx)

    (c1, i1, _l1), (c2, i2, _l2) = run()
    ok &= _expect("a lifeform prefab with one nested crystal, element overridden to Space, passes",
                  c1 == 1 and i1 == 0)
    ok &= _expect("a config with an enabled Variant and a HeartWorldScale passes", c2 == 1 and i2 == 0)

    (_c, i, lines), _ = run(crystals=0)
    ok &= _expect("a lifeform prefab with no crystal is a finding", i == 1 and any("NO crystal" in l for l in lines))
    (_c, i, lines), _ = run(removed=True)
    ok &= _expect("a crystal the nesting instance REMOVES is gone",
                  i == 1 and any("NO crystal" in l for l in lines))
    (_c, i, lines), _ = run(element_override=None)
    ok &= _expect("a crystal whose element is not elemental is a finding",
                  i == 1 and any("crystal element is '0'" in l for l in lines))
    (_c, i, lines), _ = run(crystals=2)
    ok &= _expect("a lifeform with two crystals is a finding", i == 1 and any("2 crystals" in l for l in lines))

    _, (_c, i, lines) = run(variant=None)
    ok &= _expect("a config with no Variant block is a finding",
                  i == 1 and any("no Variant tuning" in l for l in lines))
    _, (_c, i, lines) = run(variant="    Enabled: 0\n    HeartWorldScale: 2.5\n")
    ok &= _expect("a config whose Variant is disabled is a finding",
                  i == 1 and any("DISABLED Variant block" in l for l in lines))
    _, (_c, i, lines) = run(variant="    Enabled: 1\n    HeartWorldScale: 0\n")
    ok &= _expect("an enabled Variant with no HeartWorldScale is a finding",
                  i == 1 and any("authors no HeartWorldScale" in l for l in lines))
    all_components.cache_clear()
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

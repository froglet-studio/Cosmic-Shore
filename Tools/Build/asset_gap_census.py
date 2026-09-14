#!/usr/bin/env python3
"""Asset-gap census — a READER over the project's sound / VFX / art SLOTS.

The one question it answers: **which asset slots the game declares are not filled, and by whom
they are filled where they are?** A slot is a serialized field whose value is an asset another
discipline produces — an FMOD `EventReference`, a VFX prefab, a card sprite. The convention this
project runs on (CLAUDE.md "Audio (FMOD)") is that a slot ships EMPTY rather than plugged with a
temp asset, so the empty slot IS the to-do list. This script reads that list out of the tree.

Two things make a grep insufficient, and both are handled here:

  * A slot's value can live in THREE places — on the asset, as a scene / nested-prefab instance
    OVERRIDE (`m_Modifications`), or NOWHERE (a field added to the C# after the asset was last
    saved is not written into the YAML at all; the runtime value is then the field initializer,
    which for every slot kind is "empty"). `AudioSystem.prefab` carries NONE of its 50 event
    fields — every one is a Bootstrap.unity override — and `MantaStingConfig.asset` carries none
    of its six. A census that reads only the asset would report the first as 50 gaps and the
    second as zero.
  * A slot's REQUIREMENT can be conditional on a sibling (`FaunaConfigurationSO.AudioLoopEvent`
    only matters when `OverrideAudio` is on), and some gaps are the ABSENCE of a slot (an ability
    executor with no `EventReference` at all, against the "every ability gets its own field" rule).

Sound slots are discovered from the C# (every `EventReference` field). Visual / art slots are
declared in `Tools/Build/asset_slot_registry.json`, because a null `GameObject` field is usually
optional and only a human can say which ones are asset slots.

Usage:
    python3 Tools/Build/asset_gap_census.py                 # print the summary
    python3 Tools/Build/asset_gap_census.py --write         # write Docs/ASSET_GAPS/{census.json,GAP_CENSUS.md}
    python3 Tools/Build/asset_gap_census.py --check         # fail if a gap exists that the baseline does not know
    python3 Tools/Build/asset_gap_census.py --write-baseline
    python3 Tools/Build/asset_gap_census.py --self-test

Reader only: writes nothing under Assets/. Record: Docs/ASSET_GAPS/ARCHITECTURE.md.
"""
from __future__ import annotations

import argparse
import fnmatch
import json
import os
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field, asdict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")
ASSETS = os.path.join(ROOT, "Assets")
REGISTRY = os.path.join(ROOT, "Tools", "Build", "asset_slot_registry.json")
OUT_DIR = os.path.join(ROOT, "Docs", "ASSET_GAPS")
OUT_JSON = os.path.join(OUT_DIR, "census.json")
OUT_MD = os.path.join(OUT_DIR, "GAP_CENSUS.md")
BASELINE = os.path.join(OUT_DIR, "baseline.json")

ASSET_EXTS = (".prefab", ".unity", ".asset")
UNSUPPORTED: list[dict] = []
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)", re.M)
SCRIPT_RE = re.compile(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})")
GO_RE = re.compile(r"^  m_GameObject: \{fileID: (-?\d+)\}", re.M)
NAME_RE = re.compile(r"^  m_Name: ?(.*)$", re.M)
SOURCE_PREFAB_RE = re.compile(r"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32})")
MOD_RE = re.compile(
    r"- target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}),\s*type: \d\}\s*\n"
    r"\s+propertyPath: (\S+)\s*\n"
    r"\s+value: ?(.*)\n"
    r"\s+objectReference: \{fileID: (-?\d+)", re.M)
# A DECLARATION of an EventReference, of any shape. Which of these Unity actually serializes is
# decided afterwards by `serialized_event_fields`, because the shapes that are NOT slots all match
# a naive field pattern: a local variable (`EventReference reference = layers[i];`), a method
# parameter, and an expression-bodied property (`public EventReference Foo => foo;`) — the last of
# which reads exactly like a public field and duplicated all six Manta sting slots when it did.
EVENT_DECL_RE = re.compile(
    r"(?P<indent>[ \t]*)(?P<mods>(?:(?:public|private|protected|internal|static|readonly|const|new)\s+)*)"
    r"(?:FMODUnity\.)?EventReference(?P<array>\s*\[\s*\])?\s+(?P<name>\w+)\s*(?P<tail>[=;{)])", re.M)
CLASS_RE = re.compile(r"^(\s*)(?:public |internal |private |protected )?(?:static |abstract |sealed |partial )*(?:class|struct)\s+(\w+)", re.M)


@dataclass
class Slot:
    cls: str
    field: str
    kind: str                      # sound | vfx | art2d | video | model
    discipline: str
    label: str = ""
    required_when: dict = field(default_factory=dict)
    group: str = ""
    source: str = "code"           # code (auto-discovered) | registry
    script_guid: str = ""


@dataclass
class Row:
    id: str
    kind: str
    discipline: str
    cls: str
    field: str
    asset: str
    object_name: str
    status: str                    # wired | wired-by-override | empty | silent | nulled | not-required
    detail: str = ""
    group: str = ""
    label: str = ""

    @property
    def is_gap(self) -> bool:
        return self.status in ("empty", "silent", "nulled")


# ---------------------------------------------------------------- discovery

def meta_guid(path: str) -> str | None:
    try:
        with open(path + ".meta", encoding="utf-8", errors="ignore") as f:
            m = re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M)
            return m.group(1) if m else None
    except OSError:
        return None


def owning_class(text: str, pos: int) -> str | None:
    """The innermost class declared before `pos` whose indent is less than the field's."""
    line_start = text.rfind("\n", 0, pos) + 1
    field_indent = len(text[line_start:pos]) - len(text[line_start:pos].lstrip())
    best = None
    for m in CLASS_RE.finditer(text, 0, pos):
        if len(m.group(1)) < field_indent:
            best = m.group(2)
    return best


def mask_literals(text: str) -> str:
    """The text with every string literal and comment blanked, LENGTH PRESERVED.

    Needed because the delimiters that bound an attribute block are also ordinary characters
    inside a tooltip. `[SerializeField, Tooltip("... for creature kills; flora blocks ...")]`
    carries a semicolon, so a raw rfind cuts INSIDE the string, finds no [SerializeField] after
    it, and drops a real slot — which is what hid AudioSystem.creatureBlockHitEvent, a slot the
    audio owner's own task list names."""
    out, i, n = [], 0, len(text)
    while i < n:
        c = text[i]
        if c == '"' and text[i - 1:i] != "\\":
            j = i + 1
            while j < n and not (text[j] == '"' and text[j - 1] != "\\"):
                j += 1
            out.append(" " * (min(j, n - 1) - i + 1))
            i = j + 1
        elif c == "'" and text[i - 1:i] != "\\":
            j = i + 1
            while j < n and not (text[j] == "'" and text[j - 1] != "\\"):
                j += 1
            out.append(" " * (min(j, n - 1) - i + 1))
            i = j + 1
        elif text.startswith("//", i):
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            i = j
        else:
            out.append(c)
            i += 1
    masked = "".join(out)
    return masked if len(masked) == n else masked.ljust(n)[:n]


def serialized_event_fields(text: str):
    """Every EventReference field Unity will actually SERIALIZE, as (class, name, is_array).

    Unity's rule, applied literally: a field is serialized when it is `public`, or `private` /
    `protected` carrying [SerializeField]; never when it is `static`, `const`, `readonly`, a local,
    a parameter, or a property. Applying the rule is what separates a slot from the three shapes
    that merely look like one."""
    out = []
    for m in EVENT_DECL_RE.finditer(text):
        tail, mods = m.group("tail"), m.group("mods")
        if tail != ";":
            continue                                   # `=>` property, parameter, or initialiser-with-brace
        if m.group(0).rstrip().endswith("=>"):
            continue
        if re.search(r"\b(static|const|readonly)\b", mods):
            continue
        # The attribute block immediately above the declaration. Delimited by the previous
        # statement terminator, NOT by line shape: a [SerializeField, Tooltip("..." + "...")]
        # spans lines that neither start with "[" nor end with "]", and a line-shape walk stops at
        # the first of them — which silently dropped ShipAudioController's engine-layer slots and
        # reported them as no slot at all.
        head = text[:m.start()]
        cut = max(*(mask_literals(head).rfind(c) for c in ";{}"))
        attrs = head[cut + 1:]
        serialized = "public" in mods or "SerializeField" in attrs
        if not serialized:
            continue                                   # a bare `EventReference x;` local or unserialized private
        cls = owning_class(text, m.start("name"))
        out.append((cls, m.group("name"), bool(m.group("array"))))
    return out


def discover_sound_slots() -> list[Slot]:
    slots = []
    for dp, _, fns in os.walk(SCRIPTS):
        if os.sep + "Editor" in dp or os.sep + "Tests" in dp:
            continue
        for fn in fns:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dp, fn)
            with open(path, encoding="utf-8", errors="ignore") as f:
                text = f.read()
            if "EventReference" not in text:
                continue
            guid = meta_guid(path)
            for cls, name, is_array in serialized_event_fields(text):
                cls = cls or fn[:-3]
                if is_array:
                    # A LIST of slots in one field. The YAML shape is a sequence, which the
                    # single-slot reader cannot address, so it is declared unsupported rather than
                    # read as one slot and reported confidently wrong.
                    UNSUPPORTED.append({"cls": cls, "field": name, "file": rel(path),
                                        "why": "EventReference[] — a list of slots; the census reads single slots only"})
                    continue
                slots.append(Slot(cls=cls, field=name, kind="sound", discipline="Sound",
                                  label=f"{cls}.{name}", script_guid=guid or "",
                                  group=f"sound:{cls}"))
    return slots


def load_registry() -> dict:
    if not os.path.exists(REGISTRY):
        return {"slots": [], "no_slot_expectations": [], "exclude_paths": []}
    with open(REGISTRY, encoding="utf-8") as f:
        return json.load(f)


def registry_slots(reg: dict, guid_by_class: dict) -> list[Slot]:
    out = []
    for s in reg.get("slots", []):
        # A nested [Serializable] holder has no file of its own, so a row may name the FILE that
        # declares it ("script") separately from the class the field belongs to.
        guid = guid_by_class.get(s.get("script", s["class"]))
        if not guid:
            print(f"registry: no script named {s.get('script', s['class'])}.cs under Assets/_Scripts", file=sys.stderr)
            continue
        out.append(Slot(cls=s["class"], field=s["field"], kind=s["kind"], discipline=s["discipline"],
                        label=s.get("label", f"{s['class']}.{s['field']}"),
                        required_when=s.get("required_when", {}), group=s.get("group", f"{s['kind']}:{s['class']}"),
                        source="registry", script_guid=guid))
    return out


def class_guid_index() -> dict:
    idx = {}
    for dp, _, fns in os.walk(SCRIPTS):
        for fn in fns:
            if fn.endswith(".cs"):
                g = meta_guid(os.path.join(dp, fn))
                if g:
                    idx[fn[:-3]] = g
    return idx


# ---------------------------------------------------------------- asset parsing

@dataclass
class Doc:
    file: str
    file_guid: str
    cls_id: int
    doc_id: int
    text: str


def split_docs(path: str, file_guid: str) -> list[Doc]:
    with open(path, encoding="utf-8", errors="ignore") as f:
        text = f.read()
    marks = list(DOC_RE.finditer(text))
    docs = []
    for i, m in enumerate(marks):
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        docs.append(Doc(path, file_guid, int(m.group(1)), int(m.group(2)), text[m.start():end]))
    return docs


def event_ref_value(doc_text: str, fld: str):
    """(present, wired, detail, count) for an FMOD EventReference field at any indent.

    `count` is how many times the key occurs in the document. A repeated key means the field
    lives inside a LIST of serialized holders (several variants on one config), where reading
    the first match would answer confidently about one element and silently ignore the rest —
    so the caller reports that as ambiguous rather than as a status."""
    ms = list(re.finditer(r"^(\s*)" + re.escape(fld) + r":\s*\n\1\s+Guid:\s*\n((?:\1\s+Data[1-4]: -?\d+\s*\n){4})\1\s+Path: ?(.*)$",
                          doc_text, re.M))
    if not ms:
        return False, False, "", 0
    m = ms[0]
    datas = re.findall(r"Data[1-4]: (-?\d+)", m.group(2))
    path = m.group(3).strip()
    wired = any(d != "0" for d in datas) or bool(path)
    return True, wired, path, len(ms)


def object_ref_value(doc_text: str, fld: str):
    ms = list(re.finditer(r"^\s*" + re.escape(fld) + r": \{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?", doc_text, re.M))
    if not ms:
        return False, False, "", 0
    return True, ms[0].group(1) != "0", (ms[0].group(2) or ""), len(ms)


def sibling_scalar(doc_text: str, key: str):
    m = re.search(r"^\s*" + re.escape(key) + r": ?(\S*)$", doc_text, re.M)
    return m.group(1) if m else None


def is_stripped(doc_text: str) -> bool:
    """True for a scene/prefab component STUB whose values live on its source prefab."""
    first = doc_text.split("\n", 1)[0]
    if first.rstrip().endswith("stripped"):
        return True
    m = re.search(r"^  m_PrefabInstance: \{fileID: (-?\d+)\}", doc_text, re.M)
    return bool(m and m.group(1) != "0")


def parse_modifications(doc_text: str):
    src = SOURCE_PREFAB_RE.search(doc_text)
    if not src:
        return None, []
    return src.group(1), [(int(a), b, c, d.strip(), int(e)) for a, b, c, d, e in MOD_RE.findall(doc_text)]


# ---------------------------------------------------------------- census

def rel(path: str) -> str:
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def run_census(reg: dict):
    guid_by_class = class_guid_index()
    slots = discover_sound_slots() + registry_slots(reg, guid_by_class)
    # registry rows may refine an auto-discovered sound slot (required_when, label, group)
    refined = {}
    for s in slots:
        key = (s.cls, s.field)
        if key in refined and s.source == "registry":
            base = refined[key]
            base.required_when, base.label, base.group = s.required_when, s.label or base.label, s.group or base.group
            if s.discipline:
                base.discipline = s.discipline
            continue
        refined.setdefault(key, s)
    slots = list(refined.values())
    by_guid = defaultdict(list)
    for s in slots:
        if s.script_guid:
            by_guid[s.script_guid].append(s)

    excludes = reg.get("exclude_paths", [])
    overrides = defaultdict(list)       # (src_guid, target_fileid) -> [(host, propertyPath, value, objRef)]
    instances = []                      # (Doc, [Slot])
    stripped_skipped = [0]
    names = {}                          # (file, doc_id) -> GameObject name
    for dp, dns, fns in os.walk(ASSETS):
        dns[:] = [d for d in dns if not d.startswith(".")]
        for fn in fns:
            if not fn.endswith(ASSET_EXTS):
                continue
            path = os.path.join(dp, fn)
            r = rel(path)
            if any(r.startswith(e) for e in excludes):
                continue
            fguid = meta_guid(path) or ""
            docs = split_docs(path, fguid)
            for d in docs:
                if d.cls_id == 1:
                    nm = NAME_RE.search(d.text)
                    names[(path, d.doc_id)] = nm.group(1) if nm else ""
                elif d.cls_id == 1001:
                    src, mods = parse_modifications(d.text)
                    for tgt, tguid, prop, val, objref in mods:
                        overrides[(tguid, tgt)].append((r, prop, val, objref))
                elif d.cls_id == 114:
                    if is_stripped(d.text):
                        # A scene's handle onto a component that lives in a PREFAB. It carries the
                        # script guid and NONE of the fields, so counting it as an instance reports
                        # every slot on it as empty — 52 phantom AudioSystem gaps, all of them
                        # actually wired by the very overrides in the same file. Its real values are
                        # the prefab's doc plus this instance's m_Modifications, both already read.
                        stripped_skipped[0] += 1
                        continue
                    sm = SCRIPT_RE.search(d.text)
                    if sm and sm.group(1) in by_guid:
                        instances.append((d, by_guid[sm.group(1)]))

    waivers = {(w["class"], w["field"], w.get("asset", "*")): w["reason"] for w in reg.get("waivers", [])}
    rows: list[Row] = []
    for d, dslots in instances:
        r = rel(d.file)
        if d.file.endswith(".asset"):
            nm = NAME_RE.search(d.text)
            oname = nm.group(1) if nm else os.path.basename(d.file)
        else:
            gm = GO_RE.search(d.text)
            oname = names.get((d.file, int(gm.group(1))), "") if gm else ""
        for s in dslots:
            if s.required_when:
                unmet = [k for k, v in s.required_when.items() if sibling_scalar(d.text, k) != str(v)]
            else:
                unmet = []
            if s.kind == "sound":
                present, wired, detail, count = event_ref_value(d.text, s.field)
            else:
                present, wired, detail, count = object_ref_value(d.text, s.field)
            ovs = overrides.get((d.file_guid, d.doc_id), [])
            ov_wire, ov_null = [], []
            for host, prop, val, objref in ovs:
                if s.kind == "sound":
                    if prop == f"{s.field}.Path":
                        (ov_wire if val else ov_null).append(host)
                    elif prop.startswith(f"{s.field}.Guid.Data") and val not in ("0", ""):
                        ov_wire.append(host)
                elif prop == s.field:
                    (ov_wire if objref != 0 else ov_null).append(host)
            ov_wire, ov_null = sorted(set(ov_wire)), sorted(set(ov_null) - set(ov_wire))
            if count > 1:
                status, det = "ambiguous", f"{count} instances of this key in one document (a list of serialized holders) — the census reads only the first; teach the registry how to address them"
            elif unmet:
                status, det = "not-required", f"{'/'.join(unmet)} not set"
            elif wired and not ov_null:
                status, det = "wired", detail
            elif wired and ov_null:
                status, det = "nulled", "nulled by override in " + ", ".join(ov_null)
            elif ov_wire:
                status, det = "wired-by-override", "in " + ", ".join(ov_wire)
            elif present:
                status, det = "empty", ""
            else:
                status, det = "silent", "key absent from YAML: field added after the asset was last saved; runtime value is the field initializer (empty)"
            if status in ("empty", "silent", "nulled"):
                # A WAIVER is an authored statement that this slot is deliberately unfilled, with
                # the reason attached. Without one the only ways to quiet a row are to fill it or
                # to baseline it, and a baseline records that a gap exists without recording that
                # somebody decided it should.
                w = waivers.get((s.cls, s.field, r)) or waivers.get((s.cls, s.field, "*"))
                if w:
                    status, det = "waived", w
            rows.append(Row(id=f"{s.kind}:{s.cls}.{s.field}@{r}#{d.doc_id}", kind=s.kind, discipline=s.discipline,
                            cls=s.cls, field=s.field, asset=r, object_name=oname, status=status, detail=det,
                            group=s.group, label=s.label))

    # slots declared in code with NO instance anywhere: the class is never authored, which is its own finding
    seen = {(x.cls, x.field) for x in rows}
    unauthored = [s for s in slots if (s.cls, s.field) not in seen]

    # absence-of-slot expectations
    no_slot = []
    markers = re.compile(r"EventReference|PlayGameplaySFX|PlaySFXEvent|StudioEventEmitter|PlayMenuAudio")
    for exp in reg.get("no_slot_expectations", []):
        pat = exp["pattern"]
        excl = [re.compile(x) for x in exp.get("exclude_name_patterns", [])]
        for dp, _, fns in os.walk(SCRIPTS):
            for fn in fns:
                p = rel(os.path.join(dp, fn))
                if not fnmatch.fnmatch(p, pat) or any(x.search(fn) for x in excl):
                    continue
                with open(os.path.join(dp, fn), encoding="utf-8", errors="ignore") as f:
                    if not markers.search(f.read()):
                        no_slot.append({"id": f"no-slot:{exp['kind']}:{p}", "kind": exp["kind"],
                                        "discipline": exp.get("discipline", "Sound"), "file": p, "rule": exp["rule"]})
    return slots, rows, unauthored, no_slot, stripped_skipped[0]


# ---------------------------------------------------------------- reporting

def summarize(rows, unauthored, no_slot):
    gaps = [r for r in rows if r.is_gap]
    by_disc = defaultdict(int)
    for g in gaps:
        by_disc[g.discipline] += 1
    for n in no_slot:
        by_disc[n["discipline"]] += 1
    return {"slots_declared": None, "slot_instances": len(rows), "gaps": len(gaps) + len(no_slot),
            "gaps_by_discipline": dict(sorted(by_disc.items())),
            "wired": sum(r.status in ("wired", "wired-by-override") for r in rows),
            "silent": sum(r.status == "silent" for r in rows), "unauthored_slots": len(unauthored)}


def write_markdown(slots, rows, unauthored, no_slot, path):
    s = summarize(rows, unauthored, no_slot)
    lines = ["# Asset gap census", "",
             "Generated by `python3 Tools/Build/asset_gap_census.py --write`. **Do not hand-edit** — fix the",
             "asset, or teach `Tools/Build/asset_slot_registry.json` the rule, and regenerate.",
             "Architecture and the task pipeline this feeds: `Docs/ASSET_GAPS/ARCHITECTURE.md`.", "",
             f"| Slots declared | Slot instances | Wired | Gaps | Silent keys | Never-authored slots |",
             f"|---|---|---|---|---|---|",
             f"| {len(slots)} | {s['slot_instances']} | {s['wired']} | **{s['gaps']}** | {s['silent']} | {s['unauthored_slots']} |", "",
             "Gaps by discipline: " + ", ".join(f"{k} {v}" for k, v in s["gaps_by_discipline"].items()), ""]
    status_glyph = {"empty": "🔴 empty", "silent": "🔴 silent", "nulled": "🔴 nulled", "wired": "🟢 wired",
                    "wired-by-override": "🟢 override", "not-required": "⚪ n/a", "ambiguous": "🟠 ambiguous",
                    "waived": "⚪ waived"}
    gaps = defaultdict(list)
    for r in rows:
        if r.is_gap:
            gaps[(r.discipline, r.group)].append(r)
    lines += ["## Gaps (one task per group, one checkbox per row)", ""]
    for (disc, grp), grows in sorted(gaps.items()):
        lines += [f"### {disc} — `{grp}` ({len(grows)})", ""]
        for r in sorted(grows, key=lambda x: (x.asset, x.field)):
            det = f" — {r.detail}" if r.detail and r.status != "silent" else (" — silent key" if r.status == "silent" else "")
            lines.append(f"- [ ] `{r.cls}.{r.field}` on `{r.asset}`{(' (' + r.object_name + ')') if r.object_name else ''} {status_glyph[r.status]}{det}")
        lines.append("")
    if no_slot:
        lines += ["### Missing slots (a rule says a slot should exist and none does)", ""]
        for n in no_slot:
            lines.append(f"- [ ] `{n['file']}` — {n['rule']}")
        lines.append("")
    if unauthored:
        lines += ["## Slots declared in code with no authored instance", "",
                  "The class carries the field but no prefab / scene / asset instances it — either the class is",
                  "unused, or its asset has not been created yet. Both are findings.", ""]
        for u in unauthored:
            lines.append(f"- `{u.cls}.{u.field}` ({u.kind})")
        lines.append("")
    # Everything NOT a gap is summarised per slot rather than listed per instance: 1,400+ wired
    # rows is a file nobody reads, and census.json already carries every row for the task sync.
    lines += ["## Coverage per slot", "",
              "Every declared slot and how its instances resolved. The per-instance detail lives in",
              "`census.json`; this table is for spotting a slot that is authored nowhere.", "",
              "| Slot | Kind | wired | override | gap | n/a |", "|---|---|---|---|---|---|"]
    per = defaultdict(lambda: defaultdict(int))
    for r in rows:
        per[(r.cls, r.field, r.kind)][r.status] += 1
    for (cls, fld, kind), c in sorted(per.items()):
        gap = c["empty"] + c["silent"] + c["nulled"]
        na = c["not-required"] + c["waived"] + c["ambiguous"]
        lines.append(f"| `{cls}.{fld}` | {kind} | {c['wired']} | {c['wired-by-override']} | "
                     f"{'**' + str(gap) + '**' if gap else '0'} | {na} |")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--write-baseline", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    reg = load_registry()
    slots, rows, unauthored, no_slot, stripped = run_census(reg)
    s = summarize(rows, unauthored, no_slot)
    s["slots_declared"] = len(slots)
    print(f"asset-gap census: {s['slots_declared']} slots, {s['slot_instances']} instances, {s['wired']} wired, "
          f"{s['gaps']} gaps ({', '.join(f'{k} {v}' for k, v in s['gaps_by_discipline'].items())}), "
          f"{s['silent']} silent keys, {s['unauthored_slots']} never-authored slots, "
          f"{stripped} prefab-stub components resolved through their prefab"
          + (f", {len(UNSUPPORTED)} unsupported slot shapes" if UNSUPPORTED else ""))
    gap_ids = sorted([r.id for r in rows if r.is_gap] + [n["id"] for n in no_slot])
    if a.write or a.write_baseline:
        os.makedirs(OUT_DIR, exist_ok=True)
    if a.write:
        with open(OUT_JSON, "w", encoding="utf-8") as f:
            json.dump({"summary": s, "slots": [asdict(x) for x in slots], "rows": [asdict(r) for r in rows],
                       "no_slot": no_slot, "unauthored": [asdict(u) for u in unauthored],
                       "unsupported": UNSUPPORTED, "stripped_components_skipped": stripped}, f, indent=1)
        write_markdown(slots, rows, unauthored, no_slot, OUT_MD)
        print(f"wrote {rel(OUT_JSON)} and {rel(OUT_MD)}")
    if a.write_baseline:
        with open(BASELINE, "w", encoding="utf-8") as f:
            json.dump({"gap_ids": gap_ids}, f, indent=1)
        print(f"wrote {rel(BASELINE)} ({len(gap_ids)} known gaps)")
    if a.check:
        if not os.path.exists(BASELINE):
            print("no baseline; run --write-baseline first", file=sys.stderr)
            return 2
        with open(BASELINE, encoding="utf-8") as f:
            known = set(json.load(f)["gap_ids"])
        new = [g for g in gap_ids if g not in known]
        closed = [g for g in known if g not in set(gap_ids)]
        if closed:
            print(f"{len(closed)} baseline gaps are closed (prune with --write-baseline):")
            for g in closed:
                print("  closed:", g)
        if new:
            print(f"FAIL: {len(new)} gap(s) the baseline does not know — fill the slot, or record the task and re-baseline:")
            for g in new:
                print("  new:", g)
            return 1
        print("OK: no gap outside the baseline")
    return 0


# ---------------------------------------------------------------- self-test

def self_test():
    ev_wired = "MonoBehaviour:\n  fooEvent:\n    Guid:\n      Data1: 12\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: event:/x\n"
    ev_empty = "MonoBehaviour:\n  fooEvent:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
    nested = "MonoBehaviour:\n  Variant:\n    OverrideAudio: 1\n    AudioLoopEvent:\n      Guid:\n        Data1: 0\n        Data2: 0\n        Data3: 0\n        Data4: 0\n      Path: \n"
    assert event_ref_value(ev_wired, "fooEvent") == (True, True, "event:/x", 1)
    assert event_ref_value(ev_empty, "fooEvent") == (True, False, "", 1)
    assert event_ref_value(ev_empty, "barEvent") == (False, False, "", 0)         # silent key
    assert event_ref_value(nested, "AudioLoopEvent")[:2] == (True, False)         # deeper indent
    assert event_ref_value(ev_empty + ev_wired, "fooEvent")[3] == 2               # list of holders
    assert sibling_scalar(nested, "OverrideAudio") == "1"
    assert object_ref_value("  ParticleEffect: {fileID: 0}\n", "ParticleEffect") == (True, False, "", 1)
    assert object_ref_value("  Icon: {fileID: 21300000, guid: " + "a" * 32 + ", type: 3}\n", "Icon") == (True, True, "a" * 32, 1)
    mods = ("PrefabInstance:\n  m_Modification:\n    m_Modifications:\n"
            "    - target: {fileID: 42, guid: " + "b" * 32 + ",\n        type: 3}\n"
            "      propertyPath: fooEvent.Path\n      value: event:/y\n      objectReference: {fileID: 0}\n"
            "    - target: {fileID: 42, guid: " + "b" * 32 + ",\n        type: 3}\n"
            "      propertyPath: Icon\n      value: \n      objectReference: {fileID: 7, guid: " + "c" * 32 + ", type: 3}\n"
            "  m_SourcePrefab: {fileID: 100100000, guid: " + "b" * 32 + ", type: 3}\n")
    src, ms = parse_modifications(mods)
    assert src == "b" * 32 and len(ms) == 2 and ms[0][2] == "fooEvent.Path" and ms[0][3] == "event:/y" and ms[1][4] == 7
    # A nested [Serializable] holder (FaunaConfigurationSO.Variant) must be attributed to the
    # NESTED class, not the file's class — the census then looks for its key at a deeper indent.
    cs = ("namespace X\n{\n    public class Outer : MonoBehaviour\n    {\n"
          "        [SerializeField, Tooltip(\"x\")]\n        EventReference fireEvent;\n\n"
          "        [Serializable]\n        public class Inner\n        {\n"
          "            public FMODUnity.EventReference nestedEvent;\n        }\n    }\n}\n")
    found = [(c, n) for c, n, _ in serialized_event_fields(cs)]
    assert ("Outer", "fireEvent") in found, found
    assert ("Inner", "nestedEvent") in found, found

    # The three shapes that look like a slot and are not — each one shipped a false positive.
    negatives = ("public class Z : MonoBehaviour\n{\n"
                 "    public EventReference Real;\n"
                 "    public EventReference Prop => Real;\n"                       # expression-bodied property
                 "    static EventReference Shared;\n"                             # never serialized
                 "    EventReference Unserialized;\n"                              # private, no [SerializeField]
                 "    void PlayCue(FMODUnity.EventReference reference) { }\n"       # parameter
                 "    void Tick()\n    {\n"
                 "        EventReference reference = layers[i];\n"                  # local variable
                 "    }\n}\n")
    names = [n for _, n, _ in serialized_event_fields(negatives)]
    assert names == ["Real"], names
    arr = "public class A : MonoBehaviour\n{\n    [SerializeField] EventReference[] layers;\n}\n"
    assert serialized_event_fields(arr) == [("A", "layers", True)], serialized_event_fields(arr)
    # A multi-line attribute: every continuation line here neither starts with "[" nor ends with
    # "]", so a line-shape walk concludes there is no [SerializeField] and drops a real slot.
    wrapped = ('public class B : MonoBehaviour\n{\n    void Other() { }\n\n'
               '    [SerializeField, Tooltip("one " +\n        "two " +\n        "three")]\n'
               '    EventReference wrappedEvent;\n}\n')
    assert serialized_event_fields(wrapped) == [("B", "wrappedEvent", False)], serialized_event_fields(wrapped)
    # A tooltip containing the very characters that delimit an attribute block. This is real:
    # AudioSystem.creatureBlockHitEvent's tooltip carries a semicolon, and it vanished from the
    # census entirely until the scan learned to ignore string literals.
    punct = ('public class C : MonoBehaviour\n{\n    int other;\n\n'
             '    [SerializeField, Tooltip("kills; flora blocks {still} play their own")]\n'
             '    EventReference punctuatedEvent;\n}\n')
    assert serialized_event_fields(punct) == [("C", "punctuatedEvent", False)], serialized_event_fields(punct)
    assert len(mask_literals('a "b;c" d')) == len('a "b;c" d')
    assert ";" not in mask_literals('x("b;c")') and ";" in mask_literals("x; y")
    assert is_stripped("--- !u!114 &3100000003 stripped\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: x}\n")
    assert is_stripped("--- !u!114 &7\nMonoBehaviour:\n  m_PrefabInstance: {fileID: 3100000001}\n")
    assert not is_stripped("--- !u!114 &7\nMonoBehaviour:\n  m_PrefabInstance: {fileID: 0}\n  m_Script: {}\n")
    print("self-test OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())

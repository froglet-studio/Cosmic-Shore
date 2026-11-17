#!/usr/bin/env python3
"""sound_placeholders.py - recycled placeholder sounds, each one listed, findable and replaceable in one step.

Garrett, 2026-11-16: "if any vessel is missing a sound then recycle obvious sounds and adapt where possible. lets fill
all our holes ... make sure each sound has a place holder he can easily replace." Until then the convention was to
ship every empty FMOD slot silent, because a borrowed "temp" event is one nobody ever finds. This tool keeps that
promise by making every borrowed event FINDABLE: a slot may hold a recycled event only when the ledger
(Docs/AudioSystem/sound_placeholders.json) names it, and the ledger names the real event that replaces it.

THE LEDGER. One entry per sound the game owes (one TARGET event), most important first:
    sound        what the list calls it ("Manta engine")
    owner        the vessel, mode or system
    act          the moment it plays, one line, read from the code
    kind         one-shot | loop (what the slot's code does with it; the placeholder must match)
    placeholder  the banked event recycled into the slots until the target exists (null = left empty on purpose)
    target       the event the audio owner authors in FMOD Studio to replace it ("event:/SFX/Vessels/Manta/Engine")
    brief        what the real sound should be
    slots        every slot it fills: "Assets/...asset:field", "Assets/...:field@Object" when one asset carries the field
                 on several objects ("@*" = all of them)
    tier, group  the list's sections

HOW CHARLES REPLACES ONE (no Unity work): in FMOD Studio, author the event at the TARGET path (right-click the
placeholder event > Duplicate is a fine start), build the banks, commit. Then anyone runs `--promote` (or /sound-brief
does): every slot still holding the placeholder is repointed at the target. Nothing is renamed in Unity by hand.

    python3 Tools/Build/sound_placeholders.py             # the ledger's state: pending, promotable, replaced, missing
    python3 Tools/Build/sound_placeholders.py --apply     # write the placeholders into the slots that are still empty
    python3 Tools/Build/sound_placeholders.py --promote   # repoint slots at every target that is now authored and banked
    python3 Tools/Build/sound_placeholders.py --write-md  # rewrite Docs/AudioSystem/PLACEHOLDERS.md (the list for Charles)
    python3 Tools/Build/sound_placeholders.py --check     # the gate (below)
    python3 Tools/Build/sound_placeholders.py --self-test

`--check` fails when: a placeholder event is not authored or not in the built banks; a placeholder's loop/one-shot
kind does not match the entry or the slot's code (C2 / C4); a slot cannot be found (asset moved, field renamed); a
slot that should hold its placeholder is empty (run --apply); or PLACEHOLDERS.md is stale (run --write-md). A slot
that holds some OTHER event was replaced by hand: reported, never failed, and its ledger line can go.

A WRITER for Unity text assets: it edits only the `Guid: Data1..4` and `Path:` lines of the named field (or appends
the field's block to its component when the asset has never saved it), keeps every other byte, and never opens
Unity. It reads slots and events through sound_slots.py.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sound_slots as ss  # noqa: E402

ROOT = ss.ROOT
LEDGER = os.path.join("Docs", "AudioSystem", "sound_placeholders.json")
LIST_MD = os.path.join("Docs", "AudioSystem", "PLACEHOLDERS.md")
LOCK = os.path.join("Docs", "AudioSystem", "sound_placeholders.lock.json")
LOOP_EXPECT = {"loop", "held"}


# ----------------------------------------------------------------------------------------------- YAML surgery

def find_block_lines(body_lines: list[str]) -> dict[str, int]:
    """field path -> index of its `Guid:` line, for every FMOD reference block in one YAML document."""
    out: dict[str, int] = {}
    stack: list[list] = []
    for i, raw in enumerate(body_lines):
        m = ss.KEY_RE.match(raw)
        if not m:
            continue
        indent = len(m.group(1))
        key = m.group(3)
        if m.group(2):
            while stack and stack[-1][0] >= indent + 2:
                stack.pop()
            owner = stack[-1] if stack and stack[-1][0] == indent and not stack[-1][1].startswith("[") else None
            idx = owner[2] if owner else 0
            if owner:
                owner[2] += 1
            stack.append([indent + 2, f"[{idx}]", 0])
            indent += 2
        while stack and (stack[-1][0] > indent or (stack[-1][0] == indent and not stack[-1][1].startswith("["))):
            stack.pop()
        if key == "Guid" and i + 5 < len(body_lines) and [ss.KEY_RE.match(body_lines[i + k]).group(3) if ss.KEY_RE.match(body_lines[i + k]) else ""
                                                       for k in range(1, 6)] == ["Data1", "Data2", "Data3", "Data4", "Path"]:
            path = ""
            for node in stack[1:]:
                path += node[1] if node[1].startswith("[") else (("." if path else "") + node[1])
            out[path] = i
        stack.append([indent, key, 0])
    return out


def set_block(lines: list[str], guid_line: int, data: tuple[int, int, int, int], path: str) -> None:
    for k in range(4):
        ln = lines[guid_line + 1 + k]
        lines[guid_line + 1 + k] = re.sub(r"(Data%d:).*$" % (k + 1), r"\g<1> %d" % data[k], ln)
    ln = lines[guid_line + 5]
    lines[guid_line + 5] = re.sub(r"(Path:).*$", lambda m: m.group(1) + " " + path, ln)


def block_text(field: str, data: tuple[int, int, int, int], path: str, indent: str = "  ") -> list[str]:
    return [f"{indent}{field}:", f"{indent}  Guid:"] + [f"{indent}    Data{k + 1}: {data[k]}" for k in range(4)] + [f"{indent}  Path: {path}"]


def read_block(lines: list[str], guid_line: int) -> tuple[tuple[int, int, int, int], str]:
    vals = []
    for k in range(4):
        m = re.search(r"Data\d:\s*(-?\d+)", lines[guid_line + 1 + k])
        vals.append(int(m.group(1)) if m else 0)
    m = re.search(r"Path:\s?(.*)$", lines[guid_line + 5])
    return tuple(vals), (m.group(1).strip() if m else "")  # type: ignore[return-value]


class Doc:
    """One YAML document of a Unity text asset: its header line index, its body lines, its script and object."""

    def __init__(self, start: int, end: int, cid: int, fid: str, stripped: bool):
        self.start, self.end, self.cid, self.fid, self.stripped = start, end, cid, fid, stripped
        self.script_guid = ""
        self.object = ""


def split_lines(lines: list[str]) -> list[Doc]:
    docs: list[Doc] = []
    for i, ln in enumerate(lines):
        m = re.match(r"^--- !u!(\d+) &(-?\d+)( stripped)?", ln)
        if m:
            if docs:
                docs[-1].end = i
            docs.append(Doc(i, len(lines), int(m.group(1)), m.group(2), bool(m.group(3))))
    names: dict[str, str] = {}
    for d in docs:
        body = lines[d.start + 1:d.end]
        if d.cid == 1:
            for ln in body:
                m = re.match(r"^\s*m_Name: (.*)$", ln)
                if m:
                    names[d.fid] = m.group(1).strip()
                    break
    for d in docs:
        if d.cid != 114 or d.stripped:
            continue
        go = ""
        for ln in lines[d.start + 1:d.end]:
            m = ss.SCRIPT_RE.search(ln)
            if m:
                d.script_guid = m.group(1)
            m = re.match(r"^\s*m_GameObject: \{fileID: (-?\d+)\}", ln)
            if m:
                go = m.group(1)
            m = re.match(r"^\s*m_Name: (.*)$", ln)
            if m and not d.object:
                d.object = m.group(1).strip()
        if go and go != "0":
            d.object = names.get(go, "")
    return docs


# ----------------------------------------------------------------------------------------------- the generators' hook

_LOCK_CACHE: dict[str, dict] = {}


def _lock(root: str) -> dict:
    if root not in _LOCK_CACHE:
        path = os.path.join(root, LOCK)
        try:
            with open(path, encoding="utf-8") as fh:
                _LOCK_CACHE[root] = json.load(fh).get("assets", {})
        except (OSError, ValueError):
            _LOCK_CACHE[root] = {}
    return _LOCK_CACHE[root]


def fill_text(rel: str, text: str, root: str = ROOT) -> str:
    """The text a generator should write for `rel`, with every ledger-listed FMOD slot holding what the tree holds
    (its placeholder, or its target once promoted). A generator that owns an asset with a listed slot calls this on
    its output before it writes or diffs, so re-running it keeps the placeholders instead of emptying them (CLAUDE.md:
    a generator that owns an asset's content is a second place every change has to land). Reads only the lock file
    (`--apply` / `--promote` / `--write-md` rewrite it), so it is fast and needs no scan. Unchanged for any other
    asset."""
    items = _lock(root).get(rel.replace(os.sep, "/"))
    if not items:
        return text
    lines = text.split("\n")
    for it in items:
        docs = split_lines(lines)
        doc = next((d for d in docs if d.fid == it["fid"]), None)
        if doc is None:
            continue
        data = tuple(it["data"])
        blocks = find_block_lines(lines[doc.start + 1:doc.end])
        if it["field"] in blocks:
            set_block(lines, doc.start + 1 + blocks[it["field"]], data, it["path"])  # type: ignore[arg-type]
        elif any(data) or it["path"]:
            end = doc.end
            while end > doc.start + 1 and lines[end - 1] == "":
                end -= 1
            lines[end:end] = block_text(it["field"], data, it["path"])  # type: ignore[arg-type]
    return "\n".join(lines)


def fill_file(path: str, text: str) -> str:
    """fill_text for a generator that holds an absolute path (or one relative to the repository root)."""
    rel = os.path.relpath(path, ROOT) if os.path.isabs(path) else path
    return fill_text(rel.replace(os.sep, "/"), text)


# ----------------------------------------------------------------------------------------------- the ledger

def parse_selector(sel: str) -> tuple[str, str, str | None]:
    """'Assets/x.prefab:field@Object' -> (asset, field, object or None; '*' = every object carrying the field)."""
    asset, rest = sel.rsplit(":", 1) if sel.count(":") else (sel, "")
    field, obj = (rest.split("@", 1) + [None])[:2] if "@" in rest else (rest, None)
    return asset, field, obj


class Target:
    """One resolved slot: the file, the document, the field and where its block is (or that it has none).
    When a scene override sets the slot (`override`), the value lives in that scene's m_Modifications instead:
    `override` is (scene path, {"Path": line, "Data1": line, ...}) and is what is read and written."""

    def __init__(self, asset: str, field: str, doc: Doc, guid_line: int | None, slot: ss.Slot | None,
                 override: tuple[str, dict[str, int]] | None = None):
        self.asset, self.field, self.doc, self.guid_line, self.slot = asset, field, doc, guid_line, slot
        self.override = override

    @property
    def label(self) -> str:
        return f"{self.asset}:{self.field}" + (f"@{self.doc.object}" if self.doc.object else "") + \
            (f" (set in {os.path.basename(self.override[0])})" if self.override else "")


def override_lines(lines: list[str], prefab_guid: str, field: str) -> dict[str, int]:
    """{"Path"|"Data1".."Data4": index of the `value:` line} for a scene's override of `field` on a prefab."""
    out: dict[str, int] = {}
    for i, ln in enumerate(lines):
        m = re.match(r"^\s*propertyPath: (.+?)\s*$", ln)
        if not m or i + 1 >= len(lines) or i == 0:
            continue
        prop = m.group(1)
        pm = re.match(r"^%s\.(Path|Guid\.Data([1-4]))$" % re.escape(field), prop)
        if not pm or not prefab_guid or prefab_guid not in "".join(lines[max(0, i - 2):i]):  # `- target:` may wrap
            continue
        if re.match(r"^\s*value:", lines[i + 1]):
            out["Path" if pm.group(1) == "Path" else f"Data{pm.group(2)}"] = i + 1
    return out


class Ledger:
    def __init__(self, root: str, scan: ss.Scan | None = None):
        self.root = root
        with open(os.path.join(root, LEDGER), encoding="utf-8") as fh:
            self.data = json.load(fh)
        self.entries: list[dict] = self.data["sounds"]
        self.scan = scan or ss.build(root)
        self.fmod = self.scan.fmod
        self.guid_of_script = {rel: g for g, rel in self.scan.cs_by_guid.items()}
        self._lines: dict[str, list[str]] = {}
        self._docs: dict[str, list[Doc]] = {}
        self.dirty: set[str] = set()

    # -- files -------------------------------------------------------------------------------------------------
    def lines(self, asset: str) -> list[str]:
        if asset not in self._lines:
            with open(os.path.join(self.root, asset), encoding="utf-8", newline="") as fh:
                text = fh.read()
            self._lines[asset] = text.split("\n")
            self._docs[asset] = split_lines(self._lines[asset])
        return self._lines[asset]

    def docs(self, asset: str) -> list[Doc]:
        self.lines(asset)
        return self._docs[asset]

    def save(self) -> list[str]:
        for asset in sorted(self.dirty):
            with open(os.path.join(self.root, asset), "w", encoding="utf-8", newline="") as fh:
                fh.write("\n".join(self._lines[asset]))
        return sorted(self.dirty)

    # -- events ------------------------------------------------------------------------------------------------
    def event(self, path: str | None) -> ss.FmodEvent | None:
        return self.fmod.by_path.get(path) if path else None

    def banked(self, ev: ss.FmodEvent | None) -> bool:
        return ev is not None and ev.guid in self.fmod.banked

    def audible(self, ev: ss.FmodEvent | None) -> bool:
        """The event's metadata references audio (a file, a nested event or a programmer sound). `Gun Fire` and
        `Explosion` were authored and banked with nothing in them, so 'banked' alone does not mean 'makes a sound'."""
        if ev is None:
            return False
        xml = os.path.join(self.fmod.dir, "Metadata", "Event", "{%s}.xml" % ev.guid)
        if not os.path.exists(xml):
            return True  # in the bank without an XML here (authored on another branch): trust the bank
        text = ss.read_text(xml)
        return any(k in text for k in ('name="audioFile"', 'class="EventSound"', 'class="ProgrammerSound"'))

    # -- slots -------------------------------------------------------------------------------------------------
    def resolve(self, sel: str) -> tuple[list[Target], str]:
        asset, field, obj = parse_selector(sel)
        if not os.path.exists(os.path.join(self.root, asset)):
            return [], f"{sel}: no such asset"
        slots = [s for s in self.scan.slots if s.asset == asset and s.field == field
                 and (obj in (None, "*") or s.object_name == obj)]
        if not slots:
            return [], f"{sel}: no FMOD slot '{field}' in {asset}" + (f" on '{obj}'" if obj not in (None, "*") else "")
        if obj is None and len({s.object_name for s in slots}) > 1:
            return [], f"{sel}: '{field}' is on several objects ({', '.join(sorted({s.object_name for s in slots}))}); add @Object or @*"
        out: list[Target] = []
        lines = self.lines(asset)
        for s in slots:
            if s.override_from:
                prefab_guid = next((g for g, pth in self.scan.guid_map.items()
                                    if os.path.relpath(pth, self.root).replace(os.sep, "/") == asset), "")
                ov = override_lines(self.lines(s.override_from), prefab_guid, field)
                if set(ov) != {"Path", "Data1", "Data2", "Data3", "Data4"}:
                    return [], f"{sel}: set by an override in {s.override_from}, but not all five of its lines were found"
                out.append(Target(asset, field, Doc(0, 0, 114, "", False), None, s, (s.override_from, ov)))
                continue
            sg = self.guid_of_script.get(s.script, "")
            cands = [d for d in self.docs(asset) if d.cid == 114 and not d.stripped and d.script_guid == sg
                     and (d.object == s.object_name or not s.object_name)]
            hits = []
            for d in cands:
                blocks = find_block_lines(lines[d.start + 1:d.end])
                if field in blocks:
                    hits.append((d, d.start + 1 + blocks[field]))
                elif s.kind == "unserialized":
                    hits.append((d, None))
            if len(hits) != 1:
                return [], f"{sel}: found {len(hits)} components that could own it (expected exactly one)"
            out.append(Target(asset, field, hits[0][0], hits[0][1], s))
        # last in the file first: appending a block shifts only the lines after it, so earlier targets stay valid
        out.sort(key=lambda t: t.doc.start, reverse=True)
        return out, ""

    def value(self, t: Target) -> tuple[tuple[int, int, int, int], str]:
        if t.override:
            lines, ov = self.lines(t.override[0]), t.override[1]
            data = []
            for k in range(1, 5):
                m = re.search(r"value:\s*(-?\d+)", lines[ov[f"Data{k}"]])
                data.append(int(m.group(1)) if m else 0)
            m = re.search(r"value:\s?(.*)$", lines[ov["Path"]])
            return tuple(data), (m.group(1).strip() if m else "")  # type: ignore[return-value]
        if t.guid_line is None:
            return (0, 0, 0, 0), ""
        return read_block(self.lines(t.asset), t.guid_line)

    def write(self, t: Target, ev: ss.FmodEvent | None) -> None:
        data = ss.guid_to_data(ev.guid) if ev else (0, 0, 0, 0)
        path = ev.path if ev else ""
        if t.override:
            lines, ov = self.lines(t.override[0]), t.override[1]
            for k in range(1, 5):
                lines[ov[f"Data{k}"]] = re.sub(r"(value:).*$", r"\g<1> %d" % data[k - 1], lines[ov[f"Data{k}"]])
            lines[ov["Path"]] = re.sub(r"(value:).*$", lambda m: m.group(1) + " " + path, lines[ov["Path"]])
            self.dirty.add(t.override[0])
            return
        lines = self.lines(t.asset)
        if t.guid_line is not None:
            set_block(lines, t.guid_line, data, path)
        else:
            if ev is None:
                return
            end = t.doc.end
            while end > t.doc.start + 1 and lines[end - 1] == "":
                end -= 1
            new = block_text(t.field, data, path)
            lines[end:end] = new
            self._docs[t.asset] = split_lines(lines)  # every later document moved
            for d in self._docs[t.asset]:
                if d.start == t.doc.start:
                    t.doc = d
            t.guid_line = end + 1
        self.dirty.add(t.asset)

    def lock_data(self) -> dict:
        """{asset: [{fid, field, data, path}]}: what every listed slot holds now (overrides excluded: no generator
        writes a scene override). The generators' hook reproduces exactly this."""
        out: dict[str, list[dict]] = {}
        for e in self.entries:
            for sel in e["slots"]:
                for t in self.resolve(sel)[0]:
                    if t.override:
                        continue
                    data, path = self.value(t)
                    if t.guid_line is None and not any(data) and not path:
                        continue  # never saved and empty: nothing to reproduce
                    out.setdefault(t.asset, []).append(dict(fid=t.doc.fid, field=t.field, data=list(data), path=path,
                                                            _at=t.guid_line if t.guid_line is not None else 1 << 30))
        for v in out.values():
            v.sort(key=lambda d: d.pop("_at"))  # the order the tree holds them: an appended field lands where it was
        return dict(sorted(out.items()))

    def state(self, entry: dict, t: Target) -> str:
        """placeholder | target | empty | other: what the slot holds now, against this entry."""
        data, path = self.value(t)
        if not any(data) and not path:
            return "empty"
        held = self.fmod.by_data.get(data) or self.fmod.by_path.get(path)
        if held and held.path == entry.get("target"):
            return "target"
        if held and held.path == entry.get("placeholder"):
            return "placeholder"
        return "other"


# ----------------------------------------------------------------------------------------------- the passes

def entry_problems(lg: Ledger, e: dict) -> list[str]:
    out = []
    ph = lg.event(e.get("placeholder"))
    kind = e.get("kind")
    if kind not in ("one-shot", "loop"):
        out.append(f"{e['sound']}: kind must be one-shot or loop, not {kind!r}")
    if e.get("placeholder"):
        if ph is None:
            out.append(f"{e['sound']}: placeholder '{e['placeholder']}' is not an event in the FMOD project")
        elif not lg.banked(ph):
            out.append(f"{e['sound']}: placeholder '{ph.path}' is not in the built banks (it would play nothing)")
        elif not lg.audible(ph):
            out.append(f"{e['sound']}: placeholder '{ph.path}' has no audio in it (an empty event plays nothing)")
        elif ph.loops != (kind == "loop"):
            out.append(f"{e['sound']}: placeholder '{ph.path}' {'loops' if ph.loops else 'does not loop'} but the slot is a {kind} slot")
    if not str(e.get("target", "")).startswith("event:/"):
        out.append(f"{e['sound']}: target must be an event path")
    return out


def run(lg: Ledger, mode: str) -> tuple[list[str], list[str], list[str]]:
    """(report rows, failures, files written) for mode status | apply | promote | check."""
    rows, fails = [], []
    seen: dict[str, str] = {}
    for e in lg.entries:
        fails += entry_problems(lg, e)
        ph = lg.event(e.get("placeholder"))
        tgt = lg.event(e.get("target"))
        tgt_ready = lg.banked(tgt) and lg.audible(tgt) and (tgt.loops == (e.get("kind") == "loop"))
        for sel in e["slots"]:
            targets, err = lg.resolve(sel)
            if err:
                fails.append(err)
                continue
            for t in targets:
                if t.label in seen:
                    fails.append(f"{t.label}: listed under both '{seen[t.label]}' and '{e['sound']}'")
                seen[t.label] = e["sound"]
                if t.slot and e.get("kind") == "loop" and t.slot.expectation in ss.ONE_SHOT_EXPECTATIONS:
                    fails.append(f"{t.label}: a loop entry on a one-shot slot ({t.slot.how}) - C2")
                if t.slot and e.get("kind") == "one-shot" and t.slot.expectation == "loop":
                    fails.append(f"{t.label}: a one-shot entry on a loop slot ({t.slot.how}) - C4")
                st = lg.state(e, t)
                if st == "target" and not tgt_ready:
                    st = "silent-target"  # the target exists but has no sound in it yet: the placeholder stands in
                if mode == "promote" and tgt_ready and st in ("placeholder", "empty"):
                    lg.write(t, tgt)
                    st = "target (promoted now)"
                elif mode == "apply" and st in ("empty", "silent-target") and ph is not None and not tgt_ready:
                    lg.write(t, ph)
                    st = "placeholder (written now)"
                elif mode == "apply" and st == "empty" and tgt_ready:
                    lg.write(t, tgt)
                    st = "target (written now)"
                elif mode == "apply" and e.get("placeholder") is None and e.get("clear") and st == "other":
                    lg.write(t, None)
                    st = "empty (cleared now)"
                if mode == "check":
                    if st in ("empty", "silent-target") and ph is not None:
                        fails.append(f"{t.label}: empty, should hold its placeholder '{ph.path}' (run --apply)")
                    if e.get("placeholder") is None and e.get("clear") and st == "other":
                        fails.append(f"{t.label}: still holds the mis-wired event it should be cleared of (run --apply)")
                    if st == "placeholder" and tgt_ready:
                        rows.append(f"PROMOTE {t.label}: '{e['target']}' is authored and banked (run --promote)")
                rows.append(f"{st:<26} {e['sound']:<40} {t.label}")
    written = lg.save() if mode in ("apply", "promote") else []
    return rows, fails, written


# ----------------------------------------------------------------------------------------------- the list

TIER_TITLES = {
    "P1": "P1 - heard every match, every second (engines, primary fire, the race's gates)",
    "P2": "P2 - each vessel's signature ability acts",
    "P3": "P3 - secondary acts, crew and mode beats, level-5 upgrades",
    "P4": "P4 - menus, payloads, artifacts and the rest",
}


def short_event(p: str | None) -> str:
    return p.replace("event:/", "") if p else "(left empty)"


def render_md(lg: Ledger) -> str:
    entries = lg.entries
    n_slots = sum(len(lg.resolve(s)[0]) for e in entries for s in e["slots"])
    lines = [
        "# Placeholder sounds: what Charles makes first",
        "",
        "Generated by `python3 Tools/Build/sound_placeholders.py --write-md` from "
        "[`sound_placeholders.json`](sound_placeholders.json); edit the JSON, never this page.",
        "",
        f"Every empty vessel sound slot (and the race, menu and payload slots beside them) now plays a **recycled "
        f"placeholder**: an event already in the banks, chosen to fit the act (Garrett, 2026-11-16). That is "
        f"**{len(entries)} sounds over {n_slots} slots**. Each sound below has a **target** event path. Nothing in "
        f"the game points at the target yet. The same list as a page to share, with search and copy buttons: "
        f"https://claude.ai/artifact/TvjtZ42i2fwHMNF1x1tMPc (rebuilt from this ledger).",
        "",
        "**How to replace one (no Unity work):**",
        "",
        "1. In FMOD Studio, create the event at the target path (right-click the placeholder event > **Duplicate** is a "
        "quick start; keep it a loop or a one-shot as listed). Assign it to the `SFX` bank.",
        "2. Build the banks and commit `Cosmic Shore/` as usual.",
        "3. Run `python3 Tools/Build/sound_placeholders.py --promote` (or ask any Claude session to): every slot still "
        "on the placeholder moves to your event. `--check` tells you which targets are ready.",
        "",
        "Loops must stay loops and one-shots one-shots (the code starts and stops a loop; a one-shot is fired and "
        "forgotten). Parameters the code drives are listed where they matter. A sound names the act and the element, "
        "never the team (`SOUND_LANGUAGE.md`).",
        "",
    ]
    tiers: dict[str, list[dict]] = {}
    for e in entries:
        tiers.setdefault(e.get("tier", "P4"), []).append(e)
    rank = 0
    for tier in sorted(tiers):
        lines += [f"## {TIER_TITLES.get(tier, tier)}", "",
                  "| # | Sound | When it plays | Kind | Placeholder now | Make this event | Slots |", "|---|---|---|---|---|---|---|"]
        groups_done: set[str] = set()
        for e in tiers[tier]:
            g = e.get("group")
            if g and g in groups_done:
                continue
            members = [x for x in tiers[tier] if g and x.get("group") == g] or [e]
            rank += 1
            if len(members) > 1:
                groups_done.add(g)
                n = sum(len(lg.resolve(s)[0]) for x in members for s in x["slots"])
                phs = sorted({short_event(x.get("placeholder")) for x in members})
                tg = "; ".join(f"`{short_event(x['target'])}`" for x in members)
                lines.append(f"| {rank} | **{g}** ({len(members)} events) | {e.get('group_act', e['act'])} | {e['kind']} | "
                             f"{', '.join('`' + p + '`' for p in phs)} | {tg} | {n} |")
            else:
                n = sum(len(lg.resolve(s)[0]) for s in e["slots"])
                lines.append(f"| {rank} | **{e['sound']}** | {e['act']} | {e['kind']} | `{short_event(e.get('placeholder'))}` | "
                             f"`{short_event(e['target'])}` | {n} |")
        lines.append("")
    lines += ["## The brief for each sound", "",
              "What the real sound should be, in list order. The placeholder is only a stand-in for timing and level.", ""]
    for e in entries:
        lines.append(f"- **{e['sound']}** (`{short_event(e['target'])}`, {e['kind']}): {e.get('brief', '').strip()}")
    skipped = lg.data.get("not_filled", [])
    if skipped:
        lines += ["", "## Left empty on purpose", "",
                  "These slots stay silent; each needs a decision or a new sound, not a recycled one.", ""]
        for s in skipped:
            lines.append(f"- **{s['what']}**: {s['why']}")
    lines += ["", "## Every slot, by sound", "",
              "| Sound | Slot (asset : field @ object) |", "|---|---|"]
    for e in entries:
        labels = []
        for sel in e["slots"]:
            ts, err = lg.resolve(sel)
            labels += [f"`{t.label.replace('Assets/', '')}`" for t in ts] or [f"`{sel}` (unresolved: {err})"]
        lines.append(f"| {e['sound']} | " + "<br>".join(labels) + " |")
    return "\n".join(lines) + "\n"


# ----------------------------------------------------------------------------------------------- self-test

def self_test() -> int:
    asset = (
        "%YAML 1.1\n--- !u!1 &10\nGameObject:\n  m_Name: Hull\n"
        "--- !u!114 &20\nMonoBehaviour:\n  m_GameObject: {fileID: 10}\n"
        "  m_Script: {fileID: 11500000, guid: aaaa0000aaaa0000aaaa0000aaaa0000, type: 3}\n"
        "  engineEvent:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
        "  combos:\n  - name: a\n    sound:\n      Guid:\n        Data1: 0\n        Data2: 0\n        Data3: 0\n        Data4: 0\n      Path: \n"
        "  - name: b\n    sound:\n      Guid:\n        Data1: 0\n        Data2: 0\n        Data3: 0\n        Data4: 0\n      Path: \n"
        "--- !u!114 &30\nMonoBehaviour:\n  m_GameObject: {fileID: 10}\n  speed: 3\n"
    )
    lines = asset.split("\n")
    docs = split_lines(lines)
    assert [d.fid for d in docs] == ["10", "20", "30"], docs
    assert docs[1].object == "Hull" and docs[1].script_guid.startswith("aaaa"), (docs[1].object, docs[1].script_guid)
    blocks = find_block_lines(lines[docs[1].start + 1:docs[1].end])
    assert set(blocks) == {"engineEvent", "combos[0].sound", "combos[1].sound"}, blocks
    g = docs[1].start + 1 + blocks["combos[1].sound"]
    data = ss.guid_to_data("942c0807-260e-4954-8d1c-c321d5b954b3")
    set_block(lines, g, data, "event:/Engine stuff/space ship engine main")
    assert read_block(lines, g) == (data, "event:/Engine stuff/space ship engine main"), read_block(lines, g)
    g0 = docs[1].start + 1 + blocks["combos[0].sound"]
    assert read_block(lines, g0) == ((0, 0, 0, 0), ""), "the sibling block must be untouched"
    # back to empty keeps Unity's own empty spelling ("Path: " with its trailing space)
    set_block(lines, g, (0, 0, 0, 0), "")
    assert lines[g + 5] == "      Path: ", repr(lines[g + 5])
    # an unserialized field is appended to its own component, before the next document
    new = block_text("chargeEvent", data, "event:/X")
    end = docs[1].end
    lines[end:end] = new
    docs2 = split_lines(lines)
    b2 = find_block_lines(lines[docs2[1].start + 1:docs2[1].end])
    assert "chargeEvent" in b2 and docs2[2].fid == "30", (b2, docs2)
    assert read_block(lines, docs2[1].start + 1 + b2["chargeEvent"]) == (data, "event:/X")
    assert parse_selector("Assets/a b/x.prefab:combos[3].sound") == ("Assets/a b/x.prefab", "combos[3].sound", None)
    assert parse_selector("Assets/x.unity:onTriggerEvent@*") == ("Assets/x.unity", "onTriggerEvent", "*")
    # fill_text: a generator's empty output regains the listed value, both for a saved block and a missing field
    tmp = tempfile.mkdtemp()
    os.makedirs(os.path.join(tmp, "Docs", "AudioSystem"))
    with open(os.path.join(tmp, LOCK), "w", encoding="utf-8") as fh:
        json.dump({"assets": {"Assets/x.prefab": [
            dict(fid="20", field="engineEvent", data=list(data), path="event:/E"),
            dict(fid="30", field="newEvent", data=list(data), path="event:/N")]}}, fh)
    filled = fill_text("Assets/x.prefab", asset, root=tmp)
    fl = filled.split("\n")
    fd = split_lines(fl)
    b20 = find_block_lines(fl[fd[1].start + 1:fd[1].end])
    b30 = find_block_lines(fl[fd[2].start + 1:fd[2].end])
    assert read_block(fl, fd[1].start + 1 + b20["engineEvent"]) == (data, "event:/E"), "a saved block takes the lock's value"
    assert read_block(fl, fd[2].start + 1 + b30["newEvent"]) == (data, "event:/N"), "a missing field is appended"
    assert fill_text("Assets/x.prefab", filled, root=tmp) == filled, "fill_text is idempotent"
    assert fill_text("Assets/other.prefab", asset, root=tmp) == asset, "an unlisted asset is untouched"
    # the real ledger resolves, if this tree has one
    if os.path.exists(os.path.join(ROOT, LEDGER)):
        lg = Ledger(ROOT)
        rows, fails, _ = run(lg, "status")
        unresolved = [f for f in fails if "no FMOD slot" in f or "no such asset" in f or "could own" in f]
        assert not unresolved, unresolved[:5]
    print("sound_placeholders: self-test passed")
    return 0


# ----------------------------------------------------------------------------------------------- main

def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--apply", action="store_true", help="write each placeholder into its empty slots")
    ap.add_argument("--promote", action="store_true", help="repoint slots at every target now authored and banked")
    ap.add_argument("--write-md", action="store_true", help=f"rewrite {LIST_MD}")
    ap.add_argument("--check", action="store_true", help="exit 1 on a missing, unbanked or mis-kinded placeholder, or a stale list")
    ap.add_argument("--quiet", action="store_true", help="print only failures and the summary")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--root", default=ROOT, help=argparse.SUPPRESS)
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    mode = "apply" if args.apply else "promote" if args.promote else "check" if args.check else "status"
    lg = Ledger(args.root)
    rows, fails, written = run(lg, mode)
    if mode in ("apply", "promote") and written:
        lg = Ledger(args.root)  # re-read what was written, so the list below describes the files on disk
    md_path = os.path.join(args.root, LIST_MD)
    md = render_md(lg)
    lock_path = os.path.join(args.root, LOCK)
    lock = json.dumps({"about": "Generated by sound_placeholders.py: what every listed slot holds, for the generators' "
                                "fill_text() hook. Never edit by hand.", "assets": lg.lock_data()}, indent=1) + "\n"
    if args.write_md or (mode in ("apply", "promote") and written):
        with open(md_path, "w", encoding="utf-8") as fh:
            fh.write(md)
        with open(lock_path, "w", encoding="utf-8") as fh:
            fh.write(lock)
        print(f"sound_placeholders: wrote {LIST_MD} and {LOCK}")
    elif mode == "check":
        cur = open(md_path, encoding="utf-8").read() if os.path.exists(md_path) else ""
        if cur != md:
            fails.append(f"{LIST_MD} is stale (run --write-md)")
        cur = open(lock_path, encoding="utf-8").read() if os.path.exists(lock_path) else ""
        if cur != lock:
            fails.append(f"{LOCK} is stale (run --write-md)")
    if not args.quiet:
        for r in rows:
            print(r)
    for f in fails:
        print(f"FAIL {f}")
    states: dict[str, int] = {}
    for r in rows:
        k = r.split(" ")[0]
        states[k] = states.get(k, 0) + 1
    print(f"sound_placeholders: {len(lg.entries)} sounds, " + ", ".join(f"{v} {k}" for k, v in sorted(states.items()))
          + (f"; wrote {len(written)} files" if written else "") + (f"; {len(fails)} failures" if fails else ""))
    return 1 if fails and mode == "check" else 0


if __name__ == "__main__":
    sys.exit(main())

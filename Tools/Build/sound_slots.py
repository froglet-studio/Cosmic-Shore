#!/usr/bin/env python3
"""sound_slots.py - every FMOD sound slot in the game: its owner, what the code expects of it, what is in it, and
what the FMOD Studio project says about that event (does it loop? is it built into a bank?).

A READER tool. It reads prefabs, scenes and ScriptableObject assets under Assets/, the runtime C# under
Assets/_Scripts, and the FMOD Studio project under "Cosmic Shore/" (the event metadata XML, Build/GUIDs.txt, the
bank folders), plus ProjectSettings and the build profiles for the platform list. It writes nothing - except
`--charles`, which rewrites ONE generated block at the END of Docs/AudioSystem/CHARLES_TASKS.md between
`<!-- sound-slots:begin -->` and `<!-- sound-slots:end -->` (the markers are appended the first time; the
hand-written C1-C9 sections above them are never touched).

    python3 Tools/Build/sound_slots.py                   # a table per owner: field, expectation, event, loops?, banked?
    python3 Tools/Build/sound_slots.py --owners          # the owners with their slot counts
    python3 Tools/Build/sound_slots.py --empties [owner] # the empty slots, all or one owner's
    python3 Tools/Build/sound_slots.py --json
    python3 Tools/Build/sound_slots.py --check           # exit 1 on a mis-wired slot, an unbanked event or a bank gap
    python3 Tools/Build/sound_slots.py --charles         # rewrite the generated block of CHARLES_TASKS.md
    python3 Tools/Build/sound_slots.py --self-test

A SLOT is one serialised FMOD `EventReference`. They are found in three places, and the counts keep them apart:
  blocks       the YAML block `<field>: {Guid: {Data1..Data4}, Path: event:/...}` in a .prefab, .asset or .unity
  overrides    a scene's (or variant's) `m_Modifications` entry `<field>.Path` / `<field>.Guid.DataN` on a prefab
               instance. The Bootstrap scene wires the AudioSystem this way, and a scene override always beats the
               prefab (CLAUDE.md, shared prefabs), so an override is folded into the prefab's slot as its value and
               the slot says which scene set it.
  unserialized a field the component's script declares (walking its base classes) that the asset has no block for
               yet: Unity only writes a field when the asset is saved after the field exists, so such a slot is
               EMPTY and invisible to grep (the AudioSystem prefab has zero blocks for its ~50 fields)

The EXPECTATION is read from the code that uses the field, never from its name:
  one-shot   PlaySFXEvent / PlaySFXEventAttached / PlaySFXOneShot(Attached) / RuntimeManager.PlayOneShot(Attached)
  loop       FmodSafe.TryCreateInstance / RuntimeManager.CreateInstance + start(), and that file stops it
             (StopAndRelease / stop()): a sustained or looping bed, usually with a parameter driven every frame
  instance   created and started but never stopped by that file (reported, never failed)
  held       AudioSystem.StartHeldGameplaySFX: owns the instance when the event loops, plays it as a one-shot
             when it does not (FMODOneShotVolumeHelper.IsFireAndForgetSafe) - accepts either kind
  emitter    a StudioEventEmitter's EventReference (its play/stop triggers are read from the same component), or
             a config event the code stamps onto an emitter (`emitter.EventReference = config.X`)
  unknown    the field is never read, or is read in a way the tool does not follow
How a field is followed: statements are joined across line breaks (a call or a `switch` expression that spans
lines is one statement); a local helper is followed inside its file and its partial-class siblings
(PlayOneShot(evt) => audio.PlaySFXEvent(evt)); a local alias is followed (var sound = ...GrowEvent...;
PlaySound(sound)); a config SO's field is followed one hop through its public accessor (config.FormEvent) into the
files that read it; a field assigned into another class's field (TransitEvent = gateThreadEvent) takes that
field's expectation; an AudioSystem category field ({GameplaySFXCategory.X, xEvent}) is followed through the enum
member to the methods the real call sites hand it to (PlayGameplaySFX -> one-shot; HeldVesselSfx.Begin ->
StartHeldGameplaySFX -> held); a member with no literal call site (fired through a serialized or computed
category) takes what every method accepting that enum does, one-shot when they disagree, and says so.

`--check` fails on (every failure printed as asset:field):
  C2   a looping event in a one-shot slot (FMODOneShotVolumeHelper refuses it at runtime: the slot is silent and
       logs an error; fired raw it would leak one immortal voice per press)
  C4   a non-looping event in a loop slot (the bed plays once and the parameter drives nothing)
  emitter-loop   a looping event on a StudioEventEmitter with no stop trigger (FMOD's OnDestroy only DETACHES a
       looping instance: it plays at its last position forever)
  unbanked   a filled slot whose event is not in Build/GUIDs.txt: authored in the project but not built into the
       banks yet (rebuild them)
  unknown-event   a filled slot whose GUID and Path match no authored event (renamed or deleted in Studio: a stale
       reference)
  no banks for <platform>   ProjectSettings / the build profiles target Android or iOS and the FMOD project
       builds banks for no mobile platform (only Build/Desktop exists)
A non-looping event in an `instance` slot is NOT a failure (unknown), and `held` accepts either kind.

Exit 0; 1 when --check fails; 2 on a setup error.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import sys
import tempfile
from dataclasses import dataclass, field as dc_field

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

BEGIN = "<!-- sound-slots:begin -->"
END = "<!-- sound-slots:end -->"
ASSET_EXTS = (".prefab", ".asset", ".unity")

ONE_SHOT_API = {
    "PlaySFXEvent", "PlaySFXEventAttached", "PlaySFXOneShot", "PlaySFXOneShotAttached",
    "PlayOneShot", "PlayOneShotAttached",
}
INSTANCE_API = {"TryCreateInstance", "CreateInstance"}
EITHER_MARK = "IsFireAndForgetSafe"  # a body that branches on it owns loops and fires stings: accepts either
STOP_MARKS = ("StopAndRelease(", ".stop(")
CATEGORY_TYPES = ("GameplaySFXCategory", "MenuAudioCategory")
NOT_CALLS = {"if", "while", "for", "foreach", "switch", "return", "catch", "using", "lock", "else", "new", "nameof", "typeof"}
SKIP_CALLS = {"IsNull", EITHER_MARK, "IsValid", "isValid", "Equals", "ToString", "Add"}
ONE_SHOT_EXPECTATIONS = ("one-shot", "one-shot+held")
KIND_OF_EXPECTATION = {"one-shot": "one-shot", "one-shot+held": "one-shot", "held": "held", "loop": "loop",
                       "instance": "instance", "emitter": "emitter"}

EMITTER_EVENTS = [
    "None", "ObjectStart", "ObjectDestroy", "TriggerEnter", "TriggerExit", "TriggerEnter2D", "TriggerExit2D",
    "CollisionEnter", "CollisionExit", "CollisionEnter2D", "CollisionExit2D", "ObjectEnable", "ObjectDisable",
    "ObjectMouseEnter", "ObjectMouseExit", "ObjectMouseDown", "ObjectMouseUp", "UIMouseEnter", "UIMouseExit",
]
# Unity BuildTarget ids -> the FMOD platform family that must have banks.
BUILD_TARGETS = {2: "Desktop", 5: "Desktop", 19: "Desktop", 17: "Desktop", 24: "Desktop", 9: "iOS", 13: "Android", 20: "WebGL"}
APP_ID_PLATFORMS = {"Standalone": "Desktop", "Android": "Android", "iPhone": "iOS", "WebGL": "WebGL"}
# The FMOD Studio build sub-directories that serve each platform family.
BANK_DIRS = {"Desktop": ("Desktop",), "Android": ("Android", "Mobile"), "iOS": ("iOS", "Mobile"), "WebGL": ("HTML5", "WebGL", "Desktop")}

# Owners. A hull owns its prefab under Spacevessels/ (sub-folders included) and its VesselActions/<Hull>/ configs;
# these aliases cover things that belong to a hull without carrying its name.
OWNER_ALIASES = {"MorningStar": "Thresher", "OrcaCrew": "Orca", "GobyPistol": "Goby", "Butterfly": "TigerMoth"}
SCRIPT_OWNERS = [
    ("AudioSystem", "AudioSystem"), ("SoundtrackSO", "Music"), ("CallsignSetSO", "Pilots"), ("ArtifactSO", "Artifacts"),
    ("PayloadConfigSO", "Payload"), ("ArkReactorSO", "Toys"), ("FaunaConfigurationSO", "Fauna"),
    ("SwarmFaunaConfigSO", "Fauna"), ("BeeFauna", "Fauna"), ("BeeHiveFlora", "Flora"), ("IconEmitter", "UI"),
    ("ProfileModal", "UI"), ("CountdownTimer", "UI"), ("WormholeMouth", "Environment"), ("GateRaceController", "Modes"),
]
PATH_OWNERS = [
    ("/FloraAndFauna/", "Fauna"), ("/_Models/Fauna/", "Fauna"), ("/Lifeforms/", "Fauna"), ("/Swarm Fauna/", "Fauna"),
    ("Fauna Config", "Fauna"), ("/Environment/", "Environment"), ("/UI Elements/", "UI"), ("Menu_Main.unity", "UI"),
    ("/Pilots/", "Pilots"), ("/Audio/", "Music"), ("/Toys/", "Toys"),
]


# ----------------------------------------------------------------------------------------------- small helpers

def read_text(path: str) -> str:
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def load_guid_map(assets_dir: str) -> dict[str, str]:
    """Unity meta guid -> the asset's path, for every .meta under Assets (Library skipped)."""
    out: dict[str, str] = {}
    for dirpath, dirnames, filenames in os.walk(assets_dir):
        dirnames[:] = [d for d in dirnames if d != "Library"]
        for fn in filenames:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(dirpath, fn)
            try:
                with open(p, encoding="utf-8", errors="replace") as fh:
                    head = fh.read(400)
            except OSError:
                continue
            m = re.search(r"^guid: ([0-9a-f]{32})", head, re.M)
            if m:
                out[m.group(1)] = p[:-5]
    return out


def guid_to_data(guid: str) -> tuple[int, int, int, int]:
    """FMOD's GUID struct (four ints, as Unity serialises Data1..Data4) from its {8-4-4-4-12} text."""
    g = guid.strip("{}").replace("-", "")
    if len(g) != 32:
        raise ValueError(f"not a GUID: {guid}")

    def s32(v: int) -> int:
        return v - (1 << 32) if v >= (1 << 31) else v

    d1 = s32(int(g[0:8], 16))
    d2 = s32((int(g[12:16], 16) << 16) | int(g[8:12], 16))
    tail = bytes.fromhex(g[16:32])
    d3 = int.from_bytes(tail[0:4], "little", signed=True)
    d4 = int.from_bytes(tail[4:8], "little", signed=True)
    return d1, d2, d3, d4


MASK_RE = re.compile(
    r'//[^\n]*|/\*.*?\*/|\$?@"(?:[^"]|"")*"|@\$"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n]){1,2}\'', re.S)


def mask_code(text: str) -> str:
    """Comments and string contents replaced by spaces (same length), so braces and names can be counted."""
    def repl(m: re.Match) -> str:
        s = m.group(0)
        if s.startswith("/"):
            return re.sub(r"[^\n]", " ", s)
        return s[0] + re.sub(r"[^\n]", " ", s[1:-1]) + s[-1]
    return MASK_RE.sub(repl, text)


def calls_with_arg(stmt: str, names: list[str]) -> list[str]:
    """Names of the calls in `stmt` whose argument list mentions one of `names` as a whole word."""
    out = []
    pats = [re.compile(r"\b" + re.escape(n) + r"\b") for n in names]
    for m in re.finditer(r"(\w+)\s*\(", stmt):
        callee = m.group(1)
        if callee in NOT_CALLS:
            continue
        i, depth = m.end(), 1
        while i < len(stmt) and depth:
            depth += stmt[i] == "("
            depth -= stmt[i] == ")"
            i += 1
        args = stmt[m.end():i - 1]
        if any(p.search(args) for p in pats):
            out.append(callee)
    return out


def join_statements(mlines: list[str]) -> list[tuple[int, str]]:
    """(start line index, text) per statement, joining lines while a parenthesis or a `switch {` is open."""
    out: list[tuple[int, str]] = []
    i, n = 0, len(mlines)
    while i < n:
        start = i
        buf = mlines[i]
        paren = buf.count("(") - buf.count(")")
        brace = buf.count("{") - buf.count("}") if re.search(r"\bswitch\b", buf) and buf.rstrip().endswith("{") else 0
        # a switch EXPRESSION whose brace opens on the next line ("var s = k switch" / "{" / ... / "};")
        expect = bool(re.search(r"\bswitch\s*$", buf.rstrip()))
        j = i
        while (paren > 0 or brace > 0 or expect) and j + 1 < n and j - start < 60:
            j += 1
            nxt = mlines[j]
            buf += " " + nxt.strip()
            paren += nxt.count("(") - nxt.count(")")
            if expect:
                if "{" in nxt:
                    brace = nxt.count("{") - nxt.count("}")
                    expect = False
                elif nxt.strip():
                    expect = False
            elif brace > 0:
                brace += nxt.count("{") - nxt.count("}")
            if brace <= 0 and re.search(r"\bswitch\b", nxt) and nxt.rstrip().endswith("{") and paren <= 0:
                brace = nxt.count("{") - nxt.count("}")
        out.append((start, buf))
        i = j + 1
    return out


# ----------------------------------------------------------------------------------------------- the C# side

@dataclass
class CsField:
    name: str
    cls: str
    file: str
    line: int
    serialized: bool
    is_array: bool
    former: list[str]
    public: bool


@dataclass
class CsClass:
    name: str
    base: str
    file: str
    fields: list[CsField] = dc_field(default_factory=list)
    accessors: dict[str, str] = dc_field(default_factory=dict)  # accessor name -> field name


@dataclass
class CsFile:
    path: str
    rel: str
    text: str
    masked: str
    lines: list[str]
    mlines: list[str]
    classes: list[CsClass]
    stmts: list[tuple[int, str]]
    tokens: set[str]
    members: set[str]
    main: str


CLASS_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|abstract|sealed|partial|readonly|new|unsafe)\s+)*"
    r"(?:class|struct)\s+(\w+)(?:\s*<[^>]*>)?(?:\s*:\s*([^{]+?))?\s*(?:\{|$|where\b)"
)
FIELD_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*((?:(?:public|private|protected|internal|static|readonly|new)\s+)*)"
    r"(?:FMODUnity\.)?EventReference(\s*\[\s*\])?\s+(\w+)\s*(?:;|=(?!>))"
)
ACCESSOR_RE = re.compile(
    r"(?:FMODUnity\.)?EventReference\s+(\w+)\s*(?:=>\s*(\w+)\s*;|\{\s*get\s*(?:=>|\{\s*return)\s*(\w+)\s*;)"
)
FORMER_RE = re.compile(r'FormerlySerializedAs\("(\w+)"\)')
METHOD_RE_TMPL = (r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|virtual|override|async|new|unsafe|extern)\s+)*"
                  r"[\w<>\[\],.?]+\s+{name}\s*\(([^)]*)\)")
SIG_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|virtual|override|async|new)\s+)*"
                    r"[\w<>\[\],.?]+\s+(\w+)\s*\(([^)]*)\)")


def parse_cs(path: str, rel: str) -> CsFile:
    text = read_text(path)
    masked = mask_code(text)
    lines = text.split("\n")
    mlines = masked.split("\n")
    classes: list[CsClass] = []
    stack: list[list] = []  # [class, declaration depth, entered its body yet]
    depth = 0
    for i, ml in enumerate(mlines):
        cm = CLASS_RE.match(ml)
        if cm:
            cls = CsClass(cm.group(1), (cm.group(2) or "").strip(), rel)
            classes.append(cls)
            stack.append([cls, depth, False])
        elif stack and depth == stack[-1][1] + 1:
            cur = stack[-1][0]
            fm = FIELD_RE.match(ml)
            if fm and "(" not in ml[fm.start(1): fm.start(3)]:
                mods = fm.group(1).split()
                attrs = ml[: fm.start(1)]
                j = i - 1
                while j >= 0 and j >= i - 4 and mlines[j].strip().startswith("["):
                    attrs = mlines[j] + attrs
                    j -= 1
                real_attrs = "\n".join(lines[max(0, j + 1): i + 1])  # FormerlySerializedAs lives in a string
                static = "static" in mods
                public = "public" in mods
                serialized = (not static) and "NonSerialized" not in attrs and ("SerializeField" in attrs or public)
                cur.fields.append(CsField(fm.group(3), cur.name, rel, i + 1, serialized, bool(fm.group(2)),
                                          FORMER_RE.findall(real_attrs), public))
            am = ACCESSOR_RE.search(ml)
            if am:
                cur.accessors[am.group(1)] = am.group(2) or am.group(3)
        depth += ml.count("{") - ml.count("}")
        if stack and depth > stack[-1][1]:
            stack[-1][2] = True
        while stack and stack[-1][2] and depth <= stack[-1][1]:
            stack.pop()
    stmts = join_statements(mlines)
    tokens = set(re.findall(r"\.(\w+)", masked))
    members = {f"{t}.{m}" for t, m in re.findall(r"\b(GameplaySFXCategory|MenuAudioCategory)\.(\w+)", masked)}
    main = os.path.basename(rel).split(".")[0]
    return CsFile(path, rel, text, masked, lines, mlines, classes, stmts, tokens, members, main)


class Code:
    """The runtime C# corpus: classes, their EventReference fields, and how each field is played."""

    def __init__(self, scripts_dir: str, root: str):
        self.files: dict[str, CsFile] = {}
        self.classes: dict[str, CsClass] = {}
        self.partials: dict[str, list[str]] = {}
        self.files_by_token: dict[str, list[str]] = {}
        self.files_by_member: dict[str, list[str]] = {}
        self.fields_by_name: dict[str, list[CsField]] = {}
        for dirpath, dirnames, filenames in os.walk(scripts_dir):
            dirnames[:] = sorted(d for d in dirnames if d not in ("Editor", "Tests"))
            for fn in sorted(filenames):
                if not fn.endswith(".cs"):
                    continue
                p = os.path.join(dirpath, fn)
                rel = os.path.relpath(p, root).replace(os.sep, "/")
                cf = parse_cs(p, rel)
                self.files[rel] = cf
                for c in cf.classes:
                    self.classes.setdefault(c.name, c)
                    self.partials.setdefault(c.name, []).append(rel)
                    for f in c.fields:
                        self.fields_by_name.setdefault(f.name, []).append(f)
                for t in cf.tokens:
                    self.files_by_token.setdefault(t, []).append(rel)
                for m in cf.members:
                    self.files_by_member.setdefault(m, []).append(rel)
        self._sinks: dict[str, str] | None = None
        self._sink_types: dict[str, str] = {}
        self._sites: dict[str, tuple[set[str], list[str], str]] = {}
        self._mcache: dict[tuple[str, str], tuple[str | None, list[tuple[int, str]]] | None] = {}
        self._cache: dict[tuple[str, str], tuple[str, str, list[str]]] = {}
        self._busy: set[tuple[str, str]] = set()

    # -- fields -------------------------------------------------------------------------------------------------

    def main_class(self, rel: str) -> CsClass | None:
        cf = self.files.get(rel)
        if not cf or not cf.classes:
            return None
        for c in cf.classes:
            if c.name == cf.main:
                return c
        return cf.classes[0]

    def base_chain(self, cls: CsClass | None) -> list[CsClass]:
        out: list[CsClass] = []
        seen: set[str] = set()
        while cls is not None and cls.name not in seen:
            seen.add(cls.name)
            out.append(cls)
            nxt = None
            for base in [b.strip() for b in cls.base.split(",")]:
                base = re.sub(r"<.*", "", base).split(".")[-1]
                if base in self.classes and base not in seen:
                    nxt = self.classes[base]
                    break
            cls = nxt
        return out

    def serialized_fields(self, cls: CsClass | None) -> list[CsField]:
        """The class's own serialised EventReference fields plus its base classes' (partial files included)."""
        out: list[CsField] = []
        for c in self.base_chain(cls):
            for rel in self.partials.get(c.name, []):
                for cc in self.files[rel].classes:
                    if cc.name == c.name:
                        out.extend(f for f in cc.fields if f.serialized and f not in out)
        return out

    def field_in(self, rel: str, name: str) -> CsField | None:
        """The declared field a YAML key names in this script (own, base or nested class; former names too)."""
        cf = self.files.get(rel)
        if not cf:
            return None
        for c in cf.classes:
            for f in c.fields:
                if f.name == name or name in f.former:
                    return f
        for f in self.serialized_fields(self.main_class(rel)):
            if f.name == name or name in f.former:
                return f
        return None

    # -- methods, sinks ------------------------------------------------------------------------------------------

    def _method(self, cf: CsFile, name: str, siblings: bool = True):
        """(its EventReference / category parameter name, its body statements) for a method declared in `cf`,
        or in a partial-class sibling of `cf`'s main class."""
        key = (cf.rel, name)
        if key in self._mcache:
            return self._mcache[key]
        pat = re.compile(METHOD_RE_TMPL.format(name=re.escape(name)))
        found = None
        for idx, st in cf.stmts:
            m = pat.match(st)
            if not m:
                continue
            pm = re.search(r"(?:FMODUnity\.)?(?:EventReference|GameplaySFXCategory|MenuAudioCategory)\s+(\w+)", m.group(1))
            pname = pm.group(1) if pm else None
            rest = st[m.end():]
            if "=>" in rest and "{" not in rest:
                found = (pname, [(idx, st)])
                break
            depth, started, end = 0, False, idx
            for k in range(idx, len(cf.mlines)):
                depth += cf.mlines[k].count("{") - cf.mlines[k].count("}")
                if "{" in cf.mlines[k]:
                    started = True
                if started and depth <= 0:
                    end = k
                    break
            found = (pname, [s for s in cf.stmts if idx <= s[0] <= end])
            break
        if found is None and siblings:
            for rel in self.partials.get(cf.main, []):
                if rel != cf.rel:
                    got = self._method(self.files[rel], name, siblings=False)
                    if got:
                        found = got
                        break
        self._mcache[key] = found
        return found

    def sinks(self) -> dict[str, str]:
        """Methods that take a GameplaySFXCategory / MenuAudioCategory and play its event: name -> kind."""
        if self._sinks is not None:
            return self._sinks
        sinks: dict[str, str] = {}
        candidates: list[tuple[CsFile, str, str, str]] = []
        for cf in self.files.values():
            if not any(t in cf.masked for t in CATEGORY_TYPES):
                continue
            for _, st in cf.stmts:
                m = SIG_RE.match(st)
                if m and any(t in m.group(2) for t in CATEGORY_TYPES):
                    pm = re.search(r"(GameplaySFXCategory|MenuAudioCategory)\s+(\w+)", m.group(2))
                    if pm:
                        candidates.append((cf, m.group(1), pm.group(2), pm.group(1)))
        for cf, name, param, ctype in candidates:  # level 0: the body plays the event itself
            got = self._method(cf, name)
            if not got:
                continue
            body = "\n".join(s for _, s in got[1])
            if any(re.search(r"\b%s\s*\(" % api, body) for api in ONE_SHOT_API):
                sinks[name] = "one-shot"
                self._sink_types[name] = ctype
            if any(re.search(r"\b%s\s*\(" % api, body) for api in INSTANCE_API):
                sinks[name] = "held" if EITHER_MARK in body else ("loop" if any(s in cf.text for s in STOP_MARKS) else "instance")
                self._sink_types[name] = ctype
        for _ in range(2):  # wrappers: a method that hands its category to a sink
            for cf, name, param, ctype in candidates:
                if name in sinks:
                    continue
                got = self._method(cf, name)
                if not got:
                    continue
                for _, st in got[1]:
                    for callee in calls_with_arg(st, [param]):
                        if callee in sinks:
                            sinks[name] = sinks[callee]
                            self._sink_types[name] = ctype
        self._sinks = sinks
        return sinks

    def category_kinds(self, member: str) -> tuple[set[str], list[str], str]:
        """What the real call sites do with `GameplaySFXCategory.X`: (kinds, 'Class.Method' sites, note)."""
        if member in self._sites:
            return self._sites[member]
        sinks = self.sinks()
        ctype = member.split(".")[0]
        kinds: set[str] = set()
        sites: list[str] = []
        weak: list[str] = []
        note = ""
        pat = re.compile(r"\b" + re.escape(member) + r"\b")
        for rel in self.files_by_member.get(member, []):
            cf = self.files[rel]
            hit = False
            for _, st in cf.stmts:
                if not pat.search(st):
                    continue
                for callee in calls_with_arg(st, [member]):
                    if callee in sinks:
                        kinds.add(sinks[callee])
                        sites.append(f"{cf.main}.{callee}")
                        hit = True
            if not hit and cf.main != "AudioSystem":
                used = sorted({sinks[s] for s in sinks if re.search(r"\b%s\s*\(" % re.escape(s), cf.masked)})
                if used:
                    weak.append(f"{cf.main} via a variable ({'/'.join(used)})")
                    kinds_weak = set(used)
                    if not kinds:
                        kinds |= kinds_weak
        if weak and not sites:
            sites = weak
            note = "no literal call site: fired through a variable"
        if not kinds:
            kinds = {sinks[s] for s in sinks if self._sink_types.get(s) == ctype}
            note = ("no literal call site: fired through a serialized or computed category; every " +
                    f"{ctype} method is " + "/".join(sorted(kinds)))
        result = (kinds, sites, note)
        self._sites[member] = result
        return result

    # -- the expectation ----------------------------------------------------------------------------------------

    def _kinds_in(self, cf: CsFile, stmts: list[tuple[int, str]], names: list[str], depth: int, how: list[str],
                  notes: list[str]) -> set[str]:
        kinds: set[str] = set()
        name_pat = re.compile(r"\b(?:%s)\b" % "|".join(re.escape(n) for n in names))
        for idx, st in stmts:
            if not name_pat.search(st):
                continue
            if re.search(r"\.EventReference\s*=[^=]", st):
                kinds.add("emitter")
                how.append("stamped onto a StudioEventEmitter")
                continue
            for callee in calls_with_arg(st, names):
                if callee in INSTANCE_API:
                    window = "\n".join(cf.mlines[max(0, idx - 25): idx + 45])
                    kinds.add("held" if EITHER_MARK in window else ("loop" if any(s in cf.text for s in STOP_MARKS) else "instance"))
                    how.append(callee)
                elif callee in SKIP_CALLS:
                    continue
                else:
                    local = self._method(cf, callee) if depth < 4 else None
                    if local and local[0]:
                        sub = self._kinds_in(cf, local[1], [local[0]], depth + 1, how, notes)
                        if sub:
                            kinds |= sub
                            how.append(callee)
                            continue
                    if callee in ONE_SHOT_API:
                        kinds.add("one-shot")
                        how.append(callee)
            init = re.search(r"\{\s*(GameplaySFXCategory|MenuAudioCategory)\.(\w+)\s*,\s*(\w+)\s*\}", st)
            if init and init.group(3) in names:
                member = f"{init.group(1)}.{init.group(2)}"
                k, sites, note = self.category_kinds(member)
                kinds |= k
                how.append(f"{member}: " + ("; ".join(sorted(set(sites))[:4]) if sites else "never fired"))
                if note:
                    notes.append(note)
            am = re.search(r"\b(?:var|EventReference)\s+(\w+)\s*=", st)
            if am and depth < 4 and not FIELD_RE.match(st):
                follow = [s for s in cf.stmts if idx < s[0] <= idx + 80]
                sub = self._kinds_in(cf, follow, [am.group(1)], depth + 1, how, notes)
                if sub:
                    kinds |= sub
                    how.append(f"via {am.group(1)}")
            flow = re.search(r"(?:^|[\s{,(])(?:[\w.]+\.)?(\w+)\s*=\s*(?:[\w.]+\.)?(?:%s)\s*[,;)}]" % "|".join(re.escape(n) for n in names), st)
            if flow and flow.group(1) not in names and depth < 4:
                for target in self.fields_by_name.get(flow.group(1), []):
                    exp, h, _ = self.expectation(target)
                    if exp in KIND_OF_EXPECTATION:
                        kinds.add(KIND_OF_EXPECTATION[exp])
                        how.append(f"flows into {target.cls}.{target.name} ({h})")
        return kinds

    def expectation(self, f: CsField) -> tuple[str, str, list[str]]:
        """(expectation, how, notes) for a declared field, following helpers and accessors as the docstring says."""
        key = (f.file, f.name)
        if key in self._cache:
            return self._cache[key]
        if key in self._busy:
            return "unknown", "", []
        self._busy.add(key)
        cf = self.files[f.file]
        cls = next((c for c in cf.classes if c.name == f.cls), None)
        accessors = [a for a, fld in (cls.accessors.items() if cls else []) if fld == f.name]
        names = [f.name] + f.former
        how: list[str] = []
        notes: list[str] = []
        kinds: set[str] = set()
        name_pat = re.compile(r"\b(?:%s)\b" % "|".join(re.escape(n) for n in names))
        own = [(i, st) for i, st in cf.stmts
               if i != f.line - 1 and name_pat.search(st) and not ACCESSOR_RE.search(st) and not FIELD_RE.match(st)]
        uses = len(own)
        kinds |= self._kinds_in(cf, own, names, 0, how, notes)
        hop = sorted(set(accessors + ([f.name] if f.public else [])))
        if hop:
            pat = re.compile(r"\.(%s)\b" % "|".join(re.escape(h) for h in hop))
            rels = sorted({rel for h in hop for rel in self.files_by_token.get(h, []) if rel != cf.rel})
            for rel in rels:
                other = self.files[rel]
                stmts = [(i, st) for i, st in other.stmts if pat.search(st)]
                uses += len(stmts)
                sub = self._kinds_in(other, stmts, hop, 1, how, notes)
                if sub:
                    kinds |= sub
                    how.append(other.main)
        if "emitter" in kinds:
            exp = "emitter"
        elif "held" in kinds and "one-shot" in kinds:
            exp = "one-shot+held"
            notes.append("fired as a one-shot at one site and held at another")
        elif "held" in kinds:
            exp = "held"
        elif "one-shot" in kinds:
            exp = "one-shot"
        elif "loop" in kinds:
            exp = "loop"
        elif "instance" in kinds:
            exp = "instance"
        else:
            exp = "unknown"
            notes.append("field never read" if uses == 0 else "read in a way the tool does not follow")
        seen: list[str] = []
        for h in how:
            if h not in seen:
                seen.append(h)
        result = (exp, " -> ".join(seen[:6]), notes)
        self._cache[key] = result
        self._busy.discard(key)
        return result


# ----------------------------------------------------------------------------------------------- the FMOD side

@dataclass
class FmodEvent:
    guid: str
    name: str
    path: str
    loops: bool
    loop_how: str
    params: list[str]
    banks: list[str]
    nested: list[str]


class Fmod:
    """The FMOD Studio project's metadata and its built banks."""

    def __init__(self, project_dir: str):
        self.dir = project_dir
        meta = os.path.join(project_dir, "Metadata")
        self.ok = os.path.isdir(meta)
        self.events: dict[str, FmodEvent] = {}
        self.by_data: dict[tuple[int, int, int, int], FmodEvent] = {}
        self.by_path: dict[str, FmodEvent] = {}
        self.banked: dict[str, str] = {}  # event guid -> path, from Build/GUIDs.txt
        self.banked_paths: set[str] = set()
        self.platforms: list[tuple[str, str]] = []  # (name, subDirectory)
        self.bank_dirs: list[str] = []
        self.banks: dict[str, str] = {}
        self.params: dict[str, str] = {}
        if not self.ok:
            return
        folders: dict[str, tuple[str, str | None, str]] = {}
        for fn in self._xmls(meta, "EventFolder"):
            t = read_text(fn)
            m = re.search(r'<object class="(MasterEventFolder|EventFolder)" id="\{([^}]+)\}">(.*?)</object>', t, re.S)
            if not m:
                continue
            nm = re.search(r'<property name="name">\s*<value>([^<]*)</value>', m.group(3))
            parent = re.search(r'<relationship name="folder">\s*<destination>\{([^}]+)\}', m.group(3))
            folders[m.group(2)] = (nm.group(1) if nm else "", parent.group(1) if parent else None, m.group(1))

        def folder_path(fid: str | None) -> str:
            parts: list[str] = []
            while fid and fid in folders:
                name, parent, cls = folders[fid]
                if cls == "MasterEventFolder":
                    break
                parts.append(name)
                fid = parent
            return "/".join(reversed(parts))

        for sub, store in (("ParameterPreset", self.params), ("Bank", self.banks)):
            for fn in self._xmls(meta, sub):
                m = re.search(r'id="\{([^}]+)\}">.*?<property name="name">\s*<value>([^<]*)</value>', read_text(fn), re.S)
                if m:
                    store[m.group(1)] = m.group(2)
        for fn in self._xmls(meta, "Platform"):
            t = read_text(fn)
            nm = re.search(r'<property name="name">\s*<value>([^<]*)</value>', t)
            sd = re.search(r'<property name="subDirectory">\s*<value>([^<]*)</value>', t)
            if nm:
                self.platforms.append((nm.group(1), sd.group(1) if sd else nm.group(1)))
        for fn in self._xmls(meta, "Event"):
            t = read_text(fn)
            ev = re.search(r'<object class="Event" id="\{([^}]+)\}">(.*?)</object>', t, re.S)
            if not ev:
                continue
            body = ev.group(2)
            nm = re.search(r'<property name="name">\s*<value>([^<]*)</value>', body)
            fol = re.search(r'<relationship name="folder">\s*<destination>\{([^}]+)\}', body)
            name = nm.group(1) if nm else ev.group(1)
            path = "event:/" + "/".join(x for x in (folder_path(fol.group(1) if fol else None), name) if x)
            hows = []
            if re.search(r'<property name="looping">\s*<value>true</value>', t):
                hows.append("a looping instrument")
            if 'class="LoopRegion"' in t:
                hows.append("a loop region")
            if 'class="ScattererSound"' in t:
                hows.append("a scatterer")
            params = [self.params.get(g, g) for g in
                      re.findall(r'<object class="ParameterProxy".*?<relationship name="preset">\s*<destination>\{([^}]+)\}', t, re.S)]
            banks = re.search(r'<relationship name="banks">(.*?)</relationship>', body, re.S)
            bank_names = [self.banks.get(b, b) for b in re.findall(r"\{([^}]+)\}", banks.group(1))] if banks else []
            nested = re.findall(r'<object class="EventSound".*?<relationship name="event">\s*<destination>\{([^}]+)\}', t, re.S)
            e = FmodEvent(ev.group(1), name, path, bool(hows), ", ".join(hows), params, bank_names, nested)
            self.events[e.guid] = e
        for e in self.events.values():  # a nested looping event keeps its parent alive
            if e.loops:
                continue
            stack, seen = list(e.nested), set()
            while stack:
                g = stack.pop()
                if g in seen:
                    continue
                seen.add(g)
                n = self.events.get(g)
                if n and n.loops:
                    e.loops, e.loop_how = True, f"nested event '{n.name}' loops"
                    break
                if n:
                    stack.extend(n.nested)
        for e in self.events.values():
            self.by_data[guid_to_data(e.guid)] = e
            self.by_path[e.path] = e
        guids = os.path.join(project_dir, "Build", "GUIDs.txt")
        if os.path.exists(guids):
            for line in read_text(guids).splitlines():
                if " event:/" in line:
                    g, p = line.strip().split(" ", 1)
                    self.banked[g.strip("{}")] = p
                    self.banked_paths.add(p)
        build = os.path.join(project_dir, "Build")
        if os.path.isdir(build):
            for d in sorted(os.listdir(build)):
                full = os.path.join(build, d)
                if os.path.isdir(full) and any(f.endswith(".bank") for f in os.listdir(full)):
                    self.bank_dirs.append(d)

    @staticmethod
    def _xmls(meta: str, sub: str) -> list[str]:
        d = os.path.join(meta, sub)
        if not os.path.isdir(d):
            return []
        return [os.path.join(d, fn) for fn in sorted(os.listdir(d)) if fn.endswith(".xml")]

    def resolve(self, data: tuple[int, int, int, int], path: str) -> tuple[FmodEvent | None, list[str]]:
        notes: list[str] = []
        if any(data):
            e = self.by_data.get(data)
            if e:
                if path and path != e.path:
                    notes.append(f"the serialised Path text says '{path}' (stale text; the GUID wins)")
                return e, notes
            e = self.by_path.get(path)
            if e:
                notes.append("the GUID matches no authored event; resolved by Path (re-pick the event in the inspector)")
                return e, notes
            return None, notes
        if path:
            e = self.by_path.get(path)
            if e:
                notes.append("zero GUID: resolved by Path")
                return e, notes
        return None, notes

    def is_banked(self, event: FmodEvent | None, path: str) -> bool:
        if event is not None:
            return event.guid in self.banked
        return path in self.banked_paths


# ----------------------------------------------------------------------------------------------- the assets side

@dataclass
class Slot:
    asset: str
    field: str
    script: str
    cls: str
    object_name: str
    path: str
    data: tuple[int, int, int, int]
    kind: str  # block | override | unserialized
    owner: str = ""
    source_prefab: str = ""
    override_from: str = ""
    expectation: str = "unknown"
    how: str = ""
    emitter_play: str = ""
    emitter_stop: str = ""
    event: FmodEvent | None = None
    banked: bool | None = None
    notes: list[str] = dc_field(default_factory=list)

    @property
    def filled(self) -> bool:
        return bool(self.path) or any(self.data)

    @property
    def label(self) -> str:
        return f"{self.asset}:{self.field}"


DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[^\n]*\n", re.M)
KEY_RE = re.compile(r"^(\s*)(- )?([^:\s\-][^:]*?):(?: (.*))?$")
MOD_RE = re.compile(
    r"- target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}),\s*type: 3\}\s*\n\s*propertyPath: ([^\n]*)\n\s*value: ?([^\n]*)\n"
)
SCRIPT_RE = re.compile(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})")


def split_docs(text: str):
    """(class_id, file_id, stripped, body) for every YAML document of a Unity asset."""
    marks = list(DOC_RE.finditer(text))
    for i, m in enumerate(marks):
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        yield int(m.group(1)), m.group(2), bool(m.group(3)), text[m.end():end]


def find_blocks(body: str) -> list[tuple[str, str, tuple[int, int, int, int]]]:
    """(field path, Path, Guid data) for every FMOD reference block in a document body."""
    out = []
    stack: list[list] = []  # [indent, name, list counter]
    window: list[tuple[int, str, str] | None] = []
    for raw in body.split("\n"):
        m = KEY_RE.match(raw)
        if not m:
            window.append(None)
            continue
        indent = len(m.group(1))
        key, val = m.group(3), (m.group(4) or "")
        if m.group(2):  # a list item: its keys sit two deeper than the dash; the previous item ends here
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
        if key == "Path" and len(window) >= 5 and all(window[-5:]) and window[-5][1] == "Guid" and window[-5][0] == indent \
                and [w[1] for w in window[-4:]] == ["Data1", "Data2", "Data3", "Data4"]:
            try:
                data = tuple(int(w[2]) for w in window[-4:])
            except ValueError:
                data = (0, 0, 0, 0)
            path = ""
            for node in stack[1:]:
                path += node[1] if node[1].startswith("[") else (("." if path else "") + node[1])
            out.append((path, val.strip(), data))  # type: ignore[arg-type]
        stack.append([indent, key, 0])
        window.append((indent, key, val))
    return out


class Scan:
    """Every slot under Assets/, resolved to its script, its owner, its expectation and its event."""

    def __init__(self, root: str, code: Code, fmod: Fmod, guid_map: dict[str, str]):
        self.root = root
        self.code = code
        self.fmod = fmod
        self.guid_map = guid_map
        self.cs_by_guid = {g: os.path.relpath(p, root).replace(os.sep, "/") for g, p in guid_map.items() if p.endswith(".cs")}
        self.hulls = self._hulls()
        self.slots: list[Slot] = []
        self.override_count = 0
        self._docs_cache: dict[str, dict[str, tuple[int, str]]] = {}
        self._names_cache: dict[str, dict[str, str]] = {}
        self.emitter_scripts = {rel for rel in self.cs_by_guid.values() if rel.endswith("/StudioEventEmitter.cs")}
        self.declaring = {rel for rel, cf in code.files.items() if any(f.serialized for c in cf.classes for f in c.fields)}
        self.declaring |= {rel for rel in code.files if code.serialized_fields(code.main_class(rel))}

    def _hulls(self) -> list[str]:
        d = os.path.join(self.root, "Assets", "_Prefabs", "Spacevessels")
        names = [os.path.splitext(f)[0] for f in os.listdir(d) if f.endswith(".prefab")] if os.path.isdir(d) else []
        return sorted(set(names), key=len, reverse=True)

    def rel(self, p: str) -> str:
        return os.path.relpath(p, self.root).replace(os.sep, "/")

    def asset_files(self):
        assets = os.path.join(self.root, "Assets")
        for dirpath, dirnames, filenames in os.walk(assets):
            dirnames[:] = sorted(d for d in dirnames if d != "Library")
            for fn in sorted(filenames):
                if fn.endswith(ASSET_EXTS):
                    yield os.path.join(dirpath, fn)

    def run(self) -> "Scan":
        for p in self.asset_files():
            text = read_text(p)
            if "Guid:" not in text and "propertyPath:" not in text and not any(
                    self.cs_by_guid.get(g) in self.declaring for g in set(SCRIPT_RE.findall(text))):
                continue
            self._scan_file(p, text)
        self._merge_overrides()
        for s in self.slots:
            s.owner = self.owner_of(s)
            self._classify(s)
        self.slots.sort(key=lambda s: (s.owner.lower(), s.asset, s.field))
        return self

    def _names(self, rel: str, text: str | None = None) -> dict[str, str]:
        if rel not in self._names_cache:
            text = text if text is not None else read_text(os.path.join(self.root, rel))
            names = {}
            for cid, fid, stripped, body in split_docs(text):
                if cid == 1:
                    m = re.search(r"^\s*m_Name: (.*)$", body, re.M)
                    names[fid] = m.group(1).strip() if m else ""
            self._names_cache[rel] = names
        return self._names_cache[rel]

    def _scan_file(self, p: str, text: str) -> None:
        rel = self.rel(p)
        names = self._names(rel, text)
        for cid, fid, stripped, body in split_docs(text):
            if cid == 1001:
                self._scan_overrides(rel, body)
                continue
            if stripped:
                continue
            sm = SCRIPT_RE.search(body)
            script = self.cs_by_guid.get(sm.group(1), "") if sm else ""
            if not script and "Guid:" not in body:
                continue
            go = re.search(r"^\s*m_GameObject: \{fileID: (-?\d+)\}", body, re.M)
            nm = re.search(r"^\s*m_Name: (.*)$", body, re.M)
            obj = names.get(go.group(1), "") if go else (nm.group(1).strip() if nm else "")
            cls = os.path.splitext(os.path.basename(script))[0] if script else ""
            blocks = find_blocks(body) if "Guid:" in body else []
            seen_fields = set()
            for fieldpath, path, data in blocks:
                s = Slot(rel, fieldpath, script, cls, obj, path, data, "block")
                if script in self.emitter_scripts:
                    pm = re.search(r"^\s*EventPlayTrigger: (\d+)", body, re.M)
                    st = re.search(r"^\s*EventStopTrigger: (\d+)", body, re.M)
                    s.emitter_play = self._emitter_event(pm.group(1) if pm else "0")
                    s.emitter_stop = self._emitter_event(st.group(1) if st else "0")
                self.slots.append(s)
                seen_fields.add(fieldpath.split("[")[0].split(".")[0])
            if script in self.declaring and cid == 114:
                mc = self.code.main_class(script)
                for f in self.code.serialized_fields(mc):
                    if f.is_array or f.name in seen_fields or any(x in seen_fields for x in f.former):
                        continue
                    self.slots.append(Slot(rel, f.name, script, cls, obj, "", (0, 0, 0, 0), "unserialized"))

    @staticmethod
    def _emitter_event(v: str) -> str:
        try:
            return EMITTER_EVENTS[int(v)]
        except (ValueError, IndexError):
            return v

    def _prefab_docs(self, prefab_rel: str) -> dict[str, tuple[int, str]]:
        if prefab_rel not in self._docs_cache:
            docs = {}
            full = os.path.join(self.root, prefab_rel)
            if os.path.exists(full):
                for cid, fid, stripped, body in split_docs(read_text(full)):
                    docs[fid] = (cid, body)
            self._docs_cache[prefab_rel] = docs
        return self._docs_cache[prefab_rel]

    def _scan_overrides(self, rel: str, body: str) -> None:
        groups: dict[tuple[str, str, str], dict] = {}
        for m in MOD_RE.finditer(body):
            target, guid, prop, val = m.group(1), m.group(2), m.group(3).strip().strip("'\""), m.group(4).strip()
            pm = re.match(r"^(.*)\.(Path|Guid\.Data([1-4]))$", prop)
            if not pm:
                continue
            fld = pm.group(1).replace(".Array.data[", "[")
            g = groups.setdefault((target, guid, fld), {"path": None, "data": [0, 0, 0, 0]})
            if pm.group(2) == "Path":
                g["path"] = val
            else:
                try:
                    g["data"][int(pm.group(3)) - 1] = int(val)
                except ValueError:
                    pass
        for (target, guid, fld), g in groups.items():
            prefab = self.guid_map.get(guid, "")
            prefab_rel = self.rel(prefab) if prefab else ""
            script, obj = "", ""
            if prefab_rel:
                doc = self._prefab_docs(prefab_rel).get(target)
                if doc:
                    sm = SCRIPT_RE.search(doc[1])
                    script = self.cs_by_guid.get(sm.group(1), "") if sm else ""
                    go = re.search(r"^\s*m_GameObject: \{fileID: (-?\d+)\}", doc[1], re.M)
                    if go:
                        obj = self._names(prefab_rel).get(go.group(1), "")
            cls = os.path.splitext(os.path.basename(script))[0] if script else ""
            s = Slot(rel, fld, script, cls, obj, g["path"] or "", tuple(g["data"]), "override")  # type: ignore[arg-type]
            s.source_prefab = prefab_rel
            self.slots.append(s)
            self.override_count += 1

    def _merge_overrides(self) -> None:
        """A scene override becomes the prefab slot's value (a scene override always beats the prefab)."""
        by_key = {(s.asset, s.field): s for s in self.slots if s.kind != "override"}
        keep = []
        for s in self.slots:
            if s.kind != "override":
                keep.append(s)
                continue
            target = by_key.get((s.source_prefab, s.field))
            if target is None:
                keep.append(s)
                continue
            if target.override_from:
                target.notes.append(f"also overridden in {os.path.basename(s.asset)} ({s.path or 'empty'})")
                continue
            target.override_from = s.asset
            target.path, target.data = s.path, s.data
            target.notes.append(f"value set by a scene override in {os.path.basename(s.asset)}")
        self.slots = keep

    # -- owner ----------------------------------------------------------------------------------------------------

    def owner_of(self, s: Slot) -> str:
        asset = s.asset
        stem = os.path.splitext(os.path.basename(asset))[0]
        m = re.search(r"/Spacevessels/(?:[^/]+/)*([^/]+)\.prefab$", asset)
        if m:
            return self._alias_or_hull(m.group(1)) or m.group(1)
        m = re.search(r"/VesselActions/([^/]+)/", asset)
        if m:
            return self._alias_or_hull(m.group(1)) or m.group(1)
        m = re.search(r"/Minigame([A-Za-z]+?)(?:_Gameplay|Multiplayer)*\.unity$", asset)
        if m:
            return f"Mode {m.group(1)}"
        for key, owner in SCRIPT_OWNERS:
            if s.cls == key:
                return owner
        chain = {c.name for c in self.code.base_chain(self.code.main_class(s.script))} if s.script else set()
        for key, owner in SCRIPT_OWNERS:
            if key in chain:
                return owner
        for cand in (s.cls, stem):
            h = self._alias_or_hull(cand)
            if h:
                return h
        for key, owner in PATH_OWNERS:
            if key in asset:
                return owner
        parts = asset.split("/")
        return parts[2] if len(parts) > 3 else (parts[1] if len(parts) > 2 else "Assets")

    def _alias_or_hull(self, name: str) -> str:
        for alias, owner in OWNER_ALIASES.items():
            if name == alias or (name.startswith(alias) and not name[len(alias):][:1].islower()):
                return owner
        for h in self.hulls:
            if name == h or (name.startswith(h) and not name[len(h):][:1].islower()):
                return h
        return ""

    # -- expectation + event ----------------------------------------------------------------------------------------

    def _classify(self, s: Slot) -> None:
        if s.script in self.emitter_scripts:
            s.expectation = "emitter"
            s.how = f"StudioEventEmitter play={s.emitter_play or 'None'} stop={s.emitter_stop or 'None'}"
        elif s.script:
            leaf = re.sub(r"\[\d+\]", "", s.field).split(".")[-1]
            f = self.code.field_in(s.script, leaf)
            if f:
                s.expectation, s.how, notes = self.code.expectation(f)
                s.notes.extend(n for n in notes if n not in s.notes)
            else:
                s.notes.append(f"no EventReference field '{leaf}' declared in {s.cls}")
        else:
            s.notes.append("script not resolved")
        if s.filled:
            s.event, notes = self.fmod.resolve(s.data, s.path)
            s.notes.extend(notes)
            s.banked = self.fmod.is_banked(s.event, s.path)


# ----------------------------------------------------------------------------------------------- the checks

def platform_targets(root: str) -> dict[str, str]:
    """Platform family -> where it was listed (ProjectSettings application identifiers, build profiles)."""
    targets: dict[str, str] = {}
    ps = os.path.join(root, "ProjectSettings", "ProjectSettings.asset")
    if os.path.exists(ps):
        t = read_text(ps)
        m = re.search(r"^  applicationIdentifier:\n((?:    \w+: .*\n)+)", t, re.M)
        if m:
            for key in re.findall(r"^    (\w+): ", m.group(1), re.M):
                fam = APP_ID_PLATFORMS.get(key)
                if fam:
                    targets.setdefault(fam, f"ProjectSettings applicationIdentifier.{key}")
    bp = os.path.join(root, "Assets", "Settings", "Build Profiles")
    if os.path.isdir(bp):
        for fn in sorted(os.listdir(bp)):
            if fn.endswith(".asset"):
                m = re.search(r"^\s*m_BuildTarget: (\d+)", read_text(os.path.join(bp, fn)), re.M)
                if m and int(m.group(1)) in BUILD_TARGETS:
                    targets.setdefault(BUILD_TARGETS[int(m.group(1))], f"build profile {fn}")
    return targets


def bank_gaps(root: str, fmod: Fmod) -> list[str]:
    out = []
    for fam, where in sorted(platform_targets(root).items()):
        dirs = BANK_DIRS.get(fam, (fam,))
        if not any(d in fmod.bank_dirs for d in dirs):
            studio = ", ".join(n for n, _ in fmod.platforms) or "none"
            out.append(
                f"no banks for {fam} (listed in {where}; the FMOD project builds for: {studio}; bank folders built: "
                f"{', '.join(fmod.bank_dirs) or 'none'}. Add a {dirs[-1]} platform in FMOD Studio and build; until then "
                f"the Unity integration loads Build/Desktop on {fam}, because no BuildDirectory is set per platform)")
    return out


def failures(scan: Scan) -> list[tuple[Slot | None, str, str]]:
    """(slot or None, class, reason); the class is C2 / C4 / emitter-loop / unbanked / unknown-event / no-banks."""
    out: list[tuple[Slot | None, str, str]] = []
    for s in scan.slots:
        if not s.filled:
            continue
        where = f" (value set by {os.path.basename(s.override_from)})" if s.override_from else ""
        ev = s.event
        if ev is None:
            out.append((s, "unknown-event", f"'{s.path or s.data}' is not an event in the FMOD project (renamed or deleted in Studio; a stale reference){where}"))
            continue
        if not s.banked:
            out.append((s, "unbanked", f"'{ev.path}' is authored (bank: {', '.join(ev.banks) or 'none assigned'}) but not in the built banks (Build/GUIDs.txt): rebuild the banks{where}"))
        if s.expectation in ONE_SHOT_EXPECTATIONS and ev.loops:
            out.append((s, "C2", f"looping event '{ev.path}' ({ev.loop_how}) in a one-shot slot ({s.how}): refused at runtime, the slot is silent{where}"))
        if s.expectation == "loop" and not ev.loops:
            out.append((s, "C4", f"one-shot event '{ev.path}' in a loop slot ({s.how}): plays once, drives nothing{where}"))
        if s.expectation == "emitter" and ev.loops and s.emitter_play not in ("", "None") and s.emitter_stop in ("", "None"):
            out.append((s, "emitter-loop", f"looping event '{ev.path}' on an emitter with no stop trigger (play={s.emitter_play}): plays forever after the object dies"))
    for gap in bank_gaps(scan.root, scan.fmod):
        out.append((None, "no-banks", gap))
    return out


# ----------------------------------------------------------------------------------------------- output

def counts(scan: Scan) -> dict:
    slots = scan.slots
    blocks = [s for s in slots if s.kind == "block"]
    by_exp: dict[str, int] = {}
    for s in slots:
        by_exp[s.expectation] = by_exp.get(s.expectation, 0) + 1
    return {
        "slots": len(slots),
        "filled": sum(1 for s in slots if s.filled),
        "empty": sum(1 for s in slots if not s.filled),
        "blocks": {"total": len(blocks), "empty": sum(1 for s in blocks if not s.path and not any(s.data) and not s.override_from),
                   "filled_in_asset": sum(1 for s in blocks if (s.path or any(s.data)) and not s.override_from)},
        "overrides": {"total": scan.override_count, "merged": sum(1 for s in slots if s.override_from),
                      "filled": sum(1 for s in slots if s.override_from and s.filled)},
        "unserialized": sum(1 for s in slots if s.kind == "unserialized"),
        "by_expectation": dict(sorted(by_exp.items())),
        "events_in_use": len({s.event.path for s in slots if s.event}),
        "events_authored": len(scan.fmod.events),
        "events_looping": sum(1 for e in scan.fmod.events.values() if e.loops),
        "events_banked": len(scan.fmod.banked),
        "owners": len({s.owner for s in slots}),
    }


def owner_rows(scan: Scan) -> list[dict]:
    rows: dict[str, dict] = {}
    for s in scan.slots:
        r = rows.setdefault(s.owner, {"owner": s.owner, "slots": 0, "empty": 0, "filled": 0, "expectations": {}})
        r["slots"] += 1
        r["empty" if not s.filled else "filled"] += 1
        r["expectations"][s.expectation] = r["expectations"].get(s.expectation, 0) + 1
    return sorted(rows.values(), key=lambda r: (-r["empty"], r["owner"].lower()))


def short_asset(asset: str) -> str:
    return os.path.basename(asset)


def ev_cell(s: Slot) -> str:
    if not s.filled:
        return "EMPTY"
    if s.event:
        return s.event.path.replace("event:/", "")
    return (s.path or f"guid {s.data}").replace("event:/", "") + " (?)"


def slot_where(s: Slot) -> str:
    where = f"{short_asset(s.asset)}:{s.cls + '.' if s.cls else ''}{s.field}"
    if s.override_from:
        where += f" (set in {short_asset(s.override_from)})"
    elif s.kind == "override":
        where += f" (override of {short_asset(s.source_prefab) or '?'})"
    elif s.kind == "unserialized":
        where += " (not saved in the asset yet)"
    return where


def render_table(scan: Scan) -> str:
    c = counts(scan)
    out = [
        f"Sound slots: {c['slots']} ({c['empty']} empty, {c['filled']} filled) = {c['blocks']['total']} YAML blocks "
        f"({c['blocks']['empty']} empty) + {c['unserialized']} declared-but-unserialized (empty) ; "
        f"{c['overrides']['total']} scene overrides folded in ({c['overrides']['filled']} of them fill a slot)",
        "  expectation: " + "  ".join(f"{k} {v}" for k, v in c["by_expectation"].items()),
        f"  FMOD: {c['events_authored']} events authored ({c['events_looping']} loop), {c['events_banked']} in the built banks, "
        f"{c['events_in_use']} in use; bank folders: {', '.join(scan.fmod.bank_dirs) or 'none'}",
    ]
    cur = None
    for s in scan.slots:
        if s.owner != cur:
            cur = s.owner
            n = sum(1 for x in scan.slots if x.owner == cur)
            e = sum(1 for x in scan.slots if x.owner == cur and not x.filled)
            out.append(f"\n{cur}  ({n} slots, {e} empty)")
            out.append(f"  {'field':<62} {'expects':<13} {'event':<44} {'loops':<6} {'banked':<7} notes")
        loops = "-" if not s.filled else ("yes" if s.event and s.event.loops else ("no" if s.event else "?"))
        banked = "-" if not s.filled else ("yes" if s.banked else "NO")
        notes = "; ".join(s.notes)
        if s.expectation == "emitter" and s.emitter_play:
            notes = (f"play={s.emitter_play} stop={s.emitter_stop}; " + notes).rstrip("; ")
        out.append(f"  {slot_where(s):<62} {s.expectation:<13} {ev_cell(s):<44} {loops:<6} {banked:<7} {notes}")
    return "\n".join(out)


def render_owners(scan: Scan) -> str:
    out = [f"{'owner':<22} {'slots':>5} {'empty':>5} {'filled':>6}  expectations"]
    for r in owner_rows(scan):
        ex = "  ".join(f"{k} {v}" for k, v in sorted(r["expectations"].items()))
        out.append(f"{r['owner']:<22} {r['slots']:>5} {r['empty']:>5} {r['filled']:>6}  {ex}")
    return "\n".join(out)


def render_empties(scan: Scan, owner: str | None) -> str:
    out = []
    cur = None
    for s in scan.slots:
        if s.filled or (owner and s.owner.lower() != owner.lower()):
            continue
        if s.owner != cur:
            cur = s.owner
            out.append(f"\n{cur}")
        tag = " (not saved in the asset yet)" if s.kind == "unserialized" else ""
        out.append(f"  {s.asset}:{s.field}  [{s.cls}] {s.expectation}{tag}")
    if not out:
        return f"no empty slots{' for ' + owner if owner else ''}"
    return "\n".join(out).lstrip("\n")


def to_json(scan: Scan) -> dict:
    def slot(s: Slot) -> dict:
        return {
            "asset": s.asset, "field": s.field, "script": s.script, "class": s.cls, "object": s.object_name,
            "owner": s.owner, "kind": s.kind, "source_prefab": s.source_prefab or None, "override_from": s.override_from or None,
            "path": s.path, "guid": list(s.data), "filled": s.filled,
            "expectation": s.expectation, "how": s.how,
            "emitter": {"play": s.emitter_play, "stop": s.emitter_stop} if s.expectation == "emitter" else None,
            "event": {"path": s.event.path, "guid": s.event.guid, "loops": s.event.loops, "loop_how": s.event.loop_how,
                      "params": s.event.params, "banks": s.event.banks} if s.event else None,
            "banked": s.banked, "notes": s.notes,
        }
    fails = failures(scan)
    return {
        "counts": counts(scan),
        "owners": owner_rows(scan),
        "slots": [slot(s) for s in scan.slots],
        "failures": [{"slot": s.label if s else None, "class": c, "reason": r} for s, c, r in fails],
        "events": [{"path": e.path, "guid": e.guid, "loops": e.loops, "loop_how": e.loop_how, "params": e.params,
                    "banks": e.banks, "banked": e.guid in scan.fmod.banked}
                   for e in sorted(scan.fmod.events.values(), key=lambda e: e.path)],
        "platforms": platform_targets(scan.root),
        "bank_dirs": scan.fmod.bank_dirs,
    }


def render_check(scan: Scan) -> tuple[str, int]:
    fails = failures(scan)
    out = [f"FAIL [{cls}] {s.label if s else 'banks'}  {reason}" for s, cls, reason in fails]
    if fails:
        by: dict[str, int] = {}
        for _, cls, _ in fails:
            by[cls] = by.get(cls, 0) + 1
        out.append(f"sound_slots: {len(fails)} failures (" + ", ".join(f"{k} {v}" for k, v in sorted(by.items())) + ")")
        return "\n".join(out), 1
    return "sound_slots: --check OK (no mis-wired slot, no unbanked event, no bank gap)", 0


def charles_block(scan: Scan, today: str) -> str:
    c = counts(scan)
    fails = failures(scan)
    lines = [
        BEGIN,
        f"## Generated: the slot audit ({today}, `python3 Tools/Build/sound_slots.py --charles`)",
        "",
        "Written by the tool from the assets, the code and the FMOD project; edit nothing here by hand, rerun it. "
        "Every FMOD slot in the game, what the code expects of it (read from the call sites), what is in it, and what "
        "the Studio project says about that event. The hand-written sections above are the tasks; this is the queue.",
        "",
        f"**Counts.** {c['slots']} slots: {c['empty']} empty, {c['filled']} filled ({c['blocks']['total']} serialised blocks, "
        f"{c['unserialized']} fields the code declares that no asset has saved yet, {c['overrides']['total']} scene overrides folded "
        f"into the prefab slots they set). FMOD: {c['events_authored']} events authored ({c['events_looping']} loop), "
        f"{c['events_banked']} in the built banks (`Build/GUIDs.txt`), {c['events_in_use']} distinct events in use. "
        f"Bank folders built: {', '.join(scan.fmod.bank_dirs) or 'none'}.",
        "",
        "### Mis-wired slots (what `--check` fails on)",
        "",
    ]
    slot_fails = [(s, cls, r) for s, cls, r in fails if s]
    if slot_fails:
        lines.append("| Slot | Owner | Code expects | In the slot | Class | Why |")
        lines.append("|---|---|---|---|---|---|")
        for s, cls, r in slot_fails:
            where = f"`{short_asset(s.asset)}` ▸ `{s.cls}.{s.field}`" + (f" (set in `{short_asset(s.override_from)}`)" if s.override_from else "")
            lines.append(f"| {where} | {s.owner} | {s.expectation} | `{ev_cell(s)}` | {cls} | {r.replace('|', '/')} |")
    else:
        lines.append("None.")
    gaps = [r for s, cls, r in fails if s is None]
    lines += ["", "### Bank gaps", ""]
    lines += [f"- {g}" for g in gaps] or ["- None."]
    lines += ["", "### Empty slots by owner (the brief queue)", "",
              "| Owner | Empty | Slots (asset ▸ field; ◦ = declared in code, not saved in the asset yet) |", "|---|---|---|"]
    for r in owner_rows(scan):
        if r["empty"] == 0:
            continue
        items = []
        for s in scan.slots:
            if s.owner == r["owner"] and not s.filled:
                mark = "◦" if s.kind == "unserialized" else ""
                items.append(f"`{short_asset(s.asset)}` ▸ {s.field}{mark} ({s.expectation})")
        lines.append(f"| {r['owner']} | {r['empty']} | " + "; ".join(items) + " |")
    lines.append(END)
    return "\n".join(lines)


def write_charles(path: str, block: str) -> str:
    text = read_text(path) if os.path.exists(path) else ""
    if BEGIN in text and END in text:
        start = text.index(BEGIN)
        end = text.index(END) + len(END)
        new = text[:start] + block + text[end:]
        action = "rewrote"
    else:
        if text and not text.endswith("\n"):
            text += "\n"
        new = text + "\n" + block + "\n"
        action = "appended"
    if not new.endswith("\n"):
        new += "\n"
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(new)
    return action


# ----------------------------------------------------------------------------------------------- self-test

def _write(path: str, text: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(text)


def _event_xml(guid: str, name: str, folder: str, loop: bool) -> str:
    extra = (
        f'\t<object class="LoopRegion" id="{{{guid[:-1]}f}}">\n\t\t<property name="length"><value>5</value></property>\n\t</object>\n'
        if loop else "")
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n<objects serializationModel="Studio.02.02.00">\n'
        f'\t<object class="Event" id="{{{guid}}}">\n\t\t<property name="name">\n\t\t\t<value>{name}</value>\n\t\t</property>\n'
        f'\t\t<relationship name="folder">\n\t\t\t<destination>{{{folder}}}</destination>\n\t\t</relationship>\n'
        '\t\t<relationship name="banks">\n\t\t\t<destination>{bbbbbbbb-0000-0000-0000-000000000001}</destination>\n\t\t</relationship>\n'
        "\t</object>\n" + extra + "</objects>\n")


def _block(name: str, ev: tuple[str, str] | None, indent: str = "  ") -> str:
    d = guid_to_data(ev[0]) if ev else (0, 0, 0, 0)
    p = ev[1] if ev else ""
    return (f"{indent}{name}:\n{indent}  Guid:\n{indent}    Data1: {d[0]}\n{indent}    Data2: {d[1]}\n"
            f"{indent}    Data3: {d[2]}\n{indent}    Data4: {d[3]}\n{indent}  Path: {p}\n")


def _prefab(script_guid: str, sting: tuple[str, str] | None, bed: tuple[str, str] | None, emitter_guid: str = "",
            emitter_event: tuple[str, str] | None = None, emitter_stop: int = 0) -> str:
    text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &100\nGameObject:\n  m_Name: Hull\n"
            "--- !u!114 &200\nMonoBehaviour:\n  m_GameObject: {fileID: 100}\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: \n"
            + _block("stingEvent", sting) + _block("bedEvent", bed) +
            "  layers:\n  - Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")
    if emitter_guid:
        text += ("--- !u!114 &300\nMonoBehaviour:\n  m_GameObject: {fileID: 100}\n"
                 f"  m_Script: {{fileID: 11500000, guid: {emitter_guid}, type: 3}}\n  m_Name: \n"
                 + _block("EventReference", emitter_event) + f"  EventPlayTrigger: 1\n  EventStopTrigger: {emitter_stop}\n")
    return text


FOO_CS = """
using FMODUnity;
namespace T
{
    public class Foo : MonoBehaviour
    {
        [SerializeField, Tooltip("a sting (not a bed)")] EventReference stingEvent;
        [SerializeField] EventReference bedEvent;
        [SerializeField] EventReference tailEvent;
        [SerializeField] EventReference[] layers;
        [SerializeField] EventReference flowEvent;
        [SerializeField] EventReference aliasEvent;
        [SerializeField] GameplaySFXCategory engageSFX = GameplaySFXCategory.BoostActivate;
        FMOD.Studio.EventInstance _bed;
        void Fire(Vector3 at) { PlayOneShot(stingEvent, at); }
        void PlayOneShot(EventReference reference, Vector3 at)
        {
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference,
                at);
        }
        void StartBed()
        {
            if (!FmodSafe.TryCreateInstance(bedEvent, out _bed, this)) return;
            _bed.start();
        }
        void StopBed() => FmodSafe.StopAndRelease(ref _bed, true, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        void Settings() { var s = new Mouth.Settings { TransitEvent = flowEvent, Depth = 1 }; }
        void Juice(int kind)
        {
            var sound = kind switch
            {
                1 => aliasEvent,
                _ => stingEvent,
            };
            Other.Play(sound, transform.position);
        }
    }
    public class Mouth : MonoBehaviour
    {
        public class Settings { public EventReference TransitEvent; public int Depth; }
        Settings _settings;
        void Transit() { AudioSystem.Instance.PlaySFXEvent(_settings.TransitEvent, transform.position); }
    }
}
"""

FOO_PARTIAL_CS = """
namespace T
{
    public partial class Other
    {
        public static void Play(EventReference evt, Vector3 at)
        {
            FMODOneShotVolumeHelper.PlaySFXOneShot(evt, at, 1f);
        }
    }
}
"""

AUDIO_CS = """
namespace T
{
    public enum MenuAudioCategory { OptionClick, BigReward }
    public class AudioSystem : MonoBehaviour
    {
        [SerializeField] EventReference boostActivateEvent;
        [SerializeField] EventReference gunFireEvent;
        [SerializeField] EventReference bigRewardEvent;
        [SerializeField] EventReference optionClickEvent;
        Dictionary<GameplaySFXCategory, EventReference> GameplaySFXEvents;
        Dictionary<MenuAudioCategory, EventReference> MenuAudioEvents;
        void Init()
        {
            GameplaySFXEvents = new Dictionary<GameplaySFXCategory, EventReference>()
            {
                {GameplaySFXCategory.BoostActivate, boostActivateEvent},
                {GameplaySFXCategory.GunFire, gunFireEvent},
            };
            MenuAudioEvents = new Dictionary<MenuAudioCategory, EventReference>()
            {
                {MenuAudioCategory.BigReward, bigRewardEvent},
                {MenuAudioCategory.OptionClick, optionClickEvent},
            };
        }
        public void PlayGameplaySFX(GameplaySFXCategory category, Vector3 at)
        {
            if (GameplaySFXEvents.TryGetValue(category, out var reference)) FMODOneShotVolumeHelper.PlaySFXOneShot(reference, at, 1f);
        }
        public void PlayMenuAudio(MenuAudioCategory category)
        {
            if (MenuAudioEvents.TryGetValue(category, out var reference)) PlaySFXEvent(reference);
        }
        public void PlaySFXEvent(EventReference reference) { FMODOneShotVolumeHelper.PlaySFXOneShot(reference, Vector3.zero, 1f); }
        public void StartHeldGameplaySFX(GameplaySFXCategory category, Object owner)
        {
            if (!GameplaySFXEvents.TryGetValue(category, out var reference)) return;
            if (!FmodSafe.TryCreateInstance(reference, out var instance, this)) return;
            if (FMODOneShotVolumeHelper.IsFireAndForgetSafe(reference, instance)) { instance.start(); instance.release(); return; }
            instance.start();
        }
        public void StopHeldGameplaySFX(GameplaySFXCategory category, Object owner) { FmodSafe.StopAndRelease(ref _held, true, FMOD.Studio.STOP_MODE.ALLOWFADEOUT); }
    }
    public static class HeldVesselSfx
    {
        public static void Begin(AudioSystem audio, GameplaySFXCategory category,
            Object owner)
        {
            audio.StartHeldGameplaySFX(category, owner);
        }
    }
    public class Boost
    {
        void Go() { HeldVesselSfx.Begin(_audio, GameplaySFXCategory.BoostActivate, this); }
        void Gun() { AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.GunFire, transform.position); }
        void Click() { AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick); }
    }
}
"""


def self_test() -> int:
    skim = "{0a70c49e-da1c-4049-8e10-d6451d61f60b}"
    assert guid_to_data(skim) == (175162526, 1078581788, 1171656846, 200696093), guid_to_data(skim)
    assert calls_with_arg("if (x) audio.PlaySFXEvent(fireEvent, pos);", ["fireEvent"]) == ["PlaySFXEvent"]
    assert calls_with_arg("Play(a, Foo(b));", ["b"]) == ["Play", "Foo"]
    masked = mask_code('a = "x{y}" + \'"\' + b; // c "d"\n/* e\nf */ g')
    # a char literal's content is masked like a string's: the quote inside it must not open a string
    assert masked.startswith('a = "    " + \' \' + b;') and masked.count("\n") == 2 and masked.endswith(" g"), masked
    joined = join_statements(["Play(a,", "  b);", "var s = k switch", "{", "  1 => x,", "};", "done();"])
    assert [s for _, s in joined] == ["Play(a, b);", "var s = k switch { 1 => x, }; done();"[:-8].rstrip() if False else "var s = k switch { 1 => x, };", "done();"], joined

    A = "aaaaaaaa-0000-4000-8000-00000000000a"  # loops
    B = "bbbbbbbb-0000-4000-8000-00000000000b"  # one-shot
    C = "cccccccc-0000-4000-8000-00000000000c"  # authored, not banked
    with tempfile.TemporaryDirectory() as tmp:
        scripts = os.path.join(tmp, "Assets", "_Scripts")
        foo_guid, audio_guid, emitter_guid = "1" * 32, "3" * 32, "2" * 32
        _write(os.path.join(scripts, "Foo.cs"), FOO_CS)
        _write(os.path.join(scripts, "Foo.cs.meta"), f"fileFormatVersion: 2\nguid: {foo_guid}\n")
        _write(os.path.join(scripts, "Other.Play.cs"), FOO_PARTIAL_CS)
        _write(os.path.join(scripts, "Other.cs"), "namespace T { public partial class Other { } }\n")
        _write(os.path.join(scripts, "AudioSystem.cs"), AUDIO_CS)
        _write(os.path.join(scripts, "AudioSystem.cs.meta"), f"fileFormatVersion: 2\nguid: {audio_guid}\n")
        _write(os.path.join(tmp, "Assets", "Plugins", "FMOD", "src", "StudioEventEmitter.cs"),
               "namespace FMODUnity { public class StudioEventEmitter : EventHandler { public EventReference EventReference; } }\n")
        _write(os.path.join(tmp, "Assets", "Plugins", "FMOD", "src", "StudioEventEmitter.cs.meta"),
               f"fileFormatVersion: 2\nguid: {emitter_guid}\n")
        fm = os.path.join(tmp, "Cosmic Shore")
        master, sfx_folder = "f0000000-0000-4000-8000-000000000001", "f0000000-0000-4000-8000-000000000002"
        _write(os.path.join(fm, "Metadata", "EventFolder", f"{{{master}}}.xml"),
               f'<objects><object class="MasterEventFolder" id="{{{master}}}"><property name="name"><value>Master</value></property></object></objects>')
        _write(os.path.join(fm, "Metadata", "EventFolder", f"{{{sfx_folder}}}.xml"),
               f'<objects><object class="EventFolder" id="{{{sfx_folder}}}"><property name="name"><value>SFX</value></property>'
               f'<relationship name="folder"><destination>{{{master}}}</destination></relationship></object></objects>')
        _write(os.path.join(fm, "Metadata", "Bank", "{bbbbbbbb-0000-0000-0000-000000000001}.xml"),
               '<objects><object class="Bank" id="{bbbbbbbb-0000-0000-0000-000000000001}"><property name="name"><value>SFX</value></property></object></objects>')
        _write(os.path.join(fm, "Metadata", "Platform", "{p0000000-0000-0000-0000-000000000001}.xml"),
               '<objects><object class="Platform" id="{p}"><property name="name"><value>Desktop</value></property><property name="subDirectory"><value>Desktop</value></property></object></objects>')
        _write(os.path.join(fm, "Metadata", "Event", f"{{{A}}}.xml"), _event_xml(A, "Bed", sfx_folder, True))
        _write(os.path.join(fm, "Metadata", "Event", f"{{{B}}}.xml"), _event_xml(B, "Sting", sfx_folder, False))
        _write(os.path.join(fm, "Metadata", "Event", f"{{{C}}}.xml"), _event_xml(C, "Unbuilt", sfx_folder, False))
        _write(os.path.join(fm, "Build", "GUIDs.txt"), f"{{{A}}} event:/SFX/Bed\n{{{B}}} event:/SFX/Sting\n")
        _write(os.path.join(fm, "Build", "Desktop", "Master.bank"), "")
        _write(os.path.join(tmp, "ProjectSettings", "ProjectSettings.asset"),
               "PlayerSettings:\n  applicationIdentifier:\n    Standalone: com.test\n  buildNumber:\n    Standalone: 0\n")
        prefab = os.path.join(tmp, "Assets", "_Prefabs", "Spacevessels", "Hull.prefab")
        audio_prefab = os.path.join(tmp, "Assets", "_Prefabs", "CORE", "AudioSystem.prefab")
        _write(audio_prefab, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &100\nGameObject:\n  m_Name: AudioSystem\n"
               "--- !u!114 &200\nMonoBehaviour:\n  m_GameObject: {fileID: 100}\n"
               f"  m_Script: {{fileID: 11500000, guid: {audio_guid}, type: 3}}\n  m_Name: \n")
        _write(audio_prefab + ".meta", "fileFormatVersion: 2\nguid: " + "4" * 32 + "\n")
        bed_ev = (A, "event:/SFX/Bed")
        sting_ev = (B, "event:/SFX/Sting")
        d = guid_to_data(A)
        _write(os.path.join(tmp, "Assets", "_Scenes", "Bootstrap.unity"),
               "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1001 &900\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n"
               "    - target: {fileID: 200, guid: " + "4" * 32 + ",\n        type: 3}\n      propertyPath: boostActivateEvent.Path\n"
               "      value: event:/SFX/Bed\n      objectReference: {fileID: 0}\n"
               + "".join(f"    - target: {{fileID: 200, guid: {'4' * 32}, type: 3}}\n      propertyPath: boostActivateEvent.Guid.Data{i + 1}\n"
                         f"      value: {d[i]}\n      objectReference: {{fileID: 0}}\n" for i in range(4))
               + "    - target: {fileID: 200, guid: " + "4" * 32 + ", type: 3}\n      propertyPath: gunFireEvent.Path\n"
               "      value: event:/SFX/Sting\n      objectReference: {fileID: 0}\n"
               "  m_SourcePrefab: {fileID: 100100000, guid: " + "4" * 32 + ", type: 3}\n")

        def run(sting, bed, **kw) -> Scan:
            _write(prefab, _prefab(foo_guid, sting, bed, **kw))
            code = Code(scripts, tmp)
            fmod = Fmod(fm)
            return Scan(tmp, code, fmod, load_guid_map(os.path.join(tmp, "Assets"))).run()

        # the scan: both blocks, the array item, the declared-but-unserialised fields; the expectations
        scan = run(bed_ev, None)
        by = {(short_asset(s.asset), s.field): s for s in scan.slots}
        hull = {f: s for (a, f), s in by.items() if a == "Hull.prefab"}
        assert set(hull) == {"stingEvent", "bedEvent", "layers[0]", "tailEvent", "flowEvent", "aliasEvent"}, sorted(hull)
        assert hull["stingEvent"].kind == "block" and hull["stingEvent"].filled
        assert hull["bedEvent"].kind == "block" and not hull["bedEvent"].filled
        assert hull["tailEvent"].kind == "unserialized" and not hull["tailEvent"].filled
        assert hull["stingEvent"].expectation == "one-shot", hull["stingEvent"]
        assert hull["bedEvent"].expectation == "loop", hull["bedEvent"]
        assert hull["tailEvent"].expectation == "unknown" and "never read" in hull["tailEvent"].notes[0]
        assert hull["flowEvent"].expectation == "one-shot", hull["flowEvent"]  # flows into Mouth.Settings.TransitEvent
        assert hull["aliasEvent"].expectation == "one-shot", hull["aliasEvent"]  # a switch alias into a partial-class helper
        assert hull["stingEvent"].owner == "Hull" and hull["stingEvent"].cls == "Foo"
        assert len(scan.fmod.events) == 3
        assert hull["stingEvent"].event is not None and hull["stingEvent"].event.loops and hull["stingEvent"].banked
        # the AudioSystem: unserialized slots, the scene override folded in, category expectations from the call sites
        audio = {f: s for (a, f), s in by.items() if a == "AudioSystem.prefab"}
        assert set(audio) == {"boostActivateEvent", "gunFireEvent", "bigRewardEvent", "optionClickEvent"}, sorted(audio)
        assert audio["boostActivateEvent"].expectation == "held", audio["boostActivateEvent"]
        assert audio["boostActivateEvent"].filled and audio["boostActivateEvent"].override_from.endswith("Bootstrap.unity")
        assert audio["boostActivateEvent"].event is bed_event(scan) and not any(c == "C2" for _, c, _ in failures(scan))
        assert audio["gunFireEvent"].expectation == "one-shot" and audio["gunFireEvent"].path == "event:/SFX/Sting"
        assert audio["optionClickEvent"].expectation == "one-shot"
        assert audio["bigRewardEvent"].expectation == "one-shot" and any("serialized or computed" in n for n in audio["bigRewardEvent"].notes)
        assert scan.override_count == 2 and not any(s.kind == "override" for s in scan.slots)
        # the C2 negative control: a looping event in the one-shot slot FAILS
        fails = failures(scan)
        assert any(c == "C2" and s is hull["stingEvent"] for s, c, _ in fails), fails
        text, rc = render_check(scan)
        assert rc == 1 and "Hull.prefab:stingEvent" in text, text
        # the clean fixture passes
        scan = run(sting_ev, bed_ev)
        text, rc = render_check(scan)
        assert rc == 0, text
        # the C4 control: a one-shot in the loop slot
        scan = run(sting_ev, sting_ev)
        assert [c for _, c, _ in failures(scan)] == ["C4"], failures(scan)
        # the unbanked control: authored, not in GUIDs.txt
        scan = run((C, "event:/SFX/Unbuilt"), bed_ev)
        assert [c for _, c, _ in failures(scan)] == ["unbanked"], failures(scan)
        # a stale reference: a GUID and path nothing authored
        scan = run(("dddddddd-0000-4000-8000-00000000000d", "event:/SFX/Gone"), bed_ev)
        assert [c for _, c, _ in failures(scan)] == ["unknown-event"]
        # an emitter: a looping event with no stop trigger fails, with ObjectDestroy it passes
        scan = run(sting_ev, bed_ev, emitter_guid=emitter_guid, emitter_event=bed_ev, emitter_stop=0)
        em = [s for s in scan.slots if s.expectation == "emitter"]
        assert len(em) == 1 and em[0].emitter_play == "ObjectStart" and em[0].emitter_stop == "None", em
        assert [c for _, c, _ in failures(scan)] == ["emitter-loop"]
        scan = run(sting_ev, bed_ev, emitter_guid=emitter_guid, emitter_event=bed_ev, emitter_stop=2)
        assert failures(scan) == []
        # the platform gap: Android and iOS listed, only Desktop banks
        _write(os.path.join(tmp, "ProjectSettings", "ProjectSettings.asset"),
               "PlayerSettings:\n  applicationIdentifier:\n    Android: com.test\n    Standalone: com.test\n    iPhone: com.test\n")
        scan = run(sting_ev, bed_ev)
        gaps = [r for s, c, r in failures(scan) if c == "no-banks"]
        assert len(gaps) == 2 and gaps[0].startswith("no banks for Android") and gaps[1].startswith("no banks for iOS"), gaps
        # --charles: appended once, rewritten in place, the text above untouched
        doc = os.path.join(tmp, "CHARLES.md")
        _write(doc, "# Tasks\n\n## C1 - hand-written\n\nkeep me\n")
        assert write_charles(doc, charles_block(scan, "2026-01-01")) == "appended"
        assert write_charles(doc, charles_block(scan, "2026-01-02")) == "rewrote"
        t = read_text(doc)
        assert t.startswith("# Tasks\n\n## C1 - hand-written\n\nkeep me\n") and t.count(BEGIN) == 1 and t.count(END) == 1
        assert "2026-01-02" in t and "2026-01-01" not in t
        assert "Hull.prefab" in t and "no banks for Android" in t
        # the renderers do not crash and the JSON is serialisable
        render_table(scan)
        render_owners(scan)
        render_empties(scan, None)
        render_empties(scan, "Hull")
        json.dumps(to_json(scan))
        # the block parser on Unity's list shapes
        body = ("MonoBehaviour:\n  Tracks:\n  - id: a\n    trackEvent:\n      Guid:\n        Data1: 1\n        Data2: 2\n"
                "        Data3: 3\n        Data4: 4\n      Path: event:/x\n    on: 1\n  - id: b\n    trackEvent:\n      Guid:\n"
                "        Data1: 0\n        Data2: 0\n        Data3: 0\n        Data4: 0\n      Path: \n  combos:\n  - steps:\n"
                "    - t: 1\n    sound:\n      Guid:\n        Data1: 0\n        Data2: 0\n        Data3: 0\n        Data4: 0\n      Path:\n")
        got = find_blocks(body)
        assert [b[0] for b in got] == ["Tracks[0].trackEvent", "Tracks[1].trackEvent", "combos[0].sound"], got
        assert got[0][1] == "event:/x" and got[0][2] == (1, 2, 3, 4)
    print("sound_slots: self-test OK")
    return 0


def bed_event(scan: Scan) -> FmodEvent:
    return scan.fmod.by_path["event:/SFX/Bed"]


# ----------------------------------------------------------------------------------------------- main

def build(root: str) -> Scan:
    scripts = os.path.join(root, "Assets", "_Scripts")
    if not os.path.isdir(scripts):
        raise RuntimeError(f"no Assets/_Scripts under {root}")
    code = Code(scripts, root)
    fmod = Fmod(os.path.join(root, "Cosmic Shore"))
    if not fmod.ok:
        print("sound_slots: no FMOD Studio project under 'Cosmic Shore/' - events cannot be resolved", file=sys.stderr)
    return Scan(root, code, fmod, load_guid_map(os.path.join(root, "Assets"))).run()


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--owners", action="store_true", help="the owners with their slot counts")
    ap.add_argument("--empties", nargs="?", const="", metavar="OWNER", help="the empty slots, all or one owner's")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--check", action="store_true", help="exit 1 on a mis-wired slot, an unbanked event or a bank gap")
    ap.add_argument("--charles", action="store_true", help="rewrite the generated block of Docs/AudioSystem/CHARLES_TASKS.md")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--root", default=ROOT, help=argparse.SUPPRESS)
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    try:
        scan = build(args.root)
    except (RuntimeError, OSError) as exc:
        print(f"sound_slots: {exc}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps(to_json(scan), indent=2))
        return 0
    if args.owners:
        print(render_owners(scan))
        return 0
    if args.empties is not None:
        print(render_empties(scan, args.empties or None))
        return 0
    if args.charles:
        doc = os.path.join(args.root, "Docs", "AudioSystem", "CHARLES_TASKS.md")
        action = write_charles(doc, charles_block(scan, _dt.date.today().isoformat()))
        print(f"sound_slots: {action} the generated block in {os.path.relpath(doc, args.root)} "
              f"({counts(scan)['empty']} empty slots, {len(failures(scan))} failures)")
        return 0
    if args.check:
        text, rc = render_check(scan)
        print(text)
        return rc
    print(render_table(scan))
    return 0


if __name__ == "__main__":
    sys.exit(main())

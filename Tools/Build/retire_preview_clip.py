#!/usr/bin/env python3
"""Strip retired serialized keys from every game card, and keep them stripped.

WHAT IS RETIRED. Two keys that no `SO_Game`-derived class declares any more:

  * `PreviewClip` - `SO_Game.PreviewClip` was deleted when the arcade preview became a live
    satellite arena (Docs/ModePreview/ARCHITECTURE.md: the window "must never fall back to a
    video"). 31 `SO_ArcadeGame` cards and the one `SO_Mission` card still named a
    `*Preview_Prefab.prefab` through it.
  * `CallToActionTargetType` - retired with the call-to-action surface (retire_call_to_action.py,
    Docs/UI_ARCHITECTURE_AUDIT.md F7). The shipped cards were swept then; this keeps them swept.

WHY STRIP A KEY UNITY IGNORES. A key its type does not declare deserializes to nothing, so the
card still loads. But it is still TEXT: every text-based sweep follows its guid
(measure_build_reachability.py counted ~110 MB of preview prefabs/video as shipping through it,
Docs/LAUNCH_BLOCKER_INDEX.md E2), every reader takes it for a live field, and a generator that
clones a card carries it forward. The list lives in ONE place, `arcade_mode_lib.RETIRED_CARD_KEYS`,
which the mode generators already refuse to emit; this is the half that covers the SHIPPED cards.

WHICH ASSETS ARE CARDS. Resolved by the asset's `m_Script` guid to its OWNING TYPE - `SO_Game` and
every class deriving from it, discovered from the C# - never by grepping the field name:
`SO_VesselAbility` declares a LIVE `PreviewClip` (a VideoPlayer the Hangar reads) and its assets
must keep it. Before stripping, the tool also proves the retirement is still true: if any class in
a card's chain names a retired key again (a field, or a `FormerlySerializedAs` source), it refuses
to strip and `--check` fails, because the key would then be data.

THE EDIT. The key line and its wrapped continuation (Unity wraps a long `{fileID, guid, type}` onto
a second, deeper-indented line) are removed; every other byte, line ending included, is kept. The
result is validated in memory before anything is written: only the retired lines differ, the YAML
still parses, and the key is gone.

    python3 Tools/Build/retire_preview_clip.py              # strip (idempotent)
    python3 Tools/Build/retire_preview_clip.py --check      # exit 1 if any card carries a key
    python3 Tools/Build/retire_preview_clip.py --self-test  # negative controls, no repo writes

A plain READER/one-shot fixer: it runs from the terminal, not the Editor, so it has no
FrogletToolChangeLedger output - its output is the card diff in the same commit.
"""
from __future__ import annotations

import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
from arcade_mode_lib import RETIRED_CARD_KEYS  # noqa: E402  (the single list)

SCRIPTS_DIR = "Assets/_Scripts"
BASE_CLASS = "SO_Game"

KEY_RE = re.compile(r"^  (" + "|".join(map(re.escape, RETIRED_CARD_KEYS)) + r"):(?: |\r?$)")
SCRIPT_GUID_RE = re.compile(r"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}",
                            re.M)


# ── Which types are cards, and do they still declare a retired key ─────────────────────────────

def _strip_comments(src: str) -> str:
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return re.sub(r"//[^\n]*", "", src)


def card_types(root: str = ROOT) -> "dict[str, list[str]]":
    """{script guid: [.cs files of the class and every base up to SO_Game]} for SO_Game and every
    class that derives from it (transitively). Fails loud if SO_Game itself cannot be found."""
    decl = {}   # class name -> (file, base name)
    for cs in glob.glob(os.path.join(root, SCRIPTS_DIR, "**", "*.cs"), recursive=True):
        with open(cs, encoding="utf-8", errors="replace") as fh:
            src = _strip_comments(fh.read())
        for m in re.finditer(r"\bclass\s+(\w+)\s*(?::\s*([\w.]+))?", src):
            decl.setdefault(m.group(1), (cs, (m.group(2) or "").split(".")[-1]))
    if BASE_CLASS not in decl:
        raise SystemExit(f"error: class {BASE_CLASS} not found under {SCRIPTS_DIR}")

    out = {}
    for name, (cs, _) in decl.items():
        chain, cur = [], name
        while cur in decl and cur not in [c for c, _ in chain]:
            chain.append((cur, decl[cur][0]))
            if cur == BASE_CLASS:
                break
            cur = decl[cur][1]
        if chain[-1][0] != BASE_CLASS:
            continue
        meta = cs + ".meta"
        if not os.path.isfile(meta):
            continue
        with open(meta, encoding="utf-8") as fh:
            g = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
        if g:
            out[g.group(1)] = [f for _, f in chain]
    return out


def still_declared(chain_files: "list[str]") -> "list[str]":
    """Retired keys a card's C# chain names again (as a field or a FormerlySerializedAs source)."""
    hits = []
    for cs in chain_files:
        with open(cs, encoding="utf-8", errors="replace") as fh:
            src = _strip_comments(fh.read())
        for key in RETIRED_CARD_KEYS:
            if re.search(rf"\b{key}\b", src):
                hits.append(f"{os.path.relpath(cs, ROOT)} names {key}")
    return hits


# ── The edit ───────────────────────────────────────────────────────────────────────────────────

def _key_span(lines: "list[str]", i: int) -> int:
    """Index one past the last line belonging to the top-level key on line i: its wrapped
    continuation lines (indent > 2) and, for a block value, its `  - ` items."""
    j = i + 1
    block = lines[i].rstrip("\r\n").endswith(":")
    while j < len(lines):
        ln = lines[j]
        if ln.startswith("   ") or (block and ln.startswith("  - ")):
            j += 1
            continue
        break
    return j


def strip_keys(text: str) -> "tuple[str, list[str]]":
    """(new text, [removed key names]). Only top-level MonoBehaviour fields (two-space indent)."""
    lines = text.splitlines(keepends=True)
    out, removed, i = [], [], 0
    while i < len(lines):
        m = KEY_RE.match(lines[i])
        if m:
            removed.append(m.group(1))
            i = _key_span(lines, i)
            continue
        out.append(lines[i])
        i += 1
    return "".join(out), removed


def carried_keys(text: str) -> "list[str]":
    return [k for k in RETIRED_CARD_KEYS if re.search(rf"^  {re.escape(k)}:", text, re.M)]


def validate(before: str, after: str) -> "list[str]":
    """Everything that must be true of a stripped card before it is written."""
    errs = []
    if carried_keys(after):
        errs.append(f"still carries {carried_keys(after)}")
    # Only retired lines may differ: `after` must be `before` with whole lines deleted, in order,
    # and every deleted run must be ONE COMPLETE key - its key line plus its continuation lines.
    a = after.splitlines(keepends=True)
    b = before.splitlines(keepends=True)
    kept, k = [], 0
    for ln in b:
        hit = k < len(a) and ln == a[k]
        kept.append(hit)
        k += hit
    if k != len(a):
        errs.append("the edit changed a line it did not own")
    else:
        for r, ln in enumerate(b):
            if kept[r]:
                continue
            starts_run = r == 0 or kept[r - 1]
            if KEY_RE.match(ln):
                pass                       # a retired key line (two may be adjacent)
            elif starts_run:
                errs.append(f"removed a line that is not a retired key: {ln.strip()[:60]!r}")
            elif not (ln.startswith("   ") or ln.startswith("  - ")):
                errs.append(f"removed a line past the key's value: {ln.strip()[:60]!r}")
            if r + 1 < len(b) and kept[r + 1] and b[r + 1].startswith("   "):
                errs.append("left an orphaned continuation line behind a removed key")
    if ("\r\n" in before) != ("\r\n" in after):
        errs.append("line endings changed")
    try:
        import yaml  # noqa: WPS433
        body = re.sub(r"^--- !u!\d+ &-?\d+.*$", "---", after, flags=re.M)
        body = re.sub(r"^%.*$", "", body, flags=re.M)
        for doc in yaml.safe_load_all(body):
            if not isinstance(doc, dict):
                errs.append("a document no longer parses as a mapping")
    except ImportError:
        pass
    except Exception as exc:  # yaml.YAMLError and friends
        errs.append(f"YAML no longer parses: {exc}")
    return errs


# ── Repo scan ──────────────────────────────────────────────────────────────────────────────────

def scan(root: str = ROOT) -> "tuple[list[tuple[str, list[str]]], list[str], int]":
    """([(card rel path, keys it carries)], [declaration problems], number of cards scanned)."""
    types = card_types(root)
    offenders, declared, n = [], set(), 0
    for path in glob.glob(os.path.join(root, "Assets", "**", "*.asset"), recursive=True):
        with open(path, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        m = SCRIPT_GUID_RE.search(text)
        if not m or m.group(1) not in types:
            continue
        n += 1
        keys = carried_keys(text)
        if keys:
            offenders.append((os.path.relpath(path, root).replace(os.sep, "/"), keys))
        declared.update(still_declared(types[m.group(1)]))
    return sorted(offenders), sorted(declared), n


def check() -> int:
    offenders, declared, n = scan()
    if declared:
        print("retired card keys: a card type DECLARES a retired key again - the key is data, "
              "not dead. Remove it from arcade_mode_lib.RETIRED_CARD_KEYS or from the C#:",
              file=sys.stderr)
        for d in declared:
            print(f"  {d}", file=sys.stderr)
    if offenders:
        print(f"retired card keys: {len(offenders)} card(s) still carry "
              f"{'/'.join(RETIRED_CARD_KEYS)} - run python3 Tools/Build/retire_preview_clip.py",
              file=sys.stderr)
        for rel, keys in offenders:
            print(f"  {rel}: {', '.join(keys)}", file=sys.stderr)
    if declared or offenders:
        return 1
    print(f"retired card keys: OK ({n} SO_Game cards, none carries "
          f"{' or '.join(RETIRED_CARD_KEYS)})")
    return 0


def fix() -> int:
    offenders, declared, _ = scan()
    if declared:
        return check()
    if not offenders:
        print("already retired")
        return 0
    staged = {}
    for rel, _ in offenders:
        path = os.path.join(ROOT, rel)
        with open(path, encoding="utf-8", newline="") as fh:
            before = fh.read()
        after, removed = strip_keys(before)
        errs = validate(before, after)
        if errs:
            raise SystemExit(f"error: {rel}: {'; '.join(errs)} - nothing written")
        staged[rel] = (after, removed)
    for rel, (after, removed) in staged.items():
        with open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="") as fh:
            fh.write(after)
        print(f"  {rel}: removed {', '.join(removed)}")
    print(f"\nstripped {len(staged)} card(s).")
    return check()


# ── Negative controls ──────────────────────────────────────────────────────────────────────────

def self_test() -> int:
    fails = []

    def expect(cond, what):
        print(f"  {'ok  ' if cond else 'FAIL'} {what}")
        if not cond:
            fails.append(what)

    head = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_Script: {fileID: 11500000, guid: fe040efad3307fb449b6b72ad15362da, type: 3}\n"
            "  m_Name: ArcadeGameX\n  DisplayName: X\n")
    tail = "  GolfScoring: 0\n  SceneName: MinigameX\n"
    wrapped = ("  PreviewClip: {fileID: 241334157148977051, guid: f48cd1df8d808014c8a8f4db1258d202,\n"
               "    type: 3}\n")
    one_line = "  PreviewClip: {fileID: 241334157148977051, guid: f48cd1df8d808014c8a8f4db1258d202, type: 3}\n"
    null_ref = "  PreviewClip: {fileID: 0}\n"
    cta = "  CallToActionTargetType: 404\n"
    clean = head + tail

    for label, extra in (("wrapped", wrapped), ("one-line", one_line), ("null", null_ref),
                         ("CallToActionTargetType", cta), ("both", wrapped + cta)):
        dirty = head + extra + tail
        expect(carried_keys(dirty), f"a card carrying a {label} retired key is DETECTED")
        after, removed = strip_keys(dirty)
        expect(after == clean, f"stripping a {label} key yields exactly the clean card")
        expect(not validate(dirty, after), f"the {label} strip passes its own validation")

    crlf = (head + wrapped + tail).replace("\n", "\r\n")
    after, _ = strip_keys(crlf)
    expect(after == clean.replace("\n", "\r\n"), "CRLF cards are stripped with CRLF kept")

    expect(not carried_keys(clean), "a clean card is not flagged")
    expect(strip_keys(clean) == (clean, []), "stripping a clean card is a no-op (idempotent)")
    nested = head + "  Tips:\n  - PreviewClip: is a tip, not a field\n" + tail
    expect(not carried_keys(nested), "a nested/list value merely NAMED PreviewClip is not flagged")
    prefix = head + "  PreviewClipLength: 3\n" + tail
    expect(strip_keys(prefix)[0] == prefix, "a longer field sharing the prefix is left alone")

    # validate() must reject an edit that touches a line it does not own.
    expect(validate(head + wrapped + tail, clean.replace("DisplayName: X", "DisplayName: Y")),
           "validation rejects an edit that changes a non-retired line")
    expect(validate(head + wrapped + tail, head + "    type: 3}\n" + tail),
           "validation rejects a strip that leaves an orphaned continuation line")

    # Owning-type resolution against the real repo: SO_VesselAbility keeps its live PreviewClip.
    types = card_types()
    def guid_of(rel):
        with open(os.path.join(ROOT, rel + ".meta"), encoding="utf-8") as fh:
            return re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M).group(1)
    expect(guid_of("Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs") in types,
           "SO_ArcadeGame resolves as a card type")
    expect(guid_of("Assets/_Scripts/ScriptableObjects/SO_Mission.cs") in types,
           "SO_Mission resolves as a card type")
    expect(guid_of("Assets/_Scripts/ScriptableObjects/SO_VesselAbility.cs") not in types,
           "SO_VesselAbility (live PreviewClip) is NOT a card type")
    expect(not [d for g in types for d in still_declared(types[g])],
           "no card type declares a retired key today")

    # The declaration guard fires: a chain naming PreviewClip again is reported.
    import shutil
    import tempfile
    tmp = tempfile.mkdtemp(prefix="retire_preview_clip_")
    probe = os.path.join(tmp, "Probe.cs")
    try:
        with open(probe, "w", encoding="utf-8") as fh:
            fh.write("class P : SO_Game { [FormerlySerializedAs(\"PreviewClip\")] public int A; }\n")
        hits = still_declared([probe])
        expect(len(hits) == 1 and hits[0].endswith("names PreviewClip"),
               "a FormerlySerializedAs(\"PreviewClip\") in a card chain is REPORTED")
        with open(probe, "w", encoding="utf-8") as fh:
            fh.write("class P : SO_Game { // PreviewClip was retired\n public int A; }\n")
        expect(not still_declared([probe]), "a comment mentioning PreviewClip is not a declaration")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    print(f"\nself-test: {'FAILED' if fails else 'OK'} ({len(fails)} failure(s))")
    return 1 if fails else 0


def main() -> int:
    if "--self-test" in sys.argv:
        return self_test()
    if "--check" in sys.argv:
        return check()
    return fix()


if __name__ == "__main__":
    sys.exit(main())

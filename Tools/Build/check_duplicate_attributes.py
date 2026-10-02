#!/usr/bin/env python3
"""Fail on a C# member carrying the same AllowMultiple=false attribute twice (CS0579).

WHY THIS EXISTS
    Inserting a new serialized field ABOVE an existing one is a two-line edit that
    strands the existing field's [Tooltip] on top of the inserted block, so the
    inserted field ends up with two. It compiles nowhere and is reported nowhere:
    the five standing out-of-editor gates are all SYNTAX gates for any file whose
    base type lives in the Assembly-CSharp monolith (Roslyn abandons class-body
    binding when the base type is unresolved), and CS0579 is an attribute error
    inside a class body. It shipped once, in SniperShotActionSO.cs, and surfaced
    only in the Unity console -- see this script's --self-test, whose positive
    control IS that file's broken text.

    It also catches the duplicate [Header] that the same edit leaves behind, which
    is the SECOND error the compiler would have reported.

WHAT IT DOES NOT PROVE
    Nothing about types, members or overloads -- those stay editor-only. This asks
    one textual question: does any attribute run repeat an attribute C# forbids
    twice on one member.
"""
import argparse, io, re, subprocess, sys

# Unity/BCL attributes commonly used on serialized fields whose AttributeUsage is
# AllowMultiple = false. A repeat of any of these is CS0579.
SINGLE = {"Tooltip", "Header", "Space", "Range", "Min", "TextArea", "Multiline",
          "HideInInspector", "SerializeReference", "ContextMenuItem"}


def strip_strings(s):
    """Drop string literals and line comments so brackets inside them don't count."""
    out, i, n = [], 0, len(s)
    while i < n:
        c = s[i]
        if c == '"':
            i += 1
            while i < n:
                if s[i] == '\\':
                    i += 2
                    continue
                if s[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == '/' and i + 1 < n and s[i + 1] == '/':
            break
        out.append(c)
        i += 1
    return "".join(out)


def scan_text(text):
    """-> [(run_start_line, attribute_name, duplicate_line)], all 1-indexed."""
    lines = text.split("\n")
    findings, depth, run, start = [], 0, [], 0

    def flush():
        nonlocal run
        seen = set()
        for ln, name in run:
            if name in SINGLE and name in seen:
                findings.append((start + 1, name, ln + 1))
            seen.add(name)
        run = []

    for i, raw in enumerate(lines):
        t = raw.strip()
        code = strip_strings(raw).rstrip()
        if depth == 0 and not t.startswith("["):
            if run:
                flush()
            continue
        if depth == 0:
            if not run:
                start = i
            m = re.match(r"\[\s*([A-Za-z_]\w*)", code.strip())
            if m:
                run.append((i, m.group(1)))
            # comma-joined attributes on one line: [SerializeField, Min(0f)]
            head = code.split("]")[0] if "]" in code else code
            for m2 in re.finditer(r",\s*([A-Za-z_]\w*)", head):
                run.append((i, m2.group(1)))
        depth += code.count("[") - code.count("]")
        if depth < 0:
            depth = 0
        # depth back to 0 and the line continues past the ']' -> the member is
        # declared here, so the run ends with it.
        if depth == 0 and not code.endswith("]"):
            flush()
    if run:
        flush()
    return findings


def scan(path):
    return scan_text(io.open(path, encoding="utf-8").read())


BROKEN = '''
        [SerializeField, Min(0)] private int pierceCount;

        [Header("Impact")]
        [Tooltip("Debris speed the destroyed prism's pieces carry.")]
        [Header("Vessel strip")]
        [Tooltip("Normalized element levels this round strips.")]
        [SerializeField, Min(0f)] private float vesselStripPerElement = 0.1f;
'''

FIXED = '''
        [SerializeField, Min(0)] private int pierceCount;

        [Header("Vessel strip")]
        [Tooltip("Normalized element levels this round strips.")]
        [SerializeField, Min(0f)] private float vesselStripPerElement = 0.1f;

        [Header("Impact")]
        [Tooltip("Debris speed the destroyed prism's pieces carry.")]
        [SerializeField, Min(0f)] private float debrisSpeed = 90f;
'''

# Shapes that are legal and must NOT fire.
CONTROLS = [
    ("one attribute per member, several members in a row", '''
        [SerializeField] bool ram;
        [SerializeField] bool drift;
        [Tooltip("a")] [SerializeField] float a;
        [Tooltip("b")] [SerializeField] float b;
'''),
    ("header + tooltip + comma-joined field attributes", '''
        [Header("Targeting")]
        [Tooltip("one")]
        [SerializeField, Range(0f, 1f)] float t;
'''),
    ("multi-line tooltip whose text contains brackets and quotes", '''
        [Tooltip("see Foo[3] and \\"bar\\" - " +
                 "a continuation line with [brackets] in it")]
        [SerializeField] float x;
'''),
    ("repeated attribute on DIFFERENT members separated by a blank line", '''
        [Tooltip("a")]
        [SerializeField] float a;

        [Tooltip("b")]
        [SerializeField] float b;
'''),
    ("attributes that legitimately allow multiple", '''
        [ContextMenu("a")]
        [ContextMenu("b")]
        void Foo() { }
'''),
]


def self_test():
    ok = True
    hits = scan_text(BROKEN)
    names = sorted(n for _, n, _ in hits)
    if names != ["Header", "Tooltip"]:
        print(f"  FAIL  positive control expected Header+Tooltip, got {names}")
        ok = False
    else:
        print(f"  ok    positive control fires: {names}")
    if scan_text(FIXED):
        print("  FAIL  the FIXED ordering still fires")
        ok = False
    else:
        print("  ok    the fixed ordering is clean")
    for label, text in CONTROLS:
        hits = scan_text(text)
        if hits:
            print(f"  FAIL  control fired: {label} -> {hits}")
            ok = False
        else:
            print(f"  ok    silent: {label}")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--root", default="Assets/_Scripts/")
    args = ap.parse_args()

    if args.self_test:
        print("self-test")
        sys.exit(0 if self_test() else 1)

    files = [f for f in subprocess.run(["git", "ls-files"], capture_output=True,
                                       text=True).stdout.split()
             if f.startswith(args.root) and f.endswith(".cs")]
    bad = 0
    for f in files:
        for start, name, ln in scan(f):
            print(f"{f}:{ln}: error CS0579: duplicate '{name}' attribute "
                  f"(attribute run starts at line {start})")
            bad += 1
    if bad:
        print(f"\nFAIL  {bad} duplicate attribute(s) over {len(files)} files")
        sys.exit(1)
    print(f"OK  no duplicate single-use attributes over {len(files)} files")


if __name__ == "__main__":
    main()

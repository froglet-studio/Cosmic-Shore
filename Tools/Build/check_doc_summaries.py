#!/usr/bin/env python3
"""Doc-comment gate: one <summary> per member.

    python3 Tools/Build/check_doc_summaries.py                # scan Assets/, exit 1 on any finding
    python3 Tools/Build/check_doc_summaries.py --check        # the same (CI spelling)
    python3 Tools/Build/check_doc_summaries.py --root Port    # report on another tree (not gated)
    python3 Tools/Build/check_doc_summaries.py --self-test

THE DEFECT. A member is moved or deleted and its `/// <summary>` stays behind, right above the next member's own
doc. The stale block then reads as that member's doc: an IDE tooltip shows it, a generated page prints it, and every
reader of the file - every Claude session included - is told the wrong thing about the member it sits on. On
2026-11-11 bleeding-edge carried 76 of them under Assets/. The /ship pass of cece/nifty-cray-41hy43 found the first
55 by looking for `/// </summary>` directly followed by `/// <summary>`; the other 21 closed the first block on a
text line, had a <param> or <returns> between the two, or had a blank line or a `//` comment between them, which
that scan cannot see.

THE RULE. A doc run holds at most one <summary>. A run is the `///` lines in front of a declaration plus anything
that can sit between them without being code: blank lines, `//` comments, `#region` / `#endregion` lines and
one-line attributes (`[SerializeField]`). Any other line ends the run, `#if` / `#else` included, so a member
declared once per platform branch, each with its own doc, passes.

FIXING A FINDING. Read the first block and find the member it describes: search the file for the names it uses
(a <paramref> naming a parameter the member under it does not have is the quickest tell), or run
`git log -S '<a phrase from it>'`, which in a shallow clone only reaches back to the graft, so prefer the code.
Move it onto that member, merging it with any doc the member already has.
Delete it when that member is gone. When both blocks really describe this member, merge them into one <summary>
(with <para> where needed). Change docs only, never code. A block above a commented-out declaration is not a doc:
make it a plain `//` comment beside that line.

Scope: Assets/ only. Port/ and Tools/ are not gated; `--root` reports on them.

A READER: it writes nothing into the repo.
"""
import os
import re
import shutil
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DEFAULT_ROOTS = ['Assets']
SKIP_DIRS = {'Library', 'Temp', 'Logs', 'obj', 'bin', 'node_modules', '.git'}
SUMMARY_OPEN = re.compile(r'<summary\s*>')


def classify(line):
    """'doc' for a `///` line, 'gap' for a line that can sit inside a doc run, 'code' for anything else."""
    s = line.strip()
    if s.startswith('///'):
        return 'doc'
    if not s or s.startswith('//') or s.startswith('#region') or s.startswith('#endregion'):
        return 'gap'
    if s.startswith('[') and s.endswith(']'):
        return 'gap'   # a one-line attribute
    return 'code'


def scan_text(text):
    """One (second <summary> line, <summary> count, first doc line, last doc line) per doc run holding more than one."""
    lines = text.split('\n')
    found = []
    i, n = 0, len(lines)
    while i < n:
        if classify(lines[i]) != 'doc':
            i += 1
            continue
        opens, last_doc, j = [], i, i
        while j < n:
            kind = classify(lines[j])
            if kind == 'code':
                break
            if kind == 'doc':
                last_doc = j
                opens += [j + 1] * len(SUMMARY_OPEN.findall(lines[j]))
            j += 1
        if len(opens) > 1:
            found.append((opens[1], len(opens), i + 1, last_doc + 1))
        i = j
    return found


def read_source(path):
    # utf-8-sig drops a BOM, so a first-line `///` is still seen; universal newlines fold CRLF.
    with open(path, encoding='utf-8-sig', errors='replace') as fh:
        return fh.read()


def cs_files(roots, base=ROOT):
    out = []
    for r in roots:
        for d, dirs, files in os.walk(os.path.join(base, r)):
            dirs[:] = sorted(x for x in dirs if x not in SKIP_DIRS)
            out += [os.path.join(d, f) for f in sorted(files) if f.endswith('.cs')]
    return out


def scan(roots=None, base=ROOT):
    """(findings as (repo-relative path, line, message), files scanned)."""
    roots = roots or DEFAULT_ROOTS
    findings = []
    files = cs_files(roots, base)
    for p in files:
        rel = os.path.relpath(p, base).replace(os.sep, '/')
        for line, count, first, last in scan_text(read_source(p)):
            findings.append((rel, line, '%d <summary> blocks in one doc comment (lines %d-%d): the first one '
                                        'probably belongs to a member that moved or was deleted - move it onto '
                                        'that member, merge the two, or delete it' % (count, first, last)))
    return findings, len(files)


SELF_TEST_CASES = [
    # (name, source, expected findings)
    ('two adjacent blocks (the /ship scan)', '''
        /// <summary>
        /// Moved away long ago.
        /// </summary>
        /// <summary>
        /// The real doc.
        /// </summary>
        void Member() {}''', 1),
    ('one-line block over a block', '''
        /// <summary>Shared vector helpers.</summary>
        /// <summary>
        /// The ledger.
        /// </summary>
        public static class BuilderLedger {}''', 1),
    ('summary and params, then a summary', '''
        /// <summary>SERVER: the blast reached the ball.</summary>
        /// <param name="origin">Where it came from.</param>
        /// <summary>Route a blast to the ball.</summary>
        public static void RequestBlast(Vector3 origin) {}''', 1),
    ('separated by a blank line', '''
        /// <summary>Orchestrates a retired handshake.</summary>

        /// <summary>Manages the party session.</summary>
        [Inject] private IPartySessionService _partySessionService;''', 1),
    ('separated by a comment and a region line', '''
        /// <summary>The winner's name.</summary>
        // -- Match envelope --
        #region Envelope
        /// <summary>Unique per game instance.</summary>
        public string MatchId = "";''', 1),
    ('a doc after an attribute', '''
        /// <summary>A doc for something else.</summary>
        [SerializeField]
        /// <summary>The field.</summary>
        float speed;''', 1),
    ('three blocks are one finding', '''
        /// <summary>a</summary>
        /// <summary>b</summary>
        /// <summary>c</summary>
        int x;''', 1),
    ('a BOM and CRLF line endings', '﻿/// <summary>a</summary>\r\n/// <summary>b</summary>\r\nint x;\r\n', 1),
    # Negative controls: none of these is a doubled summary.
    ('two members, one summary each', '''
        /// <summary>First.</summary>
        int a;
        /// <summary>Second.</summary>
        int b;''', 0),
    ('a doc over its attributes', '''
        /// <summary>The speed.</summary>
        [Header("Flight")]
        [SerializeField, Min(0f)] float speed;''', 0),
    ('one member per #if branch', '''
        #if UNITY_EDITOR
        /// <summary>Editor flavour.</summary>
        static void Log() {}
        #else
        /// <summary>Player flavour.</summary>
        static void Log() {}
        #endif''', 0),
    ('summary, params, returns and remarks', '''
        /// <summary>Does the thing.</summary>
        /// <param name="a">The a.</param>
        /// <returns>True when it did.</returns>
        /// <remarks>Rarely.</remarks>
        bool Do(int a) => true;''', 0),
    ('<summary> outside a doc comment', '''
        // a plain comment may say /// <summary> without being one
        /// <summary>The only doc.</summary>
        string tag = "<summary>";
        /// <summary>Next member.</summary>
        int y;''', 0),
    ('an escaped tag in prose', '''
        /// <summary>Every member gets one &lt;summary&gt; block, never two.</summary>
        int z;''', 0),
]


def self_test():
    bad = 0
    for name, text, want in SELF_TEST_CASES:
        # Undo the indentation the triple-quoted cases carry, then fold CRLF the way open() does.
        text = '\n'.join(l.strip() for l in text.replace('\r\n', '\n').split('\n'))
        got = scan_text(text.lstrip('﻿'))
        if len(got) != want:
            bad += 1
            print('SELF-TEST FAIL: %s -> %d finding(s), expected %d: %r' % (name, len(got), want, got))
    # The line it names is the SECOND <summary>, so the person goes straight to the seam.
    got = scan_text('int a;\n/// <summary>old</summary>\n/// <summary>new</summary>\nint b;')
    if got != [(3, 2, 2, 3)]:
        bad += 1
        print('SELF-TEST FAIL: the finding names %r, expected [(3, 2, 2, 3)]' % (got,))
    # End to end through the walker and main(): a planted file under Assets/ fails the gate, a clean one does not,
    # and a BOM + CRLF file is read the way an editor shows it.
    tmp = tempfile.mkdtemp(prefix='doc_summaries_')
    try:
        os.makedirs(os.path.join(tmp, 'Assets', 'Sub'))
        with open(os.path.join(tmp, 'Assets', 'Sub', 'Planted.cs'), 'w', encoding='utf-8-sig', newline='\r\n') as fh:
            fh.write('namespace X\n{\n    /// <summary>stale</summary>\n    /// <summary>real</summary>\n    class A {}\n}\n')
        with open(os.path.join(tmp, 'Assets', 'Clean.cs'), 'w', encoding='utf-8') as fh:
            fh.write('/// <summary>one</summary>\nclass B {}\n')
        findings, n = scan(['Assets'], base=tmp)
        if n != 2 or [(f, l) for f, l, _ in findings] != [('Assets/Sub/Planted.cs', 4)]:
            bad += 1
            print('SELF-TEST FAIL: planted tree gave %r over %d file(s)' % (findings, n))
        if run(['--root', 'Assets'], base=tmp, quiet=True) != 1:
            bad += 1
            print('SELF-TEST FAIL: main() passed a tree with a planted doubled summary')
        os.remove(os.path.join(tmp, 'Assets', 'Sub', 'Planted.cs'))
        if run(['--root', 'Assets'], base=tmp, quiet=True) != 0:
            bad += 1
            print('SELF-TEST FAIL: main() failed a clean tree')
        if run(['--root', 'NoSuchTree'], base=tmp, quiet=True) != 2:
            bad += 1
            print('SELF-TEST FAIL: a missing --root did not exit 2 (a gate that scans nothing must not pass)')
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    total = len(SELF_TEST_CASES) + 5
    print('doc summaries self-test: %s (%d cases)' % ('OK' if bad == 0 else '%d FAILED' % bad, total))
    return 1 if bad else 0


def run(argv, base=ROOT, quiet=False):
    def say(line):
        if not quiet:
            print(line)

    roots = []
    args = list(argv)
    while '--root' in args:
        k = args.index('--root')
        if k + 1 >= len(args):
            say('check_doc_summaries: --root needs a directory')
            return 2
        roots.append(args[k + 1])
        del args[k:k + 2]
    unknown = [a for a in args if a not in ('--check',)]
    if unknown:
        say('check_doc_summaries: unknown argument(s) %s' % ' '.join(unknown))
        return 2
    roots = roots or DEFAULT_ROOTS
    missing = [r for r in roots if not os.path.isdir(os.path.join(base, r))]
    if missing:
        say('check_doc_summaries: no such directory: %s' % ', '.join(missing))
        return 2
    findings, n = scan(roots, base)
    for path, line, what in findings:
        say('%s:%d: %s' % (path, line, what))
    say('doc summaries: %d PROBLEM(S) in %d .cs file(s) under %s' % (len(findings), n, ', '.join(roots)))
    return 1 if findings else 0


def main(argv):
    if '--self-test' in argv:
        return self_test()
    return run(argv)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))

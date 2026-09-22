#!/usr/bin/env python3
"""Fail when a concrete class does not implement an inherited ABSTRACT member.

WHY THIS EXISTS
---------------
CS0534 ("does not implement inherited abstract member") and CS0115 ("no suitable
method found to override") need a SYMBOL TABLE, so they are structurally invisible
to every other out-of-editor gate in this repo — see CLAUDE.md, "Shipping C# you
could not compile". A bare Roslyn syntax pass is blind to them too, and blind in
the worst way: when a base type lives in the `Assembly-CSharp` monolith and cannot
be resolved, Roslyn ABANDONS class-body binding and reports *nothing*, which is
indistinguishable from clean.

It shipped: `ButterflyHUDView : VesselHUDView` never implemented the abstract
`Initialize()`, passed ten green gates and a syntax pass, and failed in the editor.
A new vessel is the highest-risk moment for it, because a vessel adds subclasses of
five or six abstract bases at once.

WHAT IT PROVES, AND WHAT IT DOES NOT
------------------------------------
It is TEXTUAL, on purpose: no compiler, ~2s over the whole tree. It matches on
member NAME, never on the full signature, so it cannot see an override whose
parameter list drifted (that is CS0115 and stays editor-only). It reports a class
only when the member name appears nowhere in it -- the unambiguous case.

Conservatively silent rather than noisy, since a gate that cries wolf is one
nobody reads:
  * a class whose base chain leaves the tree (a Unity or package type) is skipped
    at that link -- we only know about first-party abstracts
  * `abstract` and `partial` classes are never reported
  * a name declared anywhere in the class (override, new, a field, whatever)
    counts as satisfied

Usage:  python3 Tools/Build/check_abstract_member_implementations.py [--check] [--self-test]
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCAN = os.path.join(ROOT, "Assets", "_Scripts")

# A type declaration, with an optional base list.
TYPE_RE = re.compile(
    r"^\s*(?P<mods>(?:(?:public|internal|protected|private|abstract|sealed|static|partial|unsafe)\s+)*)"
    r"(?P<kind>class|struct|interface)\s+(?P<name>[A-Za-z_]\w*)"
    r"(?P<generic><[^>{]*>)?"
    r"\s*(?::\s*(?P<bases>[^{]*?))?\s*(?:where\b[^{]*)?$"
)

ABSTRACT_MEMBER_RE = re.compile(
    r"^\s*(?:public|protected|internal|protected\s+internal)\s+(?:[\w<>\[\],\s\.\?]*?\s)?abstract\s+"
    r"(?P<sig>[^;{]*);"
)

# Pull the member name out of an abstract declaration's signature tail:
#   "void Initialize()"           -> Initialize
#   "Vector3 Pivot { get; }"      -> Pivot
#   "void Do<T>(T a)"             -> Do
MEMBER_NAME_RE = re.compile(r"(?P<name>[A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*(?:\(|\{|=>|$)")


def member_name(tail):
    """The declared member's name in `tail`, which is everything after `abstract`/`override`.

    The FIRST identifier followed by '(' (method), '<...>(' (generic method), '{' or
    '=>' (property) -- never the last, because an expression body puts calls after it
    (`=> HashCode.Combine(...)` would otherwise read as a member named `Combine`).
    """
    m = re.search(r"(?P<name>[A-Za-z_]\w*)\s*(?:<[^>]*>\s*)?(?=\(|\{|=>|;|$)", tail.strip())
    return m.group('name') if m else None


def strip_comments(text):
    out, i, n = [], 0, len(text)
    while i < n:
        c = text[i]
        if c == '/' and i + 1 < n and text[i + 1] == '/':
            j = text.find('\n', i)
            i = n if j < 0 else j
        elif c == '/' and i + 1 < n and text[i + 1] == '*':
            j = text.find('*/', i + 2)
            i = n if j < 0 else j + 2
        elif c in '"\'':
            q, i = c, i + 1
            out.append(q)
            while i < n:
                if text[i] == '\\':
                    out.append(text[i:i + 2]); i += 2; continue
                out.append(text[i])
                if text[i] == q:
                    i += 1; break
                i += 1
            continue
        else:
            out.append(c); i += 1
    return ''.join(out)


def base_names(raw):
    """Split a base list into bare type names (drops generics and namespaces)."""
    if not raw:
        return []
    depth, cur, parts = 0, [], []
    for ch in raw:
        if ch in '<(':
            depth += 1
        elif ch in '>)':
            depth -= 1
        if ch == ',' and depth == 0:
            parts.append(''.join(cur)); cur = []
        else:
            cur.append(ch)
    if cur:
        parts.append(''.join(cur))
    names = []
    for p in parts:
        p = p.strip()
        p = re.sub(r"<.*", "", p).strip()
        p = p.split('.')[-1]
        if p:
            names.append(p)
    return names


def body_span(src, decl_end):
    """The brace-matched body that follows a declaration ending at `decl_end`."""
    i = src.find('{', decl_end)
    if i < 0:
        return ''
    depth = 0
    for j in range(i, len(src)):
        c = src[j]
        if c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0:
                return src[i + 1:j]
    return src[i + 1:]


def parse(paths):
    """{type name: {file, line, kind, abstract, partial, bases, abstract_members, names}}"""
    types = {}
    for path in paths:
        try:
            raw = open(path, encoding='utf-8-sig').read()
        except (OSError, UnicodeDecodeError):
            continue
        src = strip_comments(raw)
        lines = src.split('\n')
        offsets, acc = [], 0
        for l in lines:
            offsets.append(acc)
            acc += len(l) + 1
        for idx, line in enumerate(lines):
            head = line.split('{', 1)[0]
            m = TYPE_RE.match(head.rstrip())
            if not m or m.group('kind') == 'struct':
                continue
            mods = m.group('mods') or ''
            name = m.group('name')
            text = body_span(src, offsets[idx] + len(head))
            body = text.split('\n')
            abstracts = {}
            for bl in body:
                am = ABSTRACT_MEMBER_RE.match(bl)
                if not am:
                    continue
                mn = member_name(am.group('sig'))
                if mn:
                    abstracts[mn] = bl.strip()
            entry = types.setdefault(name, {
                'file': os.path.relpath(path, ROOT), 'line': idx + 1,
                'abstract': 'abstract' in mods.split(), 'partial': 'partial' in mods.split(),
                'bases': [], 'abstract_members': {}, 'names': set(),
            })
            entry['bases'] += base_names(m.group('bases'))
            entry['abstract_members'].update(abstracts)
            for bl in body:
                if not re.search(r"\boverride\b", bl):
                    continue
                tail = re.split(r"\boverride\b", bl, 1)[1]
                mn = member_name(tail)
                if mn:
                    entry['names'].add(mn)
            if 'abstract' in mods.split():
                entry['abstract'] = True
            if 'partial' in mods.split():
                entry['partial'] = True
    return types


def ancestors(types, name, seen=None):
    """Every first-party type up `name`'s base chain."""
    seen = seen or set()
    out = []
    for b in types.get(name, {}).get('bases', []):
        if b in seen or b not in types:
            continue
        seen.add(b)
        out.append(b)
        out += ancestors(types, b, seen)
    return out


def inherited_abstracts(types, name):
    """Abstract members declared anywhere above `name`, with the type that declared each."""
    out = {}
    for b in ancestors(types, name):
        for mn, sig in types[b]['abstract_members'].items():
            out.setdefault(mn, (b, sig))
    return out


def scan(paths):
    types = parse(paths)
    findings = []
    for name, t in sorted(types.items()):
        if t['abstract'] or t['partial']:
            continue
        # In C#, the ONLY thing that satisfies an inherited abstract member is an
        # `override` — on this class or on an intermediate base. Nothing else counts, so
        # this test is exact rather than a heuristic.
        overrides = set(t['names'])
        for a in ancestors(types, name):
            overrides |= types[a]['names']
        for mn, (owner, sig) in sorted(inherited_abstracts(types, name).items()):
            if mn in overrides:
                continue
            findings.append((t['file'], t['line'], name, owner, mn, sig))
    return types, findings


def all_sources(root):
    out = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in ('obj', 'bin', 'Temp')]
        out += [os.path.join(dirpath, f) for f in filenames if f.endswith('.cs')]
    return sorted(out)


def self_test():
    """Negative controls: the shipped bug must fire, four legal shapes must not."""
    import tempfile
    cases = {
        # 1. the shipped bug
        'bad.cs': "public abstract class B { public abstract void Initialize(); }\n"
                  "public class D : B { public void Other() {} }\n",
        # 2. implemented
        'ok_impl.cs': "public abstract class B2 { public abstract void Go(); }\n"
                      "public class D2 : B2 { public override void Go() {} }\n",
        # 3. subclass is itself abstract
        'ok_abs.cs': "public abstract class B3 { public abstract void Go3(); }\n"
                     "public abstract class D3 : B3 { }\n",
        # 4. abstract PROPERTY, implemented
        'ok_prop.cs': "public abstract class B4 { public abstract int Size { get; } }\n"
                      "public class D4 : B4 { public override int Size => 3; }\n",
        # 5. base outside the scanned tree -> unknowable, must stay silent
        'ok_extern.cs': "public class D5 : SomeUnityType { }\n",
        # 6. two links up
        'bad2.cs': "public abstract class G { public abstract void Deep(); }\n"
                   "public abstract class M : G { }\n"
                   "public class L : M { }\n",
        # 7. the member name only appears inside a COMMENT -> must still fire
        'bad3.cs': "public abstract class B7 { public abstract void Only(); }\n"
                   "public class D7 : B7 { /* Only is documented here */ }\n",
    }
    expect_fire = {'D', 'L', 'D7'}
    with tempfile.TemporaryDirectory() as td:
        paths = []
        for fn, body in cases.items():
            p = os.path.join(td, fn)
            open(p, 'w').write(body)
            paths.append(p)
        _, findings = scan(sorted(paths))
    fired = {f[2] for f in findings}
    ok = fired == expect_fire
    print("self-test: fired on %s (expected %s)" % (sorted(fired) or '-', sorted(expect_fire)))
    print("self-test: %s" % ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def main():
    args = sys.argv[1:]
    if '--self-test' in args:
        return self_test()

    paths = all_sources(SCAN)
    types, findings = scan(paths)
    print("scanned %d files, %d types, %d abstract members"
          % (len(paths), len(types), sum(len(t['abstract_members']) for t in types.values())))

    if not findings:
        print("OK: every concrete class implements its inherited abstract members")
        return 0

    print("\n%d unimplemented abstract member(s):\n" % len(findings))
    for f, line, cls, owner, mn, sig in findings:
        print("  %s:%d  %s does not implement %s.%s" % (f, line, cls, owner, mn))
        print("      %s" % sig)
    print("\nThis is CS0534. Implement the member, or mark the class abstract.")
    return 1


if __name__ == '__main__':
    sys.exit(main())

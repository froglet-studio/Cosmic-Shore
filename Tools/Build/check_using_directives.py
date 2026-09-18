#!/usr/bin/env python3
"""
Catch the ONE error class an out-of-editor syntax check is structurally blind to:
a type used without a `using` for the namespace that declares it.

    python3 Tools/Build/check_using_directives.py                # files changed vs the base ref
    python3 Tools/Build/check_using_directives.py <path ...>     # specific files or folders
    python3 Tools/Build/check_using_directives.py --all          # whole project (see the LIMIT)
    python3 Tools/Build/check_using_directives.py --self-test

WHY THIS EXISTS. CLAUDE.md already records that a `dotnet build` over changed C# with no Unity
assemblies is a SYNTAX gate and nothing more, because Roslyn abandons class-body binding when the
base type is unresolved. Compiling one file against hand-written stubs has the mirror problem: it
proves that file and says nothing about its five siblings. Both let
`error CS0246: The type or namespace name 'GameDataSO' could not be found` reach the editor.

This resolves declarations from the REPO rather than from a compiler: it indexes every
`namespace X { ... class/struct/interface/enum Y }` in Assets/_Scripts, then for each file asks
whether every type it mentions is reachable from that file's own namespace plus its usings.

WHAT IT DELIBERATELY DOES NOT DO. It reports only types whose declaring namespace is UNAMBIGUOUS
within Assets/_Scripts and entirely absent from the file's reach - never a guess. A name declared
in two first-party namespaces, and a name reachable through any using, are silent.

THE LIMIT, STATED PLAINLY, because a gate that overstates its reach is worse than none:
it indexes FIRST-PARTY declarations only, so it cannot see that `Key`, `Direction`, `Frame` or
`Stats` also exist in UnityEngine, Unity.InputSystem or NUnit. A first-party type sharing a name
with one of those looks unambiguous to this script and gets reported. Measured on the shipped
tree: 143 such reports across 1,849 files, every one a false positive.

That is why the DEFAULT is the files changed against the base ref rather than the whole project.
On a handful of new files the signal is exact and the output is short enough to read; run --all
only when you are prepared to triage. Do NOT wire --all into CI as a blocking gate without an
allowlist for the shadowed names.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")

DECL = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|protected|private|sealed|abstract|static|partial|readonly|\s)*"
    r"\b(?:class|struct|interface|enum|record)\s+([A-Z]\w*)", re.M)
NS = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)
USING = re.compile(r"^\s*using\s+(?:static\s+)?([\w.]+)\s*;", re.M)
ALIAS = re.compile(r"^\s*using\s+\w+\s*=", re.M)
ENUM_BODY = re.compile(r"\benum\s+[A-Z]\w*[^{}]*\{([^{}]*)\}", re.S)
ENUM_MEMBER = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*([A-Z]\w*)\s*(?:=.*)?$", re.S)
# A MEMBER DECLARATION: the identifier in DECLARATOR position. Same argument as the enum
# members above, one level over -- at its own declaration the name is a MEMBER, not a type
# reference, so a member sharing a type's name elsewhere reads as a missing using for a type
# the file never mentions (`void Pulse()`, `const string WeeklyChallenge = ...`).
#
# METHODS and FIELDS need DIFFERENT suppressions, and conflating them fails one way or the other:
#   - A method is also written bare at every CALL SITE (`Pulse();`), which is not a declaration,
#     so blanking only the declaration leaves every call reported. Its NAME goes in `own`.
#   - A field must NOT go in `own`: `public WidgetSO WidgetSO;` needs a using for WidgetSO, and a
#     name-set suppression would hide the very reference that needs it. Only the declarator
#     OCCURRENCE is blanked, leaving the TYPE position reported.
# Both match the name in DECLARATOR position only, never the TYPE position the regex consumes on
# the way there. `new` is deliberately NOT a listed modifier: it would let `new Vector3(...)` read
# as a declaration and blind the gate to the constructor call, which is exactly where a using is
# needed.
_MODS = (r"(?:(?:public|private|protected|internal|static|readonly|const|virtual|override|abstract"
         r"|sealed|extern|async|partial|unsafe|volatile|event|required|ref)[ \t]+)*")
_HEAD = r"^[ \t]*(?:\[[^\]]*\][ \t]*)*" + _MODS + r"[\w.<>,\[\]\?]+[ \t]+"
METHOD_DECL = re.compile(_HEAD + r"([A-Z]\w*)[ \t]*(?=\()", re.M)
FIELD_DECL = re.compile(_HEAD + r"([A-Z]\w*)[ \t]*(?==>|=[^=]|;|\{)", re.M)
# A type mention: an identifier starting uppercase, not preceded by a dot (which would make it a
# member access or an already-qualified name).
MENTION = re.compile(r"(?<![\w.])([A-Z]\w{2,})\b")

COMMENT = re.compile(r"//.*?$|/\*.*?\*/", re.S | re.M)
STRING = re.compile(r'"(?:\\.|[^"\\])*"|\$@?"(?:[^"]|"")*"')
# `#region <free text>` is PROSE, not code -- C# lets the label be anything to end of line and
# does not require quotes, so an ordinary section heading like `#region Player Profile` reads as
# a bare type reference and reports a missing `using` for a file that compiles perfectly.
# `#error` / `#warning` take free text the same way. (`#if`/`#pragma` take real identifiers and
# are deliberately left alone.)
DIRECTIVE_TEXT = re.compile(r"^[ \t]*#[ \t]*(?:region|endregion|error|warning)\b.*?$", re.M)


def strip(src: str) -> str:
    return STRING.sub('""', COMMENT.sub(" ", DIRECTIVE_TEXT.sub(" ", src)))


def index_declarations():
    """type name -> set of namespaces declaring it."""
    out = {}
    for root, _, files in os.walk(SCRIPTS):
        for f in files:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(root, f)
            try:
                src = strip(open(p, encoding="utf-8", errors="ignore").read())
            except OSError:
                continue
            m = NS.search(src)
            ns = m.group(1) if m else ""
            # A FILE-SCOPED namespace (`namespace X;`) opens no brace, so its types sit at
            # depth 0. The project has none today; without this the day one lands its whole
            # file drops out of the index silently, which is the failure mode this checker is
            # least able to notice about itself.
            braced = bool(m) and src[m.end():m.end() + 40].lstrip().startswith("{")
            for name in top_level_declarations(src, braced):
                out.setdefault(name, set()).add(ns)
    return out


def top_level_declarations(src, in_namespace):
    """
    Type names a `using <namespace>;` can actually REACH - i.e. types at the namespace's own
    brace depth, never types nested inside another type.

    A nested type is not addressable by importing its namespace at all (it is
    `Outer.Inner`, and a private one is not addressable from outside `Outer` at any price), so
    indexing one makes the checker demand a using that cannot help. It cost a real false
    positive: a private `struct Pose` inside a test class made every first-party file that
    mentions UnityEngine's Pose look like it was missing `using CosmicShore.Tests;`.

    Depth is counted over the COMMENT- AND STRING-STRIPPED source, so a brace in either cannot
    move it.
    """
    names, depth, i, n = [], 0, 0, len(src)
    want = 1 if in_namespace else 0
    marks = {m.start(): m.group(1) for m in DECL.finditer(src)}
    while i < n:
        if i in marks and depth == want:
            names.append(marks[i])
        c = src[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
        i += 1
    return names


def reachable(ns: str, usings: set) -> set:
    """A file can see its own namespace, every ancestor of it, and everything it uses."""
    r = set(usings)
    parts = ns.split(".") if ns else []
    for i in range(len(parts), 0, -1):
        r.add(".".join(parts[:i]))
    r.add("")
    return r


def check_file(path, decls):
    src_raw = open(path, encoding="utf-8", errors="ignore").read()
    if ALIAS.search(src_raw):
        return []                      # a using-alias file: out of scope, stay silent
    src = strip(src_raw)
    m = NS.search(src)
    ns = m.group(1) if m else ""
    reach = reachable(ns, set(USING.findall(src)))

    # Types this file declares itself are always in scope.
    own = set(DECL.findall(src))

    # So are the MEMBERS of enums it declares. An enum member is written bare at its
    # declaration (`Prism,`) and is not a type reference at all, so a member that happens to
    # share a name with a type elsewhere — `PrismRenderOverrideSet.Prism` against the Prism
    # class — reads as a missing using for a type the file never mentions. A gate that cries
    # wolf is a gate nobody reads, and this one is scoped to CHANGED files, so the noise
    # arrives attached to somebody's unrelated edit.
    for body in ENUM_BODY.findall(src):
        for member in body.split(","):
            m = ENUM_MEMBER.match(member)
            if m:
                own.add(m.group(1))

    # And so are the METHODS it declares -- by NAME, because a method is written bare at every
    # call site too. A FIELD or PROPERTY is suppressed only at its declarator OCCURRENCE, so its
    # own TYPE is still reported. See METHOD_DECL / FIELD_DECL.
    own.update(METHOD_DECL.findall(src))
    scan = FIELD_DECL.sub(lambda m: m.group(0)[:m.start(1) - m.start(0)], src)

    bad = []
    for name in sorted(set(MENTION.findall(scan))):
        if name in own:
            continue
        where = decls.get(name)
        if not where or len(where) != 1:
            continue                   # unknown or ambiguous: never guess
        declared = next(iter(where))
        if declared in reach:
            continue
        bad.append((name, declared))
    return bad


def self_test():
    """A gate nobody has watched FAIL is a gate nobody should trust."""
    import tempfile
    decls = {"WidgetSO": {"CosmicShore.Utility"}, "Thing": {"CosmicShore.Data"}}
    cases = [
        ("using CosmicShore.Data;\nnamespace CosmicShore.Gameplay { class A { WidgetSO w; } }", 1,
         "missing using is reported"),
        ("using CosmicShore.Data;\nusing CosmicShore.Utility;\nnamespace CosmicShore.Gameplay { class A { WidgetSO w; } }", 0,
         "present using is silent"),
        ("namespace CosmicShore.Utility { class A { WidgetSO w; } }", 0,
         "own namespace is silent"),
        ('namespace CosmicShore.Gameplay { class A { string s = "WidgetSO"; } }', 0,
         "a name inside a STRING is not a reference"),
        ("namespace CosmicShore.Gameplay { class A { /* WidgetSO */ int x; } }", 0,
         "a name inside a COMMENT is not a reference"),
        ("namespace CosmicShore.Gameplay { class A { int x = Foo.WidgetSO; } }", 0,
         "a qualified member access is not a bare reference"),
        ("namespace CosmicShore.Gameplay {\n#region WidgetSO section\nclass A { int x; }\n#endregion\n}", 0,
         "a name in a #region LABEL is not a reference"),
        ("namespace CosmicShore.Gameplay {\n#region WidgetSO section\nclass A { WidgetSO w; }\n#endregion\n}", 1,
         "...but a real reference in the same file is still caught"),
        ("namespace CosmicShore.Gameplay { enum E { WidgetSO, Other } }", 0,
         "an enum MEMBER sharing a type's name is not a reference"),
        ("namespace CosmicShore.Gameplay { enum E { Other } class A { WidgetSO w; } }", 1,
         "...but a real reference beside that enum is still caught"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    void WidgetSO() { }\n}\n}", 0,
         "a METHOD declaration sharing a type's name is not a reference"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    void WidgetSO() { }\n    void B() {\n        WidgetSO();\n    }\n}\n}", 0,
         "...and neither is a CALL to it -- the shape that motivated the split"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    public const string WidgetSO = \"x\";\n}\n}", 0,
         "a CONST declaration sharing a type's name is not a reference"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    public int WidgetSO => 1;\n}\n}", 0,
         "a PROPERTY declaration sharing a type's name is not a reference"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    void Ok() { }\n    WidgetSO w;\n}\n}", 1,
         "...but a real reference beside those members is still caught"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    void Ok() {\n        var v = new WidgetSO();\n    }\n}\n}", 1,
         "a CONSTRUCTOR call is a reference -- `new` must not read as a declaration"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    public WidgetSO Field;\n}\n}", 1,
         "a FIELD's TYPE is a reference even though its name is a declarator"),
        ("namespace CosmicShore.Gameplay {\nclass A {\n    public WidgetSO WidgetSO;\n}\n}", 1,
         "a field named after its OWN type still reports the TYPE"),
    ]
    ok = True
    for src, want, label in cases:
        with tempfile.NamedTemporaryFile("w", suffix=".cs", delete=False) as f:
            f.write(src); p = f.name
        got = len(check_file(p, decls))
        os.unlink(p)
        status = "ok " if got == want else "FAIL"
        if got != want: ok = False
        print(f"  [{status}] {label}  (expected {want}, got {got})")
    return 0 if ok else 1


def _git(args):
    """Run a git command, returning (returncode, stdout). Never raises."""
    import subprocess
    try:
        out = subprocess.run(["git"] + args, cwd=ROOT, capture_output=True,
                             text=True, timeout=30)
        return out.returncode, out.stdout
    except Exception:
        return 1, ""


def working_tree_files():
    """Uncommitted .cs files - a pre-commit run is mostly ABOUT these."""
    rc, out = _git(["status", "--porcelain"])
    if rc != 0:
        return []
    names = []
    for line in out.split("\n"):
        # Porcelain is `XY PATH`; a rename is `R  OLD -> NEW` and only NEW exists to check.
        path = line[3:].strip()
        if " -> " in path:
            path = path.split(" -> ", 1)[1].strip()
        if path.endswith(".cs"):
            names.append(path)
    return names


def changed_files():
    """
    Files changed against the base ref - the default scope, where the signal is exact.

    Returns (scope_label, names). The FIRST base that RESOLVES wins, even when its diff is
    EMPTY: an empty diff is a real answer ("this branch has no committed changes yet"), not a
    reason to try the next base. Falling through on empty is how a STALE local `bleeding-edge`
    - 661 commits behind origin on the day this was found - silently became the base and widened
    this check from one file to 411, reporting 15 pre-existing findings that belonged to nobody's
    change. The header above says never to wire the project-wide scope in as a blocking gate;
    that promise is only kept if the scope cannot widen itself by accident. Uncommitted work is
    always unioned in, and the chosen scope is always printed.
    """
    for base in ("origin/bleeding-edge", "bleeding-edge", "HEAD"):
        if _git(["rev-parse", "--verify", "--quiet", base])[0] != 0:
            continue
        rc, out = _git(["diff", "--name-only", f"{base}...HEAD"])
        if rc != 0:
            continue
        names = [n for n in out.split("\n") if n.endswith(".cs")]
        extra = [n for n in working_tree_files() if n not in names]
        label = f"{base}...HEAD"
        if extra:
            label += " + uncommitted"
        return label, names + extra
    return "uncommitted only", working_tree_files()


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if "--self-test" in sys.argv:
        return self_test()

    args_were_explicit = bool(args)
    scope = "explicit paths"
    decls = index_declarations()
    if not args:
        if "--all" in sys.argv:
            args, scope = [SCRIPTS], "--all (WHOLE PROJECT - expect false positives)"
        else:
            scope, args = changed_files()
        if not args:
            print(f"using-directive check: no changed .cs files to check (scope: {scope})")
            return 0
    targets = []
    for a in args:
        full = a if os.path.isabs(a) else os.path.join(ROOT, a)
        if not os.path.exists(full):
            continue
        if os.path.isfile(full):
            targets.append(full)
        else:
            for root, _, files in os.walk(full):
                targets += [os.path.join(root, f) for f in files if f.endswith(".cs")]

    problems = 0
    for p in sorted(targets):
        for name, declared in check_file(p, decls):
            print(f"{os.path.relpath(p, ROOT)}: '{name}' is declared in '{declared}' "
                  f"- add `using {declared};`")
            problems += 1

    scope_note = f", scope: {scope}" if not args_were_explicit else ""
    print(f"using-directive check: {'OK' if not problems else str(problems) + ' PROBLEM(S)'} "
          f"({len(targets)} files, {len(decls)} types indexed{scope_note})")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())

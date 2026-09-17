#!/usr/bin/env python3
"""A file-mode Custom Function node's HLSL signature must match the call the node writes.

THE RULE, MEASURED RATHER THAN ASSUMED. A file-mode Custom Function node does not
generate its function — it calls the one in the .hlsl file — and it builds that call as
ALL ITS INPUT SLOTS AND THEN ALL ITS OUTPUT SLOTS. Slot IDS do not decide the order:
TextMesh Pro's own shipped graphs prove it, since `Composite` carries inputs 0 and 3
around an output 2 and calls `Composite_float(float4, float4, out float4)` perfectly
happily. So what a signature has to satisfy is narrow and exact:

    the parameter list is <one per input slot>, then <one per output slot>,
    and only the trailing ones are `out`.

WHY IT IS A GATE. Break it and the graph does not compile, which means EVERY material
drawn with it renders with no material — and nothing outside the editor says so. A
verifier that compiles the .hlsl sees a perfectly good function; the call it would have
to read is in the generated shader, not in any file on disk. It is inviting to break
because HLSL itself permits an `in` parameter after an `out` one: appending a new input
as the highest slot id and declaring it last leaves every existing slot id and every
existing edge untouched, and it reads as free. Shipped 2026-09-17 — that exact edit put
`ErosionThreshold` where `out float Alpha` was declared and turned every prism in the
game into an unmaterialed magenta box.

Pure Python, no Unity. Sweeps every .shadergraph under Assets/ and resolves each node's
`m_FunctionSource` guid to its .hlsl. `--self-test` is its own negative control.
"""

import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROOT = os.path.join(REPO, "Assets")


def load_docs(text):
    """A .shadergraph is a stream of concatenated JSON documents, not one document."""
    decoder = json.JSONDecoder()
    docs, i, n = [], 0, len(text)
    while i < n:
        while i < n and text[i] in " \t\r\n":
            i += 1
        if i >= n:
            break
        obj, i = decoder.raw_decode(text, i)
        docs.append(obj)
    return docs


def guid_index():
    """guid -> path, for every .hlsl in the tree (the node stores only the guid)."""
    out = {}
    for base, _, files in os.walk(ROOT):
        for f in files:
            if not f.endswith(".hlsl.meta"):
                continue
            path = os.path.join(base, f)
            try:
                for line in open(path, encoding="utf-8", errors="ignore"):
                    if line.startswith("guid:"):
                        out[line.split(":", 1)[1].strip()] = path[: -len(".meta")]
                        break
            except OSError:
                pass
    return out


PARAM_SPLIT = re.compile(r",(?![^(]*\))")


def signature(hlsl_text, function_name):
    """The `out` pattern of `<function_name>_float`, or None if it is not in this file.

    Returns a list of bools, one per parameter, True where the parameter is an `out`.
    """
    for suffix in ("_float", "_half"):
        m = re.search(r"\bvoid\s+" + re.escape(function_name + suffix) + r"\s*\(",
                      hlsl_text)
        if not m:
            continue
        i = m.end()
        depth, start = 1, i
        while i < len(hlsl_text) and depth:
            if hlsl_text[i] == "(":
                depth += 1
            elif hlsl_text[i] == ")":
                depth -= 1
            i += 1
        params = hlsl_text[start:i - 1]
        # Strip comments so a `// out ...` note cannot be read as a parameter.
        params = re.sub(r"//[^\n]*", " ", params)
        params = re.sub(r"/\*.*?\*/", " ", params, flags=re.S)
        parts = [p.strip() for p in PARAM_SPLIT.split(params) if p.strip()]
        return [bool(re.match(r"\b(out|inout)\b", p)) for p in parts]
    return None


def check_graph(path, guids):
    """(function, problem) for every file-mode node whose signature cannot be the call."""
    problems = []
    text = open(path, encoding="utf-8").read()
    docs = load_docs(text)
    by_id = {d["m_ObjectId"]: d for d in docs if isinstance(d, dict) and "m_ObjectId" in d}
    for d in docs:
        if not isinstance(d, dict):
            continue
        fn = d.get("m_FunctionName")
        src = d.get("m_FunctionSource")
        # String-mode nodes generate their own function from the same slot list, so the
        # two can never disagree — there is nothing here to check.
        if not fn or not src:
            continue
        ins = outs = 0
        for ref in d.get("m_Slots", []):
            slot = by_id.get(ref.get("m_Id"))
            if not slot:
                continue
            if slot.get("m_SlotType") == 1:
                outs += 1
            else:
                ins += 1
        hlsl = guids.get(src)
        if not hlsl or not os.path.exists(hlsl):
            continue  # a package or missing file; not this gate's business
        pattern = signature(open(hlsl, encoding="utf-8", errors="ignore").read(), fn)
        if pattern is None:
            continue  # declared elsewhere (an include), or not a void _float entry point
        want = [False] * ins + [True] * outs
        if pattern != want:
            got = "".join("O" if p else "i" for p in pattern)
            exp = "".join("O" if p else "i" for p in want)
            problems.append((fn, os.path.relpath(hlsl, REPO), exp, got))
    return problems


def self_test():
    ok = True

    sig = signature("void F_float(float a, float b, out float c)\n{\n}\n", "F")
    if sig != [False, False, True]:
        print(f"SELF-TEST FAIL: plain signature read as {sig}"); ok = False

    # THE SHAPE THAT BROKE EVERY PRISM: an input declared after the outs.
    sig = signature("void F_float(float a, out float b, out float c, float d)\n{}\n", "F")
    if sig != [False, True, True, False]:
        print(f"SELF-TEST FAIL: the appended-input shape read as {sig}"); ok = False
    if sig == [False, False, True, True]:
        print("SELF-TEST FAIL: the broken shape was normalised away"); ok = False

    # A comment mentioning `out`, and a multi-line list, must not shift the pattern.
    sig = signature("void F_float(float a, // out of band\n    out float b)\n{}\n", "F")
    if sig != [False, True]:
        print(f"SELF-TEST FAIL: comment handling read as {sig}"); ok = False

    # A function this file does not declare is not this gate's business.
    if signature("void Other_float(out float a){}", "F") is not None:
        print("SELF-TEST FAIL: reported a signature it never found"); ok = False

    print("self-test: PASS" if ok else "self-test: FAIL")
    return 0 if ok else 1


def main():
    if "--self-test" in sys.argv:
        return self_test()

    guids = guid_index()
    graphs, bad = 0, []
    for base, _, files in os.walk(ROOT):
        for f in files:
            if not f.endswith(".shadergraph"):
                continue
            path = os.path.join(base, f)
            graphs += 1
            for fn, hlsl, exp, got in check_graph(path, guids):
                bad.append((os.path.relpath(path, REPO), fn, hlsl, exp, got))

    for rel, fn, hlsl, exp, got in bad:
        print(f"{rel}\n  {fn} ({hlsl})\n    node calls  {exp}\n    signature   {got}",
              file=sys.stderr)
    if bad:
        print(f"\ncheck FAILED: {len(bad)} custom function signature(s) cannot match the call "
              f"the node writes (i = in, O = out; the node passes every input, then every "
              f"output).\nThe graph will not compile and everything drawn with it renders "
              f"with no material.", file=sys.stderr)
        return 1
    print(f"custom function signatures: OK ({graphs} shadergraph files)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""
Turn on alpha clipping for the OPAQUE prism materials so the camera->vessel occlusion
corridor (Docs/PRISM_ANIMATION.md §5 C1) can dissolve them without ever moving a prism
into the transparent queue.

Why this is needed: BlockGraph is an Opaque surface, and URP compiles the Alpha output
away entirely on an opaque material unless `_ALPHATEST_ON` is enabled. The corridor's
shader-side fade therefore reaches the screen only on materials that opt into alpha
test. Transparent BlockGraph materials (cloak / transparent shielded / danger /
super-shielded) need no change: they already blend, so the corridor's alpha multiply
works on them as-is.

Also normalises `_Alpha` to 1 on the opaque materials. It was dead data before (opaque
+ no clip => the Alpha output is discarded), so several of them carry a stale 0 — which
would clip the prism away ENTIRELY the moment alpha test is on. This is the single
riskiest byte in the change, so it is asserted rather than assumed.

Idempotent. Run with --check to verify without writing (exit 1 if not applied).
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BLOCKGRAPH_SHADER_GUID = "bf8c159f627e64b439094797bff88611"
MATERIAL_ROOTS = ["Assets/_Graphics/Materials"]
KEYWORD = "_ALPHATEST_ON"


def find_blockgraph_materials():
    hits = []
    for root in MATERIAL_ROOTS:
        for dirpath, _dirs, files in os.walk(os.path.join(REPO, root)):
            for f in files:
                if not f.endswith(".mat"):
                    continue
                path = os.path.join(dirpath, f)
                text = open(path, encoding="utf-8", errors="ignore").read()
                if BLOCKGRAPH_SHADER_GUID in text:
                    hits.append((path, text))
    return sorted(hits)


def get_float(text, name):
    m = re.search(rf"^    - {re.escape(name)}: (-?[\d.eE+]+)$", text, re.M)
    return float(m.group(1)) if m else None


def is_opaque(text):
    return get_float(text, "_Surface") == 0.0


def apply(path, text, check_only):
    changes = []

    # 1. _AlphaClip -> 1
    new, n = re.subn(r"^    - _AlphaClip: 0$", "    - _AlphaClip: 1", text, count=1, flags=re.M)
    if n:
        changes.append("_AlphaClip 0->1")
        text = new
    elif get_float(text, "_AlphaClip") != 1.0:
        raise AssertionError(f"{path}: no '_AlphaClip' float to set")

    # 2. _Alpha -> 1 (a stale 0 here would clip the prism out of existence)
    alpha = get_float(text, "_Alpha")
    if alpha is None:
        raise AssertionError(f"{path}: no '_Alpha' float")
    if alpha != 1.0:
        new, n = re.subn(rf"^    - _Alpha: {re.escape(repr(alpha).rstrip('0').rstrip('.'))}$",
                         "    - _Alpha: 1", text, count=1, flags=re.M)
        if n != 1:
            new, n = re.subn(r"^    - _Alpha: [-\d.eE+]+$", "    - _Alpha: 1", text, count=1, flags=re.M)
        assert n == 1, f"{path}: could not rewrite _Alpha"
        changes.append(f"_Alpha {alpha}->1")
        text = new

    # 3. enable the shader_feature keyword (without it URP compiles the clip out)
    if KEYWORD not in text:
        new, n = re.subn(r"^  m_ValidKeywords: \[\]$", f"  m_ValidKeywords:\n  - {KEYWORD}",
                         text, count=1, flags=re.M)
        if n != 1:
            # non-empty keyword list: append as a sibling entry
            new, n = re.subn(r"^  m_ValidKeywords:\n", f"  m_ValidKeywords:\n  - {KEYWORD}\n",
                             text, count=1, flags=re.M)
        assert n == 1, f"{path}: could not add {KEYWORD} to m_ValidKeywords"
        changes.append(f"+{KEYWORD}")
        text = new

    if changes and not check_only:
        open(path, "w", encoding="utf-8").write(text)
    return changes, text


def verify(path):
    text = open(path, encoding="utf-8", errors="ignore").read()
    assert get_float(text, "_AlphaClip") == 1.0, f"{path}: _AlphaClip not 1"
    assert get_float(text, "_Alpha") == 1.0, f"{path}: _Alpha not 1"
    assert re.search(rf"^  - {KEYWORD}$", text, re.M), f"{path}: {KEYWORD} missing"
    assert re.search(r"^  m_InvalidKeywords: \[\]$", text, re.M) or KEYWORD not in \
        re.search(r"m_InvalidKeywords:.*?(?=\n  \w)", text, re.S).group(0), \
        f"{path}: {KEYWORD} landed in m_InvalidKeywords"


def main():
    check_only = "--check" in sys.argv
    mats = find_blockgraph_materials()
    assert mats, "no BlockGraph materials found — did the shader GUID change?"

    opaque = [(p, t) for p, t in mats if is_opaque(t)]
    transparent = [p for p, t in mats if not is_opaque(t)]
    assert opaque, "no opaque BlockGraph materials found"

    pending = []
    for path, text in opaque:
        changes, _ = apply(path, text, check_only=True)
        rel = os.path.relpath(path, REPO)
        if changes:
            pending.append((path, text, changes))
            print(f"  {'WOULD PATCH' if check_only else 'PATCH':11s} {rel}: {', '.join(changes)}")
        else:
            print(f"  {'ok':11s} {rel}")

    for path in transparent:
        print(f"  {'skip (blend)':11s} {os.path.relpath(path, REPO)}")

    if check_only:
        if pending:
            print(f"\nNOT applied: {len(pending)} material(s) still need alpha clip.", file=sys.stderr)
            return 1
        for path, _ in opaque:
            verify(path)
        print("\nAll opaque BlockGraph materials are alpha-clip enabled (verified).")
        return 0

    for path, text, _ in pending:
        apply(path, text, check_only=False)
    for path, _ in opaque:
        verify(path)
    print(f"\nPatched {len(pending)} of {len(opaque)} opaque BlockGraph materials; "
          f"{len(transparent)} transparent material(s) left alone (they blend). Verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""
Enforce the prism material contract for the screen-door transparency system
(Docs/PRISM_ANIMATION.md §4.7): EVERY prism-graph material is OPAQUE with alpha clipping
enabled. There are no transparent prism materials any more.

Why: the occlusion-corridor dither became THE prism transparency mechanism (2026-08-10) —
PrismOcclusionFade engages its kernel threshold for ANY fractional final alpha, wherever
the fragment is, not just inside the corridor. That covers the corridor fade, the
exploding-debris fade-out (PrismExplosionClock's Opacity), and the cloak family's authored
near-zero alpha, all through one pattern that composes in coverage and carries the same
depth parallax. A prism material in the transparent queue is therefore off-contract: it
pays sorting + blend overdraw + no depth write for an effect the opaque screen door
already provides, and it renders a SECOND, inconsistent kind of transparency next to the
dither. (PrismOcclusionDiagnostics.IsCorridorCapable screams at one in play mode; the
coverage test fails on one in CI. This tool is the fixer.)

Two jobs, both idempotent:
  1. Opaque materials missing alpha clip get `_AlphaClip 1` + `_ALPHATEST_ON` (URP
     compiles the Alpha output away entirely on an opaque surface without it).
  2. Transparent materials are CONVERTED to opaque + clip: surface/blend/queue flipped to
     the shipped opaque prism pattern (RenderType Opaque, queue 2000 — the same values
     MazeDangerBlockMateral proves out on ExplodingBlockGraph), `_SURFACE_TYPE_TRANSPARENT`
     retired, `_ALPHATEST_ON` enabled. Their authored `_Alpha` / `_Opacity` are PRESERVED —
     those values are now live dither coverage (the cloak family ships near-zero alpha on
     purpose), which is also why the old "_Alpha -> 1" normalisation step is gone: alpha
     is not dead data on any prism material any more, so this tool reports sub-1 values
     instead of "fixing" them.

Covers every graph a live prism can render with (the same census as
wire_prism_occlusion_corridor.py): BlockGraph and ExplodingBlockGraph.

Run with --check to verify without writing (exit 1 if the contract is not fully applied).
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PRISM_GRAPH_SHADER_GUIDS = {
    "bf8c159f627e64b439094797bff88611": "BlockGraph",
    "de59ec1f616f51044a23e6c1368d6660": "ExplodingBlockGraph",
}
MATERIAL_ROOTS = ["Assets/_Graphics/Materials"]
KEYWORD = "_ALPHATEST_ON"
TRANSPARENT_KEYWORD = "_SURFACE_TYPE_TRANSPARENT"

# The opaque prism pattern the converted materials adopt — the exact values the shipped
# opaque prism materials carry (MazeDangerBlockMateral is the proof on ExplodingBlockGraph).
OPAQUE_FLOATS = {
    "_Surface": "0",
    "_SrcBlend": "1",
    "_DstBlend": "0",
    "_SrcBlendAlpha": "1",
    "_DstBlendAlpha": "0",
    "_ZWrite": "1",
    "_ZWriteControl": "0",
    "_AlphaClip": "1",
    "_QueueOffset": "0",
}
OPAQUE_RENDER_QUEUE = "2000"


def find_prism_materials():
    hits = []
    for root in MATERIAL_ROOTS:
        for dirpath, _dirs, files in os.walk(os.path.join(REPO, root)):
            for f in files:
                if not f.endswith(".mat"):
                    continue
                path = os.path.join(dirpath, f)
                text = open(path, encoding="utf-8", errors="ignore").read()
                for guid, graph in PRISM_GRAPH_SHADER_GUIDS.items():
                    if re.search(rf"m_Shader: \{{fileID: -?\d+, guid: {guid}", text):
                        hits.append((path, text, graph))
                        break
    return sorted(hits)


def get_float(text, name):
    m = re.search(rf"^    - {re.escape(name)}: (-?[\d.eE+]+)$", text, re.M)
    return float(m.group(1)) if m else None


def set_float(text, name, value, changes):
    current = get_float(text, name)
    if current is None:
        raise AssertionError(f"no '{name}' float to set")
    if current == float(value):
        return text
    new, n = re.subn(rf"^(    - {re.escape(name)}: )-?[\d.eE+]+$", rf"\g<1>{value}",
                     text, count=1, flags=re.M)
    assert n == 1, f"could not rewrite {name}"
    changes.append(f"{name} {current:g}->{value}")
    return new


def is_opaque(text):
    return get_float(text, "_Surface") == 0.0


def add_keyword(text, keyword, changes):
    if re.search(rf"^  - {re.escape(keyword)}$", text, re.M):
        return text
    new, n = re.subn(r"^  m_ValidKeywords: \[\]$", f"  m_ValidKeywords:\n  - {keyword}",
                     text, count=1, flags=re.M)
    if n != 1:
        new, n = re.subn(r"^  m_ValidKeywords:\n", f"  m_ValidKeywords:\n  - {keyword}\n",
                         text, count=1, flags=re.M)
    assert n == 1, f"could not add {keyword} to m_ValidKeywords"
    changes.append(f"+{keyword}")
    return new


def remove_keyword(text, keyword, changes):
    new, n = re.subn(rf"^  - {re.escape(keyword)}\n", "", text, count=1, flags=re.M)
    if n:
        changes.append(f"-{keyword}")
        text = new
        # A keyword list emptied entirely must collapse back to the [] form.
        text = re.sub(r"^  m_ValidKeywords:\n(?=  m_InvalidKeywords)", "  m_ValidKeywords: []\n",
                      text, count=1, flags=re.M)
    return text


def apply(path, text, check_only):
    """Bring one material onto the contract. Returns (changes, new_text)."""
    changes = []

    if not is_opaque(text):
        # Convert transparent -> opaque + clip (job 2). Authored _Alpha/_Opacity preserved.
        for name, value in OPAQUE_FLOATS.items():
            if get_float(text, name) is not None:
                text = set_float(text, name, value, changes)
            elif name == "_AlphaClip":
                raise AssertionError(f"{path}: no '_AlphaClip' float to set")
        text = remove_keyword(text, TRANSPARENT_KEYWORD, changes)

        new, n = re.subn(r"^    RenderType: Transparent$", "    RenderType: Opaque",
                         text, count=1, flags=re.M)
        if n:
            changes.append("RenderType Transparent->Opaque")
            text = new

        queue = re.search(r"^  m_CustomRenderQueue: (-?\d+)$", text, re.M)
        assert queue, f"{path}: no m_CustomRenderQueue"
        if queue.group(1) != OPAQUE_RENDER_QUEUE:
            text = re.sub(r"^  m_CustomRenderQueue: -?\d+$",
                          f"  m_CustomRenderQueue: {OPAQUE_RENDER_QUEUE}",
                          text, count=1, flags=re.M)
            changes.append(f"queue {queue.group(1)}->{OPAQUE_RENDER_QUEUE}")
    else:
        # Already opaque: just guarantee the clip half of the contract (job 1).
        if get_float(text, "_AlphaClip") != 1.0:
            text = set_float(text, "_AlphaClip", "1", changes)

    text = add_keyword(text, KEYWORD, changes)

    if changes and not check_only:
        open(path, "w", encoding="utf-8").write(text)
    return changes, text


def verify(path):
    text = open(path, encoding="utf-8", errors="ignore").read()
    assert is_opaque(text), f"{path}: still transparent (_Surface != 0)"
    assert get_float(text, "_AlphaClip") == 1.0, f"{path}: _AlphaClip not 1"
    assert re.search(rf"^  - {KEYWORD}$", text, re.M), f"{path}: {KEYWORD} missing"
    assert not re.search(rf"^  - {TRANSPARENT_KEYWORD}$", text, re.M), \
        f"{path}: {TRANSPARENT_KEYWORD} still enabled"
    assert not re.search(r"^    RenderType: Transparent$", text, re.M), \
        f"{path}: RenderType still Transparent"


def coverage_note(text):
    """Authored alpha is live dither coverage now — surface it, never 'fix' it."""
    notes = []
    for name in ("_Alpha", "_Opacity"):
        v = get_float(text, name)
        if v is not None and v < 1.0:
            notes.append(f"{name}={v:g}")
    return f"  [authored coverage: {', '.join(notes)}]" if notes else ""


def main():
    check_only = "--check" in sys.argv
    mats = find_prism_materials()
    assert mats, "no prism-graph materials found — did a shader GUID change?"

    pending = []
    for path, text, graph in mats:
        changes, _ = apply(path, text, check_only=True)
        rel = os.path.relpath(path, REPO)
        note = coverage_note(text)
        if changes:
            pending.append((path, text))
            print(f"  {'WOULD PATCH' if check_only else 'PATCH':11s} {rel}: {', '.join(changes)}{note}")
        else:
            print(f"  {'ok':11s} {rel}{note}")

    if check_only:
        if pending:
            print(f"\nNOT applied: {len(pending)} material(s) off the opaque+clip contract.",
                  file=sys.stderr)
            return 1
        for path, _text, _graph in mats:
            verify(path)
        print(f"\nAll {len(mats)} prism-graph materials are opaque + alpha-clip (verified).")
        return 0

    for path, text in pending:
        apply(path, text, check_only=False)
    for path, _text, _graph in mats:
        verify(path)
    print(f"\nPatched {len(pending)} of {len(mats)} prism-graph materials; all are now "
          "opaque + alpha-clip (verified). Authored sub-1 alphas were preserved as dither coverage.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

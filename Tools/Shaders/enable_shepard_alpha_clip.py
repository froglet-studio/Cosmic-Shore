#!/usr/bin/env python3
"""
Bring every ShepardGraph material onto the screen-door contract: OPAQUE with alpha
clipping, no transparent queue, no blending.

The mass crystal's four nested shells now fade by COVERAGE rather than by intensity
(ShepardToneDither.hlsl, Docs/SHEPARD_TONE.md). Half the reason to do that is depth: with
ZWrite on, an outer shell genuinely occludes the ones behind it and you see them through
the holes it punches, which is the parallax that makes the nesting — and therefore the
Shepard tone — legible. A material left in the transparent queue with ZWrite off gets the
dither's stipple and none of its depth, which is strictly worse than what it replaced.

Two material SHAPES, and they need different treatment — this is the whole reason the
prism tool could not simply be pointed at these files:

  * The four PARENTS (ActiveMassCrystalMaterial*) serialize a full float block, so they
    take the full flip: surface/blend/ZWrite/queue to the opaque pattern, keyword swap,
    RenderType tag.
  * The four VARIANTS (BlueMassCrystalMaterial*) serialize `m_Floats: []` and inherit
    every number from their parent — but they carry their OWN `m_ValidKeywords`,
    `stringTagMap` and render queue. Writing floats into them would author pointless
    overrides; NOT fixing their keywords would leave `_SURFACE_TYPE_TRANSPARENT` enabled
    on a material whose inherited blend state is now opaque, which is the worst of both.
    So variants get the keyword/tag half only.

Authored `_Opacity` is PRESERVED and reported, never "fixed" — on this contract it is live
dither coverage, exactly as `_Alpha`/`_Opacity` are on prism materials.

`disabledShaderPasses` is deliberately left alone. DepthOnly/SHADOWCASTER/MOTIONVECTORS are
off because the crystal is an unlit non-shadow-casting pickup (`m_CastShadows: false` on the
graph target), and the depth ordering this change is after comes from ZWrite in the FORWARD
pass, not from the depth prepass. Re-enabling DepthOnly would put the crystal into the
camera depth texture and change every depth-sampling effect in the scene — a separate
decision, not a side effect of this one.

Reuses the prism tool's YAML helpers rather than re-deriving them. Idempotent.
Run with --check to verify without writing (exit 1 if the contract is not fully applied).
"""

import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from enable_prism_alpha_clip import (  # noqa: E402  — helpers, deliberately shared
    KEYWORD, TRANSPARENT_KEYWORD, OPAQUE_FLOATS, OPAQUE_RENDER_QUEUE,
    add_keyword, remove_keyword, get_float, set_float, is_opaque,
)

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHEPARD_GRAPH_GUID = "71fa822036d425446afa6c9c07046aef"
MATERIAL_ROOTS = ["Assets/_Graphics/Materials"]


def find_shepard_materials():
    hits = []
    for root in MATERIAL_ROOTS:
        for dirpath, _dirs, files in os.walk(os.path.join(REPO, root)):
            for f in files:
                if not f.endswith(".mat"):
                    continue
                path = os.path.join(dirpath, f)
                text = open(path, encoding="utf-8", errors="ignore").read()
                if re.search(rf"m_Shader: \{{fileID: -?\d+, guid: {SHEPARD_GRAPH_GUID}", text):
                    hits.append((path, text))
    return sorted(hits)


def is_variant(text):
    """A material variant serializes only its overrides; every float comes from the parent."""
    m = re.search(r"^  m_Parent: \{fileID: (-?\d+)", text, re.M)
    return bool(m) and m.group(1) != "0"


def fix_render_state_tags(text, changes, variant):
    text = remove_keyword(text, TRANSPARENT_KEYWORD, changes)
    text = add_keyword(text, KEYWORD, changes)

    new, n = re.subn(r"^    RenderType: Transparent$", "    RenderType: Opaque",
                     text, count=1, flags=re.M)
    if n:
        changes.append("RenderType Transparent->Opaque")
        text = new

    # A variant's -1 means "take the shader's queue", and the shader is opaque now — leave
    # it. Only a parent gets an explicit opaque queue pinned on it.
    if not variant:
        queue = re.search(r"^  m_CustomRenderQueue: (-?\d+)$", text, re.M)
        assert queue, "no m_CustomRenderQueue"
        if queue.group(1) != OPAQUE_RENDER_QUEUE:
            text = re.sub(r"^  m_CustomRenderQueue: -?\d+$",
                          f"  m_CustomRenderQueue: {OPAQUE_RENDER_QUEUE}",
                          text, count=1, flags=re.M)
            changes.append(f"queue {queue.group(1)}->{OPAQUE_RENDER_QUEUE}")
    return text


def apply(path, text, check_only):
    changes = []
    variant = is_variant(text)

    if not variant:
        for name, value in OPAQUE_FLOATS.items():
            if get_float(text, name) is not None:
                text = set_float(text, name, value, changes)
            elif name == "_AlphaClip":
                raise AssertionError(f"{path}: no '_AlphaClip' float to set")

    text = fix_render_state_tags(text, changes, variant)

    if changes and not check_only:
        open(path, "w", encoding="utf-8").write(text)
    return changes, text


def verify(path):
    text = open(path, encoding="utf-8", errors="ignore").read()
    variant = is_variant(text)
    if not variant:
        assert is_opaque(text), f"{path}: still transparent (_Surface != 0)"
        assert get_float(text, "_AlphaClip") == 1.0, f"{path}: _AlphaClip not 1"
    assert re.search(rf"^  - {KEYWORD}$", text, re.M), f"{path}: {KEYWORD} missing"
    assert not re.search(rf"^  - {TRANSPARENT_KEYWORD}$", text, re.M), \
        f"{path}: {TRANSPARENT_KEYWORD} still enabled"
    assert not re.search(r"^    RenderType: Transparent$", text, re.M), \
        f"{path}: RenderType still Transparent"


def coverage_note(text):
    v = get_float(text, "_Opacity")
    return f"  [authored coverage: _Opacity={v:g}]" if v is not None and v < 1.0 else ""


def main():
    check_only = "--check" in sys.argv
    mats = find_shepard_materials()
    assert mats, "no ShepardGraph materials found — did the shader GUID change?"

    pending = []
    for path, text in mats:
        changes, _ = apply(path, text, check_only=True)
        rel = os.path.relpath(path, REPO)
        kind = "variant" if is_variant(text) else "parent "
        note = coverage_note(text)
        if changes:
            pending.append((path, text))
            print(f"  {'WOULD PATCH' if check_only else 'PATCH':11s} [{kind}] {rel}: {', '.join(changes)}{note}")
        else:
            print(f"  {'ok':11s} [{kind}] {rel}{note}")

    if check_only:
        if pending:
            print(f"\n  {len(pending)} material(s) off contract.", file=sys.stderr)
            return 1
        print(f"\n  all {len(mats)} ShepardGraph material(s) on contract.")
        return 0

    for path, text in pending:
        apply(path, text, check_only=False)
    for path, _ in mats:
        verify(path)
    print(f"\n  {len(pending)} patched, {len(mats)} verified on contract.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

"""The look kit: the tools behind the world's look (Docs/SKILL_TRAINING_PLAN.md section 3.2), the 3D twin of
Tools/Build/icon_kit/.

What exists:
  audit.py   the world-look audit (R0's audit sheet): which prefabs draw an EFFECT in the world, what each is made of,
             and which of the LOCKED flat shapes each carries; --check is the gate that stops a NEW flat effect while
             the baseline (audit_baseline.json) carries the known offenders as owed work.

What the plan still names (not built): references/LOOK_LANGUAGE.md and VOCABULARY.md (Garrett confirms the language
first), effect.py (an effect as a SPEC with builders), verify.py (the per-effect gates with negative controls) and the
sheet. Nothing here writes a game asset.
"""

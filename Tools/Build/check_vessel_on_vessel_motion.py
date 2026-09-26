#!/usr/bin/env python3
"""A VESSEL MAY NOT MOVE AN OPPOSING VESSEL, NOR TAKE ITS CONTROLS.

Being shoved, spun or re-aimed by somebody else's weapon - or having your stick muted, your
skimmer shrunk or your trail spawner frozen - is the one kind of hit a pilot cannot answer with
flying, and it was reported as simply not fun to receive. What a weapon MAY take from another
pilot is their ELEMENTAL CRYSTALS - stolen on a contact verb, ejected as free-for-all crystals on
a ranged one (`Docs/ELEMENTAL_ECONOMY.md`) - which is a loss they can chase down and win back.

EIGHT effects broke that law and were removed in Sep 2026, in two tiers.

MOTION. The Sparrow's guns and rocket, the Urchin's spikes and the Rhino's sword all spun their
victim (the sword laterally shoved it too). `VesselTransformer.SpinShip` went with them, since
those were its only callers.

CONTROL. The Sparrow's guns shrank a Rhino's blade for 3s
(`VesselChangeSkimmerSizeByProjectileEffectSO` -> the SHARED `ShieldSkimmerScaleConfig.asset`, so
one hit shrank every Rhino in the match); the Rhino's sword muted its victim's right stick for 5s
(`VesselDamageBySkimmerEffectSO`) and froze their trail spawner for 10s
(`VesselPrismSpawnerCooldownBySkimmerEffectSO` - whose only reference was an INERT retired
serialized key on `Rhino.prefab`, so it had never actually run); and a Rhino or Squirrel crystal
blast muted it for 3s (`VesselChangeSpeedByExplosionEffectSO`, which changed no speed).
`ShieldSkimmerScaleConfigSO.ApplyMaxSizeDebuff` went with them.

WHAT THIS GATE CHECKS, AND WHY IT IS A GATE RATHER THAN A PARAGRAPH
------------------------------------------------------------------
The capability is one `[SerializeField]` away from returning: an effect SO in a victim-facing
family can call the transformer's motion API, and a designer re-adds it by dropping the asset
into a container. Nothing about that fails to compile and nothing about it looks wrong on the
asset. So the rule is enforced on the CODE that could do it: no effect in a vessel-as-victim
family may reach a motion or attitude API on the victim.

The victim-facing families are the ones named `Vessel <Source> Effects/` - by the project's
`[Impactor][Target]EffectSO` convention those are effects applied TO a vessel BY that source, and
the three whose source is another pilot's weapon are Projectile, Skimmer and Explosion.

DELIBERATELY NOT CHECKED
------------------------
  * `Vessel Prism Effects/` - a vessel deflecting off MASS it flew into is the FLIGHT MODEL, not
    a weapon, and `GentleSpinShip`/`ModifyVelocity` are how a bounce reads. A rival's trail
    deflecting you is you hitting their wall, which a pilot answers by flying around it. The
    same carve-out covers the DANGER prism's input mute (`SparrowDebuffByRhinoDangerPrismEffectSO`
    calls `MuteInput`): a danger prism is a hazard standing in the world, which is exactly the
    risk/reward surface the trail economy is built on, and a pilot answers it by not flying into
    it. The line the law draws is between the ARENA and a weapon somebody AIMED at you.
  * `Skimmer Prism Effects/` - same argument, one impactor over (the Rhino's blade bouncing off
    a prism it cut).
  * A vessel moving ITSELF (dashes, barrel rolls, boosts, `SpinAroundAction`) - that is the
    whole of what an ability is for.
  * `Vessel Crystal Effects/` - a pickup acting on the pilot who took it.

Run: python3 Tools/Build/check_vessel_on_vessel_motion.py [--self-test]
"""

from __future__ import annotations

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is wrong: {ROOT}"

EFFECTS = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO"

# The families whose source is ANOTHER PILOT'S WEAPON.
VICTIM_FACING = (
    "Vessel Projectile Effects",
    "Vessel Skimmer Effects",
    "Vessel Explosion Effects",
)

# Written as the member name so a renamed method does not silently fall out of the list - a
# rename that drops a row here fails the self-test.
#
# MOTION / ATTITUDE, on VesselTransformer.
FORBIDDEN = (
    "ModifyVelocity",
    "SpinShip",          # deleted; listed so a revival is caught rather than merely compiling
    "GentleSpinShip",
    "FlatSpinShip",
    "ApplyRotation",
    "SetPose",
    "SetInitialSpeed",
    # CONTROL THEFT. Muting a pilot's input, cancelling the action it drives, shrinking their
    # skimmer or stopping their trail spawner are not motion, and they are the same loss: a
    # window in which flying does not answer the hit.
    "MuteInput",
    "StopShipControllerActions",
    "ApplyMaxSizeDebuff",   # deleted with the effect; listed so a revival is caught
    "StopSpawn",
)

CALL = re.compile(r"\.(" + "|".join(FORBIDDEN) + r")\s*\(")
# A doc comment or a `//` line is prose, not a call.
PROSE = re.compile(r"^\s*(///|//|\*|/\*)")


def scan(text: str):
    """Return [(lineno, member)] for every forbidden CALL, skipping prose."""
    hits = []
    for i, line in enumerate(text.splitlines(), 1):
        if PROSE.match(line):
            continue
        for m in CALL.finditer(line):
            hits.append((i, m.group(1)))
    return hits


def run(verbose=True):
    problems, scanned = [], 0
    for family in VICTIM_FACING:
        d = os.path.join(ROOT, EFFECTS, family)
        if not os.path.isdir(d):
            problems.append(f"family folder missing (renamed?): {EFFECTS}/{family}")
            continue
        for name in sorted(os.listdir(d)):
            if not name.endswith(".cs"):
                continue
            scanned += 1
            with open(os.path.join(d, name), encoding="utf-8") as fh:
                text = fh.read()
            for lineno, member in scan(text):
                problems.append(
                    f"{EFFECTS}/{family}/{name}:{lineno}: calls {member} - A VESSEL MAY NOT MOVE "
                    f"AN OPPOSING VESSEL, NOR TAKE ITS CONTROLS. Take their elemental crystals "
                    f"instead (Docs/ELEMENTAL_ECONOMY.md §9)."
                )

    if verbose:
        for p in problems:
            print(p)
        tag = "OK" if not problems else f"{len(problems)} PROBLEM(S)"
        print(f"vessel-on-vessel motion/control check: {tag} "
              f"({scanned} victim-facing effect(s) over {len(VICTIM_FACING)} families)")
    return problems


def self_test():
    """Negative controls: the shapes that MUST fire, and the shapes that must NOT."""
    must_fire = [
        "transformer.ModifyVelocity(lateralDir * lateralSpeed, 0.1f);",
        "vesselStatus.VesselTransformer.SpinShip(impactVector * spinSpeed);",
        "victimStatus.VesselTransformer .GentleSpinShip(fwd, up, 1f);",
        "status.VesselTransformer.ApplyRotation(30f, axis);",
        "handler.MuteInput(inputToMute, muteSeconds);",
        "handler.StopShipControllerActions(inputToMute);",
        "_ = scaleConfig.ApplyMaxSizeDebuff(sizeMultiplier, duration);",
        "shipStatus.VesselPrismController.StopSpawn();",
    ]
    must_not_fire = [
        "/// vesselStatus.VesselTransformer.SpinShip(dir) - removed, see the law.",
        "// transformer.ModifyVelocity(shove, seconds);",
        "        /// its ModifyVelocity displacement is orthogonal to travel",
        "resources.AccrueElementalLoss(element, amount, source);",
        "ElementalTransfer.ApplyAll(form, victim, attacker, mag, v, source);",
    ]
    ok = True
    for s in must_fire:
        if not scan(s):
            print(f"SELF-TEST FAIL (should fire): {s}")
            ok = False
    for s in must_not_fire:
        if scan(s):
            print(f"SELF-TEST FAIL (should NOT fire): {s}")
            ok = False
    print("self-test: OK" if ok else "self-test: FAILED")
    return ok


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(0 if self_test() else 1)
    sys.exit(1 if run() else 0)

#!/usr/bin/env python3
"""
THE ELEMENTAL ECONOMY'S STANDING GATE.

Every elemental debuff in the game is now a TRANSFER rather than a decay: a joust steals petals,
a blast knocks them loose as collectable crystals, and exactly one force - a hostile danger prism
- destroys them. That only stays true if four properties hold, and every one of them fails
SILENTLY: a hull quietly loses its only way to affect another pilot, a crystal quietly stops
being worth what was taken to mint it, a second sink quietly appears, or half the fleet quietly
recovers at a different speed from the other half.

  1. EVERY PLAYABLE HULL CAN DEBUFF ANOTHER VESSEL. This is the headline requirement and it is
     invisible in play: a hull with no anti-vessel path does not error, it just never affects
     anybody - which is how the Serpent shipped for the fleet's whole life with 0 of 4 abilities
     able to touch a pilot, and how the Rhino's sword reported a Strike that drained nothing.

  2. A CRYSTAL IS WORTH EXACTLY ONE PETAL. Conservation is the whole design, and it rests on an
     arithmetic coincidence between three numbers in three different files: the ejector mints at
     world scale 1, the collect reward is scale x levelPerUnitScale, and a petal is 0.1. Retune
     levelPerUnitScale on the pickup asset - a perfectly reasonable thing to want to do - and
     every ejected petal silently starts paying more or less than it cost.

  3. THE SINK IS UNIQUE. A closed economy needs exactly one hole in it. A second caller of
     ElementalTransfer.Burn would drain the match with nothing to say so.

  4. THE RECOVERY RATE IS ONE NUMBER. Four prefabs SERIALIZE it and eight are silent, so editing
     the C# initializer alone splits the fleet in half (the rule-4-i trap: a silent prefab is not
     an unset one).

Run it:   python3 Tools/Build/check_elemental_economy.py [--self-test]
"""
import os, re, sys, glob

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

RESOURCE_SYSTEM = "Assets/_Scripts/Controller/Vessel/ResourceSystem.cs"
EJECTOR         = "Assets/_Scripts/Controller/Environment/FlowField/ElementalCrystalEjector.cs"
PICKUP_ASSET    = "Assets/_SO_Assets/Effects/Skimmer Crystal Effects/SkimmerAdjustElementLevelByCrystalEffect.asset"
PICKUP_CS       = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerAdjustElementLevelByCrystalEffectSO.cs"
TRANSFER        = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ElementalTransfer.cs"
CONTAINER_DIR   = "Assets/_SO_Assets/Effects/Effect Containers"
VESSEL_PREFABS  = "Assets/_Prefabs/Spacevessels"

# The hulls a player can actually fly. Grizzly/Termite/Falcon/Shrike are unfinished and are not
# held to the contract - listing them would make the gate permanently red for a reason nobody can
# act on, which is how a gate stops being read.
PLAYABLE = ["Manta", "Dolphin", "Rhino", "Serpent", "Sparrow", "Squirrel", "Urchin", "Scarab"]

# Which container carries each hull's anti-vessel weapon. A hull may reach a pilot through more
# than one; it needs at least one.
HULL_CONTAINERS = {
    "Sparrow":  ["Projectile Containers/SparrowFullAutoProjectileImpactContainer.asset",
                 "Projectile Containers/SparrowPrismProjectileImpactContainer.asset",
                 "Projectile Containers/SparrowSkyBurstProjectileImpactContainer.asset",
                 "Explosion Containers/MissileWarheadExplosionImpactorDataContainer.asset",
                 "Explosion Containers/SkyBurstBlastExplosionImpactorDataContainer.asset"],
    "Urchin":   ["Projectile Containers/UrchinSpikeProjectileImpactContainer.asset"],
    "Squirrel": ["SkimmerContainers/SquirrelSkimmerImpactorDataContainer.asset"],
    "Rhino":    ["SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset"],
    "Dolphin":  ["Explosion Containers/AOEConicExplosionImpactorDataContainer.asset"],
    "Scarab":   ["Explosion Containers/ScarabCavitationExplosionImpactorDataContainer.asset"],
    "Manta":    ["Explosion Containers/MantaBloomExplosionImpactorDataContainer.asset"],
    # The Serpent has no container-borne anti-vessel verb at all - no skimmer, no projectile, no
    # blast. Its debuff is CODE: the sniper hitscan strips pilots inside its own cone. Asserted
    # against the executor instead, which is why this entry is a path rather than a container.
    "Serpent":  [],
}

# The effect SO types that constitute an anti-vessel elemental transfer.
TRANSFER_EFFECT_TYPES = {
    "VesselElementalDebuffByExplosionEffectSO",   # blast -> eject
    "VesselOvertakeBySkimmerEffectSO",            # contact -> steal
    "VesselCombatHitByProjectileEffectSO",        # gun/rocket -> eject (via CombatHitDrain)
    "VesselCombatHitByExplosionEffectSO",
    "VesselCombatHitBySkimmerEffectSO",
}

SERPENT_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/SniperShotActionExecutor.cs"

_fails = []
def fail(msg): _fails.append(msg)
def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def guid_index():
    """guid -> (asset name, script type name) for every effect asset."""
    script_by_guid = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets/_Scripts/**/*.cs.meta"), recursive=True):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta, errors="ignore").read(), re.M)
        if m: script_by_guid[m.group(1)] = os.path.basename(meta)[:-8]

    out = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets/_SO_Assets/Effects/**/*.asset.meta"), recursive=True):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta, errors="ignore").read(), re.M)
        if not m: continue
        asset = meta[:-5]
        name = os.path.basename(asset)[:-6]
        body = open(asset, errors="ignore").read() if os.path.exists(asset) else ""
        sm = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
        out[m.group(1)] = (name, script_by_guid.get(sm.group(1), "?") if sm else "?")
    return out


def check_every_hull_can_debuff(index, hull_containers=None, serpent_src=None):
    hull_containers = hull_containers if hull_containers is not None else HULL_CONTAINERS
    print("\n1. every playable hull can debuff another vessel")
    for hull in PLAYABLE:
        found = []
        for rel in hull_containers.get(hull, []):
            path = os.path.join(ROOT, CONTAINER_DIR, rel)
            if not os.path.exists(path):
                fail(f"{hull}: container missing - {rel}")
                continue
            txt = open(path, errors="ignore").read()
            for g in re.findall(r"guid: ([0-9a-f]{32})", txt):
                if g in index and index[g][1] in TRANSFER_EFFECT_TYPES:
                    found.append(index[g][0])

        if hull == "Serpent":
            src = serpent_src if serpent_src is not None else read(SERPENT_EXECUTOR)
            # Either spelling of the same verb: the direct Eject helper, or ApplyAll carrying
            # ElementalTransferForm.Eject (the sanctioned "strip all four" shape). Pinning one
            # spelling made this gate fail the day the call was tidied into the other, which
            # reads as the Serpent losing its anti-vessel verb - ask WHICH VERB, not which word.
            ejects = re.search(r"ElementalTransfer\.Eject\s*\(", src) or \
                     re.search(r"ElementalTransferForm\.Eject\s*,", src)
            if ejects and "StripVessels" in src:
                found.append("SniperShotActionExecutor.StripVessels (code, not a container)")

        if found:
            print(f"   ok   {hull:<9} {', '.join(sorted(set(found)))}")
        else:
            fail(f"{hull}: NO anti-vessel elemental transfer - this hull cannot affect a pilot")


def check_crystal_is_one_petal(rs_src=None, ej_src=None, pickup=None, pickup_cs=None):
    print("\n2. an ejected crystal is worth exactly one petal")
    rs_src   = rs_src   if rs_src   is not None else read(RESOURCE_SYSTEM)
    ej_src   = ej_src   if ej_src   is not None else read(EJECTOR)
    pickup   = pickup   if pickup   is not None else (read(PICKUP_ASSET) if os.path.exists(os.path.join(ROOT, PICKUP_ASSET)) else "")
    pickup_cs= pickup_cs if pickup_cs is not None else read(PICKUP_CS)

    m = re.search(r"public const float PetalNormalized = 1f / LevelScale;", rs_src)
    scale_m = re.search(r"const int\s+LevelScale = (\d+);", rs_src)
    if not m or not scale_m:
        fail("ResourceSystem: PetalNormalized is no longer '1f / LevelScale' - the petal unit moved")
        return
    petal = 1.0 / int(scale_m.group(1))

    em = re.search(r"PetalCrystalWorldScale = ([0-9.]+)f", ej_src)
    if not em:
        fail("ElementalCrystalEjector: PetalCrystalWorldScale not found")
        return
    mint_scale = float(em.group(1))

    # The live reward per unit of scale: the ASSET if it authors one, else the C# initializer
    # (a silent asset is not an unset one).
    pm = re.search(r"^\s*levelPerUnitScale: ([0-9.]+)\s*$", pickup, re.M)
    if pm:
        per_unit, src = float(pm.group(1)), "asset"
    else:
        dm = re.search(r"levelPerUnitScale = ([0-9.]+)f", pickup_cs)
        if not dm:
            fail("levelPerUnitScale found in neither the pickup asset nor its C# initializer")
            return
        per_unit, src = float(dm.group(1)), "C# initializer"

    payout = mint_scale * per_unit
    if abs(payout - petal) > 1e-6:
        fail(f"a minted crystal pays {payout:.4f} but a petal costs {petal:.4f} "
             f"(mint scale {mint_scale} x levelPerUnitScale {per_unit} from the {src}) - "
             f"ejection is no longer conserving")
    else:
        print(f"   ok   mint scale {mint_scale} x {per_unit} ({src}) = {payout:.3f} = one petal")


def check_single_sink(tree=None):
    print("\n3. the economy has exactly one sink")
    callers = []
    files = tree if tree is not None else glob.glob(os.path.join(ROOT, "Assets/_Scripts/**/*.cs"), recursive=True)
    for f in files:
        src = open(f, errors="ignore").read() if isinstance(f, str) and os.path.exists(f) else f[1]
        name = os.path.relpath(f, ROOT) if isinstance(f, str) else f[0]
        if name.endswith("ElementalTransfer.cs"):
            continue   # the definition itself
        if re.search(r"ElementalTransfer\.Burn\s*\(", src) or \
           re.search(r"ElementalTransferForm\.Burn\s*,", src):
            callers.append(name)
    if len(callers) == 1 and "DangerPrism" in callers[0]:
        print(f"   ok   {callers[0]}")
    elif not callers:
        fail("NO sink: nothing burns petals, so the economy fills to the ceiling and stops")
    else:
        fail(f"{len(callers)} sinks - only a hostile danger prism may burn: {callers}")


def check_recovery_rate_is_one_number(cs=None, prefabs=None):
    print("\n4. the elemental recovery rate is one number fleet-wide")
    cs = cs if cs is not None else read(RESOURCE_SYSTEM)
    m = re.search(r"float elementalRecoveryRate = ([0-9.]+)f", cs)
    if not m:
        fail("ResourceSystem: elementalRecoveryRate initializer not found")
        return
    initializer = float(m.group(1))

    pairs = prefabs
    if pairs is None:
        pairs = []
        for p in sorted(glob.glob(os.path.join(ROOT, VESSEL_PREFABS, "*.prefab"))):
            txt = open(p, errors="ignore").read()
            for v in re.findall(r"^\s*elementalRecoveryRate: ([0-9.]+)\s*$", txt, re.M):
                pairs.append((os.path.basename(p), float(v)))

    bad = [(n, v) for n, v in pairs if abs(v - initializer) > 1e-9]
    if bad:
        fail(f"initializer is {initializer} but these prefabs SERIALIZE a different value "
             f"(editing the C# alone splits the fleet): {bad}")
    else:
        print(f"   ok   {initializer} in the initializer and in all {len(pairs)} prefabs that "
              f"serialize it ({1/initializer/10:.0f}s per level)")


def self_test():
    """Every check must FAIL on a broken input. A gate nobody has watched fail is one nobody
    should trust."""
    print("SELF-TEST (each block must report a failure)\n" + "=" * 60)
    global _fails
    ok = True

    def expect_fail(label, fn):
        nonlocal ok
        global _fails
        _fails = []
        fn()
        if _fails:
            print(f"   FIRED  {label}: {_fails[0][:90]}")
        else:
            print(f"   MISSED {label} - this control did not fire")
            ok = False

    idx = guid_index()
    expect_fail("a hull with no anti-vessel path",
                lambda: check_every_hull_can_debuff(idx, dict(HULL_CONTAINERS, Dolphin=[]), serpent_src=""))
    expect_fail("the Serpent losing its code-side strip",
                lambda: check_every_hull_can_debuff(idx, HULL_CONTAINERS, serpent_src="// nothing here"))
    expect_fail("a retuned levelPerUnitScale breaking conservation",
                lambda: check_crystal_is_one_petal(pickup="  levelPerUnitScale: 0.25\n"))
    expect_fail("a second sink",
                lambda: check_single_sink([("A/DangerPrism.cs", "ElementalTransfer.Burn(x);"),
                                           ("B/Rogue.cs", "ElementalTransfer.Burn(y);")]))
    expect_fail("no sink at all",
                lambda: check_single_sink([("A/Nothing.cs", "// nothing")]))
    expect_fail("a prefab left on the old recovery rate",
                lambda: check_recovery_rate_is_one_number(prefabs=[("Sparrow.prefab", 0.05)]))

    _fails = []
    print("\n" + ("SELF-TEST PASS" if ok else "SELF-TEST FAILED"))
    return 0 if ok else 1


def main():
    if "--self-test" in sys.argv:
        return self_test()

    print("ELEMENTAL ECONOMY" + "\n" + "=" * 60)
    idx = guid_index()
    check_every_hull_can_debuff(idx)
    check_crystal_is_one_petal()
    check_single_sink()
    check_recovery_rate_is_one_number()

    print()
    if _fails:
        for f in _fails: print("FAIL  " + f)
        return 1
    print("PASS  the economy conserves, every hull can fight for it, and one thing drains it.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

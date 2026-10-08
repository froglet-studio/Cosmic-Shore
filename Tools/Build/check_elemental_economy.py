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

  5. THE PETAL-BURN SWITCH RESOLVES AS DOCUMENTED (Docs/ELEMENTAL_ECONOMY.md §4.1). The sink's size
     is per CELL: Shipped (the asset's debuffMagnitude, five petals per element) everywhere, Tuned
     (tunedDebuffMagnitude, one petal) in the Swarm cell only. Both sizes must be WHOLE petals, the
     own-domain sting must use the same resolved size as the burn, and the effect must be wired to
     the live cell - an unwired reference silently plays Shipped in the cell meant to be Tuned.

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


DANGER_EFFECT_ASSET = "Assets/_SO_Assets/Effects/Vessel Prism Effects/VesselElementalDebuffByDangerPrismEffect.asset"
DANGER_EFFECT_CS    = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselElementalDebuffByDangerPrismEffectSO.cs"
CELL_CONFIG_SCRIPT  = "01f934d50526431a9392a6ceca1dc33d"   # CellConfigDataSO
RUNTIME_CELL_DATA   = "8d4e8398eedc76c4dadb8604f89b9e1b"   # Runtime Cell Data.asset
SILENT_RULE         = 1                                     # CellConfigDataSO.PetalBurnRule's initializer: Tuned (2026-10-08)
CELL_CONFIG_CS      = "Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs"
EXPECTED_PETALS     = {"Shipped": 5, "Tuned": 1}           # per element per hostile contact (burn-rules.md)


def petals_per_contact(magnitude, held_petals):
    """Mirror of ResourceSystem.AccrueElementalLoss for one fresh contact on one element: whole petals,
    clamped to what is held. Integer arithmetic in hundredths of a petal so the mirror cannot drift."""
    want = round(-magnitude * 1000)          # thousandths of normalized level
    return min(want // 100, held_petals)


def cell_rules(cells=None):
    """{cell config name: PetalBurnRule int} for every CellConfigDataSO asset (a silent asset plays the field's
    initializer, SILENT_RULE)."""
    if cells is not None:
        return cells
    out = {}
    for path in glob.glob(os.path.join(ROOT, "Assets/**/*.asset"), recursive=True):
        txt = open(path, errors="ignore").read()
        if f"guid: {CELL_CONFIG_SCRIPT}" not in txt:
            continue
        m = re.search(r"^  PetalBurnRule: (\d+)\s*$", txt, re.M)
        out[os.path.basename(path)[:-6]] = int(m.group(1)) if m else SILENT_RULE
    return out


def check_petal_burn_switch(asset=None, cs=None, cells=None, cfg_cs=None):
    print("\n5. the petal-burn switch resolves as documented")
    asset = asset if asset is not None else read(DANGER_EFFECT_ASSET)
    cs = cs if cs is not None else read(DANGER_EFFECT_CS)

    mags = {}
    for rule, key in (("Shipped", "debuffMagnitude"), ("Tuned", "tunedDebuffMagnitude")):
        m = re.search(rf"^  {key}: (-?[0-9.]+)\s*$", asset, re.M)
        if not m:
            fail(f"danger-prism effect asset authors no {key} (run author_petal_burn_rule.py)")
            return
        mags[rule] = float(m.group(1))
    for rule, mag in mags.items():
        tenths = -mag * 10
        if abs(tenths - round(tenths)) > 1e-6 or tenths <= 0:
            fail(f"{rule} magnitude {mag} is not a whole number of petals - a contact would bank a "
                 f"fraction that settles on a LATER contact")
            continue
        per = petals_per_contact(mag, 5)
        if per != EXPECTED_PETALS[rule]:
            fail(f"{rule} burns {per} petals per element per contact, documented {EXPECTED_PETALS[rule]} "
                 f"(Docs/ELEMENTAL_ECONOMY.md §4.1 - retune the doc with the asset)")
        else:
            clamp = petals_per_contact(mag, 2)
            print(f"   ok   {rule:<7} {mag:+.2f} = {per} petal(s) per element, {per * 4} per contact; "
                  f"a 2-petal element loses {clamp}")
    if not re.search(rf"^  cellData: \{{fileID: 11400000, guid: {RUNTIME_CELL_DATA}, type: 2\}}", asset, re.M):
        fail("danger-prism effect asset is not wired to Runtime Cell Data - every cell would play Shipped")

    body = cs[cs.find("public override void Execute"):]
    if "PetalBurnRules.Magnitude(" not in body:
        fail("the danger-prism effect no longer resolves its size through PetalBurnRules.Magnitude")
    if not re.search(r"ElementalTransfer\.Burn\([^;]*-magnitude", body) or \
       not re.search(r"ApplyElementalEffect\([^;]*\bmagnitude\b", body) or \
       re.search(r"\bdebuffMagnitude\b(?!\s*,\s*tunedDebuffMagnitude)", body):
        fail("the hostile burn and the own-domain debuff no longer share ONE resolved size")
    else:
        print("   ok   burn and own-domain debuff both use the resolved size")

    if "PetalBurnRule PetalBurnRule = PetalBurnRule.Tuned;" not in (cfg_cs if cfg_cs is not None else read(CELL_CONFIG_CS)):
        fail("CellConfigDataSO.PetalBurnRule no longer defaults to Tuned - every silent cell would play Shipped")
    if "PetalBurnRule.Tuned;" not in body[body.find("liveConfig ?"):body.find("liveConfig ?") + 120]:
        fail("the danger-prism effect no longer falls back to Tuned when no cell is live")
    rules = cell_rules(cells)
    shipped = sorted(n for n, v in rules.items() if v != 1)
    if shipped:
        fail(f"cells not on the Tuned burn (Garrett 2026-10-08: Tuned everywhere): {shipped}")
    else:
        print(f"   ok   all {len(rules)} cell configs play Tuned (1 petal per element per contact)")


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

    good_asset = read(DANGER_EFFECT_ASSET)
    expect_fail("a tuned size that is not a whole petal",
                lambda: check_petal_burn_switch(asset=good_asset.replace("tunedDebuffMagnitude: -0.1", "tunedDebuffMagnitude: -0.15")))
    expect_fail("a tuned size that drifted to two petals",
                lambda: check_petal_burn_switch(asset=good_asset.replace("tunedDebuffMagnitude: -0.1", "tunedDebuffMagnitude: -0.2")))
    expect_fail("the effect unwired from the live cell",
                lambda: check_petal_burn_switch(asset=re.sub(r"^  cellData: .*\n", "", good_asset, flags=re.M)))
    expect_fail("the own-domain debuff decoupled from the burn",
                lambda: check_petal_burn_switch(cs=read(DANGER_EFFECT_CS).replace(
                    "ApplyElementalEffect(AllElements[i], magnitude,", "ApplyElementalEffect(AllElements[i], debuffMagnitude,")))
    expect_fail("a cell switched back to Shipped",
                lambda: check_petal_burn_switch(cells=dict(cell_rules(), **{"Arboretum Cell Config": 0})))
    expect_fail("the demo cell left on Shipped",
                lambda: check_petal_burn_switch(cells=dict(cell_rules(), **{"Swarm Cell Config": 0})))
    expect_fail("an unknown burn rule",
                lambda: check_petal_burn_switch(cells=dict(cell_rules(), **{"Arboretum Cell Config": 7})))
    expect_fail("the cell default reverted to Shipped",
                lambda: check_petal_burn_switch(cfg_cs=read(CELL_CONFIG_CS).replace(
                    "PetalBurnRule PetalBurnRule = PetalBurnRule.Tuned;", "PetalBurnRule PetalBurnRule = PetalBurnRule.Shipped;")))
    expect_fail("the no-cell fallback reverted to Shipped",
                lambda: check_petal_burn_switch(cs=read(DANGER_EFFECT_CS).replace(
                    "liveConfig.PetalBurnRule : PetalBurnRule.Tuned;", "liveConfig.PetalBurnRule : PetalBurnRule.Shipped;")))

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
    check_petal_burn_switch()

    print()
    if _fails:
        for f in _fails: print("FAIL  " + f)
        return 1
    print("PASS  the economy conserves, every hull can fight for it, and one thing drains it.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""The Swarm cell's LAYOUT for the showcase-cell harness (Docs/SWARM_FAUNA.md §26), read - never re-typed - from the
generators that author the cell and from the assets they wrote:

  * author_swarm_fauna.py   - the three swarm regions (bands, start creature, food, plant floor / cap), the canonical
                              Borromean plate and budget per region, the collider model and ceiling;
  * author_substrate_fauna.py - the seven substrate populations (band, element, seeding, sector, proxies), the cell capacity;
  * author_builders.py      - the fortress colony and the thief nest (band, element, proxies);
  * the assets              - SwarmSortFaunaConfig.asset (every number SwarmFauna.BuildSortCore reads),
                              FortressColonyConfig.asset / ThiefNestConfig.asset (BuilderColonyConfigSO), the
                              Swarm Cell Config's PetalBurnRule, the flora configs' reproduction, BorromeanFlora.prefab's
                              growPeriod.

    python3 layout.py <out.json>
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BUILD = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(BUILD))
sys.path.insert(0, BUILD)

import author_builders  # noqa: E402
import author_substrate_fauna  # noqa: E402
import author_swarm_fauna as swarm  # noqa: E402

try:   # round 11c threat flora (author_threat_flora.py) - optional: absent before it merged
    import author_threat_flora as atf  # noqa: E402
except ImportError:
    atf = None

A = lambda *p: os.path.join(REPO, "Assets", *p)


def yaml_flat(path):
    """The top-level serialized fields of a ScriptableObject asset: numbers, and {x: .., y: ..} vectors as lists."""
    out = {}
    for line in open(path, encoding="utf-8"):
        m = re.match(r"^  (\w+): (.*)$", line.rstrip("\n"))
        if not m:
            continue
        k, v = m.groups()
        v = v.strip()
        vec = re.match(r"^\{x: ([-\d.eE]+), y: ([-\d.eE]+)(?:, z: ([-\d.eE]+))?(?:, w: ([-\d.eE]+))?\}$", v)
        if vec:
            out[k] = [float(x) for x in vec.groups() if x is not None]
            continue
        try:
            out[k] = float(v)
        except ValueError:
            pass
    return out


def yaml_species(path):
    """The `species:` block of a SubstrateSpeciesSO asset (the SubstrateSpeciesParams the game reads), as nested dicts:
    scalars at 4 spaces, the two regimes (Solitary, Gregarious) at 6, a float[] as a list. Numbers as floats, the rest as
    strings."""
    out, cur, inside, last = {}, None, False, None
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if line == "  species:":
            inside = True
            continue
        if not inside:
            continue
        item = re.match(r"^    - (.*)$", line)
        if item and last is not None:   # a float[] (the leviathan's BodySlots)
            if not isinstance(out[last], list):
                out[last] = []
            out[last].append(float(item.group(1)))
            continue
        m = re.match(r"^( {4,6})(\w+):(?: (.*))?$", line)
        if not m:
            break
        ind, k, v = m.groups()
        if v is None:
            cur = out[k] = {}
            last = k
            continue
        last = None
        try:
            val = float(v)
        except ValueError:
            val = v.strip()
        (cur if len(ind) == 6 else out)[k] = val
        if len(ind) == 4:
            cur = None
    return out


def main():
    out_path = sys.argv[1]
    plans = swarm.committed_plans()
    rows, tot, _eggs = swarm.model(plans)
    cell_dir = A("_SO_Assets", "Cell Configs", "Swarm Cell")
    grow_period = float(re.search(r"growPeriod: ([\d.]+)", open(A("_Prefabs", "FloraAndFauna", "BorromeanFlora.prefab")).read()).group(1))
    regions = []
    for r in swarm.REGIONS:
        c = swarm.canon(r)
        flora = yaml_flat(os.path.join(cell_dir, swarm.flora_name(r) + ".asset"))
        regions.append(dict(
            key=r["key"], band=list(r["band"]), flora_band=list(swarm.flora_band(r)),
            pens=[dict(axis=list(a or (0, 0, 0)), half=h, inner=lo, outer=hi) for a, h, lo, hi in r.get("pens", [])], start=swarm.ELEMENT_ID[r["start"]] - 1, plan=r["plan"],
            food=swarm.ELEMENT_ID[r["food"]] - 1, floor=r["floor"], cap=r["cap"], swarms=r["swarms"],
            leaf=list(c["leaf"]), budget=c["budget"],
            growth_per_offspring=flora.get("GrowthPerOffspring", 0.0),
            reproduction_cooldown=flora.get("ReproductionCooldownSeconds", 5.0)))
    sp = author_substrate_fauna
    params = json.load(open(sp.GAME_PARAMS))
    substrate = []
    for s in sp.SPECIES:
        asset = yaml_flat(os.path.join(sp.SO_DIR, f"Substrate {s['title']} Species.asset"))
        species = yaml_species(os.path.join(sp.SO_DIR, f"Substrate {s['title']} Species.asset"))
        substrate.append(dict(key=s["key"], band=list(s["band"]), element=swarm.ELEMENT_ID[s["element"]] - 1,
                              seed=s["seed"] or params[s["key"]]["N0"], spread=s["spread"], at_flora=s["at_flora"],
                              engage=s["engage"], proxies=s["proxies"], asset=asset, species=species))
    builders = []
    for s in author_builders.SPECIES:
        cfg = yaml_flat(author_builders.config_path(s))
        cfg.update({k: float(v) for k, v in s["overrides"].items()})
        builders.append(dict(key=s["key"], species=s["species"], band=list(s["band"]),
                             element=swarm.ELEMENT_ID[s["element"]] - 1, count=s["count"], config=cfg))
    cell = yaml_flat(os.path.join(cell_dir, "Swarm Cell Config.asset"))
    # round 11-10: the cell's seeder clock (RandomLifeSpawner.SpawnFaunaTypeLoop_Random) - extinction recovery
    profile = yaml_flat(os.path.join(cell_dir, "Swarm Cell Spawn Profile.asset"))
    grove = None
    if atf is not None:
        d = atf.defaults()
        grove = dict(defaults={k: (list(v) if isinstance(v, tuple) else v) for k, v in d.items()},
                     config=yaml_flat(atf.asset_path(atf.GROVE_NAME)),
                     hearts=atf.always_on_hearts(), body_prisms=atf.prism_colliders())
    layout = dict(
        membrane=swarm.MEMBRANE_RADIUS, nucleus=swarm.NUCLEUS_RADIUS, grow_period=grow_period,
        petal_burn_rule=int(cell.get("PetalBurnRule", 0)),
        fauna_spawn_wait=profile["InitialFaunaSpawnWaitTime"], fauna_spawn_period=profile["BaseFaunaSpawnTime"],
        regions=regions,
        swarm_config=yaml_flat(A("_SO_Assets", "Swarm Fauna", "SwarmSortFaunaConfig.asset")),
        substrate=substrate, substrate_capacity=sp.CELL_CAPACITY,
        builders=builders,
        grove=grove,
        colliders=dict(hearts=tot["hearts"], swarm_proxies=2 * swarm.MAX_PROXIES * swarm.TOTAL_SWARMS,
                       substrate_proxies=sp.proxy_colliders(), builder_proxies=author_builders.proxy_colliders(),
                       grove_hearts=grove["hearts"] if grove else 0,
                       worst=tot["colliders_engaged"] + (grove["hearts"] if grove else 0), ceiling=swarm.COLLIDER_CEILING),
    )
    with open(out_path, "w") as fh:
        json.dump(layout, fh, indent=1)
    print(f"layout: {len(regions)} swarm regions, {len(substrate)} substrate populations, {len(builders)} colonies; "
          f"collider worst case {layout['colliders']['worst']} / {layout['colliders']['ceiling']} -> {out_path}")


if __name__ == "__main__":
    main()

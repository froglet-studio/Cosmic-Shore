#!/usr/bin/env python3
"""Author the LIVING ECOLOGY SUBSTRATE fauna - round 11b (Docs/SUBSTRATE_FAUNA.md) - from one model.

    python3 Tools/Build/author_substrate_fauna.py            # write
    python3 Tools/Build/author_substrate_fauna.py --check    # FAIL on any drift (reads the disk)

What it owns:
  * the .meta of every script in Assets/.../FloraAndFauna/Substrate/ and of that folder (stable guids, one owner each);
  * SubstrateAgent.prefab - a substrate agent's PROXY: author_swarm_fauna.tadpole_prefab()'s body (TadPoleFauna's
    spindle + body prism, network layer and authored crystal stripped) with SubstrateAgentFauna in the member's place;
  * Substrate{Pack,Locust,Lurker,Stampede,Mobber,Leech,Leviathan}Fauna.prefab - the heartless, bodiless population
    anchors (SubstrateFauna), one per species;
  * Assets/_SO_Assets/Substrate Fauna/: the seven SubstrateSpeciesSO assets - every species number is copied from
    Tools/Build/substrate_harness/game_params.json, which the substrate harness asserts IS the research parameter set
    plus the documented game deltas (test F) - and the seven FaunaConfigurationSO configs the Swarm cell's spawner
    seeds them from (one population each, in its band, its heart element).

The Swarm cell's spawn profile lists the seven configs through ONE hook in author_swarm_fauna.py (profile_entries()),
and its collider ceiling counts this script's proxies through another (proxy_colliders()) - the swarm generator owns
the cell, this script owns the substrate.

THE POPULATIONS (radii from the cell centre; the swarm's own shells are 470-620 / 690-840 / 910-1080):

    lurker    Mass   470-620u   seeded AT the Mass flora's crystals it mimics; creeps while unwatched
    pack      Time   690-1080u  6 long hunters over the middle and outer shells: they hunt the locusts and pilots
    locust    Space  910-1080u  a sparse cute cloud over the Space flora; dense + hungry -> a gregarious storm
  round 11-11 (Docs/SUBSTRATE_FAUNA.md §9) - the middle shell split into three 110-degree sector pens:
    mobber    Time   625-685u   5 roosts in the free gap; mobs a slow pilot, dives in turn, pecks a 0.25 drain
    stampede  Mass   690-840u   sector +X: 4 herds; the alarm flips them, the bulls climb it and charge
    leech     Charge 690-840u   sector +120 deg: puddles at the flora; pounce, latch, ride, sip a 0.25 drain
    leviathan Space  690-840u   sector -120 deg: a grazer school that assembles into a manta and gulps

The proxies are the substrate harness's measured ProxyCaps (test Q) and sum to the pre-11-11 39 (78 colliders): the
four new species were fitted by re-dividing the substrate's share, never by raising the cell's 1,200 ceiling.
"""
import argparse
import hashlib
import json
import math
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import author_swarm_fauna as swarm  # noqa: E402  (shared YAML helpers + the tadpole body)

A = lambda *p: os.path.join(REPO, "Assets", *p)
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "Substrate")
SO_DIR = A("_SO_Assets", "Substrate Fauna")
PREFAB_DIR = swarm.PREFAB_DIR
GAME_PARAMS = os.path.join(HERE, "substrate_harness", "game_params.json")
SCRIPTS = ["SubstrateSpecies", "SubstrateFields", "SubstrateCore", "SubstrateTickJob", "SubstrateSpeciesSO",
           "SubstrateAgentFauna", "SubstrateMemberRenderer", "SubstrateCellHost", "SubstrateFauna",
           "SubstrateKernel", "SubstrateAgentJob"]

ROOT_MB_FID = "6630180297114401201"     # SubstrateFauna on each anchor prefab
ROOT_GO_FID = "6630180297114401202"
ROOT_TR_FID = "6630180297114401203"
AGENT_MB_FID = swarm.TADPOLE_MB_FID     # the member MB keeps the tadpole's fileID (the derived body references it)

# species key -> everything the glue and the cell need. Band radii are the cell's (FaunaConfigurationSO.Band*Radius).
SPECIES = [
    dict(key="pack", title="Pack Hunter", element="Time", band=(690, 1080), seed=0, spread=40, at_flora=0,
         engage=260, proxies=7, bites=4, spawns=7),
    dict(key="locust", title="Locust", element="Space", band=(910, 1080), seed=0, spread=80, at_flora=0,
         engage=140, proxies=12, bites=16, spawns=8),
    dict(key="lurker", title="Lurker", element="Mass", band=(470, 620), seed=8, spread=30, at_flora=1,
         engage=160, proxies=4, bites=4, spawns=4),
    # round 11-11: the rest of the bestiary (Docs/SUBSTRATE_FAUNA.md §9). The 620-690 gap between the swarm's inner and
    # middle shells is free (the mobbers' roosts); the middle shell 690-840 is split into three 110-degree SECTORS
    # about the cell's Y axis (sector pens, SubstrateCore.SetSector) for the herd, the puddles and the school.
    dict(key="stampede", title="Stampede", element="Mass", band=(690, 840), seed=0, spread=40, at_flora=0,
         engage=200, proxies=6, bites=4, spawns=6, clusters=4, sector=((1, 0, 0), 55)),
    dict(key="mobber", title="Mobber", element="Time", band=(625, 685), seed=0, spread=30, at_flora=0,
         engage=120, proxies=4, bites=4, spawns=4, clusters=5),
    dict(key="leech", title="Leech", element="Charge", band=(690, 840), seed=0, spread=8, at_flora=1,
         engage=140, proxies=2, bites=4, spawns=2, sector=((-0.5, 0, 0.866), 55)),
    dict(key="leviathan", title="Leviathan", element="Space", band=(690, 840), seed=0, spread=60, at_flora=0,
         engage=200, proxies=4, bites=4, spawns=4, sector=((-0.5, 0, -0.866), 55)),
]
NEW_SPECIES = ("stampede", "mobber", "leech", "leviathan")
# the danger-prism effect a collision-free contact (a leech's sip) is applied through
DANGER_EFFECT_GUID = "c7ccaca885824b24b716b12148d77ce1"   # VesselElementalDebuffByDangerPrismEffect.asset
HARNESS = os.path.join(HERE, "substrate_harness", "Program.cs")
CELL_CAPACITY = 1024

# Round 11-12 (Docs/SUBSTRATE_FAUNA.md §7.7): the demo (Swarm) cell's pack plays the opt-in RING HOLD - "surrounded, then
# everything dives in at once": once its ring saturates around a pilot it coils for this long, then all strike together.
# game_params.json stays the research port (RingHoldSeconds 0, harness test F); this overlay is the cell's designed beat.
# The substrate harness reads the value back from the authored asset (groups P and the emotion export's pack_hold).
DEMO_OVERRIDES = {"pack": {"RingHoldSeconds": 6.0}}


def guid(name):
    return hashlib.md5(("cosmicshore-substrate-fauna:" + name).encode()).hexdigest()


def rel(p):
    return swarm.rel(p)


def script_guid(name):
    return guid(f"{rel(SCRIPT_DIR)}/{name}.cs")


def anchor_name(s):
    return f"Substrate{s['key'].capitalize()}Fauna.prefab"


def species_path(s):
    return os.path.join(SO_DIR, f"Substrate {s['title']} Species.asset")


def config_path(s):
    return os.path.join(SO_DIR, f"Substrate {s['title']} Fauna Config Data.asset")


AGENT_PREFAB = os.path.join(PREFAB_DIR, "SubstrateAgent.prefab")


# ── prefabs ────────────────────────────────────────────────────────────────────────────────

def agent_prefab():
    """The swarm member's body (derived every time, so a fix to the tadpole flows through) as a substrate agent."""
    t = swarm.tadpole_prefab()
    member = swarm.script_guid("SwarmTadpoleFauna")
    if t.count(member) != 1:
        raise SystemExit("SwarmTadpole body: expected exactly one member script reference to swap")
    t = t.replace(member, script_guid("SubstrateAgentFauna"))
    t = t.replace("m_Name: SwarmTadpole\n", "m_Name: SubstrateAgent\n", 1)
    return t


def anchor_prefab(s):
    t = swarm.anchor_prefab("Field")
    t = t.replace(swarm.ROOT_MB_FID, ROOT_MB_FID).replace(swarm.ROOT_GO_FID, ROOT_GO_FID).replace(swarm.ROOT_TR_FID, ROOT_TR_FID)
    t = t.replace(swarm.script_guid("SwarmFauna"), script_guid("SubstrateFauna"))
    t = re.sub(r"(?m)^  m_Name: SwarmFauna$", f"  m_Name: {anchor_name(s)[:-7]}", t)
    t = re.sub(r"(?m)^  config: .*$",
               f"  species: {{fileID: 11400000, guid: {guid(rel(species_path(s)))}, type: 2}}", t)
    if "species:" not in t or anchor_name(s)[:-7] not in t:
        raise SystemExit("anchor prefab: the swarm anchor template changed shape")
    return t


# ── assets ─────────────────────────────────────────────────────────────────────────────────

def yaml_value(v):
    if isinstance(v, bool):
        return "1" if v else "0"
    if isinstance(v, str):
        return v if v else "''"
    if isinstance(v, int):
        return str(v)
    return repr(float(v)).rstrip("0").rstrip(".") if "e" not in repr(float(v)) else repr(float(v))


def yaml_obj(d, ind):
    out = ""
    for k, v in d.items():
        if isinstance(v, dict):
            out += f"{ind}{k}:\n" + yaml_obj(v, ind + "  ")
        elif isinstance(v, list):
            # a float[] (the leviathan's body plan): Unity writes an empty array inline, a full one as a block
            out += f"{ind}{k}: []\n" if not v else f"{ind}{k}:\n" + "".join(f"{ind}- {yaml_value(x)}\n" for x in v)
        else:
            out += f"{ind}{k}: {yaml_value(v)}\n"
    return out


def species_asset(s, params):
    p = dict(params[s["key"]], **DEMO_OVERRIDES.get(s["key"], {}))
    h = swarm.HEARTS
    ax, half = s.get("sector", ((1, 0, 0), 0))
    return swarm.SO_HEADER % (script_guid("SubstrateSpeciesSO"), f"Substrate {s['title']} Species") + (
        "  species:\n" + yaml_obj(p, "    ") +
        f"  seedCount: {s['seed']}\n  seedSpread: {s['spread']}\n  seedAtFlora: {s['at_flora']}\n"
        f"  seedClusters: {s.get('clusters', 0)}\n"
        f"  sectorAxis: {{x: {ax[0]}, y: {ax[1]}, z: {ax[2]}}}\n  sectorHalfAngle: {half}\n"
        f"  contactEffect: {{fileID: 11400000, guid: {DANGER_EFFECT_GUID}, type: 2}}\n"
        f"  agentPrefab: {{fileID: {AGENT_MB_FID}, guid: {guid(rel(AGENT_PREFAB))}, type: 3}}\n"
        f"  memberShader: {{fileID: 4800000, guid: {swarm.guid(rel(swarm.MEMBER_SHADER))}, type: 3}}\n"
        f"  theme: {{fileID: 11400000, guid: {swarm.THEME}, type: 2}}\n"
        f"  heartWorldScale: {{x: {h['Charge']}, y: {h['Mass']}, z: {h['Space']}, w: {h['Time']}}}\n"
        "  heartPrismGap: 0.6\n  bodyThin: 0.6\n"
        f"  engageRadius: {s['engage']}\n  maxProxies: {s['proxies']}\n  proxyLingerSeconds: 2\n"
        f"  maxSpawnsPerFrame: {s['spawns']}\n  vesselRadius: 6\n"
        f"  biteRadius: 24\n  maxBitesPerTick: {s['bites']}\n"
        f"  cellCapacity: {CELL_CAPACITY}\n  simulateOffMainThread: 1\n  extinctLingerSeconds: 8\n"
        "  macroLod: 1\n  thawReserveSeconds: 5\n")


def config_asset(s):
    lo, hi = s["band"]
    return swarm.SO_HEADER % (swarm.SO_SCRIPT["fauna"], f"Substrate {s['title']} Fauna Config Data") + (
        f"  FaunaPrefab: {{fileID: {ROOT_MB_FID}, guid: {guid(rel(os.path.join(PREFAB_DIR, anchor_name(s))))}, type: 3}}\n"
        "  InitialSpawnCount: 1\n  PopulationSize: 1\n  SpawnProbability: 1\n  NetworkSynced: 0\n"
        "  FeedsPerOffspring: 0\n  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 10\n"
        "  MaxLivePopulation: 1\n  ReleaseTier: 0\n"
        f"  BandInnerRadius: {lo}\n  BandOuterRadius: {hi}\n  CenterFocusBias: 0\n"
        f"  Element: {swarm.ELEMENT_ID[s['element']]}\n"
        "  Variant:\n    Enabled: 0\n"
        "  SpreadElements: 0\n  ElementPalette: []\n")


# ── the hooks author_swarm_fauna.py calls ──────────────────────────────────────────────────

def profile_entries():
    """The Swarm cell spawn profile's SupportedFaunas lines for the substrate populations."""
    return "".join(f"  - {{fileID: 11400000, guid: {guid(rel(config_path(s)))}, type: 2}}\n" for s in SPECIES)


def body_volume():
    """Round 11c: the substrate's agent bodies in the MATURE cell, for the phase ladder (author_swarm_fauna.ladder).
    Every agent's body is a BindVirtualMass entry whose volume is its stock, so LiveVolume counts it. Modelled like the
    swarm bodies (a mature body, grown): each population (one per species per cell, MaxLivePopulation 1) at its full
    pool, each body halfway between its seed stock and the stock it splits at (mass is conserved: a split halves)."""
    params = json.load(open(GAME_PARAMS))
    return sum(params[s["key"]]["Capacity"] * 0.5 * (params[s["key"]]["Stock0"] + params[s["key"]]["BirthStock"])
               for s in SPECIES)


def proxy_colliders():
    """Worst case: every population fully engaged, two colliders (heart + body prism) per proxy."""
    return sum(2 * s["proxies"] for s in SPECIES)


# ── assemble + verify ──────────────────────────────────────────────────────────────────────

def emit():
    params = json.load(open(GAME_PARAMS))
    out = {}
    for d in (SCRIPT_DIR, SO_DIR):
        out[d + ".meta"] = swarm.FOLDER_META % guid(rel(d))
    for n in SCRIPTS:
        out[os.path.join(SCRIPT_DIR, n + ".cs.meta")] = swarm.SCRIPT_META % script_guid(n)
    out[AGENT_PREFAB] = agent_prefab()
    for s in SPECIES:
        out[os.path.join(PREFAB_DIR, anchor_name(s))] = anchor_prefab(s)
        out[species_path(s)] = species_asset(s, params)
        out[config_path(s)] = config_asset(s)
    for p in [p for p in out if not p.endswith(".meta")]:
        meta = swarm.PREFAB_META if p.endswith(".prefab") else swarm.ASSET_META
        out[p + ".meta"] = meta % guid(rel(p))
    return out, params


def verify(out, params):
    problems = []
    minted = {re.search(r"^guid: (\w+)", t, re.M).group(1): p for p, t in out.items() if p.endswith(".meta")}
    if len(minted) != sum(1 for p in out if p.endswith(".meta")):
        problems.append("two files this script owns share a guid")
    for g, p in minted.items():
        owners = subprocess.run(["grep", "-rl", "--include=*.meta", f"^guid: {g}", A()],
                                capture_output=True, text=True).stdout.splitlines()
        owners = [o for o in owners if os.path.abspath(o) != os.path.abspath(p)]
        if owners:
            problems.append(f"guid {g} of {rel(p)} is already owned by {rel(owners[0])}")
    # every .cs in the folder has a meta this script owns (a new glue file cannot ship without one)
    on_disk = sorted(f[:-3] for f in os.listdir(SCRIPT_DIR) if f.endswith(".cs"))
    if on_disk != sorted(SCRIPTS):
        problems.append(f"Substrate/ scripts {on_disk} differ from the ones this script owns {sorted(SCRIPTS)}")
    # the species YAML carries exactly the C# fields (a renamed field would silently drop a number in Unity)
    cs = open(os.path.join(SCRIPT_DIR, "SubstrateSpecies.cs")).read()
    regime = re.search(r"public struct SubstrateRegime\s*\{(.*?)public static", cs, re.S).group(1)
    regime_fields = re.findall(r"\b([A-Z]\w*)\s*(?=[,;])", regime)
    sp = re.search(r"public sealed class SubstrateSpeciesParams\s*\{(.*?)public SubstrateSpeciesParams Clone", cs, re.S).group(1)
    sp = re.sub(r"///.*", "", sp)
    sp_fields = []
    for decl in re.findall(r"public\s+(?:float\[\]|float|int|bool|string|SubstrateRegime)\s+([^;]+);", sp):
        sp_fields += [re.match(r"\s*(\w+)", part).group(1) for part in decl.split(",")]
    for k, p in params.items():
        if list(p.keys()) != sp_fields:
            problems.append(f"game_params.json {k}: fields {list(p.keys())} != SubstrateSpeciesParams {sp_fields}")
        for reg in ("Solitary", "Gregarious"):
            if list(p[reg].keys()) != regime_fields:
                problems.append(f"game_params.json {k}.{reg}: fields differ from SubstrateRegime {regime_fields}")
    # the SO's serialized field names are the ones this script writes
    so = open(os.path.join(SCRIPT_DIR, "SubstrateSpeciesSO.cs")).read()
    so_fields = re.findall(r"\[SerializeField[^\]]*\]\s*\w+\s+(\w+)", so)
    for s in SPECIES:
        written = re.findall(r"(?m)^  (\w+):", out[species_path(s)].split("m_EditorClassIdentifier:")[-1])
        if written != so_fields:
            problems.append(f"{rel(species_path(s))}: writes {written}, SubstrateSpeciesSO serializes {so_fields}")
    # the agent prefab is the swarm member body with the substrate member in its place - and nothing else
    t = out[AGENT_PREFAB]
    if swarm.script_guid("SwarmTadpoleFauna") in t or script_guid("SubstrateAgentFauna") not in t:
        problems.append("SubstrateAgent.prefab does not carry SubstrateAgentFauna in the member's place")
    for bad, what in (("d5a57f767e5e46a458fc5d3c628d0cbb", "NetworkObject"), ("818b214228314119900f4d9860f0762d", "FaunaNetworkSync"),
                      ("cccc6ba7985893f43841fccfbb53dc71", "authored crystal")):
        if bad in t:
            problems.append(f"SubstrateAgent.prefab still carries the {what}")
    # the laws: populations spawn in the cell's controlling domain (no domain prescribed per region) - the configs
    # carry no domain, and the anchors take the spawner's
    for s in SPECIES:
        if re.search(r"(?m)^  (Domain|domain|Team):", out[config_path(s)]):
            problems.append(f"{s['key']}: a fauna config must not prescribe a domain")
        lo, hi = s["band"]
        if lo <= swarm.NUCLEUS_RADIUS or hi >= swarm.MEMBRANE_RADIUS:
            problems.append(f"{s['key']}: band {s['band']} leaves the cytoplasm")
    for key, over in DEMO_OVERRIDES.items():
        sp_out = out[species_path(next(x for x in SPECIES if x["key"] == key))]
        for f, v in over.items():
            if f not in params[key]:
                problems.append(f"demo override {key}.{f} is not a SubstrateSpeciesParams field")
            elif not re.search(rf"(?m)^    {f}: {re.escape(yaml_value(v))}$", sp_out):
                problems.append(f"demo override {key}.{f} = {v} did not reach the species asset")
    if params["pack"]["PreyName"] != "locust":
        problems.append("the pack's prey is the locust (the food web the harness proves)")
    pack, locust = next(s for s in SPECIES if s["key"] == "pack"), next(s for s in SPECIES if s["key"] == "locust")
    if not (pack["band"][0] <= locust["band"][0] and pack["band"][1] >= locust["band"][1]):
        problems.append("the pack's band must cover the locusts' (a creature must never be led to food it cannot reach)")
    if pack["proxies"] < params["pack"]["Capacity"]:
        problems.append("every pack hunter must be able to be real at once (the all-at-once strike lands on proxies)")
    # round 11-11: the collider budget is RE-DIVIDED, never raised - the proxies are the harness's measured caps
    # (substrate_harness ProxyCaps, test Q: each cap still engages) and they sum to the pre-11-11 share (39 = 78 colliders)
    caps = {k: (int(c), float(e)) for k, c, e in
            re.findall(r'\("(\w+)", (\d+), ([\d.]+)f\)', open(HARNESS).read().split("ProxyCaps =", 1)[1].split("};", 1)[0])}
    for sp_ in SPECIES:
        if caps.get(sp_["key"]) != (sp_["proxies"], float(sp_["engage"])):
            problems.append(f"{sp_['key']}: proxies {sp_['proxies']} within {sp_['engage']} u != the harness's measured cap "
                            f"{caps.get(sp_['key'])} (substrate_harness/Program.cs ProxyCaps)")
    if sum(sp_["proxies"] for sp_ in SPECIES) != 39:
        problems.append(f"substrate proxies sum to {sum(sp_['proxies'] for sp_ in SPECIES)}, not the pre-11-11 share of 39 "
                        "(the four new species are fitted by re-dividing it - the 1,200 ceiling is never raised)")
    # placement: the new four sit off the builders' and the wearer's bands, inside the swarm's outer shell (so off the
    # threat grove's rim, which author_threat_flora.py keeps beyond it), and never share a (shell, sector) pen
    import author_builders as builders
    new = [sp_ for sp_ in SPECIES if sp_["key"] in NEW_SPECIES]
    for sp_ in new:
        lo, hi = sp_["band"]
        for b in builders.SPECIES:
            if lo < b["band"][1] and hi > b["band"][0]:
                problems.append(f"{sp_['key']}: band {sp_['band']} overlaps the {b['key']} band {b['band']}")
        if hi > 1080:
            problems.append(f"{sp_['key']}: band {sp_['band']} reaches past the swarm's outer shell toward the grove rim")
        if sp_["key"] not in params or params[sp_["key"]]["Name"] != sp_["key"]:
            problems.append(f"{sp_['key']}: no game set in game_params.json (run substrate_harness/run.sh export)")
    for i, a in enumerate(new):
        for b in new[i + 1:]:
            if not (a["band"][0] < b["band"][1] and b["band"][0] < a["band"][1]):
                continue
            if "sector" not in a or "sector" not in b:
                problems.append(f"{a['key']} and {b['key']} share a shell and one of them has no sector pen")
                continue
            (va, ha), (vb, hb) = a["sector"], b["sector"]
            na, nb = sum(x * x for x in va) ** 0.5, sum(x * x for x in vb) ** 0.5
            ang = math.degrees(math.acos(max(-1.0, min(1.0, sum(x * y for x, y in zip(va, vb)) / (na * nb)))))
            if ang < ha + hb:
                problems.append(f"{a['key']} and {b['key']}: sectors overlap ({ang:.0f} deg apart < {ha} + {hb})")
    # every population at its full pool fits the cell's one core (SubstrateCore capacity: AddPopulation refuses past it)
    total = sum(params[sp_["key"]]["Capacity"] for sp_ in SPECIES)
    if total > CELL_CAPACITY:
        problems.append(f"the populations' capacities sum to {total} > the cell substrate's {CELL_CAPACITY}")
    # the burn rules (burn-rules.md): a mobber's peck is a drain, 0.25; a leech's plate never burns, it sips 0.25
    if abs(params["mobber"]["ContactWeight"] - 0.25) > 1e-6 or params["leech"]["ContactWeight"] != 0 or \
       abs(params["leech"]["SipWeight"] - 0.25) > 1e-6:
        problems.append("burn rules: the mobber's peck and the leech's sip are drains (0.25), the leech's plate never burns")
    return problems


def stale(out):
    owned = {os.path.abspath(p) for p in out}
    return sorted(os.path.join(SO_DIR, f) for f in os.listdir(SO_DIR)
                  if os.path.abspath(os.path.join(SO_DIR, f)) not in owned) if os.path.isdir(SO_DIR) else []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    out, params = emit()
    problems = verify(out, params)
    print("Substrate fauna - seven populations on the Living Ecology substrate (rounds 11b, 11-11)\n")
    for s in SPECIES:
        p = params[s["key"]]
        sec = f", sector {s['sector'][1]} deg about {s['sector'][0]}" if "sector" in s else ""
        print(f"  {s['key']:<9} {s['element']:<6} {s['band'][0]}-{s['band'][1]}u  seed {s['seed'] or p['N0']:>3} "
              f"(cap {p['Capacity']}), proxies <= {s['proxies']} within {s['engage']}u{sec}")
    print(f"  proxy colliders, every population engaged: {proxy_colliders()}")
    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or swarm.read(p) != t]
        drift += [rel(p) + " (stale: no longer authored)" for p in stale(out)]
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {d}" for d in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every substrate asset matches the model.")
        return 0
    if problems:
        print("\nFAIL - refusing to write:")
        for p in problems:
            print("  - " + p)
        return 1
    for p, t in out.items():
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    gone = stale(out)
    for p in gone:
        os.remove(p)
    print(f"\nwrote {len(out)} files, removed {len(gone)} stale")
    return 0


if __name__ == "__main__":
    sys.exit(main())

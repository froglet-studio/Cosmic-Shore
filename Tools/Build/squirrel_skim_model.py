#!/usr/bin/env python3
"""
Squirrel skim-economy model - the numbers behind Docs/AISystem/SQUIRREL_SKIM.md.

A READER: it writes nothing. Every constant is read out of the SHIPPED assets by key (never
typed in here), so a retune of the vessel, the skim effect or the Skim Race track moves the
output instead of leaving a stale table behind - "a constant copied out of an asset is true only
on the day it is copied" (CLAUDE.md, Broadside).

What it models, and the one simplification it makes:

    target speed   = XDiff * ThrottleScaler * BoostMultiplier            VesselTransformer.ComputeThrottleTarget
    speed          -> target at LERP_AMOUNT per second (exponential)      VesselTransformer.StepTowardTarget
    boost          += addPerHit * energyMultiplier per prism skimmed      SkimmerBoostPrismEffect
    boost          -= BoostDecayRate per second, clamped [base, max]      VesselTransformer.DecayBoost
    prisms skimmed  = distance flown * DUTY / prismSpacing                (the simplification)

DUTY is the fraction of distance flown with the ribbon inside skim reach. Each prism that ENTERS
the skimmer sphere is one hit (SkimmerImpactor dispatches prism effects on enter; its STAY path is
commented out, so the three stay effects the Squirrel prefab still overrides never run), so
skimming a ribbon of spacing s at speed v yields v*duty/s hits per second.

It also measures three things the AI plans around: how far a crystal actually pulls the vessel
off the ribbon (Monte Carlo over the shipped anchors, the shipped 35 u jitter and the crystal
prefab's own trigger radius), the Squirrel's own wake (two rails - VesselPrismController's Gap
cut out of its BaseScale slab), and what one Boost Ring pass is worth.

Usage:
    python3 Tools/Build/squirrel_skim_model.py            # print the tables
    python3 Tools/Build/squirrel_skim_model.py --json     # machine-readable summary
"""
import json
import math
import os
import random
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT resolved wrong: {ROOT}"

P = lambda *parts: os.path.join(ROOT, *parts)
SQUIRREL = P("Assets", "_Prefabs", "Spacevessels", "Squirrel.prefab")
SKIMMER = P("Assets", "_Prefabs", "Spacevessels", "Components", "Skimmer.prefab")
BOOST_FX = P("Assets", "_SO_Assets", "Effects", "Skimmer Prism Effects", "SkimmerBoostPrismEffect.asset")
BOOST_MAX = P("Assets", "_SO_Assets", "Event Channels", "EventMaxBoostMultiplier.asset")
BOOST_BASE = P("Assets", "_SO_Assets", "Event Channels", "EventBaseBoostMultiplier.asset")
DRIFT = P("Assets", "_SO_Assets", "VesselActions", "Squirrel", "SquirrelDriftAction.asset")
TUBE = P("Assets", "_SO_Assets", "VesselActions", "Squirrel", "SquirrelTubeAction.asset")
SCENE = P("Assets", "_Scenes", "Multiplayer Scenes", "MinigameSkimRace.unity")
TRANSFORMER_CS = P("Assets", "_Scripts", "Controller", "Vessel", "VesselTransformer.cs")
PRISM_CONTROLLER_CS = P("Assets", "_Scripts", "Controller", "Vessel", "VesselPrismController.cs")
AI_INIT_CS = P("Assets", "_Scripts", "Controller", "Multiplayer", "ServerPlayerVesselInitializerWithAI.cs")
AIPILOT_GUID = "a58bf4fb65afa704194fe9e28e67d58d"   # AIPilot.cs.meta
MC_SEED = 20260928          # the crystal Monte Carlo is deterministic, so a re-run is a diff
MC_SAMPLES_PER_ANCHOR = 600


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def blocks(text):
    """Split a Unity YAML document into its '--- !u!' blocks."""
    return re.split(r"^--- !u!", text, flags=re.M)


def block_with(text, *needles):
    """The one block that contains every needle. Fails loudly on zero or several, because a
    silently-picked wrong block is a wrong number that looks right."""
    hits = [b for b in blocks(text) if all(n in b for n in needles)]
    assert len(hits) == 1, f"expected exactly one block containing {needles}, found {len(hits)}"
    return hits[0]


def num(block, key):
    m = re.search(rf"^\s*{re.escape(key)}:\s*(-?[0-9.eE+-]+)\s*$", block, flags=re.M)
    assert m, f"key '{key}' not found"
    return float(m.group(1))


def vec3(block, key):
    m = re.search(rf"^\s*{re.escape(key)}:\s*\{{x:\s*(-?[0-9.]+),\s*y:\s*(-?[0-9.]+),\s*z:\s*(-?[0-9.]+)\}}",
                  block, flags=re.M)
    assert m, f"vector key '{key}' not found"
    return tuple(float(g) for g in m.groups())


def override(prefab_text, property_path):
    """A nested-prefab modification's value (the Squirrel overrides its nested Skimmer's Scale)."""
    m = re.search(rf"propertyPath: {re.escape(property_path)}\s*\n\s*value:\s*(-?[0-9.]+)", prefab_text)
    assert m, f"override '{property_path}' not found"
    return float(m.group(1))


def targeted_override(prefab_text, file_id, source_guid, property_path):
    """A modification on ONE object of a source prefab, or None. The target line wraps in Unity's
    YAML (`- target: {fileID: N, guid: G,\\n        type: 3}`), so match across it."""
    m = re.search(rf"target: \{{fileID: {file_id}, guid: {source_guid},\s*type: \d+\}}\s*"
                  rf"propertyPath: {re.escape(property_path)}\s*value:\s*(-?[0-9.]+)", prefab_text)
    return float(m.group(1)) if m else None


_GUID_CACHE = {}


def guid_path(guid):
    """The asset whose OWN .meta carries this guid. Only the `guid:` line at the top of a .meta is
    ownership; a guid inside an externalObjects remap is a reference and must not match (the trap
    Docs/VESSEL_CONSTRUCTION.md §2 records)."""
    if guid in _GUID_CACHE:
        return _GUID_CACHE[guid]
    owner = re.compile(rf"^guid: {guid}\s*$", re.M)
    hits = []
    for root in (P("Assets", "_Prefabs"), P("Assets")):
        for dirpath, _, files in os.walk(root):
            for fn in files:
                if not fn.endswith(".meta"):
                    continue
                fp = os.path.join(dirpath, fn)
                with open(fp, encoding="utf-8", errors="replace") as f:
                    if owner.search(f.read(256)):
                        hits.append(fp[:-len(".meta")])
        if hits:
            break
    hits = sorted(set(hits))
    assert len(hits) == 1, f"guid {guid} is owned by {len(hits)} assets: {hits}"
    _GUID_CACHE[guid] = hits[0]
    return hits[0]


def crystal_capture_radius(crystal_manager_block):
    """World radius of the trigger a hull must touch to collect a Skim Race crystal: the prefab the
    scene's CrystalManager spawns, its root SphereCollider, times the root scale (a variant's
    override wins). The Squirrel's skimmer collects nothing (skimmerCrystalEffectsSO is empty) and
    only vacuums the crystal at 80 / scale = 4 u/s, so this sphere IS the capture volume."""
    m = re.search(r"crystalPrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32})", crystal_manager_block)
    assert m, "CrystalManager.crystalPrefab not found"
    variant_path = guid_path(m.group(1))
    variant = read(variant_path)
    src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", variant)
    base_guid = src.group(1) if src else None
    base_path = guid_path(base_guid) if src else variant_path
    base = read(base_path)
    colliders = [b for b in blocks(base) if b.startswith("135 &")]
    assert len(colliders) == 1, f"expected one SphereCollider on {base_path}, found {len(colliders)}"
    col_id = re.match(r"135 &(-?\d+)", colliders[0]).group(1)
    go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", colliders[0]).group(1)
    radius = num(colliders[0], "m_Radius")
    transforms = [b for b in blocks(base) if b.startswith("4 &") and f"m_GameObject: {{fileID: {go}}}" in b]
    assert len(transforms) == 1, "crystal collider's transform not found"
    assert "m_Father: {fileID: 0}" in transforms[0], "crystal collider is not on the prefab root"
    tr_id = re.match(r"4 &(-?\d+)", transforms[0]).group(1)
    scale = list(vec3(transforms[0], "m_LocalScale"))
    if base_guid:
        for i, axis in enumerate("xyz"):
            o = targeted_override(variant, tr_id, base_guid, f"m_LocalScale.{axis}")
            if o is not None:
                scale[i] = o
        o = targeted_override(variant, col_id, base_guid, "m_Radius")
        if o is not None:
            radius = o
    assert abs(scale[0] - scale[1]) < 1e-6 and abs(scale[0] - scale[2]) < 1e-6, f"non-uniform crystal scale {scale}"
    return radius * scale[0], os.path.relpath(variant_path, ROOT)


def load():
    sq = read(SQUIRREL)
    transformer = block_with(sq, "BoostDecayRate:", "DefaultThrottleScaler:")
    aipilot = block_with(sq, f"guid: {AIPILOT_GUID}")
    fx = read(BOOST_FX)
    scene = read(SCENE)
    track = block_with(scene, "prismSpacing:", "waypointScaleMultiplier:")
    crystals = block_with(scene, "anchorJitterRadius:", "crystalCountMode:")
    spawner = block_with(scene, "superShieldTrackPrisms:")

    lerp = re.search(r"LERP_AMOUNT\s*=\s*([0-9.]+)f", read(TRANSFORMER_CS))
    assert lerp, "LERP_AMOUNT not found in VesselTransformer.cs"
    skill = re.search(r"SelectedIntensity\.Value\s*\*\s*([0-9.]+)f", read(AI_INIT_CS))
    assert skill, "skill-from-intensity formula not found in ServerPlayerVesselInitializerWithAI.cs"

    energy = re.search(r"energyMultiplier:\s*\n(?:\s+\w+:.*\n)*?\s+Min:\s*([0-9.]+)\s*\n\s+Max:\s*([0-9.]+)", fx)
    assert energy, "energyMultiplier Min/Max not found"

    # The Squirrel's own wake: VesselPrismController cuts a Gap out of a BaseScale slab.
    trail = block_with(sq, "BaseScale:", "MinimumGap:", "initialWavelength:")
    volume = re.search(r"trailVolume:\s*\n\s+Enabled:\s*(\d)\s*\n\s+Value:\s*([0-9.]+)", trail)
    assert volume, "trailVolume not found on the Squirrel's VesselPrismController"
    rail_floor = re.search(r"RailFloorFraction\s*=\s*([0-9.]+)f", read(PRISM_CONTROLLER_CS))
    assert rail_floor, "RailFloorFraction not found in VesselPrismController.cs"

    tube = read(TUBE)
    tube_cd_mass = re.search(r"^\s*cooldownMultiplierAtFull(?:Time|Mass):\s*([0-9.]+)\s*$", tube, flags=re.M)
    assert tube_cd_mass, "Boost Ring Mass cooldown multiplier not found"
    capture, crystal_prefab = crystal_capture_radius(crystals)

    return {
        "lerp": float(lerp.group(1)),
        "throttle_scaler": num(transformer, "DefaultThrottleScaler"),
        "min_speed": num(transformer, "DefaultMinimumSpeed"),
        "boost_decay": num(transformer, "BoostDecayRate"),
        "turn_dps": min(num(transformer, "PitchScaler"), num(transformer, "YawScaler")),
        "rot_throttle_scaler": num(transformer, "RotationThrottleScaler"),
        "add_per_hit": num(fx, "addPerHit"),
        "energy_min": float(energy.group(1)),
        "energy_max": float(energy.group(2)),
        "danger_mult": num(fx, "dangerEnergyMultiplier"),
        "boost_max": num(read(BOOST_MAX), "_value"),
        "boost_base": num(read(BOOST_BASE), "_value"),
        "drift_mult": num(read(DRIFT), "Mult"),
        "tube_cooldown": num(tube, "cooldown"),
        "tube_cooldown_mult_at_mass10": float(tube_cd_mass.group(1)),
        "tube_offset": num(tube, "forwardOffset"),
        "tube_lead_seconds": num(tube, "leadSeconds"),
        "tube_segments": num(tube, "segments"),
        "tube_rings": num(tube, "rings"),
        "tube_radius": num(tube, "radius"),
        "tube_prism_scale": num(tube, "prismScale"),
        "tube_danger": num(tube, "danger") == 1,
        "trail_base_scale": vec3(trail, "BaseScale"),
        "trail_gap": num(trail, "Gap"),
        "trail_x_scaler": num(trail, "minBlockScale"),   # Initialize() sets XScaler = minBlockScale
        "trail_volume": float(volume.group(2)),          # Enabled 0 -> EvaluateLive returns Value
        "trail_volume_enabled": volume.group(1) == "1",
        "trail_wavelength": num(trail, "initialWavelength"),
        "trail_drift_shields": num(trail, "driftShieldsTrail") == 1,
        "rail_floor_fraction": float(rail_floor.group(1)),
        "crystal_capture_radius": capture,
        "crystal_prefab": crystal_prefab,
        "skimmer_sphere_radius": num(read(SKIMMER), "m_Radius"),
        "skimmer_scale_min": override(sq, "Scale.Min"),
        "skimmer_scale_max": override(sq, "Scale.Max"),
        "prism_spacing": num(track, "prismSpacing"),
        "track_prism_scale": vec3(track, "scale"),
        "marker_scale_mult": num(track, "waypointScaleMultiplier"),
        "super_shielded_track": num(spawner, "superShieldTrackPrisms") == 1,
        "crystal_jitter": num(crystals, "anchorJitterRadius"),
        "ai_throttle_low": num(aipilot, "defaultThrottleLow"),
        "ai_throttle_high": num(aipilot, "defaultThrottleHigh"),
        "ai_skill_per_intensity": float(skill.group(1)),
        "ai_drift": num(aipilot, "drift"),
    }


SHELL_SCALE = 3.0  # OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE (stella spike tips at s * half-extents)


def simulate(k, duty, xdiff, energy=1.0, seconds=30.0, b0=None, v0=None, dt=1 / 60):
    """Frame-stepped copy of the shipped update order. Returns (boost, speed, t_to_saturation)."""
    b = k["boost_base"] if b0 is None else b0
    v = xdiff * k["throttle_scaler"] * b if v0 is None else v0
    carry, t, t_sat = 0.0, 0.0, None
    while t < seconds:
        target = xdiff * k["throttle_scaler"] * b + k["min_speed"]
        v += (target - v) * min(1.0, k["lerp"] * dt)
        carry += v * dt * duty / k["prism_spacing"]
        hits = int(carry)
        carry -= hits
        b = min(k["boost_max"], b + hits * k["add_per_hit"] * energy)
        b = b - k["boost_decay"] * dt if b > 1 else min(1.0, b + k["boost_decay"] * dt)
        t += dt
        if t_sat is None and b >= k["boost_max"] - 0.05:
            t_sat = t
    return b, v, t_sat


def takeoff_duty(k, xdiff, boost, energy=1.0, spacing=None):
    """Skim duty at which dB/dt = 0 with speed settled on its target: the flip-flop's threshold.
    `spacing` is distance flown per skim hit (the ribbon's prism spacing by default)."""
    spacing = k["prism_spacing"] if spacing is None else spacing
    gain_per_duty = k["add_per_hit"] * energy * xdiff * k["throttle_scaler"] * boost / spacing
    return k["boost_decay"] / gain_per_duty


def skimmer_radius(k, space_level):
    """Skimmer reach at a Space level (0..10): the nested Skimmer's Scale ElementalFloat is linear
    between the Squirrel's Min and Max overrides, times the sphere's authored radius."""
    t = space_level / 10.0
    return k["skimmer_sphere_radius"] * (k["skimmer_scale_min"] + (k["skimmer_scale_max"] - k["skimmer_scale_min"]) * t)


def wake_rails(k):
    """The Squirrel's own wake, from VesselPrismController.CreateBlock: a slab BaseScale.x * XScaler
    wide with Gap cut out of its middle, so TWO rails either side of the flight line. The inner
    edge sits exactly at the half-gap whatever the volume scale (xShift = w/2 + halfGap)."""
    bx, by, bz = k["trail_base_scale"]
    half = min(k["trail_gap"] / 2, bx * k["trail_x_scaler"] * 0.5 * k["rail_floor_fraction"])
    w, h, l = bx * k["trail_x_scaler"] / 2 - half, by, bz
    f = k["trail_volume"] ** (1 / 3) if abs(k["trail_volume"] - 1) > 1e-6 else 1.0
    w, h, l = w * f, h * f, l * f
    return {"width": w, "height": h, "length": l, "inner": half, "centre": half + w / 2, "outer": half + w,
            "shielded_inner": half + w / 2 - SHELL_SCALE * w / 2, "spacing": k["trail_wavelength"]}


def crystal_excursions(k, scene_text, capture):
    """How far off the ribbon a crystal pulls the vessel. Each crystal is anchor + jitter on a
    uniform random sphere (CrystalManager.GetSpawnPointAroundAnchor); the hull must reach the
    crystal's trigger sphere, so the lateral shift needed is max(0, distance to the ribbon's
    centre line - capture radius). Deterministic (MC_SEED), per intensity."""
    track = block_with(scene_text, "prismSpacing:", "waypointScaleMultiplier:")
    crystals = block_with(scene_text, "anchorJitterRadius:", "crystalCountMode:")
    spline_hex = re.search(r"useSplinePerIntensity: ([0-9a-f]+)", track).group(1)
    spline_flags = [int(spline_hex[i:i + 2], 16) for i in range(0, len(spline_hex), 8)]
    tracks, anchors = position_lists(track), position_lists(crystals)
    rng = random.Random(MC_SEED)
    jitter = k["crystal_jitter"]

    def seg_dist(p, a, b):
        ab = [y - x for x, y in zip(a, b)]
        ap = [y - x for x, y in zip(a, p)]
        den = sum(c * c for c in ab) or 1e-9
        t = max(0.0, min(1.0, sum(x * y for x, y in zip(ap, ab)) / den))
        return math.dist(p, [x + t * c for x, c in zip(a, ab)])

    out = []
    for i, pts in enumerate(tracks):
        spline = i < len(spline_flags) and spline_flags[i] == 1
        samples = track_samples(pts, spline)
        segs = [(samples[j], samples[(j + 1) % len(samples)]) for j in range(len(samples))]
        dists = []
        for a in (anchors[i] if i < len(anchors) else []):
            d_anchor = min(seg_dist(a, s0, s1) for s0, s1 in segs)
            reach = d_anchor + 2 * jitter + 30.0
            local = [s for s in segs if min(math.dist(a, s[0]), math.dist(a, s[1])) <= reach] or segs
            for _ in range(MC_SAMPLES_PER_ANCHOR):
                z = rng.uniform(-1.0, 1.0)
                phi = rng.uniform(0.0, 2 * math.pi)
                r = math.sqrt(max(0.0, 1 - z * z))
                c = (a[0] + jitter * r * math.cos(phi), a[1] + jitter * r * math.sin(phi), a[2] + jitter * z)
                dists.append(min(seg_dist(c, s0, s1) for s0, s1 in local))

        def stats(values):
            values = sorted(values)
            n = len(values) or 1
            return {"zero_pct": round(100.0 * sum(1 for v in values if v <= 0.0) / n),
                    "mean": round(sum(values) / n, 1),
                    "p95": round(values[min(n - 1, int(0.95 * n))], 1) if values else 0.0,
                    "max": round(values[-1], 1) if values else 0.0}

        # From the ribbon's CENTRE LINE: how far a pilot riding the line itself must swing out.
        line = stats([max(0.0, d - capture) for d in dists])
        # From the SKIM SHELL's outer edge (Space 0): a pilot may sit anywhere around the ribbon
        # within skimmer reach of its envelope, so it can already be that much closer to the
        # crystal. Measured to the centre line, which lies inside the envelope for 9 of every 12 u,
        # so this is an UPPER bound on the excursion.
        shell = stats([max(0.0, d - capture - skimmer_radius(k, 0)) for d in dists])
        out.append({"intensity": i + 1, "from_line": line, "beyond_shell": shell})
    return out


def _norm(v):
    m = math.sqrt(sum(c * c for c in v)) or 1.0
    return tuple(c / m for c in v)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _diamond_distance(x, y, a, b):
    """Distance from (x, y) to the filled diamond |x|/a + |y|/b <= 1 - the super-shield's
    cross-section through a ribbon plate's centre (both stella tetrahedra cut it exactly there)."""
    x, y = abs(x), abs(y)
    if x / a + y / b <= 1.0:
        return 0.0
    ex, ey = -a, b                       # the one edge in this quadrant, from (a, 0) to (0, b)
    t = max(0.0, min(1.0, ((x - a) * ex + y * ey) / (ex * ex + ey * ey)))
    return math.hypot(x - (a + t * ex), y - t * ey)


def straight_line_duty(k, scene_text, capture, draws=60, step=8.0, seed=MC_SEED + 1):
    """What TODAY's pilot gets: it flies straight from one crystal to the next. Fraction of that
    line inside Space-0 skim reach of the ribbon's envelope, and how often the line passes
    through the envelope itself (a hull touch = boost reset). Plates are laid with
    LookRotation(tangent, world up) (SpawnableWaypointTrack), so each ribbon point's frame is
    right = up x tangent, plate-up = tangent x right; the envelope cross-section is the
    super-shield diamond, extruded along the ribbon (it narrows between plates, so this slightly
    OVER-counts crossings and skim reach alike)."""
    track = block_with(scene_text, "prismSpacing:", "waypointScaleMultiplier:")
    crystals = block_with(scene_text, "anchorJitterRadius:", "crystalCountMode:")
    spline_hex = re.search(r"useSplinePerIntensity: ([0-9a-f]+)", track).group(1)
    spline_flags = [int(spline_hex[i:i + 2], 16) for i in range(0, len(spline_hex), 8)]
    tracks, anchors = position_lists(track), position_lists(crystals)
    half = [s / 2 * SHELL_SCALE for s in k["track_prism_scale"]]   # (15, 1.5, 4.5)
    reach = skimmer_radius(k, 0)
    jitter = k["crystal_jitter"]
    rng = random.Random(seed)
    cell = 24.0
    out = []
    for i, pts in enumerate(tracks):
        a_list = anchors[i] if i < len(anchors) else []
        if len(a_list) < 2:
            continue
        spline = i < len(spline_flags) and spline_flags[i] == 1
        samples = track_samples(pts, spline, per_segment=160)
        n = len(samples)
        frames = []
        for j in range(n):
            t = _norm(tuple(b - a for a, b in zip(samples[j - 1], samples[(j + 1) % n])))
            right = _norm(_cross((0.0, 1.0, 0.0), t))
            frames.append((t, right, _cross(t, right)))
        grid = {}
        for j, sp in enumerate(samples):
            grid.setdefault(tuple(int(math.floor(c / cell)) for c in sp), []).append(j)

        def nearest(p):
            key = tuple(int(math.floor(c / cell)) for c in p)
            best, bj = float("inf"), None
            for r in (1, 2, 4, 8):
                for dx in range(-r, r + 1):
                    for dy in range(-r, r + 1):
                        for dz in range(-r, r + 1):
                            for j in grid.get((key[0] + dx, key[1] + dy, key[2] + dz), ()):
                                d = math.dist(p, samples[j])
                                if d < best:
                                    best, bj = d, j
                if bj is not None:
                    return bj
            return min(range(n), key=lambda j: math.dist(p, samples[j]))

        duties, crossings = [], 0
        legs = 0
        for a0, a1 in zip(a_list, a_list[1:] + a_list[:1]):
            for _ in range(draws):
                ends = []
                for a in (a0, a1):
                    z = rng.uniform(-1.0, 1.0)
                    ph = rng.uniform(0.0, 2 * math.pi)
                    r = math.sqrt(max(0.0, 1 - z * z))
                    ends.append((a[0] + jitter * r * math.cos(ph), a[1] + jitter * r * math.sin(ph), a[2] + jitter * z))
                c0, c1 = ends
                length = math.dist(c0, c1)
                m = max(2, int(length / step))
                inside, crossed = 0, False
                for q in range(m + 1):
                    u = q / m
                    p = tuple(x + (y - x) * u for x, y in zip(c0, c1))
                    j = nearest(p)
                    t, right, up = frames[j]
                    d = tuple(x - y for x, y in zip(p, samples[j]))
                    x = sum(a * b for a, b in zip(d, right))
                    y = sum(a * b for a, b in zip(d, up))
                    dd = _diamond_distance(x, y, half[0], half[1])
                    if dd <= 0.0:
                        crossed = True
                    elif dd <= reach:
                        inside += 1
                duties.append(inside / (m + 1))
                crossings += crossed
                legs += 1
        duties.sort()
        out.append({"intensity": i + 1, "legs": legs,
                    "mean_duty": round(sum(duties) / len(duties), 2),
                    "p90_duty": round(duties[int(0.9 * (len(duties) - 1))], 2),
                    "crossing_pct": round(100.0 * crossings / legs)})
    return out


def s_curve_lead(radius_u, shift):
    """Forward distance a two-arc S-curve on a circle of radius R needs to move sideways by `shift`."""
    if shift <= 0:
        return 0.0
    phi = math.acos(max(-1.0, 1 - shift / (2 * radius_u)))
    return 2 * radius_u * math.sin(phi)


def radius(speed, dps):
    return speed / math.radians(dps)


def position_lists(block):
    """Every '- positions:' list in a block (the waypoint track and the crystal anchors both
    author one list per intensity, index 0 = intensity 1)."""
    out, cur = [], None
    for line in block.splitlines():
        if re.match(r"\s*- positions:", line):
            cur = []
            out.append(cur)
            continue
        m = re.match(r"\s*- \{x: (-?[0-9.]+), y: (-?[0-9.]+), z: (-?[0-9.]+)\}", line)
        if m and cur is not None:
            cur.append(tuple(float(g) for g in m.groups()))
    return out


def catmull_rom(p0, p1, p2, p3, t):
    # SpawnableWaypointTrack.CatmullRom, transcribed.
    t2, t3 = t * t, t * t * t
    return tuple(0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3)
                 for a, b, c, d in zip(p0, p1, p2, p3))


def track_samples(points, spline, per_segment=80):
    n = len(points)
    out = []
    for s in range(n):
        for i in range(per_segment):
            t = i / per_segment
            if spline:
                out.append(catmull_rom(points[(s - 1) % n], points[s], points[(s + 1) % n], points[(s + 2) % n], t))
            else:
                a, b = points[s], points[(s + 1) % n]
                out.append(tuple(x + (y - x) * t for x, y in zip(a, b)))
    return out


def turn_angles(samples, stride):
    """Heading change (deg) across every window of `stride` samples - a closed loop, so it wraps."""
    n = len(samples)
    def d(a, b):
        v = tuple(y - x for x, y in zip(a, b))
        m = math.sqrt(sum(c * c for c in v)) or 1.0
        return tuple(c / m for c in v)
    out = []
    for i in range(n):
        h0 = d(samples[i - stride], samples[i])
        h1 = d(samples[i], samples[(i + stride) % n])
        dot = max(-1.0, min(1.0, sum(x * y for x, y in zip(h0, h1))))
        out.append(math.degrees(math.acos(dot)))
    return out


def track_report(k, scene_text):
    track = block_with(scene_text, "prismSpacing:", "waypointScaleMultiplier:")
    crystals = block_with(scene_text, "anchorJitterRadius:", "crystalCountMode:")
    spline_hex = re.search(r"useSplinePerIntensity: ([0-9a-f]+)", track).group(1)
    spline_flags = [int(spline_hex[i:i + 2], 16) for i in range(0, len(spline_hex), 8)]
    tracks, anchors = position_lists(track), position_lists(crystals)
    top = k["throttle_scaler"] * k["boost_max"]
    r_top, r_drift = radius(top, k["turn_dps"]), radius(top, k["turn_dps"] * k["drift_mult"])
    rows = []
    for i, pts in enumerate(tracks):
        spline = i < len(spline_flags) and spline_flags[i] == 1
        samples = track_samples(pts, spline)
        length = sum(math.dist(samples[j], samples[(j + 1) % len(samples)]) for j in range(len(samples)))
        # The sharpest heading change over one ribbon-prism of travel: a polyline's corner shows up
        # whole; a spline's smooth bend shows as its local curvature.
        step = length / len(samples)
        stride = max(1, int(round(k["prism_spacing"] / step)))
        worst_deg = max(turn_angles(samples, stride))
        # A corner of angle a flown on a circle of radius R leaves the ribbon by R*(1/cos(a/2) - 1)
        # at its apex - how far the vessel is thrown off the skim band if it does not drift.
        def cut(r, a):
            a = math.radians(a)
            return r * (1 / math.cos(a / 2) - 1) if a < math.pi * 0.999 else float("inf")
        a_list = anchors[i] if i < len(anchors) else []
        off = [min(math.dist(a, p) for p in samples) for a in a_list] or [0.0]
        rows.append({
            "intensity": i + 1, "spline": spline, "waypoints": len(pts),
            "length": round(length), "prisms": round(length / k["prism_spacing"]),
            "sharpest_turn_deg": round(worst_deg, 1),
            "apex_miss_top": round(cut(r_top, worst_deg), 1), "apex_miss_drift": round(cut(r_drift, worst_deg), 1),
            "anchor_off_max": round(max(off), 1),
            "crystal_off_max": round(max(off) + k["crystal_jitter"], 1),
        })
    return rows


def main():
    k = load()
    top = k["throttle_scaler"] * k["boost_max"]
    cruise = k["throttle_scaler"]
    skim_r0 = k["skimmer_sphere_radius"] * k["skimmer_scale_min"]
    skim_r10 = k["skimmer_sphere_radius"] * k["skimmer_scale_max"]
    half = [s / 2 for s in k["track_prism_scale"]]
    shell = [h * SHELL_SCALE for h in half] if k["super_shielded_track"] else half
    marker_shell = [h * k["marker_scale_mult"] for h in shell]

    summary = {"constants": k}
    rows = []
    print("== Squirrel skim economy (constants read from the shipped assets) ==")
    print(f"cruise {cruise:.0f} u/s -> top {top:.0f} u/s (boost {k['boost_base']:.0f}..{k['boost_max']:.0f}x); "
          f"+{k['add_per_hit']} per skimmed prism (x{k['energy_min']:.0f}..x{k['energy_max']:.0f} Time), "
          f"decay {k['boost_decay']}/s; ribbon spacing {k['prism_spacing']:.0f} u")
    print(f"skimmer radius {skim_r0:.1f} u (Space 0) .. {skim_r10:.1f} u (Space 10); "
          f"turn {k['turn_dps']:.0f} deg/s (x{k['drift_mult']} drifting), rotation lag tau = {1 / k['lerp']:.2f} s")
    print(f"Skim Race ribbon prism {k['track_prism_scale']}, super-shielded={k['super_shielded_track']} -> contact "
          f"envelope half-extents {tuple(round(s, 1) for s in shell)} (markers {tuple(round(s, 1) for s in marker_shell)}); "
          f"crystal = anchor + {k['crystal_jitter']:.0f} u on a random sphere")

    print("\n-- 1. Take-off: does skimming at DUTY from a standing start ever reach top speed? --")
    print(" XDiff  duty  boost@30s  speed@30s  t_to_5x")
    for xdiff in (1.0, 0.9):
        for duty in (1.0, 0.8, 0.7, 0.6, 0.5):
            b, v, ts = simulate(k, duty, xdiff)
            rows.append({"xdiff": xdiff, "duty": duty, "boost30": round(b, 2), "speed30": round(v, 1),
                         "t_sat": None if ts is None else round(ts, 1)})
            print(f" {xdiff:5.2f}  {duty:4.2f}   {b:6.2f}    {v:7.1f}    {'never' if ts is None else f'{ts:.1f}s'}")
    summary["takeoff"] = rows

    print("\n-- 2. The flip-flop thresholds (analytic, speed settled on target) --")
    th = {}
    for xdiff in (1.0, 0.9):
        up = takeoff_duty(k, xdiff, k["boost_base"])
        hold = takeoff_duty(k, xdiff, k["boost_max"])
        th[str(xdiff)] = {"takeoff": round(up, 3), "hold": round(hold, 3)}
        print(f" XDiff {xdiff:4.2f}: TAKE-OFF needs duty >= {up:.2f} at {k['boost_base']:.0f}x;"
              f" HOLDING {k['boost_max']:.0f}x needs only >= {hold:.2f}")
    up_t10 = takeoff_duty(k, 1.0, k["boost_base"], energy=k["energy_max"])
    print(f" (Time 10 doubles skim energy: take-off at full throttle drops to {up_t10:.2f})")
    ring_value = k["tube_segments"] * k["tube_rings"] * k["add_per_hit"]
    up_ring = takeoff_duty(k, 1.0, k["boost_base"] + ring_value)
    print(f" (one Boost Ring pass from {k['boost_base']:.0f}x -> {k['boost_base'] + ring_value:.1f}x: "
          f"take-off drops to {up_ring:.2f})")
    th["after_one_ring"] = round(up_ring, 3)
    summary["thresholds"] = th

    print("\n-- 3. The shipped AI throttle ladder (skill = intensity * "
          f"{k['ai_skill_per_intensity']}, XDiff = lerp({k['ai_throttle_low']}, {k['ai_throttle_high']}, skill)) --")
    ladder = []
    for intensity in (1, 2, 3, 4):
        skill = min(1.0, intensity * k["ai_skill_per_intensity"])
        xdiff = k["ai_throttle_low"] + (k["ai_throttle_high"] - k["ai_throttle_low"]) * skill
        need = takeoff_duty(k, xdiff, k["boost_base"])
        ladder.append({"intensity": intensity, "xdiff": round(xdiff, 3), "cruise": round(xdiff * cruise, 1),
                       "takeoff_duty": round(need, 2), "can_take_off": need <= 1.0})
        print(f" intensity {intensity}: XDiff {xdiff:.3f} -> {xdiff * cruise:5.1f} u/s unboosted; "
              f"take-off needs duty {need:.2f} -> {'possible' if need <= 1 else 'IMPOSSIBLE even with perfect skimming'}")
    print(f" AIPilot.drift on the Squirrel prefab: {int(k['ai_drift'])}")
    summary["ai_ladder"] = ladder

    print("\n-- 4. What one hull touch costs (VesselResetBoostPrismEffect: boost -> base) --")
    _, _, t_back = simulate(k, 1.0, 1.0, v0=cruise)
    print(f" back to {k['boost_max']:.0f}x after a reset: {t_back:.1f} s of PERFECT skimming at full throttle")
    summary["reset_recovery_s"] = round(t_back, 1)

    print("\n-- 5. Geometry the planner has to respect --")
    geo = {}
    for v in (cruise, top / 2, top):
        r = radius(v, k["turn_dps"])
        rd = radius(v, k["turn_dps"] * k["drift_mult"])
        geo[str(round(v))] = {"R": round(r, 1), "R_drift": round(rd, 1), "lag_distance": round(v / k["lerp"], 1)}
        print(f" at {v:5.0f} u/s: min turn radius {r:6.1f} u ({rd:5.1f} u drifting); "
              f"distance flown per rotation-lag tau {v / k['lerp']:6.1f} u")
    band_top = skim_r0 + shell[1]
    print(f" skim band OVER the ribbon (envelope at most +/-{shell[1]:.1f} u thick, peaking on the plate's "
          f"diagonals): outer edge {band_top:.1f} u from the mid-plane at Space 0 ({skim_r10 + shell[1]:.1f} u "
          f"at Space 10); inner edge = hull clearance (measure the hull at runtime; waypoint markers are "
          f"+/-{marker_shell[1]:.1f} u thick)")
    summary["geometry"] = geo

    print("\n-- 6. The Boost Ring (SquirrelTubeAction), measured --")
    lead = max(0.0, k["tube_lead_seconds"])                  # the SO clamps leadSeconds at 0
    inner = k["tube_radius"] - k["tube_prism_scale"] / 2     # cube, 'up' radial (BoostRingBuilder)
    t5 = k["energy_min"] + (k["energy_max"] - k["energy_min"]) * 0.5
    live_wire = ring_value * t5 * k["danger_mult"]
    print(f" {int(k['tube_rings'])} ring x {int(k['tube_segments'])} {'danger ' if k['tube_danger'] else ''}prisms "
          f"({k['tube_prism_scale']:.0f} u cubes) on an {k['tube_radius']:.0f} u radius, centred "
          f"max({k['tube_offset']:.0f}, speed x {lead:.1f} s) ahead of the NOSE -> always {k['tube_offset']:.0f} u")
    print(f" hull must stay inside {inner:.1f} u of the ring's axis; the Space-0 skimmer ({skim_r0:.1f} u) reaches "
          f"the wall from the axis")
    print(f" one pass: +{ring_value:.1f} boost at rest; at Time 5 ('Live Wire', danger x{k['danger_mult']:.0f}) "
          f"+{live_wire:.0f} -> saturates in one pass")
    print(f" cooldown {k['tube_cooldown']:.0f} s at rest -> {k['tube_cooldown'] * k['tube_cooldown_mult_at_mass10']:.0f} s "
          f"at Mass 10; reached in {k['tube_offset'] / cruise:.2f} s at cruise, {k['tube_offset'] / top:.2f} s at top speed")
    summary["boost_ring"] = {"value": round(ring_value, 2), "live_wire_value": round(live_wire, 1),
                             "hull_inner_radius": inner, "offset": k["tube_offset"]}

    print("\n-- 7. The Squirrel's own wake (two rails), measured --")
    wr = wake_rails(k)
    both_space = next((s / 2 for s in range(0, 21) if skimmer_radius(k, s / 2) > wr["inner"]), None)
    shift0 = wr["inner"] - skim_r0
    print(f" rails {wr['width']:.2f} x {wr['height']:.2f} x {wr['length']:.2f} u at +/-{wr['centre']:.2f} u either side of "
          f"the line flown (inner edge {wr['inner']:.2f} u); one prism per rail every {wr['spacing']:.0f} u "
          f"(the ribbon: every {k['prism_spacing']:.0f} u)")
    print(f" re-flying the same line: the hull has {wr['inner']:.2f} u to either rail (minus its own half-width); "
          f"the Space-0 skimmer ({skim_r0:.1f} u) needs a {shift0:.2f} u shift toward one rail to reach it")
    if both_space is not None:
        print(f" from Space {both_space:.1f} the skimmer reaches BOTH rails from the centre line "
              f"({2 / wr['spacing']:.3f} hits/u vs the ribbon's {1 / k['prism_spacing']:.3f})")
    print(f" take-off duty at full throttle: ribbon {takeoff_duty(k, 1.0, k['boost_base']):.2f}, one rail "
          f"{takeoff_duty(k, 1.0, k['boost_base'], spacing=wr['spacing']):.2f}, both rails "
          f"{takeoff_duty(k, 1.0, k['boost_base'], spacing=wr['spacing'] / 2):.2f}")
    if k["trail_drift_shields"]:
        print(f" drifting lays SHIELDED rails: their octahedra reach {wr['shielded_inner']:.2f} u from the line")
    summary["wake"] = {k2: round(v, 3) for k2, v in wr.items()}
    summary["wake"]["both_rails_from_space"] = both_space

    print("\n-- 8. The four Skim Race tracks, as the planner will meet them --")
    print(" I  kind    wpts  length  prisms  sharpest turn / prism  apex miss at top (drifting)  "
          "anchor->ribbon  worst crystal->ribbon")
    tracks = track_report(k, read(SCENE))
    for r in tracks:
        print(f" {r['intensity']}  {'spline' if r['spline'] else 'linear'}  {r['waypoints']:4d}  {r['length']:6d}  "
              f"{r['prisms']:6d}  {r['sharpest_turn_deg']:8.1f} deg          "
              f"{r['apex_miss_top']:6.1f} u ({r['apex_miss_drift']:5.1f} u)          "
              f"{r['anchor_off_max']:6.1f} u        {r['crystal_off_max']:6.1f} u")
    print(" (apex miss = how far off the ribbon a vessel is thrown at the sharpest corner if it holds "
          "top speed on its minimum circle; compare with the skim band's few units)")
    summary["tracks"] = tracks

    cap = k["crystal_capture_radius"]
    print(f"\n-- 9. Crystal excursions: how far a crystal pulls the vessel off the ribbon --")
    print(f" crystal trigger: {cap:.1f} u radius ({k['crystal_prefab']}); crystal = anchor + {k['crystal_jitter']:.0f} u "
          f"on a random sphere; {MC_SAMPLES_PER_ANCHOR} draws per anchor, seed {MC_SEED}")
    print(" (from the centre line = distance from the ribbon's centre line to the crystal minus the trigger radius; "
          "beyond the shell = that minus the Space-0 skimmer reach, i.e. how far a pilot already sitting on the "
          "crystal's side of the ribbon still has to leave the skim zone - an upper bound)")
    print(" I   from the centre line: none needed / mean / p95 / max      beyond the Space-0 skim shell: "
          "none needed / p95 / max      S-curve lead at top speed (p95 beyond the shell)")
    r_top = radius(top, k["turn_dps"])
    exc = crystal_excursions(k, read(SCENE), cap)
    for e in exc:
        ln, sh = e["from_line"], e["beyond_shell"]
        lead = s_curve_lead(r_top, sh["p95"])
        e["lead_p95_beyond_shell"] = round(lead, 1)
        print(f" {e['intensity']}   {ln['zero_pct']:3d}% / {ln['mean']:5.1f} / {ln['p95']:5.1f} / {ln['max']:5.1f} u"
              f"                       {sh['zero_pct']:3d}% / {sh['p95']:5.1f} / {sh['max']:5.1f} u"
              f"                    {lead:5.1f} u ({lead / top:.2f} s)")
    print(" (the hull only has to TOUCH the trigger, so its own half-width comes off every number above)")
    summary["crystal_excursions"] = exc

    print(f"\n-- 10. What TODAY's pilot gets: a straight line from one crystal to the next --")
    print(" I   legs x draws   skim duty along the line (mean / p90)   lines passing THROUGH the ribbon envelope")
    sl = straight_line_duty(k, read(SCENE), cap)
    for r in sl:
        print(f" {r['intensity']}      {r['legs']:5d}              {r['mean_duty']:.2f} / {r['p90_duty']:.2f}"
              f"                       {r['crossing_pct']:3d}%   (take-off needs {takeoff_duty(k, 1.0, 1.0):.2f})")
    print(" (the envelope is modelled as the plate-centre diamond extruded along the ribbon, so both columns "
          "are slight OVER-estimates; a line through the envelope is a hull touch = boost reset)")
    summary["straight_line"] = sl

    if "--json" in sys.argv:
        print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()

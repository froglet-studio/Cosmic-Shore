#!/usr/bin/env python3
"""Verify the freestyle toybox's SWITCH RING geometry against the shipped source + assets.

The switch law (Docs/ToySystem/ARCHITECTURE.md, "The switch") is: *the ring IS the trigger
volume, drawn at its own radius*. That is enforced in code -- `Toy.Initialize` reads its own
`SphereCollider`. What code CANNOT enforce is the layout consequence: a ring must

  1. enclose its own station's content (a ring inside its own ship is not a ring around it),
  2. not interpenetrate its neighbour's ring in a matrix, and
  3. carry NO TEXT - toys are read by their ring and icon, and the Toy Box menu is where a player
     learns their names (Docs/ToySystem/ARCHITECTURE.md, "Toys carry NO text"). Checked as the
     absence of any TextMeshPro type in the toy sources, so a label cannot quietly grow back.

(2) is what `ToyFactory.MaxRingSpacingFraction` exists to hold, and (1)-(2) move whenever a
station radius, a matrix spacing, or the toybox's body/trigger radii are retuned -- in DATA, where
no compiler sees it. So this script re-derives them from the SHIPPED constants and the SHIPPED
authored assets rather than from numbers pasted into a doc.

    python3 Tools/Build/toy_switch_ring_geometry.py           # table + PASS/FAIL
    python3 Tools/Build/toy_switch_ring_geometry.py --check    # exit 1 on any failure

Not a coverage claim: it checks the sites listed in SITES below, which is every fly-through
station the toybox builds today. Add a row when you add a toy.
"""

import argparse
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOY_FACTORY = ROOT / "Assets/_Scripts/Controller/Toys/ToyFactory.cs"
TOYBOX_CONTROLLER = ROOT / "Assets/_Scripts/Controller/Toys/ToyboxController.cs"
ASSET_DIR = ROOT / "Assets/_SO_Assets/Toys"

# Everything a freestyle toy is built from. ArkwayVoyageHud is the one exempt file: it is the
# screen-space leash COUNTDOWN (a warning with a clock on it), not text on a toy.
NO_TEXT_SOURCES = [
    ROOT / "Assets/_Scripts/Controller/Toys",
    ROOT / "Assets/_Scripts/ScriptableObjects/Toys",
    ROOT / "Assets/_Scripts/Controller/Environment/Ark.cs",
]
NO_TEXT_EXEMPT = {"ArkwayVoyageHud.cs"}
TEXT_TYPES = re.compile(r"\b(TMP_Text|TextMeshPro|TextMeshProUGUI|TextMesh|UnityEngine\.UI\.Text)\b")

# `CreateBareRoot(..., radius * 1.6f)` is the shared station trigger factor, repeated at every
# station builder. A VARIANT station uses the plain `StationRadius` like the species and hangar
# rows: it used to be `StationRadius * (1 + 0.35 * (L - 1))`, but lifeform levels are retired
# (Docs/ECOSYSTEM.md §40) and LifeformMatrixToy.BuildVariantGrid now passes `_def.StationRadius`.
# The variant's own crystal is scaled by its authored heart size, which is a MODEL-child scale
# and does not touch the station radius this file models.
STATION_TRIGGER_FACTOR = 1.6
# LifeformMatrixToy.BuildKingdomGrid: the KINGDOM row (Fauna / Flora / Vessels) is half again the
# radius of the species stations behind it, so the first row you meet is the biggest thing there.
LIFEFORM_KINGDOM_FACTOR = 1.5
# DomainChangerToySet.HubBodyFraction: its slots are switches now (the cone body they used to wear
# is reserved for a booster), and the only thing inside the ring is a hub sphere.
DOMAIN_HUB_BODY_FRACTION = 0.5


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def const(src: str, name: str) -> float:
    """Pull `public const float NAME = X;` out of the shipped C#."""
    m = re.search(rf"const\s+float\s+{name}\s*=\s*([0-9.]+)f", src)
    if not m:
        sys.exit(f"could not find const {name} in ToyFactory.cs")
    return float(m.group(1))


def serialized_float(src: str, name: str, default: float) -> float:
    """Pull `name: X` out of a serialized field in a .cs (`float name = X;`) or a .asset."""
    m = re.search(rf"^\s*{name}:\s*([0-9.]+)\s*$", src, re.M)
    return float(m.group(1)) if m else default


def asset(name: str) -> str:
    path = ASSET_DIR / f"{name}.asset"
    return read(path) if path.exists() else ""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="exit 1 on any failure")
    args = ap.parse_args()

    factory = read(TOY_FACTORY)
    tube = const(factory, "RingTubeFraction")
    clamp = const(factory, "MaxRingSpacingFraction")

    ctl = read(TOYBOX_CONTROLLER)
    body_r = float(re.search(r"float toyBodyRadius = ([0-9.]+)f", ctl).group(1))
    trigger_r = float(re.search(r"float toyTriggerRadius = ([0-9.]+)f", ctl).group(1))
    pole_body_r = float(re.search(r"float poleBodyRadius = ([0-9.]+)f", ctl).group(1))
    pole_trigger_r = float(re.search(r"float poleTriggerRadius = ([0-9.]+)f", ctl).group(1))

    cell = asset("Toy_CellSelector")
    vessel = asset("Toy_VesselChanger")
    life = asset("Toy_LifeformMatrix")
    paint = asset("Toy_Painting")

    cell_r = serialized_float(cell, "stationRadius", 18)
    cell_sp = serialized_float(cell, "stationSpacing", 110)
    vessel_sp = serialized_float(vessel, "stationSpacing", 60)
    life_r = serialized_float(life, "stationRadius", 12)
    life_sp = serialized_float(life, "stationSpacing", 90)
    paint_icon = serialized_float(paint, "iconScaleBodies", 2)
    paint_cluster = serialized_float(paint, "clusterSpacingBodies", 3.2)

    paint_r = body_r * paint_icon
    # PaintingGalleryToy.StationSpacing = max(TriggerRadius * 2.2, StationRadius * cluster)
    paint_sp = max(trigger_r * 2.2, paint_r * paint_cluster)

    # DomainChangerToySet slots are placed on the toybox's own body/trigger radii, laid out
    # `anglePerToyDeg` apart on the placement circle rather than on a matrix spacing. Their
    # "spacing" is therefore the CHORD between adjacent slots, and it is a function of the
    # placement radius - so this models the TIGHTEST circle the toybox can place on, its own
    # no-membrane `fallbackRadius`. (On the menu membrane, ~984u, every margin below is far
    # wider; the fallback is the case that has to hold.)
    domain_hub = body_r * DOMAIN_HUB_BODY_FRACTION
    angle_deg = float(re.search(r"float anglePerToyDeg = ([0-9.]+)f",
                                read(ROOT / "Assets/_Scripts/Controller/Toys/SwapToySetCoordinator.cs")).group(1))
    fallback_r = float(re.search(r"float fallbackRadius = ([0-9.]+)f", ctl).group(1))
    domain_sp = 2.0 * fallback_r * math.sin(math.radians(angle_deg) / 2.0)

    # (site, content radius, trigger radius, spacing or None)
    SITES = [
        ("toy root", body_r, trigger_r, None),
        # ToyboxPoleSwitch: today's activity / shuffle, one at each pole - never a neighbour.
        ("pole switch", pole_body_r, pole_trigger_r, None),
        ("Domain Changer slot", domain_hub, trigger_r, domain_sp),
        ("Cell Selector station", cell_r, cell_r * STATION_TRIGGER_FACTOR, cell_sp),
        ("Vessel Changer station", body_r, body_r * STATION_TRIGGER_FACTOR, vessel_sp),
        ("Lifeform kingdom station",
         life_r * LIFEFORM_KINGDOM_FACTOR,
         life_r * LIFEFORM_KINGDOM_FACTOR * STATION_TRIGGER_FACTOR, life_sp),
        # The hangar row uses the species station's exact geometry (same radius, same spacing), so
        # it is listed rather than re-derived - if the two ever diverge, this row is where it shows.
        ("Lifeform species station", life_r, life_r * STATION_TRIGGER_FACTOR, life_sp),
        ("Lifeform hangar station", life_r, life_r * STATION_TRIGGER_FACTOR, life_sp),
        ("Lifeform variant station", life_r, life_r * STATION_TRIGGER_FACTOR, life_sp),
        ("Painting gallery station", paint_r, paint_r * STATION_TRIGGER_FACTOR, paint_sp),
    ]

    # ToyEmblem's outer extent, the one thing that must fit INSIDE a toy root's ring.
    emblem_src = read(ROOT / "Assets/_Scripts/Controller/Toys/ToyEmblem.cs")
    orbit = const(emblem_src, "OrbitRadiusBodies")
    sat = const(emblem_src, "SatelliteRadiusBodies")
    emblem_outer = (orbit + sat) * body_r

    print(f"tube {tube}  clamp {clamp}  "
          f"body {body_r}  trigger {trigger_r}  emblem outer {emblem_outer:.1f}\n")
    header = f"{'site':<26}{'ring':>8}{'inner':>8}{'outer':>8}{'content':>9}{'gap':>8}"
    print(header)
    print("-" * len(header))

    failures = []
    for name, content, trigger, spacing in SITES:
        ring = trigger if spacing is None else min(trigger, max(1.0, spacing) * clamp)
        inner, outer = ring * (1 - tube), ring * (1 + tube)
        gap = (spacing - 2 * outer) if spacing else float("inf")

        print(f"{name:<26}{ring:>8.1f}{inner:>8.1f}{outer:>8.1f}{content:>9.1f}"
              f"{'' if spacing is None else f'{gap:>8.1f}'}")

        if inner <= content:
            failures.append(f"{name}: ring inner {inner:.1f} does not enclose content {content:.1f}")
        if spacing is not None and gap <= 0:
            failures.append(f"{name}: rings interpenetrate ({gap:.1f} between neighbours)")
        if name == "toy root" and emblem_outer >= inner:
            failures.append(f"{name}: emblem outer {emblem_outer:.1f} collides with ring inner {inner:.1f}")

    for src in NO_TEXT_SOURCES:
        files = [src] if src.is_file() else sorted(src.rglob("*.cs"))
        for f in files:
            if f.name in NO_TEXT_EXEMPT:
                continue
            for n, line in enumerate(read(f).splitlines(), 1):
                code = line.split("//", 1)[0]
                if TEXT_TYPES.search(code):
                    failures.append(f"{f.relative_to(ROOT)}:{n}: a toy carries text ({code.strip()})")

    print()
    if failures:
        for f in failures:
            print(f"FAIL  {f}")
        print(f"\n{len(failures)} failure(s). See Docs/ToySystem/ARCHITECTURE.md, 'The switch'.")
        return 1 if args.check else 0

    print("PASS  every ring encloses its content and clears its neighbours, and no toy carries text.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

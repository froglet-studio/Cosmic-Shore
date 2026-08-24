#!/usr/bin/env python3
"""Verify the freestyle toybox's SWITCH RING geometry against the shipped source + assets.

The switch law (Docs/ToySystem/ARCHITECTURE.md, "The switch") is: *the ring IS the trigger
volume, drawn at its own radius*. That is enforced in code -- `Toy.Initialize` reads its own
`SphereCollider`. What code CANNOT enforce is the layout consequence: a ring must

  1. enclose its own station's content (a ring inside its own ship is not a ring around it),
  2. clear its own label (`ToyFactory.AddRingedLabel` hangs the text above the rim), and
  3. not interpenetrate its neighbour's ring in a matrix.

(3) is what `ToyFactory.MaxRingSpacingFraction` exists to hold, and all three move whenever a
station radius, a matrix spacing, or the toybox's body/trigger radii are retuned -- in DATA, where
no compiler sees it. So this script re-derives them from the SHIPPED constants and the SHIPPED
authored assets rather than from numbers pasted into a doc.

    python3 Tools/Build/toy_switch_ring_geometry.py           # table + PASS/FAIL
    python3 Tools/Build/toy_switch_ring_geometry.py --check    # exit 1 on any failure

Not a coverage claim: it checks the sites listed in SITES below, which is every fly-through
station the toybox builds today. Add a row when you add a toy.
"""

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOY_FACTORY = ROOT / "Assets/_Scripts/Controller/Toys/ToyFactory.cs"
TOYBOX_CONTROLLER = ROOT / "Assets/_Scripts/Controller/Toys/ToyboxController.cs"
ASSET_DIR = ROOT / "Assets/_SO_Assets/Toys"

# `CreateBareRoot(..., radius * 1.6f)` is the shared station trigger factor, repeated at every
# station builder; and a variant station at level L is `StationRadius * (1 + 0.35 * (L - 1))`.
STATION_TRIGGER_FACTOR = 1.6
LIFEFORM_LEVEL5_FACTOR = 1.0 + 0.35 * (5 - 1)


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

    # AddRingedLabel: fontSize = contentRadius * K; height = ringOuter + fontSize * H.
    font_k = float(re.search(r"contentRadius \* ([0-9.]+)f", factory).group(1))
    label_h = float(re.search(r"fontSize \* ([0-9.]+)f", factory).group(1))

    ctl = read(TOYBOX_CONTROLLER)
    body_r = float(re.search(r"float toyBodyRadius = ([0-9.]+)f", ctl).group(1))
    trigger_r = float(re.search(r"float toyTriggerRadius = ([0-9.]+)f", ctl).group(1))

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

    # (label, content radius, trigger radius, spacing or None)
    SITES = [
        ("toy root", body_r, trigger_r, None),
        ("Cell Selector station", cell_r, cell_r * STATION_TRIGGER_FACTOR, cell_sp),
        ("Vessel Changer station", body_r, body_r * STATION_TRIGGER_FACTOR, vessel_sp),
        ("Lifeform species station", life_r, life_r * STATION_TRIGGER_FACTOR, life_sp),
        ("Lifeform variant L5",
         life_r * LIFEFORM_LEVEL5_FACTOR,
         life_r * LIFEFORM_LEVEL5_FACTOR * STATION_TRIGGER_FACTOR, life_sp),
        ("Painting gallery station", paint_r, paint_r * STATION_TRIGGER_FACTOR, paint_sp),
    ]

    # ToyEmblem's outer extent, the one thing that must fit INSIDE a toy root's ring.
    emblem_src = read(ROOT / "Assets/_Scripts/Controller/Toys/ToyEmblem.cs")
    orbit = const(emblem_src, "OrbitRadiusBodies")
    sat = const(emblem_src, "SatelliteRadiusBodies")
    emblem_outer = (orbit + sat) * body_r

    print(f"tube {tube}  clamp {clamp}  font x{font_k}  label +{label_h}xfont  "
          f"body {body_r}  trigger {trigger_r}  emblem outer {emblem_outer:.1f}\n")
    header = f"{'site':<26}{'ring':>8}{'inner':>8}{'outer':>8}{'content':>9}{'label':>8}{'gap':>8}"
    print(header)
    print("-" * len(header))

    failures = []
    for name, content, trigger, spacing in SITES:
        ring = trigger if spacing is None else min(trigger, max(1.0, spacing) * clamp)
        inner, outer = ring * (1 - tube), ring * (1 + tube)
        font = max(8.0, content * font_k)
        label = outer + font * label_h
        # A two-line label (line 2 at 60%) is 1.6 x font tall, TMP-anchored at its middle.
        label_bottom = label - 0.8 * font
        gap = (spacing - 2 * outer) if spacing else float("inf")

        print(f"{name:<26}{ring:>8.1f}{inner:>8.1f}{outer:>8.1f}{content:>9.1f}"
              f"{label:>8.1f}{'' if spacing is None else f'{gap:>8.1f}'}")

        if inner <= content:
            failures.append(f"{name}: ring inner {inner:.1f} does not enclose content {content:.1f}")
        if label_bottom <= outer:
            failures.append(f"{name}: label bottom {label_bottom:.1f} sits on the ring (outer {outer:.1f})")
        if spacing is not None and gap <= 0:
            failures.append(f"{name}: rings interpenetrate ({gap:.1f} between neighbours)")
        if name == "toy root" and emblem_outer >= inner:
            failures.append(f"{name}: emblem outer {emblem_outer:.1f} collides with ring inner {inner:.1f}")

    print()
    if failures:
        for f in failures:
            print(f"FAIL  {f}")
        print(f"\n{len(failures)} failure(s). See Docs/ToySystem/ARCHITECTURE.md, 'The switch'.")
        return 1 if args.check else 0

    print("PASS  every ring encloses its content, clears its own label, and clears its neighbours.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

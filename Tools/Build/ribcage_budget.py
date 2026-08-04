#!/usr/bin/env python3
"""Analytic budget for SpawnableRibcage. Keep in sync with the C# generator.

Counts are exact loop arithmetic; volume uses E[k^3] ~ 1.04 for Jit(s, 0.2)
(one uniform factor applied to all three axes)."""
import math

R          = 360.0     # cage shell radius
RIBS       = 48        # meridian great circles
BAR_STEP   = 17.0      # arc spacing along ribs and hoops
HOOP_COUNT = 21        # latitude hoops (odd, symmetric about the equator)
HOOP_SPAN  = 78.0      # outermost hoop latitude, degrees
BANDS, PER_STRUT = 6, 3
CROWN_LAT, CROWN_N = 84.0, 18
DANGER_EVERY = 19      # every Nth rib prism is a DANGER bar (one-hit, punishes contact)
JIT = ((1.2)**4 - (0.8)**4) / (4*0.2)

def hoop_lats():
    half = (HOOP_COUNT - 1) // 2
    out = [0.0]
    for i in range(1, half + 1):
        out += [HOOP_SPAN * i / half, -HOOP_SPAN * i / half]
    return out

def vol(s): return s[0]*s[1]*s[2]
BAR   = (3.6, 3.6, 16.0)
JOINT = (5.4, 5.4, 5.4)
STRUT = (2.4, 2.4, 11.0)
CROWN = (3.2, 3.2, 12.0)

LATS = hoop_lats()
rows = []
per_rib = int(round(2*math.pi*R / BAR_STEP))
rib_n = RIBS * per_rib
danger_n = sum(1 for rib in range(RIBS) for i in range(per_rib) if (rib*per_rib + i) % DANGER_EVERY == 0)
rows.append(("meridian ribs (shielded)", rib_n - danger_n, vol(BAR)*JIT, f"{RIBS} x {per_rib} minus danger"))
rows.append(("  of which DANGER bars", danger_n, vol(BAR)*JIT, f"every {DANGER_EVERY}th rib prism"))
hoop_n = sum(int(round(2*math.pi*R*math.cos(math.radians(l)) / BAR_STEP)) for l in LATS)
rows.append(("latitude hoops", hoop_n, vol(BAR)*JIT, f"{len(LATS)} hoops to +/-{HOOP_SPAN:.0f}deg"))
rows.append(("cross-lattice", RIBS*BANDS*PER_STRUT, vol(STRUT)*JIT, f"{RIBS}x{BANDS}x{PER_STRUT}"))
rows.append(("joints", RIBS*len(LATS), vol(JOINT)*JIT, f"{RIBS} x {len(LATS)}"))
rows.append(("polar crowns", 2*CROWN_N, vol(CROWN)*JIT, f"2 x {CROWN_N}"))

total_n = sum(r[1] for r in rows)
total_v = sum(r[1]*r[2] for r in rows)
print(f"{'structure':<28}{'count':>7}{'vol/prism':>11}{'volume':>12}   detail")
for n_, c, v, d in rows: print(f"{n_:<28}{c:>7}{v:>11.1f}{c*v:>12.0f}   {d}")
print("-"*84)
print(f"{'TOTAL':<28}{total_n:>7}{'':>11}{total_v:>12.0f}")

rib_gap  = 2*math.pi*R/RIBS
hoop_gap = (2*HOOP_SPAN/360.0)*2*math.pi*R/(len(LATS)-1)
print(f"\ncage radius {R:.0f}  (containment {R*0.94:.0f} - inside the shell so bars are never fauna food)")
print(f"spawn ring  {R*1.6:.0f}  (membrane is 1200)")
print(f"grille opening: {rib_gap:.1f}u x {hoop_gap:.1f}u  (squareness {max(rib_gap,hoop_gap)/min(rib_gap,hoop_gap):.2f})")
print("\nPhaseThresholds = baseline + Blob deltas (Docs/ECOSYSTEM.md §18):")
for name, dc, dv in (("Restless", 700, 11200), ("RestlessExit", 500, 8000),
                     ("Frenzy", 3600, 57600), ("FrenzyExit", 3000, 48000)):
    print(f"  {name:<13} count {total_n+dc:>6}   volume {total_v+dv:>10.0f}")

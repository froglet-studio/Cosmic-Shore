import math
R, RIBS, STEP = 300.0, 16, 17.0
HOOP_LATS = [0.0, 26.0, -26.0, 52.0, -52.0, 74.0, -74.0]
BANDS, PER_STRUT = 6, 3          # cross-lattice
CROWN_LAT, CROWN_N = 84.0, 18
JIT = ((1.2)**4 - (0.8)**4)/(4*0.2)
def vol(s): return s[0]*s[1]*s[2]
BAR   = (3.6, 3.6, 16.0)
JOINT = (5.4, 5.4, 5.4)
STRUT = (2.4, 2.4, 11.0)
CROWN = (3.2, 3.2, 12.0)

rows=[]
per_rib = int(round(2*math.pi*R/STEP))
rows.append(("meridian ribs", RIBS*per_rib, vol(BAR)*JIT, f"{RIBS} x {per_rib}"))
hn=0; det=[]
for lat in HOOP_LATS:
    n=int(round(2*math.pi*R*math.cos(math.radians(lat))/STEP)); hn+=n; det.append(f"{lat:+.0f}:{n}")
rows.append(("latitude hoops", hn, vol(BAR)*JIT, ",".join(det)))
rows.append(("cross-lattice", RIBS*BANDS*PER_STRUT, vol(STRUT)*JIT, f"{RIBS} pairs x {BANDS} bands x {PER_STRUT}"))
rows.append(("joints", RIBS*len(HOOP_LATS), vol(JOINT)*JIT, f"{RIBS} x {len(HOOP_LATS)}"))
rows.append(("polar crowns", 2*CROWN_N, vol(CROWN)*JIT, f"2 x {CROWN_N}"))
tn=sum(r[1] for r in rows); tv=sum(r[1]*r[2] for r in rows)
print(f"{'structure':<18}{'count':>7}{'vol/prism':>11}{'volume':>12}   detail")
for n_,c,v,d in rows: print(f"{n_:<18}{c:>7}{v:>11.1f}{c*v:>12.0f}   {d}")
print("-"*74); print(f"{'TOTAL':<18}{tn:>7}{'':>11}{tv:>12.0f}")
print(f"\nnominal-prism equivalents (volume/16): {tv/16:.0f}")
for tgt in (500,600,700):
    print(f"  target {tgt}: releases at {tgt//4}/{tgt//2}; 3-domain worst case {3*tgt} = {100*3*tgt/tn:.0f}% of the {tn}-prism cage")
print()
for lat in (0,52,74):
    g=2*math.pi*R*math.cos(math.radians(lat))/RIBS
    print(f"  rib gap at lat {lat:>2}: {g:6.1f}u")

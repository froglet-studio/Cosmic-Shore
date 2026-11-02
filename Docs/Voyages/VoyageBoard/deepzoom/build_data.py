"""Build the Voyage Board's real-object layers (deep zoom around Sol and Tumult).

Inputs (all public, cite on use):
  HYG v4.1 (astronexus, CC BY-SA 4.0)        - stars, Hipparcos/Yale/Gliese
  Harris 2010 globular clusters (via NASA HEASARC; free, cite McMaster source)
  Hunt & Reffert 2024 open clusters (A&A 686 A42; upstream columns only: name, position, distance, members, age)
Output: board coordinates in ly. Sol (0, 26000), centre at origin, x = -d_plane sin l, y = R0 - d_plane cos l,
z = 65 + d sin b (height above the midplane), matching voyage-board.html fromLD/zOf.
"""
import csv, json, math, sys
R0, ZSUN, PC = 26000.0, 65.0, 3.26156
M = [[-0.0548755604, -0.8734370902, -0.4838350155],
     [ 0.4941094279, -0.4448296300,  0.7469822445],
     [-0.8676661490, -0.1980763734,  0.4559837762]]
def eq2gal(v): return [sum(M[i][k]*v[k] for k in range(3)) for i in range(3)]
def board_from_gal(g):  # g in ly, galactic cartesian (x to centre, y to l=90, z north)
    return [-g[1], R0 - g[0], ZSUN + g[2]]
def board_from_radec(ra, dec, d_ly):
    ra, dec = math.radians(ra), math.radians(dec)
    v = [math.cos(dec)*math.cos(ra), math.cos(dec)*math.sin(ra), math.sin(dec)]
    return board_from_gal([c*d_ly for c in eq2gal(v)])
def board_from_lb(l, b, d_ly):
    l, b = math.radians(l), math.radians(b)
    return board_from_gal([d_ly*math.cos(b)*math.cos(l), d_ly*math.cos(b)*math.sin(l), d_ly*math.sin(b)])

src = sys.argv[1]
GREEK = {"Alp":"α","Bet":"β","Gam":"γ","Del":"δ","Eps":"ε","Zet":"ζ","Eta":"η","The":"θ","Iot":"ι","Kap":"κ","Lam":"λ","Mu":"μ","Nu":"ν","Xi":"ξ","Omi":"ο","Pi":"π","Rho":"ρ","Sig":"σ","Tau":"τ","Ups":"υ","Phi":"φ","Chi":"χ","Psi":"ψ","Ome":"ω"}
stars, names = [], []
MAX_LY = 1600   # Hipparcos distances are trustworthy to roughly 500 pc; past that the board's model fills in
with open(f"{src}/hyg/hyg/CURRENT/hygdata_v41.csv") as f:
    for r in csv.DictReader(f):
        try: d = float(r["dist"]) * PC; am = float(r["absmag"])
        except ValueError: continue
        if d <= 0 or d > MAX_LY or float(r["dist"]) >= 100000: continue
        if r["id"] == "0": x = [0.0, R0, ZSUN]
        else:
            g = eq2gal([float(r["x"])*PC, float(r["y"])*PC, float(r["z"])*PC]); x = board_from_gal(g)
        nm = r["proper"]
        if not nm and r["bayer"]:
            b = r["bayer"]; b2 = GREEK.get(b.split("-")[0][:3], b)
            nm = f'{b2}{("-"+b.split("-")[1]) if "-" in b else ""} {r["con"]}'
        elif not nm and r["flam"]: nm = f'{r["flam"]} {r["con"]}'
        rank = 3 if r["proper"] else 2 if r["bayer"] or r["flam"] else 0
        desig = f'HIP {r["hip"]}' if r["hip"] else f'HD {r["hd"]}' if r["hd"] else (r["gl"] or "").strip()
        try: ci = float(r["ci"])
        except ValueError: ci = 0.6
        sp = (r["spect"] or "")[:1]
        ni = -1
        if nm:
            names.append(nm + ("|" + desig if desig else "")); ni = len(names) - 1
        elif r["gl"].strip():
            names.append(r["gl"].strip()); ni = len(names) - 1; rank = 1
        hip = int(r["hip"]) if r["hip"] else 0
        stars.append([round(x[0], 1), round(x[1], 1), round(x[2], 1), round(am, 1), round(ci, 1), rank, ni, hip])

globs = []
with open(f"{src}/wenu/src/wenu/data/catalogs/globular_clusters/globular_clusters_harris_heasarc.csv") as f:
    for r in csv.DictReader(f):
        try: d = float(r["helio_distance"]) * 1000 * PC; l = float(r["lii"]); b = float(r["bii"])
        except ValueError: continue
        try: mv = float(r["abs_vmag"])
        except ValueError: mv = -6
        x = board_from_lb(l, b, d)
        globs.append([round(x[0]), round(x[1]), round(x[2]), round(mv, 1), r["name"].strip() + (" · " + r["alt_name"].strip() if r["alt_name"].strip() else ""), round(d)])

ocs = []
hdr = None
with open(f"{src}/wenu/src/wenu/data/catalogs/open_clusters/open_clusters_hunt_reffert2024_v1.ecsv") as f:
    import shlex
    for line in f:
        if line.startswith("#"): continue
        p = shlex.split(line)
        if hdr is None: hdr = p; continue
        r = dict(zip(hdr, p))
        if r["hunt_type"] != "o": continue
        try: d = float(r["distance_pc"]) * PC; ra = float(r["ra_deg"]); dec = float(r["dec_deg"])
        except ValueError: continue
        if not (d > 0): continue
        try: n = int(float(r["member_count"]))
        except ValueError: n = 0
        try: age = float(r["log_age_years"])
        except ValueError: age = float("nan")
        # brightness from the published member count only (the mirror's integrated magnitudes are its own derivation, not the catalog's)
        mv = -1.0 - 1.2*math.log10(max(n, 10))
        x = board_from_radec(ra, dec, d)
        al = [a for a in r["aliases"].split(",") if a and not a.startswith(("MWSC", "FSR", "UBC", "CWNU", "HSC"))]
        nm = r["identifier"].replace("_", " ")
        famous = any(a.startswith(("NGC", "IC_", "Melotte", "Collinder", "Messier", "M_")) for a in r["aliases"].split(","))
        ocs.append([round(x[0], 1), round(x[1], 1), round(x[2], 1), round(mv, 1), nm, n, None if math.isnan(age) else round(age, 2), 1 if famous else 0, round(d)])

out = {"stars": stars, "names": names, "globs": globs, "ocs": ocs,
       "src": {"stars": "HYG v4.1 (astronexus, CC BY-SA 4.0), within 1,600 ly", "globs": "Harris 2010 globular clusters (via NASA HEASARC)", "ocs": "Hunt & Reffert 2024 open clusters (A&A)"}}
json.dump(out, open(sys.argv[2], "w"), separators=(",", ":"), ensure_ascii=False)
print(len(stars), "stars;", len(names), "names;", len(globs), "globulars;", len(ocs), "open clusters")

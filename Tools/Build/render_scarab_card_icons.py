#!/usr/bin/env python3
"""Render the Scarab's card icons from its PROCEDURAL hull.

Every other hull's `SO_Class_*.IconActive` is hand-drawn card art in
`Assets/_Graphics/CardImages/`; the Scarab has no art, and the icon it shipped with was the
codex's `tool_vessel-changer__scarab.png` - which is byte-identical to the Sparrow's, because
the codex baker reads the prefab ASSET, where the Scarab's wrapped Sparrow renderer is still
enabled (ScarabHullBuilder hides it at Awake) and the real hull is an empty MeshFilter. So the
Arena carousel showed a Sparrow and called it the Scarab.

Until an artist draws the Scarab, its icon is the ship itself: this renders the SHIPPED
`ScarabHullForm.Generate` (compiled and run with Roslyn by `scarab_icon_harness/run.sh`) at the
proportions authored on `Scarab.prefab`, in the card family's palette (navy chassis, gold
carapace, inked outline), to

    Assets/_Graphics/CardImages/Scarab.png           (IconActive)
    Assets/_Graphics/CardImages/Scarab_Inactive.png  (IconInactive - the greyed variant)

plus their .meta files (sprite import settings cloned from Sparrow.png's, fixed guids so
SO_Class_Scarab keeps pointing at them across re-renders).

    python3 Tools/Build/render_scarab_card_icons.py           # (re)render
    python3 Tools/Build/render_scarab_card_icons.py --check   # fail if the shipped PNGs are stale

`--check` re-renders and compares bytes: a hull retune (any ScarabHullBuilder proportion, any
ScarabHullForm edit) changes the icon, and the icon is re-rendered by re-running this script,
never by hand. Deterministic: the C# runs in float32 on .NET, the rasterizer is pure Python,
the PNG encoder is zlib at a fixed level.

Needs a dotnet 8 SDK reachable through DOTNET_ROOT (default ~/.dotnet); see run.sh.
"""
import json
import math
import os
import re
import struct
import subprocess
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PREFAB = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels", "Scarab.prefab")
BUILDER_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "ScarabHullBuilder.cs.meta")
HARNESS = os.path.join(ROOT, "Tools", "Build", "scarab_icon_harness", "run.sh")
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "CardImages")
META_TEMPLATE = os.path.join(OUT_DIR, "Sparrow.png.meta")

OUTPUTS = {
    "Scarab.png": "7a3f9c2e5b1d4e8fa6c0b2d4e6f8a1c3",           # IconActive
    "Scarab_Inactive.png": "9c1e4b7a2d3f4a6e8b0c5d7f9e1a3b25",  # IconInactive
}

SIZE = 256          # the card family's size (Sparrow.png / Squirrel.png are 256 x 256)
SUPERSAMPLE = 3     # rasterize at 768 and box-filter down: the inked edge needs it
FILL = 0.86         # fraction of the frame the hull's larger screen extent occupies

SETTING_KEYS = [
    "length", "width", "domeHeight", "bellyDepth", "seamFraction", "elytraFront", "pronotumFront",
    "pronotumSwell", "striaeCount", "striaeDepth", "lengthSegments", "widthSegments", "hornLength",
    "hornCurve", "hornSides", "legLength", "legThickness", "abdomenHeight", "antennaLength",
    "antennaThickness",
]


# ── the prefab is authoritative ─────────────────────────────────────────────────

def read_builder_settings():
    with open(BUILDER_META, encoding="utf-8") as fh:
        guid = re.search(r"^guid:\s*([0-9a-f]{32})", fh.read(), re.M).group(1)
    with open(PREFAB, encoding="utf-8") as fh:
        text = fh.read()
    m = re.search(rf"m_Script: \{{fileID: 11500000, guid: {guid}, type: 3\}}\n(.*?)(?=^--- !u!)", text, re.M | re.S)
    assert m, "ScarabHullBuilder component not found on Scarab.prefab"
    block = m.group(1)
    settings = {}
    for key in SETTING_KEYS:
        km = re.search(rf"^  {re.escape(key)}:\s*(-?[0-9.]+)\s*$", block, re.M)
        assert km, f"Scarab.prefab: ScarabHullBuilder.{key} not authored"
        settings[key] = km.group(1)
    return settings


def run_harness(settings):
    env = dict(os.environ)
    env.setdefault("DOTNET_ROOT", os.path.expanduser("~/.dotnet"))
    args = ["bash", HARNESS] + [f"{k}={v}" for k, v in settings.items()]
    out = subprocess.run(args, env=env, capture_output=True, text=True)
    if out.returncode != 0:
        sys.stderr.write(out.stderr)
        raise SystemExit(f"scarab_icon_harness failed ({out.returncode})")
    return json.loads(out.stdout)


# ── tiny vector helpers ─────────────────────────────────────────────────────────

def sub(a, b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])
def dot(a, b): return a[0]*b[0] + a[1]*b[1] + a[2]*b[2]
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def norm(a):
    m = math.sqrt(dot(a, a))
    return (a[0]/m, a[1]/m, a[2]/m) if m > 1e-12 else (0.0, 0.0, 0.0)


# ── the shot ────────────────────────────────────────────────────────────────────
# Hull space: +Z is the nose, +Y up. The card family shows a three-quarter view with the nose
# toward the upper right, so the camera sits ahead, to the ship's right, and above.

CAMERA_DIR = norm((0.78, 0.62, 0.92))     # from the hull centre TOWARD the camera
KEY_LIGHT = norm((-0.45, 0.85, 0.55))      # upper-left-front, as the card art is lit
FILL_LIGHT = norm((0.6, -0.2, -0.3))

CHASSIS = ((0.11, 0.12, 0.24), (0.05, 0.05, 0.11))     # lit, shadow
SHELL = ((0.98, 0.74, 0.20), (0.62, 0.34, 0.06))
RIM = (0.55, 0.95, 0.30)                                 # the family's lime accent, on the rim only
INK = (0.04, 0.04, 0.08)


def build_view():
    f = (-CAMERA_DIR[0], -CAMERA_DIR[1], -CAMERA_DIR[2])   # camera looks toward the hull
    right = norm(cross((0.0, 1.0, 0.0), f))
    up = cross(f, right)
    return f, right, up


def project(v, f, right, up):
    # Depth grows TOWARD the camera, so the z-buffer keeps the larger value (the nearer surface).
    return dot(v, right), dot(v, up), dot(v, CAMERA_DIR)


def rasterize(parts, px):
    """Flat-fill every triangle with a z-buffer (nearest wins); returns rgb rows of size px x px."""
    f, right, up = build_view()
    tris = []   # (screen xy per vertex, depth, shaded colour)
    xs, ys = [], []
    for part in parts:
        verts = part["verts"]; normals = part["normals"]
        v3 = [(verts[i], verts[i+1], verts[i+2]) for i in range(0, len(verts), 3)]
        n3 = [(normals[i], normals[i+1], normals[i+2]) for i in range(0, len(normals), 3)]
        p2 = [project(v, f, right, up) for v in v3]
        for sx, sy, _ in p2:
            xs.append(sx); ys.append(sy)
        for lit, shadow, idx in ((CHASSIS[0], CHASSIS[1], part["chassis"]), (SHELL[0], SHELL[1], part["shell"])):
            for t in range(0, len(idx), 3):
                a, b, c = idx[t], idx[t+1], idx[t+2]
                # Face normal from geometry decides facing; vertex normals shade (smooth shell).
                fn = norm(cross(sub(v3[b], v3[a]), sub(v3[c], v3[a])))
                if dot(fn, CAMERA_DIR) <= 0.0:
                    continue   # back face
                n = norm((n3[a][0]+n3[b][0]+n3[c][0], n3[a][1]+n3[b][1]+n3[c][1], n3[a][2]+n3[b][2]+n3[c][2]))
                if dot(n, CAMERA_DIR) < 0.0:
                    n = fn
                key = max(0.0, dot(n, KEY_LIGHT))
                fill = max(0.0, dot(n, FILL_LIGHT)) * 0.25
                l = min(1.0, 0.18 + 0.82 * key + fill)
                rim = (1.0 - max(0.0, dot(n, CAMERA_DIR))) ** 4 * 0.55
                col = tuple(shadow[i] + (lit[i] - shadow[i]) * l for i in range(3))
                col = tuple(col[i] + (RIM[i] - col[i]) * rim for i in range(3))
                tris.append(((p2[a][0], p2[a][1]), (p2[b][0], p2[b][1]), (p2[c][0], p2[c][1]),
                             (p2[a][2] + p2[b][2] + p2[c][2]) / 3.0, col))

    # Fit: centre the screen bounds, scale the larger extent to FILL of the frame.
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    cx, cy = (minx + maxx) / 2.0, (miny + maxy) / 2.0
    extent = max(maxx - minx, maxy - miny)
    scale = px * FILL / extent
    def to_px(p):
        return (px / 2.0 + (p[0] - cx) * scale, px / 2.0 - (p[1] - cy) * scale)

    depth = [[-1e30] * px for _ in range(px)]
    rgb = [[None] * px for _ in range(px)]
    for a, b, c, z, col in tris:
        (ax, ay), (bx, by), (cx2, cy2) = to_px(a), to_px(b), to_px(c)
        x0 = max(0, int(math.floor(min(ax, bx, cx2)))); x1 = min(px - 1, int(math.ceil(max(ax, bx, cx2))))
        y0 = max(0, int(math.floor(min(ay, by, cy2)))); y1 = min(px - 1, int(math.ceil(max(ay, by, cy2))))
        det = (bx - ax) * (cy2 - ay) - (cx2 - ax) * (by - ay)
        if abs(det) < 1e-9:
            continue
        inv = 1.0 / det
        for y in range(y0, y1 + 1):
            py = y + 0.5
            row_d = depth[y]; row_c = rgb[y]
            for x in range(x0, x1 + 1):
                pxx = x + 0.5
                w0 = ((bx - pxx) * (cy2 - py) - (cx2 - pxx) * (by - py)) * inv
                w1 = ((cx2 - pxx) * (ay - py) - (ax - pxx) * (cy2 - py)) * inv
                w2 = 1.0 - w0 - w1
                if w0 < -1e-6 or w1 < -1e-6 or w2 < -1e-6:
                    continue
                if z > row_d[x]:
                    row_d[x] = z
                    row_c[x] = col
    return rgb


def ink_outline(rgb, px, width):
    """The card family's inked edge: paint INK on every empty pixel within `width` of the fill."""
    filled = [[c is not None for c in row] for row in rgb]
    out = [row[:] for row in rgb]
    offsets = [(dx, dy) for dx in range(-width, width + 1) for dy in range(-width, width + 1)
               if dx * dx + dy * dy <= width * width]
    for y in range(px):
        for x in range(px):
            if filled[y][x]:
                continue
            for dx, dy in offsets:
                nx, ny = x + dx, y + dy
                if 0 <= nx < px and 0 <= ny < px and filled[ny][nx]:
                    out[y][x] = INK
                    break
    return out


def downsample(rgb, px, factor):
    size = px // factor
    pixels = []
    for y in range(size):
        row = []
        for x in range(size):
            r = g = b = a = 0.0
            for sy in range(factor):
                for sx in range(factor):
                    c = rgb[y * factor + sy][x * factor + sx]
                    if c is None:
                        continue
                    r += c[0]; g += c[1]; b += c[2]; a += 1.0
            n = factor * factor
            if a > 0:
                row.append((r / a, g / a, b / a, a / n))   # colour of the covered part, coverage as alpha
            else:
                row.append((0.0, 0.0, 0.0, 0.0))
        pixels.append(row)
    return pixels


def greyed(pixels):
    """The family's inactive look (Sparrow_Inactive.png): grey with a trace of the accent."""
    out = []
    for row in pixels:
        r2 = []
        for r, g, b, a in row:
            lum = 0.299 * r + 0.587 * g + 0.114 * b
            grey = 0.28 + 0.36 * lum
            r2.append((grey + (r - lum) * 0.22, grey + (g - lum) * 0.22, grey + (b - lum) * 0.22, a))
        out.append(r2)
    return out


def encode_png(pixels):
    size = len(pixels)
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for r, g, b, a in row:
            raw += bytes((int(round(min(1.0, max(0.0, r)) * 255)), int(round(min(1.0, max(0.0, g)) * 255)),
                          int(round(min(1.0, max(0.0, b)) * 255)), int(round(min(1.0, max(0.0, a)) * 255))))
    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xffffffff)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def render():
    settings = read_builder_settings()
    hull = run_harness(settings)
    px = SIZE * SUPERSAMPLE
    rgb = rasterize(hull["parts"], px)
    rgb = ink_outline(rgb, px, SUPERSAMPLE * 2)
    active = downsample(rgb, px, SUPERSAMPLE)
    return {"Scarab.png": encode_png(active), "Scarab_Inactive.png": encode_png(greyed(active))}


def meta_for(guid):
    with open(META_TEMPLATE, encoding="utf-8") as fh:
        text = fh.read()
    return re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid}", text, count=1, flags=re.M)


def main(argv):
    check = "--check" in argv
    images = render()
    stale = []
    for name, guid in OUTPUTS.items():
        path = os.path.join(OUT_DIR, name)
        meta = path + ".meta"
        want_meta = meta_for(guid)
        have = open(path, "rb").read() if os.path.exists(path) else None
        have_meta = open(meta, encoding="utf-8").read() if os.path.exists(meta) else None
        if have != images[name] or have_meta != want_meta:
            stale.append(name)
            if not check:
                with open(path, "wb") as fh: fh.write(images[name])
                with open(meta, "w", encoding="utf-8", newline="\n") as fh: fh.write(want_meta)
    if check:
        if stale:
            print("STALE: " + ", ".join(stale) + " - re-run Tools/Build/render_scarab_card_icons.py")
            return 1
        print("OK: Scarab card icons match the shipped hull.")
        return 0
    print(("wrote " + ", ".join(stale)) if stale else "up to date")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

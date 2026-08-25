"""
Core library for building TextMeshPro SDF font assets outside the Unity Editor.

Every formula in here was DERIVED by measuring TMP-generated assets already in this
repo (see Docs/FONTS.md "How the model was validated"), never from documentation:

  * SDF encoding   alpha = 0.5 + d / (2 * (padding + 1)) , d in pixels, + inside.
                   Confirmed by the 18.2/px ramp measured off ChakraPetch-Regular SDF
                   and by that material's _GradientScale = padding + 1 = 7.
  * glyph table    FreeType at pixel size == pointSize, FT_LOAD_RENDER|NO_HINTING,
                   metrics >> 6. Reproduced the donor's 97 glyphs with 0 mismatches.
  * face info      solved against every SDFAA asset in the project (see FACE_MODEL).

Requires: freetype-py, numpy, fonttools.
"""
import hashlib, math, struct
import numpy as np
import freetype
from fontTools.ttLib import TTFont

SS_DEFAULT = 8          # supersample factor for the distance field
EDGE_BAND  = 1.5        # how far (in units of gradientScale) to gather edge points


# ---------------------------------------------------------------- face metrics
def face_info(ttf_path, point_size):
    """Return TMP's m_FaceInfo values. Formula provenance in the module docstring."""
    tt   = TTFont(ttf_path, fontNumber=0)
    face = freetype.Face(ttf_path)
    face.set_pixel_sizes(0, point_size)
    upem = tt['head'].unitsPerEm
    s    = point_size / upem
    hhea, post = tt['hhea'], tt['post']

    def bearing_y(ch):
        face.load_char(ch, freetype.FT_LOAD_RENDER | freetype.FT_LOAD_NO_HINTING)
        return face.glyph.metrics.horiBearingY / 64.0

    def advance(ch):
        face.load_char(ch, freetype.FT_LOAD_RENDER | freetype.FT_LOAD_NO_HINTING)
        return face.glyph.metrics.horiAdvance / 64.0

    ascent   = hhea.ascender  * s
    descent  = hhea.descender * s
    cap      = math.ceil(bearing_y('X'))
    mean     = math.ceil(bearing_y('x'))
    ul_thick = post.underlineThickness * s
    return dict(
        familyName=tt['name'].getDebugName(1) or '',
        styleName=tt['name'].getDebugName(2) or '',
        pointSize=point_size, scale=1.0, unitsPerEM=upem,
        lineHeight=(hhea.ascender - hhea.descender + hhea.lineGap) * s,
        ascentLine=ascent, capLine=float(cap), meanLine=float(mean),
        baseline=0.0, descentLine=descent,
        superscriptOffset=ascent, superscriptSize=0.5,
        subscriptOffset=descent,  subscriptSize=0.5,
        underlineOffset=face.underline_position * s,
        underlineThickness=ul_thick,
        strikethroughOffset=mean * 0.4, strikethroughThickness=ul_thick,
        tabWidth=float(math.floor(advance(' ') + 0.5)),
    )


# --------------------------------------------------------------- SDF rendering
def _edge_points(cov):
    """Sub-pixel outline points from an ANTIALIASED coverage map.

    Coordinate convention: pixel (r, c) has its CENTRE at (x=c, y=r).
    An outline crosses between two neighbouring pixels wherever their coverages
    straddle 0.5; linear interpolation puts the crossing where it really is,
    which matters most on diagonals -- thresholding alone leaves up to half a
    supersample pixel of error there and that is the whole residual.
    """
    xs, ys = [], []
    a, b = cov[:, :-1], cov[:, 1:]                       # vertical crossings
    m = (a - 0.5) * (b - 0.5) < 0
    r, c = np.nonzero(m)
    if r.size:
        av, bv = a[r, c], b[r, c]
        xs.append(c + (0.5 - av) / (bv - av)); ys.append(r.astype(float))
    a, b = cov[:-1, :], cov[1:, :]                       # horizontal crossings
    m = (a - 0.5) * (b - 0.5) < 0
    r, c = np.nonzero(m)
    if r.size:
        av, bv = a[r, c], b[r, c]
        xs.append(c.astype(float)); ys.append(r + (0.5 - av) / (bv - av))
    if not xs:
        return np.empty(0), np.empty(0)
    return np.concatenate(xs), np.concatenate(ys)


def render_glyph(face, codepoint, point_size, padding, ss=SS_DEFAULT):
    """Render one glyph's SDF tile.

    Returns (metrics_dict, tile) where tile is a uint8 array of shape
    (h + 2*padding, w + 2*padding), row 0 = TOP, or tile None for a blank glyph.
    """
    grad = padding + 1.0

    face.set_pixel_sizes(0, point_size)
    face.load_char(chr(codepoint), freetype.FT_LOAD_RENDER | freetype.FT_LOAD_NO_HINTING)
    g, m = face.glyph, face.glyph.metrics
    w, h = g.bitmap.width, g.bitmap.rows
    metrics = dict(width=m.width / 64.0, height=m.height / 64.0,
                   bearingX=m.horiBearingX / 64.0, bearingY=m.horiBearingY / 64.0,
                   advance=m.horiAdvance / 64.0, w=w, h=h)
    if w == 0 or h == 0:                                   # space and friends
        return metrics, None
    left, top = g.bitmap_left, g.bitmap_top

    face.set_pixel_sizes(0, point_size * ss)
    face.load_char(chr(codepoint), freetype.FT_LOAD_RENDER | freetype.FT_LOAD_NO_HINTING)
    G, B = face.glyph, face.glyph.bitmap
    if B.width == 0 or B.rows == 0:
        return metrics, None
    buf = np.frombuffer(bytes(B.buffer), dtype=np.uint8).reshape(B.rows, B.pitch)[:, :B.width]
    cov = buf.astype(np.float32) / 255.0
    mask = cov >= 0.5
    sl, st = G.bitmap_left, G.bitmap_top

    # FreeType's bitmap is TIGHT: where the glyph touches the bitmap edge there is
    # no set/unset transition, so that whole boundary yields no edge points and the
    # distance is then measured to some interior contour instead. Pad by one empty
    # pixel so every outer boundary is a real transition, then shift back.
    padded = np.zeros((cov.shape[0] + 2, cov.shape[1] + 2), dtype=np.float32)
    padded[1:-1, 1:-1] = cov
    ex, ey = _edge_points(padded)
    ex -= 1.0
    ey -= 1.0
    if ex.size == 0:
        return metrics, None

    # target-pixel centres over the padded tile, expressed in supersample index space
    ti = np.arange(-padding, w + padding)                   # columns
    tj = np.arange(-padding, h + padding)                   # rows (0 = top of glyph box)
    qx = (left + ti + 0.5) * ss - sl - 0.5                  # ss column coordinate
    qy = st - (top - tj - 0.5) * ss - 0.5                   # ss row coordinate

    band = EDGE_BAND * grad * ss                            # only edges this close can matter
    out = np.zeros((tj.size, ti.size), dtype=np.uint8)
    inside = _sample_inside(mask, qx, qy)

    order = np.argsort(ey)
    ey_s, ex_s = ey[order], ex[order]
    for jj, y in enumerate(qy):
        lo = np.searchsorted(ey_s, y - band, 'left')
        hi = np.searchsorted(ey_s, y + band, 'right')
        if hi <= lo:                                        # nothing near: fully in/out
            out[jj, :] = np.where(inside[jj, :], 255, 0)
            continue
        dx = qx[None, :] - ex_s[lo:hi, None]
        dy = y - ey_s[lo:hi, None]
        dist = np.sqrt((dx * dx + dy * dy).min(axis=0)) / ss
        sd = np.where(inside[jj, :], dist, -dist)
        out[jj, :] = np.clip(np.rint(255.0 * (0.5 + sd / (2.0 * grad))), 0, 255).astype(np.uint8)
    return metrics, out


def _sample_inside(mask, qx, qy):
    """Nearest-neighbour lookup of the binary mask at the query grid."""
    cx = np.clip(np.rint(qx).astype(int), -1, mask.shape[1])
    cy = np.clip(np.rint(qy).astype(int), -1, mask.shape[0])
    okx = (cx >= 0) & (cx < mask.shape[1])
    oky = (cy >= 0) & (cy < mask.shape[0])
    res = np.zeros((cy.size, cx.size), dtype=bool)
    if okx.any() and oky.any():
        sub = mask[np.ix_(cy[oky], cx[okx])]
        res[np.ix_(oky, okx)] = sub
    return res


# ------------------------------------------------------------------- packing
class ShelfPacker:
    """Row/shelf packer. Deterministic: same inputs -> same atlas, so --check works."""

    def __init__(self, width, height):
        self.w, self.h = width, height
        self.shelves = []            # (y, height, cursor_x)

    def place(self, w, h):
        for i, (y, sh, cx) in enumerate(self.shelves):
            if h <= sh and cx + w <= self.w:
                self.shelves[i] = (y, sh, cx + w)
                return cx, y
        top = self.shelves[-1][0] + self.shelves[-1][1] if self.shelves else 0
        if top + h > self.h or w > self.w:
            return None
        self.shelves.append((top, h, w))
        return 0, top


# --------------------------------------------------------------- TMP hashing
def tmp_hash(s):
    """TMP_TextUtilities.GetSimpleHashCode - djb2-xor, int32 wraparound."""
    h = 0
    for ch in s:
        h = ((h << 5) + h) ^ ord(ch)
        h &= 0xFFFFFFFF
    return struct.unpack('<i', struct.pack('<I', h))[0]


def stable_guid(key):
    return hashlib.md5(f"CosmicShore/Fonts/{key}".encode()).hexdigest()


def stable_file_id(key):
    """Deterministic, positive, and safely inside signed int64 (see asset-surgery §3)."""
    v = int(hashlib.md5(f"CosmicShore/Fonts/fileid/{key}".encode()).hexdigest()[:15], 16)
    assert 0 < v <= 0x7FFFFFFFFFFFFFFF
    return v

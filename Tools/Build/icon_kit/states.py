"""The STATES sheet: a card as the HUD draws it in every state it has, before and after - from the spec, one command.

Garrett judges a card by its states on the real card ("show the old when comparing"; "your before pictures are not
accurate or exhaustive"), and a card's parts are tinted by team and state (the domain colour, the domain's shielded
colour, the HUD blue, white, lime, danger). Until 2026-10-26 those sheets were scratch scripts; a spec now declares
its cards and `author.py` draws them:

    CARDS = [dict(
        name="afterburner",                      # the sheet: Docs/Icons/sheets/<hull>_<name>.png
        element="Time",                          # which flower the card sits under
        title="...",                             # one line of what the card teaches
        before="Sparrow_Afterburner",            # an icon NAME (the sprite as the last commit before this round had it:
                                                 # `before_rev`) or a repo path; drawn once per state column
        before_rev="f9e5f73e3~1",                # optional: the commit to read the old sprite from (default HEAD)
        states=[("rest", [("hull_rest", "domain"), ("jets_rest", "blue")]),
                ("boosting", [("hull_boost", "domain"), ("jets_boost", "blue")])],
        parts={"hull_rest": afterburner_hull_rest, ...},   # name -> draw() (a mask or layers)
        gauge={"rest": (0.3, None), ...},        # optional per-state plate gauge (fill 0..1, colour or None = the style's);
                                                 # before_gauge=True draws it on the BEFORE row too (a card that already had one)
        domains=("jade", "gold"),                # one AFTER row per sample domain
    )]

A part entry is (part, colour) or (part, colour, alpha). Colours: "domain", "shielded", "blue", "white", "lime",
"danger", or an (r, g, b, a) tuple. The sample domains come from the game's palette (studio-domains.js, generated
from the ColorSet asset); the shielded and blue samples are the HUD's authored values. The sheet is a JUDGING aid:
the Editor is the truth for the live colours.
"""
import io
import json
import os
import re
import subprocess

from PIL import Image, ImageDraw, ImageFont

import canvas as C
import card

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
NAVY = (14, 20, 44, 255)
PLATE = (0, 4, 31, 255)
FIXED = {
    "white": (255, 255, 255, 255),
    "blue": (56, 130, 255, 255),       # ElementalBarsConfigSO.blueColor
    "lime": (150, 235, 41, 255),       # ElementalBarsConfigSO.limeColor
    "danger": (255, 84, 26, 255),      # ElementalBarsConfigSO.fireColor - the danger tint the Squirrel's joust opponent wears
}
# the domains' SHIELDED signal colours as the sheet samples them (the live value is ThemeManagerData.GetShieldedSignalColor)
SHIELDED = {"jade": (104, 164, 255, 255), "ruby": (230, 127, 255, 255), "gold": (255, 214, 120, 255), "blue": (160, 180, 255, 255)}


def domain_colours():
    """{key: (r, g, b, a)} from the generated studio-domains.js (the game's palette)."""
    js = open(os.path.join(ROOT, "Docs", "Studios", "VesselStudio", "studio-domains.js"), encoding="utf-8").read()
    out = {}
    for key, hexv in re.findall(r"key: '(\w+)'.*?hex: 0x([0-9a-fA-F]{6})", js):
        out[key] = (int(hexv[0:2], 16), int(hexv[2:4], 16), int(hexv[4:6], 16), 255)
    return out


def _colour(mask_or_layers, rgba, alpha=1.0):
    im = C.render(mask_or_layers)
    r, g, b, a = im.split()
    return Image.merge("RGBA", (r.point(lambda v: v * rgba[0] // 255), g.point(lambda v: v * rgba[1] // 255),
                                b.point(lambda v: v * rgba[2] // 255), a.point(lambda v: int(v * alpha))))


def compose(parts, draws, domain_key, domains):
    """One state's icon: the parts composited in order, each in its colour for `domain_key`."""
    out = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    for entry in parts:
        name, colour = entry[0], entry[1]
        alpha = entry[2] if len(entry) > 2 else 1.0
        if isinstance(colour, tuple):
            rgba = colour
        elif colour == "domain":
            rgba = domains[domain_key]
        elif colour == "shielded":
            rgba = SHIELDED[domain_key]
        else:
            rgba = FIXED[colour]
        out.alpha_composite(_colour(draws[name](), rgba, alpha))
    return out


def before_image(spec_hull, before, rev="HEAD"):
    """The old sprite: an icon name under the hull's folder, or a repo path; read from `rev`."""
    rel = before if "/" in before else f"Assets/_Graphics/Icons/AbilityIcons/{spec_hull}/{before}.png"
    data = subprocess.run(["git", "-C", ROOT, "show", f"{rev}:{rel}"], capture_output=True).stdout
    if not data:
        p = os.path.join(ROOT, rel)
        return Image.open(p).convert("RGBA") if os.path.exists(p) else None
    return Image.open(io.BytesIO(data)).convert("RGBA")


def _tiles(sheet, im, x, yy):
    for s in (60, 40):
        t = Image.new("RGBA", (s + 16, s + 16), PLATE)
        t.alpha_composite(im.resize((s, s), Image.LANCZOS), (8, 8))
        sheet.alpha_composite(t, (x, yy + 60))
        x += t.width + 8
    t = Image.new("RGBA", (144, 144), PLATE)
    t.alpha_composite(im, (8, 8))
    sheet.alpha_composite(t, (x, yy + 20))


def sheet(spec, c, out):
    """Draw one CARDS entry of `spec` to `out`. Returns the path."""
    domains = domain_colours()
    font = ImageFont.load_default()
    element = c["element"]
    states = c["states"]
    gauges = c.get("gauge", {})
    sample = c.get("domains", ("jade", "gold"))
    cw, chh = card.card(None, element).width, card.card(None, element).height
    rows = 1 + len(sample)
    im = Image.new("RGBA", (max(2, len(states)) * (cw + 8) + 76 + 56 + 150 + 30, 24 + rows * (chh + 70)), NAVY)
    d = ImageDraw.Draw(im)
    d.text((10, 6), f"{spec.HULL} {element.upper()} card - {c.get('title', c['name'])}", fill=(230, 230, 230, 255), font=font)

    def row(y, label, imgs, with_gauge=True):
        d.text((10, y), label, fill=(230, 230, 230, 255), font=font)
        x, yy = 10, y + 26
        for (name, icon) in imgs:
            g = gauges.get(name) if with_gauge else None
            cd = card.card(icon, element, gauge=g[0] if g else None, gauge_color=g[1] if g else None)
            im.alpha_composite(cd, (x, yy))
            d.text((x, yy + cd.height + 2), name[:30], fill=(170, 170, 190, 255), font=font)
            x += cd.width + 8
        _tiles(im, imgs[min(1, len(imgs) - 1)][1], max(x, 10 + 2 * (cw + 8)), yy)

    y = 24
    old = before_image(spec.HULL, c["before"], c.get("before_rev", "HEAD")) if c.get("before") else None
    if old is not None:
        # the old card is drawn WITHOUT this round's gauge: a meter the card did not have is not part of its before
        row(y, f"BEFORE  {c['before']} as committed ({c.get('before_rev', 'HEAD')})", [(n, old) for n, _p in states], with_gauge=c.get("before_gauge", False))
        y += chh + 70
    for key in sample:
        row(y, f"AFTER  {key.capitalize()} pilot", [(n, compose(parts, c["parts"], key, domains)) for n, parts in states])
        y += chh + 70
    im.save(out)
    return out

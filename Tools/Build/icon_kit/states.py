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
    "blue": (56, 130, 255, 255),       # ElementalBarsConfigSO.BlueColor
    "lime": (150, 235, 41, 255),       # ElementalBarsConfigSO.LimeColor
    "danger": (255, 84, 26, 255),      # ElementalBarsConfigSO.FireColor - the danger tint the Squirrel's joust opponent wears
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
            cd = card.card(icon, element, gauge=g[0] if g else None, gauge_color=g[1] if g else None, gauge_marks=c.get("gauge_marks"), gauge_marks_style=c.get("gauge_marks_style", "lines"))
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


ORDER = ("Charge", "Mass", "Space", "Time")   # the lockup, left to right (VesselHUDView.AbilityDisplayOrder)


def _lockup(spec, slot_element, slot_icon, slot_gauge, domain_key, domains, slot_marks=None, slot_style="lines"):
    """The hull's whole ability row as the HUD draws it, with `slot_icon` in `slot_element`'s card and every other card
    as the spec's LOCKUP has it today - so a candidate is judged next to its siblings, not alone."""
    cards = []
    for el in ORDER:
        if el == slot_element:
            cards.append(card.card(slot_icon, el, gauge=slot_gauge, gauge_marks=slot_marks, gauge_marks_style=slot_style))
            continue
        sib = spec.LOCKUP.get(el)
        if sib is None:
            cards.append(card.card(None, el, locked=True))
        else:
            cards.append(card.card(compose(sib["state"], sib["parts"], domain_key, domains), el, gauge=sib.get("gauge"),
                                   gauge_marks=sib.get("gauge_marks")))
    w = sum(c.width for c in cards) + 4 * (len(cards) - 1)
    row = Image.new("RGBA", (w, max(c.height for c in cards)), NAVY)
    x = 0
    for c in cards:
        row.alpha_composite(c, (x, 0))
        x += c.width + 4
    return row


def options_sheet(spec, group, entries, out, domain_key="jade"):
    """One OPTIONS sheet for a card (Garrett 2026-10-27: "going forward when showing vessel icons lets show the before
    and the other icons of the vessel so we can consider how they distinguish themselves too"): the BEFORE row, then
    one row per candidate - each the hull's full lockup with the candidate in its slot, beside the candidate's states.
    Built from the CARDS entries that share `group`; needs the spec's LOCKUP (its other cards today)."""
    domains = domain_colours()
    font = ImageFont.load_default()
    first = entries[0]
    element = first["element"]
    rows = []
    old = before_image(spec.HULL, first["before"], first.get("before_rev", "HEAD")) if first.get("before") else None
    if old is not None:
        rows.append((f"BEFORE  the {element} card as committed ({first['before']}), in the lockup", _lockup(spec, element, old, first.get("before_gauge_value"), domain_key, domains, first.get("gauge_marks")), []))
    for c in entries:
        states = c["states"]
        gauges = c.get("gauge", {})
        shown = states[c.get("lockup_state", 0)]
        g = gauges.get(shown[0])
        icon = compose(shown[1], c["parts"], domain_key, domains)
        others = []
        for name, parts in states:
            gg = gauges.get(name)
            others.append((name, card.card(compose(parts, c["parts"], domain_key, domains), element, gauge=gg[0] if gg else None,
                                           gauge_color=gg[1] if gg else None, gauge_marks=c.get("gauge_marks"), gauge_marks_style=c.get("gauge_marks_style", "lines"))))
        rows.append((c.get("title", c["name"]), _lockup(spec, element, icon, g[0] if g else None, domain_key, domains, c.get("gauge_marks"), c.get("gauge_marks_style", "lines")), others))
    lock_w = max(r[1].width for r in rows)
    cw = card.card(None, element).width
    width = 20 + lock_w + 40 + max(len(r[2]) for r in rows) * (cw + 8) + 20
    row_h = max(r[1].height for r in rows) + 60
    im = Image.new("RGBA", (max(width, 900), 30 + len(rows) * row_h), NAVY)
    d = ImageDraw.Draw(im)
    d.text((10, 8), f"{spec.HULL} {element.upper()} card - options in the lockup ({domain_key.capitalize()} pilot); the states of each candidate to the right",
           fill=(230, 230, 230, 255), font=font)
    y = 30
    for label, lock, others in rows:
        d.text((10, y), label[:150], fill=(255, 230, 120, 255), font=font)
        im.alpha_composite(lock, (10, y + 16))
        x = 10 + lock_w + 40
        for name, cd in others:
            im.alpha_composite(cd, (x, y + 16))
            d.text((x, y + 16 + cd.height + 2), name[:28], fill=(170, 170, 190, 255), font=font)
            x += cw + 8
        y += row_h
    im.save(out)
    return out

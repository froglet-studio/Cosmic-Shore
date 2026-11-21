#!/usr/bin/env python3
"""Writes the Style Guide tab of Frogma: the boards that show Docs/UI_STYLE_GUIDE.md on the canvas's FIRST page.

    python3 Docs/MenuRedesignB/rounds/style_guide_boards.py --live <canvas.json> --root <scratch dir>

It reads the colours from Docs/UI/froglet-tokens.json (Tools/Build/froglet_style.py writes it from the game), adds
the tab `style-guide` as the first page, the boards SG*.dc.html, a title note and its section, and points launch at
the tab. Gate with `frogma.py check <root> --only SG`, then publish the index and the boards in one call.
Every board is static markup: the guide's rules drawn as the components themselves, with the screen each rule comes
from named under it.
"""
from __future__ import annotations

import argparse
import html
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
sys.path.insert(0, os.path.join(REPO, ".claude", "skills", "frogma"))
import frogma  # noqa: E402

TOK = json.load(open(os.path.join(REPO, "Docs", "UI", "froglet-tokens.json"), encoding="utf-8"))
D = TOK["themes"]["deep-sea"]
S = TOK["themes"]["shore"]
W, H = 1920, 1080
TAB, TAB_NAME = "style-guide", "Style Guide"
TITLE_NOTE = "style-guide"


def e(s: str) -> str:
    return html.escape(s, quote=True)


def css_vars(theme: dict, prefix: str = "") -> str:
    return "".join(f"--{prefix}{k}:{v};" for k, v in theme.items())


HELMET = f"""<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{{margin:0;background:{D['ground-deep']}}}
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.14em;text-transform:uppercase;color:{D['text-soft']}}}
.h1{{font-family:'Aldrich',sans-serif;font-size:36px;color:{D['text']};margin:0}}
.h2{{font-family:'Aldrich',sans-serif;font-size:28px;color:{D['text']};margin:0}}
.h3{{font-family:'Aldrich',sans-serif;font-size:22px;color:{D['text']};margin:0}}
.body{{font-family:'Aldrich',sans-serif;font-size:18px;line-height:1.45;color:{D['text']};margin:0}}
.small{{font-family:'Aldrich',sans-serif;font-size:15px;line-height:1.4;color:{D['text-soft']};margin:0}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.sls{{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}}
.panel{{background:{D['surface']};box-shadow:inset 0 0 0 1px {D['line']}}}
.btn{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:18px;letter-spacing:.1em;text-transform:uppercase;border:0;padding:0 28px;height:52px;cursor:pointer}}
.num{{font-variant-numeric:tabular-nums}}
.src{{font-family:'Aldrich',sans-serif;font-size:14px;color:{D['text-soft']};margin:0}}
.do{{color:{D['cta']}}}
.dont{{color:{D['danger']}}}
</style>
</helmet>"""


def board(title: str, inner: str, w: int = W, h: int = H) -> str:
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{e(title)}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
{HELMET}
<div style="width: {w}px; height: {h}px; box-sizing: border-box; position: relative; overflow: hidden; background: {D['ground']}; color: {D['text']}; font-family: 'Aldrich', sans-serif; padding: 64px 72px; display: flex; flex-direction: column; gap: 28px">
{inner}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
class Component extends DCLogic {{
renderVals() {{
return {{}};
}}
}}
</script>
</body>
</html>
"""


def header(eyebrow: str, title: str, lede: str) -> str:
    return (f'<div style="display: flex; flex-direction: column; gap: 10px"><span class="lab">{e(eyebrow)}</span>'
            f'<h1 class="h1">{e(title)}</h1><p class="body" style="color: {D["text-soft"]}; max-width: 1500px">{e(lede)}</p></div>')


def swatch(hexv: str, label: str, size: int = 56, on: str | None = None) -> str:
    return (f'<div style="display: flex; align-items: center; gap: 12px"><span class="sls" style="width: {size}px; height: {size}px; '
            f'background: {hexv}; box-shadow: inset 0 0 0 1px rgba(255,255,255,.12)"></span>'
            f'<span style="display: flex; flex-direction: column; gap: 2px"><span class="lab" style="color: {D["text"]}">{e(label)}</span>'
            f'<span class="small num">{hexv}</span></span></div>')


# ------------------------------------------------------------------------------------------------ the boards

def sg_readme() -> str:
    rules = [
        ("Colour is information", "Six colour families, one meaning each. Nothing is coloured to look nice."),
        ("Elements have no colour", "Charge, Mass, Space and Time are told by shape and motion, never by a hue."),
        ("One shape", "Two opposite corners cut. Nothing is rounded. Hexagons only for icon tiles and handles."),
        ("Still unless it means something", "The sea lines and the card drift are the only idle motion."),
        ("Every word is authored", "All text a player reads lives in a field a person can edit."),
        ("Shared prefabs stay shared", "Change the prefab or make a variant. Never park overrides in a scene."),
    ]
    cards = "".join(
        f'<div class="sl panel" style="padding: 24px 28px; display: flex; flex-direction: column; gap: 10px">'
        f'<span class="lab" style="color: {D["cta"]}">Rule {i + 1}</span><h3 class="h3">{e(t)}</h3><p class="small">{e(b)}</p></div>'
        for i, (t, b) in enumerate(rules))
    surfaces = ["The game: Menu_Main, GameCanvas, the start flow", "Frogma boards", "Artifacts and diagrams",
                "Decks and presentations", "The website", "The Fleet Codex and every studio"]
    chips = "".join(f'<span class="sls lab" style="padding: 10px 16px; background: {D["surface"]}; color: {D["text"]}">{e(s)}</span>' for s in surfaces)
    return board("Frogma style guide", header(
        "Frogma style guide · start here",
        "One look for everything Froglet makes",
        "The rules every new screen, board, deck and page is checked against. The guide in words is "
        "Docs/UI_STYLE_GUIDE.md; the colours come from Garrett's colour chart (colour_domains.json) and the game's UITheme.")
        + f'<div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 20px">{cards}</div>'
        + f'<div style="display: flex; flex-direction: column; gap: 12px"><span class="lab">Where it applies</span>'
          f'<div style="display: flex; flex-wrap: wrap; gap: 12px">{chips}</div></div>'
        + f'<p class="src">Read in order: colour, themes, type, shape and space, buttons, cards and windows, motion, '
          f'do and don\'t, the PR checklist. Web pages load Docs/UI/froglet.css for the same tokens.</p>')


FAMILIES = [
    ("Lime", "lime", "Call to action, always", "The important thing to do now that you can still change: join, accept, claim; online, a done tick",
     "Headings, eyebrows, decoration, a choice you can change", [("cta", D["cta"])]),
    ("Fire", "fire", "Danger", "Delete my data, a guest-account warning, why a game cannot launch, connection lost",
     "Anything merely important", [("danger", D["danger"])]),
    ("Gold", "gold", "Spend, and the Gold domain", "A purchase, an upgrade, any button that uses a resource; the crystal glyph",
     "First place, a highlight, a reward amount", [("purchase", D["purchase"]), ("Gold domain", D["gold"])]),
    ("Jade", "jade", "Pick, change again", "A choice you can claim and reclaim: a hull pick in an arena mode, friends, the selected thing, the focus ring; toggles and sliders; Team 1",
     "An element, a commitment", [("Jade domain", D["jade"]), ("selection / control", D["control"])]),
    ("Ruby", "ruby", "No way back", "A commitment you cannot take back: Play (launching a game), Ready; Team 2",
     "An element, a choice you can change", [("Ruby domain", D["ruby"]), ("commit", D["commit"])]),
    ("Blue", "blue", "Neutral, and every grey", "Text, panels, lines, structure, information",
     "A call to action, a selection", [("text", D["text"]), ("text-soft", D["text-soft"]), ("surface", D["surface"])]),
]


def sg_colour() -> str:
    cards = []
    for name, _, means, use, never, toks in FAMILIES:
        sw = "".join(swatch(h, n, 48) for n, h in toks)
        cards.append(
            f'<div class="sl panel" style="padding: 22px 24px; display: flex; flex-direction: column; gap: 14px">'
            f'<div style="display: flex; align-items: baseline; justify-content: space-between"><h2 class="h2">{name}</h2>'
            f'<span class="lab">{e(means)}</span></div>'
            f'<div style="display: flex; flex-wrap: wrap; gap: 14px 22px">{sw}</div>'
            f'<p class="small"><span class="do">Use for</span> {e(use)}</p>'
            f'<p class="small"><span class="dont">Never for</span> {e(never)}</p></div>')
    return board("Colour: the six families", header(
        "1 · Colour",
        "Six families, one meaning each",
        "Garrett's colour chart (#1664) decides which family a colour belongs to, with a forbidden band between "
        "families. Jade is what you can change again, Ruby is what you cannot take back, Lime is always the call to action. "
        "A colour that means nothing is Blue. Elements carry no colour.")
        + f'<div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 20px">{"".join(cards)}</div>'
        + '<p class="src">Sources: Docs/VesselModels/colour_domains.json (families, checked by colour_domains.py --check), '
          'Assets/_SO_Assets/UI/UITheme.asset (UI tokens), the live palette (Nocturne since 2026-11-03; domains). Docs/UI/froglet-tokens.json collects them.</p>')


def mini_screen(theme: dict, name: str, note: str) -> str:
    gold_btn = theme.get("purchase-fill", theme.get("gold-fill"))
    return (
        f'<div class="sl" style="flex: 1; padding: 28px; background: {theme["ground"]}; box-shadow: inset 0 0 0 1px {theme["line"]}; '
        f'display: flex; flex-direction: column; gap: 18px">'
        f'<span class="lab" style="color: {theme["text-soft"]}">{e(name)}</span>'
        f'<h2 class="h2" style="color: {theme["text"]}">Weekly Regatta</h2>'
        f'<p class="body" style="color: {theme["text"]}">Three laps of the Lantern Reach. Fastest time wins the week.</p>'
        f'<p class="small" style="color: {theme["text-soft"]}">Secondary text reads at 4.5:1 or better.</p>'
        f'<div class="sl" style="padding: 18px; background: {theme["surface"]}; box-shadow: inset 0 0 0 1px {theme["line"]}; display: flex; gap: 16px; align-items: center">'
        f'<span class="lab" style="color: {theme["text-soft"]}">Best</span><span class="num" style="font-size: 44px; color: {theme["text"]}">01:12.48</span></div>'
        f'<div style="display: flex; gap: 14px; flex-wrap: wrap">'
        f'<button class="btn sls" type="button" style="background: {theme["commit-fill"]}; color: {theme["on-commit"]}">Play</button>'
        f'<button class="btn sls" type="button" style="background: {gold_btn}; color: {theme["on-fill"]}">Upgrade 40</button>'
        f'<button class="btn sls" type="button" style="background: transparent; color: {theme["danger"]}; box-shadow: inset 0 0 0 2px {theme["danger"]}">Leave</button></div>'
        f'<p class="small" style="color: {theme["text-soft"]}">{e(note)}</p></div>')


def sg_themes() -> str:
    doms = "".join(
        f'<div class="sl" style="flex: 1; padding: 18px 22px; background: {D["surface"]}; box-shadow: inset 4px 0 0 {D[k]}; display: flex; flex-direction: column; gap: 6px">'
        f'<span class="lab" style="color: {D[k]}">{k.title()} domain</span><span class="small num">{D[k]} · data-fs-domain="{k}"</span></div>'
        for k in ("jade", "ruby", "gold"))
    return board("Themes: Deep Sea and Shore", header(
        "2 · Themes",
        "Deep Sea, and its light twin Shore",
        "Every Froglet theme is built from the six families, nothing borrowed. Deep Sea is the game's own look and the "
        "default everywhere. Shore is for documents, print and daylight web pages. A domain is never a theme: it is one accent.")
        + f'<div style="display: flex; gap: 28px">{mini_screen(D, "Deep Sea · data-fs-theme=deep-sea", "The menu, the HUD, Frogma, the codex, decks.")}'
          f'{mini_screen(S, "Shore · data-fs-theme=shore", "Docs, print, the website by day. Gold is a fill under dark text, never text.")}</div>'
        + f'<div style="display: flex; gap: 20px">{doms}</div>'
        + '<p class="src">Names decided by Shombith (2026-11-05). The film-named codex themes (Silver Screen, Tesseract, '
          'Endurance, Gargantua, HyperSea) move to these.</p>')


def sg_type() -> str:
    rows = [("Display", "Aldrich", 48, "Victory"), ("H1", "Aldrich", 36, "The Hangar"), ("H2", "Aldrich", 28, "Weekly leaderboard"),
            ("H3", "Aldrich", 22, "Squad presets"), ("Body", "Aldrich", 18, "Three laps of the Lantern Reach. Fastest time wins."),
            ("Body small", "Aldrich", 15, "Unlocks at clout 4"), ("Button", "Chakra Petch 600", 18, "PLAY"),
            ("Button small", "Chakra Petch 600", 14, "CONFIGURE"), ("Data large", "Aldrich, tabular", 44, "01:12.48"),
            ("Data", "Aldrich, tabular", 20, "12,480"), ("Data small", "Aldrich, tabular", 15, "x3")]
    lines = []
    for role, fam, px, sample in rows:
        face = "'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.1em" if fam.startswith("Chakra") else "'Aldrich',sans-serif"
        lines.append(
            f'<div style="display: grid; grid-template-columns: 200px 220px 80px minmax(0, 1fr); align-items: baseline; gap: 20px; '
            f'padding: 8px 0; box-shadow: inset 0 -1px 0 {D["surface"]}">'
            f'<span class="lab" style="color: {D["text"]}">{role}</span><span class="small">{fam}</span>'
            f'<span class="small num">{px}</span><span class="num" style="font-family: {face}; font-size: {px}px; color: {D["text"]}; white-space: nowrap">{e(sample)}</span></div>')
    return board("Type and numbers", header(
        "3 · Type",
        "Aldrich to read, Chakra Petch to press",
        "Headings and body in Aldrich. Buttons in Chakra Petch SemiBold, always capitals. Emphasis is a colour shift, "
        "never italics. Any number that changes while you watch is tabular, so it never jitters.")
        + f'<div style="display: flex; flex-direction: column">{"".join(lines)}</div>'
        + '<p class="src">Sizes are canvas units at 1920 wide (STYLE_FOUNDATION.md section 4). Phones scale the canvas; '
          'never author a second, smaller set.</p>')


def sg_shape() -> str:
    spaces = "".join(
        f'<div style="display: flex; flex-direction: column; align-items: center; gap: 8px"><span style="width: {v}px; height: {v}px; background: {D["control"]}"></span>'
        f'<span class="small num">{v}</span></div>' for v in TOK["space"])
    layers = [("transition", 32767), ("consent", 32766), ("veil", 30000), ("overlay", 10), ("modal", 5), ("hud", 1), ("base", 0)]
    layer_rows = "".join(
        f'<div class="sls" style="display: flex; justify-content: space-between; padding: 10px 16px; background: {D["surface"]}">'
        f'<span class="lab" style="color: {D["text"]}">{n}</span><span class="small num">{o}</span></div>' for n, o in layers)
    devices = [("Phone", 390, 844, 0.4), ("iPad", 820, 1180, 0.28), ("PC 16:9", 1920, 1080, 0.15)]
    dev = "".join(
        f'<div style="display: flex; flex-direction: column; align-items: center; gap: 10px">'
        f'<div style="width: {round(w * k)}px; height: {round(h * k)}px; background: {D["surface"]}; box-shadow: inset 0 0 0 2px {D["line"]}; position: relative">'
        f'<div style="position: absolute; inset: {max(6, round(24 * k * 2))}px; box-shadow: inset 0 0 0 1px {D["cta"]}"></div></div>'
        f'<span class="small">{n} {w}x{h}</span></div>' for n, w, h, k in devices)
    return board("Shape, space, layers and the safe area", header(
        "4 · Shape and space",
        "The corner sliver, an 8 grid, and the safe area",
        "Large surfaces cut 14, buttons and chips 10. Lines are 1, emphasis 2. Spacing comes only from the scale below. "
        "Backgrounds bleed to the edge; content stays inside the safe area, at least 24 in.")
        + f'<div style="display: grid; grid-template-columns: 520px 460px minmax(0, 1fr); gap: 36px">'
          f'<div style="display: flex; flex-direction: column; gap: 18px"><span class="lab">The sliver</span>'
          f'<div class="sl panel" style="height: 150px; display: flex; align-items: center; justify-content: center"><span class="small">Panel · cut 14</span></div>'
          f'<div style="display: flex; gap: 14px"><span class="sls" style="padding: 14px 22px; background: {D["text"]}; color: {D["ground"]}; font-family: \'Chakra Petch\'; font-weight: 600">BUTTON · CUT 10</span>'
          f'<span class="sls" style="padding: 14px 22px; box-shadow: inset 0 0 0 1px {D["line"]}; color: {D["text"]}">Chip</span></div>'
          f'<p class="small"><span class="dont">Never</span> a rounded corner, a pill, a circle frame.</p></div>'
          f'<div style="display: flex; flex-direction: column; gap: 10px"><span class="lab">Layers (sort order)</span>{layer_rows}'
          f'<p class="small">Hide with CanvasGroup alpha, never SetActive.</p></div>'
          f'<div style="display: flex; flex-direction: column; gap: 14px"><span class="lab">Safe area: test every screen at these</span>'
          f'<div style="display: flex; gap: 28px; align-items: flex-end">{dev}</div>'
          f'<p class="small">The green line is the 24-unit floor inside Screen.safeArea (SafeAreaFitter).</p></div></div>'
        + f'<div style="display: flex; flex-direction: column; gap: 12px"><span class="lab">Spacing scale</span><div style="display: flex; gap: 26px; align-items: flex-end">{spaces}</div></div>')


def button(label: str, fill: str, text: str, state: str) -> str:
    extra = ""
    if state == "hover":
        extra = f"background-image: linear-gradient(100deg, transparent 30%, rgba(255,255,255,.28) 50%, transparent 70%);"
    if state == "pressed":
        extra = "transform: translateY(2px); filter: brightness(.85);"
    if state == "disabled":
        fill, text = D["text-off"], "#25262D"
    if state == "focus":  # the sliver clips an outline, so the ring is a selection-blue sliver behind the button
        return (f'<span class="sls" style="display: inline-flex; padding: 4px; margin: -4px; background: {D["selection"]}">'
                f'<button class="btn sls" type="button" style="background-color: {fill}; color: {text}; width: 210px; '
                f'box-shadow: 0 0 0 3px {D["ground"]}">{e(label)}</button></span>')
    return (f'<button class="btn sls" type="button" style="background-color: {fill}; color: {text}; width: 210px; {extra}"'
            f'{" disabled" if state == "disabled" else ""}>{e(label)}</button>')


def sg_buttons() -> str:
    variants = [("Play / Ready", D["commit-fill"], D["on-commit"], "No way back: launch a game, Ready"),
                ("CTA", D["cta-fill"], D["on-fill"], "Join, accept, claim"),
                ("Purchase", D["purchase-fill"], D["on-fill"], "Any spend: icon + amount"),
                ("Default", D["text"], D["on-fill"], "A standard action"),
                ("Cancel", D["text-soft"], D["on-fill"], "Back out, not now"),
                ("Danger", D["danger"], D["on-fill"], "Delete, leave, can't launch")]
    states = ["rest", "hover", "focus", "pressed", "disabled"]
    head = "".join(f'<span class="lab" style="width: 210px">{s}</span>' for s in states)
    rows = "".join(
        f'<div style="display: flex; align-items: center; gap: 24px"><div style="width: 260px; display: flex; flex-direction: column; gap: 4px">'
        f'<span class="h3">{e(n)}</span><span class="small">{e(why)}</span></div>'
        + "".join(button({"CTA": "JOIN"}.get(n, n.split(" ")[0].upper()), f, t, s) for s in states) + "</div>" for n, f, t, why in variants)
    return board("Buttons and their states", header(
        "5 · Buttons",
        "Six buttons, five states, nothing else",
        "Hover is the kit's sheen, one band across the fill (Rev 35 C, built in #1619). Focus is the Jade ring, the same for "
        "pad, keyboard and touch. Pressed sinks 2. Disabled is grey and says why nearby. Play and Ready are Ruby: once pressed "
        "there is no way back. Lime is the call to action you can still change: join, accept, claim.")
        + f'<div style="display: flex; flex-direction: column; gap: 20px"><div style="display: flex; gap: 24px; padding-left: 284px">{head}</div>{rows}</div>'
        + '<p class="src">Kit prefabs: Assets/_Prefabs/UI Elements (KitButton, KitButtonSmall), drawn by '
          'Tools/Build/author_ui_kit_prefabs.py. On a popup and on a line buttons are opaque; elsewhere transparent with the same border.</p>')


def sg_windows() -> str:
    states = [("Free", D["cta"], "Claim"), ("Buy", D["purchase"], "40"), ("Locked", D["text-off"], "Clout 4"), ("Owned", D["text-soft"], "Owned")]
    cards = "".join(
        f'<div class="sl" style="width: 168px; height: 260px; background: {D["surface"]}; box-shadow: inset 0 0 0 {"2px " + c if n in ("Free", "Buy") else "1px " + D["line"]}; '
        f'display: flex; flex-direction: column; justify-content: flex-end; gap: 10px; padding: 16px; box-sizing: border-box">'
        f'<span class="lab" style="color: {D["text"]}">{n}</span><span class="sls lab" style="padding: 8px 12px; background: {c}; color: {D["on-fill"]}; align-self: flex-start">{e(b)}</span></div>'
        for n, c, b in states)
    modal = (f'<div style="position: relative; width: 560px; padding-bottom: 28px"><div class="sl" style="background: {D["ground-deep"]}; box-shadow: inset 0 0 0 1px {D["text"]}; '
             f'padding: 32px 36px 56px; display: flex; flex-direction: column; gap: 14px"><span class="lab">Confirm</span><h2 class="h2">Upgrade the Sparrow?</h2>'
             f'<p class="body">Spend 40 crystals to raise Charge to level 2.</p></div>'
             f'<div style="position: absolute; left: 36px; bottom: 0; display: flex; gap: 14px">'
             f'<button class="btn sls" type="button" style="background: {D["purchase-fill"]}; color: {D["on-fill"]}">Spend 40</button>'
             f'<button class="btn sls" type="button" style="background: {D["text-soft"]}; color: {D["on-fill"]}">Cancel</button></div></div>')
    toasts = "".join(
        f'<div class="sls" style="width: 360px; padding: 14px 18px; background: {D["surface"]}; box-shadow: inset 3px 0 0 {c}; display: flex; flex-direction: column; gap: 4px">'
        f'<span class="lab" style="color: {D["text"]}">{e(t)}</span><span class="small">{e(b)}</span></div>'
        for t, b, c in [("Kestrel is online", "Invite her to your squad", D["cta"]), ("Quest done", "Claim 120 knowledge", D["cta"]),
                        ("Connection lost", "Reconnecting...", D["danger"])])
    return board("Cards, windows and toasts", header(
        "6 · Cards, windows, toasts",
        "Cards say their state, windows straddle, toasts stay small",
        "A card shows one of four states. A window's buttons straddle its bottom edge, the spend or confirm first. "
        "Toasts stack top right, one line and a short second line, a 3-unit family strip, gone in a few seconds.")
        + f'<div style="display: grid; grid-template-columns: auto auto minmax(0, 1fr); gap: 48px; align-items: start">'
          f'<div style="display: flex; flex-direction: column; gap: 14px"><span class="lab">Card states</span><div style="display: flex; gap: 16px">{cards}</div></div>'
          f'<div style="display: flex; flex-direction: column; gap: 14px"><span class="lab">Window (popup)</span>{modal}</div>'
          f'<div style="display: flex; flex-direction: column; gap: 12px"><span class="lab">Toasts</span>{toasts}</div></div>'
        + '<p class="src">From the shipped screens: Arcade and Toy Box cards (author_menu_arcade.py, author_menu_toybox.py), '
          'PurchaseConfirm and Toasts boards (Frogma, Menu tab, Items 10 + 11), ToastNotificationManager.</p>')


def sg_motion() -> str:
    toks = [("micro", 120, "Hover, tint, focus"), ("std", 200, "Press, toggle, tab change"), ("panel", 320, "A window, a screen slide, a toast"),
            ("ceremony", 500, "Quest claim and end-of-match reveal only")]
    bars = "".join(
        f'<div style="display: grid; grid-template-columns: 160px 90px 1fr; align-items: center; gap: 20px">'
        f'<span class="lab" style="color: {D["text"]}">{n}</span><span class="small num">{ms} ms</span>'
        f'<div style="display: flex; align-items: center; gap: 16px"><span style="height: 14px; width: {ms * 1.6}px; background: {D["control"]}"></span>'
        f'<span class="small">{e(u)}</span></div></div>' for n, ms, u in toks)
    moves = ["The sea lines behind a window, one loop in 45 s or slower", "A game card's picture drift, at most 5 x 3 units",
             "A spinner while something loads or connects", "The hint that teaches the next control", "Window entrances and the START sweep"]
    still = ["Tab underlines", "Panel backgrounds, lattices, halos", "The presence orb and party bonds", "Avatars, art, the settings gear"]
    lst = lambda xs, cls: "".join(f'<p class="small"><span class="{cls}">{"Moves" if cls == "do" else "Still"}</span> {e(x)}</p>' for x in xs)  # noqa: E731
    return board("Motion", header(
        "7 · Motion",
        "Short, useful, and rationed",
        "Anything the player triggers answers in under 200 ms. Ceremony is kept for two moments. Lists stagger 40 ms an "
        "item, eight at most. Nothing moves for its own sake while someone is reading or playing.")
        + f'<div style="display: flex; flex-direction: column; gap: 18px">{bars}</div>'
        + f'<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 36px">'
          f'<div class="sl panel" style="padding: 22px 26px; display: flex; flex-direction: column; gap: 10px"><span class="lab">Allowed to move</span>{lst(moves, "do")}</div>'
          f'<div class="sl panel" style="padding: 22px 26px; display: flex; flex-direction: column; gap: 10px"><span class="lab">Always still</span>{lst(still, "dont")}</div></div>'
        + '<p class="src">Docs/UI_COLOR_RULES.md "Attention economy"; author_ui_lattice.py fails a sea faster than 40 s a loop.</p>')


def sg_dodont() -> str:
    pairs = [
        ("Element colours on the HUD", "Rev 3 HUDs: each element a colour, drifting like a lava lamp (R3HudSquirrel)",
         "Today's lockup: one neutral, the element told by its glyph (AbilityLockupStyle elementColour off)"),
        ("A placeholder backdrop", "Rev 34: the options floated over an empty sea with a THE CELL label; rejected outright",
         "Rev 36: the same captain drawn over the real Home screen"),
        ("Gold for a highlight", "Gold on a first-place row or a featured card",
         "Gold only where crystals are spent: the Purchase button, the upgrade price"),
        ("Glow as decoration", "A pulsing ring round a card nobody needs to press",
         "The one glow: a free Daily Deal (lime) or a buyable one (gold)"),
        ("Lime on Play or Ready", "PLAY or READY drawn as a lime call to action, as if you could change your mind",
         "PLAY and READY in Ruby: once pressed there is no way back"),
        ("A hull pick in sky blue", "The picked hull in an arena lineup marked in the old sky-blue selection",
         "The picked hull in Jade: you can claim it and pick again"),
        ("A string in code", "PrivacyConsentOverlay builds its colours and words in C#",
         "Every word in a serialized field, filled from a template (TemplateText.Fill)"),
    ]
    rows = "".join(
        f'<div class="sl panel" style="padding: 18px 22px; display: grid; grid-template-columns: 300px minmax(0, 1fr) minmax(0, 1fr); gap: 24px; align-items: center">'
        f'<span class="h3">{e(t)}</span><p class="small"><span class="dont">Don\'t</span> {e(a)}</p><p class="small"><span class="do">Do</span> {e(b)}</p></div>'
        for t, a, b in pairs)
    return board("Do and don't, from our own screens", header(
        "8 · Do and don't",
        "Every rule has been broken once already",
        "Each pair comes from a board on Frogma or a shipped screen. The don'ts are the reason the rule exists.")
        + f'<div style="display: flex; flex-direction: column; gap: 14px">{rows}</div>')


def sg_checklist() -> str:
    checks = ["Colours come from theme tokens (UIThemeSO or froglet.css), none typed in by hand",
              "Each colour means what its family means (the colour chart), elements carry none",
              "Tested at phone (390x844), iPad (820x1180) and PC (16:9, 21:9), inside the safe area",
              "Every control reachable with a gamepad and by touch; focus shows the Jade ring",
              "No scene-level overrides on a shared prefab (GameCanvas, kit prefabs): changed on the prefab or a variant",
              "Every word a player reads is an authored field",
              "Matches its picked Frogma board, or says why not"]
    lst = "".join(f'<div style="display: flex; gap: 14px; align-items: flex-start"><span class="sls" style="width: 22px; height: 22px; flex: none; box-shadow: inset 0 0 0 2px {D["cta"]}"></span>'
                  f'<p class="body">{e(c)}</p></div>' for c in checks)
    top = [("Settings window", "OptionsMenuContent: 43 typed-in colours, no theme"), ("Connecting panel", "39 typed-in colours, the pre-kit look"),
           ("In-game HUD (GameCanvas)", "34 typed-in colours, scene overrides in 4 scenes"), ("Privacy and age gate", "colours and words built in code"),
           ("Hangar", "4 canvases without a SafeAreaFitter")]
    tl = "".join(f'<div style="display: grid; grid-template-columns: 40px 1fr; gap: 12px"><span class="num" style="font-size: 28px; color: {D["danger"]}">{i + 1}</span>'
                 f'<div style="display: flex; flex-direction: column; gap: 2px"><span class="h3">{e(n)}</span><span class="small">{e(w)}</span></div></div>'
                 for i, (n, w) in enumerate(top))
    return board("The UI checklist and the five screens to fix", header(
        "9 · Review",
        "The UI checklist every UI pull request answers",
        "A pull request that touches Assets/_Prefabs/UI*, Menu_Main or GameCanvas fills this in (the PR template's UI "
        "section); the UI checklist workflow fails while a box is unanswered, and Shombith reviews as code owner.")
        + f'<div style="display: grid; grid-template-columns: minmax(0, 1.3fr) minmax(0, 1fr); gap: 56px">'
          f'<div style="display: flex; flex-direction: column; gap: 14px">{lst}</div>'
          f'<div class="sl panel" style="padding: 24px 28px; display: flex; flex-direction: column; gap: 16px"><span class="lab">The five screens that break it most today</span>{tl}'
          f'<p class="small">Measured 2026-11-03; fixes in Docs/UI_STYLE_GUIDE.md section 12.</p></div></div>')


def sg_chart() -> str:
    wheel = json.load(open(os.path.join(REPO, "Docs", "UI", "colour-wheel.json"), encoding="utf-8"))
    pal = TOK["palettes"]
    iw, ih = 1000, 500  # the wheel image drawn at this size (two 500 discs)
    names = {"fire": "Fire", "gold": "Gold", "lime": "Lime", "jade": "Jade", "blue": "Blue", "ruby": "Ruby"}
    labels = "".join(
        f'<span class="lab" style="position: absolute; left: {round(x * iw)}px; top: {round(y * ih)}px; transform: translate(-50%, -50%); '
        f'color: #00041F; background: rgba(230,233,255,.86); padding: 4px 8px">{names[f]}</span>'
        for f, pts in wheel["labels"].items() for x, y in pts)
    rows = []
    for f in ("lime", "fire", "gold", "jade", "ruby", "blue"):
        chips = "".join(
            f'<span title="{sh["name"]} {sh["hex"]}" style="display: flex; flex-direction: column; gap: 4px; align-items: center">'
            f'<span class="sls" style="width: 52px; height: 40px; background: {sh["hex"]}"></span>'
            f'<span class="small num" style="font-size: 11px">{sh["hex"][1:]}</span></span>'
            for sh in pal[f])
        rows.append(f'<div style="display: grid; grid-template-columns: 90px minmax(0, 1fr); align-items: center; gap: 14px">'
                    f'<span class="lab" style="color: {D["text"]}">{names[f]}</span><div style="display: flex; gap: 8px; flex-wrap: wrap">{chips}</div></div>')
    return board("The colour chart and the shades to choose from", header(
        "1b · The colour chart",
        "Garrett's chart, and every shade you may choose",
        "Hue runs clockwise from red at the top, saturation grows outward; left at full value, right at 55%. Hatched is the "
        "forbidden band, where two meanings would look alike: never pick a colour there. On the right, the shades inside "
        "each family, clear of the band (--fs-<family>-<lightness> in froglet.css).")
        + f'<div style="display: grid; grid-template-columns: {iw}px minmax(0, 1fr); gap: 40px; align-items: start">'
          f'<div style="position: relative; width: {iw}px; height: {ih}px"><img src="/_blob/ebcbacf2eb55f7cfa395c7d3686efaaf" '
          f'alt="Garrett\'s colour chart: two HSV discs split into the six families, the forbidden band hatched" style="width: {iw}px; height: {ih}px; display: block">{labels}</div>'
          f'<div style="display: flex; flex-direction: column; gap: 16px">{"".join(rows)}</div></div>'
        + '<p class="src">Drawn from Docs/VesselModels/colour_domains.json by Tools/Build/froglet_style.py (Docs/UI/colour-wheel.png), '
          'the same rule the Colour Domains lab shows (https://claude.ai/artifact/QT7nMWURMQgznfAnAg2FXh). '
          'python3 Tools/Build/colour_domains.py classify \'#hex\' says where any colour falls.</p>')


BOARDS = [
    ("SGReadMe.dc.html", "Style Guide - start here: one look for everything Froglet makes", sg_readme),
    ("SGColour.dc.html", "Style Guide 1 - Colour: the six families (Garrett's colour chart)", sg_colour),
    ("SGChart.dc.html", "Style Guide 1b - The colour chart: the wheel, the forbidden band, the shades to choose from", sg_chart),
    ("SGThemes.dc.html", "Style Guide 2 - Themes: Deep Sea and Shore, domains as one accent", sg_themes),
    ("SGType.dc.html", "Style Guide 3 - Type and numbers", sg_type),
    ("SGShape.dc.html", "Style Guide 4 - Shape, space, layers and the safe area", sg_shape),
    ("SGButtons.dc.html", "Style Guide 5 - Buttons and their states", sg_buttons),
    ("SGWindows.dc.html", "Style Guide 6 - Cards, windows and toasts", sg_windows),
    ("SGMotion.dc.html", "Style Guide 7 - Motion", sg_motion),
    ("SGDoDont.dc.html", "Style Guide 8 - Do and don't, from our own screens", sg_dodont),
    ("SGChecklist.dc.html", "Style Guide 9 - The UI checklist and the five screens to fix", sg_checklist),
]


def update(live: dict, original: dict, root_dir: str) -> int:
    """The tab exists: rewrite every board, add new ones, lay them out again three to a row, and refit the section,
    keeping any extent a person gave the frame by hand (the new frame is the union of theirs and the fitted one)."""
    root = os.path.join(root_dir, "project")
    os.makedirs(root, exist_ok=True)
    top = live["notes"][TITLE_NOTE]["y"] + frogma.TITLE_ABOVE
    names = []
    for i, (file, title, fn) in enumerate(BOARDS):
        col, row = i % 3, i // 3
        entry = live["boards"].get(file, {})
        entry.update({"x": col * (W + frogma.GAP_X), "y": top + row * (H + frogma.GAP_Y), "w": W, "h": H, "page": TAB, "title": title})
        live["boards"][file] = entry
        if file not in live["order"]:
            live["order"].append(file)
        names.append(file)
        with open(os.path.join(root, file), "w", encoding="utf-8") as fh:
            fh.write(fn())
    key = f"sec-{TITLE_NOTE}"
    fs = frogma.frames(live)
    old = fs[key]["box"] if key in fs else None
    content = frogma.bbox(live, names, [TITLE_NOTE])
    box = frogma.frame_box(content, frogma.SECTION_PAD)
    if old:
        x0, y0 = min(box[0], old[0]), min(box[1], old[1])
        box = (x0, y0, max(box[0] + box[2], old[0] + old[2]) - x0, max(box[1] + box[3], old[1] + old[3]) - y0)
    tiles = frogma.put_frame(live, key, "section", box, "teal", TAB)
    live["launch"] = {"view": "canvas", "page": TAB}
    touched = set(tiles) | {k for k in original["notes"] if frogma.frame_key(k) == key}
    bad = [k for k in frogma.changed_outside(original, live, set(names), touched) if k != "order"]
    if bad:
        print(f"changed keys that are not ours {bad[:5]}", file=sys.stderr)
        return 1
    frogma.write_index(__import__("pathlib").Path(root_dir), live)
    print(f"Style Guide updated: {len(names)} boards, section {box}. Root: {root_dir}")
    print("files: " + json.dumps({f"project/{n}": f"project/{n}" for n in names}))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--live", required=True)
    ap.add_argument("--root", required=True)
    a = ap.parse_args()
    live = frogma.load_index(a.live)
    original = json.loads(json.dumps(live))
    pages = live.setdefault("pages", [])
    if any(p["id"] == TAB for p in pages):
        return update(live, original, a.root)
    if any(not b.get("page") for b in live["boards"].values()) or any(not n.get("page") for n in live["notes"].values()):
        print("a board or note relies on the first page; give it its page before inserting a new first tab", file=sys.stderr)
        return 1
    tab, why = frogma.add_tab(live, TAB_NAME, TAB, at=0)
    if tab != TAB:
        print(f"could not add the tab: {why or tab}", file=sys.stderr)
        return 1
    root = os.path.join(a.root, "project")
    os.makedirs(root, exist_ok=True)
    top = frogma.TITLE_ABOVE
    live["notes"][TITLE_NOTE] = {"kind": "title1", "text": "Frogma style guide - the rules every screen, board, deck and page follows",
                                 "x": 0, "y": 0, "w": 240, "maxW": 3 * W + 2 * frogma.GAP_X, "page": TAB}
    names = []
    for i, (file, title, fn) in enumerate(BOARDS):
        col, row = i % 3, i // 3
        live["boards"][file] = {"x": col * (W + frogma.GAP_X), "y": top + row * (H + frogma.GAP_Y), "w": W, "h": H, "page": TAB, "title": title}
        live["order"].append(file)
        names.append(file)
        with open(os.path.join(root, file), "w", encoding="utf-8") as fh:
            fh.write(fn())
    w, why = frogma.make_frame(live, "section", f"sec-{TITLE_NOTE}", None, names, TITLE_NOTE, "teal")
    if why:
        print(f"section: {why}", file=sys.stderr)
        return 1
    live["launch"] = {"view": "canvas", "page": TAB}
    bad = [k for k in frogma.changed_outside(original, live, set(names), set(w) | {TITLE_NOTE}, {"pages"}) if k != "order"]
    if sorted(set(live["order"]) - set(original["order"])) != sorted(names):
        bad.append("order gained more than the new boards")
    if bad:
        print(f"changed keys that are not ours {bad[:5]}", file=sys.stderr)
        return 1
    frogma.write_index(__import__("pathlib").Path(a.root), live)
    print(f"Style Guide tab first, {len(names)} boards, section sec-{TITLE_NOTE}; launch opens on it. Root: {a.root}")
    print("files: " + json.dumps({f"project/{n}": f"project/{n}" for n in names}))
    return 0


if __name__ == "__main__":
    sys.exit(main())

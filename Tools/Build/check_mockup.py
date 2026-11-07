#!/usr/bin/env python3
"""check_mockup.py - the mockup gate: a Frogma board (or any mockup HTML) is measured against the Froglet style guide
BEFORE the owner sees it (Docs/SKILL_TRAINING_PLAN.md section 3.4, round R1; the rules: Docs/UI_STYLE_GUIDE.md; the
tokens: Docs/UI/froglet.css and froglet-tokens.json, written from the game by Tools/Build/froglet_style.py).

Six gates, one line each (ok / FAIL / warn / skip, then the reason):
  colours   every colour literal in inline styles, <style> blocks and SVG fill / stroke / stop-color attributes is a
            token colour (compared as normalised RGB; white, black, transparent and an alpha variant of a token pass).
            Each typed colour is named with its count and the nearest token.
  type      every font-size in px is on the type scale the guide names (froglet-tokens.json `type`: 48 36 28 22 18 15
            14 44 20; a board that declares a phone may also use STYLE_FOUNDATION section 4's mobile column 24 20 16
            12). Skipped, and said so, when the tokens name no scale.
  contrast  every text element with a colour and a resolvable background (its own, or the nearest ancestor's;
            rgba composited over what is under it; a gradient is checked at every stop) reaches WCAG 4.5:1, or 3.0:1
            at 24 px and larger. Text over a gradient fails only when every stop fails (some stops: warn).
  phone     a board that declares a phone (an 800x450 or 960x540 frame, or "phone" / "portrait" in its title) must
            have such a frame and a safe-area marker: an element whose class, id, aria-label or data-* attribute names
            "safe", or an element inset from its frame with a dashed outline / border or an inset box-shadow (the way
            the style guide's SGShape board draws the 24-unit floor). No marker is reported as not checkable, not as
            a fail. A board that declares no phone skips.
  cta       at most one element coloured with the Lime family (cta, cta-fill, lime-*) per screen frame; nested Lime
            elements (a button, its fill and its glyph) count as one; a Lime-coloured text is counted, as the guide
            says Lime is never a heading, an eyebrow or decoration.
  noise     distinct visual elements (an element with text, a background, a border, an outline, a shadow, or a
            picture: img / svg / video) per screen frame against a budget: --budget N, else 1.3 x the median of the
            boards named by --baseline (each board's busiest frame), else 60.

Screen frames: the board's root element (the first element inside <x-dc>, or the first element in <body>) and every
container inside it whose inline size is a known screen (1920x1080, 800x450, 960x540, 390x844, 844x390, 820x1180,
1180x820, 1080x1920) and that is not inside another such container: a steps board (six screens stacked under radio
tabs) or a compare board (nine screens scaled down) is then counted screen by screen. What is hidden at rest is left
out of cta and noise: inline display:none / visibility:hidden, the `hidden` attribute, and an <sc-if> whose
hint-placeholder-val is false; a class rule that hides (a step, a tab) is a state and stays in. An <sc-for> with a
hint-placeholder-count multiplies what it repeats.

    python3 Tools/Build/check_mockup.py <board.dc.html> [more ...]
        [--budget N | --baseline <picked.dc.html> ...] [--legacy] [--board-date YYYY-MM-DD] [--json]
        [--doc Docs/MENU_REDESIGN_B.md] [--tokens Docs/UI] [--no-svg-attrs]
    python3 Tools/Build/check_mockup.py --self-test

--legacy: a board drawn before the tokens existed (2026-11-03) reports typed colours as a warn, not a FAIL. The date
is read from the round's section of Docs/MENU_REDESIGN_B.md ("## N. Rev 29 mockups: ... (2026-10-27, ...)"), the
round from the file name (R29SquadsC.dc.html -> Rev 29); --board-date gives it for a board the doc does not date.
It never weakens the other five gates.

Exit 0 = no gate FAILed (warn and skip are not fails), 1 = a gate FAILed (each named), 2 = it could not run.
A READER: it writes nothing but the temp fixtures of --self-test. Stdlib only.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import html as _html
import json
import os
import re
import statistics
import sys
import tempfile
from html.parser import HTMLParser

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
TOKENS_DIR = os.path.join(REPO, "Docs", "UI")
DOC = os.path.join(REPO, "Docs", "MENU_REDESIGN_B.md")
TOKENS_DATE = _dt.date(2026, 11, 3)  # the day froglet.css / froglet-tokens.json were first written (UI_STYLE_GUIDE.md)
PHONE_SIZES = {(800, 450), (960, 540)}
SCREEN_SIZES = PHONE_SIZES | {(1920, 1080), (390, 844), (844, 390), (820, 1180), (1180, 820), (1080, 1920)}
MOBILE_SCALE = (24, 20, 16, 12)  # STYLE_FOUNDATION.md section 4, the Mobile @800 column (H1, H2, H3 / Body / Button, Button small)
DEFAULT_BUDGET = 60
BUDGET_FACTOR = 1.3
VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"}
SKIP_TAGS = {"script", "style", "title", "helmet", "template", "head", "noscript"}
PICTURE_TAGS = {"img", "svg", "video", "canvas", "picture"}
GATES = ("colours", "type", "contrast", "phone", "cta", "noise")
LIME_TOKENS = ("cta", "cta-fill")

# ----------------------------------------------------------------------------------------------------------- colours

HEX_RE = r"#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})(?![0-9a-zA-Z_-])"
FUNC_RE = r"\b(?:rgba?|hsla?)\(\s*[^()]*?\)"
NAMED = {"white": (255, 255, 255, 1.0), "black": (0, 0, 0, 1.0), "transparent": (0, 0, 0, 0.0)}
TYPED_NAMES = ("red", "green", "blue", "yellow", "orange", "purple", "pink", "cyan", "magenta", "lime", "navy",
               "teal", "gray", "grey", "silver", "gold", "maroon", "olive", "aqua", "fuchsia", "brown", "beige",
               "ivory", "coral", "salmon", "tomato", "violet", "indigo", "turquoise", "crimson", "khaki", "tan")
COLOUR_RE = re.compile(HEX_RE + "|" + FUNC_RE + r"|(?<![\w-])(?:" + "|".join(list(NAMED) + list(TYPED_NAMES)) + r")(?![\w-])",
                       re.I)


def _hsl_to_rgb(h: float, s: float, l: float) -> tuple[int, int, int]:
    h = (h % 360) / 360.0
    if s == 0:
        v = round(l * 255)
        return v, v, v
    q = l * (1 + s) if l < 0.5 else l + s - l * s
    p = 2 * l - q

    def hue(t: float) -> float:
        t %= 1.0
        if t < 1 / 6:
            return p + (q - p) * 6 * t
        if t < 1 / 2:
            return q
        if t < 2 / 3:
            return p + (q - p) * (2 / 3 - t) * 6
        return p

    return round(hue(h + 1 / 3) * 255), round(hue(h) * 255), round(hue(h - 1 / 3) * 255)


def parse_colour(literal: str) -> tuple[int, int, int, float] | None:
    """A CSS colour literal -> (r, g, b, alpha), or None when it is not one."""
    s = literal.strip()
    low = s.lower()
    if low in NAMED:
        return NAMED[low]
    if low in TYPED_NAMES:
        return _named_typed(low)
    if s.startswith("#"):
        h = s[1:]
        if len(h) in (3, 4):
            h = "".join(c * 2 for c in h)
        if len(h) == 6:
            return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 1.0
        if len(h) == 8:
            return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), round(int(h[6:8], 16) / 255, 3)
        return None
    m = re.match(r"(rgba?|hsla?)\(\s*(.*)\)\s*$", s, re.I | re.S)
    if not m:
        return None
    fn = m.group(1).lower()
    body = m.group(2).strip()
    alpha = 1.0
    if "/" in body:
        body, a = body.split("/", 1)
        alpha = _num(a.strip(), 1.0)
    parts = [p for p in re.split(r"[\s,]+", body.strip()) if p]
    if len(parts) == 4:
        alpha = _num(parts[3], 1.0)
        parts = parts[:3]
    if len(parts) != 3:
        return None
    try:
        if fn.startswith("rgb"):
            vals = []
            for p in parts:
                if p.endswith("%"):
                    vals.append(round(float(p[:-1]) * 2.55))
                else:
                    vals.append(int(round(float(p))))
            r, g, b = (max(0, min(255, v)) for v in vals)
        else:
            h = float(re.sub(r"(deg|turn|rad|grad)$", "", parts[0]))
            sat = float(parts[1].rstrip("%")) / 100
            lig = float(parts[2].rstrip("%")) / 100
            r, g, b = _hsl_to_rgb(h, sat, lig)
    except ValueError:
        return None
    return r, g, b, max(0.0, min(1.0, alpha))


def _num(s: str, default: float) -> float:
    try:
        if s.endswith("%"):
            return float(s[:-1]) / 100
        return float(s)
    except ValueError:
        return default


_TYPED_RGB = {"red": (255, 0, 0), "green": (0, 128, 0), "blue": (0, 0, 255), "yellow": (255, 255, 0),
              "orange": (255, 165, 0), "purple": (128, 0, 128), "pink": (255, 192, 203), "cyan": (0, 255, 255),
              "magenta": (255, 0, 255), "lime": (0, 255, 0), "navy": (0, 0, 128), "teal": (0, 128, 128),
              "gray": (128, 128, 128), "grey": (128, 128, 128), "silver": (192, 192, 192), "gold": (255, 215, 0),
              "maroon": (128, 0, 0), "olive": (128, 128, 0), "aqua": (0, 255, 255), "fuchsia": (255, 0, 255),
              "brown": (165, 42, 42), "beige": (245, 245, 220), "ivory": (255, 255, 240), "coral": (255, 127, 80),
              "salmon": (250, 128, 114), "tomato": (255, 99, 71), "violet": (238, 130, 238), "indigo": (75, 0, 130),
              "turquoise": (64, 224, 208), "crimson": (220, 20, 60), "khaki": (240, 230, 140), "tan": (210, 180, 140)}


def _named_typed(name: str) -> tuple[int, int, int, float]:
    r, g, b = _TYPED_RGB[name]
    return r, g, b, 1.0


def find_colours(value: str) -> list[tuple[str, tuple[int, int, int, float]]]:
    """Every colour literal in a CSS value (never a selector), with its parsed rgba."""
    out = []
    for m in COLOUR_RE.finditer(value):
        c = parse_colour(m.group(0))
        if c is not None:
            out.append((m.group(0), c))
    return out


def hexname(rgb: tuple) -> str:
    return "#%02X%02X%02X" % tuple(rgb[:3])


def luminance(rgb: tuple) -> float:
    def lin(c: float) -> float:
        c /= 255.0
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4

    r, g, b = rgb[:3]
    return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)


def contrast(a: tuple, b: tuple) -> float:
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def composite(top: tuple, under: tuple) -> tuple:
    """top (r,g,b,a) over an opaque under (r,g,b[,a]) -> opaque (r,g,b,1.0)."""
    a = top[3]
    return (round(top[0] * a + under[0] * (1 - a)), round(top[1] * a + under[1] * (1 - a)),
            round(top[2] * a + under[2] * (1 - a)), 1.0)


# ------------------------------------------------------------------------------------------------------------ tokens

class Tokens:
    """The token colours (rgb -> names), the Lime family, the type scale."""

    def __init__(self, colours: dict, lime: set, type_scale: list[int] | None, source: str):
        self.colours = colours          # (r,g,b) -> [names]
        self.lime = lime                # (r,g,b) of the Lime family
        self.type_scale = type_scale    # the PC column, or None when the tokens name none
        self.source = source

    def names(self, rgb: tuple) -> list[str]:
        return self.colours.get(tuple(rgb[:3]), [])

    def is_token(self, rgba: tuple) -> bool:
        return tuple(rgba[:3]) in self.colours

    def is_lime(self, rgba: tuple) -> bool:
        return tuple(rgba[:3]) in self.lime

    def nearest(self, rgb: tuple) -> tuple[str, str]:
        best, bname = None, ""
        for trgb, names in self.colours.items():
            d = sum((a - b) ** 2 for a, b in zip(trgb, rgb[:3]))
            if best is None or d < best:
                best, bname = d, names[0]
        return bname, hexname(next(k for k, v in self.colours.items() if v[0] == bname)) if bname else ""


def _rel(path: str) -> str:
    path = os.path.abspath(path)
    return os.path.relpath(path, REPO) if path.startswith(REPO + os.sep) else path


def load_tokens(tokens_dir: str) -> Tokens:
    colours: dict = {}
    lime: set = set()
    type_scale = None
    source = []

    def add(name: str, hexv: str) -> None:
        c = parse_colour(hexv)
        if c is None:
            return
        rgb = tuple(c[:3])
        colours.setdefault(rgb, [])
        if name not in colours[rgb]:
            colours[rgb].append(name)
        base = name.split(".")[-1]
        if base in LIME_TOKENS or base.startswith("lime-"):
            lime.add(rgb)

    jpath = os.path.join(tokens_dir, "froglet-tokens.json")
    if os.path.exists(jpath):
        with open(jpath, encoding="utf-8") as f:
            data = json.load(f)
        for theme, vals in (data.get("themes") or {}).items():
            for name, hexv in vals.items():
                add(f"{theme}.{name}", hexv)
        for fam, shades in (data.get("palettes") or {}).items():
            for sh in shades:
                add(sh.get("name", fam), sh.get("hex", ""))
        if isinstance(data.get("type"), dict) and data["type"]:
            type_scale = sorted({int(v) for v in data["type"].values()})
        source.append(_rel(jpath))
    cpath = os.path.join(tokens_dir, "froglet.css")
    if os.path.exists(cpath):
        with open(cpath, encoding="utf-8") as f:
            css = f.read()
        for m in re.finditer(r"--fs-([\w-]+)\s*:\s*(#[0-9a-fA-F]{3,8})\b", css):
            add(m.group(1), m.group(2))
        if type_scale is None:
            sizes = {int(m.group(1)) for m in re.finditer(r"--fs-type-[\w-]+\s*:\s*(\d+)px", css)}
            if sizes:
                type_scale = sorted(sizes)
        source.append(_rel(cpath))
    if not colours:
        raise SystemExit(f"no tokens under {tokens_dir}: run python3 Tools/Build/froglet_style.py first")
    return Tokens(colours, lime, type_scale, " + ".join(source))


# --------------------------------------------------------------------------------------------------------------- CSS

def strip_comments(css: str) -> str:
    return re.sub(r"/\*.*?\*/", "", css, flags=re.S)


def split_decls(block: str) -> list[tuple[str, str, bool]]:
    """'a: b; c: d' -> [(prop, value, important)], splitting on ; outside parentheses and quotes."""
    out, depth, cur, quote = [], 0, [], None
    for ch in block:
        if quote:
            cur.append(ch)
            if ch == quote:
                quote = None
            continue
        if ch in "\"'":
            quote = ch
            cur.append(ch)
        elif ch == "(":
            depth += 1
            cur.append(ch)
        elif ch == ")":
            depth = max(0, depth - 1)
            cur.append(ch)
        elif ch == ";" and depth == 0:
            out.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
    out.append("".join(cur))
    decls = []
    for d in out:
        if ":" not in d:
            continue
        prop, value = d.split(":", 1)
        prop = prop.strip().lower()
        value = value.strip()
        important = False
        if value.lower().endswith("!important"):
            important = True
            value = value[: -len("!important")].strip()
        if prop and value:
            decls.append((prop, value, important))
    return decls


def parse_css(css: str) -> list[dict]:
    """Rules: {'selector', 'decls', 'kind': 'rule'|'keyframes'|'media', 'order'}. @media rules keep their kind so the
    cascade can leave them out (a reduced-motion or light-scheme branch is not the board at rest)."""
    css = strip_comments(css)
    rules: list[dict] = []

    def walk(text: str, kind: str) -> None:
        i, n = 0, len(text)
        while i < n:
            j = text.find("{", i)
            if j < 0:
                break
            selector = text[i:j].strip()
            depth, k = 1, j + 1
            while k < n and depth:
                if text[k] == "{":
                    depth += 1
                elif text[k] == "}":
                    depth -= 1
                k += 1
            inner = text[j + 1: k - 1]
            i = k
            if not selector:
                continue
            if selector.startswith("@"):
                low = selector.lower()
                if low.startswith("@media") or low.startswith("@supports"):
                    walk(inner, "media")
                elif "keyframes" in low:
                    walk(inner, "keyframes")
                # @font-face, @import, @layer ...: no colours worth gating
                continue
            rules.append({"selector": selector, "decls": split_decls(inner), "kind": kind, "order": len(rules)})

    walk(css, "rule")
    return rules


class Compound:
    __slots__ = ("tag", "classes", "id", "attrs", "root")

    def __init__(self) -> None:
        self.tag: str | None = None
        self.classes: list[str] = []
        self.id: str | None = None
        self.attrs: list[tuple[str, str | None, str | None]] = []
        self.root = False


_COMPOUND_RE = re.compile(r"(\*|[a-zA-Z][\w-]*)|\.([\w-]+)|#([\w-]+)|\[([^\]]+)\]|(::?[\w-]+(?:\([^)]*\))?)")


def parse_selector(sel: str) -> list[tuple[str, Compound]] | None:
    """A selector -> [(combinator, compound)] right-to-left friendly list, or None when a part is unsupported
    (pseudo-classes other than :root / :not(.x), sibling combinators, pseudo-elements)."""
    sel = sel.strip()
    if not sel:
        return None
    parts: list[tuple[str, Compound]] = []
    tokens = re.split(r"(\s*[>+~]\s*|\s+)", sel)
    comb = ""
    for tok in tokens:
        if tok is None or tok == "":
            continue
        t = tok.strip()
        if t in (">", "+", "~"):
            comb = t
            continue
        if tok.isspace():
            comb = " "
            continue
        if comb in ("+", "~"):
            return None
        comp = Compound()
        pos = 0
        while pos < len(t):
            m = _COMPOUND_RE.match(t, pos)
            if not m:
                return None
            pos = m.end()
            if m.group(1):
                comp.tag = None if m.group(1) == "*" else m.group(1).lower()
            elif m.group(2):
                comp.classes.append(m.group(2))
            elif m.group(3):
                comp.id = m.group(3)
            elif m.group(4):
                am = re.match(r"\s*([\w:-]+)\s*(?:([~|^$*]?=)\s*[\"']?([^\"']*)[\"']?)?\s*$", m.group(4))
                if not am:
                    return None
                comp.attrs.append((am.group(1).lower(), am.group(2), am.group(3)))
            elif m.group(5):
                p = m.group(5)
                if p == ":root":
                    comp.root = True
                elif p.startswith(":not(") and p.endswith(")"):
                    inner = p[5:-1].strip()
                    if not re.fullmatch(r"\.[\w-]+", inner):
                        return None
                    comp.attrs.append(("class", "!~=", inner[1:]))
                else:
                    return None
        parts.append((comb, comp))
        comb = ""
    return parts or None


def specificity(parts: list[tuple[str, Compound]]) -> tuple[int, int, int]:
    a = b = c = 0
    for _, comp in parts:
        if comp.id:
            a += 1
        b += len(comp.classes) + len(comp.attrs) + (1 if comp.root else 0)
        if comp.tag:
            c += 1
    return a, b, c


# -------------------------------------------------------------------------------------------------------------- DOM

class Node:
    __slots__ = ("tag", "attrs", "parent", "children", "texts", "classes", "id", "inline", "_decl", "_computed",
                 "hidden", "mult", "frame", "index")

    def __init__(self, tag: str, attrs: list, parent) -> None:
        self.tag = tag
        self.attrs = {k.lower(): (v if v is not None else "") for k, v in attrs}
        self.parent = parent
        self.children: list[Node] = []
        self.texts: list[str] = []
        self.classes = self.attrs.get("class", "").split()
        self.id = self.attrs.get("id") or None
        style = self.attrs.get("style", "")
        self.inline = [] if "{{" in style else split_decls(style)
        self._decl = None
        self._computed = None
        self.hidden = False
        self.mult = 1
        self.frame = None
        self.index = 0

    def text(self) -> str:
        return re.sub(r"\s+", " ", "".join(self.texts)).strip()

    def ancestors(self):
        n = self.parent
        while n is not None:
            yield n
            n = n.parent

    def walk(self):
        yield self
        for c in self.children:
            yield from c.walk()

    def inside(self, tags: set) -> bool:
        return any(a.tag in tags for a in self.ancestors())

    def describe(self) -> str:
        bits = self.tag
        if self.classes:
            bits += "." + ".".join(self.classes[:2])
        t = self.text()
        label = self.attrs.get("aria-label")
        if t:
            bits += f' "{t[:28]}"'
        elif label:
            bits += f' [{label[:28]}]'
        return bits


class TreeBuilder(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.root = Node("#document", [], None)
        self.stack = [self.root]
        self.styles: list[str] = []
        self.title = ""
        self._mode: str | None = None
        self._buf: list[str] = []

    def handle_starttag(self, tag, attrs):
        tag = tag.lower()
        node = Node(tag, attrs, self.stack[-1])
        self.stack[-1].children.append(node)
        if tag in ("style", "title", "script"):
            self._mode, self._buf = tag, []
        if tag not in VOID:
            self.stack.append(node)

    def handle_startendtag(self, tag, attrs):
        node = Node(tag.lower(), attrs, self.stack[-1])
        self.stack[-1].children.append(node)

    def handle_endtag(self, tag):
        tag = tag.lower()
        if tag == self._mode:
            text = "".join(self._buf)
            if tag == "style":
                self.styles.append(text)
            elif tag == "title" and not self.title:
                self.title = re.sub(r"\s+", " ", text).strip()
            self._mode = None
        for i in range(len(self.stack) - 1, 0, -1):
            if self.stack[i].tag == tag:
                del self.stack[i:]
                break

    def handle_data(self, data):
        if self._mode:
            self._buf.append(data)
        else:
            self.stack[-1].texts.append(data)


def parse_html(src: str) -> TreeBuilder:
    tb = TreeBuilder()
    tb.feed(src)
    tb.close()
    return tb


# ------------------------------------------------------------------------------------------------------- the cascade

INHERITED = ("color", "font-size", "font-weight", "font-family", "visibility")


class Cascade:
    def __init__(self, rules: list[dict], doc_root: Node) -> None:
        self.rules = []
        for r in rules:
            if r["kind"] != "rule":
                continue
            for sel in r["selector"].split(","):
                parts = parse_selector(sel)
                if parts is None:
                    continue
                self.rules.append((specificity(parts), r["order"], parts, r["decls"]))
        self.doc_root = doc_root
        self.html = next((c for c in doc_root.children if c.tag == "html"), doc_root)

    def matches_compound(self, node: Node, comp: Compound) -> bool:
        if comp.root and node is not self.html:
            return False
        if comp.tag and node.tag != comp.tag:
            return False
        if comp.id and node.id != comp.id:
            return False
        for c in comp.classes:
            if c not in node.classes:
                return False
        for name, op, val in comp.attrs:
            if op == "!~=":
                if val in node.classes:
                    return False
                continue
            if name not in node.attrs:
                return False
            if op is None:
                continue
            have = node.attrs[name]
            if op == "=" and have != val:
                return False
            if op == "~=" and val not in have.split():
                return False
            if op == "^=" and not have.startswith(val or ""):
                return False
            if op == "$=" and not have.endswith(val or ""):
                return False
            if op == "*=" and (val or "") not in have:
                return False
            if op == "|=" and not (have == val or have.startswith((val or "") + "-")):
                return False
        return True

    def matches(self, node: Node, parts: list[tuple[str, Compound]]) -> bool:
        idx = len(parts) - 1
        comb, comp = parts[idx]
        if not self.matches_compound(node, comp):
            return False
        return self._match_up(node, parts, idx)

    def _match_up(self, node: Node, parts, idx: int) -> bool:
        if idx == 0:
            return True
        comb = parts[idx][0]
        prev = parts[idx - 1][1]
        if comb == ">":
            p = node.parent
            return p is not None and self.matches_compound(p, prev) and self._match_up(p, parts, idx - 1)
        p = node.parent
        while p is not None:
            if self.matches_compound(p, prev) and self._match_up(p, parts, idx - 1):
                return True
            p = p.parent
        return False

    def declared(self, node: Node) -> dict:
        """prop -> raw value after the cascade (rules by specificity and order, then inline, !important on top)."""
        if node._decl is not None:
            return node._decl
        applied: list[tuple[tuple, int, int, str, str]] = []  # (specificity, order, important, prop, value)
        for spec, order, parts, decls in self.rules:
            if self.matches(node, parts):
                for prop, value, imp in decls:
                    applied.append((spec, order, 1 if imp else 0, prop, value))
        for prop, value, imp in node.inline:
            applied.append(((9, 9, 9), 10 ** 6, 1 if imp else 0, prop, value))
        applied.sort(key=lambda t: (t[2], t[0], t[1]))
        out: dict = {}
        for _, _, _, prop, value in applied:
            out[prop] = value
        node._decl = out
        return out

    def custom(self, node: Node, name: str) -> str | None:
        n: Node | None = node
        while n is not None:
            v = self.declared(n).get(name) if n.tag != "#document" else None
            if v is not None:
                return v
            n = n.parent
        return None

    def resolve(self, value: str, node: Node) -> str:
        """var(--x[, fallback]) replaced from the nearest declaration; unresolved -> the fallback or ''."""
        for _ in range(6):
            m = re.search(r"var\(\s*(--[\w-]+)\s*(?:,\s*([^()]*(?:\([^()]*\)[^()]*)*))?\)", value)
            if not m:
                break
            got = self.custom(node, m.group(1))
            if got is None:
                got = (m.group(2) or "").strip()
            value = value[: m.start()] + got + value[m.end():]
        return value

    def computed(self, node: Node) -> dict:
        """The few properties the gates need, resolved and inherited."""
        if node._computed is not None:
            return node._computed
        parent = self.computed(node.parent) if node.parent is not None and node.parent.tag != "#document" else {}
        decl = self.declared(node) if node.tag != "#document" else {}
        c: dict = {}
        res = lambda v: self.resolve(v, node)  # noqa: E731
        # inherited text properties
        colour = None
        if "color" in decl and "{{" not in decl["color"]:
            cs = find_colours(res(decl["color"]))
            colour = cs[0][1] if cs else None
            if not cs and res(decl["color"]).strip().lower() in ("inherit", "currentcolor"):
                colour = parent.get("color")
        c["color"] = colour if colour is not None else parent.get("color")
        c["color-explicit"] = "color" in decl and "{{" not in decl["color"]
        size = parent.get("font-size", 16.0)
        if "font-size" in decl and "{{" not in decl["font-size"]:
            size = px_size(res(decl["font-size"]), parent.get("font-size", 16.0)) or size
        c["font-size"] = size
        weight = parent.get("font-weight", 700 if node.tag in ("b", "strong", "th", "h1", "h2", "h3", "h4") else 400)
        if "font-weight" in decl:
            w = res(decl["font-weight"]).strip().lower()
            weight = 700 if w in ("bold", "bolder") else (400 if w in ("normal", "lighter") else (int(w) if w.isdigit() else weight))
        c["font-weight"] = weight
        # background: a colour, gradient stops, or nothing
        bg_colour, stops = None, []
        for prop in ("background", "background-color", "background-image"):
            if prop not in decl or "{{" in decl[prop]:
                continue
            v = res(decl[prop])
            low = v.lower()
            if "gradient(" in low:
                stops = [col for _, col in find_colours(v)]
                if prop == "background":
                    bg_colour = None
            elif prop in ("background", "background-color"):
                cs = find_colours(v)
                bg_colour = cs[0][1] if cs else (NAMED["transparent"] if "none" in low or "transparent" in low else bg_colour)
                if prop == "background":
                    stops = [] if "gradient(" not in low else stops
        c["bg"] = bg_colour
        c["bg-stops"] = stops
        # borders, outline, shadow: the colours that make an element visible
        edge_colours, has_border = [], False
        for prop, v in decl.items():
            if "{{" in v:
                continue
            if prop == "border" or re.fullmatch(r"border-(top|right|bottom|left)", prop) or prop.startswith("border-") and prop.endswith("-color") or prop == "border-color":
                v2 = res(v)
                low = v2.strip().lower()
                if low in ("0", "none", "0px") or re.match(r"^(0|0px)\s", low) or " none" in f" {low}" and not find_colours(v2):
                    continue
                has_border = True
                edge_colours += [col for _, col in find_colours(v2)]
            elif prop in ("outline", "outline-color", "box-shadow"):
                v2 = res(v)
                if v2.strip().lower() in ("0", "none"):
                    continue
                has_border = has_border or prop != "box-shadow"
                edge_colours += [col for _, col in find_colours(v2)]
                if prop == "box-shadow":
                    c["shadow"] = True
        c["border"] = has_border
        c["edge-colours"] = edge_colours
        c["display"] = res(decl.get("display", "")).strip().lower() if "display" in decl else ""
        c["visibility"] = res(decl["visibility"]).strip().lower() if "visibility" in decl else parent.get("visibility", "")
        c["width"], c["height"] = px_len(decl.get("width")), px_len(decl.get("height"))
        node._computed = c
        return c


def px_size(value: str, parent_px: float) -> float | None:
    v = value.strip().lower()
    m = re.match(r"^([\d.]+)\s*(px|em|rem|%|pt)?$", v)
    if not m:
        return None
    n = float(m.group(1))
    unit = m.group(2) or "px"
    if unit == "px":
        return n
    if unit == "em":
        return n * parent_px
    if unit == "rem":
        return n * 16.0
    if unit == "%":
        return n / 100 * parent_px
    if unit == "pt":
        return n * 4 / 3
    return None


def px_len(value: str | None) -> int | None:
    if not value:
        return None
    m = re.match(r"^\s*(\d+(?:\.\d+)?)px\s*$", value)
    return int(float(m.group(1))) if m else None


# ------------------------------------------------------------------------------------------------------- the board

class Board:
    def __init__(self, path: str, src: str, tokens: Tokens, svg_attrs: bool = True) -> None:
        self.path = path
        self.name = os.path.basename(path)
        self.src = src
        self.tokens = tokens
        self.svg_attrs = svg_attrs
        self.tree = parse_html(src)
        self.title = self.tree.title
        self.rules = []
        for css in self.tree.styles:
            self.rules += parse_css(css)
        self.cascade = Cascade(self.rules, self.tree.root)
        self.root = self._find_root()
        self.frames: list[Node] = []
        self._mark()

    # the board root: the first element inside <x-dc>, else the first element in <body>, else <body> / the document
    def _find_root(self) -> Node:
        xdc = next((n for n in self.tree.root.walk() if n.tag == "x-dc"), None)
        host = xdc or next((n for n in self.tree.root.walk() if n.tag == "body"), None) or self.tree.root
        for ch in host.children:
            if ch.tag not in SKIP_TAGS and ch.tag != "helmet":
                return ch
        return host

    def _mark(self) -> None:
        # hidden-at-rest subtrees, repeat multipliers, frames
        order = 0
        for n in self.root.walk():
            n.index = order
            order += 1
            p = n.parent
            hidden = p.hidden if p is not None else False
            mult = p.mult if p is not None else 1
            if n.tag in SKIP_TAGS or "hidden" in n.attrs:
                hidden = True
            if n.tag == "sc-if":
                hint = n.attrs.get("hint-placeholder-val", "").replace(" ", "").lower()
                if hint in ("{{false}}", "false", "{{0}}", "0", "{{''}}", "{{\"\"}}"):
                    hidden = True
            if n.tag == "sc-for":
                cnt = n.attrs.get("hint-placeholder-count", "").strip()
                if cnt.isdigit() and int(cnt) > 0:
                    mult *= int(cnt)
            if n.tag == "input" and n.attrs.get("type", "").lower() in ("hidden", "radio", "checkbox"):
                hidden = True
            for prop, value, _ in n.inline:
                if prop == "display" and value.strip().lower() == "none":
                    hidden = True
                if prop == "visibility" and value.strip().lower() == "hidden":
                    hidden = True
            n.hidden = hidden
            n.mult = mult
        # frames: the root, then screen-sized containers not nested in another
        self.frames = [self.root]
        for n in self.root.walk():
            if n is self.root or n.tag in PICTURE_TAGS or n.inside(PICTURE_TAGS):
                continue
            if self.size_of(n) in SCREEN_SIZES and not any(a is not self.root and a in self.frames for a in n.ancestors()):
                self.frames.append(n)
        for n in self.root.walk():
            n.frame = next((a for a in [n] + list(n.ancestors()) if a in self.frames), self.root)

    def size_of(self, n: Node) -> tuple[int, int] | None:
        w = h = None
        for prop, value, _ in n.inline:
            if prop == "width":
                w = px_len(value)
            elif prop == "height":
                h = px_len(value)
        if w is None or h is None:
            return None
        return w, h

    def frame_index(self, n: Node) -> int:
        return self.frames.index(n.frame) if n.frame in self.frames else 0

    def frame_label(self, i: int) -> str:
        f = self.frames[i]
        size = self.size_of(f)
        tag = "root" if i == 0 else f"screen {i}"
        return f"{tag} {size[0]}x{size[1]}" if size else tag

    # -- the colour literals: inline styles, <style> blocks (values only), SVG attributes
    def colour_literals(self) -> list[tuple[str, tuple, str]]:
        out = []
        for n in self.root.walk():
            for prop, value, _ in n.inline:
                for lit, col in find_colours(value):
                    out.append((lit, col, f"{n.describe()} {prop}"))
            if self.svg_attrs and (n.tag == "svg" or n.inside({"svg"})):
                for attr in ("fill", "stroke", "stop-color", "flood-color", "lighting-color"):
                    v = n.attrs.get(attr)
                    if v and "{{" not in v:
                        for lit, col in find_colours(v):
                            out.append((lit, col, f"{n.tag} {attr}="))
        for r in self.rules:
            for prop, value, _ in r["decls"]:
                for lit, col in find_colours(value):
                    out.append((lit, col, f"<style> {r['selector'][:40]} {prop}"))
        return out

    def font_sizes(self) -> list[tuple[str, str]]:
        """Every declared font-size: (value, where)."""
        out = []
        for n in self.root.walk():
            for prop, value, _ in n.inline:
                if prop == "font-size":
                    out.append((value, n.describe()))
        for r in self.rules:
            if r["kind"] == "keyframes":
                continue
            for prop, value, _ in r["decls"]:
                if prop == "font-size":
                    out.append((value, f"<style> {r['selector'][:40]}"))
        return out

    # -- the background under a node, composited; None when nothing opaque is found up to the root
    def background_of(self, n: Node) -> tuple[list[tuple], bool]:
        """-> ([opaque colours under the node: one, or one per gradient stop], resolvable)"""
        layers: list = []  # (colour or stops) from the node up
        node: Node | None = n
        while node is not None and node.tag != "#document":
            c = self.cascade.computed(node)
            if c["bg-stops"]:
                layers.append([s for s in c["bg-stops"]])
                if all(s[3] >= 0.999 for s in c["bg-stops"]):
                    break
            elif c["bg"] is not None and c["bg"][3] > 0:
                layers.append([c["bg"]])
                if c["bg"][3] >= 0.999:
                    break
            if node is self.root:
                break
            node = node.parent
        if not layers:
            return [], False
        bottom = layers[-1]
        if not all(col[3] >= 0.999 for col in bottom):
            return [], False  # nothing opaque under it: the picture or the canvas shows through
        results = [tuple(col[:3]) + (1.0,) for col in bottom]
        for layer in reversed(layers[:-1]):
            results = [composite(top, under) for under in results for top in layer]
            if len(results) > 16:
                results = results[:16]
        return results, True

    def text_nodes(self) -> list[Node]:
        out = []
        for n in self.root.walk():
            if n.hidden or n.tag in SKIP_TAGS or n.inside(SKIP_TAGS | {"svg"}) or n.tag == "svg":
                continue
            if n.text():
                out.append(n)
        return out

    def node_colours(self, n: Node) -> list[tuple]:
        """Every colour that paints the node: background, stops, text, edges, and its SVG attributes."""
        c = self.cascade.computed(n)
        cols = []
        if c["bg"] is not None and c["bg"][3] > 0:
            cols.append(c["bg"])
        cols += [s for s in c["bg-stops"] if s[3] > 0]
        if c["color-explicit"] and c["color"] is not None and n.text():
            cols.append(c["color"])
        cols += c["edge-colours"]
        if self.svg_attrs and n.tag == "svg":
            for d in n.walk():
                for attr in ("fill", "stroke", "stop-color"):
                    v = d.attrs.get(attr)
                    if v and "{{" not in v:
                        cols += [col for _, col in find_colours(v)]
        return cols

    def is_visual(self, n: Node) -> bool:
        if n.tag in PICTURE_TAGS:
            return True
        c = self.cascade.computed(n)
        if n.text():
            return True
        if (c["bg"] is not None and c["bg"][3] > 0) or c["bg-stops"]:
            return True
        if c["border"] or c.get("shadow"):
            return True
        return n.tag in ("input", "textarea", "select", "hr")


# --------------------------------------------------------------------------------------------------------- the gates

def gate(name: str, status: str, reason: str, **details) -> dict:
    return {"gate": name, "status": status, "reason": reason, "details": details}


def gate_colours(b: Board, legacy_date: _dt.date | None) -> dict:
    lits = b.colour_literals()
    typed: dict = {}
    total = 0
    for lit, col, where in lits:
        total += 1
        if col[3] == 0 or tuple(col[:3]) in ((255, 255, 255), (0, 0, 0)) or b.tokens.is_token(col):
            continue
        key = hexname(col)
        entry = typed.setdefault(key, {"count": 0, "where": where, "literal": lit})
        entry["count"] += 1
    if not typed:
        return gate("colours", "ok", f"{total} colour literals, every one a token colour ({b.tokens.source})", literals=total)
    items = sorted(typed.items(), key=lambda kv: -kv[1]["count"])
    shown = []
    for key, e in items[:8]:
        near, near_hex = b.tokens.nearest(parse_colour(key))
        shown.append(f"{key} x{e['count']} (nearest {near} {near_hex})")
    more = f", +{len(items) - 8} more" if len(items) > 8 else ""
    n_typed = sum(e["count"] for e in typed.values())
    reason = f"{n_typed} of {total} colour literals are typed in, {len(items)} distinct: " + ", ".join(shown) + more
    status = "FAIL"
    if legacy_date is not None and legacy_date < TOKENS_DATE:
        status = "warn"
        reason = f"legacy board ({legacy_date}, before the tokens of {TOKENS_DATE}): " + reason
    return gate("colours", status, reason, typed={k: v["count"] for k, v in items}, literals=total)


def gate_type(b: Board, phone: bool) -> dict:
    scale = b.tokens.type_scale
    if not scale:
        return gate("type", "skip", "the tokens name no type scale (froglet-tokens.json `type` is empty): nothing to check against")
    allowed = set(scale) | (set(MOBILE_SCALE) if phone else set())
    sizes = b.font_sizes()
    off: dict = {}
    unchecked = 0
    checked = 0
    for value, where in sizes:
        if "{{" in value:
            continue
        v = value.strip().lower()
        m = re.match(r"^([\d.]+)px$", v)
        if not m:
            unchecked += 1
            continue
        checked += 1
        px = float(m.group(1))
        if px not in allowed:
            k = f"{px:g}px"
            off.setdefault(k, [0, where])
            off[k][0] += 1
    scale_txt = " ".join(str(s) for s in sorted(allowed, reverse=True))
    extra = f"; {unchecked} relative sizes (em/rem/%) not checked" if unchecked else ""
    if not off:
        return gate("type", "ok", f"{checked} font sizes, all on the scale {scale_txt}{extra}", checked=checked)
    items = sorted(off.items(), key=lambda kv: -kv[1][0])
    shown = ", ".join(f"{k} x{v[0]}" for k, v in items[:10]) + (f", +{len(items) - 10} more" if len(items) > 10 else "")
    n_off = sum(v[0] for v in off.values())
    return gate("type", "FAIL", f"{n_off} of {checked} font sizes are off the scale {scale_txt}: {shown}{extra}",
                off={k: v[0] for k, v in items}, checked=checked)


def gate_contrast(b: Board) -> dict:
    fails, warns = [], []
    checked = skipped = 0
    lowest = None
    for n in b.text_nodes():
        c = b.cascade.computed(n)
        colour = c["color"]
        if colour is None:
            skipped += 1
            continue
        unders, ok = b.background_of(n)
        if not ok:
            skipped += 1
            continue
        size = c["font-size"]
        large = size >= 24 or (size >= 18.66 and c["font-weight"] >= 700)
        need = 3.0 if large else 4.5
        ratios = []
        for under in unders:
            fg = composite(colour, under) if colour[3] < 0.999 else colour
            ratios.append(contrast(fg, under))
        checked += 1
        worst, best = min(ratios), max(ratios)
        if lowest is None or worst < lowest[0]:
            lowest = (worst, n)
        desc = f"{n.describe()} {hexname(colour)} on {hexname(unders[ratios.index(worst)])} = {worst:.1f}:1 (needs {need:g}, {size:g}px)"
        if best < need:
            fails.append(desc)
        elif worst < need:
            warns.append(desc + " at one gradient stop")
    low = f"; lowest {lowest[0]:.1f}:1" if lowest else ""
    if fails:
        shown = "; ".join(fails[:5]) + (f"; +{len(fails) - 5} more" if len(fails) > 5 else "")
        return gate("contrast", "FAIL", f"{len(fails)} of {checked} text elements under 4.5:1 (3.0 at 24 px): {shown}"
                    + (f"; {skipped} without a resolvable background" if skipped else ""), fails=len(fails), checked=checked, skipped=skipped)
    if warns:
        shown = "; ".join(warns[:3]) + (f"; +{len(warns) - 3} more" if len(warns) > 3 else "")
        return gate("contrast", "warn", f"{checked} text elements pass; {len(warns)} fail at one stop of a gradient: {shown}"
                    + (f"; {skipped} skipped (no resolvable background or colour)" if skipped else ""), warns=len(warns), checked=checked, skipped=skipped)
    return gate("contrast", "ok", f"{checked} text elements at 4.5:1 or better (3.0 at 24 px){low}"
                + (f"; {skipped} skipped (no resolvable background or colour)" if skipped else ""), checked=checked, skipped=skipped)


def safe_marker(b: Board, frame: Node) -> str | None:
    for n in frame.walk():
        if n is frame:
            continue
        blob = " ".join([n.attrs.get("class", ""), n.attrs.get("id", ""), n.attrs.get("aria-label", "")]
                        + [v for k, v in n.attrs.items() if k.startswith("data-")] + [k for k in n.attrs if k.startswith("data-")])
        if "safe" in blob.lower():
            return n.describe()
        st = {p: v for p, v, _ in n.inline}
        inset = st.get("inset")
        sides = [st.get(s) for s in ("left", "top", "right", "bottom")]
        inset_px = px_len(inset) if inset else (px_len(sides[0]) if all(sides) and len({s.strip() for s in sides}) == 1 else None)
        if inset_px is None or inset_px < 8:
            continue
        edge = " ".join(v for p, v in st.items() if p in ("outline", "border", "box-shadow", "outline-style", "border-style"))
        if "dashed" in edge or "dotted" in edge or "inset" in edge:
            return n.describe() + f" (inset {inset_px}px)"
    return None


def gate_phone(b: Board, canvas_title: str) -> dict:
    sizes = [b.size_of(f) for f in b.frames]
    phone_frames = [s for s in sizes if s in PHONE_SIZES]
    title = f"{b.title} {canvas_title}".lower()
    by_title = bool(re.search(r"\b(phone|phones|portrait)\b", title))
    if not phone_frames and not by_title:
        root = sizes[0]
        head = f"no phone declared (frame {root[0]}x{root[1]}" if root else "no phone declared (root without a px size"
        return gate("phone", "skip", head + "; a phone is an 800x450 or 960x540 frame, or phone / portrait in the title)", declared=False)
    if not phone_frames:
        root = sizes[0]
        return gate("phone", "FAIL", f"the title says phone but no frame is 800x450 or 960x540 (root {root[0]}x{root[1]})" if root
                    else "the title says phone but no frame has a px size", declared=True)
    frames = [f for f in b.frames if b.size_of(f) in PHONE_SIZES]
    marks = [(b.frame_label(b.frames.index(f)), safe_marker(b, f)) for f in frames]
    missing = [lab for lab, m in marks if m is None]
    if missing:
        return gate("phone", "skip", f"phone frame {', '.join(f'{s[0]}x{s[1]}' for s in phone_frames)} present; safe area not checkable: no marker in "
                    + ", ".join(missing) + " (mark it with class / aria-label / data-safe-area, or an inset dashed outline)", declared=True, marker=False)
    return gate("phone", "ok", f"phone frame {', '.join(f'{s[0]}x{s[1]}' for s in phone_frames)} with a safe-area marker: "
                + "; ".join(f"{lab}: {m}" for lab, m in marks), declared=True, marker=True)


def gate_cta(b: Board) -> dict:
    per_frame: dict = {i: [] for i in range(len(b.frames))}
    lime_nodes: set = set()
    for n in b.root.walk():
        if n.hidden or n.tag in SKIP_TAGS or n.inside(SKIP_TAGS | {"svg"}):
            continue
        if any(b.tokens.is_lime(c) for c in b.node_colours(n)):
            lime_nodes.add(id(n))
            if any(id(a) in lime_nodes and a.frame is n.frame for a in n.ancestors()):
                continue  # part of the same call to action
            per_frame[b.frame_index(n)].append((n.describe(), n.mult))
    bad = {i: items for i, items in per_frame.items() if sum(m for _, m in items) > 1}
    total = sum(m for items in per_frame.values() for _, m in items)
    if not bad:
        return gate("cta", "ok", f"{total} Lime call{'s' if total != 1 else ''} to action across {len(b.frames)} frame{'s' if len(b.frames) != 1 else ''}, at most one each",
                    per_frame={b.frame_label(i): sum(m for _, m in it) for i, it in per_frame.items()})
    parts = []
    for i, items in sorted(bad.items()):
        shown = ", ".join(d + (f" x{m}" if m > 1 else "") for d, m in items[:4]) + (f", +{len(items) - 4} more" if len(items) > 4 else "")
        parts.append(f"{b.frame_label(i)}: {sum(m for _, m in items)} Lime elements ({shown})")
    return gate("cta", "FAIL", "more than one Lime call to action in " + "; ".join(parts),
                per_frame={b.frame_label(i): sum(m for _, m in it) for i, it in per_frame.items()})


def noise_counts(b: Board) -> dict:
    counts = {i: 0 for i in range(len(b.frames))}
    for n in b.root.walk():
        if n.hidden or n.tag in SKIP_TAGS or n.inside(SKIP_TAGS | PICTURE_TAGS):
            continue
        if b.is_visual(n):
            counts[b.frame_index(n)] += n.mult
    return counts


def gate_noise(b: Board, budget: int, budget_source: str) -> dict:
    counts = noise_counts(b)
    over = {i: c for i, c in counts.items() if c > budget}
    desc = ", ".join(f"{b.frame_label(i)}: {c}" for i, c in counts.items())
    if over:
        return gate("noise", "FAIL", f"over the budget of {budget} ({budget_source}) in " + "; ".join(f"{b.frame_label(i)}: {c} visual elements" for i, c in over.items()),
                    counts={b.frame_label(i): c for i, c in counts.items()}, budget=budget)
    return gate("noise", "ok", f"visual elements per frame within the budget of {budget} ({budget_source}): {desc}",
                counts={b.frame_label(i): c for i, c in counts.items()}, budget=budget)


# ----------------------------------------------------------------------------------------------- the doc, the canvas

def round_dates(doc_path: str) -> dict[int, _dt.date]:
    """Rev N -> the earliest date a section heading of Docs/MENU_REDESIGN_B.md gives it."""
    out: dict[int, _dt.date] = {}
    if not os.path.exists(doc_path):
        return out
    with open(doc_path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if not line.startswith("#"):
                continue
            rm = re.search(r"\bRev (\d+)\b", line)
            dm = re.search(r"\((\d{4})-(\d{2})-(\d{2})", line)
            if rm and dm:
                rev = int(rm.group(1))
                try:
                    d = _dt.date(int(dm.group(1)), int(dm.group(2)), int(dm.group(3)))
                except ValueError:
                    continue
                if rev not in out or d < out[rev]:
                    out[rev] = d
    return out


def board_rev(name: str) -> int | None:
    m = re.match(r"^R(\d+)[A-Za-z_-]", os.path.basename(name))
    return int(m.group(1)) if m else None


def canvas_title(path: str) -> str:
    cj = os.path.join(os.path.dirname(os.path.abspath(path)), "canvas.json")
    if not os.path.exists(cj):
        return ""
    try:
        with open(cj, encoding="utf-8") as f:
            data = json.load(f)
        return str((data.get("boards") or {}).get(os.path.basename(path), {}).get("title", ""))
    except (OSError, ValueError):
        return ""


# ------------------------------------------------------------------------------------------------------------ driver

def check_board(path: str, tokens: Tokens, budget: int, budget_source: str, legacy: bool, board_date: _dt.date | None,
                doc_path: str, svg_attrs: bool = True) -> dict:
    with open(path, encoding="utf-8", errors="replace") as f:
        src = f.read()
    b = Board(path, src, tokens, svg_attrs=svg_attrs)
    ctitle = canvas_title(path)
    legacy_date = None
    legacy_note = ""
    if legacy:
        if board_date is not None:
            legacy_date = board_date
        else:
            rev = board_rev(path)
            dates = round_dates(doc_path)
            if rev is None:
                legacy_note = "no Rev in the file name; --legacy not applied (give --board-date)"
            elif rev not in dates:
                legacy_note = f"Rev {rev} has no dated section in {_rel(doc_path)}; --legacy not applied"
            else:
                legacy_date = dates[rev]
    phone_declared = any(b.size_of(f) in PHONE_SIZES for f in b.frames) or bool(re.search(r"\b(phone|phones|portrait)\b", f"{b.title} {ctitle}".lower()))
    gates = [gate_colours(b, legacy_date), gate_type(b, phone_declared), gate_contrast(b), gate_phone(b, ctitle), gate_cta(b),
             gate_noise(b, budget, budget_source)]
    if legacy_note:
        gates[0]["reason"] += f" [{legacy_note}]"
    return {"file": path, "title": b.title, "canvas_title": ctitle, "frames": [b.size_of(f) for f in b.frames],
            "legacy_date": legacy_date.isoformat() if legacy_date else None, "gates": gates,
            "fail": any(g["status"] == "FAIL" for g in gates)}


def busiest_frame(path: str, tokens: Tokens, svg_attrs: bool = True) -> int:
    with open(path, encoding="utf-8", errors="replace") as f:
        b = Board(path, f.read(), tokens, svg_attrs=svg_attrs)
    return max(noise_counts(b).values() or [0])


def budget_from(args, tokens: Tokens) -> tuple[int, str]:
    if args.budget is not None:
        return int(args.budget), "--budget"
    if args.baseline:
        counts = [busiest_frame(p, tokens, not args.no_svg_attrs) for p in args.baseline]
        med = statistics.median(counts)
        return int(round(med * BUDGET_FACTOR)), f"{BUDGET_FACTOR} x median {med:g} of {len(counts)} baseline board{'s' if len(counts) != 1 else ''}"
    return DEFAULT_BUDGET, "default"


def print_report(result: dict, header: bool) -> None:
    if header:
        print(f"== {result['file']}" + (f"  ({result['title']})" if result["title"] else ""))
    for g in result["gates"]:
        print(f"{g['gate']:<9}{g['status']:<5} {g['reason']}")


def parse_date(s: str | None) -> _dt.date | None:
    if not s:
        return None
    try:
        return _dt.date.fromisoformat(s)
    except ValueError:
        raise SystemExit(f"--board-date {s!r} is not YYYY-MM-DD")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="the mockup gate (Docs/UI_STYLE_GUIDE.md): colours, type, contrast, phone, cta, noise")
    ap.add_argument("boards", nargs="*", help="mockup / board HTML files")
    ap.add_argument("--budget", type=int, default=None, help="noise budget: visual elements per screen frame")
    ap.add_argument("--baseline", nargs="+", default=None, help="picked boards; the budget is 1.3 x their median busiest frame")
    ap.add_argument("--legacy", action="store_true", help="typed colours on a board dated before 2026-11-03 are a warn")
    ap.add_argument("--board-date", default=None, help="the board's date for --legacy (YYYY-MM-DD), when the doc has none")
    ap.add_argument("--doc", default=DOC, help="the redesign doc the round dates are read from")
    ap.add_argument("--tokens", default=TOKENS_DIR, help="the folder with froglet.css / froglet-tokens.json")
    ap.add_argument("--no-svg-attrs", action="store_true", help="leave SVG fill / stroke attributes out of the colour gate")
    ap.add_argument("--json", action="store_true", help="machine output")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    if not args.boards:
        ap.error("name a board, or --self-test")
    for p in args.boards + (args.baseline or []):
        if not os.path.exists(p):
            print(f"not found: {p}", file=sys.stderr)
            return 2
    tokens = load_tokens(args.tokens)
    budget, source = budget_from(args, tokens)
    results = [check_board(p, tokens, budget, source, args.legacy, parse_date(args.board_date), args.doc, not args.no_svg_attrs)
               for p in args.boards]
    failed = [r for r in results if r["fail"]]
    if args.json:
        print(json.dumps({"budget": budget, "budget_source": source, "boards": results, "exit": 1 if failed else 0}, indent=1))
    else:
        for r in results:
            print_report(r, header=len(results) > 1)
        if len(results) > 1:
            print(f"{len(results) - len(failed)} of {len(results)} boards pass; " + (", ".join(os.path.basename(r['file']) for r in failed) + " FAIL" if failed else "no FAIL"))
    return 1 if failed else 0


# --------------------------------------------------------------------------------------------------------- self-test

def _fixture(body: str, title: str = "Fixture", style: str = "", w: int = 1920, h: int = 1080) -> str:
    return ("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><title>" + title + "</title></head><body><x-dc><helmet><style>"
            "body{margin:0;background:#00010A}.lab{font-size:14px;color:#747BAD}" + style + "</style></helmet>"
            f"<div style=\"width: {w}px; height: {h}px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-size: 18px\">"
            + body + "</div></x-dc></body></html>")


CLEAN_BODY = (
    '<div style="position:absolute;left:40px;top:40px;background:#222645;border:1px solid #434C89;padding:16px">'
    '<span style="font-size:28px">Hangar</span><p>Pick a hull and fly it.</p>'
    '<span style="color:#FFAE00;font-size:20px">1,250</span></div><span class="lab" style="position:absolute;left:40px;top:260px">Crystals</span>'
    '<button style="position:absolute;left:40px;top:300px;background:#99FF80;color:#00041F;font-size:18px;border:0">JOIN</button>'
    '<div style="position:absolute;inset:0;box-shadow:0 0 18px rgba(0,212,255,.22)"></div>'
    '<svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor"><path d="M1 1l5 5"></path></svg>'
)


def self_test() -> int:
    tmp = tempfile.mkdtemp(prefix="check_mockup_")
    tokens_dir = os.path.join(tmp, "tokens")
    os.makedirs(tokens_dir)
    with open(os.path.join(tokens_dir, "froglet-tokens.json"), "w", encoding="utf-8") as f:
        json.dump({"themes": {"deep-sea": {"ground": "#00041F", "ground-deep": "#00010A", "surface": "#222645", "surface-raised": "#434C89",
                                           "text": "#E6E9FF", "text-soft": "#747BAD", "cta": "#99FF80", "purchase": "#FFAE00", "control": "#00D4FF"}},
                   "palettes": {"lime": [{"name": "lime-60", "hex": "#509509"}]},
                   "type": {"display": 48, "h1": 36, "h2": 28, "h3": 22, "body": 18, "body-small": 15, "button": 18,
                            "button-small": 14, "data-large": 44, "data": 20, "data-small": 15}}, f)
    doc = os.path.join(tmp, "MENU_REDESIGN_B.md")
    with open(doc, "w", encoding="utf-8") as f:
        f.write("# doc\n\n## 94. Rev 29 mockups: Squads, six ways (2026-10-27, awaiting a pick)\n\n## 125. Rev 54 mockups: colour sets (2026-11-05; picked A)\n")
    tokens = load_tokens(tokens_dir)
    assert tokens.is_lime(parse_colour("#99FF80")) and tokens.is_lime(parse_colour("#509509")) and not tokens.is_lime(parse_colour("#FFAE00"))
    assert tokens.type_scale == [14, 15, 18, 20, 22, 28, 36, 44, 48]
    # colour parsing
    assert parse_colour("#9f8") == (153, 255, 136, 1.0) and parse_colour("rgba(153, 255, 128, .22)") == (153, 255, 128, 0.22)
    assert parse_colour("hsl(0, 100%, 50%)") == (255, 0, 0, 1.0) and parse_colour("#99FF8080")[3] == 0.502
    assert abs(contrast((255, 255, 255, 1.0), (0, 0, 0, 1.0)) - 21.0) < 0.01
    r = contrast(parse_colour("#747BAD"), parse_colour("#434C89"))
    assert 1.5 < r < 2.5, r  # the 2.0 pair below is honest: two token colours at about 2:1

    def run(name: str, html: str, **kw) -> dict:
        p = os.path.join(tmp, name)
        with open(p, "w", encoding="utf-8") as f:
            f.write(html)
        budget = kw.pop("budget", DEFAULT_BUDGET)
        return check_board(p, tokens, budget, "default", kw.pop("legacy", False), kw.pop("board_date", None), doc)

    def status(res: dict, g: str) -> str:
        return next(x["status"] for x in res["gates"] if x["gate"] == g)

    fired = 0
    # the clean fixture passes every gate (phone skips: no phone declared)
    clean = run("clean.dc.html", _fixture(CLEAN_BODY))
    bad = [g for g in clean["gates"] if g["status"] == "FAIL"]
    assert not bad, "clean fixture failed: " + "; ".join(f"{g['gate']}: {g['reason']}" for g in bad)
    assert status(clean, "phone") == "skip" and status(clean, "colours") == "ok" and status(clean, "type") == "ok"
    assert status(clean, "contrast") == "ok" and status(clean, "cta") == "ok" and status(clean, "noise") == "ok"
    # (1) a typed-in colour
    res = run("typed.dc.html", _fixture(CLEAN_BODY + '<span style="color:#1AB2B2">row</span>'))
    assert status(res, "colours") == "FAIL" and "#1AB2B2" in next(g["reason"] for g in res["gates"] if g["gate"] == "colours"), res
    fired += 1
    res = run("typed_style.dc.html", _fixture(CLEAN_BODY, style=".x{border:1px solid #33FF66}"))
    assert status(res, "colours") == "FAIL"
    res = run("typed_svg.dc.html", _fixture(CLEAN_BODY + '<svg width="9" height="9"><path stroke="#49B8FF" d="M0 0"></path></svg>'))
    assert status(res, "colours") == "FAIL"
    # an alpha variant, white, black, transparent, a token in rgb() pass
    res = run("alpha.dc.html", _fixture(CLEAN_BODY + '<div style="background:rgba(0,4,31,.6);border:1px solid white;color:black;outline:1px solid transparent;box-shadow:0 0 0 rgb(67,76,137)">x</div>'))
    assert status(res, "colours") == "ok", res
    # (2) a 2.0 contrast pair
    res = run("contrast.dc.html", _fixture(CLEAN_BODY + '<div style="position:absolute;top:500px;background:#434C89"><span style="color:#747BAD">Lost pilot</span></div>'))
    assert status(res, "contrast") == "FAIL" and "Lost pilot" in next(g["reason"] for g in res["gates"] if g["gate"] == "contrast"), res
    fired += 1
    # the same pair at 24 px passes the 3.0 bar? no: 2.0 < 3.0, still a fail; at 24 px a 3.2 pair passes
    res = run("large.dc.html", _fixture(CLEAN_BODY + '<div style="position:absolute;top:500px;background:#222645"><span style="color:#747BAD;font-size:28px">Big soft</span></div>'))
    rr = contrast(parse_colour("#747BAD"), parse_colour("#222645"))
    assert 3.0 <= rr < 4.5 and status(res, "contrast") == "ok", (rr, res)
    # an rgba background composited over the ground; inherited colour from the root
    res = run("rgba.dc.html", _fixture(CLEAN_BODY + '<div style="position:absolute;top:500px;background:rgba(230,233,255,.9)"><span>Pale card</span></div>'))
    assert status(res, "contrast") == "FAIL", res
    # a gradient: fails at one stop only -> warn
    res = run("gradient.dc.html", _fixture(CLEAN_BODY + '<div style="position:absolute;top:500px;background:linear-gradient(180deg,#00041F,#E6E9FF)"><span>Over a wash</span></div>'))
    assert status(res, "contrast") == "warn", res
    # (3) two Lime buttons
    res = run("twolime.dc.html", _fixture(CLEAN_BODY + '<button style="position:absolute;left:400px;top:300px;background:#99FF80;color:#00041F;border:0">ACCEPT</button>'))
    assert status(res, "cta") == "FAIL", res
    fired += 1
    # a Lime button made of a Lime shell and a Lime fill (var()) is one call to action; a Lime eyebrow is a second
    res = run("nested.dc.html", _fixture('<button class="btn" style="--bc:#99FF80;--bf:#99FF80;--bt:#00041F;position:absolute;top:40px"><span>JOIN</span></button>',
                                        style=".btn{background:var(--bc);padding:1px;border:0}.btn>span{background:var(--bf);color:var(--bt);display:block}"))
    assert status(res, "cta") == "ok" and status(res, "contrast") == "ok" and status(res, "colours") == "ok", res
    res = run("eyebrow.dc.html", _fixture('<button style="position:absolute;top:40px;background:#99FF80;color:#00041F;border:0">JOIN</button>'
                                         '<span class="lab" style="position:absolute;top:140px;color:#99FF80">Party invite</span>'))
    assert status(res, "cta") == "FAIL", res
    # a hidden-at-rest <sc-if> and an inline display:none do not count; an <sc-for> multiplies
    res = run("scif.dc.html", _fixture(CLEAN_BODY + '<sc-if value="{{invite}}" hint-placeholder-val="{{ false }}"><button style="background:#99FF80;color:#00041F;border:0">JOIN</button></sc-if>'
                                      '<button style="display:none;background:#99FF80;color:#00041F;border:0">JOIN</button>'))
    assert status(res, "cta") == "ok", res
    res = run("scfor.dc.html", _fixture('<sc-for list="{{rows}}" as="r" hint-placeholder-count="4"><div style="border:1px solid #434C89;padding:8px">{{r.name}}<span style="color:#99FF80">online</span></div></sc-for>'))
    assert status(res, "cta") == "FAIL" and next(g["details"]["counts"] for g in res["gates"] if g["gate"] == "noise")["root 1920x1080"] == 9, res  # 4 rows x 2 + the root's ground
    # two screens stacked in one board (steps): one Lime each is fine
    res = run("steps.dc.html", _fixture('<div style="position:absolute;inset:0"><div style="position:absolute;left:0;top:0;width:1920px;height:1080px"><button style="background:#99FF80;color:#00041F;border:0">JOIN</button></div>'
                                       '<div style="position:absolute;left:0;top:0;width:1920px;height:1080px"><button style="background:#99FF80;color:#00041F;border:0">CLAIM</button></div></div>'))
    assert status(res, "cta") == "ok" and len(res["frames"]) == 3, res
    # (4) 200 elements against the default budget of 60
    res = run("noise.dc.html", _fixture("".join(f'<span style="border:1px solid #434C89">{i}</span>' for i in range(200))))
    assert status(res, "noise") == "FAIL", res
    fired += 1
    # a baseline of 10 / 20 / 30 elements (plus each root's ground) -> median 21, budget 27: 28 fails, 27 passes
    base = []
    for k in (10, 20, 30):
        p = os.path.join(tmp, f"base{k}.dc.html")
        with open(p, "w", encoding="utf-8") as f:
            f.write(_fixture("".join(f'<span style="border:1px solid #434C89">{i}</span>' for i in range(k))))
        base.append(p)
    ns = argparse.Namespace(budget=None, baseline=base, no_svg_attrs=False)
    budget, source = budget_from(ns, tokens)
    assert budget == 27 and "median 21" in source, (budget, source)
    assert status(run("b28.dc.html", _fixture("".join(f'<span style="border:1px solid #434C89">{i}</span>' for i in range(27))), budget=27), "noise") == "FAIL"
    assert status(run("b27.dc.html", _fixture("".join(f'<span style="border:1px solid #434C89">{i}</span>' for i in range(26))), budget=27), "noise") == "ok"
    # (5) type off the scale
    res = run("type.dc.html", _fixture(CLEAN_BODY + '<span style="font-size:17px">odd</span>'))
    assert status(res, "type") == "FAIL" and "17px" in next(g["reason"] for g in res["gates"] if g["gate"] == "type"), res
    fired += 1
    res = run("type_style.dc.html", _fixture(CLEAN_BODY, style=".odd{font-size:13px}"))
    assert status(res, "type") == "FAIL"
    # no type scale in the tokens -> skip, and say so
    no_scale = Tokens(tokens.colours, tokens.lime, None, "test")
    p = os.path.join(tmp, "clean.dc.html")
    b = Board(p, open(p, encoding="utf-8").read(), no_scale)
    assert gate_type(b, False)["status"] == "skip"
    # (6) the phone frame: title says phone but the frame is 1920x1080
    res = run("phone_wrong.dc.html", _fixture(CLEAN_BODY, title="Phone home"))
    assert status(res, "phone") == "FAIL", res
    fired += 1
    # an 800x450 frame without a marker is not checkable; with one it passes, and the mobile type column is allowed
    res = run("phone_nomark.dc.html", _fixture('<span style="font-size:16px">Home</span>', w=800, h=450))
    assert status(res, "phone") == "skip" and "not checkable" in next(g["reason"] for g in res["gates"] if g["gate"] == "phone") and status(res, "type") == "ok", res
    res = run("phone_marked.dc.html", _fixture('<div data-safe-area style="position:absolute;inset:24px"></div><span style="font-size:16px">Home</span>', w=800, h=450))
    assert status(res, "phone") == "ok", res
    res = run("phone_outline.dc.html", _fixture('<div style="position:absolute;inset:24px;outline:1px dashed #434C89"></div>', w=960, h=540))
    assert status(res, "phone") == "ok", res
    res = run("phone_pc_type.dc.html", _fixture('<span style="font-size:16px">Home</span>'))
    assert status(res, "type") == "FAIL", res  # 16 is the mobile column only
    # (7) --legacy: a Rev 29 board (2026-10-27) warns on typed colours; a Rev 54 board (2026-11-05) still fails
    res = run("R29Fixture.dc.html", _fixture(CLEAN_BODY + '<span style="color:#1AB2B2">row</span>'), legacy=True)
    assert status(res, "colours") == "warn" and res["legacy_date"] == "2026-10-27" and not res["fail"], res
    res = run("R54Fixture.dc.html", _fixture(CLEAN_BODY + '<span style="color:#1AB2B2">row</span>'), legacy=True)
    assert status(res, "colours") == "FAIL" and res["legacy_date"] == "2026-11-05", res
    res = run("Undated.dc.html", _fixture(CLEAN_BODY + '<span style="color:#1AB2B2">row</span>'), legacy=True)
    assert status(res, "colours") == "FAIL" and "not applied" in next(g["reason"] for g in res["gates"] if g["gate"] == "colours"), res
    res = run("Undated2.dc.html", _fixture(CLEAN_BODY + '<span style="color:#1AB2B2">row</span>'), legacy=True, board_date=_dt.date(2026, 10, 20))
    assert status(res, "colours") == "warn", res
    fired += 1
    # the CLI: exit 1 on a FAIL, 0 on the clean fixture, --json parses
    import io
    import contextlib
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        rc = main([os.path.join(tmp, "typed.dc.html"), "--tokens", tokens_dir, "--doc", doc, "--json"])
    assert rc == 1 and json.loads(out.getvalue())["exit"] == 1
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        rc = main([os.path.join(tmp, "clean.dc.html"), "--tokens", tokens_dir, "--doc", doc])
    assert rc == 0 and out.getvalue().count("\n") == 6, out.getvalue()
    print(f"self-test ok: {len(GATES)} gates, {fired} negative controls fired, clean fixture passed (fixtures in {tmp})")
    return 0


if __name__ == "__main__":
    sys.exit(main())

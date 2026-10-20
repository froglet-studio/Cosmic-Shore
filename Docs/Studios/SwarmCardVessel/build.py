import json, math, re, sys, os, unicodedata
sys.path.insert(0, os.path.dirname(__file__))
from cards import E, OBJ

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_MD = os.path.join(HERE, "..", "SWARM_CARD_VESSEL.md")
REPO_JSON = os.path.join(HERE, "cards.json")


def slug(s):
    return re.sub(r"[^a-z0-9]+", "-", unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode().lower()).strip("-")


def value(st, L):
    if st[1] == "pts":
        return None
    _, kind, a, b, unit, dec = st
    v = a + b * (L - 1) if kind == "lin" else a * b ** (L - 1)
    return math.floor(v + 1e-9) if dec == 0 else round(v, dec)


def fmt(v, unit, dec):
    s = f"{v:.{dec}f}" if dec else str(int(v))
    if unit in ("%", "×", "/s"):
        return s + unit
    if unit in ("s", "u", "u/s"):
        return f"{s} {unit}"
    return s + unit


cards = []
for el, block in E.items():
    for i, (name, kind, obj, text, stats, ms) in enumerate(block["cards"], 1):
        out = []
        for st in stats:
            if st[1] == "pts":
                out.append(dict(label=st[0], kind="pts", pts=st[2]))
            else:
                _, k, a, b, unit, dec = st
                out.append(dict(label=st[0], kind=k, a=a, b=b, unit=unit, dec=dec,
                                key=[fmt(value(st, L), unit, dec) for L in (1, 5, 10, 15)]))
        cards.append(dict(id=f"{el.lower()}-{slug(name)}", n=i, element=el, name=name, kind=kind,
                          objective=OBJ[obj], text=text, stats=out,
                          milestones=[dict(level=l, text=t) for l, t in sorted(ms.items())]))

assert len(cards) == 80, len(cards)
assert len({c["id"] for c in cards}) == 80
assert len({c["name"] for c in cards}) == 80
meanings = {el: b["meaning"] for el, b in E.items()}

# ---- markdown ----
md = [open(os.path.join(HERE, "md_head.md")).read().rstrip(), ""]
for el in E:
    md += [f"## {el} (20 cards)", "", f"*{meanings[el]}*", "",
           "| # | Card | Kind | Best with | What it does | Scales: L1 / L5 / L10 / L15 | Milestones |",
           "|---|---|---|---|---|---|---|"]
    for c in (c for c in cards if c["element"] == el):
        sc = "<br>".join(f"{s['label']}: " + " / ".join(s["pts"] if s["kind"] == "pts" else s["key"]) for s in c["stats"])
        mi = "<br>".join(f"L{m['level']}: {m['text']}" for m in c["milestones"]) or "-"
        md.append(f"| {c['n']} | **{c['name']}** | {c['kind']} | {c['objective']} | {c['text']} | {sc} | {mi} |")
    md.append("")
os.makedirs(os.path.dirname(REPO_JSON), exist_ok=True)
open(REPO_MD, "w").write("\n".join(md))
json.dump(dict(revision=1, date="2026-10-20", meanings=meanings, cards=cards), open(REPO_JSON, "w"), indent=1, ensure_ascii=False)

# ---- page ----
page = open(os.path.join(HERE, "page.html")).read()
page = page.replace("/*DATA*/null", json.dumps(dict(meanings=meanings, cards=cards), ensure_ascii=False))
open(os.path.join(HERE, "..", "SwarmDeck.html"), "w").write(page)
print("ok", len(cards))

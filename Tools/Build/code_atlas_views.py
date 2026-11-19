#!/usr/bin/env python3
"""Code Atlas views: what the Loop Atlas map's inspection window shows for the node you are zoomed on, drawn by Amoebius.

Garrett, 2026-11-19: "a small inspection window that can use an amoebius tool to display a flora, fauna, vessel, etc.
so have that tool give a responsive window into the current view. if it is the home screen it shows the layout of the
buttons."

A web page cannot run the engine, so Amoebius draws the views ahead of time and the page picks one as you zoom:

- **Models**: every first-party FBX as a turntable, drawn on the CPU by Amoebius's `cs-asset model-preview --turntable`
  (the game's own material colours). Drag in the window to turn it. A model is the view of every object whose mesh
  is that FBX, of the prefab or scene holding it, of the types whose components sit on it, and of every folder and
  loop above them.
- **Screens**: the real game, booted headless in Amoebius with its control port, sweeps Menu_Main (`ui_sweep`: each
  menu screen, then each modal) and records every RectTransform's screen rect and text. The window draws that layout
  at its own size, and lights the rect of the node you are on: zoom to a button and the window shows the screen with
  that button lit.

Commands:
  sweep  [--sweep DIR]           boot the game in Amoebius (xvfb) and sweep Menu_Main at 1920x1080 (about 10 minutes)
  build  [--sweep DIR] [--out D] render the turntables and write views/ beside the Code Atlas output
  self-test                       the path matcher and the row filter refuse what they must

Needs the .NET 10 SDK (Port/), and a Code Atlas build in --out (code_atlas.py build) to map nodes. Output:
views/views.json (the views and node id -> views), views/m/NN.webp (turntables), views/ui/<view>.json (layouts).
"""
from __future__ import annotations

import argparse
import base64
import collections
import gzip
import io
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO / "Docs/LoopAtlas/code-out"
DEFAULT_SWEEP = REPO / "Docs/LoopAtlas/code-out/sweep"
CS_ASSET = REPO / "Port/src/CosmicShore.AssetTool/bin/Debug/net10.0/cs-asset"
PLAYER = REPO / "Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore"
SCENE = "Assets/_Scenes/Menu_Main.unity"
FRAMES, SIZE = 12, 160
MAX_VIEWS = 4
PORT = 47811
THIRD_PARTY = ("/Plugins/", "/ThirdParty/", "/Third Party/", "/TextMesh Pro/", "/Samples/")


def say(*a):
    print(*a, file=sys.stderr, flush=True)


# -- the sweep: the real game in Amoebius

def post(cmd: str, arg: str = "", timeout: int = 60) -> dict:
    import urllib.request
    req = urllib.request.Request(f"http://127.0.0.1:{PORT}/", data=json.dumps({"cmd": cmd, "arg": arg}).encode(),
                                 method="POST")
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read() or b"{}")


def sweep(dest: Path) -> None:
    subprocess.run(["dotnet", "build", str(REPO / "Port/src/CosmicShore.Player"), "-v", "q"], check=True)
    dest.mkdir(parents=True, exist_ok=True)
    log = open(dest / "player.log", "w")
    proc = subprocess.Popen(["xvfb-run", "-a", "-s", "-screen 0 1920x1080x24", str(PLAYER), "--scene", "Menu_Main",
                             "--control-port", str(PORT)], stdout=log, stderr=subprocess.STDOUT, cwd=REPO)
    try:
        for _ in range(120):
            time.sleep(2)
            try:
                if post("state", timeout=5).get("scene") == "Menu_Main":
                    break
            except OSError:
                pass
        else:
            raise SystemExit("the player never answered on its control port (see player.log)")
        time.sleep(20)  # let the menu build its screens
        say("sweeping Menu_Main (each screen and modal) ...")
        post("ui_sweep", f"{dest};1920x1080;", timeout=3600)
    finally:
        try:
            post("quit", timeout=5)
        except OSError:
            pass
        time.sleep(3)
        proc.kill()
    n = len(list((dest / "ui").glob("*.jsonl")))
    if not n:
        raise SystemExit("the sweep wrote no layouts")
    say(f"swept {n} views into {dest}")


# -- the atlas records

def load_records(out: Path) -> dict:
    recs = {}
    for f in sorted((out / "shards").glob("*")):
        raw = f.read_bytes()
        if f.name.endswith(".txt"):
            raw = base64.b64decode(raw)
        if raw[:2] == b"\x1f\x8b":
            raw = gzip.decompress(raw)
        recs.update(json.loads(raw))
    if not recs:
        raise SystemExit(f"no Code Atlas records in {out}: run code_atlas.py build first")
    return recs


# -- screens

def keep_row(view: str, r: dict) -> bool:
    """A row the window draws for this view: on screen, visible, and the view's own (a menu leaves the modals out,
    a modal is only its own window)."""
    if r["x1"] <= 0 or r["x0"] >= 1920 or r["y1"] <= 0 or r["y0"] >= 1080:
        return False
    if r["x1"] - r["x0"] < 2 or r["y1"] - r["y0"] < 2 or r.get("alpha", 1) < 0.05:
        return False
    if r["x1"] - r["x0"] >= 1919 and r["y1"] - r["y0"] >= 1079 and r["kind"] in ("image", "rawimage"):
        return False  # full-screen backdrops say nothing about the layout
    in_modal = "/ModalWindows/" in r["path"]
    if view.startswith("menu-"):
        return not in_modal
    want = norm(view.split("-", 1)[1])
    segs = r["path"].split("/")
    i = segs.index("ModalWindows") if "ModalWindows" in segs else -1
    return in_modal and i + 1 < len(segs) and want in norm(segs[i + 1])


def norm(s: str) -> str:
    return re.sub(r"[^a-z]", "", s.lower()).replace("modal", "").replace("view", "")


KIND = {"image": "i", "rawimage": "i", "text": "t", "none": "n"}


def screen_rows(view: str, path: Path) -> list[dict]:
    rows = [json.loads(line) for line in path.read_text().splitlines() if line.strip()]
    return [r for r in rows if keep_row(view, r)]


class Matcher:
    """Finds the atlas GameObject a sweep path names, walking the scene's tree and into each prefab it instances."""

    def __init__(self, recs: dict):
        self.recs = recs
        self.kids = collections.defaultdict(lambda: collections.defaultdict(list))
        self.roots = collections.defaultdict(list)  # name -> root GameObjects of scene and prefabs
        self.any = collections.defaultdict(list)  # name -> every GameObject of the scene and its prefabs
        files = {SCENE}
        todo = [SCENE]
        while todo:  # the scene and every prefab it instances, nested
            f = todo.pop()
            for c in recs.get("f:" + f, {}).get("c", []):
                r = recs.get(c, {})
                for src in r.get("r", {}).get("source", []):
                    p = src[2:] if src.startswith("f:") else src.split("#")[0][2:]
                    if p not in files:
                        files.add(p)
                        todo.append(p)
        self.files = files
        for f in files:
            for c in recs.get("f:" + f, {}).get("c", []):
                r = recs.get(c, {})
                if r.get("k") != "go":
                    continue
                self.roots[r["n"]].append(c)
                self.any[r["n"]].append(c)
                self._index(c)
        for f in files:  # a prefab is also known by its file name
            if f.endswith(".prefab"):
                gos = [c for c in recs.get("f:" + f, {}).get("c", []) if recs.get(c, {}).get("k") == "go"]
                if gos:
                    self.roots[Path(f).stem].insert(0, gos[0])

    def _index(self, gid: str) -> None:
        stack = [gid]
        while stack:
            g = stack.pop()
            for c in self.recs.get(g, {}).get("c", []):
                r = self.recs.get(c, {})
                if r.get("k") == "go":
                    self.kids[g][r["n"]].append(c)
                    self.any[r["n"]].append(c)
                    stack.append(c)

    def under(self, nid: str, anc: str) -> bool:
        seen = 0
        while nid and seen < 64:
            if nid == anc:
                return True
            nid, seen = self.recs.get(nid, {}).get("p"), seen + 1
        return False

    def find(self, path: str) -> str | None:
        """The deepest GameObject the path names. A name is taken as a child of the last match, a root of the scene or
        one of its prefabs, or (where the atlas lost the nesting, as under prefab instances) the one GameObject of
        that name below the last match, or the only one anywhere. An ambiguous name is skipped, never guessed."""
        cur, hit = None, None
        for seg in path.split("/"):
            cand = (self.kids[cur].get(seg) if cur else None) or self.roots.get(seg)
            if not cand:
                many = self.any.get(seg, [])
                below = [c for c in many if cur and self.under(c, cur)]
                cand = below[:1] if len(below) == 1 else many[:1] if len(many) == 1 else None
            if cand:
                cur = hit = cand[0]
        return hit


def screen_title(view: str, rows: list[dict]) -> str:
    """The screen the rows really show (a sweep can name a view after the button it pressed, not what came up)."""
    if view.startswith("menu-"):
        segs = collections.Counter()
        for r in rows:
            p = r["path"].split("/")
            if "Screens" in p and p.index("Screens") + 1 < len(p):
                segs[p[p.index("Screens") + 1]] += 1
        if segs:
            name = segs.most_common(1)[0][0]
            name = re.sub(r"(Screen|View)$", "", name).strip()
            return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", name) + " screen"
    return view.split("-", 1)[1].replace("_", " ").capitalize() + (" screen" if view.startswith("menu-") else "")


# -- models

def fbx_guids() -> dict[str, str]:
    out = {}
    for meta in (REPO / "Assets").rglob("*.fbx.meta"):
        rel = meta.relative_to(REPO).as_posix()[:-5]
        if any(t in "/" + rel for t in THIRD_PARTY):
            continue
        m = re.search(r"guid: ([0-9a-f]{32})", meta.read_text(errors="ignore")[:3000])
        if m:
            out[m.group(1)] = rel
    return out


DOC = re.compile(r"^--- !u!(\d+) &(-?\d+)", re.M)


def mesh_users(guids: dict[str, str]) -> dict[str, collections.Counter]:
    """Object id -> the FBX models it references (a MeshFilter's mesh, a SkinnedMeshRenderer's, a model prefab)."""
    users = collections.defaultdict(collections.Counter)
    for ext in ("*.prefab", "*.unity"):
        for p in (REPO / "Assets").rglob(ext):
            rel = p.relative_to(REPO).as_posix()
            if any(t in "/" + rel for t in THIRD_PARTY):
                continue
            text = p.read_text(errors="ignore")
            if not any(g in text for g in guids):
                continue
            starts = [(m.start(), m.group(2)) for m in DOC.finditer(text)]
            for i, (at, fid) in enumerate(starts):
                body = text[at:starts[i + 1][0] if i + 1 < len(starts) else len(text)]
                for g in re.findall(r"guid: ([0-9a-f]{32})", body):
                    if g in guids:
                        users[f"o:{rel}#{fid}"][guids[g]] += 1
                        go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
                        if go:
                            users[f"o:{rel}#{go.group(1)}"][guids[g]] += 1
                        users[f"f:{rel}"][guids[g]] += 1
    return users


def render_models(models: list[str], dest: Path) -> dict[str, dict]:
    from PIL import Image
    if not CS_ASSET.exists():
        subprocess.run(["dotnet", "build", str(REPO / "Port/src/CosmicShore.AssetTool"), "-v", "q"], check=True)
    dest.mkdir(parents=True, exist_ok=True)
    done = {}
    for i, m in enumerate(sorted(models)):
        png = dest / f"{i:03d}.png"
        r = subprocess.run([str(CS_ASSET), "model-preview", m, "--out", str(png), "--size", str(SIZE),
                            "--turntable", str(FRAMES)], cwd=REPO, capture_output=True, text=True)
        if r.returncode or not png.exists():
            say(f"  no picture for {m}: {(r.stderr or r.stdout).strip().splitlines()[-1:]}")
            continue
        info = json.loads(r.stdout.strip().splitlines()[-1])
        Image.open(png).save(dest / f"{i:03d}.webp", "WEBP", quality=82, method=6)
        png.unlink()
        done[m] = {"k": "model", "n": Path(m).stem, "path": m, "src": f"m/{i:03d}.webp", "frames": info["frames"],
                   "cols": info["cols"], "size": SIZE}
    say(f"rendered {len(done)} of {len(models)} models")
    return done


# -- the build

def build(out: Path, sweep_dir: Path) -> None:
    recs = load_records(out)
    vdir = out / "views"
    shutil.rmtree(vdir, ignore_errors=True)
    (vdir / "ui").mkdir(parents=True)
    views: list[dict] = []
    node_views: dict[str, list] = collections.defaultdict(list)

    def give(nid: str, entry) -> None:
        lst = node_views[nid]
        if nid in recs and len(lst) < MAX_VIEWS and all(e[0] != entry[0] for e in lst):
            lst.append(entry)

    # Screens, home first so a node shared by every screen (the nav bar) opens on home.
    files = sorted((sweep_dir / "ui").glob("*_1920x1080.jsonl"),
                   key=lambda p: (not p.name.startswith("menu-home"), not p.name.startswith("menu-"), p.name))
    if not files:
        say(f"no sweep in {sweep_dir} (code_atlas_views.py sweep): screens left out")
    match = Matcher(recs) if files else None
    seen_layouts = set()
    for f in files:
        view = f.name.split("_1920x1080")[0]
        rows = screen_rows(view, f)
        key = json.dumps([(r["path"], r["x0"], r["y0"]) for r in rows])
        if not rows or key in seen_layouts:
            continue  # the sweep caught a screen mid-transition and recorded the one before it again
        seen_layouts.add(key)
        vi = len(views)
        out_rows, hits = [], collections.Counter()
        for j, r in enumerate(rows):
            nid = match.find(r["path"])
            out_rows.append([round(r["x0"]), round(1080 - r["y1"]), round(r["x1"] - r["x0"]), round(r["y1"] - r["y0"]),
                             KIND.get(r["kind"], "g"), (r.get("text") or "")[:60], r["path"].rsplit("/", 1)[-1]])
            if nid:
                give(nid, [vi, j])
                hits[nid] += 1
        (vdir / "ui" / f"{view}.json").write_text(json.dumps({"w": 1920, "h": 1080, "rows": out_rows},
                                                             separators=(",", ":"), ensure_ascii=False))
        title = screen_title(view, rows)
        views.append({"k": "ui", "n": title, "src": f"ui/{view}.json", "rows": len(out_rows)})
        say(f"  {view}: {len(out_rows)} rects, {len(hits)} matched to atlas nodes")

    # Models.
    guids = fbx_guids()
    users = mesh_users(guids)
    used = sorted({m for c in users.values() for m in c} | set(guids.values()))
    pics = render_models(used, vdir / "m")
    mi = {}
    for m, v in pics.items():
        mi[m] = len(views)
        views.append(v)
        give("f:" + m, [mi[m]])
    for oid, c in users.items():
        for m, _ in c.most_common(MAX_VIEWS):
            if m in mi:
                give(oid, [mi[m]])

    # Types whose components sit on a node with a view; then up the tree and out to the loops.
    for nid in list(node_views):
        r = recs.get(nid, {})
        for c in r.get("c", []):
            for t in recs.get(c, {}).get("r", {}).get("script", []):
                for e in node_views[nid][:1]:
                    give(t, [e[0]] + e[1:])
    up = collections.defaultdict(list)
    for nid in list(node_views):
        p = recs.get(nid, {}).get("p")
        seen = set()
        while p and p not in seen:
            seen.add(p)
            up[p].append(nid)
            p = recs.get(p, {}).get("p")
    for p, below in up.items():  # a parent borrows its children's views, one from each in turn
        if len(node_views.get(p, [])) >= MAX_VIEWS:
            continue
        for e in sorted((node_views[b][0] for b in below), key=lambda e: (views[e[0]]["k"] != "ui", e[0])):
            give(p, [e[0]])
    for lid, r in recs.items():
        if r.get("k") == "loop":
            for cid in r.get("r", {}).get("code", []):
                for e in node_views.get(cid, [])[:1]:
                    give(lid, [e[0]])

    doc = {"schema": 1, "views": views, "nodes": {k: v for k, v in sorted(node_views.items()) if v}}
    (vdir / "views.json").write_text(json.dumps(doc, separators=(",", ":"), ensure_ascii=False))
    idx = out / "index.json"
    j = json.loads(idx.read_text())
    j["views"] = {"count": len(views), "nodes": len(node_views)}
    idx.write_text(json.dumps(j, indent=1))
    say(f"views: {len(views)} ({sum(v['k'] == 'ui' for v in views)} screens, {len(pics)} models) on "
        f"{len(node_views)} nodes")


# -- proofs

def self_test() -> None:
    row = lambda p, x0=10, y0=10, x1=200, y1=60, **k: {"path": p, "x0": x0, "y0": y0, "x1": x1, "y1": y1, "kind": "image", **k}
    ok = [
        keep_row("menu-home", row("UI/Screens Viewport/Screens/HomeScreen/Play")),
        not keep_row("menu-home", row("UI/Safe Area/ModalWindows/SettingsModal/Window")),
        not keep_row("menu-home", row("UI/Screens/HangarView/X", x0=-1500, x1=-1300)),
        not keep_row("menu-home", row("UI/Screens/HomeScreen/Hidden", alpha=0)),
        not keep_row("menu-home", row("UI/Backdrop", x0=0, y0=0, x1=1920, y1=1080)),
        keep_row("modal-settings", row("UI/Safe Area/ModalWindows/SettingsModal/Window")),
        not keep_row("modal-settings", row("UI/Safe Area/ModalWindows/ArcadeModal/Window")),
        not keep_row("modal-settings", row("UI/Screens/HomeScreen/Play")),
    ]
    recs = {
        "f:" + SCENE: {"k": "unity", "c": ["o:s#1", "o:s#9"]},
        "o:s#1": {"k": "go", "n": "UI", "c": ["o:s#2"]},
        "o:s#2": {"k": "go", "n": "Screens", "c": []},
        "o:s#9": {"k": "pi", "n": "Prefab: HomeView.prefab", "r": {"source": ["f:P/HomeView.prefab"]}},
        "f:P/HomeView.prefab": {"k": "prefab", "c": ["o:h#1"]},
        "o:h#1": {"k": "go", "n": "HomeView", "c": ["o:h#2"]},
        "o:h#2": {"k": "go", "n": "PlayButton", "c": []},
        "f:P/Other.prefab": {"k": "prefab", "c": ["o:x#1", "o:y#0"]},
        "o:y#0": {"k": "go", "n": "Z", "c": ["o:y#1"]},
        "o:x#1": {"k": "go", "n": "Lost", "c": ["o:x#2"]},
        "o:x#2": {"k": "go", "n": "Label", "p": "o:x#1", "c": []},
        "o:y#1": {"k": "go", "n": "Label", "p": "o:y#0", "c": []},
    }
    recs["o:s#9"]["r"]["source"].append("f:P/Other.prefab")
    m = Matcher(recs)
    ok += [
        m.find("UI/Screens") == "o:s#2",
        m.find("UI/Screens/HomeView/PlayButton") == "o:h#2",  # walks into the instanced prefab
        m.find("UI/Screens/HomeView/Nothing") == "o:h#1",  # the deepest node that exists
        m.find("Elsewhere/Entirely") is None,
        m.find("UI/Somewhere/Lost") == "o:x#1",  # the only GameObject of that name, nesting lost
        m.find("UI/Somewhere/Lost/Label") == "o:x#2",  # an ambiguous name, settled by the last match
        m.find("UI/Screens/Label") == "o:s#2",  # an ambiguous name with nothing to settle it is skipped
    ]
    if not all(ok):
        raise SystemExit(f"self-test FAILED: {[i for i, v in enumerate(ok) if not v]}")
    print("code_atlas_views self-test: all checks refuse what they must")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["sweep", "build", "self-test"])
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--sweep", type=Path, default=DEFAULT_SWEEP)
    a = ap.parse_args()
    if a.cmd == "sweep":
        sweep(a.sweep)
    elif a.cmd == "build":
        build(a.out, a.sweep)
    else:
        self_test()


if __name__ == "__main__":
    main()

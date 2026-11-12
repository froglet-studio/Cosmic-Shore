#!/usr/bin/env python3
"""
Proven Win sign-off page: builds the blind A/B page a person signs off from a candidate's real Unity frames and its
verdict, so nobody hand-writes HTML. It writes <candidate>/signoff/ (index.html, img/, froglet.css) and prints the
exact Artifact publish call to make.

    python Tools/Build/proven_win/signoff.py Docs/ProvenWin/candidates/<id> [--api dx12]

Needs: verdict.py has run and said PASS (refuses otherwise), reports/<api>/change_frames/ holds A_t00..B_t03 (run.py
copies them), and candidate.json has a "signoff" block:
    "signoff": {
      "heading": "Spindle lace sign-off",              # the page's name; also the <title>
      "subject": "the spindle shader (SpindleGraph)",  # "Two builds of <subject>, rendered by ..."
      "whatChanged": "One sentence a non-programmer understands.",
      "changeName": "6-cell Voronoi",                  # how the reveal names B
      "frameNote": "Material and mesh, and anything the frames leave out (an overlay, a tint).",
      "notMeasured": ["A phone GPU.", "Its share of a real frame."]
    }
"""
import argparse
import html
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
API_NAMES = {"dx12": "Direct3D 12", "vulkan": "Vulkan", "d3d11": "Direct3D 11", "editor": "Editor"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("candidate")
    ap.add_argument("--api", default=None, help="whose frames to show (default: dx12, else the first measured)")
    args = ap.parse_args()
    cd = os.path.abspath(args.candidate)
    cand = json.load(open(os.path.join(cd, "candidate.json"), encoding="utf-8"))
    vpath = os.path.join(cd, "verdict.json")
    if not os.path.exists(vpath):
        sys.exit("no verdict.json: run verdict.py first")
    verdict = json.load(open(vpath, encoding="utf-8"))
    if verdict["verdict"] != "PASS":
        sys.exit(f"verdict is {verdict['verdict']}: only a PASS gets a sign-off page. Reasons: {verdict['reasons']}")
    so = cand.get("signoff") or sys.exit('candidate.json has no "signoff" block (see this script\'s docstring)')
    rows = verdict["rows"]
    api = args.api or ("dx12" if "dx12" in rows else next(iter(rows)))
    frames = os.path.join(cd, "reports", api, "change_frames")
    if not os.path.isdir(frames):
        sys.exit(f"no frames at {frames}: run.py copies them from the change role")

    from PIL import Image  # pip install pillow
    out = os.path.join(cd, "signoff")
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(os.path.join(out, "img"))
    moments = 0
    for t in range(4):
        a, b = os.path.join(frames, f"A_t{t:02d}.png"), os.path.join(frames, f"B_t{t:02d}.png")
        if not (os.path.exists(a) and os.path.exists(b)):
            break
        ia, ib = Image.open(a).convert("RGB"), Image.open(b).convert("RGB")
        w, h = ia.size
        # The 1:1 crop is centred on whatever is drawn (the bounding box of non-black pixels), not the screen centre.
        box = ia.point(lambda v: 255 if v > 8 else 0).getbbox() or (0, 0, w, h)
        cx, cy = (box[0] + box[2]) // 2, (box[1] + box[3]) // 2
        x0, y0 = max(0, min(w - 640, cx - 320)), max(0, min(h - 640, cy - 320))
        for tag, im in (("A", ia), ("B", ib)):
            im.resize((1280, int(1280 * h / w)), Image.LANCZOS).save(os.path.join(out, "img", f"{tag}{t}.png"), optimize=True)
            im.crop((x0, y0, x0 + 640, y0 + 640)).save(os.path.join(out, "img", f"{tag}{t}c.png"), optimize=True)
        moments += 1
    if moments == 0:
        sys.exit("no A_t00/B_t00 frames found")

    results = []
    for k, r in rows.items():
        identical = r["tier"] if r["px8"] == 0 else f"{r['px8']} px, at most {r.get('levels')} level"
        results.append(dict(api=API_NAMES.get(k, k), a=r["a"], b=r["b"], ratio=r["ratio"],
                            won=f"{round(100 * r['share'])} %", diff8=identical))
    spec = cand.get("spec", {})
    fill = {
        "TITLE": html.escape(so["heading"]),
        "HEADING": html.escape(so["heading"]),
        "SUBJECT": html.escape(so["subject"]),
        "RENDERER": "Unity's own renderer on " + html.escape(cand.get("machine", "the bench machine's GPU")),
        "FRAME_NOTE": html.escape(so.get("frameNote", "")),
        "WHAT_CHANGED": html.escape(so["whatChanged"]),
        "MEASUREMENT_NOTE": html.escape(
            f"Milliseconds are per batch of {spec.get('drawsPerBatch', '?')} draws at {spec.get('width', '?')} x {spec.get('height', '?')}, "
            f"timed in interleaved A/B pairs. Controls: an unchanged copy read about 1.00x, and a deliberately wrong version "
            f"was caught by the pixel comparison. Verdict: {verdict['verdict']}."),
        "NOT_MEASURED_ITEMS": "".join(f"<li>{html.escape(x)}</li>" for x in so.get("notMeasured", [])),
        "TEST_ID": cand["id"],
        "RESULTS_JSON": json.dumps(results),
        "MOMENTS": str(moments),
        "CHANGE_NAME": html.escape(so["changeName"]),
    }
    page = open(os.path.join(HERE, "signoff_template.html"), encoding="utf-8").read()
    for k, v in fill.items():
        page = page.replace("{{" + k + "}}", v)
    left = [p for p in fill if "{{" + p + "}}" in page]
    if left:
        sys.exit(f"template placeholders left unfilled: {left}")
    open(os.path.join(out, "index.html"), "w", encoding="utf-8").write(page)
    shutil.copy(os.path.join(ROOT, "Docs", "UI", "froglet.css"), os.path.join(out, "froglet.css"))
    files = ["froglet.css"] + sorted("img/" + f for f in os.listdir(os.path.join(out, "img")))
    print(f"wrote {out}\n")
    print("Now publish it with the Artifact tool, exactly:")
    print(json.dumps({
        "file_path": os.path.join(out, "index.html"),
        "root": out,
        "files": files,
        "capabilities": {"db": {"rules": [{"path": "votes", "read": "view", "write": "owner"},
                                          {"path": "votes/{self}", "write": "interact"}]}, "user": {}},
        "icon": "chart",
        "description": f"Blind A/B sign-off: {so['whatChanged']}"[:300],
    }, indent=1))
    print("\n(The Artifact tool only publishes files under the working directory or the scratchpad: if it refuses, "
          "copy the signoff folder into the scratchpad first and pass that path.)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

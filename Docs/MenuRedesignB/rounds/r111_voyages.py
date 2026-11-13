"""Rev 111 - the Voyages map, with full screen in one board (Frogma, Voyages tab).

Shombith, 2026-11-14, on Rev 108 Full: "Remove the other variations on the board and craete the 109 full and on
clicking full screen the animation too on full screen I do not like the border there should be any border and the
view of zoom should be inside th map and there hsould be a back from full screen button".

  Full   one interactive board. It opens as the Rev 108 table (Rev 100 P1A's 50 degree table on the moving sea, its
         front edge the side view, drag to pan, wheel to zoom). "Full screen" animates the table up and out: it lies
         flat and grows to the whole screen, with no border; the zoom ladder rides inside the map's top-left corner,
         the crew becomes a column of portraits on the right, the picked voyage sits at the foot, and two deeper tiers
         open (Neighbourhood 20 ly and Cell 5 ly, the Voyage Board's foam from the voyages branch). "Back from full
         screen" animates it home.

(Rev 109 was taken on the canvas by another round, so this is Rev 111, the next free number.)

Run: python3 Docs/MenuRedesignB/rounds/r111_voyages.py <publish root> [rev]   (writes <root>/project/R<rev>Full.dc.html)
"""
import importlib.util
import json
import sys
from pathlib import Path

_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "111"
_spec = importlib.util.spec_from_file_location("r108_voyages", Path(__file__).with_name("r108_voyages.py"))
r108 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, "108"]
_spec.loader.exec_module(r108)
r100 = r108.r100
REV = int(_rev)
ROOT = r100.ROOT

EASE = "cubic-bezier(.65,0,.25,1)"
CSS = f"""
.fsx{{transition:left .9s {EASE},top .9s {EASE},width .9s {EASE},height .9s {EASE}}}
.fsx .slab{{transition:left .9s {EASE},top .9s {EASE},width .9s {EASE},height .9s {EASE},transform .9s {EASE}}}
.fsx .clip{{transition:box-shadow .6s ease}}
.fsx .front{{transition:opacity .45s ease}}
.zrow{{transition:top .9s {EASE}}}
.late{{animation:rise .6s cubic-bezier(.2,.7,.2,1) .55s both}}
"""
r100.HELMET = r100.HELMET.replace("</style>", CSS + "</style>")

FS = r"""
  // Full screen: the table lies flat and grows to the whole screen (no border); two deeper tiers open.
  const F = !!st.fs;
  const fs = { on: F, off: !F,
    // One value per hole: Frogma sets each style property on its own, so a hole may not hold several declarations.
    cl: F ? '0px' : '220px', ct: F ? '0px' : '236px', cw: F ? '1920px' : '1080px', ch: F ? '1080px' : '680px', cz: F ? 6 : 2,
    sl: F ? '0px' : '90px', sw: F ? '1920px' : '900px', sh: F ? '1080px' : '540px', stf: F ? 'rotateX(0deg)' : 'rotateX(50deg)',
    rim: F ? 'none' : '0 0 0 2px rgba(116,123,173,.5),0 40px 120px rgba(0,0,0,.7)',
    face: F ? 0 : 1, zy: F ? '40px' : '172px',
    go: () => this.setState({ fs: true, drag: null }),
    back: () => this.setState({ fs: false, drag: null, z: st.z > 3 ? 2 : st.z }) };
  map.upr = F ? 'none' : 'rotateX(-50deg)';
  // The crew: portraits always at the right edge; their cards slide out only when asked for.
  const D = !!st.dr;
  const dr = { on: D, sy: F ? '150px' : '236px', ty: F ? '100px' : '176px',
    pressed: D ? 'true' : 'false', label: D ? 'Hide' : 'Crew', rot: D ? 'rotate(180deg)' : 'none',
    kl: F ? '40px' : '900px', kt: F ? '920px' : '800px', kw: F ? '1180px' : '860px',
    toggle: () => this.setState({ dr: !st.dr }) };
  // Pan: the map sits in a 130% sheet the drag slides (at most 15% each way); pins and Tumult follow it.
  const K = 1.3, lim = 15, ease = 'left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1),transform .9s cubic-bezier(.65,0,.25,1)';
  const clamp = (x) => Math.max(-lim, Math.min(lim, x));
  const panAt = (q) => { const L = -15 + st.px + parseFloat(q.left) * K, T = -15 + st.py + parseFloat(q.top) * K;
    q.left = L.toFixed(2) + '%'; q.top = T.toFixed(2) + '%'; return L > 1 && L < 99 && T > 2 && T < 98; };
  pins.forEach((q) => { if (!panAt(q)) q.vis = 'none'; q.tr = st.drag ? 'none' : ease; if (F) q.stalk = '0px'; });
  panAt(home); home.tr = st.drag ? 'none' : ease;
  if (z > 3) { home.left = '-50%'; home.top = '-50%'; }
  map.panL = (-15 + st.px).toFixed(2) + '%'; map.panT = (-15 + st.py).toFixed(2) + '%';
  map.ptr = st.drag ? 'none' : 'left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1)';
  map.grab = st.drag ? 'grabbing' : 'grab';
  const DX = F ? 19.2 : 9, DY = F ? 10.8 : 4.2;
  map.down = (e) => this.setState({ drag: { x: e.clientX, y: e.clientY, px: st.px, py: st.py } });
  map.move = (e) => { const g = st.drag; if (!g) return;
    this.setState({ px: clamp(g.px + (e.clientX - g.x) / DX), py: clamp(g.py + (e.clientY - g.y) / DY) }); };
  map.up = () => { if (st.drag) this.setState({ drag: null }); };
"""


def full_board():
    move = "transition:left .8s cubic-bezier(.2,.7,.2,1),top .8s cubic-bezier(.2,.7,.2,1)"
    beacons = (r100.beacon("transform:{{map.upr}}")
               .replace("border:2px solid {{p.ring}};", "border:2px solid {{p.ring}};animation:{{p.anim}};")
               .replace(move, "transition:{{p.tr}}"))
    home = r100.home_mark().replace(move, "transition:{{home.tr}}")
    table = f"""<div class="abs fsx" onWheel="{{{{map.wheel}}}}" onMouseDown="{{{{map.down}}}}" onMouseMove="{{{{map.move}}}}" onMouseUp="{{{{map.up}}}}" onMouseLeave="{{{{map.up}}}}" style="left:{{{{fs.cl}}}};top:{{{{fs.ct}}}};width:{{{{fs.cw}}}};height:{{{{fs.ch}}}};z-index:{{{{fs.cz}}}};perspective:1600px;perspective-origin:50% 10%;cursor:{{{{map.grab}}}}">
<div class="slab" style="position:absolute;left:{{{{fs.sl}}}};top:0;width:{{{{fs.sw}}}};height:{{{{fs.sh}}}};transform:{{{{fs.stf}}}};transform-style:preserve-3d;transform-origin:50% 0%">
<div class="clip" style="position:absolute;inset:0;overflow:hidden;background:#00010A;box-shadow:{{{{fs.rim}}}}">
<div style="position:absolute;left:{{{{map.panL}}}};top:{{{{map.panT}}}};width:130%;height:130%;transition:{{{{map.ptr}}}}">{r100.layers()}</div></div>
<div style="position:absolute;inset:0;transform-style:preserve-3d">{home}{beacons}</div>
<figure class="front" style="position:absolute;left:0;top:100%;width:100%;height:70px;margin:0;transform-origin:50% 0;transform:rotateX(-50deg);overflow:hidden;background:#00010A;opacity:{{{{fs.face}}}};box-shadow:0 0 0 2px rgba(116,123,173,.5),0 40px 80px rgba(0,0,0,.6)">
<div style="position:absolute;top:0;bottom:0;left:{{{{map.panL}}}};width:130%;transition:{{{{map.ptr}}}}">
<img src="{{{{voyage.edge}}}}" alt="The course seen edge on: the table's front, sliding with the map" style="width:100%;height:100%;object-fit:cover;object-position:40% 78%"></div></figure>
</div></div>"""
    zrow = (r108.zoom_row(40, 172).replace("width:70px", "min-width:70px")
            .replace('class="abs" style="left:40px;top:172px;', 'class="abs zrow" style="left:40px;top:{{fs.zy}};', 1)
            .replace("z-index:3", "z-index:7", 1))
    assert "zrow" in zrow
    go_full = ('<sc-if value="{{fs.off}}" hint-placeholder-val="{{ true }}"><button class="btn sm soft abs" type="button" onClick="{{fs.go}}" '
               f'style="left:1028px;top:172px;width:220px;gap:12px;z-index:3">{r108.EXPAND}Full screen</button></sc-if>')
    back_full = ('<sc-if value="{{fs.on}}" hint-placeholder-val="{{ false }}"><button class="btn sm soft abs late" type="button" onClick="{{fs.back}}" '
                 f'style="right:40px;top:40px;width:320px;gap:12px;z-index:7"><span class="kc">B</span>Back from full screen</button></sc-if>')
    chev = ('<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true" '
            'style="transform:{{dr.rot}};transition:transform .4s ease"><path d="M9 2L4 7l5 5"></path></svg>')
    toggle = ('<button class="pick abs" type="button" aria-expanded="{{dr.pressed}}" aria-label="Your crew recommends: show the details" onClick="{{dr.toggle}}" '
              'style="left:1780px;top:{{dr.ty}};width:100px;height:40px;display:flex;align-items:center;justify-content:center;gap:8px;z-index:8;'
              'font-size:14px;letter-spacing:.12em;text-transform:uppercase;color:#E6E9FF;background:rgba(0,4,31,.86);box-shadow:inset 0 0 0 1px #434C89;transition:top .9s cubic-bezier(.65,0,.25,1)">'
              + chev + '<span>{{dr.label}}</span></button>')
    strip = (r108.crew_strip(1780, 236).replace("top:236px;", "top:{{dr.sy}};transition:top .9s cubic-bezier(.65,0,.25,1);", 1)
             .replace("z-index:3", "z-index:7", 1))
    # The cards slide out only when the crew toggle is pressed (closed is the default view).
    drawer = ('<sc-if value="{{dr.on}}" hint-placeholder-val="{{ false }}">'
              + r100.rec_list(1250, 236, 500, card_h=112, gap=8)
              .replace("top:236px;", "top:{{dr.sy}};", 1)
              .replace('class="abs"', 'class="abs drawin"', 1)
              .replace("z-index:3\"", "z-index:8;padding:18px;box-sizing:border-box;background:rgba(0,4,31,.94);box-shadow:inset 0 0 0 1px #434C89\"", 1)
              + '</sc-if>')
    assert "drawin" in drawer and "dr.sy" in strip
    # One picked-voyage card for both views: under the table beside the crew, or along the foot of the full screen.
    card = (r108.picked_card(0, 0, 100)
            .replace("left:0px;top:0px;width:100px;", "left:{{dr.kl}};top:{{dr.kt}};width:{{dr.kw}};transition:left .9s cubic-bezier(.65,0,.25,1),top .9s cubic-bezier(.65,0,.25,1),width .9s cubic-bezier(.65,0,.25,1);", 1)
            .replace("z-index:3", "z-index:7", 1))
    assert "dr.kl" in card
    body = (table + zrow + go_full + card + back_full + strip + toggle + drawer)
    html = r100.window(1, body, f"Rev {REV} Full - the table opens to full screen (animated, no border, zoom inside the map, back from full screen), Galaxy down to one Cell - interactive",
                       ground=r108.SEA)
    html = html.replace("ring: i === st.sel ? '#00D4FF'", "anim: i === st.sel ? 'breathe 2.8s ease-in-out infinite' : 'none', ring: i === st.sel ? '#00D4FF'")
    deep = json.dumps(r108.DEEP)[1:-1]
    for a, b in (("t: 0 }; }", "t: 0, px: 0, py: 0, drag: null, fs: false, dr: false }; }"),
                 ('["Course", "50 ly", "", ""]]', '["Course", "50 ly", "", ""], ' + deep + ']'),
                 ("Math.min(3, z)) }); }", "Math.min(this.state.fs ? 5 : 3, z)) }); }"),
                 ("Math.min(3, z + (e.deltaY", "Math.min(st.fs ? 5 : 3, z + (e.deltaY"),
                 ("if (z === 3) return null;", "if (z >= 3) return null;"),
                 ("vis: (z === 3 && i !== st.sel)", "vis: (z > 3 || (z === 3 && i !== st.sel))"),
                 ("z: z === 3 ? 3 : 2 })", "z: z >= 3 ? z : 2 })"),
                 ("const ladder = TIERS.map(", "const ladderAll = TIERS.map("),
                 ("pick: () => this.go(i) })).reverse();", "pick: () => this.go(i) })).reverse();\n  const ladder = st.fs ? ladderAll : ladderAll.slice(2);"),
                 ("  const pips = (n) =>", FS.strip("\n") + "\n  const pips = (n) =>"),
                 ("  return { map, layers,", "  const pick = recs[st.sel];\n  return { dr, fs, pick, map, layers,")):
        assert html.count(a) >= 1, a
        html = html.replace(a, b, 1)
    return html


BOARDS = {f"R{REV}Full.dc.html": full_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)

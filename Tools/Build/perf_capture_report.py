#!/usr/bin/env python3
"""
Read one or more `capture_*.json` bundles (the HUD's `capture` command - Docs/PERFORMANCE_OPTIMIZATION.md
§4.6) and print the judgement a performance session needs, in the order it needs it:

  1. Is the measurement VALID?   Code Optimization, Deep Profile, vsync/frame cap, focus, Burst,
                                 managed job time, Profiler recording, stall notes.
  2. What is the frame?          fps, CPU busy vs GPU, p50/p95/p99, bound verdict, GC KB/frame,
                                 prism path and count, draws.
  3. What is it made of?         per-system markers (diag), Profiler top self-time, top allocators,
                                 the spike frame's top rows, thread waits, managed jobs.
  4. What is in the world?       renderer census, cell counts, burst report, console warnings/errors.

    python3 Tools/Build/perf_capture_report.py capture_S2.json
    python3 Tools/Build/perf_capture_report.py before.json after.json      # the second column is the delta
    python3 Tools/Build/perf_capture_report.py --json capture.json          # machine-readable summary
    python3 Tools/Build/perf_capture_report.py --self-test

A bundle pasted from the clipboard can be saved to a file first; this script reads the file.
"""
import argparse, json, sys



def gc_owner(row):
    """A 'GC.Alloc' row names the CALLER: the nearest path element that is not GC.Alloc, the Profiler's own
    [Invoke] / dll decoration stripped ('VesselAnimation.Update()' from '...Gameplay::VesselAnimation.Update()
    [Invoke] / GC.Alloc'). The bare name 'GC.Alloc' told the reader nothing (first shared capture, 2026-10-16)."""
    if row.get("name") != "GC.Alloc":
        return row.get("name", "?")
    parts = [x.strip() for x in (row.get("path") or "").split(" / ") if x.strip() and x.strip() != "GC.Alloc"]
    if not parts:
        return "GC.Alloc"
    owner = parts[-1].replace(" [Invoke]", "").replace(" [Coroutine: MoveNext]", " (coroutine)")
    owner = owner.split("::")[-1] if "::" in owner else owner
    return owner


def _pct(row):
    pct = row.get("presentPct")
    return f"in {pct:.0f}% of frames" if isinstance(pct, (int, float)) else ""

def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def validity(b):
    """Reasons this capture cannot be trusted as a Release number, in severity order."""
    out = []
    d = b.get("diag") or {}
    env = d.get("environment") or {}
    p = b.get("prof") or {}
    # Editor: "Debug" / "Release"; a player reports "Player" (its optimisation is the build's).
    if env.get("codeOptimization") and "debug" in env["codeOptimization"].lower():
        out.append(f"ERROR  Code Optimization is '{env['codeOptimization']}' - C# runs ~5x slower; switch to Release (bug icon, bottom right) and re-capture")
    if p.get("deepProfiling"):
        out.append("ERROR  Deep Profile was ON - every managed call is timed; script costs are inflated several-fold")
    if d.get("frameCapVSync", 0) or d.get("frameCapTarget", 0) not in (0, -1, None):
        out.append(f"WARN   frame cap active (vsync {d.get('frameCapVSync')}, target {d.get('frameCapTarget')}) - fps and frame ms are capped; use CPU busy ms, or 'fps uncap'")
    if env.get("focused") is False:
        out.append("WARN   the Game view was not focused - the editor throttles an unfocused player")
    if env.get("burstEnabled") is False:
        out.append("ERROR  Burst is disabled - every job runs managed")
    if env.get("jobCompilerEnabled") is False:
        out.append("WARN   JobsUtility.JobCompilerEnabled is false")
    if env.get("debuggerAttached"):
        out.append("WARN   a debugger is attached")
    mj = p.get("managedJobMs")
    if mj and mj > 0.2:
        rows = ", ".join(f"{r.get('caller','?')} {r.get('avgMs',0):.2f} ms" for r in (p.get("managedJobs") or [])[:4])
        out.append(f"ERROR  {mj:.2f} ms/frame of Burst jobs ran as MANAGED code ({rows}) - the Burst fix is not in effect; read the 'burst' section")
    for n in (p.get("notes") or []):
        out.append("NOTE   prof: " + n)
    for n in (b.get("notes") or []):
        out.append("NOTE   capture: " + n)
    if not d:
        out.append("ERROR  no diag in this bundle")
    if not p:
        out.append("WARN   no prof in this bundle (Editor only; or the Profiler window was closed)")
    cams = b.get("cameras") or ""
    try:
        n = int(cams.split(" ")[0]) if cams else 0
    except ValueError:
        n = 0
    if n > 1:
        out.append(f"WARN   {n} cameras were enabled ({cams[:120]}) - every extra camera (Third Eye, a preview, a Scene view) renders the world again; close them and re-capture to measure the game's frame")
    ed = editor_state(b)
    if ed.get("deepProfile"):
        out.append("ERROR  the Profiler was DEEP profiling - every C# call is instrumented and the frame is several times too slow; turn Deep Profile off and re-capture")
    if ed.get("profileEditor"):
        out.append("WARN   the Profiler was profiling the EDITOR too - its windows' cost lands in the frame; set the Profiler's target to Play Mode")
    if ed.get("sceneViews"):
        out.append(f"NOTE   {ed['sceneViews']} Scene view(s) open - a VISIBLE Scene view renders the world again every Play frame; hide it behind another tab or close it")
    gw, gh = ed.get("gameW", 0), ed.get("gameH", 0)
    if gw * gh > 2560 * 1440:
        out.append(f"NOTE   the Game view renders at {gw}x{gh} - more pixels than 1440p; a smaller Game view or a fixed resolution costs the GPU less")
    if "MANAGED" in (b.get("burst") or ""):
        out.append("ERROR  the burst probe job ran MANAGED - the game's jobs are not Burst-compiled; see the burst section")
    return out


def editor_state(b):
    """The capture's 'editor' line ('sceneViews 1 | gameView 3840x2160 | deepProfile 0 | ...') as numbers."""
    out = {}
    for part in (b.get("editor") or "").split("|"):
        bits = part.split()
        if len(bits) != 2:
            continue
        key, val = bits
        if key == "gameView" and "x" in val:
            w, _, h = val.partition("x")
            out["gameW"], out["gameH"] = int(w or 0), int(h or 0)
        elif val.isdigit():
            out[key] = int(val)
    return out


def frame(b):
    d = b.get("diag") or {}
    return {
        "scene": d.get("scene") or b.get("scene"), "label": d.get("label") or b.get("label"),
        "fps": d.get("avgFps"), "frameMs": d.get("avgFrameMs"), "p50": d.get("p50FrameMs"), "p95": d.get("p95FrameMs"),
        "p99": d.get("p99FrameMs"), "max": d.get("maxFrameMs"), "cpuBusy": d.get("avgCpuBusyMs"), "cpu": d.get("avgCpuMs"),
        "gpu": d.get("avgGpuMs"), "idle": d.get("idleMs"), "bound": d.get("boundVerdict"), "gcKB": d.get("avgGcKbPerFrame"),
        "draws": d.get("avgDraws"), "setPass": d.get("avgSetPass"), "tris": d.get("tris"), "prismPath": d.get("prismPath"),
        "prismEnts": d.get("prismEnts"), "frames": d.get("frames"), "allocMB": d.get("allocMB"), "reservedMB": d.get("reservedMB"),
        "spikes": len(d.get("spikes") or []), "frozen": d.get("ecologyFrozen"),
        "codeOpt": (d.get("environment") or {}).get("codeOptimization"), "gpuName": (d.get("environment") or {}).get("gpu"),
    }


def markers(b):
    rows = (b.get("diag") or {}).get("markers") or []
    rows = [m for m in rows if m.get("found") and (m.get("avgMs") or 0) > 0]
    return sorted(rows, key=lambda m: -(m.get("avgMs") or 0))


def fmt(v, f="{:.2f}"):
    return "-" if v is None else f.format(v)


def delta(a, b):
    if a is None or b is None or a == 0:
        return ""
    return f"{(b - a) / a * 100:+5.0f}%"


def print_report(bundles, top):
    a = bundles[0]
    b2 = bundles[1] if len(bundles) > 1 else None
    fa, fb = frame(a), frame(b2) if b2 else None

    print(f"== {fa['scene']} '{fa['label']}'  ({a.get('timestamp')})" + (f"   vs  '{fb['label']}' ({b2.get('timestamp')})" if fb else ""))
    print("\n-- 1. validity")
    v = validity(a)
    print("\n".join("   " + x for x in v) if v else "   OK: Release, no deep profile, no cap flagged, Burst on, no managed job time")
    if b2:
        vb = validity(b2)
        if vb:
            print("   B:")
            print("\n".join("   " + x for x in vb))

    print("\n-- 2. the frame")
    keys = [("fps", "fps", "{:.1f}"), ("frameMs", "frame ms", "{:.1f}"), ("cpuBusy", "CPU busy ms", "{:.1f}"), ("cpu", "CPU ms", "{:.1f}"),
            ("gpu", "GPU ms", "{:.1f}"), ("idle", "idle ms", "{:.1f}"), ("p50", "p50 ms", "{:.1f}"), ("p95", "p95 ms", "{:.1f}"),
            ("p99", "p99 ms", "{:.1f}"), ("max", "max ms", "{:.1f}"), ("gcKB", "GC KB/frame", "{:.1f}"), ("draws", "draws", "{:.0f}"),
            ("setPass", "setPass", "{:.0f}"), ("prismEnts", "prism entities", "{:.0f}"), ("allocMB", "managed MB", "{:.0f}"), ("spikes", "spike frames", "{:.0f}")]
    for k, name, f in keys:
        line = f"   {name:<16} {fmt(fa[k], f):>10}"
        if fb:
            line += f" {fmt(fb[k], f):>10} {delta(fa[k], fb[k]):>7}"
        print(line)
    print(f"   {'bound':<16} {fa['bound']}" + (f"   |  {fb['bound']}" if fb else ""))
    print(f"   {'prism path':<16} {fa['prismPath']}" + (f"   |  {fb['prismPath']}" if fb else ""))
    print(f"   {'code opt / gpu':<16} {fa['codeOpt']} / {fa['gpuName']}")

    print(f"\n-- 3a. per-system markers (diag; main-thread ms/frame, avg over every frame)")
    ma = markers(a)
    mb = {m["name"]: m for m in markers(b2)} if b2 else {}
    print(f"   {'marker':<44} {'avg':>7} {'p95':>7} {'max':>7} {'calls':>6}" + (f" {'B avg':>7} {'delta':>7}" if b2 else ""))
    for m in ma[:top]:
        line = f"   {m['name'][:44]:<44} {m.get('avgMs',0):7.3f} {m.get('p95Ms',0):7.2f} {m.get('maxMs',0):7.2f} {m.get('avgCalls',0):6.1f}"
        if b2:
            o = mb.get(m["name"])
            line += f" {fmt(o.get('avgMs') if o else None, '{:.3f}'):>7} {delta(m.get('avgMs'), o.get('avgMs') if o else None):>7}"
        print(line)
    missing = [m["name"] for m in ((a.get("diag") or {}).get("markers") or []) if not m.get("found")]
    if missing:
        print(f"   not found in this build: {', '.join(missing[:12])}")

    p = a.get("prof") or {}
    if p:
        pl, mt = p.get("playerLoopMs") or {}, p.get("mainThreadFrameMs") or {}
        print(f"\n-- 3b. Profiler ({p.get('framesRead')} frames{', INCOMPLETE' if not p.get('completed') else ''}): PlayerLoop avg {fmt(pl.get('avgMs'))} p50 {fmt(pl.get('p50Ms'))} p99 {fmt(pl.get('p99Ms'))} ms; main thread avg {fmt(mt.get('avgMs'))}")
        print(f"   top SELF time (ms/frame, summed by name across paths):")
        for r in [r for r in (p.get("topSelf") or []) if not r.get("editorOnly")][:top]:
            print(f"   {r['name'][:60]:<60} {r.get('avgSelfMs',0):7.3f}  x{r.get('avgCalls',0):.0f}")
        print(f"   top SELF allocation (KB/frame):")
        for r in [r for r in (p.get("topGc") or []) if not r.get("editorOnly") and (r.get("avgSelfGcKB") or 0) > 0][:12]:
            print(f"   {gc_owner(r)[:60]:<60} {r.get('avgSelfGcKB',0):7.2f} KB  {_pct(r)}")
        sp = p.get("spikeFrameTree") or []
        if sp:
            print(f"   spike frame ({fmt(p.get('spikeFrameMs'))} ms vs typical {fmt(p.get('typicalFrameMs'))}): top rows")
            for r in sorted([r for r in sp if r.get("depth", 0) >= 2], key=lambda r: -r.get("selfMs", 0))[:8]:
                print(f"     {'  '*min(r.get('depth',0),6)}{r['name'][:50]:<50} total {r.get('totalMs',0):6.2f} self {r.get('selfMs',0):6.2f}")
        th = [t for t in (p.get("threads") or []) if (t.get("avgBusyMs") or 0) > 0.5]
        if th:
            print("   threads (busy / wait ms):")
            for t in th[:10]:
                print(f"     {t['name'][:40]:<40} {t.get('avgBusyMs',0):6.2f} / {t.get('avgWaitMs',0):6.2f}  {t.get('busyPct',0):.0f}% busy")
        if p.get("managedJobs"):
            print(f"   managed job time {p.get('managedJobMs',0):.2f} ms/frame:")
            for r in p["managedJobs"][:6]:
                print(f"     {r.get('caller','?')[:60]:<60} {r.get('avgMs',0):6.2f} ms  present {r.get('presentPct',0):.0f}%")

    print("\n-- 4. the world")
    for key in ("renderers", "cells", "burst", "prismPath", "cameras", "editor"):
        text = (a.get(key) or "").strip()
        if text:
            print(f"   {key}: " + text.replace("\n", "\n      ")[:1500])
    log = a.get("log") or []
    if log:
        print(f"   console during capture ({len(log)} distinct):")
        for e in log[:15]:
            print(f"     x{e.get('count',1)} {e.get('message','')[:160]}")


def summary(b):
    return {"validity": validity(b), "frame": frame(b), "markers": markers(b)[:40],
            "topSelf": ((b.get("prof") or {}).get("topSelf") or [])[:40], "topGc": ((b.get("prof") or {}).get("topGc") or [])[:20]}


def self_test():
    good = {"kind": "perf-capture", "label": "t", "scene": "S", "burst": "Run() BURST, Schedule() BURST", "notes": [], "log": [],
            "diag": {"avgFps": 60.0, "avgCpuBusyMs": 10.0, "boundVerdict": "CPU", "frameCapVSync": 0, "frameCapTarget": -1,
                     "environment": {"codeOptimization": "Release", "burstEnabled": True, "focused": True},
                     "markers": [{"name": "A", "found": True, "avgMs": 1.0, "p95Ms": 2, "maxMs": 3, "avgCalls": 1},
                                 {"name": "B", "found": False}]},
            "prof": {"completed": True, "deepProfiling": False, "managedJobMs": 0.0, "topSelf": [], "topGc": [], "notes": []}}
    assert validity(good) == [], validity(good)
    bad = json.loads(json.dumps(good))
    bad["diag"]["environment"]["codeOptimization"] = "Debug"
    bad["prof"]["deepProfiling"] = True
    bad["prof"]["managedJobMs"] = 1.3
    bad["prof"]["managedJobs"] = [{"caller": "ShellContactQueryJob", "avgMs": 1.3}]
    v = validity(bad)
    assert any("Code Optimization" in x for x in v) and any("Deep Profile" in x for x in v) and any("MANAGED" in x for x in v), v
    two = json.loads(json.dumps(good)); two["cameras"] = "2 enabled | Main Camera screen depth 0 | ThirdEye 960x540 rt depth 1"
    assert any("cameras" in x for x in validity(two)), validity(two)
    ed = json.loads(json.dumps(good)); ed["editor"] = "sceneViews 0 | gameView 1920x1080 | deepProfile 0 | profileEditor 0 | profilerRecording 1"
    assert validity(ed) == [], validity(ed)
    ed["editor"] = "sceneViews 1 | gameView 3840x2160 | deepProfile 1 | profileEditor 1 | profilerRecording 1"
    v = validity(ed)
    assert any("DEEP" in x for x in v) and any("EDITOR too" in x for x in v) and any("Scene view" in x for x in v) \
        and any("3840x2160" in x for x in v), v
    assert markers(good)[0]["name"] == "A" and len(markers(good)) == 1
    assert delta(10, 5) == "  -50%" and delta(None, 1) == ""
    print("self-test ok")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("bundles", nargs="*", help="capture_*.json; a second one is compared against the first")
    ap.add_argument("--top", type=int, default=25)
    ap.add_argument("--json", action="store_true", help="print the first bundle's summary as JSON")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        self_test()
        return
    if not a.bundles:
        ap.error("give a capture_*.json")
    bundles = [load(p) for p in a.bundles]
    for b in bundles:
        if b.get("kind") != "perf-capture":
            print(f"warning: {b.get('kind')!r} is not a perf-capture bundle (a bare diag JSON? wrap: {{\"kind\":\"perf-capture\",\"diag\":<it>}})", file=sys.stderr)
    if a.json:
        print(json.dumps(summary(bundles[0]), indent=2))
    else:
        print_report(bundles, a.top)


if __name__ == "__main__":
    main()

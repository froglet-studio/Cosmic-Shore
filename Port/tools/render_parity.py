#!/usr/bin/env python3
"""Render parity: Amoebius renders the control take, and the pixels are scored against Unity's.

    python3 Port/tools/render_parity.py record  [--take control] [--out DIR]   # Amoebius renders the take
    python3 Port/tools/render_parity.py compare [--take control] [--golden DIR] [--actual DIR] [--every 10]
    python3 Port/tools/render_parity.py self    [--take control]               # Amoebius twice: determinism

The control take is the scene Tools/Build/author_render_parity_scene.py writes
(Assets/_Scenes/Game_TestDesign/RenderParity.unity) driven by its RenderParityTake. Unity records it with
FrogletTools > Amoebius > Render Parity (the Unity Recorder, RenderParityRecorder.cs) into
Port/parity/goldens/render/<take>/ (frames/f0000.png ..., take.mp4, take.json). `record` boots the
player on the same scene at the take's size and fps, waits for the take to start counting, and records
every frame from the warm-up into Port/parity/results/render/<take>/frames/. `compare` aligns the two
sequences on the camera's jump at take frame 0 (never on frame numbers: the two engines' first frames
differ by their boots), then scores every aligned pair:

  rmse        root mean square difference of linear RGB (0..1), the loss's main term
  luma_rmse   the same on luminance alone (shape and shading, colour aside)
  ssim        structural similarity of the luminance (1 = identical), gaussian 7x7 at half size
  tiles       a 16x9 grid of per-tile rmse: WHERE the picture is wrong

and writes report.json (per frame, the worst frames and tiles, the loss = mean rmse + 0.5 * (1 - mean
ssim)), sheet.png (unity | amoebius | 4x difference for every --every-th aligned frame) and heat.png.
A golden with only take.mp4 is decoded with ffmpeg (near-lossless, a ~0.005 rmse floor).

The loss is what the engine's rendering is iterated on: fix the worst tile's cause, re-record, compare.
"""
import argparse
import csv
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import time

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENE = "Assets/_Scenes/Game_TestDesign/RenderParity.unity"
GOLDENS = os.path.join(ROOT, "Port", "parity", "goldens", "render")
RESULTS = os.path.join(ROOT, "Port", "parity", "results", "render")
PLAYER_DLL = os.path.join(ROOT, "Port", "src", "CosmicShore.Player", "bin", "Debug", "net10.0", "CosmicShore.dll")
FFMPEG = shutil.which("ffmpeg") or "ffmpeg"


def take_settings(take):
    """fps, warm-up, frames, size, msaa: from the golden's take.json when it exists, else the scene's authored numbers."""
    p = os.path.join(GOLDENS, take, "take.json")
    if os.path.exists(p):
        return json.load(open(p))
    sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
    import author_render_parity_scene as a
    return {"fps": a.FPS, "warmupFrames": a.WARMUP, "takeFrames": a.FRAMES, "width": a.W, "height": a.H, "msaa": 4}


# ---------------------------------------------------------------- record

def record(take, out):
    sys.path.insert(0, os.path.join(ROOT, "Tools", "Build", "music_video"))
    import shoot  # the player wrapper + control port (Tools/Build/music_video/shoot.py)
    ts = take_settings(take)
    fps, warm, n, w, h = int(ts["fps"]), int(ts["warmupFrames"]), int(ts["takeFrames"]), int(ts["width"]), int(ts["height"])
    frames_dir = os.path.join(out, "frames")
    if os.path.isdir(frames_dir): shutil.rmtree(frames_dir)
    os.makedirs(frames_dir)
    log = open(os.path.join(out, "record.log"), "w")

    def L(m):
        print(m, flush=True); log.write(m + "\n"); log.flush()

    extra = ["--scene", SCENE, "--step-hz", str(fps), "--msaa", str(ts.get("msaa", 4)), "--render-scale", "1", "--render-from", "999999999"]
    p = shoot.Player(47990, "parity", os.path.join(out, "player.log"), size=f"{w}x{h}", extra=extra)
    try:
        p.wait_up()
        # The scene starts directly (no first-run prompts: the scene has no menu). Wait for the take to count.
        take_frame = None
        for _ in range(300):
            r = p.cmd("get", "[RenderParityTake] RenderParityTake FrameIndex", timeout=60)
            txt = str(r.get("output", "")) + str(r.get("value", ""))
            m = re.search(r"(-?\d+)", txt)
            if m and int(m.group(1)) > -10**8:
                take_frame = int(m.group(1)); break
            p.wait(5)
        if take_frame is None:
            raise RuntimeError("the take never started counting (is the scene's RenderParityTake alive? see player.log)")
        L(f"take frame {take_frame} now; recording from warm-up frame -{min(warm, 10)} through the take")
        # Record from 10 frames before take frame 0 (the camera's jump there aligns the sequences) to the end.
        lead = min(warm, 10)
        start_in = -take_frame - lead      # frames until the first recorded frame
        total = lead + n
        if start_in < 1:
            raise RuntimeError(f"too late: the take is already at frame {take_frame}; raise warmupFrames")
        L(p.do(f"mv in {start_in} record {frames_dir} {total} 1 1").strip())
        done = 0
        while done < start_in + total + 2:
            step = min(60, start_in + total + 2 - done)
            p.wait(step); done += step
            got = len(glob.glob(os.path.join(frames_dir, "f*.png")))
            if got and got % 60 == 0: L(f"  {got}/{total} frames")
        got = len(glob.glob(os.path.join(frames_dir, "f*.png")))
        # Measure where the take's cut (its frame 0) landed rather than trust the schedule: the
        # comparison aligns on the cut, and run.json says whether the lead came out as intended.
        _, head = load_frames(out, limit=lead + 20)
        cut = align_index(head) if len(head) > 1 else 0
        L(f"{got} frames recorded (lead {lead}, take {n}, cut at frame {cut}) -> {frames_dir}")
        if cut != lead: L(f"  warning: the cut landed on frame {cut}, not {lead}; the comparison aligns on the cut anyway")
        json.dump({"take": take, "lead": lead, "cut": cut, "takeFrames": n, "fps": fps, "width": w, "height": h, "recorded": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())},
                  open(os.path.join(out, "run.json"), "w"), indent=1)
        return got >= total - 1
    finally:
        p.quit()
        log.close()


# ---------------------------------------------------------------- compare

def load_frames(d, limit=None):
    """Sorted PNG frames of a directory as uint8 arrays; a directory with only take.mp4 is decoded first."""
    files = sorted(glob.glob(os.path.join(d, "frames", "f*.png")))
    if not files and os.path.exists(os.path.join(d, "take.mp4")):
        os.makedirs(os.path.join(d, "frames"), exist_ok=True)
        subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-i", os.path.join(d, "take.mp4"), os.path.join(d, "frames", "f%04d.png")], check=True)
        files = sorted(glob.glob(os.path.join(d, "frames", "f*.png")))
    from PIL import Image
    if limit: files = files[:limit]
    return files, [np.asarray(Image.open(f).convert("RGB")) for f in files]


def srgb_to_linear(u8):
    c = u8.astype(np.float32) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def luma(lin):
    return 0.2126 * lin[..., 0] + 0.7152 * lin[..., 1] + 0.0722 * lin[..., 2]


def align_index(frames):
    """The index of the first frame that differs strongly from the first frame: the camera's jump at take frame 0."""
    base = luma(srgb_to_linear(frames[0]))
    for i in range(1, len(frames)):
        d = float(np.abs(luma(srgb_to_linear(frames[i])) - base).mean())
        if d > 0.02:
            return i
    return 0


def ssim(a, b):
    """SSIM of two luminance images (0..1 floats), gaussian window sigma 1.5, computed at half size."""
    from scipy.ndimage import gaussian_filter
    a = a[::2, ::2]; b = b[::2, ::2]
    c1, c2 = 0.01 ** 2, 0.03 ** 2
    mu_a, mu_b = gaussian_filter(a, 1.5), gaussian_filter(b, 1.5)
    saa = gaussian_filter(a * a, 1.5) - mu_a ** 2
    sbb = gaussian_filter(b * b, 1.5) - mu_b ** 2
    sab = gaussian_filter(a * b, 1.5) - mu_a * mu_b
    m = ((2 * mu_a * mu_b + c1) * (2 * sab + c2)) / ((mu_a ** 2 + mu_b ** 2 + c1) * (saa + sbb + c2))
    return float(m.mean())


def tiles(diff, nx=16, ny=9):
    h, w = diff.shape
    out = np.zeros((ny, nx))
    for j in range(ny):
        for i in range(nx):
            out[j, i] = np.sqrt((diff[j * h // ny:(j + 1) * h // ny, i * w // nx:(i + 1) * w // nx] ** 2).mean())
    return out


def compare(take, golden, actual, every, out):
    from PIL import Image, ImageDraw
    gfiles, gframes = load_frames(golden)
    afiles, aframes = load_frames(actual)
    if not gframes or not aframes:
        raise SystemExit(f"no frames: golden {len(gframes)} ({golden}), actual {len(aframes)} ({actual})")
    if gframes[0].shape != aframes[0].shape:
        raise SystemExit(f"sizes differ: golden {gframes[0].shape} vs actual {aframes[0].shape}")
    g0, a0 = align_index(gframes), align_index(aframes)
    n = min(len(gframes) - g0, len(aframes) - a0)
    print(f"aligned: golden frame {g0} = actual frame {a0} = take frame 0; {n} pairs")
    rows, heat = [], np.zeros((9, 16))
    sheet_tiles = []
    for k in range(n):
        gl, al = srgb_to_linear(gframes[g0 + k]), srgb_to_linear(aframes[a0 + k])
        d = al - gl
        rmse = float(np.sqrt((d ** 2).mean()))
        gy, ay = luma(gl), luma(al)
        lr = float(np.sqrt(((ay - gy) ** 2).mean()))
        s = ssim(gy, ay)
        t = tiles(ay - gy)
        heat += t
        rows.append({"frame": k, "rmse": round(rmse, 5), "luma_rmse": round(lr, 5), "ssim": round(s, 4),
                     "worst_tile": [int(x) for x in np.unravel_index(int(t.argmax()), t.shape)][::-1], "worst_tile_rmse": round(float(t.max()), 4)})
        if k % every == 0 or k == n - 1:
            diffimg = np.clip(np.abs(aframes[a0 + k].astype(np.int16) - gframes[g0 + k].astype(np.int16)) * 4, 0, 255).astype(np.uint8)
            sheet_tiles.append((k, gframes[g0 + k], aframes[a0 + k], diffimg, rmse, s))
    heat /= max(n, 1)
    mean_rmse = float(np.mean([r["rmse"] for r in rows])); mean_ssim = float(np.mean([r["ssim"] for r in rows]))
    loss = mean_rmse + 0.5 * (1.0 - mean_ssim)
    worst_frames = sorted(rows, key=lambda r: -r["rmse"])[:5]
    wt = np.dstack(np.unravel_index(np.argsort(-heat, axis=None)[:5], heat.shape))[0]
    report = {"take": take, "golden": golden, "actual": actual, "aligned": {"golden_frame0": g0, "actual_frame0": a0, "pairs": n},
              "loss": round(loss, 5), "mean_rmse": round(mean_rmse, 5), "mean_luma_rmse": round(float(np.mean([r["luma_rmse"] for r in rows])), 5),
              "mean_ssim": round(mean_ssim, 4), "worst_frames": worst_frames,
              "worst_tiles": [{"tile_xy": [int(x[1]), int(x[0])], "rmse": round(float(heat[x[0], x[1]]), 4)} for x in wt],
              "frames": rows}
    os.makedirs(out, exist_ok=True)
    json.dump(report, open(os.path.join(out, "report.json"), "w"), indent=1)
    # the sheet: unity | amoebius | 4x diff, labelled
    tw, th = 480, 270
    sheet = Image.new("RGB", (tw * 3, (th + 22) * len(sheet_tiles)), (12, 12, 20))
    dr = ImageDraw.Draw(sheet)
    for i, (k, g, a, d, rmse, s) in enumerate(sheet_tiles):
        y = i * (th + 22)
        for j, img in enumerate((g, a, d)):
            sheet.paste(Image.fromarray(img).resize((tw, th)), (j * tw, y + 22))
        dr.text((6, y + 4), f"take frame {k}: unity | amoebius | 4x diff   rmse {rmse:.4f}  ssim {s:.3f}", fill=(230, 230, 240))
    sheet.save(os.path.join(out, "sheet.png"))
    hm = (np.clip(heat / max(heat.max(), 1e-6), 0, 1) * 255).astype(np.uint8)
    Image.fromarray(hm).resize((16 * 40, 9 * 40), Image.NEAREST).save(os.path.join(out, "heat.png"))
    print(f"loss {loss:.4f}  mean rmse {mean_rmse:.4f}  luma rmse {report['mean_luma_rmse']:.4f}  ssim {mean_ssim:.4f}  over {n} frames")
    print("worst frames: " + ", ".join(f"{r['frame']} ({r['rmse']:.4f})" for r in worst_frames))
    print("worst tiles (x,y of 16x9): " + ", ".join(f"{t['tile_xy']} {t['rmse']:.4f}" for t in report["worst_tiles"]))
    print(f"report -> {os.path.join(out, 'report.json')}; sheet -> {os.path.join(out, 'sheet.png')}")
    return report


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["record", "compare", "self"])
    ap.add_argument("--take", default="control")
    ap.add_argument("--out")
    ap.add_argument("--golden")
    ap.add_argument("--actual")
    ap.add_argument("--every", type=int, default=30)
    a = ap.parse_args(argv)
    if a.cmd == "record":
        out = a.out or os.path.join(RESULTS, a.take)
        os.makedirs(out, exist_ok=True)
        return 0 if record(a.take, out) else 1
    if a.cmd == "self":
        d1, d2 = os.path.join(RESULTS, a.take + "-self1"), os.path.join(RESULTS, a.take + "-self2")
        for d in (d1, d2):
            os.makedirs(d, exist_ok=True)
            if not record(a.take, d): return 1
        compare(a.take, d1, d2, a.every, os.path.join(RESULTS, a.take + "-self"))
        return 0
    golden = a.golden or os.path.join(GOLDENS, a.take)
    actual = a.actual or os.path.join(RESULTS, a.take)
    compare(a.take, golden, actual, a.every, a.out or actual)
    return 0


if __name__ == "__main__":
    sys.exit(main())

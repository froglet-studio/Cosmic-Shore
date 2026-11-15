#!/usr/bin/env python3
"""Cut the Cosmic Shore trailer from Unity-filmed shots, to the beat of a soundtrack song.

    python3 Tools/Build/trailer/cut.py --edl Tools/Build/trailer/edl.json --shots <dir> --out trailer.mp4
    python3 Tools/Build/trailer/cut.py ... --sheet sheet.png    # also write a contact sheet of every cut

The EDL is written in SONG time: each segment starts at a song second (on a beat) and runs to the
next segment's start, so every cut lands on the music by construction. A segment names a shot file
in --shots, the source second to start from, a playback speed, and optional effects (a white flash
in, a fade from black). Text cards are rendered here with the game's UI face (Chakra Petch) and laid
over the footage; the end card puts the game's logo (the splash screen's wordmark) over a blurred,
darkened frame of the finale. Output: H.264 1920x1080 60 fps, AAC 320k, ready to post.

Needs ffmpeg and Pillow. Footage comes from Unity (Assets/_Scripts/Editor/Trailer/), never Amoebius.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
FONT = os.path.join(ROOT, "Assets", "_Graphics", "Fonts", "ChakraPetch", "ChakraPetch-SemiBold.ttf")
LOGO = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "App Icons", "SPLASH SCREEN.png")
W, H, FPS = 1920, 1080, 60
CYAN = (47, 212, 245)


def find_ffmpeg():
    """$FFMPEG, then PATH, then the binary bundled with the imageio-ffmpeg pip package (Windows has no system one)."""
    found = os.environ.get("FFMPEG") or shutil.which("ffmpeg")
    if found:
        return found
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        raise SystemExit("ffmpeg not found: put it on PATH, set FFMPEG, or pip install imageio-ffmpeg")


FFMPEG = find_ffmpeg()


def run(cmd):
    if cmd[0] == "ffmpeg":
        cmd = [FFMPEG] + cmd[1:]
    r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", errors="replace")
    if r.returncode:
        sys.stderr.write(" ".join(cmd) + "\n" + r.stderr[-3000:])
        raise SystemExit(f"ffmpeg failed ({r.returncode})")
    return r.stdout


def font(size):
    return ImageFont.truetype(FONT, size)


def spaced_width(f, text, tracking):
    return sum(f.getlength(c) for c in text) + tracking * max(0, len(text) - 1)


def draw_spaced(d, xy, text, f, tracking, fill):
    x, y = xy
    for c in text:
        d.text((x, y), c, font=f, fill=fill)
        x += f.getlength(c) + tracking


def text_frame(lines, alpha, grow, sizes, cy=0.5):
    """One RGBA overlay frame: centred lines with a cyan glow; alpha 0..1, grow 0..1 sets the tracking.
    cy is the block's vertical centre as a fraction of the height, so a card can sit clear of a hull."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d, g = ImageDraw.Draw(img), ImageDraw.Draw(glow)
    fonts = [font(s) for s in sizes]
    total = sum(s * 1.35 for s in sizes)
    y = H * cy - total / 2
    for text, f, s in zip(lines, fonts, sizes):
        tracking = s * (0.10 + 0.08 * grow)
        w = spaced_width(f, text, tracking)
        x = (W - w) / 2
        draw_spaced(g, (x, y), text, f, tracking, CYAN + (int(255 * alpha),))
        draw_spaced(d, (x, y), text, f, tracking, (255, 255, 255, int(255 * alpha)))
        y += s * 1.35
    glow = glow.filter(ImageFilter.GaussianBlur(14))
    # a soft dark band behind the words keeps them legible over bright footage
    band = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bd = ImageDraw.Draw(band)
    bd.rectangle([0, H * cy - total * 0.9, W, H * cy + total * 0.9], fill=(0, 0, 0, int(110 * alpha)))
    band = band.filter(ImageFilter.GaussianBlur(60))
    return Image.alpha_composite(Image.alpha_composite(band, glow), img)


def render_text(card, out_dir):
    """PNG sequence for one text card; fades 0.25 s in and 0.2 s out, tracking opens over its life."""
    dur = card["t1"] - card["t0"]
    n = int(round(dur * FPS))
    sizes = card.get("sizes") or [64] * len(card["lines"])
    os.makedirs(out_dir, exist_ok=True)
    full = text_frame(card["lines"], 1.0, 0.5, sizes, card.get("cy", 0.5))   # drawn once; each frame only scales its alpha
    alpha = full.getchannel("A")
    for i in range(n):
        t = i / FPS
        a = max(0.0, min(1.0, t / 0.25, (dur - t) / 0.2))
        f = full.copy()
        f.putalpha(alpha.point(lambda v, a=a: int(v * a)))
        f.save(os.path.join(out_dir, f"{i:05d}.png"), compress_level=1)
    return n


def logo_rgba():
    im = Image.open(LOGO).convert("RGB")
    bbox = (330, 166, 1268, 555)
    im = im.crop(bbox)
    # the splash sits on near-black: key luminance to alpha so it lays cleanly over footage
    lum = im.convert("L").point(lambda v: 0 if v < 14 else min(255, int((v - 14) * 3.2)))
    out = im.convert("RGBA")
    out.putalpha(lum)
    return out


def render_endcard(bg_png, card, out_dir):
    dur = card["dur"]
    n = int(round(dur * FPS))
    bg = Image.open(bg_png).convert("RGB").resize((W, H))
    bg = bg.filter(ImageFilter.GaussianBlur(10))
    logo = logo_rgba()
    f_tag, f_plat = font(46), font(32)
    os.makedirs(out_dir, exist_ok=True)
    for i in range(n):
        t = i / FPS
        dim = 0.35 + 0.30 * max(0.0, 1 - t / 0.8)          # the finale fades down under the logo
        frame = Image.blend(Image.new("RGB", (W, H)), bg, dim * 0.55).convert("RGBA")
        k = min(1.0, t / 0.9)
        ease = 1 - (1 - k) ** 3
        scale = 0.92 + 0.08 * ease
        lw = int(1000 * scale)
        lg = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)
        a = lg.getchannel("A").point(lambda v: int(v * ease))
        lg.putalpha(a)
        gl = lg.filter(ImageFilter.GaussianBlur(24))
        x, y = (W - lg.width) // 2, int(H * 0.40 - lg.height / 2)
        frame.alpha_composite(gl, (x, y))
        frame.alpha_composite(lg, (x, y))
        d = ImageDraw.Draw(frame)
        for txt, f, yy, start, col in ((card["tagline"], f_tag, 0.70, 0.7, (255, 255, 255)),
                                        (card["platforms"], f_plat, 0.79, 1.2, CYAN)):
            ta = max(0.0, min(1.0, (t - start) / 0.4))
            if ta <= 0:
                continue
            tr = f.size * 0.18
            w = spaced_width(f, txt, tr)
            draw_spaced(d, ((W - w) / 2, H * yy), txt, f, tr, col + (int(255 * ta),))
        fo = min(1.0, (dur - t) / 0.6)                    # fade to black at the very end
        if fo < 1:
            frame = Image.blend(Image.new("RGBA", (W, H), (0, 0, 0, 255)), frame, max(0.0, fo))
        frame.convert("RGB").save(os.path.join(out_dir, f"{i:05d}.png"))
    return n


def grade(seg):
    g = seg.get("grade", {})
    c, s, b = g.get("contrast", 1.06), g.get("saturation", 1.12), g.get("brightness", 0.0)
    return f"eq=contrast={c}:saturation={s}:brightness={b}"


def resolve_shot(shots, name):
    """A shot is named by its id ("S01") or a file; the Unity shoot names files S01_<what>.mp4.
    shots.json wins, because a shot's id can differ from its file's prefix (S16a is S16_finale_hesperides.mp4)."""
    exact = os.path.join(shots, name)
    if os.path.exists(exact):
        return exact
    manifest = os.path.join(shots, "shots.json")
    if os.path.exists(manifest):
        with open(manifest, encoding="utf-8") as f:
            for entry in json.load(f):
                if entry.get("shot") == name and os.path.exists(os.path.join(shots, entry["file"])):
                    return os.path.join(shots, entry["file"])
    import glob
    hits = sorted(glob.glob(os.path.join(shots, name + "_*.mp4"))) + sorted(glob.glob(os.path.join(shots, name + ".mp4")))
    if not hits:
        raise SystemExit(f"no shot {name} in {shots}")
    return hits[0]


def cut_segment(seg, dur, shots, out):
    src = resolve_shot(shots, seg["shot"])
    speed = seg.get("speed", 1.0)
    vf = [f"setpts=(PTS-STARTPTS)/{speed}", f"fps={FPS}",
          f"scale={W}:{H}:force_original_aspect_ratio=increase", f"crop={W}:{H}", grade(seg)]
    if seg.get("flash"):
        vf.append("fade=t=in:st=0:d=0.35:color=white")
    if seg.get("fade_in"):
        vf.append(f"fade=t=in:st=0:d={seg['fade_in']}")
    if seg.get("fade_out"):
        vf.append(f"fade=t=out:st={dur - seg['fade_out']:.3f}:d={seg['fade_out']}")
    # -t limits the SOURCE (before -i): as an output option it would cut a slowed segment short and slide
    # every later cut off the music.
    run(["ffmpeg", "-y", "-v", "error", "-ss", str(seg.get("src_in", 0)), "-t", f"{dur * speed:.4f}", "-i", src,
         "-an", "-vf", ",".join(vf), "-frames:v", str(int(round(dur * FPS))),
         "-c:v", "libx264", "-preset", "veryfast", "-crf", "10", "-pix_fmt", "yuv420p", out])


def loudnorm(path, target):
    """Two-pass EBU R128 on the audio only (the picture is copied): measure, then correct linearly to the target."""
    spec = f"I={target.get('I', -14)}:TP={target.get('TP', -1)}:LRA={target.get('LRA', 11)}"
    r = subprocess.run([FFMPEG, "-hide_banner", "-nostats", "-i", path, "-vn", "-af", f"loudnorm={spec}:print_format=json", "-f", "null", "-"],
                       stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", errors="replace")
    tail = r.stderr[r.stderr.rindex("{"):]
    m = json.loads(tail[:tail.rindex("}") + 1])
    chain = (f"loudnorm={spec}:measured_I={m['input_i']}:measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}"
             f":measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true")
    out = path + ".norm.mp4"
    run(["ffmpeg", "-y", "-v", "error", "-i", path, "-c:v", "copy", "-af", chain, "-ar", "48000",
         "-c:a", "aac", "-b:a", "320k", "-movflags", "+faststart", out])
    os.replace(out, path)
    print(f"loudness: {m['input_i']} LUFS, {m['input_tp']} dBTP -> target {spec}")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--edl", required=True)
    ap.add_argument("--shots", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--sheet")
    ap.add_argument("--keep", help="keep the work folder here")
    a = ap.parse_args(argv)
    with open(a.edl, encoding="utf-8") as f:   # the EDL has non-ASCII; Windows would default to cp1252
        edl = json.load(f)
    music = edl["music"] if os.path.isabs(edl["music"]) else os.path.join(ROOT, edl["music"])
    work = a.keep or tempfile.mkdtemp(prefix="trailer-")
    os.makedirs(work, exist_ok=True)
    segs = edl["segments"]
    t_start, t_end = segs[0]["t0"], edl["end"]
    bounds = [s["t0"] for s in segs] + [t_end]
    # cut on whole frames so the picture never drifts off the music
    frames = [int(round((b - t_start) * FPS)) for b in bounds]
    parts = []
    for i, seg in enumerate(segs):
        dur = (frames[i + 1] - frames[i]) / FPS
        p = os.path.join(work, f"seg{i:02d}.mp4")
        cut_segment(seg, dur, a.shots, p)
        parts.append(p)
        print(f"  {bounds[i]:6.2f} +{dur:5.2f}s  {seg['shot']} @{seg.get('src_in', 0)} x{seg.get('speed', 1.0)}")
    # end card over the finale's last frame
    ec = edl["endcard"]
    last = os.path.join(work, "last.png")
    run(["ffmpeg", "-y", "-v", "error", "-sseof", "-0.05", "-i", parts[-1], "-frames:v", "1", "-update", "1", last])
    ecdir = os.path.join(work, "endcard")
    render_endcard(last, ec, ecdir)
    ecmp4 = os.path.join(work, "endcard.mp4")
    run(["ffmpeg", "-y", "-v", "error", "-framerate", str(FPS), "-i", os.path.join(ecdir, "%05d.png"),
         "-c:v", "libx264", "-preset", "veryfast", "-crf", "10", "-pix_fmt", "yuv420p", ecmp4])
    parts.append(ecmp4)
    lst = os.path.join(work, "list.txt")
    with open(lst, "w") as f:
        # forward slashes: the concat demuxer reads a backslash as an escape (Windows paths)
        f.writelines("file '" + p.replace(os.sep, "/") + "'\n" for p in parts)
    body = os.path.join(work, "body.mp4")
    run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", body])
    total = frames[-1] / FPS + ec["dur"]
    # text cards
    inputs, chains, prev = ["-i", body], [], "0:v"
    for k, card in enumerate(edl.get("texts", [])):
        d = os.path.join(work, f"text{k}")
        render_text(card, d)
        inputs += ["-framerate", str(FPS), "-i", os.path.join(d, "%05d.png")]
        off = card["t0"] - t_start
        chains.append(f"[{k + 1}:v]setpts=PTS-STARTPTS+{off:.4f}/TB[t{k}];[{prev}][t{k}]overlay=eof_action=pass[v{k}]")
        prev = f"v{k}"
    a_in = inputs.count("-i")
    fade_out = edl.get("audio_fade_out", 2.5)
    audio = (f"[{a_in}:a]atrim=start={t_start}:end={t_start + total},asetpts=PTS-STARTPTS,"
             f"afade=t=in:st=0:d={edl.get('audio_fade_in', 0.5)},afade=t=out:st={total - fade_out:.3f}:d={fade_out}[a]")
    fc = ";".join(chains + [audio])
    vmap = f"[{prev}]" if chains else "0:v"
    run(["ffmpeg", "-y", "-v", "error", *inputs, "-i", music, "-filter_complex", fc, "-map", vmap, "-map", "[a]",
         "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-pix_fmt", "yuv420p", "-r", str(FPS),
         "-c:a", "aac", "-b:a", "320k", "-movflags", "+faststart", "-t", f"{total:.3f}", a.out])
    if edl.get("loudness"):
        loudnorm(a.out, edl["loudness"])
    print(f"wrote {a.out} ({total:.1f} s)")
    if a.sheet:
        mids = [((frames[i] + frames[i + 1]) / 2) / FPS for i in range(len(segs))] + [total - 2.0]
        tiles = []
        for j, t in enumerate(mids):
            p = os.path.join(work, f"sheet{j:02d}.png")
            run(["ffmpeg", "-y", "-v", "error", "-ss", f"{t:.3f}", "-i", a.out, "-frames:v", "1", "-vf", "scale=480:270", p])
            tiles.append(p)
        cols = 4
        rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new("RGB", (480 * cols, 270 * rows))
        for j, p in enumerate(tiles):
            sheet.paste(Image.open(p), ((j % cols) * 480, (j // cols) * 270))
        sheet.save(a.sheet)
        print(f"wrote {a.sheet}")
    if not a.keep:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())

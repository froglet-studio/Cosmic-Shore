#!/usr/bin/env python3
"""REFERENCE PICTURES: what a painter that takes pictures is shown, so the art matches the game and a pilot is the
same person from one picture to the next.

Garrett, 2026-11-11 (22:49 UTC), on a bridge whose windows and hologram showed a sea of soap bubbles and beads: "both
the hypersea outside and the hypersea in the rendering are very different than the hypersea in our gameplay. is there
a way to upload screenshots or other content so we can keep consistency between the game and the art we are
generating? it seems like we will need something like this if we want the same character in more than one piece of
art as well". And (23:03 UTC): "i connected weave. use amoebius etc. to make and send screenshots. you can also use
froglet.games".

The painter of every bust and scene so far took words only (.claude/skills/pilot-art/references/PAINTERS.md). Its
successor, run through Weave, also takes pictures (PAINTER). So:

  * THE LIBRARY (Docs/Pilots/art/refs, refs.json): real frames of the game and views of its models, each with its
    role, the words the painter is told about it (`shows`) and where it came from (`source`). A frame is a real
    frame: never retouched, cut only to leave out the game's own lettering (a HUD, a menu).
  * A PILOT'S LIKENESS (Docs/Pilots/art/scenes/likeness/<WORD>.jpg): head and shoulders, cut from their approved
    scene. Every later painting of that pilot is shown it first. It is cut once and kept (likeness.json says from
    which picture): when a scene is painted again, the likeness is still the face the pilot had.
  * A RECORD'S refs=(...): the library pictures that go with the scene, whatever is outside its window or in its
    display.
  * A PATCH: a record's patch=dict(what=, ask=, edit=, keep=, refs=). Part of a finished scene is painted again and
    the rest is kept. The painter gets the scene, the references and what to change, and `apply` takes from its
    answer only the pixels inside the record's region (edit, less keep), feathered inward. A painter asked to
    change one thing still repaints the whole picture a little, faces included (Jugger's answer moved 14.36 of
    255 outside its region, Scales's 6.09), so the pilot is never taken from its answer. The region is also
    written as a mask (`mask`): a painter that takes one is given it.
  * A PATCH OF THE PILOT. Where the head itself changes (a fin, a skin), no region can hold the change and keep
    the face: the patch's edit is the whole frame (WHOLE) and the picture is the painter's answer, looked at whole
    before it is taken. Its likeness= names the one thing taken from the pilot's likeness, which is then given as
    picture 2 (PATCH_LIKENESS). Flux's fin was painted this way, on the first run (LESSONS 87).
  * A PATCH SHOWN THE BUST. Where the pilot's earlier bust holds a part the scene lost (a pouch a stranger named,
    a tail beside the head), the patch's bust= names that one part and the bust goes with the scene as a picture
    (PATCH_BUST). The bust is a painting, so the words take its shape and never its finish, and the patch's own
    words give the part its size, its colours and its root. A bust also shows what the patch takes away (the
    plumes a pouch came with, a crest), and a reference is copied before the words are read: bust_box=(l, t, r, b)
    sends only that detail of the bust (`detail` cuts it to patches/<WORD>_bust.png, which is committed).
  * WHAT A PATCH CHANGES OF THE PILOT. A patch's first line tells the painter what stays: the pilot, the face,
    the hands, the clothes, the place and the light (PATCH_KEPT). A patch that redraws one of those says so in
    changes=(...), and the line then lists the rest: a painter told that the hands stay and asked for new hands
    is given two orders and follows the smaller one.
  * ASKED AND TAKEN. A run is paid for whole, so one ask may name several things, and each is judged on its own
    when the answer is in: the record's `edit` holds only the regions that are taken, and its took= says in a
    phrase what of the ask went into the scene and what was left out (Trace's port and cylinder were taken; her
    head was read blind and left as it was, LESSONS 88, 89). The note beside the mask holds the words as they
    were sent, `took`, and under `earlier` the note of every patch the scene had before.
  * THE SCENE AS ITS OWN REFERENCE. A patch's refs=() sends the scene alone. Where the scene already holds the
    thing at another size (a port beside a display), the words point at it ("the round port of this same
    photograph") and no frame of the game goes with it: a frame is copied for its colour before the words are
    read for theirs. Trace's cylinder came back orange beside an orange frame, and in her port's colours from her
    port (LESSONS 90).
  * LINKS, NOT UPLOADS. The painter fetches each picture by its link. The repository is public, so a picture that is
    committed and pushed has one (raw_url), and `input` refuses a picture that is not pushed. A link names the
    repository, which the words never do: the game's own frames were asked for (above).
  * EVERY RUN IS APPROVED FIRST. Weave quotes a run's cost and runs nothing until the person approves that cost on a
    card, reruns too. `input` only prints what would be sent.

    python3 Tools/Build/pilot_art/refs.py list                         # the library, and who has a likeness
    python3 Tools/Build/pilot_art/refs.py likeness <WORD> [l t r b]    # cut a pilot's likeness from their scene
    python3 Tools/Build/pilot_art/refs.py mask <WORD> [view.jpg]       # write a patch's mask (and a view of it)
    python3 Tools/Build/pilot_art/refs.py detail <WORD>                # cut the detail of the bust a patch shows
    python3 Tools/Build/pilot_art/refs.py prompt <WORD> [--patch]      # the words the painter gets
    python3 Tools/Build/pilot_art/refs.py input <WORD> [--patch] [--size W H]   # the run's input, with its links
    python3 Tools/Build/pilot_art/refs.py apply <WORD> <painted file>  # put a patch into the scene
    python3 Tools/Build/pilot_art/refs.py --self-test
"""
import json
import os
import subprocess
import sys
import urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bust     # noqa: E402
import scene    # noqa: E402

ROOT = scene.ROOT
REFS = "Docs/Pilots/art/refs"
LIBRARY = f"{REFS}/refs.json"
LIKENESS = f"{scene.SCENES}/likeness"
LIKENESS_LOG = f"{LIKENESS}/likeness.json"
PATCHES = f"{scene.SCENES}/patches"
REPO = "froglet-studio/cosmic-shore"   # public: a pushed picture answers at RAW/<commit>/<path> without a token
RAW = f"https://raw.githubusercontent.com/{REPO}"

# The painter. Every scene was painted by GPT Image 2.5 Sunburst from words. On Weave that model also takes a mask,
# but the studio's Weave plan does not include it (asked 2026-11-12: "This model is not available on the Free
# plan"). Its successor is included, takes up to sixteen pictures and no mask, and is quoted at 11 credits a run at
# this variant and quality, whatever the size (3 at quality medium). `size` is the key its contract gives the
# picture's size; `mask` is the key of a painter that takes one, None for one that does not. The Free plan runs a
# model twice for an agent, whatever credits remain, and then asks for a paid plan (met 2026-11-12 on the third run).
PAINTER = dict(name="GPT Image 2.6 (Sunburst), through Weave", id="96f53e1c-465d-44d6-90fd-38bcb9fada66", most=16,
               size="size", mask=None, params=dict(model="Sunburst", quality="high", output_format="png"))

# What a painter is told about each kind of reference. {pictures} is "Picture 3" or "Pictures 3 to 5", {shows} the
# library's own words for them.
ROLES = {
    "sea": ("{pictures} {be} the real sea {name} sails: {shows}. Whatever of that sea this picture holds, beyond a "
            "window or small in a display, has exactly that look: the same shapes, the same colours, the same glow. "
            "Take the look and never the framing. No reference is copied whole, and no craft in them is {name}'s."),
    "ship": ("{pictures} {be} the great ship itself, the same ship from four sides: {shows}. Wherever this picture "
             "shows that ship, small in a display or from outside, it is this ship."),
}
REF_HEAD = "REFERENCE PICTURES, in the order they are given."
LIKENESS_LINE = ("Picture {n} is {name}, photographed before: the same person. The head here is that head exactly, "
                 "its shape, its markings, its colours and its eyes, and the clothes are those clothes. Take the "
                 "person from it and nothing else: the place, the pose, the framing and the light are new, as "
                 "written above.")
PATCH_HEAD = ("Edit this photograph, picture 1. Change only {what}. Everything else stays exactly as it is, in the "
              "same place and at the same size: {kept}.")
# What a patch's first line says stays, after the pilot. A patch names in changes=(...) the ones it redraws.
PATCH_KEPT = ("the face", "the hands", "the clothes", "the place", "every light that falls on them")
PATCH_FOOT = ("It stays one photograph taken with a real camera: the new parts have the grain, the focus and the "
              "exposure of the rest and meet it without a seam. Nothing in the picture carries writing, numbers or "
              "a symbol.")
PATCH_LIKENESS = ("Picture {n} is {name}, photographed before in another place: the same person. Take from it only "
                  "{take}. Everything else stays as picture 1 has it.")
# {portrait} is BUST_WHOLE, or BUST_DETAIL when the patch sends a detail of the bust (bust_box=). The words above
# it own the part's size, colours and root: three critics found "its size against the head" at odds with the sizes
# the asks gave (2026-11-12), and a painter given two sizes takes the picture's.
PATCH_BUST = ("Picture {n} is {name} too, the same person, in {portrait} made before this photograph. Take from it "
              "only {take}: the shape, and nothing else. The size, the colours and where it grows from are as the "
              "words above say. It is a painting and picture 1 is a photograph: what is taken from it is "
              "photographed here, with the grain, the focus and the light of picture 1, and nothing of the "
              "painting's smooth finish, its background or its pose.")
BUST_WHOLE, BUST_DETAIL = "a painted portrait", "a detail cut from a painted portrait"
PATCH_KEYS = ("what", "ask", "edit", "keep", "refs", "likeness", "bust", "bust_box", "changes", "took")
WHOLE = ((0, 0), (scene.FRAME[0], 0), scene.FRAME, (0, scene.FRAME[1]))   # an edit polygon: the whole frame
VIEW_TOKEN = "{view}"   # in a patch's ask: the words the record's own display shows (scene.CHART_SHOWS)
GROW = 0.12             # a likeness is the avatar's square and a little more of the shoulders
FEATHER = 2             # px: how softly a patch's edge meets the scene, all of it inside the mask


def _git(*args):
    return subprocess.run(["git", "-C", ROOT, *args], capture_output=True, text=True, check=False).stdout.strip()


def _word(word):
    return scene.identity(word)["word"]


# ---------------------------------------------------------------------------------------------------------------
# The library.
# ---------------------------------------------------------------------------------------------------------------
def library():
    """{name: dict(role, shows, source)} from refs.json. A picture's file is <name>.jpg beside it."""
    path = os.path.join(ROOT, LIBRARY)
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)["pictures"]


def ref_path(name):
    return os.path.join(ROOT, REFS, f"{name}.jpg")


def library_errors():
    out = []
    lib = library()
    folder = os.path.join(ROOT, REFS)
    on_disk = {n[:-4] for n in os.listdir(folder) if n.endswith(".jpg")} if os.path.isdir(folder) else set()
    for name in sorted(on_disk - set(lib)):
        out.append(f"{REFS}/{name}.jpg is not in refs.json: say what it shows and where it came from")
    for name, row in lib.items():
        if name not in on_disk:
            out.append(f"refs.json lists {name}, and {REFS}/{name}.jpg is missing")
            continue
        if row.get("role") not in ROLES:
            out.append(f"{name}: the role is one of {', '.join(ROLES)}")
        shows = row.get("shows", "")
        if not shows or shows.rstrip()[-1:] in ".;:" or shows[:1].isupper():
            out.append(f"{name}: `shows` is a phrase that goes inside a sentence (no capital, no full stop)")
        if not row.get("source"):
            out.append(f"{name}: say where the picture came from (`source`)")
        for gate, what in ((bust.world_name_in, "names the world"), (bust.emblem_word, "invites an emblem"),
                           (lambda t: (scene.STYLE_WORDS.search(t) or [None])[0], "pulls back to a painting")):
            if gate(shows):
                out.append(f"{name}: what it shows {what} ({gate(shows)!r}): the painter reads these words")
        from PIL import Image
        size = Image.open(ref_path(name)).size
        if size[0] < 900:
            out.append(f"{name}: {size[0]} px wide, and a reference is at least 900: the painter reads its detail")
    return out


# ---------------------------------------------------------------------------------------------------------------
# A pilot's likeness.
# ---------------------------------------------------------------------------------------------------------------
def likeness_path(word):
    return os.path.join(ROOT, LIKENESS, f"{word}.jpg")


def bust_path(word):
    """The pilot's earlier painted bust (creator.py): a patch's bust= shows it to the painter for one part."""
    return scene.creator.bust_path(word)


def bust_detail_path(word):
    """The detail of the bust a patch with bust_box= shows the painter in place of the whole bust."""
    return os.path.join(ROOT, PATCHES, f"{word}_bust.png")


def bust_detail(word, box):
    """The bust cut to `box` (left, top, right, bottom, in the bust's own pixels)."""
    from PIL import Image
    return Image.open(bust_path(word)).convert("RGB").crop(tuple(int(v) for v in box))


def write_bust_detail(word):
    """Cut the detail a record's patch names (bust_box=) and write it beside the mask. Lossless, so the self-test
    can hold the file on disk to the record's box."""
    w = _word(word)
    box = (patch_of(w) or {}).get("bust_box")
    if not box:
        raise ValueError(f"{w}'s patch has no bust_box: it shows the painter the whole bust, or none")
    os.makedirs(os.path.join(ROOT, PATCHES), exist_ok=True)
    bust_detail(w, box).save(bust_detail_path(w), optimize=True)
    return bust_detail_path(w)


def likeness_log():
    path = os.path.join(ROOT, LIKENESS_LOG)
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def likeness_box(size, close, grow=GROW):
    """The avatar's square grown a little on every side, inside the picture."""
    pad = round((close[2] - close[0]) * grow)
    return (max(0, close[0] - pad), max(0, close[1] - pad),
            min(size[0], close[2] + pad), min(size[1], close[3] + pad))


def cut_likeness(word, box=None):
    """Cut a pilot's likeness from their painted scene and log which picture it came from. `box` overrides the
    avatar's square where that square holds something the next picture must not copy (a tail outside a cockpit)."""
    from PIL import Image
    w = _word(word)
    if not os.path.exists(scene.scene_path(w)):
        raise ValueError(f"{w} has no painted scene to take a likeness from")
    im = Image.open(scene.scene_path(w)).convert("RGB")
    if box is None:
        close = scene.close_of(w)
        if not close:
            raise ValueError(f"{w}'s scene has no close box: give the likeness its own box (left top right bottom)")
        box = likeness_box(im.size, close)
    box = tuple(int(v) for v in box)
    if not (0 <= box[0] < box[2] <= im.width and 0 <= box[1] < box[3] <= im.height):
        raise ValueError(f"the box {box} is not inside the scene {im.size}")
    if min(box[2] - box[0], box[3] - box[1]) < scene.AVATAR:
        raise ValueError(f"the box {box} is smaller than an avatar ({scene.AVATAR} px): too little to know a face by")
    os.makedirs(os.path.join(ROOT, LIKENESS), exist_ok=True)
    im.crop(box).save(likeness_path(w), quality=93, subsampling=0, optimize=True)
    rel = f"{scene.SCENES}/{w}.jpg"
    log = likeness_log()
    log[w] = dict(box=list(box), cut_from=f"{_git('log', '-1', '--format=%H', '--', rel) or 'uncommitted'}:{rel}")
    with open(os.path.join(ROOT, LIKENESS_LOG), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(json.dumps(dict(sorted(log.items())), indent=1) + "\n")
    return likeness_path(w)


# ---------------------------------------------------------------------------------------------------------------
# A patch: its mask, and putting the painter's answer into the scene.
# ---------------------------------------------------------------------------------------------------------------
def patch_of(word):
    return (scene.record(_word(word)) or {}).get("patch")


def mask_path(word):
    return os.path.join(ROOT, PATCHES, f"{word}_mask.png")


def patch_note_path(word):
    return os.path.join(ROOT, PATCHES, f"{word}.json")


def _polygons(size, polys):
    from PIL import Image, ImageDraw
    im = Image.new("L", size, 0)
    d = ImageDraw.Draw(im)
    for poly in polys:
        d.polygon([tuple(p) for p in poly], fill=255)
    return im


def region(patch, size=scene.FRAME):
    """Where the painter may work, white on black: every `edit` polygon, less every `keep` polygon."""
    from PIL import ImageChops
    return ImageChops.subtract(_polygons(size, patch["edit"]), _polygons(size, patch.get("keep", ())))


def mask_image(patch, size=scene.FRAME):
    """The mask a painter is given: transparent where it may work. Read either way a painter reads a mask, it says
    the same thing: the region is white and clear, the rest black and solid."""
    from PIL import Image, ImageChops
    reg = region(patch, size)
    rgba = Image.merge("RGB", (reg, reg, reg)).convert("RGBA")
    rgba.putalpha(ImageChops.invert(reg))
    return rgba


def write_mask(word, view=None):
    """Write a record's patch mask beside the scenes (it needs a link, so it is committed); `view` is a scratch
    picture of the scene with the region tinted magenta and what is kept inside it tinted green, to look at."""
    from PIL import Image
    w = _word(word)
    patch = patch_of(w)
    if not patch:
        raise ValueError(f"{w}'s record has no patch")
    base = Image.open(scene.scene_path(w)).convert("RGB")
    os.makedirs(os.path.join(ROOT, PATCHES), exist_ok=True)
    mask_image(patch, base.size).save(mask_path(w), optimize=True)
    if view:
        reg, keep = region(patch, base.size), _polygons(base.size, patch.get("keep", ()))
        seen = Image.composite(Image.blend(base, Image.new("RGB", base.size, (255, 0, 200)), 0.45), base, reg)
        seen = Image.composite(Image.blend(seen, Image.new("RGB", base.size, (0, 255, 120)), 0.45), seen, keep)
        seen.save(view, quality=90)
    return mask_path(w)


def blend(base, painted, patch, feather=FEATHER):
    """(the scene with the painter's pixels inside the patch's region and its own everywhere else, how far the
    painter's answer moved outside the region as a mean over 0 to 255). The soft edge is all inside the region."""
    from PIL import Image, ImageChops, ImageFilter, ImageStat
    if painted.size != base.size:
        if abs(painted.width * base.height - painted.height * base.width) > 2 * max(painted.size):
            raise ValueError(f"the painter's answer is {painted.size} and the scene {base.size}: not the same shape")
        painted = painted.resize(base.size, Image.LANCZOS)
    reg = region(patch, base.size)
    soft = reg
    if feather:   # drawn in by twice its own softness first, so it has faded to nothing where the region ends
        soft = reg.filter(ImageFilter.MinFilter(4 * feather + 1)).filter(ImageFilter.GaussianBlur(feather))
        soft = ImageChops.multiply(soft, reg)
    outside = ImageChops.invert(reg)   # empty when the region is the whole frame: nothing is kept, nothing drifts
    drift = (ImageStat.Stat(ImageChops.difference(painted, base).convert("L"), outside).mean[0]
             if outside.getbbox() else 0.0)
    return Image.composite(painted, base, soft), drift


def apply(word, painted_file, out=None):
    """Put a painter's answer to a patch into the scene, and note what was done beside the mask. Returns the note."""
    from PIL import Image
    w = _word(word)
    patch = patch_of(w)
    if not patch:
        raise ValueError(f"{w}'s record has no patch")
    rel = f"{scene.SCENES}/{w}.jpg"
    before = f"{_git('log', '-1', '--format=%H', '--', rel) or 'uncommitted'}:{rel}"
    base = Image.open(scene.scene_path(w)).convert("RGB")
    result, drift = blend(base, Image.open(painted_file).convert("RGB"), patch)
    result.save(out or scene.scene_path(w), quality=93, subsampling=0, optimize=True)
    note = dict(what=patch["what"], before=before, painter=PAINTER["name"], refs=list(patch_refs(w)),
                prompt=prompt(w, patch=True), moved_outside_the_mask=round(drift, 2))
    if patch.get("bust"):   # the pilot's bust went with the scene, for this one part
        note["bust"] = patch["bust"]
        if patch.get("bust_box"):   # and only this detail of it
            note["bust_box"] = list(patch["bust_box"])
    if patch.get("took"):   # less than the ask named went into the scene: say what did
        note["took"] = patch["took"]
    if not out:
        note.update(earlier_notes(patch_note_path(w), before))
        with open(patch_note_path(w), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(json.dumps(note, indent=1, ensure_ascii=False) + "\n")
    return note


def earlier_notes(path, before):
    """{"earlier": [...]} for a scene that was patched before, oldest first, or {}. A note on disk that starts from
    the same picture is this patch tried again, and is replaced; one that starts from another is an earlier patch
    of the scene, and is kept."""
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as fh:
        old = json.load(fh)
    kept = old.pop("earlier", [])
    if old.get("before") != before:
        kept = kept + [old]
    return dict(earlier=kept) if kept else {}


# ---------------------------------------------------------------------------------------------------------------
# What a record asks for, and whether it may.
# ---------------------------------------------------------------------------------------------------------------
def patch_refs(word):
    rec = scene.record(_word(word)) or {}
    patch = rec.get("patch") or {}
    return tuple(patch.get("refs", rec.get("refs", ())))


def record_errors(word, rec):
    """What is wrong with a record's refs= and patch=, as sentences (none: []). scene.py's self-test asks this."""
    out = []
    lib = library()
    for name in rec.get("refs", ()):
        if name not in lib:
            out.append(f"{word}: refs names {name!r}, which is not in the library ({LIBRARY})")
    patch = rec.get("patch")
    if patch is None:
        return out
    for k in patch:
        if k not in PATCH_KEYS:
            out.append(f"{word}: the patch has an unknown field {k}")
    for k in ("what", "ask", "edit"):
        if not patch.get(k):
            out.append(f"{word}: the patch has no {k}")
    for name in patch.get("refs", ()):
        if name not in lib:
            out.append(f"{word}: the patch's refs names {name!r}, which is not in the library ({LIBRARY})")
    W, H = scene.FRAME
    for k in ("edit", "keep"):
        for poly in patch.get(k, ()):
            if len(poly) < 3 or any(not (0 <= x <= W and 0 <= y <= H) for x, y in poly):
                out.append(f"{word}: a patch {k} polygon has under three points or leaves the frame {scene.FRAME}")
    if patch.get("what", "").rstrip()[-1:] == "." or patch.get("what", "")[:1].isupper():
        out.append(f"{word}: the patch's `what` is a phrase that goes inside a sentence (no capital, no full stop)")
    took = patch.get("took")
    if took is not None and (not took or took.rstrip()[-1:] == "." or took[:1].isupper()):
        out.append(f"{word}: the patch's `took` is a phrase: what of the ask went into the scene")
    take = patch.get("likeness")
    if take is not None:
        if not take or take.rstrip()[-1:] == "." or take[:1].isupper():
            out.append(f"{word}: the patch's `likeness` is a phrase that goes inside a sentence: what is taken")
        if not os.path.exists(likeness_path(word)):
            out.append(f"{word}: the patch takes from a likeness and {word} has none (refs.py likeness {word})")
    shown = patch.get("bust")
    if shown is not None:
        if not shown or shown.rstrip()[-1:] == "." or shown[:1].isupper():
            out.append(f"{word}: the patch's `bust` is a phrase that goes inside a sentence: the one part taken")
        if not os.path.exists(bust_path(word)):
            out.append(f"{word}: the patch takes from the bust and {word} has none ({scene.creator.BUSTS})")
    box = patch.get("bust_box")
    if box is not None:
        if not shown:
            out.append(f"{word}: the patch has a bust_box and no `bust`: the box is the detail of the bust it shows")
        elif os.path.exists(bust_path(word)):
            from PIL import Image
            bw, bh = Image.open(bust_path(word)).size
            if (len(box) != 4 or any(not isinstance(v, int) for v in box)
                    or not (0 <= box[0] < box[2] <= bw and 0 <= box[1] < box[3] <= bh)
                    or min(box[2] - box[0], box[3] - box[1]) < scene.AVATAR):
                out.append(f"{word}: the patch's bust_box is (left, top, right, bottom) inside the bust ({bw}x{bh}), "
                           f"no side under {scene.AVATAR} px")
    changed = patch.get("changes", ())
    if isinstance(changed, str) or len(set(changed)) != len(changed) or any(c not in PATCH_KEPT for c in changed):
        out.append(f"{word}: the patch's `changes` is a tuple of what its first line would say stays, each once: "
                   f"{', '.join(PATCH_KEPT)}")
    if VIEW_TOKEN in patch.get("ask", "") and not rec.get("chart"):
        out.append(f"{word}: the patch's ask says {VIEW_TOKEN} and the record has no chart to take the view from")
    if not out:
        try:
            prompt(word, patch=True)
        except ValueError as e:
            out.append(str(e))
    return out


# ---------------------------------------------------------------------------------------------------------------
# The words and the run.
# ---------------------------------------------------------------------------------------------------------------
def _span(first, count):
    return f"Picture {first}" if count == 1 else (f"Pictures {first} and {first + 1}" if count == 2
                                                  else f"Pictures {first} to {first + count - 1}")


def reference_lines(names, name, first):
    """The paragraph that tells the painter what its reference pictures are: `names` in the order they are given,
    numbered from `first`. Pictures of one role that follow each other are told in one sentence."""
    lib = library()
    lines, n, i = [], first, 0
    while i < len(names):
        role = lib[names[i]]["role"]
        j = i
        while j < len(names) and lib[names[j]]["role"] == role:
            j += 1
        group = names[i:j]
        shows = lib[group[0]]["shows"] if len(group) == 1 else "; ".join(
            f"in picture {n + k}, {lib[g]['shows']}" for k, g in enumerate(group))
        lines.append(ROLES[role].replace("{pictures}", _span(n, len(group)))
                     .replace("{be}", "is" if len(group) == 1 else "are").replace("{shows}", shows)
                     .replace("{name}", name))
        n += len(group)
        i = j
    return lines


def patch_head(patch):
    """A patch's first line: what changes, then everything that stays, which is the pilot and PATCH_KEPT less the
    patch's changes=. With no changes= it is the line every patch before 2026-11-12 was sent with."""
    kept = ["{name}"] + [k for k in PATCH_KEPT if k not in patch.get("changes", ())]
    told = kept[0] if len(kept) == 1 else ", ".join(kept[:-1]) + " and " + kept[-1]
    return PATCH_HEAD.replace("{what}", patch["what"]).replace("{kept}", told)


def _gates(word, text):
    for gate, what in ((bust.world_name_in, "names the world"), (bust.emblem_word, "invites an emblem"),
                       (lambda t: (scene.STYLE_WORDS.search(t) or [None])[0], "pulls back to a painting")):
        hit = gate(text)
        if hit:
            raise ValueError(f"{word}'s reference words {what}: {hit!r}")
    return text


def prompt(word, patch=False):
    """The words the reference painter gets. A new painting: the scene's own brief (scene.compose), then what each
    reference picture is. A patch: what to change inside the mask, then the references."""
    w = _word(word)
    rec = scene.record(w) or {}
    i = scene.identity(w)
    name = i["name"]
    if patch:
        p = rec.get("patch")
        if not p:
            raise ValueError(f"{w}'s record has no patch")
        ask = p["ask"]
        if VIEW_TOKEN in ask:
            kind, view = rec["chart"]
            own = i["accent"] if i["accent"] in scene.PLAY_COLOURS else scene.PLAY_COLOURS[0]
            other, third = [c for c in scene.PLAY_COLOURS if c != own]
            ask = ask.replace(VIEW_TOKEN, scene.CHART_SHOWS[kind][view].replace("{own}", own)
                              .replace("{other}", other).replace("{third}", third))
        refs = patch_refs(w)
        parts = [patch_head(p), ask]
        told = [PATCH_LIKENESS.replace("{n}", "2").replace("{take}", p["likeness"])] if p.get("likeness") else []
        if p.get("bust"):
            told.append(PATCH_BUST.replace("{n}", str(2 + len(told))).replace("{take}", p["bust"])
                        .replace("{portrait}", BUST_DETAIL if p.get("bust_box") else BUST_WHOLE))
        told += reference_lines(refs, name, 2 + len(told))
        if told:
            parts.append(REF_HEAD + " " + " ".join(told))
        parts.append(PATCH_FOOT)
        own_words = "\n\n".join(parts).replace("{name}", name)
        _gates(w, own_words)
        return own_words
    refs = tuple(rec.get("refs", ()))
    lines, n = [], 1
    if os.path.exists(likeness_path(w)):
        lines.append(LIKENESS_LINE.replace("{n}", "1"))
        n = 2
    lines += reference_lines(refs, name, n)
    if not lines:
        raise ValueError(f"{w} has no likeness and the record names no refs: nothing to show a painter "
                         f"(scene.py brief {w} is the words-only brief)")
    tail = (REF_HEAD + " " + " ".join(lines)).replace("{name}", name)
    _gates(w, tail)
    return scene.compose(w) + "\n\n" + tail


def pushed_commit():
    """The commit the links are made at: HEAD, when a remote branch holds it."""
    head = _git("rev-parse", "HEAD")
    if not head or not _git("branch", "-r", "--contains", head):
        raise ValueError("HEAD is not on the remote: the painter fetches each picture by its link, so push first")
    return head


def raw_url(path, commit=None):
    """The public link of a picture as it stands at the pushed commit. Refuses a picture that is not in the
    repository or differs from what is committed: the painter would be shown something else."""
    rel = os.path.relpath(os.path.abspath(path), ROOT).replace(os.sep, "/")
    if rel.startswith("..") or not _git("ls-files", "--", rel):
        raise ValueError(f"{rel} is not in the repository: commit and push it, the painter fetches it by its link")
    if _git("status", "--porcelain", "--", rel):
        raise ValueError(f"{rel} differs from what is committed: commit and push it first")
    return f"{RAW}/{commit or pushed_commit()}/{urllib.parse.quote(rel)}"


def pictures(word, patch=False):
    """[(what it is, path)] in the order the painter gets them."""
    w = _word(word)
    rec = scene.record(w) or {}
    if patch:
        out = [("the scene", scene.scene_path(w))]
        if (rec.get("patch") or {}).get("likeness"):
            out.append(("the likeness", likeness_path(w)))
        if (rec.get("patch") or {}).get("bust"):
            detail = (rec.get("patch") or {}).get("bust_box")
            out.append(("a detail of the bust", bust_detail_path(w)) if detail else ("the bust", bust_path(w)))
        names = patch_refs(w)
    else:
        out = [("the likeness", likeness_path(w))] if os.path.exists(likeness_path(w)) else []
        names = tuple(rec.get("refs", ()))
    return out + [(n, ref_path(n)) for n in names]


def run_input(word, patch=False, size=None, commit=None):
    """What a run of the painter is given: dict(id, input), the input keyed as the painter's contract names it."""
    w = _word(word)
    pics = pictures(w, patch)
    if len(pics) > PAINTER["most"]:
        raise ValueError(f"{len(pics)} pictures: the painter takes {PAINTER['most']}")
    commit = commit or pushed_commit()
    size = size or scene.FRAME
    given = dict(prompt=prompt(w, patch), images=[raw_url(p, commit) for _, p in pics], **PAINTER["params"])
    given[PAINTER["size"]] = dict(width=size[0], height=size[1])
    if patch and PAINTER["mask"]:
        given[PAINTER["mask"]] = raw_url(mask_path(w), commit)
    return dict(id=PAINTER["id"], input=given)


# ---------------------------------------------------------------------------------------------------------------
# Self-test.
# ---------------------------------------------------------------------------------------------------------------
def self_test():
    from PIL import Image
    errors = library_errors()
    lib = library()
    recs = scene.records()
    for w, rec in recs.items():
        errors += record_errors(w, rec)

    # Every likeness is logged, is a catalog pilot's, and is at least an avatar.
    log = likeness_log()
    folder = os.path.join(ROOT, LIKENESS)
    for name in sorted(os.listdir(folder)) if os.path.isdir(folder) else ():
        if name.endswith(".jpg") and name[:-4] not in log:
            errors.append(f"{LIKENESS}/{name} is not in likeness.json: cut it with refs.py likeness")
    for w in log:
        if not os.path.exists(likeness_path(w)):
            errors.append(f"likeness.json lists {w}, and the picture is missing")
        elif min(Image.open(likeness_path(w)).size) < scene.AVATAR:
            errors.append(f"{w}: the likeness is smaller than an avatar")
    if likeness_box((1536, 864), (480, 0, 1100, 620)) != (406, 0, 1174, 694):
        errors.append("likeness_box does not grow the avatar's square inside the picture")

    # Every patch has its mask on disk, the size of its scene, and the mask says what the record says.
    for w, rec in recs.items():
        patch = rec.get("patch")
        if not patch or not os.path.exists(scene.scene_path(w)):
            continue
        if not os.path.exists(mask_path(w)):
            errors.append(f"{w}: the patch has no mask on disk (refs.py mask {w})")
            continue
        size = Image.open(scene.scene_path(w)).size
        on_disk = Image.open(mask_path(w))
        if on_disk.size != size or on_disk.mode != "RGBA":
            errors.append(f"{w}: the mask is {on_disk.size} {on_disk.mode}, and the scene is {size}")
        elif on_disk.tobytes() != mask_image(patch, size).tobytes():
            errors.append(f"{w}: the mask on disk is not the record's polygons (refs.py mask {w})")

    # A patch that shows a detail of the bust has it on disk, and it is the bust cut to the record's box.
    for w, rec in recs.items():
        box = (rec.get("patch") or {}).get("bust_box")
        if not box or not os.path.exists(bust_path(w)):
            continue
        if not os.path.exists(bust_detail_path(w)):
            errors.append(f"{w}: the patch shows a detail of the bust and it is not on disk (refs.py detail {w})")
        elif Image.open(bust_detail_path(w)).convert("RGB").tobytes() != bust_detail(w, box).tobytes():
            errors.append(f"{w}: the detail on disk is not the bust cut to the record's bust_box "
                          f"(refs.py detail {w})")

    # A mask is clear exactly where the painter may work, and blending keeps everything else to the pixel.
    patch = dict(edit=[[(100, 100), (500, 100), (500, 400), (100, 400)]], keep=[[(250, 200), (350, 200), (350, 300)]])
    size = (640, 480)
    reg, mask = region(patch, size), mask_image(patch, size)
    if reg.getpixel((120, 120)) != 255 or reg.getpixel((330, 240)) != 0 or reg.getpixel((50, 50)) != 0:
        errors.append("a patch's region is not its edit polygons less its keep polygons")
    if mask.getpixel((120, 120)) != (255, 255, 255, 0) or mask.getpixel((50, 50)) != (0, 0, 0, 255):
        errors.append("a mask is not white and clear where the painter may work, black and solid elsewhere")
    base, new = Image.new("RGB", size, (10, 20, 30)), Image.new("RGB", size, (200, 150, 100))
    out, drift = blend(base, new, patch)
    if out.getpixel((50, 50)) != (10, 20, 30) or out.getpixel((330, 240)) != (10, 20, 30):
        errors.append("blend changed a pixel outside the patch's region")
    if out.getpixel((200, 150)) != (200, 150, 100):
        errors.append("blend did not take the painter's pixels inside the region")
    edge = [out.getpixel((x, 150))[0] for x in (99, 100, 104, 112)]
    if edge[0] != 10 or edge[1] > 20 or not edge[1] <= edge[2] <= edge[3] or edge[3] != 200:
        errors.append(f"blend's edge is not soft, inside the region and faded out where it ends: {edge}")
    if drift < 100:
        errors.append("blend does not measure how far a painter's answer moved outside the mask")
    whole, drift0 = blend(base, new, dict(edit=[[(0, 0), (640, 0), (640, 480), (0, 480)]]))
    if drift0 != 0.0 or {whole.getpixel(xy) for xy in ((0, 0), (639, 479), (320, 240))} != {(200, 150, 100)}:
        errors.append("a region that is the whole frame does not take the whole answer, to its corners")
    if _polygons(scene.FRAME, [WHOLE]).getextrema() != (255, 255):
        errors.append("WHOLE is not the whole frame")
    try:
        blend(base, Image.new("RGB", (480, 480)), patch)
        errors.append("blend took an answer of another shape")
    except ValueError:
        pass
    if blend(base, Image.new("RGB", (1280, 960), (200, 150, 100)), patch)[0].getpixel((200, 150)) != (200, 150, 100):
        errors.append("blend does not size a larger answer of the same shape to the scene")

    # A scene patched twice keeps its first note; the same patch tried again replaces its own; `took` is a phrase.
    import tempfile
    with tempfile.TemporaryDirectory() as tmp:
        path = os.path.join(tmp, "note.json")
        first, second = dict(what="a", before="c1:x"), dict(what="b", before="c2:x", earlier=[dict(what="a")])
        cases = [(None, "c1:x", {}), (first, "c1:x", {}), (first, "c2:x", dict(earlier=[first])),
                 (second, "c3:x", dict(earlier=[dict(what="a"), dict(what="b", before="c2:x")]))]
        for on_disk, before, want in cases:
            if on_disk is None:
                if os.path.exists(path):
                    os.remove(path)
            else:
                with open(path, "w", encoding="utf-8") as fh:
                    json.dump(on_disk, fh)
            if earlier_notes(path, before) != want:
                errors.append(f"earlier_notes kept the wrong notes for a patch that starts from {before}")
    whole = dict(what="a thing", ask="Change it.", edit=(WHOLE,))
    if not any("`took`" in e for e in record_errors("MANGER", dict(patch=dict(whole, took="Everything.")))):
        errors.append("record_errors took a sentence for a patch's `took`")
    if not any("`bust`" in e for e in record_errors("MANGER", dict(patch=dict(whole, bust="The whole bust.")))):
        errors.append("record_errors took a sentence for a patch's `bust`")
    if not any("`changes`" in e for e in record_errors("MANGER", dict(patch=dict(whole, changes=("the tail",))))):
        errors.append("record_errors took a patch that changes something its first line never said stays")
    if not any("`changes`" in e for e in record_errors("MANGER", dict(patch=dict(whole, changes="the face")))):
        errors.append("record_errors took a string for a patch's `changes`")
    as_sent = ("Edit this photograph, picture 1. Change only a thing. Everything else stays exactly as it is, in "
               "the same place and at the same size: {name}, the face, the hands, the clothes, the place and every "
               "light that falls on them.")
    if patch_head(whole) != as_sent:
        errors.append("a patch with no changes= no longer gets the first line the earlier patches were sent with")
    fewer = patch_head(dict(whole, changes=("the hands", "every light that falls on them")))
    if not fewer.endswith("at the same size: {name}, the face, the clothes and the place.") or "hands" in fewer:
        errors.append("patch_head still says that what a patch changes stays")
    if patch_head(dict(whole, changes=PATCH_KEPT)) != as_sent.split("{name}")[0] + "{name}.":
        errors.append("patch_head does not end at the pilot when a patch changes everything else")
    shown = (PATCH_BUST.replace("{n}", "2").replace("{take}", "the pouch under the bill").replace("{name}", "Tamako")
             .replace("{portrait}", BUST_DETAIL))
    if ("{" in shown or "only the pouch under the bill: the shape" not in shown or "size against" in shown
            or "Picture 2 is Tamako too, the same person, in a detail cut from a painted portrait" not in shown):
        errors.append("PATCH_BUST does not say which picture the bust is, the one part taken from it, and that the "
                      "words own its size")
    busted = dict(whole, bust="the pouch under the bill")
    for bad, why in (((0, 0, 100, 100), "smaller than an avatar"), ((0, 0, 99999, 400), "outside the bust"),
                     ((0, 0, 400.0, 400), "not whole numbers")):
        if not any("bust_box" in e for e in record_errors("WINGMAN", dict(patch=dict(busted, bust_box=bad)))):
            errors.append(f"record_errors took a bust_box that is {why}")
    if not any("bust_box" in e for e in record_errors("WINGMAN", dict(patch=dict(whole, bust_box=(0, 0, 400, 400))))):
        errors.append("record_errors took a bust_box on a patch that shows no bust")

    # A link is only given for what is committed; the reference words number the pictures as they are given.
    for bad in (os.path.join(ROOT, REFS, "no_such_picture.jpg"), "/tmp/outside.jpg"):
        try:
            raw_url(bad, "0" * 40)
            errors.append(f"raw_url gave a link for {bad}")
        except ValueError:
            pass
    seas = [n for n, r in lib.items() if r["role"] == "sea"]
    ships = [n for n, r in lib.items() if r["role"] == "ship"]
    if len(seas) >= 2 and ships:
        told = " ".join(reference_lines(seas[:2] + ships[:1], "Tamako", 2))
        if "Pictures 2 and 3 are" not in told or "Picture 4 is" not in told or "{" in told:
            errors.append("reference_lines does not number the pictures in the order they are given")
    else:
        errors.append("the library needs at least two pictures of the sea and one of the ship")
    for w, rec in recs.items():
        for is_patch in (False, True):
            if is_patch and not rec.get("patch"):
                continue
            if not is_patch and not (rec.get("refs") or os.path.exists(likeness_path(w))):
                continue
            try:
                text = prompt(w, is_patch)
            except Exception as e:   # noqa: BLE001
                errors.append(f"{w}: the reference words do not compose: {e}")
                continue
            count = len(pictures(w, is_patch))
            import re
            told = {int(n) for n in re.findall(r"[Pp]ictures? (\d+)", text)} | \
                   {int(n) for n in re.findall(r"(?:to|and) (\d+) are", text)}
            if not told or max(told) != count or (is_patch and 1 not in told):
                errors.append(f"{w}: the words speak of pictures {sorted(told)} and {count} are given")
            if "{" in text:
                errors.append(f"{w}: a token is left in the reference words")
    for line in list(ROLES.values()) + [REF_HEAD, LIKENESS_LINE, PATCH_LIKENESS, PATCH_BUST, PATCH_HEAD, PATCH_FOOT]:
        for gate, what in ((bust.world_name_in, "names the world"), (bust.emblem_word, "invites an emblem")):
            if gate(line):
                errors.append(f"a reference line {what} ({gate(line)!r}): {line[:60]}...")
    if errors:
        print("refs.py self-test FAILED:")
        for e in errors:
            print("  -", e)
        return 1
    print(f"refs.py self-test: ok ({len(lib)} library pictures, {len(log)} likenesses, "
          f"{sum(1 for r in recs.values() if r.get('patch'))} patches, "
          f"{sum(1 for r in recs.values() if r.get('refs'))} records with references)")
    return 0


def main():
    args = sys.argv[1:]
    if not args or args[0] in ("-h", "--help"):
        print(__doc__)
        return 0
    if args[0] == "--self-test":
        return self_test()
    try:
        if args[0] == "list":
            for name, row in library().items():
                print(f"{name:22} {row['role']:5} {row['shows'][:88]}")
            print("likenesses:", ", ".join(sorted(likeness_log())) or "none")
            return 0
        if args[0] == "likeness" and len(args) in (2, 6):
            path = cut_likeness(args[1], args[2:] or None)
            print(f"cut {os.path.relpath(path, ROOT)}: look at it, then commit it (it needs a link)")
            return 0
        if args[0] == "mask" and len(args) in (2, 3):
            path = write_mask(args[1], args[2] if len(args) == 3 else None)
            print(f"wrote {os.path.relpath(path, ROOT)}" + (f" and the view {args[2]}" if len(args) == 3 else ""))
            return 0
        if args[0] == "detail" and len(args) == 2:
            path = write_bust_detail(args[1])
            print(f"cut {os.path.relpath(path, ROOT)}: look at it, then commit it (it needs a link)")
            return 0
        if args[0] == "prompt" and len(args) >= 2:
            print(prompt(args[1], "--patch" in args))
            return 0
        if args[0] == "input" and len(args) >= 2:
            size = tuple(int(v) for v in args[args.index("--size") + 1:][:2]) if "--size" in args else None
            print(json.dumps(run_input(args[1], "--patch" in args, size), indent=1, ensure_ascii=False))
            return 0
        if args[0] == "apply" and len(args) == 3:
            note = apply(args[1], args[2])
            print(f"patched {scene.SCENES}/{_word(args[1])}.jpg; the painter's answer moved "
                  f"{note['moved_outside_the_mask']} of 255 outside the mask (kept from the scene, not from it)")
            return 0
    except ValueError as e:
        print(f"refs.py: {e}")
        return 2
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""
Author the FTUE captain's COMMS WINDOW (Rev 36 B, Docs/MENU_REDESIGN_B.md section 102.1; Shombith: "Lets go with B") and
put Trace on the first-login guide.

    Assets/_Prefabs/UI Elements/Guide/GuideCommsView.prefab
        A 400 x 560 window (1080 reference): the slivered fill with a white border, Trace's portrait in a 300-unit top
        band (a RectMask2D) fading into the window, "Incoming", her name and title, 18 voice bars, her line, and the prompt
        (a keycap and the action in lime). A separate lime Pointer runs from the window to the lit control. Every graphic is
        raycast-off: the spotlight's dim decides what a press reaches. GuideCommsView names the parts; MenuSpotlight
        instantiates it and places it every frame.

    Assets/FTUE/DataContainer/Speakers/GuideSpeaker_Trace.asset
        GuideSpeakerSO: Trace, "Your captain", "Incoming", her portrait (Portrait_Trace_Placeholder), the prefab, 45
        characters a second, a 0.5 s dock.

    Assets/FTUE/DataContainer/Phases/MainQuest_Phase0_FirstLogin.asset
        The QuestGuideToMicrogameNode's speaker, its three lines in Trace's voice and the three prompts.

    python3 Tools/Build/author_guide_comms.py              # write
    python3 Tools/Build/author_guide_comms.py --check      # fail if any output drifted
    python3 Tools/Build/author_guide_comms.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_start_flow", os.path.join(HERE, "author_start_flow.py"))
SF = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(SF)

HUD, P, K, H, T = SF.HUD, SF.P, SF.K, SF.H, SF.T
REPO = K.REPO
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok

VIEW = "GuideCommsView"
OUT_DIR = "Assets/_Prefabs/UI Elements/Guide"
SPEAKER = "Assets/FTUE/DataContainer/Speakers/GuideSpeaker_Trace.asset"
SPEAKER_DIR = "Assets/FTUE/DataContainer/Speakers"
PHASE = "Assets/FTUE/DataContainer/Phases/MainQuest_Phase0_FirstLogin.asset"
VIEW_SCRIPT = HUD.script_guid("Assets/_Scripts/UI/Guide/GuideCommsView.cs")
SPEAKER_SCRIPT = HUD.script_guid("Assets/_Scripts/UI/Guide/GuideSpeakerSO.cs")
NODE_SCRIPT = HUD.script_guid("Assets/FTUE/Scripts/Graph/Nodes/QuestGuideToMicrogameNode.cs")
PORTRAIT = K.meta_guid("Assets/_Graphics/Pilots/Portrait_Trace_Placeholder.png")
RECT_MASK_2D = "3312d7739989d2b4e91e6319e9a96d76"
W, H_, BAND, PAD, BARS = 400, 560, 300, 30, 18

# Trace's words on the three steps of the first-login guide (the guide's words live on the node, in the asset).
LINES = dict(arcadeCaption="Pilot. I'm Trace, your captain. Your first race is in the Arcade. Take us in.",
             cardCaption="This week's game. Fly close to a trail and you steal its speed. Open it.",
             previewCaption="Your vessel is warm. Tap the window and fly. Show me what you've got.")
ACTIONS = dict(arcadeAction="Arcade", cardAction="Open", previewAction="Fly", actionKeycap="A")


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path():
    return f"{OUT_DIR}/{VIEW}.prefab"


def F(key):
    return K.FileIds(VIEW)(key)


def TL(x, y, w, h):
    return SF.TL(x, y, w, h)


def image(parent, key, name, r, token, sprite=None, alpha=1.0, sliced=False, preserve=False):
    n = parent.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok(token, alpha), sprite, sliced, False, preserve))
    return n


def build_view():
    root = N("root", VIEW, R((0, 0), None, (0, 0), (W, H_), (0, 0)))
    win = root.child(N("Window", "Window", STRETCH))
    image(win, "Window/Fill", "Fill", STRETCH, "SurfaceBlack", K.kit_sprite("kit_sliver14_fill"), 0.97, sliced=True)
    band = win.child(N("Window/Band", "Band", R((0, 1), (1, 1), (0, 0), (-2, BAND), (0.5, 1))))
    band.add("mask", 114, lambda ctx, go: K.mono(go, RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n"))
    image(band, "Window/Band/Back", "Back", STRETCH, "SurfaceDark", alpha=0.9)
    # The portrait at about 2x, its face (beak to chest) in the band: 220 x 280 source, head near x 95.
    image(band, "Window/Band/Portrait", "Portrait", R((0, 1), None, (2, 6), (460, 585), (0, 1)), "TextLight", PORTRAIT)
    image(band, "Window/Band/Fade", "Fade", R((0, 0), (1, 0), (0, 0), (0, 60), (0.5, 0)), "SurfaceBlack", K.kit_sprite("kit_fade_v"), 0.97)
    image(win, "Window/Border", "Border", STRETCH, "TextLight", K.kit_sprite("kit_sliver14_line1"), sliced=True)
    H.label(win.child(N("Window/Eyebrow", "Eyebrow", TL(PAD, 20, 200, 18))), "Incoming", "Label", "NeutralLightest")
    H.label(win.child(N("Window/Name", "Name", TL(PAD, 278, 140, 26))), "Trace", "H3", "TextLight")
    H.label(win.child(N("Window/Title", "Title", TL(PAD + 110, 284, W - 2 * PAD - 110, 18))), "Your captain", "Label", "NeutralLightest")
    bars = win.child(N("Window/Voice", "Voice", TL(PAD, 314, BARS * 8, 26)))
    for i in range(BARS):
        image(bars, f"Window/Voice/Bar{i}", f"Bar{i}", R((0, 0.5), None, (i * 8 + 2, 0), (4, 26), (0.5, 0.5)), "TextLight")
    H.label(win.child(N("Window/Line", "Line", TL(PAD, 356, W - 2 * PAD, 120))), "", "Body", "TextLight", valign="top", wrap=True)
    prompt = win.child(N("Window/Prompt", "Prompt", R((0, 0), None, (PAD, PAD), (W - 2 * PAD, 30), (0, 0))))
    prompt.add("cg", 225, H.canvas_group(False))
    cap = prompt.child(N("Window/Prompt/Keycap", "Keycap", R((0, 0.5), None, (0, 0), (30, 26), (0, 0.5))))
    image(cap, "Window/Prompt/Keycap/Border", "Border", STRETCH, "SurfaceLight", K.kit_sprite("kit_sliver10_line1"), sliced=True)
    H.label(cap.child(N("Window/Prompt/Keycap/Label", "Label", STRETCH)), "A", "Label", "TextLight", align="center")
    H.label(prompt.child(N("Window/Prompt/Action", "Action", R((0, 0.5), None, (42, 0), (W - 2 * PAD - 42, 26), (0, 0.5)))),
            "", "Button", "Cta")
    image(root, "Pointer", "Pointer", R((0, 0), None, (0, 0), (10, 2), (0, 0.5)), "Cta")
    root.add("view", 114, lambda ctx, go: K.mono(go, VIEW_SCRIPT,
             f"  window: {{fileID: {ctx.rt('Window')}}}\n"
             f"  portrait: {{fileID: {ctx.c('Window/Band/Portrait', 'img')}}}\n"
             f"  eyebrowText: {{fileID: {ctx.c('Window/Eyebrow', 'tmp')}}}\n"
             f"  nameText: {{fileID: {ctx.c('Window/Name', 'tmp')}}}\n"
             f"  titleText: {{fileID: {ctx.c('Window/Title', 'tmp')}}}\n"
             f"  lineText: {{fileID: {ctx.c('Window/Line', 'tmp')}}}\n"
             f"  prompt: {{fileID: {ctx.c('Window/Prompt', 'cg')}}}\n"
             f"  keycapText: {{fileID: {ctx.c('Window/Prompt/Keycap/Label', 'tmp')}}}\n"
             f"  actionText: {{fileID: {ctx.c('Window/Prompt/Action', 'tmp')}}}\n"
             + K.refs_list("voiceBars", [ctx.rt(f"Window/Voice/Bar{i}") for i in range(BARS)])
             + f"  pointer: {{fileID: {ctx.rt('Pointer')}}}\n"
             "  edgeMargin: 40\n  bottomMargin: 120\n  topMargin: 110\n"))
    return root


def speaker_text():
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {SPEAKER_SCRIPT}, type: 3}}\n  m_Name: GuideSpeaker_Trace\n"
            "  m_EditorClassIdentifier: \n  speakerName: Trace\n  speakerTitle: Your captain\n  eyebrow: Incoming\n"
            f"  portrait: {{fileID: 21300000, guid: {PORTRAIT}, type: 3}}\n"
            f"  commsPrefab: {{fileID: {F('root#view')}, guid: {asset_guid(VIEW)}, type: 3}}\n"
            "  charactersPerSecond: 45\n  slideSeconds: 0.5\n")


def yq(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"' if re.search(r"[:'#]", s) else s


def phase_text():
    text = open(os.path.join(REPO, PHASE), encoding="utf-8").read()
    m = re.search(r"(--- !u!114 &-?\d+\nMonoBehaviour:\n(?:  .*\n)*?  m_Script: \{fileID: 11500000, guid: " + NODE_SCRIPT
                  + r", type: 3\}\n(?:  .*\n)*?)(?=--- |\Z)", text)
    assert m, "the first-login phase no longer holds a QuestGuideToMicrogameNode"
    doc = m.group(1)
    new = doc
    for k, v in LINES.items():
        new = re.sub(rf"^  {k}: .*\n", f"  {k}: {yq(v)}\n", new, count=1, flags=re.M)
    new = re.sub(r"^  (speaker|arcadeAction|cardAction|previewAction|actionKeycap): .*\n", "", new, flags=re.M)
    block = (f"  speaker: {{fileID: 11400000, guid: {asset_guid('GuideSpeaker_Trace')}, type: 2}}\n"
             + "".join(f"  {k}: {v}\n" for k, v in ACTIONS.items()))
    new = new.replace("  skipLabel:", block + "  skipLabel:", 1)
    return text.replace(doc, new, 1)


def outputs():
    return {prefab_path(): P.prefab_text(build_view(), VIEW),
            prefab_path() + ".meta": K.PREFAB_META.format(g=asset_guid(VIEW)),
            OUT_DIR + ".meta": K.FOLDER_META.format(g=asset_guid("folder/Guide")),
            SPEAKER: speaker_text(),
            SPEAKER + ".meta": ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
                           "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % asset_guid("GuideSpeaker_Trace")),
            SPEAKER_DIR + ".meta": K.FOLDER_META.format(g=asset_guid("folder/Speakers")),
            PHASE: phase_text()}


def validate(files):
    view = files[prefab_path()]
    guids = T.known_guids() | P.PACKAGE | {RECT_MASK_2D}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    HUD.A.check_file(prefab_path(), view, guids, minted)
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", view, re.M))
    _, body = HUD.A.doc_of(view, F("root#view"))
    assert body and f"guid: {VIEW_SCRIPT}" in body, "GuideCommsView is missing from the window's root"
    for field in ("window", "portrait", "nameText", "titleText", "lineText", "prompt", "keycapText", "actionText", "pointer"):
        m = re.search(r"^  %s: \{fileID: (-?\d+)\}" % field, body, re.M)
        assert m and m.group(1) in anchors, f"GuideCommsView.{field} is not wired to an object in the prefab"
    bars = re.findall(r"^  - \{fileID: (-?\d+)\}", body.split("  voiceBars:")[1].split("  pointer:")[0], re.M)
    assert len(bars) == BARS and all(b in anchors for b in bars), f"GuideCommsView needs its {BARS} voice bars"
    assert "m_RaycastTarget: 1" not in view, "a comms graphic takes presses: the spotlight's dim must decide what a press reaches"
    sp = files[SPEAKER]
    assert f"  commsPrefab: {{fileID: {F('root#view')}, guid: {asset_guid(VIEW)}, type: 3}}\n" in sp, "the speaker names no comms window"
    assert f"  portrait: {{fileID: 21300000, guid: {PORTRAIT}, type: 3}}\n" in sp, "Trace has no portrait"
    ph = files[PHASE]
    assert f"  speaker: {{fileID: 11400000, guid: {asset_guid('GuideSpeaker_Trace')}, type: 2}}\n" in ph, \
        "the first-login guide has no speaker (Rev 36 B)"
    for k in LINES:
        assert re.search(rf"^  {k}: \S", ph, re.M), f"the first-login guide's {k} is blank"
    assert "  actionKeycap: A\n" in ph, "the guide's prompt names no keycap"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def self_test(files):
    def expect(label_, path, mutate):
        bad = dict(files)
        bad[path] = mutate(bad[path])
        try:
            validate(bad)
        except AssertionError as e:
            print(f"  caught: {label_} ({e})")
            return True
        print(f"  MISSED: {label_}")
        return False
    view = prefab_path()
    cases = [
        ("the guide speaking with no voice", PHASE, lambda t: re.sub(r"^  speaker: .*\n", "  speaker: {fileID: 0}\n", t, count=1, flags=re.M)),
        ("a comms graphic taking presses", view, lambda t: t.replace("m_RaycastTarget: 0", "m_RaycastTarget: 1", 1)),
        ("the line unwired", view, lambda t: re.sub(r"^(  lineText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1, flags=re.M)),
        ("Trace without a face", SPEAKER, lambda t: re.sub(r"^  portrait: .*\n", "  portrait: {fileID: 0}\n", t, flags=re.M)),
        ("a voice bar missing", view, lambda t: re.sub(r"(  voiceBars:\n)  - \{fileID: -?\d+\}\n", r"\1", t, count=1)),
    ]
    ok = all(expect(*c) for c in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print("OK: GuideCommsView, Trace and the first-login guide validate")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        full = os.path.join(REPO, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

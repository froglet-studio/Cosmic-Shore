#!/usr/bin/env python3
"""
Author the style-B DISCONNECT NOTICE (Docs/MENU_REDESIGN_B.md section 100.2) - the "connection lost" window every
scene shows when the device loses its connection - as a prefab, and point DisconnectNoticeConfig at it.

    Assets/_Prefabs/UI Elements/Popups/DisconnectNoticeView.prefab
        The dim and the boot Retry window's look (author_start_flow.retry_window: a slivered window, its corner
        brackets, the "Connection" eyebrow, the title, the line), with RECONNECT (lime, A keycap; selected as the
        notice opens, so A arrives as a submit) and CONTINUE OFFLINE (B keycap, answering the pad's B through
        ControllerButtonPress gated by the window's own group). DisconnectNoticeView names the parts;
        DisconnectNotice fills every word from the config and fades the window's group.

    Assets/Resources/DisconnectNoticeConfig.asset
        WindowPrefab names the view. The legacy palette and layout fields stay (an empty WindowPrefab builds the
        old panel), so nothing else in the asset changes.

    python3 Tools/Build/author_disconnect_notice.py              # write
    python3 Tools/Build/author_disconnect_notice.py --check      # fail if any output drifted
    python3 Tools/Build/author_disconnect_notice.py --self-test  # planted defects must all be caught
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

HUD, P, K, H, M, T = SF.HUD, SF.P, SF.K, SF.H, SF.M, SF.T
REPO = K.REPO
N, STRETCH, R, tok, TL = K.Node, K.STRETCH, K.rect, K.tok, SF.TL

VIEW = "DisconnectNoticeView"
OUT_DIR = "Assets/_Prefabs/UI Elements/Popups"
CONFIG = "Assets/Resources/DisconnectNoticeConfig.asset"
VIEW_SCRIPT = HUD.script_guid("Assets/_Scripts/UI/Elements/DisconnectNoticeView.cs")
KITBUTTON = HUD.S["kitbutton"]
PAD_EAST = 5                                    # UnityEngine.InputSystem.LowLevel.GamepadButton.East (B)
WIN_W, PAD = SF.WIN_W, SF.PAD
BTN_Y = 176
WIN_H = BTN_Y + K.line_height("Button") + 28 + PAD


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path():
    return f"{OUT_DIR}/{VIEW}.prefab"


def F(key):
    return K.FileIds(VIEW)(key)


def button_width(caption, cap):
    return 24 + K.label_width(cap.upper(), "Label") + 24 + 12 + K.label_width(caption.upper(), "Button") + 24


def build_view():
    root = N("root", VIEW, STRETCH)
    root.add("cg", 225, H.canvas_group(True))
    dim = root.child(N("Dim", "Dim", STRETCH))
    dim.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.82), raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", 0.82))
    # No entrance: the view stays enabled for the whole session (DisconnectNotice fades its group), so an entrance
    # would play once at install, unseen.
    w = SF.window("Window", R((0.5, 0.5), None, (0, 0), (WIN_W, WIN_H)), "Connection", "NeutralLightest",
                  "Connection Lost", "H2", entrance=False)
    root.children.append(w)
    H.label(w.child(N("Window/Body", "Body", TL(PAD, 104, WIN_W - 2 * PAD, 56))), "", "BodySmall", "NeutralLightest",
            valign="top", wrap=True)
    # RECONNECT is selected as the notice opens, so A reaches it as a submit (no pad poll: it would press twice).
    w.child(SF.plain_kit_button("Window/Reconnect", "ReconnectButton", PAD, BTN_Y, "Play", True, "Reconnect", "A"))
    x = PAD + button_width("Reconnect", "A") + 16
    w.child(HUD.kit_button("Window/Dismiss", "DismissButton", TL(x, BTN_Y, button_width("Continue offline", "B"),
                                                                 K.line_height("Button") + 28),
                           "Cancel", True, "Continue offline", "B", PAD_EAST, "root", "DismissKeycap"))
    root.add("view", 114, lambda ctx, go: K.mono(go, VIEW_SCRIPT,
             f"  group: {{fileID: {ctx.c('root', 'cg')}}}\n"
             f"  title: {{fileID: {ctx.c('Window/Title', 'tmp')}}}\n"
             f"  body: {{fileID: {ctx.c('Window/Body', 'tmp')}}}\n"
             f"  reconnectButton: {{fileID: {ctx.c('Window/Reconnect', 'button')}}}\n"
             f"  reconnectLabel: {{fileID: {ctx.c('Window/Reconnect/Label', 'tmp')}}}\n"
             f"  dismissButton: {{fileID: {ctx.c('Window/Dismiss', 'button')}}}\n"
             f"  dismissLabel: {{fileID: {ctx.c('Window/Dismiss/Label', 'tmp')}}}\n"))
    return root


# Section 103 (Rev 37, approved): the rejoin, dropped-out and host-left words. Written once when absent, so a later edit
# in the inspector is kept (the generator owns the keys being THERE, not their wording).
REJOIN_WORDS = [
    ("TitleDroppedOut", "You dropped out"),
    ("BodyDroppedOut", "Your connection to {mode} broke. An AI pilot took your vessel so your team keeps its seat."),
    ("TitleRejoin", "Rejoin {mode}?"),
    ("BodyRejoin", "You left a match that is still going. An AI pilot has your vessel; rejoin and it is yours again."),
    ("RejoinLabel", "Rejoin"),
    ("NotNowLabel", "Not now"),
    ("TitleHostLeft", "The host left"),
    ("BodyHostLeft", "The match ended when its host disconnected. Nothing you earned in it is lost."),
    ("BackHomeLabel", "Back to Home"),
    ("RejoinCheckSeconds", "2"),
    ("RejoinGiveUpSeconds", "20"),
    ("DroppedOutRecentSeconds", "180"),
]


def yq(v):
    return '"' + v.replace('"', '\\"') + '"' if re.search(r"[:{}?;'#]", v) else v


def config_text():
    text = open(os.path.join(REPO, CONFIG), encoding="utf-8").read()
    line = f"  WindowPrefab: {K.ref(F('root#view'), asset_guid(VIEW), 3)}\n"
    if "  WindowPrefab:" in text:
        text = re.sub(r"^  WindowPrefab: .*\n", line, text, flags=re.M)
    else:
        text = text.replace("  ScrimColor:", line + "  ScrimColor:", 1)
    missing = "".join(f"  {k}: {yq(v)}\n" for k, v in REJOIN_WORDS if not re.search(rf"^  {k}: ", text, re.M))
    if missing:
        text = text.replace("  RestoredDwellSeconds:", missing + "  RestoredDwellSeconds:", 1)
    return text


def outputs():
    return {prefab_path(): P.prefab_text(build_view(), VIEW),
            prefab_path() + ".meta": K.PREFAB_META.format(g=asset_guid(VIEW)),
            OUT_DIR + ".meta": K.FOLDER_META.format(g=asset_guid("folder/Popups")),
            CONFIG: config_text()}


def validate(files):
    view = files[prefab_path()]
    guids = T.known_guids() | P.PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    HUD.A.check_file(prefab_path(), view, guids, minted)
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", view, re.M))
    _, body = HUD.A.doc_of(view, F("root#view"))
    assert body and f"guid: {VIEW_SCRIPT}" in body, "DisconnectNoticeView is missing from the window's root"
    for field in ("group", "title", "body", "reconnectButton", "reconnectLabel", "dismissButton", "dismissLabel"):
        m = re.search(r"^  %s: \{fileID: (-?\d+)\}" % field, body, re.M)
        assert m and m.group(1) in anchors, f"DisconnectNoticeView.{field} is not wired to an object in the prefab"
    for key in ("Window/Reconnect", "Window/Dismiss"):
        _, b = HUD.A.doc_of(view, F(f"{key}#button"))
        assert b and f"guid: {KITBUTTON}" in b, f"{key} is not a KitButton"
    _, rb = HUD.A.doc_of(view, F("Window/Reconnect#button"))
    assert "  variant: %d\n" % K.VARIANTS["Play"] in rb, "RECONNECT must be the lime Play button (CTA)"
    assert not HUD.A.doc_of(view, F("Window/Reconnect#cbp"))[1], "RECONNECT must not poll the pad (A is its submit)"
    _, cbp = HUD.A.doc_of(view, F("Window/Dismiss#cbp"))
    assert cbp and f"  activationButton: {PAD_EAST}\n" in cbp and f"canvasGroup: {{fileID: {F('root#cg')}}}" in cbp, \
        "CONTINUE OFFLINE must answer B, gated by the window's group"
    row = PAD + button_width("Reconnect", "A") + 16 + button_width("Continue offline", "B")
    assert row <= WIN_W - PAD, f"the two buttons ({row:.0f}) run out of the window ({WIN_W - PAD})"
    for t in re.findall(r"^  m_text: (.*)$", view, re.M):
        assert "Tap" not in t, "the window says what happened; the buttons say what to do"
    cfg = files[CONFIG]
    assert f"  WindowPrefab: {{fileID: {F('root#view')}, guid: {asset_guid(VIEW)}, type: 3}}\n" in cfg, \
        "DisconnectNoticeConfig.WindowPrefab must name the view"
    for field in ("TitleConnectionLost", "BodyConnectionLost", "ReconnectLabel", "DismissLabel", "TitleConnectionRestored",
                  "TitleDroppedOut", "TitleRejoin", "RejoinLabel", "NotNowLabel", "TitleHostLeft", "BackHomeLabel"):
        assert re.search(r"^  %s: \S" % field, cfg, re.M), f"DisconnectNoticeConfig.{field} is blank"


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
        ("the config pointing nowhere", CONFIG, lambda t: re.sub(r"^  WindowPrefab: .*\n", "  WindowPrefab: {fileID: 0}\n", t, flags=re.M)),
        ("the title unwired", view, lambda t: re.sub(r"^(  title: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1, flags=re.M)),
        ("B answering the wrong button", view, lambda t: t.replace(f"  activationButton: {PAD_EAST}\n", "  activationButton: 6\n", 1)),
        ("RECONNECT in the cancel grey", view, lambda t: t.replace("  variant: %d\n" % K.VARIANTS["Play"],
                                                                   "  variant: %d\n" % K.VARIANTS["Cancel"], 1)),
        ("the rejoin words missing", CONFIG, lambda t: re.sub(r"^  TitleRejoin: .*\n", "", t, flags=re.M)),
        ("a blank title in the config", CONFIG, lambda t: re.sub(r"^  TitleConnectionLost: .*\n", "  TitleConnectionLost: \n", t, flags=re.M)),
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
    print("OK: DisconnectNoticeView and DisconnectNoticeConfig validate")
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

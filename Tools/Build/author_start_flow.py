#!/usr/bin/env python3
"""
Author ITEM 13 of the style-B menu redesign - the START FLOW - in place in Bootstrap.unity and
Authentication.unity, plus the privacy overlay's sprites (Docs/MENU_REDESIGN_B.md section 26; the
approved StartSplash / StartRetry / StartSignIn / StartSignInFailed / StartName / StartNameTaken /
StartAgeGate / StartConsent boards).

    Bootstrap splash ("Canvas - Splash Screen", the DDOL overlay SceneTransitionManager fades):
        the feature art keeps its place; a dim and a bottom fade go over it. The rotating Loader icon is
        deleted. The bottom readout is a LOADING caption over a 180 x 2 KitLoadBar (the same line the
        boot-world cover draws), with the status line (BootStatusPanel.statusText, restyled) under it.
        The flat RETRY button is replaced by a "Connection lost" panel - eyebrow, title, the request's
        message (BootStatusPanel.retryMessageText) and a kit RETRY button with an A keycap, wired as
        BootStatusPanel.retryButton / retryPanel; BootStatusPanel hides the readout while it is up and
        selects the button for the pad. BootStatusBroadcaster's connection-lost copy loses its "Tap
        retry." - the panel title already says what happened and the button says what to do.
    Sign-in scene (Authentication.unity):
        the same dim; the unreferenced "Status Text" + Loader pair (what players actually saw during the
        auto sign-in: "Loading" and a spinning icon) become the same bottom readout, its line written by
        a BootStatusLine reading the boot status channel the controller already raises on. The auth
        panel becomes a slivered window - "Welcome, pilot" / "Ready to launch" / a body line / a kit PLAY
        AS GUEST button (the controller's guestLoginButton) / the controller's status line, restyled.
        The pilot-name panel becomes a window - "Pilot name" / "What should we call you?" / the input
        field restyled (kit fill, a border that turns Danger while a typed name would be refused, an
        "n / max" count), DisplayNameRulesView's four rule ticks, the controller's error line and a kit
        CONFIRM button. Both panels now ship INACTIVE: the controller shows them (SetActive, kept), and
        a panel that started active flashed at full size for a frame before the controller hid it.
        The kit buttons carry NO ControllerButtonPress: the controller selects each panel's control as
        the panel appears, so A arrives as an EventSystem submit; a pad poll on top fired it twice.
    Rev 9 (Docs/MENU_REDESIGN_B.md §54, the approved R9 boards): the living lattice (Lattice_Window) drifts
        over the feature art on both the splash and the sign-in scene, between the dim and the bottom fade.
        The auth panel's window becomes the sign-in window - SignInChooser's three faces: CHOOSE ("Sign in to
        fly", Continue with Steam / Google / Apple, the "or" rule, Play as guest - the controller's
        guestLoginButton - a hint and the controller's status line), PROGRESS (waiting / did not finish / not
        ready in this build) and AUTO (signed in for you with Steam). The window is top-pivoted: the chooser
        collapses rows the platform does not offer and sets its height. SignInProviders.asset (the per-platform
        order, the silent provider, the stand-in marks) is written here too.
    PrivacyConsentConfig.asset: the overlay's five optional sliver sprites (the overlay builds its
        own UI in code - PrivacyConsentOverlay - so this is the only authored part of it).

STANDING DOWN: every legacy deletion runs only while the legacy object exists; a re-run re-derives the
same output (--check reports no drift).

    python3 Tools/Build/author_start_flow.py              # write
    python3 Tools/Build/author_start_flow.py --check      # fail if any output drifted
    python3 Tools/Build/author_start_flow.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_ingame_hud", os.path.join(HERE, "author_ingame_hud.py"))
HUD = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HUD)

HM, P, C, A, K, T, H, M = HUD.HM, HUD.P, HUD.C, HUD.A, HUD.K, HUD.T, HUD.H, HUD.HM.M
REPO = K.REPO
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL = HM.TL
Patch, set_field, tmp_restyle = HUD.Patch, HUD.set_field, HUD.tmp_restyle

SPLASH = "Assets/_Scenes/Bootstrap.unity"
PROVIDERS_ASSET = "Assets/_SO_Assets/UI/SignInProviders.asset"
AUTH = "Assets/_Scenes/Authentication.unity"
PRIVACY = "Assets/Resources/PrivacyConsentConfig.asset"

S = dict(
    panel=HUD.script_guid("Assets/_Scripts/UI/Screens/BootStatusPanel.cs"),
    broadcaster=HUD.script_guid(next(os.path.join(dp, f)[len(REPO) + 1:] for dp, _, fs in os.walk(os.path.join(REPO, "Assets/_Scripts"))
                                     for f in fs if f == "BootStatusBroadcaster.cs")),
    line=HUD.script_guid("Assets/_Scripts/UI/Screens/BootStatusLine.cs"),
    rules=HUD.script_guid("Assets/_Scripts/UI/Elements/DisplayNameRulesView.cs"),
    auth=HUD.script_guid("Assets/_Scripts/System/AuthenticationSceneController.cs"),
    loadbar=K.kit_script("KitLoadBar"),
    kitbutton=HUD.S["kitbutton"],
    cbp=HUD.S["cbp"],
    loader="96d427922847afd4ca2fcdffed2c2b3b",        # Loader.prefab (the rotating icon)
    chooser=HUD.script_guid("Assets/_Scripts/UI/Screens/SignInChooser.cs"),
    providers_so=HUD.script_guid("Assets/_Scripts/UI/SignInProvidersSO.cs"),
)
PROVIDERS_GUID = "5b0c1f7e9a2d4c6e8f1a3b5d7c9e0f21"          # SignInProviders.asset
PROVIDERS = (("Steam", 2, "chrome_provider_steam"), ("Google", 3, "chrome_provider_google"), ("Apple", 4, "chrome_provider_apple"))
BOOT_STATUS_CHANNEL = "8a2f3b41c9e7456db1d8f9c0a1b2e84b"   # ScriptableEventBootStatusRequest asset

# Bootstrap.unity, measured 2026-10-15.
SP = dict(canvas_go="820208339", panel_comp="820208346", broadcaster="820208347", art_rt="1866429628",
          art_img="1866429629", safe_rt="277081002343380552", status_go="1713505285", status_rt="1713505286",
          status_tmp="1713505288", loader_pid="8514091261509452843", old_retry_go="203003804")
CONNECTION_LOST = "We could not reach the servers. Check your network, then retry."

# Authentication.unity, measured 2026-10-15.
AU = dict(canvas_rt="394550809", bg_rt="1700000002", safe_rt="612275706100289267", ctrl="708444222",
          auth_go="1203594348", auth_rt="1203594349", old_guest_go="1671311812",
          login_status_rt="200000002", login_status_tmp="200000004",
          name_go="832278049", name_rt="832278050", old_confirm_go="532112917",
          field_go="698878738", field_rt="698878739", field_img="698878741", field_comp="698878740",
          area_rt="1460613548", ph_tmp="1657716500", text_tmp="652275683",
          error_rt="1498739106", error_tmp="1498739108",
          old_status_go="1617331190")

WIN_W, PAD = 600, 34
INNER = WIN_W - 2 * PAD
# The sign-in window (R9Chooser): rows from STACK_TOP, ROW_GAP apart; SignInChooser re-lays them at runtime.
STACK_TOP, ROW_GAP, BTN_H, OR_H, HINT_H, STATUS_H = 166, 14, 60, 20, 22, 44
PROGRESS_H, AUTO_H = 360, 250
AUTH_H, NAME_H, RETRY_H = 330, 440, 250
RULES = 4                      # DisplayNameRuleState.Count


# --------------------------------------------------------------------------------------------
# Scene helpers
# --------------------------------------------------------------------------------------------

def father(p, rt):
    return re.search(r"  m_Father: \{fileID: (-?\d+)\}", p.doc(rt)).group(1)


def reparent(p, rt, new_parent):
    """Move an existing RectTransform under `new_parent` (the caller orders new_parent's children)."""
    old = father(p, rt)
    if old != new_parent:
        p.children(old, [k for k in p.kids(old) if k != rt])
        p.replace(rt, re.sub(r"  m_Father: \{fileID: -?\d+\}", f"  m_Father: {{fileID: {new_parent}}}", p.doc(rt), count=1))


def remove_go(p, go):
    """Delete a GameObject subtree and drop it from its parent's children (no-op once gone)."""
    if not p.sc.has(go):
        return
    rt = p.sc.rect_of(go)
    parent = father(p, rt)
    p.remove_subtree(go)
    p.children(parent, [k for k in p.kids(parent) if k != rt])


def remove_instance(p, pid, parent_rt):
    """Delete a nested prefab instance (the PrefabInstance and every stripped stub of it)."""
    if not p.sc.has(pid):
        return
    stubs = [fid for fid, d in p.sc.docs.items() if f"  m_PrefabInstance: {{fileID: {pid}}}" in d and "stripped" in d.split("\n", 1)[0]]
    for fid in C.instance_docs(p.sc, pid):
        if p.sc.has(fid):
            p.sc.remove(fid)
    p.children(parent_rt, [k for k in p.kids(parent_rt) if k not in stubs])


def loader_instances(sc):
    return [fid for fid, d in sc.docs.items() if d.startswith("--- !u!1001 ") and f"guid: {S['loader']}" in d.split("m_SourcePrefab", 1)[-1][:120]]


def set_active(p, go, active):
    p.replace(go, re.sub(r"  m_IsActive: \d", f"  m_IsActive: {1 if active else 0}", p.doc(go), count=1))


def ref(fid):
    return "{fileID: %s}" % fid


def plain_kit_button(k, name, x, y, variant, opaque, caption, cap, active=True):
    """author_ingame_hud.kit_button without its ControllerButtonPress: these buttons are SELECTED as
    their panel appears, so A reaches them as a submit - a pad poll as well would press them twice."""
    kw = K.label_width(cap.upper(), "Label") + 24
    w = 24 + kw + 12 + K.label_width(caption.upper(), "Button") + 24
    h = K.line_height("Button") + 28
    n = HUD.kit_button(k, name, TL(x, y, w, h), variant, opaque, caption, cap, 1)
    n.comps = [c for c in n.comps if c[0] != "cbp"]
    n.active = active
    return n


def vignette_nodes(prefix):
    """The board's dim over the feature art, and a deeper fade along the bottom where the readout sits."""
    dim = N(f"{prefix}/Dim", "Dim", STRETCH)
    dim.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.32))).add("th", 114, H.themed("SurfaceBlack", 0.32))
    fade = N(f"{prefix}/BottomFade", "BottomFade", R((0, 0), (1, 0), (0, 210), (0, 420)))
    fade.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.9), K.kit_sprite("kit_fade_v"))) \
        .add("th", 114, H.themed("SurfaceBlack", 0.9))
    # Rev 9: the living lattice drifts over the dimmed art (shader-only: no CPU, never dirties the canvas).
    return [dim, M.window_lattice_node(f"{prefix}/Lattice"), fade]


def readout_node(prefix, with_line):
    """LOADING over the 180 x 2 KitLoadBar, bottom centre; the sign-in scene's also carries its line."""
    n = N(f"{prefix}/Readout", "Readout", R((0.5, 0), None, (0, 92 if with_line else 76), (420, 64)))
    n.add("cg", 225, H.canvas_group(True))
    H.label(n.child(N(f"{prefix}/Readout/Caption", "Caption", R((0.5, 1), None, (0, -10), (420, 20)))),
            "Loading", "Label", "NeutralLightest", align="center")
    track = n.child(N(f"{prefix}/Readout/Track", "Track", R((0.5, 1), None, (0, -44), (180, 2))))
    track.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight", 0.12))).add("th", 114, H.themed("TextLight", 0.12))
    fill = track.child(N(f"{prefix}/Readout/Track/Fill", "Fill", R((0, 0), (0.22, 1), (0, 0), (0, 0))))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))).add("th", 114, H.themed("ControlActive"))
    track.add("bar", 114, lambda ctx, go: K.mono(go, S["loadbar"], f"  fill: {{fileID: {ctx.rt(prefix + '/Readout/Track/Fill')}}}\n"))
    if with_line:
        H.label(n.child(N(f"{prefix}/Readout/Line", "StatusLine", R((0.5, 0), None, (0, 0), (900, 22)))),
                "", "BodySmall", "NeutralLightest", align="center")
        n.add("line", 114, lambda ctx, go: K.mono(go, S["line"],
              f"  requestEvent: {K.ref(11400000, BOOT_STATUS_CHANNEL, 2)}\n"
              f"  statusText: {{fileID: {ctx.c(prefix + '/Readout/Line', 'tmp')}}}\n"
              f"  readoutGroup: {{fileID: {ctx.c(prefix + '/Readout', 'cg')}}}\n"))
    return n


def window(key, r, eyebrow, eyebrow_token, title, title_role, entrance=True):
    """A slivered style-B window: fill, border, corner brackets, eyebrow and title."""
    w = N(key, "Window", r)
    w.add("cg", 225, H.canvas_group(True))
    if entrance:
        w.add("in", 114, M.entrance("Rise", distance=14))
    H.framed(w, key, 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    M.bracket_nodes(w, f"{key}/Bracket", inset=-6)
    H.label(w.child(N(f"{key}/Eyebrow", "Eyebrow", TL(PAD, 30, WIN_W - 2 * PAD, 20))), eyebrow, "Label", eyebrow_token)
    H.label(w.child(N(f"{key}/Title", "Title", TL(PAD, 56, WIN_W - 2 * PAD, 44))), title, title_role, "TextLight")
    return w


def emit_under(p, parent_rt, nodes, after, order):
    """Emit `nodes` under `parent_rt` and set its children to `order(new_rts)`."""
    rts = []
    for n in nodes:
        rts.append(p.emit(n, parent_rt, after))
        after = HM._last_doc(p, n)
    p.children(parent_rt, order(rts))
    return rts


# --------------------------------------------------------------------------------------------
# Bootstrap splash
# --------------------------------------------------------------------------------------------

def splash_ctx():
    return P.Ctx("Bootstrap/StartFlow")


def retry_window():
    w = window("Retry", R((0.5, 0.5), None, (0, -185), (WIN_W, RETRY_H)), "Connection", "NeutralLightest", "Connection lost", "H2")
    msg = w.child(N("Retry/Message", "Message", TL(PAD, 104, WIN_W - 2 * PAD, 48)))
    H.label(msg, "", "BodySmall", "NeutralLightest", valign="top", wrap=True)
    w.child(plain_kit_button("Retry/Button", "RetryButton", PAD, 168, "Play", True, "Retry", "A", active=False))
    return w


def splash_text(text_):
    p = Patch(text_, "Bootstrap/StartFlow")
    assert p.sc.script(SP["panel_comp"]) == S["panel"], "BootStatusPanel moved"
    assert p.sc.script(SP["broadcaster"]) == S["broadcaster"], "BootStatusBroadcaster moved"
    assert p.sc.field(SP["status_go"], "m_Name") == "Status Text", "the splash status line moved"
    remove_instance(p, SP["loader_pid"], SP["safe_rt"])
    remove_go(p, SP["old_retry_go"])

    # The dim rides the art (it bleeds); everything readable rides the safe area.
    vig = vignette_nodes("Splash")
    emit_under(p, SP["art_rt"], vig, SP["art_img"], lambda rts: rts + [k for k in p.kids(SP["art_rt"]) if k not in rts])

    p.rect(SP["status_rt"], R((0.5, 0), None, (0, 40), (900, 22)))
    p.replace(SP["status_tmp"], tmp_restyle(p.doc(SP["status_tmp"]), "Chakra", 15, False, 2, "center", "NeutralLightest"))
    readout, retry = readout_node("Splash", False), retry_window()
    emit_under(p, SP["safe_rt"], [readout, retry], SP["status_tmp"],
               lambda rts: [k for k in p.kids(SP["safe_rt"]) if k not in rts] + rts)
    retry_cg = str(p.ctx.c("Retry", "cg"))
    cg = p.doc(retry_cg)
    for f in ("m_Alpha", "m_Interactable", "m_BlocksRaycasts"):
        cg = C.field_set(cg, f, "0")
    p.replace(retry_cg, cg)

    c = p.ctx.c
    comp = p.doc(SP["panel_comp"])
    comp = set_field(comp, "retryButton", ref(c("Retry/Button", "button")))
    comp = HUD.insert_after_field(comp, "retryButton", "retryPanel", ref(retry_cg))
    comp = HUD.insert_after_field(comp, "retryPanel", "retryMessageText", ref(c("Retry/Message", "tmp")))
    comp = HUD.insert_after_field(comp, "retryMessageText", "readoutGroup", ref(c("Splash/Readout", "cg")))
    p.replace(SP["panel_comp"], comp)
    p.replace(SP["broadcaster"], set_field(p.doc(SP["broadcaster"]), "labelConnectionLost", CONNECTION_LOST))
    return p.text()


# --------------------------------------------------------------------------------------------
# Sign-in scene
# --------------------------------------------------------------------------------------------

def auth_ctx():
    return P.Ctx("Authentication/StartFlow")


def icon_button(k, name, r, variant, caption, icon=None, cap=None, align=3, sub=None):
    """A KitButton like author_ingame_hud.kit_button, without the pad poll (it is SELECTED, so A arrives as a
    submit) and with an optional provider mark before its label, an optional keycap and an optional small line
    at its right end (Play as guest's "Saved on this device only")."""
    border, fillc, content = K.button_palette(variant, False)
    n = N(k, name, r)
    ring = n.child(N(f"{k}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10))))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Cta", 0.0), K.kit_sprite("kit_sliver10_line2"), True)) \
        .add("le", 114, H.le(ignore=True))
    fill = n.child(N(f"{k}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)) \
        .add("le", 114, H.le(ignore=True))
    bord = n.child(N(f"{k}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)) \
        .add("le", 114, H.le(ignore=True))
    content_keys = []
    if icon:
        mark = n.child(N(f"{k}/Mark", "Mark", R(size=(26, 26))))
        mark.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(content, K.chrome_sprite(icon), preserve=True)) \
            .add("le", 114, H.le(pw=26, ph=26))
        content_keys.append((f"{k}/Mark", "img"))
    if cap:
        kw, kh = K.label_width(cap.upper(), "Label") + 24, K.line_height("Label") + 8
        slot = n.child(N(f"{k}/KeycapSlot", "KeycapSlot", R(size=(kw, kh))))
        slot.add("le", 114, H.le(pw=kw, ph=kh))
        slot.children.append(P.Inst(k.replace("/", "") + "Keycap", "KitKeycap", "Keycap", STRETCH, [("Label#tmp", "m_text", cap, None)]))
    lab = n.child(N(f"{k}/Label", "Label", R(size=(K.label_width(caption.upper(), "Button"), K.line_height("Button")))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(caption, "Button", content)) \
        .add("kt", 114, K.kit_text("Button", driven=True))
    content_keys.insert(0, (f"{k}/Label", "tmp"))
    if sub:
        sl = n.child(N(f"{k}/Sub", "Sub", R((1, 0.5), None, (-24, 0), (220, 20), (1, 0.5))))
        H.label(sl, sub, "BodySmall", "NeutralLightest", align="right")
        sl.add("le", 114, H.le(ignore=True))
    n.add("button", 114, lambda ctx, go: K.mono(go, S["kitbutton"],
        K.SELECTABLE_FIELDS
        + f"  m_TargetGraphic: {{fileID: {ctx.c(f'{k}/Fill', 'img')}}}\n"
        "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"
        f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {{fileID: {ctx.c(f'{k}/FocusRing', 'img')}}}\n"
        f"  variant: {K.VARIANTS[variant]}\n  fill: 0\n"
        f"  fillGraphic: {{fileID: {ctx.c(f'{k}/Fill', 'img')}}}\n"
        f"  borderGraphic: {{fileID: {ctx.c(f'{k}/Border', 'img')}}}\n"
        + K.refs_list("contentGraphics", [ctx.c(a, b) for a, b in content_keys])
        + "  playGradient: {fileID: 0}\n"))
    n.add("hlg", 114, K.hv_layout(False, (24, 24, 0, 0), 16, align, (1, 1), (0, 0)))
    return n


def bare_window(key, r):
    """A slivered style-B window: fill, border and corner brackets (author_start_flow.window without its
    eyebrow and title - each of the sign-in window's faces carries its own)."""
    w = N(key, "Window", r)
    w.add("cg", 225, H.canvas_group(True))
    w.add("in", 114, M.entrance("Rise", distance=14))
    H.framed(w, key, 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    M.bracket_nodes(w, f"{key}/Bracket", inset=-6)
    return w


def face(w, key, shown):
    f = w.child(N(key, key.split("/")[-1], STRETCH))
    cg = H.canvas_group(True)
    f.add("cg", 225, cg)
    return f


def heading(f, key, eyebrow, eyebrow_token, title, title_role, body=None, body_h=46):
    H.label(f.child(N(f"{key}/Eyebrow", "Eyebrow", TL(PAD, 30, INNER, 20))), eyebrow, "Label", eyebrow_token)
    H.label(f.child(N(f"{key}/Title", "Title", TL(PAD, 56, INNER, 44))), title, title_role, "TextLight")
    if body is not None:
        H.label(f.child(N(f"{key}/Body", "Body", TL(PAD, 108, INNER, body_h))), body, "BodySmall", "NeutralLightest", valign="top", wrap=True)


def spinner_line(f, key, top):
    row = f.child(N(f"{key}/Waiting", "Waiting", TL(PAD, top, INNER, 22)))
    sp = row.child(N(f"{key}/Waiting/Spinner", "Spinner", R((0, 0.5), None, (9, 0), (18, 16))))
    sp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Selection"), K.kit_sprite("kit_hex_line1"), preserve=True)) \
        .add("spin", 114, M.ambient("Spin", period=1.1, amplitude=0))
    H.label(row.child(N(f"{key}/Waiting/Line", "Line", R((0, 0.5), (1, 0.5), (14, 0), (-28, 22)))), "", "BodySmall", "NeutralLightest")
    return row


def chooser_rows():
    """The CHOOSE face's rows below the body, top-down, as the board lays them out with every provider shown."""
    y, rows = STACK_TOP, []
    for name, _, _ in PROVIDERS:
        rows.append((f"SignIn/Choose/{name}", y, BTN_H))
        y += BTN_H + ROW_GAP
    for key, h in (("SignIn/Choose/Or", OR_H), ("SignIn/Choose/Guest", BTN_H), ("SignIn/Choose/Hint", HINT_H), ("status", STATUS_H)):
        rows.append((key, y, h))
        y += h + ROW_GAP
    return rows, y - ROW_GAP + PAD


def auth_window():
    rows, height = chooser_rows()
    at = {k: y for k, y, _ in rows}
    w = bare_window("SignIn", R((0.5, 0.5), None, (0, height / 2 - 80), (WIN_W, height), (0.5, 1)))

    ch = face(w, "SignIn/Choose", True)
    heading(ch, "SignIn/Choose", "Welcome, pilot", "Cta", "Sign in to fly", "H1",
            "Your pilot, crystals and vessels follow you to every device you sign in on.")
    for name, _, icon in PROVIDERS:
        ch.child(icon_button(f"SignIn/Choose/{name}", f"{name}Button", TL(PAD, at[f"SignIn/Choose/{name}"], INNER, BTN_H),
                             "Default", f"Continue with {name}", icon=icon))
    orr = ch.child(N("SignIn/Choose/Or", "Or", TL(PAD, at["SignIn/Choose/Or"], INNER, OR_H)))
    for side, amin, amax, pos in (("L", (0, 0.5), (0.5, 0.5), (-14, 0)), ("R", (0.5, 0.5), (1, 0.5), (14, 0))):   # 24 px clear round "or"
        rule = orr.child(N(f"SignIn/Choose/Or/{side}", f"Rule{side}", R(amin, amax, (pos[0] * 6 / 7, 0), (-24, 1))))
        rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    H.label(orr.child(N("SignIn/Choose/Or/Word", "Word", R((0.5, 0.5), None, (0, 0), (40, 20)))), "or", "Label", "NeutralLightest", align="center")
    ch.child(icon_button("SignIn/Choose/Guest", "GuestLoginButton", TL(PAD, at["SignIn/Choose/Guest"], INNER, BTN_H),
                         "Default", "Play as guest", icon="chrome_person", sub="Saved on this device only"))
    H.label(ch.child(N("SignIn/Choose/Hint", "Hint", TL(PAD, at["SignIn/Choose/Hint"], INNER, HINT_H))),
            "You can link an account later in Settings > Account.", "BodySmall", "NeutralLightest")

    pr = face(w, "SignIn/Progress", False)
    heading(pr, "SignIn/Progress", "Google", "Cta", "Finish in your browser", "H2",
            "A Google sign-in page opened in your browser. Choose your account there, then come back here.", body_h=70)
    spinner_line(pr, "SignIn/Progress", 196)
    pr.child(icon_button("SignIn/Progress/Primary", "PrimaryButton", TL(PAD, 236, 240, 52), "Play", "Try again", cap="A", align=4))
    pr.child(icon_button("SignIn/Progress/Secondary", "SecondaryButton", TL(PAD + 256, 236, 240, 52), "Space", "Other ways", align=4))
    pr.child(icon_button("SignIn/Progress/Guest", "GuestLink", TL(PAD, 300, 260, 36), "Cancel", "Play as guest", icon="chrome_person"))

    au = face(w, "SignIn/Auto", False)
    H.label(au.child(N("SignIn/Auto/Eyebrow", "Eyebrow", TL(PAD, 30, INNER, 20))), "Welcome back, pilot", "Label", "Cta")
    mk = au.child(N("SignIn/Auto/Mark", "Mark", TL(PAD, 66, 64, 64)))
    mk.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.chrome_sprite("chrome_provider_steam"), preserve=True))
    H.label(au.child(N("SignIn/Auto/Name", "Name", TL(PAD + 84, 70, INNER - 84, 34))), "", "H2", "TextLight")
    H.label(au.child(N("SignIn/Auto/Caption", "Caption", TL(PAD + 84, 108, INNER - 84, 20))), "Steam account", "Label", "Cta")
    spinner_line(au, "SignIn/Auto", 152)
    au.child(icon_button("SignIn/Auto/OtherWays", "OtherWaysButton", TL(PAD, 190, 300, 36), "Cancel", "Other ways to sign in"))

    w.add("chooser", 114, lambda ctx, go: K.mono(go, S["chooser"], chooser_body(ctx)))
    return w


def chooser_body(ctx):
    c = ctx.c
    rows, _ = chooser_rows()
    trailing = [k for k, _, _ in rows if k in ("SignIn/Choose/Hint", "status")]
    return (f"  providers: {K.ref(11400000, PROVIDERS_GUID, 2)}\n"
            f"  window: {{fileID: {ctx.rt('SignIn')}}}\n"
            f"  stackTop: {STACK_TOP}\n  rowGap: {ROW_GAP}\n  bottomPadding: {PAD}\n"
            f"  progressHeight: {PROGRESS_H}\n  autoHeight: {AUTO_H}\n"
            f"  chooseGroup: {{fileID: {c('SignIn/Choose', 'cg')}}}\n"
            "  providerButtons:\n" + "".join(
                f"  - provider: {v}\n    button: {{fileID: {c(f'SignIn/Choose/{n}', 'button')}}}\n"
                f"    mark: {{fileID: {c(f'SignIn/Choose/{n}/Mark', 'img')}}}\n" for n, v, _ in PROVIDERS)
            + f"  orRule: {{fileID: {ctx.rt('SignIn/Choose/Or')}}}\n"
            f"  guestRow: {{fileID: {ctx.rt('SignIn/Choose/Guest')}}}\n"
            "  trailingRows:\n" + "".join(
                f"  - {{fileID: {AU['login_status_rt'] if k == 'status' else ctx.rt(k)}}}\n" for k in trailing)
            + f"  progressGroup: {{fileID: {c('SignIn/Progress', 'cg')}}}\n"
            f"  progressEyebrow: {{fileID: {c('SignIn/Progress/Eyebrow', 'tmp')}}}\n"
            f"  progressTitle: {{fileID: {c('SignIn/Progress/Title', 'tmp')}}}\n"
            f"  progressBody: {{fileID: {c('SignIn/Progress/Body', 'tmp')}}}\n"
            f"  progressLine: {{fileID: {c('SignIn/Progress/Waiting/Line', 'tmp')}}}\n"
            f"  spinner: {{fileID: {ctx.go('SignIn/Progress/Waiting/Spinner')}}}\n"
            f"  primaryButton: {{fileID: {c('SignIn/Progress/Primary', 'button')}}}\n"
            f"  primaryLabel: {{fileID: {c('SignIn/Progress/Primary/Label', 'tmp')}}}\n"
            f"  secondaryButton: {{fileID: {c('SignIn/Progress/Secondary', 'button')}}}\n"
            f"  secondaryLabel: {{fileID: {c('SignIn/Progress/Secondary/Label', 'tmp')}}}\n"
            f"  guestLinkButton: {{fileID: {c('SignIn/Progress/Guest', 'button')}}}\n"
            f"  autoGroup: {{fileID: {c('SignIn/Auto', 'cg')}}}\n"
            f"  autoMark: {{fileID: {c('SignIn/Auto/Mark', 'img')}}}\n"
            f"  autoName: {{fileID: {c('SignIn/Auto/Name', 'tmp')}}}\n"
            f"  autoCaption: {{fileID: {c('SignIn/Auto/Caption', 'tmp')}}}\n"
            f"  autoLine: {{fileID: {c('SignIn/Auto/Waiting/Line', 'tmp')}}}\n"
            f"  otherWaysButton: {{fileID: {c('SignIn/Auto/OtherWays', 'button')}}}\n")


def providers_asset():
    """SignInProviders.asset: the platforms' orders (Steam / Google / Apple on desktop; Apple first on iPhone,
    Google first on Android), Steam as the desktop's silent provider, the stand-in marks."""
    def order(name, vals):
        return f"  {name}:\n" + "".join(f"  - {v}\n" for v in vals)
    marks = {n: K.sprite_ref(K.chrome_sprite(icon)) for n, _, icon in PROVIDERS}
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {S['providers_so']}, type: 3}}\n"
            "  m_Name: SignInProviders\n  m_EditorClassIdentifier: \n"
            + order("desktopOrder", (2, 3, 4)) + order("iPhoneOrder", (4, 3)) + order("androidOrder", (3, 4)) + order("webOrder", (3, 4))
            + "  desktopSilentProvider: 2\n"
            f"  steamMark: {marks['Steam']}\n  googleMark: {marks['Google']}\n  appleMark: {marks['Apple']}\n"
            "  showUnavailableInDevBuilds: 1\n  editorPreview: 0\n")


PROVIDERS_META = ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % PROVIDERS_GUID)


def name_window():
    w = window("Name", R((0.5, 0.5), None, (0, 30), (WIN_W, NAME_H)), "Pilot name", "NeutralLightest", "What should we call you?", "H2")
    for i in range(RULES):
        row = w.child(N(f"Name/Rule{i}", f"Rule{i}", TL(PAD, 182 + i * 26, WIN_W - 2 * PAD, 22)))
        mark = row.child(N(f"Name/Rule{i}/Mark", "Mark", R((0, 0.5), None, (7, 0), (14, 12))))
        mark.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.kit_sprite("kit_hex_line1"), preserve=True))
        H.label(row.child(N(f"Name/Rule{i}/Label", "Label", R((0, 0.5), (1, 0.5), (12, 0), (-24, 22)))), "", "BodySmall", "NeutralLightest")
    w.child(plain_kit_button("Name/Confirm", "ConfirmUsernameButton", PAD, 352, "Play", True, "Confirm", "A"))
    w.add("rules", 114, lambda ctx, go: K.mono(go, S["rules"],
          f"  input: {{fileID: {AU['field_comp']}}}\n  theme: {K.ref(11400000, K.THEME, 2)}\n"
          + K.refs_list("ruleLabels", [ctx.c(f"Name/Rule{i}/Label", "tmp") for i in range(RULES)])
          + K.refs_list("ruleMarks", [ctx.c(f"Name/Rule{i}/Mark", "img") for i in range(RULES)])
          + f"  ruleMetSprite: {K.sprite_ref(K.kit_sprite('kit_hex_fill'))}\n"
          f"  ruleOpenSprite: {K.sprite_ref(K.kit_sprite('kit_hex_line1'))}\n"
          f"  countText: {{fileID: {ctx.c('Name/Count', 'tmp')}}}\n"
          f"  fieldBorder: {{fileID: {ctx.c('Name/FieldBorder', 'img')}}}\n"))
    return w


def field_parts():
    """Children added to the existing input field: its border and the n / max count."""
    bord = N("Name/FieldBorder", "Border", STRETCH)
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"), K.kit_sprite("kit_sliver10_line1"), True))
    count = N("Name/Count", "Count", R((1, 0.5), None, (-18, 0), (80, 24), (1, 0.5)))
    H.label(count, "0 / 25", "DataSmall", "NeutralLightest", align="right")
    return [bord, count]


def auth_text(text_):
    p = Patch(text_, "Authentication/StartFlow")
    I = AU
    assert p.sc.script(I["ctrl"]) == S["auth"], "AuthenticationSceneController moved"
    assert p.sc.field(I["auth_go"], "m_Name") == "AuthPanel" and p.sc.field(I["name_go"], "m_Name") == "UsernameSetupPanel", "panels moved"
    for pid in loader_instances(p.sc):
        remove_instance(p, pid, I["safe_rt"])
    remove_go(p, I["old_status_go"])
    remove_go(p, I["old_guest_go"])
    remove_go(p, I["old_confirm_go"])

    emit_under(p, I["canvas_rt"], vignette_nodes("Auth"), "1700000004",
               lambda rts: [I["bg_rt"]] + rts + [k for k in p.kids(I["canvas_rt"]) if k not in rts and k != I["bg_rt"]])
    readout = readout_node("Auth", True)
    emit_under(p, I["safe_rt"], [readout], "979943021161530881",
               lambda rts: rts + [k for k in p.kids(I["safe_rt"]) if k not in rts])

    # Auth panel: the sign-in window (Rev 9), then the controller's status line moved into its CHOOSE face.
    # The section-26 window ("Auth") is retired first, its status line lifted out so it survives.
    legacy = str(p.ctx.go("Auth"))
    if p.sc.has(legacy):
        reparent(p, I["login_status_rt"], I["auth_rt"])
        remove_go(p, legacy)
    aw = auth_window()
    (win_rt,) = emit_under(p, I["auth_rt"], [aw], I["auth_rt"], lambda rts: rts)
    for f in ("SignIn/Progress", "SignIn/Auto"):
        cg_ = str(p.ctx.c(f, "cg"))
        d = p.doc(cg_)
        for fld in ("m_Alpha", "m_Interactable", "m_BlocksRaycasts"):
            d = C.field_set(d, fld, "0")
        p.replace(cg_, d)
    choose_rt = str(p.ctx.rt("SignIn/Choose"))
    reparent(p, I["login_status_rt"], choose_rt)
    status_y = dict((k, y) for k, y, _ in chooser_rows()[0])["status"]
    p.rect(I["login_status_rt"], TL(PAD, status_y, INNER, STATUS_H))
    st = tmp_restyle(p.doc(I["login_status_tmp"]), "Chakra", 15, False, 2, "left", "NeutralLightest")
    p.replace(I["login_status_tmp"], C.field_set(st, "m_TextWrappingMode", "1"))
    p.children(choose_rt, p.kids(choose_rt) + [I["login_status_rt"]])

    # Pilot-name panel: the window; the field and the error line move into it.
    nw = name_window()
    (nwin_rt,) = emit_under(p, I["name_rt"], [nw], I["name_rt"], lambda rts: rts)
    reparent(p, I["field_rt"], nwin_rt)
    p.rect(I["field_rt"], TL(PAD, 110, WIN_W - 2 * PAD, 58))
    p.body(I["field_img"], HM.img_body(tok("SurfaceBlack"), K.kit_sprite("kit_sliver10_fill"), sliced=True, raycast=True), I["field_go"])
    p.rect(I["area_rt"], R((0, 0), (1, 1), (-36, 0), (-108, -16)))
    for fid, color in ((I["text_tmp"], "TextLight"), (I["ph_tmp"], "SurfaceLight")):
        d = tmp_restyle(p.doc(fid), "Aldrich", 22, False, 0, "left", color)
        p.replace(fid, d)
    p.replace(I["ph_tmp"], C.field_set(p.doc(I["ph_tmp"]), "m_text", "Pilot name"))
    parts = field_parts()
    emit_under(p, I["field_rt"], parts, I["field_comp"], lambda rts: [rts[0]] + [k for k in p.kids(I["field_rt"]) if k not in rts] + [rts[1]])
    reparent(p, I["error_rt"], nwin_rt)
    p.rect(I["error_rt"], TL(PAD, 290, WIN_W - 2 * PAD, 44))
    er = tmp_restyle(p.doc(I["error_tmp"]), "Chakra", 15, False, 2, "left", "Danger")
    p.replace(I["error_tmp"], C.field_set(er, "m_TextWrappingMode", "1"))
    kids = [k for k in p.kids(nwin_rt) if k not in (I["field_rt"], I["error_rt"])]
    title_at = kids.index(str(p.ctx.rt("Name/Title"))) + 1
    p.children(nwin_rt, kids[:title_at] + [I["field_rt"]] + kids[title_at:] + [I["error_rt"]])

    set_active(p, I["auth_go"], False)
    set_active(p, I["name_go"], False)
    c = p.ctx.c
    comp = p.doc(I["ctrl"])
    comp = set_field(comp, "guestLoginButton", ref(c("SignIn/Choose/Guest", "button")))
    if not re.search(r"^  signInChooser: ", comp, re.M):
        comp = HUD.insert_after_field(comp, "statusText", "signInChooser", ref(c("SignIn", "chooser")))
    comp = set_field(comp, "signInChooser", ref(c("SignIn", "chooser")))
    comp = set_field(comp, "confirmUsernameButton", ref(c("Name/Confirm", "button")))
    p.replace(I["ctrl"], comp)
    return p.text()


# --------------------------------------------------------------------------------------------
# Privacy overlay sprites
# --------------------------------------------------------------------------------------------

PRIVACY_SPRITES = [("panelFillSprite", "kit_sliver14_fill"), ("panelBorderSprite", "kit_sliver14_line1"),
                   ("buttonFillSprite", "kit_sliver10_fill"), ("buttonBorderSprite", "kit_sliver10_line1"),
                   ("focusRingSprite", "kit_sliver10_line2")]


def privacy_text(text_):
    for f, sprite in PRIVACY_SPRITES:
        text_ = set_field(text_, f, K.sprite_ref(K.kit_sprite(sprite)))
    return text_


# --------------------------------------------------------------------------------------------
# Outputs and validation
# --------------------------------------------------------------------------------------------

TRANSFORMS = {SPLASH: splash_text, AUTH: auth_text, PRIVACY: privacy_text}


def outputs():
    files = {path: fn(open(os.path.join(REPO, path), encoding="utf-8").read()) for path, fn in TRANSFORMS.items()}
    files[PROVIDERS_ASSET] = providers_asset()
    files[PROVIDERS_ASSET + ".meta"] = PROVIDERS_META
    return files


def field_ref(doc, name):
    m = re.search(r"^  " + re.escape(name) + r": \{fileID: (-?\d+)", doc, re.M)
    return m.group(1) if m else "0"


def check_scene_links(path, text_):
    sc = T.Scene(text_)
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", text_, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", text_):
        assert m.group(1) in anchors, f"{path}: dangling local fileID {m.group(1)}"
    for fid, d in sc.docs.items():
        if not d.startswith("--- !u!224 ") or "stripped" in d.split("\n", 1)[0]:
            continue
        for kid in re.findall(r"\{fileID: (-?\d+)\}", (re.search(r"^  m_Children:\n((?:  - .*\n)*)", d, re.M) or [None, ""])[1] or ""):
            kd = sc.docs[kid]
            if "stripped" in kd.split("\n", 1)[0]:
                pid = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)\}", kd).group(1)
                assert f"m_TransformParent: {{fileID: {fid}}}" in sc.docs[pid], f"{path}: instance {pid} under {fid} names another parent"
            else:
                assert f"  m_Father: {{fileID: {fid}}}" in kd, f"{path}: child {kid} of {fid} names another father"
    return sc


def validate(files):
    M.require_entrances(files, {SPLASH: 1, AUTH: 2})
    # Bootstrap splash
    sc = check_scene_links(SPLASH, files[SPLASH])
    assert not loader_instances(sc), "the splash still carries the rotating Loader icon"
    assert not sc.has(SP["old_retry_go"]), "the flat RETRY button survives"
    panel = sc.docs[SP["panel_comp"]]
    for f in ("statusText", "retryButton", "retryPanel", "retryMessageText", "readoutGroup"):
        assert field_ref(panel, f) not in ("0", None) and sc.has(field_ref(panel, f)), f"BootStatusPanel.{f} is not wired"
    retry_btn = sc.docs[field_ref(panel, "retryButton")]
    assert f"guid: {S['kitbutton']}" in retry_btn, "the retry button is not a KitButton"
    cg = sc.docs[field_ref(panel, "retryPanel")]
    assert "  m_Alpha: 0\n" in cg and "  m_BlocksRaycasts: 0\n" in cg, "the retry panel ships shown"
    assert f"  labelConnectionLost: {CONNECTION_LOST}\n" in sc.docs[SP["broadcaster"]], "the connection-lost copy still says Tap retry"
    for path, s in ((SPLASH, sc),):
        bars = [d for d in s.docs.values() if f"guid: {S['loadbar']}" in d]
        assert bars and all(field_ref(d, "fill") != "0" for d in bars), f"{path}: a KitLoadBar has no fill"
    # Sign-in scene
    au = check_scene_links(AUTH, files[AUTH])
    assert not loader_instances(au), "the sign-in scene still carries the rotating Loader icon"
    assert not au.has(AU["old_status_go"]), "the unreferenced Loading text survives"
    ctrl = au.docs[AU["ctrl"]]
    for f in ("guestLoginButton", "confirmUsernameButton"):
        d = au.docs.get(field_ref(ctrl, f), "")
        assert f"guid: {S['kitbutton']}" in d, f"AuthenticationSceneController.{f} is not a kit button"
    for f in ("statusText", "usernameInputField", "usernameStatusText", "authPanel", "usernameSetupPanel"):
        assert au.has(field_ref(ctrl, f)), f"AuthenticationSceneController.{f} is not wired"
    for go in (AU["auth_go"], AU["name_go"]):
        assert au.field(go, "m_IsActive") == "0", f"{au.field(go, 'm_Name')} ships active (it flashes before the controller hides it)"
    lines = [d for d in au.docs.values() if f"guid: {S['line']}" in d]
    assert len(lines) == 1, "the sign-in readout has no BootStatusLine"
    assert f"guid: {BOOT_STATUS_CHANNEL}" in lines[0] and field_ref(lines[0], "statusText") != "0" \
        and field_ref(lines[0], "readoutGroup") != "0", "BootStatusLine is not wired to the boot status channel"
    rules = [d for d in au.docs.values() if f"guid: {S['rules']}" in d]
    assert len(rules) == 1, "the pilot-name panel has no DisplayNameRulesView"
    assert f"  input: {{fileID: {field_ref(ctrl, 'usernameInputField')}}}" in rules[0], "the rule ticks judge another field"
    for f in ("ruleLabels", "ruleMarks"):
        got = re.search(r"^  %s:\n((?:  - .*\n)*)" % f, rules[0], re.M)
        assert got and got.group(1).count("{fileID: ") == RULES, f"DisplayNameRulesView.{f} needs {RULES} entries"
    for f in ("countText", "fieldBorder"):
        assert field_ref(rules[0], f) != "0", f"DisplayNameRulesView.{f} is not wired"
    bars = [d for d in au.docs.values() if f"guid: {S['loadbar']}" in d]
    assert bars and all(field_ref(d, "fill") != "0" for d in bars), f"{AUTH}: a KitLoadBar has no fill"
    for path, s in ((SPLASH, sc), (AUTH, au)):
        for fid, d in s.docs.items():
            if f"guid: {S['kitbutton']}" in d:
                go = s.go_of(fid)
                assert not any(f"guid: {S['cbp']}" in s.docs.get(c, "") for c in s.components(go)), \
                    f"{path}: a start-flow button also polls the pad (it would press twice)"
    # Rev 9: the lattice over the art, in both scenes, between the dim and the bottom fade.
    for path, s_ in ((SPLASH, sc), (AUTH, au)):
        lat = [d for d in s_.docs.values() if f"guid: {M.LATTICE_WINDOW_MAT}, type: 2" in d]
        assert lat, f"{path}: no living lattice over the feature art"
    # Rev 9: the sign-in window.
    choosers = [(fid, d) for fid, d in au.docs.items() if f"guid: {S['chooser']}" in d]
    assert len(choosers) == 1, "the sign-in window has no SignInChooser"
    cfid, cd = choosers[0]
    assert field_ref(ctrl, "signInChooser") == cfid, "AuthenticationSceneController.signInChooser is not the window's chooser"
    assert f"  providers: {K.ref(11400000, PROVIDERS_GUID, 2)}" in cd, "SignInChooser reads another providers asset"
    for f in ("window", "chooseGroup", "progressGroup", "autoGroup", "orRule", "guestRow", "primaryButton", "secondaryButton",
              "guestLinkButton", "progressLine", "spinner", "autoName", "otherWaysButton"):
        assert field_ref(cd, f) != "0" and au.has(field_ref(cd, f)), f"SignInChooser.{f} is not wired"
    buttons = re.findall(r"  - provider: (\d+)\n    button: \{fileID: (-?\d+)\}", cd)
    assert sorted(int(v) for v, _ in buttons) == [2, 3, 4], "SignInChooser needs one button each for Steam, Google and Apple"
    for _, b in buttons:
        assert f"guid: {S['kitbutton']}" in au.docs.get(b, ""), "a provider button is not a KitButton"
    trailing = re.search(r"^  trailingRows:\n((?:  - .*\n)*)", cd, re.M)
    assert trailing and f"{{fileID: {AU['login_status_rt']}}}" in trailing.group(1), "the controller's status line is not a chooser row"
    for f in ("progressGroup", "autoGroup"):
        g = au.docs[field_ref(cd, f)]
        assert "  m_Alpha: 0\n" in g and "  m_BlocksRaycasts: 0\n" in g, f"SignInChooser.{f} ships shown"
    guest_go = au.go_of(field_ref(ctrl, "guestLoginButton"))
    choose_rt = au.rect_of(au.go_of(field_ref(cd, "chooseGroup")))
    assert f"  m_Father: {{fileID: {choose_rt}}}" in au.docs[au.rect_of(guest_go)], "Play as guest is not on the CHOOSE face"
    # SignInProviders.asset
    a = files[PROVIDERS_ASSET]
    assert f"guid: {S['providers_so']}" in a and f"guid: {PROVIDERS_GUID}" in files[PROVIDERS_ASSET + ".meta"], "SignInProviders.asset is not a SignInProvidersSO"
    assert "  desktopOrder:\n  - 2\n  - 3\n  - 4\n" in a and "  iPhoneOrder:\n  - 4\n" in a and "  androidOrder:\n  - 3\n" in a, \
        "the platform orders differ from the approved boards (Steam, Google, Apple; Apple first on iPhone; Google first on Android)"
    assert "  desktopSilentProvider: 2\n" in a, "Steam no longer signs the player in on its own on desktop"
    for n in ("steamMark", "googleMark", "appleMark"):
        assert re.search(r"^  %s: \{fileID: 21300000, guid: \w{32}, type: 3\}" % n, a, re.M), f"SignInProviders.{n} has no sprite"
    # Privacy
    for f, sprite in PRIVACY_SPRITES:
        assert f"  {f}: {K.sprite_ref(K.kit_sprite(sprite))}" in files[PRIVACY], f"PrivacyConsentConfig.{f} is not set"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def _show_face(text_, field):
    cg = re.search(r"^  %s: \{fileID: (-?\d+)\}" % field, text_, re.M).group(1)
    i = text_.index(f" &{cg}\n")
    j = text_.find("\n--- ", i)
    return text_[:i] + text_[i:j].replace("  m_Alpha: 0\n", "  m_Alpha: 1\n").replace("  m_BlocksRaycasts: 0\n", "  m_BlocksRaycasts: 1\n") + text_[j:]


def self_test(files):
    def expect(label_, mutate):
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
        except AssertionError as e:
            print(f"  caught: {label_} ({e})")
            return True
        print(f"  MISSED: {label_}")
        return False

    def edit_doc(path, fid, fn):
        def go(f):
            s = f[path]
            i = s.index(f"--- !u!", s.index(f" &{fid}\n") - 12)
            j = s.find("\n--- ", i + 4)
            j = len(s) if j < 0 else j + 1
            f[path] = s[:i] + fn(s[i:j]) + s[j:]
        return go

    def add_cbp(f):
        s = f[AUTH]
        btn = re.search(r"  guestLoginButton: \{fileID: (-?\d+)\}", s).group(1)
        sc = T.Scene(s)
        go = sc.go_of(btn)
        other = next(c for c in sc.components(go) if c != btn and "MonoBehaviour" in sc.docs[c])
        f[AUTH] = s.replace(sc.docs[other], sc.docs[other].replace(re.search(r"guid: (\w+), type: 3", sc.docs[other]).group(1), S["cbp"]), 1)

    cases = [
        ("the sign-in windows' rise stripped", lambda f: f.__setitem__(AUTH, M.strip_entrances(f[AUTH]))),
        ("the retry panel unwired", edit_doc(SPLASH, SP["panel_comp"], lambda d: re.sub(r"(  retryPanel: )\{fileID: -?\d+\}", r"\1{fileID: 0}", d))),
        ("the retry panel ships shown", lambda f: f.__setitem__(SPLASH, f[SPLASH].replace(
            "  m_Alpha: 0\n  m_Interactable: 0\n  m_BlocksRaycasts: 0\n", "  m_Alpha: 1\n  m_Interactable: 1\n  m_BlocksRaycasts: 1\n", 1))),
        ("a load bar with no fill", lambda f: f.__setitem__(AUTH, re.sub(r"(  fill: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[AUTH], count=1))),
        ("the auth panel ships active", edit_doc(AUTH, AU["auth_go"], lambda d: d.replace("  m_IsActive: 0", "  m_IsActive: 1"))),
        ("the guest button also polls the pad", add_cbp),
        ("a privacy sprite missing", lambda f: f.__setitem__(PRIVACY, re.sub(r"(  focusRingSprite: ).*", r"\1{fileID: 0}", f[PRIVACY]))),
        ("the splash lost its lattice", lambda f: f.__setitem__(SPLASH, f[SPLASH].replace(M.LATTICE_WINDOW_MAT, "0" * 32))),
        ("the controller lost its chooser", edit_doc(AUTH, AU["ctrl"], lambda d: re.sub(r"(  signInChooser: )\{fileID: -?\d+\}", r"\1{fileID: 0}", d))),
        ("the PROGRESS face ships shown", lambda f: f.__setitem__(AUTH, _show_face(f[AUTH], "progressGroup"))),
        ("a provider button missing", lambda f: f.__setitem__(AUTH, re.sub(r"  - provider: 4\n    button: \{fileID: -?\d+\}\n    mark: \{fileID: -?\d+\}\n", "", f[AUTH]))),
        ("Steam no longer silent", lambda f: f.__setitem__(PROVIDERS_ASSET, f[PROVIDERS_ASSET].replace("desktopSilentProvider: 2", "desktopSilentProvider: 0"))),
        ("the rule ticks judge another field", lambda f: f.__setitem__(AUTH, re.sub(r"(  input: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[AUTH], count=1))),
    ]
    ok = all(expect(l, m) for l, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print("OK: the start flow validates (Bootstrap splash, sign-in scene, privacy sprites)")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

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
    Rev 9 (Docs/MENU_REDESIGN_B.md §54, the approved R9 boards): the sea (Sea_Window, §70; the living lattice before it) moves
        over the feature art on both the splash and the sign-in scene, between the dim and the bottom fade.
        The auth panel's window becomes the sign-in window - SignInChooser's three faces: CHOOSE ("Sign in to
        fly", Continue with Steam / Google / Apple, the "or" rule, Play as guest - the controller's
        guestLoginButton - a hint and the controller's status line), PROGRESS (waiting / did not finish / not
        ready in this build) and AUTO (signed in for you with Steam). The window is top-pivoted: the chooser
        collapses rows the platform does not offer and sets its height. SignInProviders.asset (the per-platform
        order, the silent provider, the stand-in marks) is written here too.
    Rev 94 C (Docs/MENU_REDESIGN_B.md section 177.2; Docs/MenuRedesignB/rounds/r94_posters.py, board R94C): the
        still feature art, its dim and its bottom fade give way to a POSTER on both the splash and the sign-in scene:
        a real frame of a cell far behind, pushing in, under the sea lines; three slanted domain bands (Jade, Ruby,
        Gold) painted as prisms of the WORN colour set (PosterDomainPaint: face colour body, rim colour edges, read
        at runtime, blended when the set changes - no colour is written here); five captains in slanted cards
        (KitSlant); three hulls in silver, bobbing; Omni crystals, fauna, flora and two runs of prisms in the
        margins (PosterDrift); the flat logos; a dark foot with the Rev 91 bar (BootStepBar, a KitStepBar that
        draws BootLoadProgress, whose shares this script reads from the C# so the bar's steps cannot disagree with
        the model). The poster's art is imported here from Docs/ into Assets/_Graphics/UI/Splash/. The start flow's
        windows become opaque: they no longer sit on a dimmed still.
    PrivacyConsentConfig.asset: the overlay's five optional sliver sprites (the overlay builds its
        own UI in code - PrivacyConsentOverlay - so this is the only authored part of it).

STANDING DOWN: every legacy deletion runs only while the legacy object exists; a re-run re-derives the
same output (--check reports no drift).

    python3 Tools/Build/author_start_flow.py              # write
    python3 Tools/Build/author_start_flow.py --check      # fail if any output drifted
    python3 Tools/Build/author_start_flow.py --self-test  # planted defects must all be caught
"""

import argparse
import hashlib
import importlib.util
import math
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
    loadbar=K.kit_script("KitLoadBar"),        # retired from both scenes by Rev 94 C; the gate keeps it out
    kitbutton=HUD.S["kitbutton"],
    cbp=HUD.S["cbp"],
    loader="96d427922847afd4ca2fcdffed2c2b3b",        # Loader.prefab (the rotating icon)
    chooser=HUD.script_guid("Assets/_Scripts/UI/Screens/SignInChooser.cs"),
    providers_so=HUD.script_guid("Assets/_Scripts/UI/SignInProvidersSO.cs"),
    brand=K.kit_script("KitBrandButton"),
    welcome=HUD.script_guid("Assets/_Scripts/UI/Screens/BootWelcomeCard.cs"),
    stepbar=HUD.script_guid("Assets/_Scripts/UI/Screens/BootStepBar.cs"),
    paint=HUD.script_guid("Assets/_Scripts/UI/Screens/PosterDomainPaint.cs"),
    drift=HUD.script_guid("Assets/_Scripts/UI/Screens/PosterDrift.cs"),
    fit=HUD.script_guid("Assets/_Scripts/UI/Screens/PosterFit.cs"),
    exit=HUD.script_guid("Assets/_Scripts/UI/Screens/PosterExit.cs"),
    slant=K.kit_script("KitSlant"),
    cover=K.kit_script("KitCoverImage"),
)
AUTH_DATA = "875bcedfcf3335047b416297732cba15"            # the AuthenticationData variable the broadcaster reads
WELCOME_H = 168
PROVIDERS_GUID = "5b0c1f7e9a2d4c6e8f1a3b5d7c9e0f21"          # SignInProviders.asset
PROVIDERS = (("Steam", 2, "chrome_provider_steam"), ("Google", 3, "chrome_provider_google"), ("Apple", 4, "chrome_provider_apple"))
# R9b (section 57): the chooser carries the providers' own button artwork, untouched; Steam has no button (a Steam build
# signs in on its own). The files are the providers' downloads byte for byte - Google's branding kit (signin-assets.zip,
# Android + Web / PNG @4x / Dark) and Apple's button image service (appleid.cdn-apple.com/appleid/button) - so their
# SHA-256 is pinned: a re-export, crop or recolour fails --check.
CHOOSER = (("Google", 3), ("Apple", 4))
BRAND_DIR = "Assets/_Graphics/UI/Brands"
BRANDS = {
    "googleButton": ("google_signin_dark_pill.png", "1aa891e26e1d9ff7ad15e7e56874f3e15daa3e85c74c674fa763badc37417b55"),
    "appleButton": ("apple_signin_white_pill.png", "14ab059af981b221b87035665572143623cc439b20ee3fdaef76c5fd6ff84b53"),
    "googleMark": ("google_logo_dark_pill.png", "b3c9cbc019a473dc9fdee0c2e56e7f79eb78275bdbf09d4608d08fae722bdadb"),
    "appleMark": ("apple_logo_white_pill.png", "4adbf6ff7e8a57181984302b4dae004fac942e78be33ccb26de03b94f751c1bb"),
}
BRAND_H = 52
BRAND_W = BRAND_H * 720 // 160   # both button images are 720 x 160: Apple's no smaller than Google's (Apple HIG)


R9B_RETIRED_GOS = ["SignIn/Choose/Steam", "SignIn/Auto/Mark"] + [
    f"SignIn/Choose/{n}/{part}" for n, _ in CHOOSER for part in ("Fill", "Border", "Mark", "Label")]
R9B_RETIRED_COMPONENTS = [(f"SignIn/Choose/{n}", "hlg") for n, _ in CHOOSER] + [(f"SignIn/Choose/{n}/FocusRing", "le") for n, _ in CHOOSER]


def brand_sprite(field):
    return K.meta_guid(f"{BRAND_DIR}/{BRANDS[field][0]}")
BOOT_STATUS_CHANNEL = "8a2f3b41c9e7456db1d8f9c0a1b2e84b"   # ScriptableEventBootStatusRequest asset

# Bootstrap.unity, measured 2026-10-15.
SP = dict(canvas_go="820208339", panel_comp="820208346", broadcaster="820208347", art_rt="1866429628",
          art_img="1866429629", art_go="1866429627", safe_rt="277081002343380552", status_go="1713505285", status_rt="1713505286",
          status_tmp="1713505288", loader_pid="8514091261509452843", old_retry_go="203003804")
CONNECTION_LOST = "We could not reach the servers. Check your network, then retry."

# Authentication.unity, measured 2026-10-15.
AU = dict(canvas_rt="394550809", bg_rt="1700000002", bg_img="1700000004", bg_go="1700000001", safe_rt="612275706100289267", ctrl="708444222",
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
NOTE_H = 40   # R9b: "Steam is not running" above the buttons
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


# --------------------------------------------------------------------------------------------
# Rev 94 C: the poster (Docs/MENU_REDESIGN_B.md section 177.2; board R94C, Docs/MenuRedesignB/rounds/r94_posters.py)
# --------------------------------------------------------------------------------------------
# Positions are the board's, on its 1920 x 1080 page with y down. The foot is the bottom FOOT_H of the screen; the
# STAGE is the board's area above it, 1920 wide, standing on the foot's top edge and scaled down by PosterFit on a
# screen narrower than the board. No colour is typed: art is drawn white (the identity tint), chrome takes theme tokens,
# and the domain colours are read from the worn colour set at runtime (PosterDomainPaint), so a band ships transparent.

SPLASH_DIR = "Assets/_Graphics/UI/Splash"
CAPTAINS = (("Hornet", 400, 230), ("Needle", 470, 260), ("Galleon", 540, 300), ("Sundial", 470, 260), ("Anvil", 400, 230))
CAPTAIN_X = (452, 700, 960, 1220, 1468)        # card centres; the middle card is drawn last, in front
CAPTAIN_ORDER = (0, 4, 1, 3, 2)
CAPTAIN_FOOT = 862                             # the cards' bottom edge on the board: 2 px under the foot's top edge
# The poster's own pictures, imported from Docs/ (file name under SPLASH_DIR -> source). The busts are Garrett's new
# 1024 px captains (the 256 px avatars and the 16:9 pilot scenes in Assets/ are other pictures); the logos are
# thresholded cuts of the neon logos, not drawn marks; the cell is one of the painter's reference frames of the game.
ART = {"poster_cell.jpg": "Docs/MenuRedesignB/build_refs/sea_cell_outside.jpg",
       "poster_logo_cosmic_shore.png": "Docs/MenuRedesignB/build_refs/flat_cosmic_shore.png",
       "poster_logo_froglet.png": "Docs/MenuRedesignB/build_refs/flat_froglet.png"}
ART.update({f"captain_{n.lower()}.jpg": f"Docs/Pilots/art/busts/{n.upper()}.jpg" for n, _, _ in CAPTAINS})
ART_META_DONOR = "Assets/_Graphics/CardImages/Squirrel_large.png.meta"     # a single sprite, no mipmaps
HULLS = (("Squirrel", 500, 20, 9), ("Dolphin", 830, -10, 7), ("Sparrow", 1190, 20, 8))    # name, x, y, bob seconds
HULL_SIZE = 300
DOMAIN_VALUE = {"Jade": 1, "Ruby": 2, "Gold": 4}           # CosmicShore.Data.Domains
BANDS = ("Jade", "Ruby", "Gold")
BAND_X0, BAND_PITCH, BAND_W, BAND_ALPHA, RIM_W = 360, 400, 400, 0.74, 3
BAND_MID = 440                 # the board's bands (top -60, 1000 tall) are centred here; the lean pivots on it
BAND_TOP, BAND_BOTTOM = -4000, 940     # drawn far past the board's top, so a tall screen never shows a band's end
LEAN = 14.0
SLANT = math.tan(math.radians(LEAN))
FOOT_H, STAGE_W = 220, 1920
STAGE_H = 1080 - FOOT_H
POSTER_MARGIN = 96
POSTER_EXIT_PUSH = 0.12      # PosterExit: 12 percent larger when the splash has dissolved
FROGLET_W, FROGLET_BOTTOM = 150, 8   # the Froglet logo under the bar (whose step names sit 72 above the canvas foot)
BLEND_SECONDS = 1.0
WINDOW_FILL = 1.0              # the start flow's windows are opaque: they sit on the poster, not on a dimmed still
BAR_H = 132
STEP_NAMES = ("Connecting", "Signing in", "Loading profile", "Creating session")
DONE_WORD = "Ready"
RETURN_WORDS = "Building the world"
PROGRESS_CS = "Assets/_Scripts/UI/Elements/BootLoadProgress.cs"


def step_ends(src=None):
    """BootLoadProgress.StepEnds, read from the C#: the bar's steps end where the model's shares end."""
    if src is None:
        src = open(os.path.join(REPO, PROGRESS_CS), encoding="utf-8").read()
    m = re.search(r"StepEnds = \{([^}]*)\}", src)
    assert m, "BootLoadProgress.StepEnds moved"
    return [float(x.strip().rstrip("f")) for x in m.group(1).split(",")]


STEP_ENDS = step_ends()
assert len(STEP_ENDS) == len(STEP_NAMES) and STEP_ENDS[-1] == 1 and STEP_ENDS == sorted(STEP_ENDS), \
    "BootLoadProgress.StepEnds must be one rising end per step, the last at 1"


def art_guid(name):
    return hashlib.md5(f"CosmicShore/Splash/{name}".encode()).hexdigest()


def art_bytes():
    """The poster's pictures, byte for byte from their sources."""
    out = {}
    for name, src in ART.items():
        with open(os.path.join(REPO, src), "rb") as fh:
            out[f"{SPLASH_DIR}/{name}"] = fh.read()
    return out


def art_metas():
    donor = open(os.path.join(REPO, ART_META_DONOR), encoding="utf-8").read()
    assert "spriteMode: 1\n" in donor and "textureType: 8\n" in donor and "enableMipMap: 0\n" in donor, "the sprite meta donor changed"
    out = {SPLASH_DIR + ".meta": K.FOLDER_META.format(g=art_guid("folder"))}
    for name in ART:
        out[f"{SPLASH_DIR}/{name}.meta"] = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + art_guid(name), donor, count=1, flags=re.M)
    return out


def codex(name):
    return K.meta_guid(f"Assets/_Graphics/Codex/{name}.png")


def SC(x, y, w, h, pivot=(0.5, 0.5)):
    """A rect on the stage whose PIVOT sits at the board's (x, y) (y down from the stage's top)."""
    return R((0, 1), None, (x, -y), (w, h), pivot)


def picture(n, sprite, alpha=1.0, preserve=True):
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, alpha), sprite, preserve=preserve))
    return n


def motion(seconds, phase=0.0, offset=((0, 0), (0, 0)), turn=(0, 0), scale=(1, 1)):
    """PosterDrift: between two poses and back, eased both ways. Offsets are canvas units with y UP."""
    return lambda ctx, go: K.mono(go, S["drift"],
                                  f"  offsetFrom: {K.v2(*offset[0])}\n  turnFrom: {K.f32(turn[0])}\n  scaleFrom: {K.f32(scale[0])}\n"
                                  f"  offsetTo: {K.v2(*offset[1])}\n  turnTo: {K.f32(turn[1])}\n  scaleTo: {K.f32(scale[1])}\n"
                                  f"  seconds: {K.f32(seconds)}\n  phase: {K.f32(phase)}\n")


def slant(color, lean=LEAN, sprite=None, focus=(0.5, 0.5)):
    """KitSlant: a leaning quad, flat or showing a picture cropped to it."""
    return lambda ctx, go: K.mono(go, S["slant"],
                                  "  m_Material: {fileID: 0}\n"
                                  f"  m_Color: {K.col(color)}\n  m_RaycastTarget: 0\n"
                                  "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
                                  "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
                                  f"  leanDegrees: {K.f32(lean)}\n  sprite: {K.sprite_ref(sprite)}\n  focus: {K.v2(*focus)}\n")


UNPAINTED = tok("SurfaceBlack", 0.0)      # what a domain shape ships as: clear until the worn set paints it


def paint(domain, faces=(), rims=(), face_alpha=BAND_ALPHA, rim_alpha=1.0):
    """PosterDomainPaint: `faces` and `rims` are node keys of KitSlant shapes."""
    return lambda ctx, go: K.mono(go, S["paint"],
                                  f"  domain: {DOMAIN_VALUE[domain]}\n"
                                  + K.refs_list("faces", [ctx.c(k, "slant") for k in faces])
                                  + f"  faceAlpha: {K.f32(face_alpha)}\n"
                                  + K.refs_list("rims", [ctx.c(k, "slant") for k in rims])
                                  + f"  rimAlpha: {K.f32(rim_alpha)}\n  blendSeconds: {K.f32(BLEND_SECONDS)}\n")


def band_node(k, i, domain):
    """One domain as its prism: the worn set's face colour as the body, its rim colour along both long edges."""
    height = BAND_BOTTOM - BAND_TOP
    reach = SLANT * height
    cy = (BAND_TOP + BAND_BOTTOM) / 2
    cx = BAND_X0 + i * BAND_PITCH + BAND_W / 2 + SLANT * (BAND_MID - cy)
    key = f"{k}/Band{domain}"
    n = N(key, f"Band {domain}", SC(cx, cy, BAND_W + reach, height))
    n.child(N(f"{key}/Face", "Face", STRETCH)).add("cr", 222, K.canvas_renderer()).add("slant", 114, slant(UNPAINTED))
    for side, ax in (("RimL", 0), ("RimR", 1)):
        n.child(N(f"{key}/{side}", side, R((ax, 0), (ax, 1), (0, 0), (RIM_W + reach, 0), (ax, 0.5)))) \
            .add("cr", 222, K.canvas_renderer()).add("slant", 114, slant(UNPAINTED))
    n.add("paint", 114, paint(domain, [f"{key}/Face"], [f"{key}/RimL", f"{key}/RimR"]))
    return n


def prism_run(k, i, domain, x, y, count, phase):
    """A short run of prisms in a domain's rim colour, as a vessel lays them (r94_posters.prisms)."""
    key = f"{k}/Run{domain}"
    n = N(key, f"Prisms {domain}", R((0, 1), None, (x, -y), (0, 0), (0, 1)))
    keys = []
    for j in range(count):
        w, h = 26, 70 + j * 8
        pk = f"{key}/P{j}"
        prism = n.child(N(pk, f"Prism{j}", R((0, 1), None, (j * 40 + w / 2, round(j * 40 * 0.45) - h / 2), (w, h))))
        prism.rot = 24
        prism.add("cr", 222, K.canvas_renderer()).add("slant", 114, slant(UNPAINTED, lean=0))
        keys.append(pk)
    n.add("paint", 114, paint(domain, rims=keys, rim_alpha=0.8))
    n.add("drift", 114, motion(8 + i, phase, offset=((0, 8), (0, -10))))
    return n


def captain_card(k, name, cx, h, w):
    """A captain's bust in a slanted card that leans the way the bands lean, an ink line round it."""
    box = w + round(h * SLANT)
    key = f"{k}/{name}"
    n = N(key, name, TL(cx - box // 2, CAPTAIN_FOOT - h, box, h))
    n.child(N(f"{key}/Ink", "Ink", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("slant", 114, slant(tok("TextLight"))).add("th", 114, H.themed("TextLight"))
    n.child(N(f"{key}/Picture", "Picture", R((0, 0), (1, 1), (0, 0), (-6, -6)))).add("cr", 222, K.canvas_renderer()) \
        .add("slant", 114, slant(K.WHITE, sprite=art_guid(f"captain_{name.lower()}.jpg"), focus=(0.5, 0.82)))
    return n


def life_nodes(k):
    """The far sea's life, drawn with the game's own pictures (r94_posters.far_sea)."""
    out = []
    omni = codex("ethirion_omni")
    for i, (x, y, size, seconds) in enumerate(((170, 520, 150, 9), (1730, 250, 170, 11), (1560, 640, 96, 8), (330, 330, 84, 10), (1840, 560, 70, 12))):
        n = picture(N(f"{k}/Omni{i}", f"Omni{i}", SC(x, y, size, size)), omni)
        out.append(n.add("drift", 114, motion(seconds, i * 0.37, offset=((0, 6), (0, -8)), turn=(-7, 7))))
    right, left = ((-50, -16), (90, 24)), ((60, -10), (-80, 20))
    for name, x, y, w, h, alpha, seconds, way in (("shoal", 40, 380, 260, 87, 0.9, 30, right), ("quadfish", 1600, 420, 150, 152, 0.9, 26, left),
                                                    ("shark", 1570, 80, 170, 100, 0.8, 36, left), ("piranha", 60, 650, 150, 116, 0.85, 34, right)):
        n = picture(N(f"{k}/{name}", name.capitalize(), SC(x + w / 2, y + h / 2, w, h)), codex(f"fauna_{name}"), alpha)
        out.append(n.add("drift", 114, motion(seconds, offset=way)))
    for name, x, y, w, h, alpha, seconds in (("arbor", 250, 590, 142, 270, 0.85, 9), ("coral", 20, 690, 180, 176, 0.8, 11),
                                             ("lantern", 1790, 560, 99, 300, 0.85, 10), ("pine", 1560, 700, 160, 169, 0.8, 12)):
        n = picture(N(f"{k}/{name}", name.capitalize(), SC(x + w / 2, y + h, w, h, (0.5, 0))), codex(f"flora_{name}"), alpha)
        out.append(n.add("drift", 114, motion(seconds, turn=(-2.5, 2.5))))       # rooted: it sways about its foot
    out.append(prism_run(k, 0, "Jade", 70, 340, 4, 0.0))
    out.append(prism_run(k, 2, "Gold", 1650, 820, 3, 0.8))
    return out


def poster_node(prefix):
    k = f"{prefix}/Poster"
    root = N(k, "Poster", STRETCH)
    # The exit (section 185): the poster grows as the splash dissolves, so the reveal moves through it into the world.
    root.add("exit", 114, lambda ctx, go: K.mono(go, S["exit"], f"  push: {K.f32(POSTER_EXIT_PUSH)}\n"))
    cell = root.child(N(f"{k}/Cell", "Cell", STRETCH))
    shot = picture(cell.child(N(f"{k}/Cell/Picture", "Picture", R(size=(1920, 1080)))), art_guid("poster_cell.jpg"), 0.62, preserve=False)
    shot.add("drift", 114, motion(40, offset=((-10, -6), (14, 10)), scale=(1.02, 1.1)))       # the slow push-in
    cell.add("cover", 114, lambda ctx, go: K.mono(go, S["cover"], f"  image: {{fileID: {ctx.c(f'{k}/Cell/Picture', 'img')}}}\n  overscan: 1.02\n"))
    root.child(M.window_sea_node(f"{k}/Sea", name="Sea"))        # shader-only: no CPU, never dirties the canvas

    stage = root.child(N(f"{k}/Stage", "Stage", R((0.5, 0), None, (0, FOOT_H), (STAGE_W, STAGE_H), (0.5, 0))))
    stage.add("fit", 114, lambda ctx, go: K.mono(go, S["fit"], f"  frame: {{fileID: {ctx.rt(k)}}}\n  authoredWidth: {STAGE_W}\n"))
    for n in life_nodes(f"{k}/Stage"):
        stage.child(n)
    for i, domain in enumerate(BANDS):
        stage.child(band_node(f"{k}/Stage", i, domain))
    for name, x, y, seconds in HULLS:
        hull = picture(N(f"{k}/Stage/{name}", name, SC(x + HULL_SIZE / 2, y + HULL_SIZE / 2, HULL_SIZE, HULL_SIZE)),
                       K.meta_guid(f"Assets/_Graphics/CardImages/{name}_large.png"))
        stage.child(hull.add("drift", 114, motion(seconds, offset=((0, 8), (0, -10)))))
    for i in CAPTAIN_ORDER:
        stage.child(captain_card(f"{k}/Stage", CAPTAINS[i][0], CAPTAIN_X[i], CAPTAINS[i][1], CAPTAINS[i][2]))

    picture(root.child(N(f"{k}/Logo", "Logo Cosmic Shore", TL(POSTER_MARGIN, 64, 300, round(300 * 359 / 910)))), art_guid("poster_logo_cosmic_shore.png"))
    foot = root.child(N(f"{k}/Foot", "Foot", R((0, 0), (1, 0), (0, 0), (0, FOOT_H), (0.5, 0))))
    foot.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack"))).add("th", 114, H.themed("SurfaceBlack"))
    # Froglet's logo sits in the foot's bottom-right corner, under the bar's percentage (Shombith, 2026-11-13: "The
    # froglet logo should be in the bottom right corner below the percentage bar"). After the foot, so it draws on it.
    picture(root.child(N(f"{k}/Froglet", "Logo Froglet",
                         R((1, 0), None, (-POSTER_MARGIN, FROGLET_BOTTOM), (FROGLET_W, round(FROGLET_W * 322 / 900)), (1, 0)))),
            art_guid("poster_logo_froglet.png"))
    return root


def ground(p, img, go, raycast):
    """The feature art's Image becomes the poster's ground: the theme's deepest surface, no picture."""
    p.body(img, K.image(tok("SurfaceBlack"), raycast=raycast), go)


RETIRED_BACKDROP = ("Dim", "Lattice", "BottomFade")       # section 26's dim and bottom fade, section 70's sea node


def retire_backdrop(p, prefix):
    """Delete what the poster replaces (emitting over the committed scene keeps what it no longer writes): the dim,
    the old sea node, the bottom fade, and the 180 x 2 KitLoadBar on the readout's track."""
    for name in RETIRED_BACKDROP:
        remove_go(p, str(p.ctx.go(f"{prefix}/{name}")))
    bar = str(p.ctx.c(f"{prefix}/Readout/Track", "bar"))
    if p.sc.has(bar):
        p.sc.remove(bar)


def themed_image(n, token, alpha=1.0, sprite=None, preserve=False):
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok(token, alpha), sprite, preserve=preserve)) \
        .add("th", 114, H.themed(token, alpha))
    return n


def readout_node(prefix, with_line):
    """The foot's bar (Rev 91's, board R94C): LOADING, the running step's name, the percentage, one bar cut into the
    boot's steps with a mark and a name under each. BootStepBar draws BootLoadProgress on it; the sign-in scene's
    also carries its status line (BootStatusLine)."""
    k = f"{prefix}/Readout"
    n = N(k, "Readout", R((0, 0), (1, 0), (0, 64), (-2 * POSTER_MARGIN, BAR_H), (0.5, 0)))
    n.add("cg", 225, H.canvas_group(True))
    H.label(n.child(N(f"{k}/Caption", "Caption", TL(0, 0, 420, 20))), "Loading", "Label", "NeutralLightest")
    words = n.child(N(f"{k}/StepWords", "StepWords", TL(0, 26, 900, 34)))
    words.add("cg", 225, H.canvas_group(True))
    H.label(words.child(N(f"{k}/StepWords/Step", "Step", STRETCH)), "", "H2", "TextLight")
    back = n.child(N(f"{k}/ReturnWords", "ReturnWords", TL(0, 26, 900, 34)))
    back.add("cg", 225, H.canvas_group(False))
    H.label(back.child(N(f"{k}/ReturnWords/Line", "Line", STRETCH)), RETURN_WORDS, "H2", "TextLight")
    H.label(n.child(N(f"{k}/Percent", "Percent", HM.TR(28, 6, 160, 54))), "", "DataLarge", "TextLight", align="right")
    H.label(n.child(N(f"{k}/PercentSign", "PercentSign", HM.TR(0, 30, 24, 26))), "%", "Data", "NeutralLightest", align="right")

    track = themed_image(n.child(N(f"{k}/Track", "Track", R((0, 1), (1, 1), (0, -76), (0, 8), (0.5, 1)))), "SurfaceLight", 0.55)
    fill = themed_image(track.child(N(f"{k}/Track/Fill", "Fill", R((0, 0), (0, 1), (0, 0), (0, 0)))), "TextLight")
    fill.add("mask", 114, M.rect_mask())
    themed_image(fill.child(N(f"{k}/Track/Fill/Glint", "Glint", R((0, 0), (0.12, 1), (0, 0), (0, 0)))), "SurfaceVeryDark", 0.55,
                 K.kit_sprite("kit_sheen"))
    for i, end in enumerate(STEP_ENDS[:-1]):          # the cuts between steps, in the foot's own colour
        themed_image(track.child(N(f"{k}/Track/Cut{i}", f"Cut{i}", R((end, 0), (end, 1), (0, 0), (4, 0)))), "SurfaceBlack")

    row = n.child(N(f"{k}/Steps", "Steps", R((0, 1), (1, 1), (0, -104), (0, 20), (0.5, 1))))
    row.add("cg", 225, H.canvas_group(True))
    for i, name in enumerate(STEP_NAMES):
        start = STEP_ENDS[i - 1] if i else 0
        step = row.child(N(f"{k}/Steps/S{i}", f"Step{i}", R((start, 0), (start, 1), (0, 0), (300, 0), (0, 0.5))))
        themed_image(step.child(N(f"{k}/Steps/S{i}/Mark", "Mark", R((0, 0.5), None, (7, 0), (14, 12)))), "NeutralLightest",
                     sprite=K.kit_sprite("kit_hex_line1"), preserve=True)
        word = step.child(N(f"{k}/Steps/S{i}/Word", "Word", R((0, 0.5), None, (24, 0), (270, 20), (0, 0.5))))
        word.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text(name, "BodySmall", tok("NeutralLightest"))) \
            .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest", driven=True))

    if with_line:
        H.label(n.child(N(f"{k}/Line", "StatusLine", R((0, 0), None, (0, -34), (900, 22), (0, 0)))), "", "BodySmall", "NeutralLightest")
        n.add("line", 114, lambda ctx, go: K.mono(go, S["line"],
              f"  requestEvent: {K.ref(11400000, BOOT_STATUS_CHANNEL, 2)}\n"
              f"  statusText: {{fileID: {ctx.c(k + '/Line', 'tmp')}}}\n"
              f"  readoutGroup: {{fileID: {ctx.c(k, 'cg')}}}\n"))

    def bar(ctx, go):
        c = ctx.c
        marks = [c(f"{k}/Steps/S{i}/Mark", "img") for i in range(len(STEP_NAMES))]
        words_ = [c(f"{k}/Steps/S{i}/Word", "tmp") for i in range(len(STEP_NAMES))]
        return K.mono(go, S["stepbar"],
                      f"  fill: {{fileID: {ctx.rt(k + '/Track/Fill')}}}\n"
                      f"  fillGraphic: {{fileID: {c(k + '/Track/Fill', 'img')}}}\n"
                      f"  glint: {{fileID: {ctx.rt(k + '/Track/Fill/Glint')}}}\n  glintSeconds: 2.4\n"
                      "  stepEnds:\n" + "".join(f"  - {K.f32(e)}\n" for e in STEP_ENDS)
                      + "  stepNames:\n" + "".join(f"  - {K.yaml_scalar(s_)}\n" for s_ in STEP_NAMES)
                      + K.refs_list("stepLabels", words_) + K.refs_list("stepMarkers", marks)
                      + f"  stepText: {{fileID: {c(k + '/StepWords/Step', 'tmp')}}}\n"
                      f"  percentText: {{fileID: {c(k + '/Percent', 'tmp')}}}\n"
                      "  percentTemplate: '{pct}'\n"
                      f"  doneWord: {DONE_WORD}\n"
                      f"  requestEvent: {K.ref(11400000, BOOT_STATUS_CHANNEL, 2)}\n"
                      f"  theme: {K.ref(11400000, K.THEME, 2)}\n"
                      + K.refs_list("stepMarks", marks) + K.refs_list("stepWords", words_)
                      + f"  markOpen: {K.sprite_ref(K.kit_sprite('kit_hex_line1'))}\n"
                      f"  markDone: {K.sprite_ref(K.kit_sprite('kit_hex_fill'))}\n"
                      f"  stepRow: {{fileID: {c(k + '/Steps', 'cg')}}}\n"
                      f"  bootWords: {{fileID: {c(k + '/StepWords', 'cg')}}}\n"
                      f"  returnWords: {{fileID: {c(k + '/ReturnWords', 'cg')}}}\n")
    n.add("stepbar", 114, bar)
    return n


def window(key, r, eyebrow, eyebrow_token, title, title_role, entrance=True):
    """A slivered style-B window: fill, border, corner brackets, eyebrow and title."""
    w = N(key, "Window", r)
    w.add("cg", 225, H.canvas_group(True))
    if entrance:
        w.add("in", 114, M.entrance("Rise", distance=14))
    H.framed(w, key, 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=WINDOW_FILL)
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


def welcome_window():
    """Section 58: "Welcome back, pilot" over the splash while a signed-in pilot's profile and session load
    (BootWelcomeCard reads the boot status channel; hidden until then)."""
    w = window("Welcome", R((0.5, 0.5), None, (0, 60), (WIN_W, WELCOME_H)), "Welcome back, pilot", "NeutralLightest", "", "H2")
    spinner_line(w, "Welcome", 116)
    w.add("welcome", 114, lambda ctx, go: K.mono(go, S["welcome"],
          f"  requestEvent: {K.ref(11400000, BOOT_STATUS_CHANNEL, 2)}\n"
          f"  authData: {K.ref(11400000, AUTH_DATA, 2)}\n"
          f"  group: {{fileID: {ctx.c('Welcome', 'cg')}}}\n"
          f"  eyebrowText: {{fileID: {ctx.c('Welcome/Eyebrow', 'tmp')}}}\n"
          f"  nameText: {{fileID: {ctx.c('Welcome/Title', 'tmp')}}}\n"
          f"  lineText: {{fileID: {ctx.c('Welcome/Waiting/Line', 'tmp')}}}\n"))
    return w


def splash_text(text_):
    p = Patch(text_, "Bootstrap/StartFlow")
    assert p.sc.script(SP["panel_comp"]) == S["panel"], "BootStatusPanel moved"
    assert p.sc.script(SP["broadcaster"]) == S["broadcaster"], "BootStatusBroadcaster moved"
    assert p.sc.field(SP["status_go"], "m_Name") == "Status Text", "the splash status line moved"
    remove_instance(p, SP["loader_pid"], SP["safe_rt"])
    remove_go(p, SP["old_retry_go"])

    # Rev 94 C: the poster takes the feature art's place (it bleeds); everything readable rides the safe area.
    retire_backdrop(p, "Splash")
    ground(p, SP["art_img"], SP["art_go"], raycast=True)
    emit_under(p, SP["art_rt"], [poster_node("Splash")], SP["art_img"],
               lambda rts: rts + [k for k in p.kids(SP["art_rt"]) if k not in rts])

    p.rect(SP["status_rt"], R((0, 0), None, (POSTER_MARGIN, 30), (900, 22), (0, 0)))
    p.replace(SP["status_tmp"], tmp_restyle(p.doc(SP["status_tmp"]), "Chakra", 15, False, 2, "left", "NeutralLightest"))
    readout, retry, welcome = readout_node("Splash", False), retry_window(), welcome_window()
    emit_under(p, SP["safe_rt"], [readout, welcome, retry], SP["status_tmp"],
               lambda rts: [k for k in p.kids(SP["safe_rt"]) if k not in rts] + rts)
    retry_cg = str(p.ctx.c("Retry", "cg"))
    for hidden in (retry_cg, str(p.ctx.c("Welcome", "cg"))):
        cg = p.doc(hidden)
        for f in ("m_Alpha", "m_Interactable", "m_BlocksRaycasts"):
            cg = C.field_set(cg, f, "0")
        p.replace(hidden, cg)

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


def brand_button(k, name, r, sprite):
    """R9b: a provider's own sign-in button image, untouched (KitBrandButton never tints it), with the kit's sky-blue
    focus ring drawn AROUND it - the ring rect is 6 px larger on every side, so it never covers the artwork."""
    n = N(k, name, r)
    ring = n.child(N(f"{k}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (12, 12))))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Selection", 0.0), K.kit_sprite("kit_sliver10_line2"), True))
    face_ = n.child(N(f"{k}/Face", "Face", STRETCH))
    face_.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1, 1, 1, 1), sprite, raycast=True, preserve=True))
    n.add("button", 114, lambda ctx, go: K.mono(go, S["brand"],
        K.SELECTABLE_FIELDS
        + f"  m_TargetGraphic: {{fileID: {ctx.c(f'{k}/Face', 'img')}}}\n"
        "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"
        f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {{fileID: {ctx.c(f'{k}/FocusRing', 'img')}}}\n"
        "  focusRingToken: 17\n"
        f"  face: {{fileID: {ctx.c(f'{k}/Face', 'img')}}}\n"))
    return n


def bare_window(key, r):
    """A slivered style-B window: fill, border and corner brackets (author_start_flow.window without its
    eyebrow and title - each of the sign-in window's faces carries its own)."""
    w = N(key, "Window", r)
    w.add("cg", 225, H.canvas_group(True))
    w.add("in", 114, M.entrance("Rise", distance=14))
    H.framed(w, key, 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=WINDOW_FILL)
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
    sp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.kit_sprite("kit_hex_line1"), preserve=True)) \
        .add("spin", 114, M.ambient("Spin", period=1.1, amplitude=0))
    H.label(row.child(N(f"{key}/Waiting/Line", "Line", R((0, 0.5), (1, 0.5), (14, 0), (-28, 22)))), "", "BodySmall", "NeutralLightest")
    return row


def chooser_rows():
    """The CHOOSE face's rows below the body, top-down, as the board lays them out with every provider shown."""
    y, rows = STACK_TOP, [("SignIn/Choose/SilentNote", STACK_TOP, NOTE_H)]
    y += NOTE_H + ROW_GAP
    for name, _ in CHOOSER:
        rows.append((f"SignIn/Choose/{name}", y, BRAND_H))
        y += BRAND_H + ROW_GAP
    for key, h in (("SignIn/Choose/Or", OR_H), ("SignIn/Choose/Guest", BTN_H), ("SignIn/Choose/Hint", HINT_H), ("status", STATUS_H)):
        rows.append((key, y, h))
        y += h + ROW_GAP
    return rows, y - ROW_GAP + PAD


def auth_window():
    rows, height = chooser_rows()
    at = {k: y for k, y, _ in rows}
    w = bare_window("SignIn", R((0.5, 0.5), None, (0, height / 2 - 80), (WIN_W, height), (0.5, 1)))

    ch = face(w, "SignIn/Choose", True)
    heading(ch, "SignIn/Choose", "Welcome, pilot", "NeutralLightest", "Sign in to fly", "H1",
            "Your pilot, crystals and vessels follow you to every device you sign in on.")
    H.label(ch.child(N("SignIn/Choose/SilentNote", "SilentNote", TL(PAD, at["SignIn/Choose/SilentNote"], INNER, NOTE_H), active=False)),
            "Steam is not running. Start Steam to sign in with it, or pick a way below.", "BodySmall", "Danger",
            valign="top", wrap=True)
    for name, _ in CHOOSER:
        ch.child(brand_button(f"SignIn/Choose/{name}", f"{name}Button",
                              TL((WIN_W - BRAND_W) / 2, at[f"SignIn/Choose/{name}"], BRAND_W, BRAND_H), brand_sprite(f"{name.lower()}Button")))
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
    heading(pr, "SignIn/Progress", "Google", "NeutralLightest", "Finish in your browser", "H2",
            "A Google sign-in page opened in your browser. Choose your account there, then come back here.", body_h=70)
    spinner_line(pr, "SignIn/Progress", 196)
    pr.child(icon_button("SignIn/Progress/Primary", "PrimaryButton", TL(PAD, 236, 240, 52), "Play", "Try again", cap="A", align=4))
    pr.child(icon_button("SignIn/Progress/Secondary", "SecondaryButton", TL(PAD + 256, 236, 240, 52), "Default", "Other ways", align=4))
    pr.child(icon_button("SignIn/Progress/Guest", "GuestLink", TL(PAD, 300, 260, 36), "Cancel", "Play as guest", icon="chrome_person"))

    au = face(w, "SignIn/Auto", False)
    H.label(au.child(N("SignIn/Auto/Eyebrow", "Eyebrow", TL(PAD, 30, INNER, 20))), "Welcome back, pilot", "Label", "NeutralLightest")
    # R9b: no Steam mark (Valve allows only its approved artwork); the account name and caption carry the card.
    H.label(au.child(N("SignIn/Auto/Name", "Name", TL(PAD, 70, INNER, 34))), "", "H2", "TextLight")
    H.label(au.child(N("SignIn/Auto/Caption", "Caption", TL(PAD, 108, INNER, 20))), "Steam account", "Label", "NeutralLightest")
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
                f"    mark: {{fileID: {c(f'SignIn/Choose/{n}/Face', 'img')}}}\n" for n, v in CHOOSER)
            + f"  silentNoteRow: {{fileID: {ctx.rt('SignIn/Choose/SilentNote')}}}\n"
            f"  silentNoteText: {{fileID: {c('SignIn/Choose/SilentNote', 'tmp')}}}\n"
            f"  orRule: {{fileID: {ctx.rt('SignIn/Choose/Or')}}}\n"
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
            "  autoMark: {fileID: 0}\n"
            f"  autoName: {{fileID: {c('SignIn/Auto/Name', 'tmp')}}}\n"
            f"  autoCaption: {{fileID: {c('SignIn/Auto/Caption', 'tmp')}}}\n"
            f"  autoLine: {{fileID: {c('SignIn/Auto/Waiting/Line', 'tmp')}}}\n"
            f"  otherWaysButton: {{fileID: {c('SignIn/Auto/OtherWays', 'button')}}}\n")


def providers_asset():
    """SignInProviders.asset: the platforms' orders (Steam / Google / Apple on desktop; Apple first on iPhone,
    Google first on Android), Steam as the desktop's silent provider, the providers' own button art and logo-only marks
    (R9b; Steam's mark stays empty - Valve allows only its approved artwork)."""
    def order(name, vals):
        return f"  {name}:\n" + "".join(f"  - {v}\n" for v in vals)
    art = {f: K.sprite_ref(brand_sprite(f)) for f in BRANDS}
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {S['providers_so']}, type: 3}}\n"
            "  m_Name: SignInProviders\n  m_EditorClassIdentifier: \n"
            + order("desktopOrder", (2, 3, 4)) + order("iPhoneOrder", (4, 3)) + order("androidOrder", (3, 4)) + order("webOrder", (3, 4))
            + "  desktopSilentProvider: 2\n"
            f"  googleButton: {art['googleButton']}\n  appleButton: {art['appleButton']}\n"
            f"  steamMark: {{fileID: 0}}\n  googleMark: {art['googleMark']}\n  appleMark: {art['appleMark']}\n"
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

    retire_backdrop(p, "Auth")
    ground(p, I["bg_img"], I["bg_go"], raycast=False)
    emit_under(p, I["canvas_rt"], [poster_node("Auth")], I["bg_img"],
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
    # R9b retired the Steam button, the AUTO card's Steam mark and the kit-button parts of the Google / Apple rows
    # (they became the providers' own images): delete them, since emitting over the committed scene keeps what it no
    # longer writes.
    for key in R9B_RETIRED_GOS:
        remove_go(p, str(p.ctx.go(key)))
    for key, comp in R9B_RETIRED_COMPONENTS:
        fid = str(p.ctx.c(key, comp))
        if p.sc.has(fid):
            p.sc.remove(fid)
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
    files.update(art_metas())
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
    # The other direction: nothing left behind by a retired node (a transform its father does not list, or a
    # component its GameObject does not list) - emitting over the committed scene keeps what it no longer writes.
    for fid, d in sc.docs.items():
        if "stripped" in d.split("\n", 1)[0]:
            continue
        m = re.search(r"^  m_Father: \{fileID: (-?\d+)\}", d, re.M)
        if d.startswith("--- !u!224 ") and m and m.group(1) != "0" and m.group(1) in sc.docs \
                and "stripped" not in sc.docs[m.group(1)].split("\n", 1)[0]:
            assert f"{{fileID: {fid}}}" in (re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[m.group(1)], re.M) or [None, ""])[1], \
                f"{path}: transform {fid} is not among its father's children (a node left behind)"
        g = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", d, re.M)
        if g and g.group(1) != "0" and g.group(1) in sc.docs and not d.startswith("--- !u!1 ") \
                and "stripped" not in sc.docs[g.group(1)].split("\n", 1)[0]:   # a component added to an instance
            assert f"component: {{fileID: {fid}}}" in sc.docs[g.group(1)], f"{path}: component {fid} is not on its GameObject's list (left behind)"
    return sc


def validate(files):
    M.require_entrances(files, {SPLASH: 2, AUTH: 2})
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
    # Section 58: the welcome card on the splash, on the boot status channel, hidden until a signed-in status.
    cards = [(fid, d) for fid, d in sc.docs.items() if f"guid: {S['welcome']}" in d]
    assert len(cards) == 1, "the splash has no BootWelcomeCard"
    wd = cards[0][1]
    assert f"guid: {BOOT_STATUS_CHANNEL}" in wd and f"guid: {AUTH_DATA}" in wd, "BootWelcomeCard reads another channel or auth data"
    for f in ("group", "eyebrowText", "nameText", "lineText"):
        assert field_ref(wd, f) != "0" and sc.has(field_ref(wd, f)), f"BootWelcomeCard.{f} is not wired"
    wg = sc.docs[field_ref(wd, "group")]
    assert "  m_Alpha: 0\n" in wg and "  m_BlocksRaycasts: 0\n" in wg, "the welcome card ships shown"
    check_poster(SPLASH, sc, splash_ctx(), "Splash", files)
    assert sc.go_of(field_ref(panel, "readoutGroup")) == sc.go_of(step_bar(SPLASH, sc)[0]), \
        "BootStatusPanel.readoutGroup is not the bar's readout (the bar would stay up under the retry window)"
    for key in ("Retry", "Welcome"):
        assert "  alpha: 1\n" in sc.docs[str(splash_ctx().c(f"{key}/Fill", "th"))], f"the {key} window is see-through over the poster"
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
    check_poster(AUTH, au, auth_ctx(), "Auth", files)
    assert au.go_of(field_ref(lines[0], "readoutGroup")) == au.go_of(step_bar(AUTH, au)[0]), \
        "BootStatusLine.readoutGroup is not the bar's readout (a loading bar would sit under a question)"
    for key in ("SignIn", "Name"):
        assert "  alpha: 1\n" in au.docs[str(auth_ctx().c(f"{key}/Fill", "th"))], f"the {key} window is see-through over the poster"
    for path, s in ((SPLASH, sc), (AUTH, au)):
        for fid, d in s.docs.items():
            if f"guid: {S['kitbutton']}" in d:
                go = s.go_of(fid)
                assert not any(f"guid: {S['cbp']}" in s.docs.get(c, "") for c in s.components(go)), \
                    f"{path}: a start-flow button also polls the pad (it would press twice)"
    # Rev 9: the lattice over the art, in both scenes, between the dim and the bottom fade.
    for path, s_ in ((SPLASH, sc), (AUTH, au)):
        lat = [d for d in s_.docs.values() if f"guid: {M.SEA_WINDOW_MAT}, type: 2" in d]
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
    # R9b: Google and Apple only (Steam signs in on its own), each the provider's own artwork, untinted, ring outside.
    buttons = re.findall(r"  - provider: (\d+)\n    button: \{fileID: (-?\d+)\}\n    mark: \{fileID: (-?\d+)\}", cd)
    assert sorted(int(v) for v, _, _ in buttons) == [3, 4], "SignInChooser needs exactly one button each for Google and Apple (no Steam button: R9b)"
    for v, b, face_ in buttons:
        bd = au.docs.get(b, "")
        assert f"guid: {S['brand']}" in bd, "a provider button is not a KitBrandButton (its artwork would be tinted)"
        fd = au.docs.get(face_, "")
        want = brand_sprite("googleButton" if v == "3" else "appleButton")
        assert f"  m_Sprite: {K.sprite_ref(want)}" in fd, "a provider button does not show its provider's own button image"
        assert "  m_Color: {r: 1, g: 1, b: 1, a: 1}\n" in fd, "a provider's button image is tinted"
        assert f"  face: {{fileID: {face_}}}" in bd, "KitBrandButton.face is not the button image"
        ring = field_ref(bd, "focusRing")
        rd = au.docs.get(au.rect_of(au.go_of(ring)), "")
        m = re.search(r"  m_SizeDelta: \{x: ([\d.]+), y: ([\d.]+)\}", rd)
        assert m and float(m.group(1)) > 0 and float(m.group(2)) > 0, "the focus ring sits on the provider's artwork, not around it"
    note = field_ref(cd, "silentNoteRow")
    assert note != "0" and field_ref(cd, "silentNoteText") != "0", "SignInChooser has no Steam-not-running note"
    assert au.field(au.go_of(note), "m_IsActive") == "0", "the Steam-not-running note ships shown"
    assert field_ref(cd, "autoMark") == "0", "the AUTO card carries a Steam mark (Valve allows only its approved artwork)"
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
    # Owner decisions, 2026-10-18 (section 57): Steam signs in silently (above), Apple on every platform, and Play as
    # guest stays on Steam builds too (it is the controller's own button on the CHOOSE face, checked above).
    for order in ("desktopOrder", "iPhoneOrder", "androidOrder", "webOrder"):
        block = re.search(r"^  %s:\n((?:  - \d+\n)*)" % order, a, re.M)
        assert block and "  - 4\n" in block.group(1), f"SignInProviders.{order} drops Apple (the owner: Apple on every platform)"
    for f in BRANDS:
        assert f"  {f}: {K.sprite_ref(brand_sprite(f))}\n" in a, f"SignInProviders.{f} is not the provider's own artwork"
    assert "  steamMark: {fileID: 0}\n" in a, "SignInProviders.steamMark is set: only Valve-approved Steam artwork may go there"
    for f, (name, sha) in BRANDS.items():
        data = files.get(f"{BRAND_DIR}/{name}")
        if data is None:
            data = open(os.path.join(REPO, BRAND_DIR, name), "rb").read()
        assert hashlib.sha256(data).hexdigest() == sha, f"{name} is not the provider's file byte for byte (re-exported, cropped or recoloured?)"
    # Privacy
    for f, sprite in PRIVACY_SPRITES:
        assert f"  {f}: {K.sprite_ref(K.kit_sprite(sprite))}" in files[PRIVACY], f"PrivacyConsentConfig.{f} is not set"


def step_bar(path, sc):
    bars = [(fid, d) for fid, d in sc.docs.items() if f"guid: {S['stepbar']}" in d]
    assert len(bars) == 1, f"{path}: needs exactly one BootStepBar"
    return bars[0]


def subtree(sc, rt):
    """Every RectTransform at or under `rt`."""
    out, todo = [], [str(rt)]
    while todo:
        cur = todo.pop()
        out.append(cur)
        m = re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[cur], re.M)
        todo += re.findall(r"\{fileID: (-?\d+)\}", m.group(1)) if m else []
    return out


def colour_of(doc):
    m = re.search(r"^  m_Color: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}", doc, re.M)
    return tuple(float(x) for x in m.groups()) if m else None


def from_the_theme(rgb):
    """True for white (the identity tint over art) and for a UI theme token: the only colours this script may write."""
    allowed = [K.WHITE[:3]] + [K.hexc(h)[:3] for h in K.TOK.values()]
    return any(all(abs(a - b) < 5e-4 for a, b in zip(rgb, t)) for t in allowed)


def check_poster(path, sc, ctx, prefix, files):
    """Rev 94 C: the poster and the foot's bar, on one surface."""
    assert not any(f"guid: {S['loadbar']}" in d for d in sc.docs.values()), f"{path}: the 180 x 2 KitLoadBar is back"
    for name in RETIRED_BACKDROP:
        assert not sc.has(str(ctx.go(f"{prefix}/{name}"))), f"{path}: the still's {name} survives under the poster"
    # The bar: wired, on the boot status channel, cut where the model's shares end.
    _, bar = step_bar(path, sc)
    for f in ("fill", "fillGraphic", "glint", "stepText", "percentText", "stepRow", "bootWords", "returnWords"):
        assert field_ref(bar, f) != "0" and sc.has(field_ref(bar, f)), f"{path}: BootStepBar.{f} is not wired"
    assert f"  requestEvent: {K.ref(11400000, BOOT_STATUS_CHANNEL, 2)}\n" in bar, f"{path}: BootStepBar is not on the boot status channel"
    ends = re.search(r"^  stepEnds:\n((?:  - .*\n)*)", bar, re.M)
    got = [float(x) for x in re.findall(r"  - (.*)\n", ends.group(1))] if ends else []
    assert len(got) == len(STEP_ENDS) and all(abs(a - b) < 1e-6 for a, b in zip(got, STEP_ENDS)), \
        f"{path}: the bar's steps end at {got}, BootLoadProgress.StepEnds says {STEP_ENDS} (the name over the bar would disagree with the fill)"
    names = re.search(r"^  stepNames:\n((?:  - .*\n)*)", bar, re.M)
    assert names and len([x for x in re.findall(r"  - (.*)\n", names.group(1)) if x.strip()]) == len(STEP_ENDS), f"{path}: the bar needs a name per step"
    for f in ("stepMarks", "stepWords"):
        got_ = re.search(r"^  %s:\n((?:  - .*\n)*)" % f, bar, re.M)
        assert got_ and got_.group(1).count("{fileID: ") == len(STEP_ENDS), f"{path}: BootStepBar.{f} needs {len(STEP_ENDS)} entries"
    hidden = sc.docs[field_ref(bar, "returnWords")]
    assert "  m_Alpha: 0\n" in hidden, f"{path}: the return line ships shown over the step's name"

    # The poster: every piece present, every colour from the theme or left to the worn set.
    root = str(ctx.rt(f"{prefix}/Poster"))
    assert sc.has(root), f"{path}: no poster"
    comps = [sc.docs[c] for rt in subtree(sc, root) for c in sc.components(sc.go_of(rt)) if c in sc.docs]
    slants = [d for d in comps if f"guid: {S['slant']}" in d]
    for d in comps:
        if f"guid: {S['slant']}" in d or f"guid: {K.IMAGE}" in d:
            rgb = colour_of(d)
            assert rgb and from_the_theme(rgb[:3]), f"{path}: a poster graphic carries a typed colour {rgb} (white, a theme token, or the worn set at runtime)"
    paints = [d for d in comps if f"guid: {S['paint']}" in d]
    bands = [d for d in paints if "  faces:\n" in d]
    assert sorted(int(re.search(r"^  domain: (\d+)", d, re.M).group(1)) for d in bands) == sorted(DOMAIN_VALUE[b] for b in BANDS), \
        f"{path}: the poster needs one band each for {', '.join(BANDS)}"
    assert len(paints) == len(bands) + 2, f"{path}: the poster needs its two runs of prisms"
    for d in paints:
        shapes = re.findall(r"^  - \{fileID: (-?\d+)\}", d, re.M)
        assert shapes, f"{path}: a PosterDomainPaint paints nothing"
        for fid in shapes:
            sd = sc.docs.get(fid, "")
            assert f"guid: {S['slant']}" in sd, f"{path}: a PosterDomainPaint names a shape that is not a KitSlant"
            assert colour_of(sd)[3] == 0, f"{path}: a domain shape ships with a colour of its own (it must be clear until the worn colour set paints it)"
    for d in bands:
        rims = re.search(r"^  rims:\n((?:  - .*\n)*)", d, re.M)
        assert rims and rims.group(1).count("{fileID: ") == 2, f"{path}: a band needs a rim along both long edges"
        assert f"  blendSeconds: {K.f32(BLEND_SECONDS)}\n" in d, f"{path}: a band does not blend when the worn set changes"
    pictures = "".join(comps)
    for name, _, _ in CAPTAINS:
        assert f"  sprite: {K.sprite_ref(art_guid('captain_' + name.lower() + '.jpg'))}\n" in pictures, f"{path}: the poster lost {name}"
    for name, _, _, _ in HULLS:
        assert K.sprite_ref(K.meta_guid(f"Assets/_Graphics/CardImages/{name}_large.png")) in pictures, f"{path}: the poster lost the {name}"
    for name in ("poster_cell.jpg", "poster_logo_cosmic_shore.png", "poster_logo_froglet.png"):
        assert K.sprite_ref(art_guid(name)) in pictures, f"{path}: the poster lost {name}"
    assert K.sprite_ref(codex("ethirion_omni")) in pictures, f"{path}: the poster lost its Omni crystals"
    drifts = [d for d in comps if f"guid: {S['drift']}" in d]
    assert len(drifts) >= 19 and all(float(re.search(r"^  seconds: (.*)$", d, re.M).group(1)) > 0 for d in drifts), \
        f"{path}: the poster's motion is missing or has a zero period"
    fits = [d for d in comps if f"guid: {S['fit']}" in d]
    assert len(fits) == 1 and field_ref(fits[0], "frame") == root, f"{path}: the stage does not fit itself to the poster"
    # The exit (section 185): on the poster's root, so the whole picture moves through as the splash dissolves.
    exits = [c for c in sc.components(sc.go_of(root)) if f"guid: {S['exit']}" in sc.docs.get(c, "")]
    assert len(exits) == 1 and f"  push: {K.f32(POSTER_EXIT_PUSH)}\n" in sc.docs[exits[0]], \
        f"{path}: the poster has no PosterExit on its root (the reveal would pop open instead of moving through it)"
    # Froglet's logo: in the bottom-right corner under the bar, drawn after the foot so the foot does not hide it.
    frog = sc.docs[str(ctx.rt(f"{prefix}/Poster/Froglet"))]
    assert "  m_AnchorMin: {x: 1, y: 0}\n" in frog and "  m_AnchorMax: {x: 1, y: 0}\n" in frog \
        and "  m_Pivot: {x: 1, y: 0}\n" in frog, f"{path}: the Froglet logo is not in the bottom-right corner"
    kids = re.findall(r"^  - \{fileID: (-?\d+)\}", sc.docs[root].split("  m_Children:\n", 1)[1].split("  m_Father:", 1)[0], re.M)
    assert kids.index(str(ctx.rt(f"{prefix}/Poster/Froglet"))) > kids.index(str(ctx.rt(f"{prefix}/Poster/Foot"))), \
        f"{path}: the Froglet logo is drawn under the foot"
    for name in ART:
        meta = files[f"{SPLASH_DIR}/{name}.meta"]
        assert f"guid: {art_guid(name)}\n" in meta and "spriteMode: 1\n" in meta, f"{name}.meta is not a sprite with the poster's guid"


def drift(files):
    stale = [p for p, t in files.items()
             if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]
    return stale + [p for p, b in art_bytes().items()
                    if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), "rb").read() != b]


def _show_face(text_, field):
    cg = re.search(r"^  %s: \{fileID: (-?\d+)\}" % field, text_, re.M).group(1)
    i = text_.index(f" &{cg}\n")
    j = text_.find("\n--- ", i)
    return text_[:i] + text_[i:j].replace("  m_Alpha: 0\n", "  m_Alpha: 1\n").replace("  m_BlocksRaycasts: 0\n", "  m_BlocksRaycasts: 1\n") + text_[j:]


def _tint_face(text_):
    face_ = re.search(r"  - provider: 3\n    button: \{fileID: -?\d+\}\n    mark: \{fileID: (-?\d+)\}", text_).group(1)
    i = text_.index(f" &{face_}\n")
    j = text_.find("\n--- ", i)
    return text_[:i] + text_[i:j].replace("  m_Color: {r: 1, g: 1, b: 1, a: 1}", "  m_Color: {r: 0.24, g: 0.7, b: 1, a: 1}") + text_[j:]


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
        ("the retry panel ships shown", lambda f: f.__setitem__(SPLASH, _show_face(f[SPLASH], "retryPanel"))),
        ("a load bar with no fill", lambda f: f.__setitem__(AUTH, re.sub(r"(  fill: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[AUTH], count=1))),
        ("the auth panel ships active", edit_doc(AUTH, AU["auth_go"], lambda d: d.replace("  m_IsActive: 0", "  m_IsActive: 1"))),
        ("the guest button also polls the pad", add_cbp),
        ("a privacy sprite missing", lambda f: f.__setitem__(PRIVACY, re.sub(r"(  focusRingSprite: ).*", r"\1{fileID: 0}", f[PRIVACY]))),
        ("the splash lost its sea", lambda f: f.__setitem__(SPLASH, f[SPLASH].replace(M.SEA_WINDOW_MAT, "0" * 32))),
        ("the controller lost its chooser", edit_doc(AUTH, AU["ctrl"], lambda d: re.sub(r"(  signInChooser: )\{fileID: -?\d+\}", r"\1{fileID: 0}", d))),
        ("the welcome card ships shown", lambda f: f.__setitem__(SPLASH, _show_face(f[SPLASH], "group"))),
        ("the PROGRESS face ships shown", lambda f: f.__setitem__(AUTH, _show_face(f[AUTH], "progressGroup"))),
        ("a provider button missing", lambda f: f.__setitem__(AUTH, re.sub(r"  - provider: 4\n    button: \{fileID: -?\d+\}\n    mark: \{fileID: -?\d+\}\n", "", f[AUTH]))),
        ("a retired component left behind", lambda f: f.__setitem__(AUTH, f[AUTH] + (
            "--- !u!114 &999000111\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_GameObject: {fileID: %s}\n  m_Enabled: 1\n"
            % AU["auth_go"]))),
        ("a Steam button back in the chooser", lambda f: f.__setitem__(AUTH, f[AUTH].replace(
            "  providerButtons:\n", "  providerButtons:\n  - provider: 2\n    button: {fileID: 0}\n    mark: {fileID: 0}\n", 1))),
        ("Google's button image tinted", lambda f: f.__setitem__(AUTH, _tint_face(f[AUTH]))),
        ("Apple's artwork altered", lambda f: f.__setitem__(f"{BRAND_DIR}/{BRANDS['appleButton'][0]}", b"\x89PNG recoloured")),
        ("a stand-in Steam mark", lambda f: f.__setitem__(PROVIDERS_ASSET, f[PROVIDERS_ASSET].replace(
            "  steamMark: {fileID: 0}", "  steamMark: " + K.sprite_ref(K.chrome_sprite("chrome_provider_steam"))))),
        ("Apple dropped from Android", lambda f: f.__setitem__(PROVIDERS_ASSET, f[PROVIDERS_ASSET].replace(
            "  androidOrder:\n  - 3\n  - 4\n", "  androidOrder:\n  - 3\n"))),
        ("Steam no longer silent", lambda f: f.__setitem__(PROVIDERS_ASSET, f[PROVIDERS_ASSET].replace("desktopSilentProvider: 2", "desktopSilentProvider: 0"))),
        ("a band with a colour of its own", edit_doc(SPLASH, str(splash_ctx().c("Splash/Poster/Stage/BandJade/Face", "slant")),
                                             lambda d: d.replace("a: 0}", "a: 0.74}", 1))),
        ("a typed colour on the poster", edit_doc(AUTH, str(auth_ctx().c("Auth/Poster/Foot", "img")),
                                          lambda d: re.sub(r"  m_Color: .*", "  m_Color: {r: 0.1, g: 0.2, b: 0.3, a: 1}", d, count=1))),
        ("the bar's steps disagree with the model", edit_doc(AUTH, str(auth_ctx().c("Auth/Readout", "stepbar")),
                                                     lambda d: d.replace(f"  - {K.f32(STEP_ENDS[1])}\n", "  - 0.3\n", 1))),
        ("the bar off the boot status channel", edit_doc(SPLASH, str(splash_ctx().c("Splash/Readout", "stepbar")),
                                                 lambda d: re.sub(r"  requestEvent: .*", "  requestEvent: {fileID: 0}", d, count=1))),
        ("a captain missing", lambda f: f.__setitem__(SPLASH, f[SPLASH].replace(art_guid("captain_galleon.jpg"), "0" * 32))),
        ("the retry window see-through", edit_doc(SPLASH, str(splash_ctx().c("Retry/Fill", "th")), lambda d: d.replace("  alpha: 1\n", "  alpha: 0.92\n"))),
        ("the bar stays up under the retry window", edit_doc(SPLASH, SP["panel_comp"], lambda d: re.sub(
            r"(  readoutGroup: )\{fileID: -?\d+\}", r"\1{fileID: %s}" % splash_ctx().c("Splash/Readout/StepWords", "cg"), d))),
        ("the poster lost its exit", lambda f: f.__setitem__(SPLASH, f[SPLASH].replace(f"guid: {S['exit']}", "0" * 32))),
        ("the Froglet logo back at the top left", edit_doc(SPLASH, str(splash_ctx().rt("Splash/Poster/Froglet")),
                                                  lambda d: d.replace("  m_AnchorMin: {x: 1, y: 0}", "  m_AnchorMin: {x: 0, y: 1}", 1))),
        ("a band lost a rim", edit_doc(AUTH, str(auth_ctx().c("Auth/Poster/Stage/BandGold", "paint")),
                               lambda d: re.sub(r"(  rims:\n  - .*\n)  - .*\n", r"\1", d, count=1))),
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
    print("OK: the start flow validates (Bootstrap splash, sign-in scene, the poster and its bar, privacy sprites)")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    pictures = art_bytes()
    for path in stale:
        os.makedirs(os.path.dirname(os.path.join(REPO, path)), exist_ok=True)
        if path in pictures:
            with open(os.path.join(REPO, path), "wb") as fh:
                fh.write(pictures[path])
        else:
            with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="\n") as fh:
                fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())

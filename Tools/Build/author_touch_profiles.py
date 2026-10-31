#!/usr/bin/env python3
"""Author Resources/TouchControlProfiles.asset - every hull's phone controls in one place.

    python3 Tools/Build/author_touch_profiles.py           # write the asset (and its .meta once)
    python3 Tools/Build/author_touch_profiles.py --check   # exit 1 if the asset differs (drift gate)

The decisions are Garrett's, 2026-10-21 (Docs/Reports/MOBILE_VESSELS.md section 8):
  * thumb-lift hulls keep their lifts and show a glyph on the card the lift drives;
  * a hull that needs its cards gets TotemButtons: no thumb-lift abilities, the cards (plus the extra
    buttons below) laid out round the right thumb;
  * the Dolphin's one-thumb aim rolls one to one with the phone (2026-10-22: "like augmented reality"); the Manta soars and the Thresher aims its launch with no
    thumb down, steering by tilt (the Manta's pitch turned over); the Rhino energizes with no thumb down;
  * the Scarab has a set-and-hold throttle slider and a drift button, the Brittle Star two fire buttons
    whose drag aims, the Shrike a fire button and a zoom toggle, the Serpent's scope latches.

The tuning numbers are the library's own defaults (TouchControlProfileLibrarySO); this script owns
the per-hull rows and the art references. A hull with no row flies the default thumb-lift profile.
"""
import hashlib
import os
import sys

ASSET = "Assets/Resources/TouchControlProfiles.asset"
SCRIPT_META = "Assets/_Scripts/Controller/IO/TouchControls/TouchControlProfileLibrarySO.cs.meta"

VESSEL = dict(Manta=1, Dolphin=2, Rhino=3, Urchin=4, Grizzly=5, Squirrel=6, Serpent=7, Shrike=10, Sparrow=11,
              Scarab=12, Thresher=14, Stoat=15, BrittleStar=16, Angler=17, Goby=18, Wrasse=19, Orca=20, Honeyguide=23,
              TigerMoth=29)
INPUT = dict(RightStick=1, LeftStick=2, Flip=3, Idle=4, A=6, B=7, X=8, Y=14)
ELEMENT = dict(none=0, Charge=1, Mass=2, Space=3, Time=4)
SCHEME = dict(ThumbLifts=0, TotemButtons=1)
GLYPH = dict(LiftLeftThumb=1, LiftRightThumb=2, LiftEitherThumb=3, LiftBothThumbs=4, ThumbsApart=5)
CORE = dict(Drift=1)
HOLD, TOGGLE = 0, 1


def art_guid(name):
    # Tools/Build/touch_glyph_art.py derives every guid from the file name the same way.
    return hashlib.md5(("touch-glyph:" + name).encode()).hexdigest()


def sprite(name):
    return "{fileID: 21300000, guid: %s, type: 3}" % art_guid(name)


def button(inp, icon, behaviour=HOLD, aims=False, primary=False, tint="none"):
    return dict(input=INPUT[inp], behaviour=behaviour, aims=aims, icon=icon, tint=ELEMENT[tint], primary=primary)


def profile(vessel, scheme, **kw):
    p = dict(vessel=VESSEL[vessel], scheme=SCHEME[scheme], liftDefer=0, tiltRoll=False, tiltSteer=False,
             tiltFlipPitch=False, tiltFlipYaw=False, oneThumbCurve=0,
             idleTriggers=False, idleFull=False, toggles=[], displayOnly=[], extras=[], slider=False, sliderStart=0.5,
             glyphs=[], coreGlyphs=[])
    p.update(kw)
    return p


PROFILES = [
    # ---- Thumb lifts: lift the RIGHT thumb = the pad's LT, the LEFT = RT, both = IdleAction.
    profile("Squirrel", "ThumbLifts",
            glyphs=[("Mass", "LiftLeftThumb")], coreGlyphs=[("Drift", "LiftRightThumb")]),
    # Lift EITHER thumb: drift and Echo Sight together (touch overrides on the prefab); the phone rolls
    # the hull one to one while that thumb aims (TouchAttitudeRoll). One-thumb flight is softened at home and
    # as sharp at the rim (Garrett, 2026-10-25: "too sensitive around the home... I like that it can turn sharp").
    profile("Dolphin", "ThumbLifts", tiltRoll=True, oneThumbCurve=0.6,
            glyphs=[("Charge", "LiftEitherThumb"), ("Time", "LiftEitherThumb")]),
    # Yastri on either lift; both thumbs off soars (IdleAction -> the boost) steered by tilt.
    # Flat out: the throttle reads full with no thumb down (720 u/s, the pad's full soar). Its tilt PITCH is turned
    # over (Garrett, 2026-10-22: "both left/right and up/down were the opposite of my expectation"); its yaw was
    # flipped with it and put back on 2026-10-25 ("manta gyro has its yaw controls flipped from intended").
    profile("Manta", "ThumbLifts", tiltSteer=True, tiltFlipPitch=True, idleFull=True,
            glyphs=[("Mass", "LiftEitherThumb"), ("Time", "LiftBothThumbs")]),
    # A lift swings that side's blade; both thumbs off energizes (both triggers fully pulled).
    profile("Rhino", "ThumbLifts", idleTriggers=True,
            glyphs=[("Charge", "LiftBothThumbs"), ("Mass", "LiftEitherThumb"), ("Space", "LiftEitherThumb"),
                    ("Time", "ThumbsApart")]),
    # The Tiger Moth (the Butterfly's airframe with the Drop; the Butterfly retired 2026-10-29) keeps its LT binding: lift
    # the right thumb to drop, put it back to land (R_VesselActions/TIGER_MOTH.md).
    profile("TigerMoth", "ThumbLifts",
            glyphs=[("Mass", "LiftLeftThumb"), ("Time", "LiftRightThumb")]),
    # Plant on the right lift, Winch on the left; both off aims the launch by tilt. The Mass Mode card
    # is a button, so a lift waits 0.12 s and is dropped if the finger lands on it.
    profile("Thresher", "ThumbLifts", liftDefer=0.12, tiltSteer=True,
            glyphs=[("Time", "LiftRightThumb"), ("Space", "LiftLeftThumb")]),
    # The crew hulls read their triggers through CrewTriggerActionSO / HoneyguideTriggerActionSO (RT = RightStickAction,
    # LT = LeftStickAction), so a lift is the trigger. The Wrasse's pilot (the Grouper) spends its saved boosts on RT: lift
    # the left thumb and fly on with the right ("The grouper can not use boost on mobile", Garrett, 2026-10-21).
    profile("Wrasse", "ThumbLifts", glyphs=[("Time", "LiftLeftThumb")]),
    # The Orca's helm: RT boosts while the tank lasts, LT raises the shield.
    profile("Orca", "ThumbLifts", glyphs=[("Time", "LiftLeftThumb"), ("Mass", "LiftRightThumb")]),
    # The Goby perches and aims, then darts (Garrett, 2026-10-22): its Dart and Lock, and the Pistol seat's Ghost, Snap and
    # Back, are each seat's own buttons round the right thumb, drawn by the seat (GobyPistolButtons), not profile extras
    # (only the seat the person is in shows them). Nothing is bound to its thumb lifts, so lifting a thumb fires nothing.
    profile("Goby", "ThumbLifts"),
    # The Honeyguide's RT Peck and LT Flit; its Time card (B, the boost) is a button.
    profile("Honeyguide", "ThumbLifts", glyphs=[("Charge", "LiftLeftThumb"), ("Mass", "LiftRightThumb")]),

    # ---- Totem buttons: no thumb-lift abilities; cards (and extras) round the right thumb.
    profile("Urchin", "TotemButtons"),
    profile("Sparrow", "TotemButtons"),
    profile("Serpent", "TotemButtons", toggles=["Space"]),
    profile("Grizzly", "TotemButtons", extras=[
        button("LeftStick", "TouchIcon_Bomb", primary=True),
        button("RightStick", "TouchIcon_Bomb", primary=True)]),
    profile("Scarab", "TotemButtons", displayOnly=["Time"], slider=True, sliderStart=0.5, extras=[
        button("LeftStick", "TouchIcon_Drift", primary=True, tint="Time")]),
    profile("Shrike", "TotemButtons", extras=[
        button("RightStick", "TouchIcon_Fire", primary=True),
        button("LeftStick", "TouchIcon_Zoom", behaviour=TOGGLE, tint="Charge")]),
    profile("BrittleStar", "TotemButtons", extras=[
        button("RightStick", "TouchIcon_Fire", aims=True, primary=True, tint="Space"),
        button("LeftStick", "TouchIcon_Ring", aims=True, primary=True, tint="Charge")]),
    profile("Stoat", "TotemButtons", extras=[
        button("RightStick", "TouchIcon_Dipole", primary=True, tint="Space"),
        button("A", "TouchIcon_Hold", tint="Mass")]),
    # The Angler's squad pick and recall were retired on 2026-10-21 (two alternating groups, ANGLER.md §1m): its four cards
    # are everything, so it takes no extra buttons.
    profile("Angler", "TotemButtons"),
]


def yaml_list(items, indent, render):
    pad = " " * indent
    if not items:
        return " []\n"
    return "\n" + "".join(pad + "- " + render(i).replace("\n", "\n" + pad + "  ") + "\n" for i in items)


def render_button(b):
    return ("input: %d\nbehaviour: %d\naimsWhileHeld: %d\nicon: %s\ntint: %d\nlabel: \nprimary: %d"
            % (b["input"], b["behaviour"], 1 if b["aims"] else 0, sprite(b["icon"]), b["tint"],
               1 if b["primary"] else 0))


def render_profile(p):
    out = "vessel: %d\nscheme: %d\nliftDeferSeconds: %s\ntiltRollOnOneThumb: %d\ntiltSteersWhileIdle: %d\n" % (
        p["vessel"], p["scheme"], p["liftDefer"], int(p["tiltRoll"]), int(p["tiltSteer"]))
    out += "flipTiltSteerPitch: %d\nflipTiltSteerYaw: %d\n" % (int(p["tiltFlipPitch"]), int(p["tiltFlipYaw"]))
    out += "oneThumbResponseCurve: %s\n" % p["oneThumbCurve"]
    out += "idlePullsBothTriggers: %d\nidleFullThrottle: %d\n" % (int(p["idleTriggers"]), int(p["idleFull"]))
    out += "toggleElements:" + yaml_list([ELEMENT[e] for e in p["toggles"]], 0, str).rstrip("\n") + "\n"
    out += "displayOnlyElements:" + yaml_list([ELEMENT[e] for e in p["displayOnly"]], 0, str).rstrip("\n") + "\n"
    out += "extraButtons:" + yaml_list(p["extras"], 0, render_button).rstrip("\n") + "\n"
    out += "throttleSlider: %d\nthrottleSliderStart: %s\n" % (int(p["slider"]), p["sliderStart"])
    out += "glyphs:" + yaml_list(p["glyphs"], 0,
                                 lambda g: "element: %d\nglyph: %d" % (ELEMENT[g[0]], GLYPH[g[1]])).rstrip("\n") + "\n"
    out += "coreGlyphs:" + yaml_list(p["coreGlyphs"], 0,
                                     lambda g: "core: %d\nglyph: %d" % (CORE[g[0]], GLYPH[g[1]])).rstrip("\n")
    return out


def build():
    script_guid = None
    for line in open(SCRIPT_META):
        if line.startswith("guid:"):
            script_guid = line.split()[1]
    head = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: TouchControlProfiles
  m_EditorClassIdentifier:
""" % script_guid
    body = "  profiles:" + yaml_list(PROFILES, 2, render_profile)
    tail = """  tiltFullScaleDegrees: 25
  tiltDeadZoneDegrees: 2.5
  tiltSmoothingPerSecond: 12
  tiltRollMaxDegrees: 90
  noseFollowPerSecond: 9
  buttonMillimetres: 10
  buttonGapMillimetres: 1.5
  innerArcMillimetres: 22
  arcSpacingMillimetres: 12
  maxHeightFraction: 0.55
  maxWidthFraction: 0.36
  glyphLiftLeftThumb: %s
  glyphLiftRightThumb: %s
  glyphLiftEitherThumb: %s
  glyphLiftBothThumbs: %s
  glyphThumbsApart: %s
  buttonDisc: %s
  buttonRim: %s
  sliderWidthMillimetres: 9
  sliderHeightMillimetres: 32
""" % (sprite("TouchGlyph_LiftLeftThumb"), sprite("TouchGlyph_LiftRightThumb"), sprite("TouchGlyph_LiftEitherThumb"),
       sprite("TouchGlyph_LiftBothThumbs"), sprite("TouchGlyph_ThumbsApart"), sprite("TouchIcon_Disc"),
       sprite("TouchIcon_DiscRing"))
    return head + body + tail


PREFABS = "Assets/_Prefabs/Spacevessels/%s.prefab"
MAPS = "Assets/Resources/ElementalAbilityMaps/%s.asset"


def bound_inputs(vessel):
    """InputEvents a touch press resolves on this hull: the shared map plus the touch overrides."""
    import re
    text = open(PREFABS % vessel).read()
    bound = set()
    for key in ("_inputEventShipActions", "_touchActionOverrides"):
        m = re.search(r"\n  %s:(.*?)(?=\n  [A-Za-z_])" % key, text, re.S)
        if not m:
            continue
        for entry in re.split(r"\n  - ", m.group(1)):
            ie = re.search(r"InputEvent: (\d+)", entry)
            if ie and re.search(r"guid: [0-9a-f]{32}", entry):
                bound.add(int(ie.group(1)))
    return bound


def map_inputs(vessel):
    """Element -> InputEvent as the hull's ElementalAbilityMap authors it (0 = passive)."""
    import re
    out = {}
    path = MAPS % vessel
    if not os.path.exists(path):
        return out
    for el, inp in re.findall(r"Element: (\d+)\s*\n(?:.*\n)*?\s*Input: (\d+)", open(path).read()):
        out.setdefault(int(el), int(inp))
    return out


# What a thumb lift raises (TouchInputStrategy): the "only" event at once and, while the other thumb flies on, that thumb's
# stick event - lifting the LEFT thumb is OnlyRightStickAction + RightStickAction (the pad's RT), the right thumb LT's pair.
LIFT_RAISES = {"LiftLeftThumb": [{11, INPUT["RightStick"]}], "LiftRightThumb": [{12, INPUT["LeftStick"]}],
               "LiftEitherThumb": [{11, INPUT["RightStick"]}, {12, INPUT["LeftStick"]}]}


def lift_errors(vessel, glyphs, bound):
    """A card whose glyph names a thumb lift must have that lift press something on the hull (a ThumbLifts hull)."""
    errors = []
    for element, glyph in glyphs:
        for raised in LIFT_RAISES.get(glyph, []):
            if not raised & bound:
                errors.append("%s: the %s card shows %s, but no input that lift raises %s is bound"
                              % (vessel, element, glyph, sorted(raised)))
    return errors


def binding_errors():
    """Every button a phone shows on a TotemButtons hull must press something bound on that hull, and every thumb lift a
    ThumbLifts hull's card names must too (the Wrasse's boost read the analog trigger, which no phone writes, and its card
    named no control: "The grouper can not use boost on mobile", Garrett, 2026-10-21)."""
    errors = []
    names = {v: k for k, v in VESSEL.items()}
    for p in PROFILES:
        if p["scheme"] != SCHEME["TotemButtons"]:
            vessel = names[p["vessel"]]
            errors += lift_errors(vessel, p["glyphs"] + p["coreGlyphs"], bound_inputs(vessel))
            continue
        vessel = names[p["vessel"]]
        bound = bound_inputs(vessel)
        for b in p["extras"]:
            if b["input"] not in bound:
                errors.append("%s: extra button input %d presses nothing on touch" % (vessel, b["input"]))
        display_only = {ELEMENT[e] for e in p["displayOnly"]}
        for element, inp in map_inputs(vessel).items():
            if inp == 0 or element in display_only:
                continue
            if inp not in bound:
                errors.append("%s: element %d's card (input %d) presses nothing on touch" % (vessel, element, inp))
    return errors


def main():
    text = build()
    errors = binding_errors()
    for e in errors:
        print("author_touch_profiles: " + e)
    for name in ("TouchIcon_Disc", "TouchGlyph_LiftLeftThumb"):
        if not os.path.exists("Assets/_Graphics/Design Assests/HUD UI/Touch/%s.png.meta" % name):
            print("author_touch_profiles: run Tools/Build/touch_glyph_art.py first (missing %s)" % name)
            return 1
    if "--check" in sys.argv:
        if errors:
            return 1
        if not os.path.exists(ASSET) or open(ASSET).read() != text:
            print("author_touch_profiles: %s differs from the generator" % ASSET)
            return 1
        print("author_touch_profiles: OK (%d hulls)" % len(PROFILES))
        return 0
    open(ASSET, "w").write(text)
    meta = ASSET + ".meta"
    if not os.path.exists(meta):
        guid = hashlib.md5(b"touch-control-profiles").hexdigest()
        open(meta, "w").write("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                              "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n"
                              "  assetBundleVariant: \n" % guid)
    print("author_touch_profiles: wrote %s (%d hulls)" % (ASSET, len(PROFILES)))
    return 0


if __name__ == "__main__":
    sys.exit(main())

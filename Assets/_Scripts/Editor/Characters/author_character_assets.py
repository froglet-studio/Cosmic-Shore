#!/usr/bin/env python3
"""Authors the chimeric character generator's DATA: the six clade assets + the human source,
the generation config asset, and every .meta under the two character folders.

    python3 Assets/_Scripts/Editor/Characters/author_character_assets.py            # write
    python3 Assets/_Scripts/Editor/Characters/author_character_assets.py --check    # fail on drift
    python3 ... --fixture /path/CladeFixtures.cs                                    # emit the offline-harness fixture

The clade table below is the SOURCE; the .asset files are the build. Field names are checked
against the C# by regex before anything is written, so a renamed field fails here rather than
silently defaulting in Unity. Guids are md5 of the repo-relative path, so re-runs are
byte-stable and cross-references never move.
"""
import hashlib, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
RUNTIME = "Assets/_Scripts/Controller/Characters"
EDITOR = "Assets/_Scripts/Editor/Characters"
CLADE_DIR = RUNTIME + "/Resources/Characters/Clades"
CONFIG_PATH = RUNTIME + "/Resources/Characters/CharacterGenerationConfig.asset"

def guid_for(rel):
    return hashlib.md5(("cosmicshore/characters/" + rel).encode()).hexdigest()

def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
        return f.read()

# ---------------------------------------------------------------- enums from the C#
def parse_enum(src, name):
    m = re.search(r"enum\s+" + name + r"\s*\{(.*?)\}", src, re.S)
    assert m, name
    out, nxt = {}, 0
    for line in m.group(1).split("\n"):
        line = line.split("//")[0].strip().rstrip(",")
        if not line or line.startswith("///") or line.startswith("["): continue
        mm = re.match(r"(\w+)\s*(?:=\s*(-?\d+))?", line)
        if not mm: continue
        if mm.group(2) is not None: nxt = int(mm.group(2))
        out[mm.group(1)] = nxt; nxt += 1
    return out

FK = read(RUNTIME + "/FeatureKind.cs")
HeadAxis = parse_enum(read(RUNTIME + "/HeadAxis.cs"), "HeadAxis")
FeatureKind = parse_enum(FK, "FeatureKind")
FeatureSlot = parse_enum(FK, "FeatureSlot")
PupilKind = parse_enum(FK, "PupilKind")
CoveringKind = parse_enum(FK, "CoveringKind")
MarkingKind = parse_enum(FK, "MarkingKind")

# ---------------------------------------------------------------- serialized field sets (parity)
def fields_of_class(src, cls):
    m = re.search(r"class\s+" + cls + r"\b[^{]*\{", src)
    assert m, cls
    depth, i, body = 0, m.end() - 1, []
    start = m.end()
    while i < len(src):
        if src[i] == "{": depth += 1
        elif src[i] == "}":
            depth -= 1
            if depth == 0: break
        i += 1
    body = src[start:i]
    names = []
    for line in body.split("\n"):
        s = re.sub(r"^\s*(?:\[[^\]\n]*\]\s*)+", "", line.strip())
        if "=>" in s or "{" in s or "(" in s.split("=")[0]: continue
        mm = re.match(r"public\s+(?!static|const|override|virtual|void)([\w<>\[\],\.]+)\s+(\w+)\s*(=|;)", s)
        if mm:
            names.append(mm.group(2))
    return names

CLADE_SRC = read(RUNTIME + "/CladeSO.cs")
PARAMS_SRC = read(RUNTIME + "/FeatureParams.cs")
FIELDS = {
    "CladeSO": fields_of_class(CLADE_SRC, "CladeSO"),
    "TraitDefinition": fields_of_class(CLADE_SRC, "TraitDefinition"),
    "CoveringRecipe": fields_of_class(CLADE_SRC, "CoveringRecipe"),
}
for cls in ["EyeParams","PinnaParams","BeakParams","MandibleParams","NoseLeafParams","AntennaParams",
            "CrestParams","BlowholeParams","WhiskerParams","HairParams","FangParams","TrunkParams","FeatureParams"]:
    FIELDS[cls] = fields_of_class(PARAMS_SRC, cls)

PARAM_GROUP_CLASS = {"Eye":"EyeParams","Pinna":"PinnaParams","Beak":"BeakParams","Mandible":"MandibleParams",
                     "NoseLeaf":"NoseLeafParams","Antenna":"AntennaParams","Crest":"CrestParams",
                     "Blowhole":"BlowholeParams","Whisker":"WhiskerParams","Hair":"HairParams","Fang":"FangParams","Trunk":"TrunkParams"}

def check_keys(cls, d):
    bad = [k for k in d if k not in FIELDS[cls]]
    assert not bad, f"{cls}: unknown keys {bad}; known {FIELDS[cls]}"

# ---------------------------------------------------------------- helpers
def C(r, g, b, a=1.0): return ("color", r, g, b, a)
def axes(**kv):
    for k in kv: assert k in HeadAxis, k
    return [(k, v) for k, v in kv.items()]
def trait(id, feature="None", slots=(), site="", signature=False, min_weight=0.0,
          overrides=None, pitch=0.0, yaw=0.0, roll=0.0, **groups):
    for g in groups: assert g in PARAM_GROUP_CLASS, g; check_keys(PARAM_GROUP_CLASS[g], groups[g])
    for s in slots: assert s in FeatureSlot, s
    assert feature in FeatureKind, feature
    return dict(Id=id, Signature=signature, MinWeight=min_weight, Feature=feature, Slots=list(slots),
                Site=site, SitePitchDeg=pitch, SiteYawDeg=yaw, SiteRollDeg=roll,
                AxisOverrides=[(k, v) for k, v in (overrides or {}).items()], Params=groups)
def covering(**kv):
    check_keys("CoveringRecipe", kv); return kv

# ---------------------------------------------------------------- THE CLADE TABLE
HUMAN_EYE = dict(Radius=0.058, IrisFraction=0.50, LidOpen=0.58, CanthalTiltDeg=3.0, LidThickness=0.011,
                 Pupil="Round", PupilSize=0.34, IrisA=C(0.30,0.18,0.08), IrisB=C(0.34,0.50,0.58), Sclera=C(0.88,0.85,0.80))

CLADES = [
 dict(Key="Human", DisplayName="Human", IsHuman=True, AxisTargets=[],
      Traits=[
        trait("Hair", "HairCap", ["Crown"], "Crown", signature=True, Hair=dict(Thickness=0.030, FrontHairlineDeg=44, SideHairlineDeg=68, BackHairlineDeg=108, Clumping=0.5, StrandCount=700, StrandLength=0.30, StrandWidth=0.036, PartDeg=26, Fringe=0.3, BrowCards=60)),
        trait("Eyes", "VertebrateEye", ["Eyes"], "Eye", signature=True, Eye=HUMAN_EYE),
        trait("Ears", "Pinna", ["Ears"], "EarSide", signature=True, Pinna=dict(Height=0.19, Width=0.105, Pointed=0.0, CupDepth=0.45, RimThickness=0.16, Tragus=0.4, ForwardTiltDeg=24, OutwardRollDeg=6, Thickness=0.012)),
        trait("Nose", "None", ["Nose"], signature=True),
        trait("Mouth", "None", ["Mouth"], signature=True),
      ],
      Covering=covering(Kind="Skin", BaseA=C(0.75,0.58,0.48), BaseB=C(0.45,0.30,0.22), Marking="None", MarkingColor=C(0,0,0),
                        MarkingStrength=0.0, MarkingScale=1.0, Smoothness=0.32, DetailStrength=0.75, AnchorThetaDeg=180.0,
                        ReachDegAtMin=0.0, ReachDegAtMax=0.0, PaintsBrows=True)),

 dict(Key="Felidae", DisplayName="Felidae", IsHuman=False,
      AxisTargets=axes(MuzzleLength=0.35, MuzzleWidth=0.40, CranialWidth=0.35, OrbitalSize=0.55, OrbitalSpacing=0.35, JawWidth=0.20,
                       NoseProjection=-0.70, NoseWidth=0.50, ChinProjection=-0.60, LowerFaceHeight=-0.45, LipFullness=-0.50,
                       BrowRidge=-0.30, CheekboneWidth=0.50, ForeheadBulge=0.15, CranialHeight=-0.10, NeckThickness=0.20),
      Traits=[
        trait("PointedEars", "Pinna", ["Ears"], "EarTop", signature=True, pitch=78.0, roll=-12.0,
              Pinna=dict(Height=0.23, Width=0.16, Pointed=1.0, CupDepth=0.40, RimThickness=0.12, Tragus=0.0, ForwardTiltDeg=0.0, OutwardRollDeg=0.0, Thickness=0.010, RootOffset=0.85)),
        trait("SlitEyes", "VertebrateEye", ["Eyes"], "Eye", signature=True,
              Eye=dict(Radius=0.068, IrisFraction=0.80, LidOpen=0.72, CanthalTiltDeg=9.0, LidThickness=0.010, Pupil="VerticalSlit", PupilSize=0.55,
                       IrisA=C(0.88,0.58,0.12), IrisB=C(0.45,0.74,0.36), Sclera=C(0.88,0.86,0.80))),
        trait("Whiskers", "Whiskers", ["Cheeks"], "Cheek", signature=True, Whisker=dict(PerSide=4, Length=0.24, Radius=0.0032, FanDeg=26.0)),
        trait("Fangs", "Fangs", [], "MouthCorner", Fang=dict(Length=0.05, Radius=0.011, Keratin=C(0.92,0.88,0.80))),
        trait("Coat", "None", [], signature=True),
      ],
      Covering=covering(Kind="Fur", BaseA=C(0.74,0.54,0.30), BaseB=C(0.56,0.53,0.49), Marking="Stripes", MarkingColor=C(0.20,0.13,0.08),
                        MarkingStrength=0.7, MarkingScale=7.0, Smoothness=0.25, DetailStrength=0.8, AnchorThetaDeg=0.0,
                        ReachDegAtMin=58.0, ReachDegAtMax=132.0, PaintsBrows=False)),

 dict(Key="Corvidae", DisplayName="Corvidae", IsHuman=False,
      AxisTargets=axes(OrbitalSize=0.35, OrbitalSpacing=0.60, BrowRidge=-0.60, ChinProjection=-0.80, LowerFaceHeight=-0.60,
                       LipFullness=-1.0, NoseProjection=-1.0, CranialHeight=0.25, CranialLength=0.20, NeckThickness=-0.35,
                       NeckLength=0.40, CheekboneWidth=-0.30, JawWidth=-0.40, MuzzleLength=0.20, ForeheadBulge=0.20),
      Traits=[
        trait("Beak", "Beak", ["Mouth", "Nose"], "Muzzle", signature=True, pitch=16,
              overrides=dict(LipFullness=-1.0, NoseProjection=-1.0, ChinProjection=-0.6),
              Beak=dict(Length=0.42, Hook=0.12, BaseHeight=0.95, BaseWidth=0.80, TipSharpness=0.75, GapeHeight=0.42, Culmen=0.20,
                        KeratinA=C(0.05,0.05,0.06), KeratinB=C(0.13,0.12,0.11))),
        trait("BeadEyes", "VertebrateEye", ["Eyes"], "Eye", signature=True,
              Eye=dict(Radius=0.056, IrisFraction=0.90, LidOpen=0.85, CanthalTiltDeg=0.0, LidThickness=0.008, Pupil="Dark", PupilSize=0.8,
                       IrisA=C(0.10,0.08,0.06), IrisB=C(0.25,0.18,0.10), Sclera=C(0.35,0.30,0.28))),
        trait("Plumage", "None", [], signature=True),
        trait("NoEars", "None", ["Ears"]),
        trait("Crest", "Crest", ["Crown"], "CrownFront", Crest=dict(Feathers=9, Length=0.22, Width=0.045, LeanDeg=40.0, FanDeg=30.0)),
      ],
      Covering=covering(Kind="Feather", BaseA=C(0.04,0.04,0.06), BaseB=C(0.09,0.08,0.11), Marking="Sheen", MarkingColor=C(0.18,0.26,0.62),
                        MarkingStrength=0.9, MarkingScale=1.0, Smoothness=0.45, DetailStrength=0.8, AnchorThetaDeg=0.0,
                        ReachDegAtMin=52.0, ReachDegAtMax=126.0, PaintsBrows=False)),

 dict(Key="Cetacea", DisplayName="Cetacea", IsHuman=False,
      AxisTargets=axes(ForeheadBulge=1.0, MuzzleLength=0.45, MuzzleWidth=0.60, OrbitalSize=-0.55, OrbitalSpacing=0.90, OrbitalDepth=-0.60,
                       BrowRidge=-1.0, NoseProjection=-1.0, NoseWidth=-0.30, LipFullness=-0.60, MouthWidth=1.0, JawWidth=0.50,
                       ChinProjection=-0.40, LowerFaceHeight=-0.20, NeckThickness=1.0, NeckLength=-0.50, CranialWidth=0.40,
                       CranialLength=0.30, CheekboneWidth=-0.40),
      Traits=[
        trait("Blowhole", "Blowhole", ["Nose", "Crown"], "CrownBack", signature=True,
              overrides=dict(NoseProjection=-1.0, NoseWidth=-0.5), Blowhole=dict(Radius=0.045, RimHeight=0.012)),
        trait("Melon", "None", [], signature=True, overrides=dict(ForeheadBulge=1.0, BrowRidge=-1.0)),
        trait("NoEars", "None", ["Ears"]),
        trait("SmallEyes", "VertebrateEye", ["Eyes"], "Eye",
              Eye=dict(Radius=0.048, IrisFraction=0.85, LidOpen=0.55, CanthalTiltDeg=-4.0, LidThickness=0.009, Pupil="Dark", PupilSize=0.7,
                       IrisA=C(0.08,0.07,0.06), IrisB=C(0.16,0.12,0.10), Sclera=C(0.55,0.50,0.48))),
        trait("Rostrum", "None", [], overrides=dict(MuzzleLength=0.55, MouthWidth=1.0)),
      ],
      Covering=covering(Kind="Hide", BaseA=C(0.42,0.50,0.58), BaseB=C(0.28,0.34,0.42), Marking="Countershade", MarkingColor=C(0.80,0.82,0.84),
                        MarkingStrength=0.85, MarkingScale=1.0, Smoothness=0.75, DetailStrength=0.4, AnchorThetaDeg=0.0,
                        ReachDegAtMin=72.0, ReachDegAtMax=170.0, PaintsBrows=False)),

 dict(Key="Chiroptera", DisplayName="Chiroptera", IsHuman=False,
      AxisTargets=axes(MuzzleLength=0.45, MuzzleWidth=-0.20, OrbitalSize=0.35, OrbitalSpacing=0.20, BrowRidge=0.25, ChinProjection=-0.50,
                       LowerFaceHeight=-0.40, NoseProjection=0.20, NoseWidth=0.60, LipFullness=-0.30, MouthWidth=0.30, CranialWidth=0.20,
                       CranialHeight=-0.15, CheekboneWidth=0.30, NeckThickness=-0.20),
      Traits=[
        trait("GreatEars", "Pinna", ["Ears"], "EarTop", signature=True, pitch=72.0, roll=-8.0,
              Pinna=dict(Height=0.30, Width=0.15, Pointed=0.6, CupDepth=0.60, RimThickness=0.10, Tragus=1.0, ForwardTiltDeg=0.0, OutwardRollDeg=4.0, Thickness=0.008, RootOffset=0.85)),
        trait("NoseLeaf", "NoseLeaf", ["Nose"], "NoseTip", signature=True, overrides=dict(NoseProjection=0.3, NoseWidth=0.6),
              NoseLeaf=dict(Height=0.11, Width=0.07, Spear=0.7, Thickness=0.008)),
        trait("NightEyes", "VertebrateEye", ["Eyes"], "Eye",
              Eye=dict(Radius=0.058, IrisFraction=0.85, LidOpen=0.80, CanthalTiltDeg=4.0, LidThickness=0.009, Pupil="Dark", PupilSize=0.85,
                       IrisA=C(0.12,0.08,0.06), IrisB=C(0.28,0.16,0.08), Sclera=C(0.75,0.68,0.62))),
        trait("Fangs", "Fangs", [], "MouthCorner", Fang=dict(Length=0.045, Radius=0.010, Keratin=C(0.92,0.88,0.80))),
        trait("Fur", "None", [], signature=True),
      ],
      Covering=covering(Kind="Fur", BaseA=C(0.35,0.27,0.22), BaseB=C(0.22,0.18,0.16), Marking="Ridges", MarkingColor=C(0.15,0.10,0.08),
                        MarkingStrength=0.3, MarkingScale=4.0, Smoothness=0.30, DetailStrength=0.9, AnchorThetaDeg=0.0,
                        ReachDegAtMin=60.0, ReachDegAtMax=135.0, PaintsBrows=False)),

 dict(Key="Coleoptera", DisplayName="Coleoptera", IsHuman=False,
      AxisTargets=axes(CranialWidth=0.55, CranialHeight=-0.25, ForeheadBulge=0.40, BrowRidge=-0.80, OrbitalSize=0.90, OrbitalSpacing=0.90,
                       OrbitalDepth=-0.80, MuzzleLength=-0.70, NoseProjection=-1.0, LipFullness=-1.0, MouthWidth=-0.40, JawWidth=0.40,
                       ChinProjection=-0.50, LowerFaceHeight=-0.50, CheekboneWidth=0.40, NeckThickness=-0.40, NeckLength=-0.30),
      Traits=[
        trait("CompoundEyes", "CompoundEye", ["Eyes"], "Eye", signature=True,
              Eye=dict(Radius=0.062, IrisFraction=0.5, LidOpen=0.6, CanthalTiltDeg=0.0, LidThickness=0.01, Pupil="Compound", PupilSize=0.3,
                       IrisA=C(0.1,0.1,0.1), IrisB=C(0.2,0.2,0.2), Sclera=C(0.5,0.5,0.5), DomeRadius=0.095, DomeProud=0.50,
                       CompoundRim=C(0.50,0.40,0.22), CompoundFacet=C(0.07,0.05,0.04))),
        trait("Mandibles", "Mandibles", ["Mouth"], "Mouth", signature=True, overrides=dict(LipFullness=-1.0),
              Mandible=dict(Length=0.24, RootRadius=0.032, Curl=0.6, Spread=0.16, KeratinA=C(0.06,0.04,0.03), KeratinB=C(0.20,0.12,0.06))),
        trait("Antennae", "Antennae", ["Brow"], "Brow", signature=True,
              Antenna=dict(Length=0.28, Radius=0.012, Plates=3, PlateLength=0.07, ElevationDeg=35.0, SplayDeg=25.0, KeratinA=C(0.05,0.04,0.03), KeratinB=C(0.16,0.11,0.05))),
        trait("NoEars", "None", ["Ears"]),
        trait("ChitinDome", "None", ["Crown"], overrides=dict(CranialWidth=0.5)),
        trait("Chitin", "None", [], signature=True),
      ],
      Covering=covering(Kind="Chitin", BaseA=C(0.04,0.03,0.03), BaseB=C(0.10,0.07,0.04), Marking="Sheen", MarkingColor=C(0.10,0.45,0.28),
                        MarkingStrength=0.9, MarkingScale=1.0, Smoothness=0.85, DetailStrength=0.5, AnchorThetaDeg=0.0,
                        ReachDegAtMin=48.0, ReachDegAtMax=150.0, PaintsBrows=False)),

 dict(Key="Testudines", DisplayName="Testudines", IsHuman=False,
      AxisTargets=axes(MuzzleLength=-0.25, BrowRidge=-0.45, OrbitalSize=0.15, OrbitalSpacing=0.35, ChinProjection=-0.70, LowerFaceHeight=-0.50,
                       LipFullness=-1.0, NoseProjection=-0.90, NoseWidth=-0.40, MouthWidth=0.50, NeckThickness=0.50, NeckLength=0.70,
                       CranialLength=0.35, CranialHeight=-0.35, CranialWidth=0.15, JawWidth=0.10, CheekboneWidth=-0.20, ForeheadBulge=-0.30),
      Traits=[
        trait("HookedBeak", "Beak", ["Mouth", "Nose"], "Muzzle", signature=True, pitch=16, overrides=dict(LipFullness=-1.0, NoseProjection=-1.0),
              Beak=dict(Length=0.17, Hook=0.75, BaseHeight=0.85, BaseWidth=1.10, TipSharpness=0.9, GapeHeight=0.5, Culmen=0.05,
                        KeratinA=C(0.42,0.36,0.24), KeratinB=C(0.64,0.56,0.38))),
        trait("Scutes", "None", [], signature=True),
        trait("NoEars", "None", ["Ears"]),
        trait("HoodedEyes", "VertebrateEye", ["Eyes"], "Eye",
              Eye=dict(Radius=0.058, IrisFraction=0.70, LidOpen=0.45, CanthalTiltDeg=-6.0, LidThickness=0.012, Pupil="Round", PupilSize=0.45,
                       IrisA=C(0.55,0.40,0.12), IrisB=C(0.30,0.25,0.12), Sclera=C(0.80,0.74,0.62))),
        trait("NeckFolds", "None", ["Neck"], overrides=dict(NeckThickness=0.6, NeckLength=0.8)),
      ],
      Covering=covering(Kind="Scale", BaseA=C(0.40,0.42,0.22), BaseB=C(0.32,0.28,0.16), Marking="Scutes", MarkingColor=C(0.18,0.16,0.08),
                        MarkingStrength=0.8, MarkingScale=5.0, Smoothness=0.45, DetailStrength=0.9, AnchorThetaDeg=0.0,
                        ReachDegAtMin=72.0, ReachDegAtMax=160.0, PaintsBrows=False)),

 # ---- the six clades added for the avatar recreations (Docs: CHARACTERS.md §8) ----------------
 dict(Key="Leporidae", DisplayName="Leporidae", IsHuman=False,
      AxisTargets=axes(MuzzleLength=0.30, MuzzleWidth=-0.10, CranialWidth=-0.10, CranialLength=0.25, OrbitalSize=0.60, OrbitalSpacing=0.55,
                       NoseProjection=-0.55, NoseWidth=-0.20, ChinProjection=-0.70, LowerFaceHeight=-0.40, LipFullness=-0.60,
                       BrowRidge=-0.50, CheekboneWidth=0.30, ForeheadBulge=0.10, JawWidth=-0.20, NeckThickness=-0.20),
      Traits=[
        trait("TallEars", "Pinna", ["Ears"], "EarTop", signature=True, pitch=70.0, roll=-6.0,
              Pinna=dict(Height=0.46, Width=0.19, Pointed=0.35, CupDepth=0.55, RimThickness=0.10, Tragus=0.0, ForwardTiltDeg=0.0, OutwardRollDeg=0.0, Thickness=0.008, RootOffset=0.9)),
        trait("SideEyes", "VertebrateEye", ["Eyes"], "Eye", signature=True,
              Eye=dict(Radius=0.064, IrisFraction=0.88, LidOpen=0.80, CanthalTiltDeg=6.0, LidThickness=0.009, Pupil="Round", PupilSize=0.72,
                       IrisA=C(0.22,0.10,0.06), IrisB=C(0.60,0.18,0.16), Sclera=C(0.80,0.78,0.74))),
        trait("Whiskers", "Whiskers", ["Cheeks"], "Cheek", signature=True, Whisker=dict(PerSide=4, Length=0.22, Radius=0.0028, FanDeg=22.0)),
        trait("SplitNose", "None", ["Nose"], overrides=dict(NoseProjection=-0.6, NoseWidth=-0.3)),
        trait("RabbitMouth", "None", ["Mouth"], min_weight=0.45, overrides=dict(LipFullness=-0.8, MuzzleLength=0.35)),
        trait("Coat", "None", [], signature=True),
      ],
      Covering=covering(Kind="Fur", BaseA=C(0.62,0.50,0.36), BaseB=C(0.86,0.82,0.76), Marking="Countershade", MarkingColor=C(0.92,0.90,0.86),
                        MarkingStrength=0.6, MarkingScale=6.0, Smoothness=0.22, DetailStrength=0.8, AnchorThetaDeg=0.0,
                        ReachDegAtMin=60.0, ReachDegAtMax=150.0, PaintsBrows=False)),

 dict(Key="Canidae", DisplayName="Canidae", IsHuman=False,
      AxisTargets=axes(MuzzleLength=0.65, MuzzleWidth=0.10, CranialWidth=0.10, OrbitalSize=0.35, OrbitalSpacing=0.30,
                       NoseProjection=-0.40, NoseWidth=0.30, ChinProjection=-0.50, LowerFaceHeight=-0.30, LipFullness=-0.60,
                       BrowRidge=-0.20, CheekboneWidth=0.30, ForeheadBulge=-0.10, JawWidth=0.15, NeckThickness=0.30),
      Traits=[
        trait("PrickEars", "Pinna", ["Ears"], "EarTop", signature=True, pitch=74.0, roll=-10.0,
              Pinna=dict(Height=0.25, Width=0.16, Pointed=0.85, CupDepth=0.45, RimThickness=0.12, Tragus=0.0, ForwardTiltDeg=0.0, OutwardRollDeg=0.0, Thickness=0.010, RootOffset=0.85)),
        trait("Muzzle", "None", ["Nose"], signature=True, overrides=dict(MuzzleLength=0.7, NoseProjection=-0.3)),
        trait("DogEyes", "VertebrateEye", ["Eyes"], "Eye",
              Eye=dict(Radius=0.062, IrisFraction=0.84, LidOpen=0.70, CanthalTiltDeg=4.0, LidThickness=0.010, Pupil="Round", PupilSize=0.70,
                       IrisA=C(0.30,0.16,0.06), IrisB=C(0.10,0.08,0.06), Sclera=C(0.80,0.78,0.74))),
        trait("Whiskers", "Whiskers", ["Cheeks"], "Cheek", Whisker=dict(PerSide=3, Length=0.16, Radius=0.0026, FanDeg=18.0)),
        trait("Fangs", "Fangs", [], "MouthCorner", Fang=dict(Length=0.045, Radius=0.010, Keratin=C(0.92,0.88,0.80))),
        trait("DogMouth", "None", ["Mouth"], min_weight=0.45, overrides=dict(LipFullness=-0.9)),
        trait("Coat", "None", [], signature=True),
      ],
      Covering=covering(Kind="Fur", BaseA=C(0.80,0.74,0.62), BaseB=C(0.36,0.26,0.18), Marking="Countershade", MarkingColor=C(0.92,0.90,0.86),
                        MarkingStrength=0.7, MarkingScale=6.0, Smoothness=0.25, DetailStrength=0.8, AnchorThetaDeg=0.0,
                        ReachDegAtMin=60.0, ReachDegAtMax=145.0, PaintsBrows=False)),

 dict(Key="Squamata", DisplayName="Squamata", IsHuman=False,
      AxisTargets=axes(MuzzleLength=0.45, MuzzleWidth=0.30, CranialWidth=0.20, CranialHeight=-0.40, OrbitalSize=0.40, OrbitalSpacing=0.70,
                       NoseProjection=-0.90, NoseWidth=0.20, ChinProjection=-0.60, LowerFaceHeight=-0.20, LipFullness=-0.90,
                       MouthWidth=0.60, BrowRidge=0.30, CheekboneWidth=0.30, ForeheadBulge=-0.50, JawWidth=0.30, NeckThickness=0.30),
      Traits=[
        trait("Scales", "None", [], signature=True),
        trait("SlitEyes", "VertebrateEye", ["Eyes"], "Eye", signature=True,
              Eye=dict(Radius=0.066, IrisFraction=0.82, LidOpen=0.70, CanthalTiltDeg=8.0, LidThickness=0.009, Pupil="VerticalSlit", PupilSize=0.5,
                       IrisA=C(0.85,0.62,0.10), IrisB=C(0.55,0.72,0.25), Sclera=C(0.80,0.78,0.60))),
        trait("NoEars", "None", ["Ears"], signature=True),
        trait("FlatNose", "None", ["Nose"], overrides=dict(NoseProjection=-0.9)),
        trait("WideMouth", "None", ["Mouth"], overrides=dict(LipFullness=-0.9, MouthWidth=0.7)),
      ],
      Covering=covering(Kind="Scale", BaseA=C(0.22,0.52,0.30), BaseB=C(0.45,0.58,0.26), Marking="Scutes", MarkingColor=C(0.10,0.28,0.14),
                        MarkingStrength=0.7, MarkingScale=5.0, Smoothness=0.5, DetailStrength=0.9, AnchorThetaDeg=0.0,
                        ReachDegAtMin=80.0, ReachDegAtMax=165.0, PaintsBrows=False)),

 dict(Key="Hymenoptera", DisplayName="Hymenoptera", IsHuman=False,
      AxisTargets=axes(CranialWidth=0.40, CranialHeight=-0.20, ForeheadBulge=0.30, BrowRidge=-0.80, OrbitalSize=0.90, OrbitalSpacing=0.85,
                       OrbitalDepth=-0.80, MuzzleLength=-0.60, NoseProjection=-1.0, LipFullness=-1.0, MouthWidth=-0.40, JawWidth=0.20,
                       ChinProjection=-0.60, LowerFaceHeight=-0.50, CheekboneWidth=0.30, NeckThickness=-0.50, NeckLength=-0.20),
      Traits=[
        trait("CompoundEyes", "CompoundEye", ["Eyes"], "Eye", signature=True,
              Eye=dict(Radius=0.062, IrisFraction=0.5, LidOpen=0.6, CanthalTiltDeg=0.0, LidThickness=0.01, Pupil="Compound", PupilSize=0.3,
                       IrisA=C(0.1,0.1,0.1), IrisB=C(0.2,0.2,0.2), Sclera=C(0.5,0.5,0.5), DomeRadius=0.10, DomeProud=0.55,
                       CompoundRim=C(0.55,0.42,0.18), CompoundFacet=C(0.10,0.07,0.04))),
        trait("Antennae", "Antennae", ["Brow"], "Brow", signature=True,
              Antenna=dict(Length=0.30, Radius=0.010, Plates=1, PlateLength=0.04, ElevationDeg=45.0, SplayDeg=22.0, KeratinA=C(0.05,0.04,0.03), KeratinB=C(0.16,0.11,0.05))),
        trait("Stripes", "None", [], signature=True),
        trait("NoEars", "None", ["Ears"]),
        trait("Mandibles", "Mandibles", ["Mouth"], "Mouth", overrides=dict(LipFullness=-1.0),
              Mandible=dict(Length=0.14, RootRadius=0.022, Curl=0.7, Spread=0.12, KeratinA=C(0.06,0.04,0.03), KeratinB=C(0.20,0.12,0.06))),
      ],
      Covering=covering(Kind="Chitin", BaseA=C(0.92,0.70,0.12), BaseB=C(0.95,0.80,0.25), Marking="Stripes", MarkingColor=C(0.08,0.06,0.04),
                        MarkingStrength=0.9, MarkingScale=9.0, Smoothness=0.7, DetailStrength=0.5, AnchorThetaDeg=0.0,
                        ReachDegAtMin=50.0, ReachDegAtMax=150.0, PaintsBrows=False)),

 dict(Key="Proboscidea", DisplayName="Proboscidea", IsHuman=False,
      AxisTargets=axes(CranialWidth=0.50, CranialHeight=0.30, CranialLength=0.30, ForeheadBulge=0.50, BrowRidge=0.20, OrbitalSize=-0.50,
                       OrbitalSpacing=0.60, MuzzleLength=0.20, MuzzleWidth=0.40, NoseProjection=-0.30, LipFullness=-0.80,
                       MouthWidth=-0.20, JawWidth=0.50, ChinProjection=-0.80, LowerFaceHeight=0.20, CheekboneWidth=0.50, NeckThickness=0.90),
      Traits=[
        trait("Trunk", "Trunk", ["Nose", "Mouth"], "NoseTip", signature=True, pitch=-10.0, overrides=dict(NoseProjection=-0.4, LipFullness=-0.8),
              Trunk=dict(Length=0.72, RootRadius=0.075, TipRadius=0.032, Droop=0.8, Rings=24)),
        trait("FanEars", "Pinna", ["Ears"], "EarSide", signature=True, pitch=0.0, roll=0.0,
              Pinna=dict(Height=0.38, Width=0.30, Pointed=0.0, CupDepth=0.15, RimThickness=0.08, Tragus=0.0, ForwardTiltDeg=55.0, OutwardRollDeg=6.0, Thickness=0.014, RootOffset=0.0)),
        trait("Tusks", "Fangs", [], "MouthCorner", signature=True, Fang=dict(Length=0.20, Radius=0.024, Keratin=C(0.92,0.88,0.78))),
        trait("SmallEyes", "VertebrateEye", ["Eyes"], "Eye",
              Eye=dict(Radius=0.046, IrisFraction=0.75, LidOpen=0.55, CanthalTiltDeg=-4.0, LidThickness=0.014, Pupil="Round", PupilSize=0.5,
                       IrisA=C(0.35,0.22,0.10), IrisB=C(0.20,0.16,0.10), Sclera=C(0.78,0.74,0.68))),
      ],
      Covering=covering(Kind="Hide", BaseA=C(0.46,0.44,0.43), BaseB=C(0.34,0.32,0.31), Marking="Ridges", MarkingColor=C(0.24,0.22,0.22),
                        MarkingStrength=0.6, MarkingScale=6.0, Smoothness=0.30, DetailStrength=0.9, AnchorThetaDeg=0.0,
                        ReachDegAtMin=80.0, ReachDegAtMax=170.0, PaintsBrows=False)),

 dict(Key="Primates", DisplayName="Primates", IsHuman=False,
      AxisTargets=axes(MuzzleLength=0.55, MuzzleWidth=0.30, CranialWidth=0.10, CranialHeight=-0.20, OrbitalSize=0.30, OrbitalSpacing=-0.20,
                       NoseProjection=-0.70, NoseWidth=0.50, ChinProjection=-0.70, LowerFaceHeight=0.10, LipFullness=0.30,
                       BrowRidge=0.90, CheekboneWidth=0.40, ForeheadBulge=-0.40, JawWidth=0.30, NeckThickness=0.40),
      Traits=[
        trait("Mane", "HairCap", ["Crown"], "Crown", signature=True,
              Hair=dict(Thickness=0.06, FrontHairlineDeg=48, SideHairlineDeg=95, BackHairlineDeg=125, Clumping=0.7, StrandCount=900, StrandLength=0.34, StrandWidth=0.05, PartDeg=0, Fringe=0.2, BrowCards=40)),
        trait("MuzzleRidges", "None", ["Nose"], signature=True, overrides=dict(MuzzleLength=0.6, NoseProjection=-0.7, NoseWidth=0.5)),
        trait("DeepEyes", "VertebrateEye", ["Eyes"], "Eye",
              Eye=dict(Radius=0.060, IrisFraction=0.70, LidOpen=0.55, CanthalTiltDeg=-2.0, LidThickness=0.012, Pupil="Round", PupilSize=0.5,
                       IrisA=C(0.40,0.22,0.08), IrisB=C(0.20,0.14,0.08), Sclera=C(0.82,0.78,0.70))),
        trait("Fangs", "Fangs", [], "MouthCorner", Fang=dict(Length=0.06, Radius=0.012, Keratin=C(0.92,0.88,0.80))),
        trait("Blaze", "None", [], signature=True),
      ],
      Covering=covering(Kind="Skin", BaseA=C(0.62,0.56,0.50), BaseB=C(0.36,0.52,0.72), Marking="Blaze", MarkingColor=C(0.82,0.16,0.16),
                        MarkingStrength=0.9, MarkingScale=8.0, Smoothness=0.35, DetailStrength=0.7, AnchorThetaDeg=60.0,
                        ReachDegAtMin=60.0, ReachDegAtMax=120.0, PaintsBrows=False)),
]


# ---------------------------------------------------------------- AVATAR PRESETS
# One genome per shipped profile-icon illustration (Assets/_Graphics/Profile/ProfileIcon*.png, in
# SO_DefaultProfileIcons order): the nearest point in the generator's space to each picture.
# Weights obey the [0.20, 0.60] region, so every avatar is at least a fifth human by construction.
PRESET_DIR = RUNTIME + "/Resources/Characters/AvatarPresets"
def preset(name, icon, a, b, wa, wb, coat, gear="None", hair=0.6, skin=0.4, warmth=0.5, hair_shade=0.4, hair_warmth=0.4,
           iris=0.5, marking=0.5, domain="Jade", seed=1000, flesh=0.5, age=0.35):
    assert a in [c["Key"] for c in CLADES] and b in [c["Key"] for c in CLADES], (a, b)
    wh = round(1.0 - wa - wb, 4); assert 0.2 - 1e-6 <= wh <= 0.6 + 1e-6 and 0.2 <= wa <= 0.6 and 0.2 <= wb <= 0.6, (name, wa, wb, wh)
    return dict(Name=name, Icon=icon, CladeA=a, CladeB=b, Wa=wa, Wb=wb, Wh=wh, Coat=coat, Gear=gear, Hair=hair, Skin=skin, Warmth=warmth,
                HairShade=hair_shade, HairWarmth=hair_warmth, Iris=iris, Marking=marking, Domain=domain, Seed=seed, Flesh=flesh, Age=age)
DOMAIN_ID = {"Jade": 1, "Ruby": 2, "Blue": 3, "Gold": 4}
GEAR_ID = {"None": 0, "GogglesUp": 1, "GogglesOn": 2}
PRESETS = [
    preset("Crested Red",     "ProfileIcon01",  "Corvidae",    "Squamata",    0.55, 0.25, C(0.95,0.28,0.22), hair=0.0, domain="Ruby",  seed=1001, iris=0.2),
    preset("Goggle Hare",     "ProfileIcon02",  "Leporidae",   "Canidae",     0.60, 0.20, C(0.80,0.66,0.48), gear="GogglesUp", hair=0.0, domain="Jade", seed=1002, iris=0.3),
    preset("Mandrill",        "ProfileIcon03",  "Primates",    "Felidae",     0.60, 0.20, C(0.72,0.70,0.58), hair=1.0, hair_shade=0.18, hair_warmth=0.2, domain="Gold", seed=1003, iris=0.6),
    preset("Tiger",           "ProfileIcon04",  "Felidae",     "Canidae",     0.60, 0.20, C(1.00,0.62,0.22), hair=0.0, marking=0.9, domain="Gold", seed=1004, iris=0.1),
    preset("Chick",           "ProfileIcon05",  "Corvidae",    "Hymenoptera", 0.60, 0.20, C(1.00,0.82,0.18), hair=0.0, domain="Gold", seed=1005, iris=0.9),
    preset("Orchid Mantis",   "ProfileIcon06",  "Coleoptera",  "Chiroptera",  0.60, 0.20, C(0.95,0.48,0.78), hair=0.0, domain="Ruby", seed=1006, iris=0.7),
    preset("Hound",           "ProfileIcon_07", "Canidae",     "Leporidae",   0.60, 0.20, C(0.92,0.88,0.80), hair=0.0, domain="Ruby", seed=1007, iris=0.9),
    preset("Tabby",           "ProfileIcon_08", "Felidae",     "Leporidae",   0.60, 0.20, C(0.90,0.66,0.34), hair=0.0, marking=0.95, domain="Gold", seed=1008, iris=0.35),
    preset("Goggle Lizard",   "ProfileIcon_09", "Squamata",    "Canidae",     0.60, 0.20, C(0.35,0.80,0.45), gear="GogglesUp", hair=0.0, domain="Jade", seed=1009, iris=0.5),
    preset("Pink Rabbit",     "ProfileIcon_10", "Leporidae",   "Chiroptera",  0.60, 0.20, C(1.00,0.55,0.72), hair=0.0, domain="Ruby", seed=1010, iris=0.6),
    preset("Elephant",        "ProfileIcon_11", "Proboscidea", "Testudines",  0.60, 0.20, C(0.72,0.70,0.70), hair=0.0, domain="Ruby", seed=1011, iris=0.4),
    preset("Aviator",         "ProfileIcon_12", "Felidae",     "Leporidae",   0.60, 0.20, C(0.96,0.94,0.90), gear="GogglesOn", hair=0.9, hair_shade=0.9, hair_warmth=0.1, domain="Jade", seed=1012, iris=0.6),
    preset("Shepherd",        "ProfileIcon_13", "Canidae",     "Felidae",     0.60, 0.20, C(0.94,0.92,0.86), gear="GogglesUp", hair=0.0, domain="Gold", seed=1013, iris=0.2),
    preset("Cosmonaut Hare",  "ProfileIcon_14", "Leporidae",   "Cetacea",     0.60, 0.20, C(0.96,0.94,0.92), gear="GogglesOn", hair=0.0, domain="Jade", seed=1014, iris=0.85),
    preset("Helmet",          "ProfileIcon_15", "Cetacea",     "Coleoptera",  0.20, 0.20, C(0.70,0.72,0.74), gear="GogglesOn", hair=0.3, skin=0.35, domain="Jade", seed=1015, iris=0.5),
    preset("Goggle Jack",     "ProfileIcon_16", "Leporidae",   "Canidae",     0.60, 0.20, C(0.82,0.62,0.40), gear="GogglesOn", hair=0.0, domain="Jade", seed=1016, iris=0.4),
    preset("Field Mouse",     "ProfileIcon_17", "Leporidae",   "Chiroptera",  0.60, 0.20, C(0.94,0.92,0.90), hair=0.0, domain="Gold", seed=1017, iris=0.95),
    preset("Bee",             "ProfileIcon_18", "Hymenoptera", "Coleoptera",  0.60, 0.20, C(1.00,0.80,0.20), hair=0.0, marking=0.9, domain="Gold", seed=1018, iris=0.5),
]

def preset_json(pr):
    def f(v): return repr(float(v))
    c = pr["Coat"]
    fields = [
        f'"FormatVersion":1', f'"Seed":{pr["Seed"]}', f'"CladeA":"{pr["CladeA"]}"', f'"CladeB":"{pr["CladeB"]}"',
        f'"Weights":{{"Human":{f(pr["Wh"])},"CladeA":{f(pr["Wa"])},"CladeB":{f(pr["Wb"])}}}',
        '"BaseShape":{"_values":[' + ",".join(["0.0"] * len(HeadAxis)) + ']}',
        f'"Age":{f(pr["Age"])}', f'"Fleshiness":{f(pr["Flesh"])}', f'"HairVolume":{f(pr["Hair"])}',
        f'"SkinTone":{f(pr["Skin"])}', f'"SkinWarmth":{f(pr["Warmth"])}', f'"HairShade":{f(pr["HairShade"])}', f'"HairWarmth":{f(pr["HairWarmth"])}',
        f'"IrisKey":{f(pr["Iris"])}', f'"MarkingKey":{f(pr["Marking"])}', f'"Domain":{DOMAIN_ID[pr["Domain"]]}', f'"Gear":{GEAR_ID[pr["Gear"]]}',
        f'"CoatTint":{{"r":{f(c[1])},"g":{f(c[2])},"b":{f(c[3])},"a":1.0}}',
        '"ExpressedTraits":[]',
    ]
    return "{" + ",".join(fields) + "}\n"

# ---------------------------------------------------------------- YAML emit
def fmt(v):
    if isinstance(v, bool): return "1" if v else "0"
    if isinstance(v, int): return str(v)
    if isinstance(v, float):
        s = repr(v)
        return s
    if isinstance(v, str): return v
    raise TypeError(v)

def color_yaml(c):
    _, r, g, b, a = c
    return "{r: %s, g: %s, b: %s, a: %s}" % (fmt(float(r)), fmt(float(g)), fmt(float(b)), fmt(float(a)))

def enum_val(table, name):
    assert name in table, (name, table); return table[name]

def emit_group(cls, d, ind):
    lines = []
    for k in FIELDS[cls]:
        if k not in d: continue
        v = d[k]
        if isinstance(v, tuple) and v[0] == "color": lines.append(f"{ind}{k}: {color_yaml(v)}")
        elif k == "Pupil": lines.append(f"{ind}{k}: {enum_val(PupilKind, v)}")
        else: lines.append(f"{ind}{k}: {fmt(v)}")
    return lines

def emit_trait(t, ind):
    L = [f"{ind}- Id: {t['Id']}",
         f"{ind}  Signature: {fmt(t['Signature'])}",
         f"{ind}  MinWeight: {fmt(float(t['MinWeight']))}",
         f"{ind}  Feature: {enum_val(FeatureKind, t['Feature'])}"]
    if t["Slots"]:
        L.append(f"{ind}  Slots:")
        for s in t["Slots"]: L.append(f"{ind}  - {enum_val(FeatureSlot, s)}")
    else: L.append(f"{ind}  Slots: []")
    L.append(f"{ind}  Site: {t['Site']}")
    L.append(f"{ind}  SitePitchDeg: {fmt(float(t['SitePitchDeg']))}")
    L.append(f"{ind}  SiteYawDeg: {fmt(float(t['SiteYawDeg']))}")
    L.append(f"{ind}  SiteRollDeg: {fmt(float(t['SiteRollDeg']))}")
    if t["AxisOverrides"]:
        L.append(f"{ind}  AxisOverrides:")
        for a, v in t["AxisOverrides"]:
            L.append(f"{ind}  - Axis: {enum_val(HeadAxis, a)}")
            L.append(f"{ind}    Value: {fmt(float(v))}")
    else: L.append(f"{ind}  AxisOverrides: []")
    L.append(f"{ind}  Params:")
    for g in FIELDS["FeatureParams"]:
        if g in t["Params"]:
            L.append(f"{ind}    {g}:")
            L += emit_group(PARAM_GROUP_CLASS[g], t["Params"][g], ind + "      ")
    return L

def clade_yaml(c, script_guid):
    check_keys("CladeSO", {k: 1 for k in c})
    L = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
         "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
         "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
         f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}", f"  m_Name: Clade {c['Key']}",
         "  m_EditorClassIdentifier: ",
         f"  Key: {c['Key']}", f"  DisplayName: {c['DisplayName']}", f"  IsHuman: {fmt(c['IsHuman'])}"]
    if c["AxisTargets"]:
        L.append("  AxisTargets:")
        for a, v in c["AxisTargets"]:
            L.append(f"  - Axis: {enum_val(HeadAxis, a)}"); L.append(f"    Value: {fmt(float(v))}")
    else: L.append("  AxisTargets: []")
    L.append("  Traits:")
    for t in c["Traits"]: L += emit_trait(t, "  ")
    L.append("  Covering:")
    cov = c["Covering"]
    for k in FIELDS["CoveringRecipe"]:
        v = cov[k]
        if isinstance(v, tuple): L.append(f"    {k}: {color_yaml(v)}")
        elif k == "Kind": L.append(f"    {k}: {enum_val(CoveringKind, v)}")
        elif k == "Marking": L.append(f"    {k}: {enum_val(MarkingKind, v)}")
        else: L.append(f"    {k}: {fmt(v)}")
    return "\n".join(L) + "\n"

def config_yaml(script_guid, human_guid):
    return "\n".join(["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
         "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
         "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
         f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}", "  m_Name: CharacterGenerationConfig",
         "  m_EditorClassIdentifier: ",
         f"  HumanSource: {{fileID: 11400000, guid: {human_guid}, type: 2}}",
         "  Head: 0"]) + "\n"   # every other field takes its C# initializer (Unity YAML is name-keyed)

def meta_for(rel):
    g = guid_for(rel)
    if rel.endswith(".cs"):
        return f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    if rel.endswith(".asset"):
        return f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    if rel.endswith(".md") or rel.endswith(".json"):
        return f"fileFormatVersion: 2\nguid: {g}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    if os.path.isdir(os.path.join(ROOT, rel)):
        return f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    return f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

def plan():
    files = {}
    clade_guid = guid_for(RUNTIME + "/CladeSO.cs")
    cfg_guid = guid_for(RUNTIME + "/CharacterGenerationConfigSO.cs")
    human_asset = CLADE_DIR + "/Clade Human.asset"
    for c in CLADES:
        files[CLADE_DIR + f"/Clade {c['Key']}.asset"] = clade_yaml(c, clade_guid)
    files[CONFIG_PATH] = config_yaml(cfg_guid, guid_for(human_asset))
    for i, pr in enumerate(PRESETS):
        files[PRESET_DIR + f"/Avatar_{i + 1:02d}_{pr['Icon']}.json"] = preset_json(pr)
    return files

def all_meta_targets():
    targets = []
    for base in (RUNTIME, EDITOR):
        targets.append(base)
        for dp, dns, fns in os.walk(os.path.join(ROOT, base)):
            dns[:] = [d for d in dns if not d.endswith("~")]   # Unity ignores `~` folders: no .meta
            for d in dns: targets.append(os.path.relpath(os.path.join(dp, d), ROOT))
            for f in fns:
                if f.endswith(".meta"): continue
                targets.append(os.path.relpath(os.path.join(dp, f), ROOT))
    return sorted(set(targets))

def fixture_cs(path):
    """A C# file constructing the same clades in code, for the offline harness ONLY (never shipped)."""
    def col(c): _, r, g, b, a = c; return f"new Color({r}f, {g}f, {b}f, {a}f)"
    def group(cls, d):
        parts = []
        for k, v in d.items():
            if isinstance(v, tuple): parts.append(f"{k} = {col(v)}")
            elif k == "Pupil": parts.append(f"{k} = PupilKind.{v}")
            elif k == "Kind": parts.append(f"{k} = CoveringKind.{v}")
            elif k == "Marking": parts.append(f"{k} = MarkingKind.{v}")
            elif isinstance(v, bool): parts.append(f"{k} = {'true' if v else 'false'}")
            elif isinstance(v, int): parts.append(f"{k} = {v}")
            else: parts.append(f"{k} = {float(v)}f")
        return f"new {cls} {{ {', '.join(parts)} }}"
    L = ["using System.Collections.Generic; using UnityEngine; namespace CosmicShore.Gameplay { public static class CladeFixtures {",
         "public static List<CladeSO> All() { var list = new List<CladeSO>();"]
    for c in CLADES:
        L.append(f"{{ var c = new CladeSO {{ Key = \"{c['Key']}\", DisplayName = \"{c['DisplayName']}\", IsHuman = {'true' if c['IsHuman'] else 'false'} }};")
        L.append("c.AxisTargets = new AxisTarget[] { " + ", ".join(f"new AxisTarget {{ Axis = HeadAxis.{a}, Value = {float(v)}f }}" for a, v in c["AxisTargets"]) + " };")
        L.append("c.Traits = new TraitDefinition[] {")
        for t in c["Traits"]:
            params = ", ".join(f"{g} = {group(PARAM_GROUP_CLASS[g], d)}" for g, d in t["Params"].items())
            L.append(f"new TraitDefinition {{ Id = \"{t['Id']}\", Signature = {'true' if t['Signature'] else 'false'}, MinWeight = {float(t['MinWeight'])}f, Feature = FeatureKind.{t['Feature']}, "
                     f"Slots = new List<FeatureSlot> {{ {', '.join('FeatureSlot.'+s for s in t['Slots'])} }}, Site = \"{t['Site']}\", SitePitchDeg = {float(t['SitePitchDeg'])}f, SiteYawDeg = {float(t['SiteYawDeg'])}f, SiteRollDeg = {float(t['SiteRollDeg'])}f, "
                     f"AxisOverrides = new AxisOverride[] {{ {', '.join(f'new AxisOverride {{ Axis = HeadAxis.{a}, Value = {float(v)}f }}' for a, v in t['AxisOverrides'])} }}, "
                     f"Params = new FeatureParams {{ {params} }} }},")
        L.append("};")
        cov = c["Covering"]
        L.append("c.Covering = " + group("CoveringRecipe", cov) + ";")
        L.append("list.Add(c); }")
    L.append("return list; } } }")
    with open(path, "w") as f: f.write("\n".join(L) + "\n")

def main():
    check = "--check" in sys.argv
    if "--fixture" in sys.argv:
        fixture_cs(sys.argv[sys.argv.index("--fixture") + 1]); print("fixture written")
    files = plan()
    for rel in all_meta_targets():
        files[rel + ".meta"] = meta_for(rel)
    drift, wrote = [], 0
    for rel, content in files.items():
        p = os.path.join(ROOT, rel)
        cur = open(p, encoding="utf-8").read() if os.path.exists(p) else None
        if cur == content: continue
        if check: drift.append(rel); continue
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as f: f.write(content)
        wrote += 1
    # guid uniqueness across the repo's .meta files
    mine = {guid_for(rel): rel for rel in all_meta_targets()}
    for dp, dns, fns in os.walk(os.path.join(ROOT, "Assets")):
        for fn in fns:
            if not fn.endswith(".meta"): continue
            rel = os.path.relpath(os.path.join(dp, fn), ROOT)[:-5]
            with open(os.path.join(dp, fn), encoding="utf-8", errors="ignore") as f:
                m = re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M)
            if m and m.group(1) in mine and mine[m.group(1)] != rel:
                sys.exit(f"guid collision: {rel} vs {mine[m.group(1)]}")
    if check:
        if drift: sys.exit("DRIFT: " + ", ".join(drift))
        print(f"check ok: {len(files)} files match")
    else:
        print(f"wrote {wrote} of {len(files)} files")

if __name__ == "__main__":
    main()

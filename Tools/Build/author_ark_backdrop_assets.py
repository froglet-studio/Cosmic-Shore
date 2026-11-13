#!/usr/bin/env python3
"""
Author the Ark screen's backdrops (Docs/MENU_REDESIGN_B.md section 190): the Frogma renders as video behind each Ark tab
(Shombith, 2026-11-12: "do exactly how you showed me on Frogma, no need to use the actual model, it can be a video").
ElementalConverterView plays them through ArkBackdrop from ArkBackdropConfigSO.

Writes (and with --check, compares; fails when a clip or a referenced script is missing):
    Assets/Resources/ArkBackdropConfig.asset (+ .meta)       the clips by room, run, colour set and domain
    Assets/_Graphics/Video/Ark/*.mp4.meta                     VideoClip importers, transcoding off (the clips play as encoded)
    Assets/_Scripts/ScriptableObjects/Toys/ArkBackdropConfigSO.cs.meta, Assets/_Scripts/UI/Elements/Ark/ArkBackdrop.cs.meta,
    Assets/_Scripts/UI/Elements/Ark/SquadVesselLayer.cs.meta (the squad's real vessels over the hangar clips, section 202)

The clips themselves are encoded by Docs/MenuRedesignB/rounds/r67_ark_backdrop_videos.py from the Frogma renders.

    python3 Tools/Build/author_ark_backdrop_assets.py           # write
    python3 Tools/Build/author_ark_backdrop_assets.py --check   # exit 1 on drift or a missing clip
"""
import argparse
import os
import re
import sys
import uuid

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
NS = uuid.UUID("2b7e5a61-4d0c-4f3a-9c1e-a8b4c2d0e6f1")   # fixed: the guids must never change

SO_CS = "Assets/_Scripts/ScriptableObjects/Toys/ArkBackdropConfigSO.cs"
PLAYER_CS = "Assets/_Scripts/UI/Elements/Ark/ArkBackdrop.cs"
LAYER_CS = "Assets/_Scripts/UI/Elements/Ark/SquadVesselLayer.cs"
VESSEL_PREFABS = "05f8f1205bd90e14b8e59cf78da1a6f2"   # Assets/_SO_Assets/Vessel Prefab Container.asset
OFFLINE_LIVERY = "bda6299e6e2046339d7a18b1a1ef8443"   # Assets/_Graphics/Materials/ClassLiveryVesselMaterial.mat (the Hangar's)
# The squad's real vessels over the hangar clips (Docs/MENU_REDESIGN_B.md section 202): the clips' camera and pads from
# r67_ark_tabs_render.py (CAM["squads"], SPOTS, Z0) with Blender's z up turned into Unity's y up, (x, z, y). Seat order:
# the player's pad (the leader, a step back in the middle), then seats two (left) and three (right).
HANGAR = {
    "enabled": 1,
    "cameraPosition": (6.0, -15.6, -85.0),
    "cameraAim": (6.0, -20.6, -62.0),
    "verticalFov": 45.75,                  # 2 atan(18 / 24 * 9 / 16): the 24 mm lens on the 36 mm sensor at 16:9
    "cameraSway": (0.18, 0.08),
    "aimSway": 0.3,
    "loopSeconds": 3.2,
    "pads": [(0.0, -23.0, -66.0), (-9.0, -23.0, -60.5), (9.6, -23.0, -60.5)],
    "hullLength": 7.5,                     # Rev 110's check: 0.6 of the clip's Sparrow (12.5 m)
    "clearance": 1.6,
    "hover": 0.12,
    "rockDegrees": 1.7,
    "phaseStep": 2.1,
    "padLightIntensity": 4.0,
    "padLightRange": 7.0,
    "lampOffset": (-2.0, 6.0, -4.0),
    "lampIntensity": 2.0,
    "lampRange": 16.0,
    "layerSize": (1920, 1080),
    "renderRate": 30,
}
ASSET = "Assets/Resources/ArkBackdropConfig.asset"
VIDEO_DIR = "Assets/_Graphics/Video/Ark"
ROOM = {"Hangar": 1, "Pier": 2, "Artifacts": 3}
RUN = {"loop": 0, "convert": 1, "done": 2}
DOMAIN = {"Jade": 1, "Ruby": 2, "Gold": 4}
SETS = ("Nocturne", "Photic", "DartFrog")


def guid(path):
    return uuid.uuid5(NS, path).hex


def clips():
    """(file, room, run, set, domain or None)"""
    out = []
    for s in SETS:
        for d in ("Jade", "Ruby", "Gold"):
            out.append((f"squads_{s}_{d}_loop.mp4", "Hangar", "loop", s, d))
    for run in ("loop", "convert", "done"):
        out.append((f"converter_Nocturne_{run}.mp4", "Pier", run, "Nocturne", None))
    out.append(("artifacts_Nocturne_loop.mp4", "Artifacts", "loop", "Nocturne", None))
    return out


def num(v):
    return ("%.4f" % v).rstrip("0").rstrip(".") if isinstance(v, float) else str(v)


def yaml_value(key, v):
    if isinstance(v, list):
        return "\n" + "\n".join("    - " + yaml_value(key, x) for x in v)
    if isinstance(v, tuple):
        axes = ("r", "g", "b", "a") if key.endswith("Color") else ("x", "y", "z", "w")
        return "{" + ", ".join(f"{a}: {num(x)}" for a, x in zip(axes, v)) + "}"
    return num(v)


def hangar_text():
    return "\n".join(f"    {k}:{'' if isinstance(v, list) else ' '}{yaml_value(k, v)}" for k, v in HANGAR.items())


def asset_text():
    rows = []
    for f, room, run, s, d in clips():
        rows.append(f"""  - room: {ROOM[room]}
    run: {RUN[run]}
    colourSet: {s}
    domain: {DOMAIN.get(d, 1)}
    anyDomain: {0 if d else 1}
    clip: {{fileID: 32900000, guid: {guid(VIDEO_DIR + '/' + f)}, type: 3}}""")
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid(SO_CS)}, type: 3}}
  m_Name: ArkBackdropConfig
  m_EditorClassIdentifier:
  clips:
{chr(10).join(rows)}
  textureSize: {{x: 1920, y: 1080}}
  hangar:
{hangar_text()}
  vesselPrefabs: {{fileID: 11400000, guid: {VESSEL_PREFABS}, type: 2}}
  offlineLivery: {{fileID: 2100000, guid: {OFFLINE_LIVERY}, type: 2}}
"""


def video_meta(path):
    return f"""fileFormatVersion: 2
guid: {guid(path)}
VideoClipImporter:
  externalObjects: {{}}
  serializedVersion: 3
  frameRange: 0
  startFrame: -1
  endFrame: -1
  colorSpace: 0
  deinterlace: 0
  encodeAlpha: 0
  flipVertical: 0
  flipHorizontal: 0
  importAudio: 0
  targetSettings:
    :
      enableTranscoding: 0
      codec: 0
      resizeFormat: 0
      aspectRatio: 0
      customWidth: 1920
      customHeight: 1080
      bitrateMode: 2
      spatialQuality: 2
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def script_meta(path):
    return f"""fileFormatVersion: 2
guid: {guid(path)}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def asset_meta():
    return f"""fileFormatVersion: 2
guid: {guid(ASSET)}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def outputs():
    out = {ASSET: asset_text(), ASSET + ".meta": asset_meta(),
           SO_CS + ".meta": script_meta(SO_CS), PLAYER_CS + ".meta": script_meta(PLAYER_CS),
           LAYER_CS + ".meta": script_meta(LAYER_CS)}
    for f, *_ in clips():
        p = VIDEO_DIR + "/" + f
        out[p + ".meta"] = video_meta(p)
    return out


def validate():
    errors = []
    for f, *_ in clips():
        if not os.path.exists(os.path.join(REPO, VIDEO_DIR, f)):
            errors.append(f"clip {VIDEO_DIR}/{f} is missing (encode it: Docs/MenuRedesignB/rounds/r67_ark_backdrop_videos.py)")
    stray = sorted(set(os.listdir(os.path.join(REPO, VIDEO_DIR))) - {f for f, *_ in clips()} - {f + ".meta" for f, *_ in clips()}) \
        if os.path.isdir(os.path.join(REPO, VIDEO_DIR)) else []
    for f in stray:
        errors.append(f"{VIDEO_DIR}/{f} is not a backdrop this config names (ship it in the table or delete it)")
    so = open(os.path.join(REPO, SO_CS)).read() if os.path.exists(os.path.join(REPO, SO_CS)) else ""
    for field in ("clips", "textureSize", "room", "run", "colourSet", "domain", "anyDomain", "clip", "hangar",
                  "vesselPrefabs", "offlineLivery", *HANGAR):
        if not re.search(r"\b" + field + r"\s*[;=]", so):
            errors.append(f"the asset writes '{field}', which ArkBackdropConfigSO does not declare")
    for cs in (PLAYER_CS, LAYER_CS):
        if not os.path.exists(os.path.join(REPO, cs)):
            errors.append(f"{cs} is missing")
    for g, what in ((VESSEL_PREFABS, "the Vessel Prefab Container"), (OFFLINE_LIVERY, "the class livery material")):
        metas = ("Assets/_SO_Assets/Vessel Prefab Container.asset.meta", "Assets/_Graphics/Materials/ClassLiveryVesselMaterial.mat.meta")
        if not any(os.path.exists(os.path.join(REPO, m)) and f"guid: {g}" in open(os.path.join(REPO, m)).read() for m in metas):
            errors.append(f"{what} ({g}) is not where the config points")
    return errors


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    errors = validate()
    drift = []
    for rel, text in outputs().items():
        p = os.path.join(REPO, rel)
        old = open(p).read() if os.path.exists(p) else None
        if old != text:
            drift.append(rel)
            if not a.check:
                os.makedirs(os.path.dirname(p), exist_ok=True)
                with open(p, "w", newline="\n") as f:
                    f.write(text)
    for e in errors:
        print("ERROR", e)
    if a.check:
        for d in drift:
            print("DRIFT", d)
        ok = not errors and not drift
        print("author_ark_backdrop_assets: " + ("OK" if ok else "FAILED"))
        return 0 if ok else 1
    print(f"author_ark_backdrop_assets: wrote {len(drift)} file(s)" + (f", {len(errors)} error(s)" if errors else ""))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())

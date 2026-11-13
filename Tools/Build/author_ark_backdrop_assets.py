#!/usr/bin/env python3
"""
Author the Ark screen's backdrops (Docs/MENU_REDESIGN_B.md section 190): the Frogma renders as video behind each Ark tab
(Shombith, 2026-11-12: "do exactly how you showed me on Frogma, no need to use the actual model, it can be a video").
ElementalConverterView plays them through ArkBackdrop from ArkBackdropConfigSO.

Writes (and with --check, compares; fails when a clip or a referenced script is missing):
    Assets/Resources/ArkBackdropConfig.asset (+ .meta)       the clips by room, run, colour set and domain
    Assets/_Graphics/Video/Ark/*.mp4.meta                     VideoClip importers, transcoding off (the clips play as encoded)
    Assets/_Scripts/ScriptableObjects/Toys/ArkBackdropConfigSO.cs.meta, Assets/_Scripts/UI/Elements/Ark/ArkBackdrop.cs.meta

The clips themselves are encoded by Docs/MenuRedesignB/rounds/r67_ark_backdrop_videos.py from the Frogma renders, and
the PILOTS tab's bench clips by Docs/MenuRedesignB/rounds/r99_pilots_bench_videos.py (Rev 99 C, section 191).

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
ASSET = "Assets/Resources/ArkBackdropConfig.asset"
VIDEO_DIR = "Assets/_Graphics/Video/Ark"
PASS_SHADER = "Assets/_Graphics/Materials/Shaders/UI/ArkPass.shader"   # the HyperSea pass (MENU_REDESIGN_B.md section 197)
PASS_SECONDS = 0.5        # a pass from one room to another (Shombith, 2026-11-13: "the pass can be .50s")
PASS_STYLES = (0, 1, 2)   # membrane, ripple, caustic veil, picked at random per change until one is final
DISSOLVE_SECONDS = 0.3    # the pier's own runs: at rest, the conversion, the Omni made
ROOM = {"Hangar": 1, "Pier": 2, "Artifacts": 3, "Bench": 4}
RUN = {"loop": 0, "convert": 1, "done": 2, "pickup": 3, "held": 4, "putdown": 5}
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
    for run in ("loop", "pickup", "held", "putdown"):     # PILOTS: the crew PDA on the hangar's workbench (section 191)
        out.append((f"bench_Nocturne_{run}.mp4", "Bench", run, "Nocturne", None))
    return out


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
  passShader: {{fileID: 4800000, guid: {guid(PASS_SHADER)}, type: 3}}
  passSeconds: {PASS_SECONDS}
  passStyles:{"".join(chr(10) + "  - " + str(x) for x in PASS_STYLES) if PASS_STYLES else " []"}
  dissolveSeconds: {DISSOLVE_SECONDS}
"""


def shader_meta():
    return f"""fileFormatVersion: 2
guid: {guid(PASS_SHADER)}
ShaderImporter:
  externalObjects: {{}}
  defaultTextures: []
  nonModifiableTextures: []
  preprocessorOverride: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
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
           PASS_SHADER + ".meta": shader_meta()}
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
    for member, value in (("Bench", ROOM["Bench"]), ("PickUp", RUN["pickup"]), ("Held", RUN["held"]), ("PutDown", RUN["putdown"])):
        if not re.search(r"\b" + member + r"\s*=\s*" + str(value) + r"\s*,", so):
            errors.append(f"the asset writes {member} = {value}, which ArkBackdropConfigSO's enums do not declare")
    for field in ("clips", "textureSize", "room", "run", "colourSet", "domain", "anyDomain", "clip", "passShader",
                  "passSeconds", "dissolveSeconds", "passStyles"):
        if not re.search(r"\b" + field + r"\s*[;=]", so):
            errors.append(f"the asset writes '{field}', which ArkBackdropConfigSO does not declare")
    if not os.path.exists(os.path.join(REPO, PASS_SHADER)):
        errors.append(f"{PASS_SHADER} is missing (the HyperSea pass)")
    elif 'Shader "CosmicShore/UI/ArkPass"' not in open(os.path.join(REPO, PASS_SHADER)).read():
        errors.append(f"{PASS_SHADER} is not the CosmicShore/UI/ArkPass shader")
    if not os.path.exists(os.path.join(REPO, PLAYER_CS)):
        errors.append(f"{PLAYER_CS} is missing")
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

#!/usr/bin/env python3
"""
Author the Ark screen's 3D config: the menu INSIDE the Ark (Docs/MENU_REDESIGN_B.md sections 166-168, the build in
section 169). ElementalConverterView builds ArkInteriorStage from it on its off-screen stage: the hangar behind SQUADS,
the pier the omni combine plays on behind CONVERTER, the artifacts room behind ARTIFACTS, nothing behind DECOR.

Writes (and with --check, compares):
    Assets/Resources/ArkInteriorConfig.asset (+ .meta)            the config ElementalConverterView loads when unwired
    Assets/_Scripts/ScriptableObjects/Toys/ArkInteriorConfigSO.cs.meta
    Assets/_Scripts/UI/Elements/Ark/ArkInteriorStage.cs.meta

The numbers are the Blender mockups' (Docs/MenuRedesignB/rounds/r67_ark_tabs_render.py: the `pier`, `squads` and
`art_ab` scenes), turned from Blender's frame into Unity model space (x, y, z) = (-bx, bz, -by); a 24 mm lens on a 36 mm
sensor at 16:9 is 45.6 degrees of vertical field, 20 mm is 53.7.

    python3 Tools/Build/author_ark_interior_assets.py           # write
    python3 Tools/Build/author_ark_interior_assets.py --check   # exit 1 if any output drifted or a referenced asset is missing
"""
import argparse
import os
import re
import sys
import uuid

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
NS = uuid.UUID("6f1c2d0e-7a2b-4c55-9d1e-a5c0b1d2e3f4")   # fixed: the guids must never change


def guid(path):
    return uuid.uuid5(NS, path).hex


SO_CS = "Assets/_Scripts/ScriptableObjects/Toys/ArkInteriorConfigSO.cs"
STAGE_CS = "Assets/_Scripts/UI/Elements/Ark/ArkInteriorStage.cs"
ASSET = "Assets/Resources/ArkInteriorConfig.asset"
REFS = {
    "arkModel": ("Assets/_SO_Assets/Toys/ArkReverseModel.asset", "8caf2baf516a4c169ca47fe0558a3c2c", 11400000),
    "prismBoxMesh": ("Assets/_Models/Testing/Prism.asset", "8f4a329b97818584886f68265bb01988", 4300000),
}


def v3(x, y, z):
    return f"{{x: {x}, y: {y}, z: {z}}}"


def view(pos, at, fov, drift):
    return (f"\n    position: {v3(*pos)}\n    lookAt: {v3(*at)}\n    verticalFov: {fov}\n    driftDegrees: {drift}")


def ref(key):
    path, g, fid = REFS[key]
    return f"{{fileID: {fid}, guid: {g}, type: 2}}"


def asset_text():
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
  m_Name: ArkInteriorConfig
  m_EditorClassIdentifier:
  arkModel: {ref("arkModel")}
  prismBoxMesh: {ref("prismBoxMesh")}
  topDoorDegrees: -150
  bottomDoorDegrees: 150
  hangarView:{view((-6, -15.6, 85), (-6, -20.6, 62), 45.6, 0.4)}
  pierView:{view((0, -16.4, 63), (0, -19.6, 128), 45.6, 0)}
  artifactsView:{view((-3.6, -16.4, 590.5), (-0.6, -16.6, 606), 53.7, 0.3)}
  clipPlanes: {{x: 0.3, y: 1500}}
  pierStartZ: 88
  pierLength: 82
  pierWidth: 20
  pierTopY: -23
  deckZ: 128
  deckRadius: 7.2
  lampSpacing: 10
  combineHeight: 6.5
  combineRadius: 9
  artifactsRoomOrigin: {v3(0, -20, 600)}
  hallSize: {v3(8, 17, 10)}
  platformRadii: {{x: 6.6, y: 4.4}}
  alcovesPerWall: 4
  artifactScale: {{x: 2.3, y: 1.25}}
  cellLight: {{r: 0.55, g: 0.68, b: 1, a: 1}}
  cellLightIntensity: 1.2
  crystalLight: {{r: 0.6, g: 1, b: 0.25, a: 1}}
  crystalLightIntensity: 3
  artifactSpotIntensity: 6
  cellBackground: {{r: 0.12, g: 0.32, b: 0.62, a: 1}}
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
    return {
        ASSET: asset_text(),
        ASSET + ".meta": asset_meta(),
        SO_CS + ".meta": script_meta(SO_CS),
        STAGE_CS + ".meta": script_meta(STAGE_CS),
    }


def validate():
    errors = []
    for key, (path, g, _fid) in REFS.items():
        meta = os.path.join(REPO, path + ".meta")
        if not os.path.exists(meta):
            errors.append(f"{key}: {path} is missing")
        elif f"guid: {g}" not in open(meta).read():
            errors.append(f"{key}: {path}'s guid is no longer {g}")
    for cs in (SO_CS, STAGE_CS):
        if not os.path.exists(os.path.join(REPO, cs)):
            errors.append(f"{cs} is missing")
    so = open(os.path.join(REPO, SO_CS)).read() if os.path.exists(os.path.join(REPO, SO_CS)) else ""
    for line in asset_text().splitlines():
        field = line.strip().split(":")[0]
        declared = re.search(r"\bSerializeField\b[^;]*?\b" + re.escape(field) + r"\s*[;=]", so)
        if line.startswith("  ") and not line.startswith("   ") and not field.startswith("m_") and field and not declared:
            errors.append(f"the asset writes '{field}', which ArkInteriorConfigSO does not declare")
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
        print("author_ark_interior_assets: " + ("OK" if ok else "FAILED"))
        return 0 if ok else 1
    print(f"author_ark_interior_assets: wrote {len(drift)} file(s)" + (f", {len(errors)} error(s)" if errors else ""))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())

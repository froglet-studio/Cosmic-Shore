#!/usr/bin/env python3
"""
Author the pilot sight's assets (Docs/Pilots/CALLSIGNS.md §6.3). This script OWNS them:

  Assets/Resources/Pilots/CallsignSet.asset      CallsignSetSO: the seventeen callsigns + the sight's look
  Assets/Resources/Pilots/PilotSightMark.mat     the instanced mark material (PilotSightMark.shader)

and their .meta files. The callsign words and lines are CALLSIGNS.md §2's table; a change is made
here and re-run, never in the inspector. Glyphs and plate glyphs are left empty for /ability-icon,
and every open sting ships empty (the FMOD rule).

Assets/Resources/Pilots.meta is written with the guid Shombith's pilot branch
(claude/pensive-dijkstra-qxoxo0) gives the same folder, so the two branches merge without a
folder-guid conflict.

Usage:
    python3 Tools/Build/author_callsign_assets.py             # write
    python3 Tools/Build/author_callsign_assets.py --check     # diff disk; exit 1 on drift
    python3 Tools/Build/author_callsign_assets.py --self-test # negative controls
"""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SET_SCRIPT_META = 'Assets/_Scripts/ScriptableObjects/CallsignSetSO.cs.meta'
SHADER_META = 'Assets/_Graphics/Materials/Graphs/PilotSightMark.shader.meta'
ENUM = 'Assets/_Scripts/Data/Enums/PilotVision.cs'

FOLDER_META = 'Assets/Resources/Pilots.meta'
FOLDER_GUID = 'f6ed14cf729e60c697e78031f9d3b709'   # = Shombith's branch, deliberately
SET = 'Assets/Resources/Pilots/CallsignSet.asset'
SET_GUID = 'f138de33e985425c95451e6c4edf5270'
MAT = 'Assets/Resources/Pilots/PilotSightMark.mat'
MAT_GUID = '0bb6068a060d4322bb5030b312a06e19'

# CALLSIGNS.md §2, in the # column's order: (value, word, what holding LB lights).
CALLSIGNS = [
    (1, 'ENSIGN', "Your domain's mass: what is yours to defend, and the gaps in it."),
    (2, 'MANGER', 'The mass cluster the fauna are heading for.'),
    (3, 'ANVIL', 'Every Mass crystal.'),
    (4, 'VOLT', 'Every Charge crystal.'),
    (5, 'ZODIAC', 'Every Space crystal.'),
    (6, 'SUNDIAL', 'Every Time crystal.'),
    (7, 'OPAL', 'Every Omni crystal.'),
    (8, 'RAMPART', 'Every shielded prism.'),
    (9, 'CITADEL', 'Every super-shielded prism: what a payload is for.'),
    (10, 'HORNET', 'Every danger prism: the mass that burns your petals.'),
    (11, 'IVY', 'Every plant.'),
    (12, 'TALON', 'Every predator.'),
    (13, 'DOE', 'Every herbivore.'),
    (14, 'WINGMAN', 'Every vessel on your side, through mass.'),
    (15, 'BANDIT', 'Every opposing vessel, through mass.'),
    (16, 'NEEDLE', 'Every ring that activates something: gates, Scarab switches, goals.'),
    (17, 'GALLEON', 'The Arks, and the payload aboard.'),
]

LOOK = {
    'fadeInSeconds': 0.25,
    'fadeOutSeconds': 0.35,
    'suppressSaturation': -70,
    'suppressExposure': -1,
    'volumePriority': 50,
    'crystalMarkRadius': 6,
    'vesselMarkRadius': 12,
    'markIntensity': 1.2,
    'markMinScreenRadius': 0.02,
}
UNASSIGNED_SIGHT = 0   # PilotVision.None: LB does nothing until a pilot carries a sight


def guid_of(meta):
    for line in open(os.path.join(ROOT, meta), encoding='utf-8'):
        if line.startswith('guid:'):
            return line.split(':', 1)[1].strip()
    raise SystemExit(f'no guid in {meta}')


def num(v):
    return str(int(v)) if float(v).is_integer() else repr(float(v))


def yaml_text(s):
    if s == '':
        return ' '
    if re.search(r"[:#'\"{}\[\],&*!|>%@`]", s) or s != s.strip():
        return " '" + s.replace("'", "''") + "'"
    return ' ' + s


def empty_event(indent):
    p = ' ' * indent
    return (f'{p}openSound:\n{p}  Guid:\n{p}    Data1: 0\n{p}    Data2: 0\n{p}    Data3: 0\n{p}    Data4: 0\n'
            f'{p}  Path: \n')


def set_asset(callsigns=CALLSIGNS):
    out = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
           '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n'
           '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n'
           '  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
           f'  m_Script: {{fileID: 11500000, guid: {guid_of(SET_SCRIPT_META)}, type: 3}}\n'
           '  m_Name: CallsignSet\n  m_EditorClassIdentifier: \n  callsigns:\n')
    for value, word, meaning in callsigns:
        out += (f'  - sight: {value}\n    word:{yaml_text(word)}\n    meaning:{yaml_text(meaning)}\n'
                '    glyph: {fileID: 0}\n    plateGlyph: {fileID: 0}\n' + empty_event(4))
    out += f'  fadeInSeconds: {num(LOOK["fadeInSeconds"])}\n  fadeOutSeconds: {num(LOOK["fadeOutSeconds"])}\n'
    out += f'  suppressSaturation: {num(LOOK["suppressSaturation"])}\n  suppressExposure: {num(LOOK["suppressExposure"])}\n'
    out += f'  volumePriority: {num(LOOK["volumePriority"])}\n'
    out += f'  crystalMarkRadius: {num(LOOK["crystalMarkRadius"])}\n  vesselMarkRadius: {num(LOOK["vesselMarkRadius"])}\n'
    out += '  crystalMarkColor: {r: 1, g: 1, b: 1, a: 1}\n'
    out += f'  markIntensity: {num(LOOK["markIntensity"])}\n  markMinScreenRadius: {num(LOOK["markMinScreenRadius"])}\n'
    out += f'  unassignedSight: {UNASSIGNED_SIGHT}\n'
    return out


def mat_asset():
    return ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n'
            '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
            '  m_PrefabAsset: {fileID: 0}\n  m_Name: PilotSightMark\n'
            f'  m_Shader: {{fileID: 4800000, guid: {guid_of(SHADER_META)}, type: 3}}\n'
            '  m_Parent: {fileID: 0}\n  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n'
            '  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 1\n  m_DoubleSidedGI: 0\n'
            '  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n'
            '  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n'
            '    - _GlowFalloff: 2.5\n    - _MinScreenRadius: 0.02\n    - _RingGain: 1.6\n    - _RingPos: 0.45\n'
            '    - _RingWidth: 0.12\n    m_Colors: []\n  m_BuildTextureStacks: []\n  m_AllowLocalizedText: 1\n')


def native_meta(guid, main_id):
    return (f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n'
            f'  mainObjectFileID: {main_id}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def folder_meta():
    # Byte-identical to the same folder's meta on Shombith's branch (no trailing spaces).
    return (f'fileFormatVersion: 2\nguid: {FOLDER_GUID}\nfolderAsset: yes\nDefaultImporter:\n'
            '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')


def outputs(callsigns=CALLSIGNS):
    return {
        FOLDER_META: folder_meta(),
        SET: set_asset(callsigns),
        SET + '.meta': native_meta(SET_GUID, 11400000),
        MAT: mat_asset(),
        MAT + '.meta': native_meta(MAT_GUID, 2100000),
    }


def enum_values():
    text = open(os.path.join(ROOT, ENUM), encoding='utf-8').read()
    return {name.upper(): int(v) for name, v in re.findall(r'^\s*(\w+)\s*=\s*(\d+),', text, re.M) if name != 'None'}


def validate(callsigns=CALLSIGNS):
    errors = []
    values = enum_values()
    words = [w for _, w, _ in callsigns]
    if len(callsigns) != 17 or sorted(v for v, _, _ in callsigns) != list(range(1, 18)):
        errors.append('the set must hold exactly the seventeen callsigns, 1-17')
    for value, word, meaning in callsigns:
        if values.get(word) != value:
            errors.append(f'{word} is {value} here but {values.get(word)} in PilotVision.cs')
        if not meaning:
            errors.append(f'{word} has no line')
    if len({w[0] for w in words}) != len(words):
        errors.append('two callsigns share a first letter (CALLSIGNS.md §2: seventeen different first letters)')
    if len(set(words)) != len(words):
        errors.append('a callsign word is used twice')
    return errors


def drift(files):
    out = []
    for rel, want in files.items():
        path = os.path.join(ROOT, rel)
        have = open(path, encoding='utf-8').read() if os.path.exists(path) else None
        if have != want:
            out.append(rel if have is not None else rel + ' (missing)')
    return out


def self_test():
    ok = True
    bad = list(CALLSIGNS); bad[0] = (1, 'BANDOLEER', bad[0][2])  # B taken by BANDIT, and not the enum's name
    if not validate(bad):
        print('self-test FAILED: a duplicate first letter / renamed word validated'); ok = False
    bad = list(CALLSIGNS); bad[8] = (10, 'CITADEL', bad[8][2])
    if not validate(bad):
        print('self-test FAILED: a callsign off its enum value validated'); ok = False
    if not validate(CALLSIGNS[:-1]):
        print('self-test FAILED: sixteen callsigns validated'); ok = False
    if drift({SET + '.missing': 'x'}) != [SET + '.missing (missing)']:
        print('self-test FAILED: --check did not see a missing asset'); ok = False
    print('self-test passed' if ok else 'self-test FAILED')
    return 0 if ok else 1


def main():
    if '--self-test' in sys.argv:
        return self_test()
    errors = validate()
    if errors:
        for e in errors: print('ERROR:', e)
        return 1
    files = outputs()
    changed = drift(files)
    if '--check' in sys.argv:
        if changed:
            print('callsign assets DRIFT from author_callsign_assets.py:'); [print('  ' + c) for c in changed]
            return 1
        print(f'callsign assets match ({len(files)} files).')
        return 0
    for rel, body in files.items():
        path = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(body)
    print(f'wrote {len(changed)} of {len(files)} callsign files.' if changed else 'callsign assets already up to date.')
    return 0


if __name__ == '__main__':
    sys.exit(main())

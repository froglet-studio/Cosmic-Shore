#!/usr/bin/env python3
"""
Author the PLAIN payload's assets (Docs/Pilots/ARTIFACTS.md §2). This script OWNS them:

  Assets/Resources/PayloadConfig.asset                                  PayloadConfigSO
  Assets/_SO_Assets/Effects/Payload Prism Effects/PayloadCrackPrismEffect.asset
                                                                        PayloadCrackPrismEffectSO

and their .meta files (fixed guids). Every tuning value, authored line and (empty) FMOD slot is
written from SPEC below, so a change is made here and re-run, never in the inspector.

Usage:
    python3 Tools/Build/author_payload_assets.py             # write
    python3 Tools/Build/author_payload_assets.py --check     # diff disk; exit 1 on drift
    python3 Tools/Build/author_payload_assets.py --self-test # negative controls: --check must fail
"""
import os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

SCRIPTS = {
    'PayloadConfigSO': 'Assets/_Scripts/ScriptableObjects/PayloadConfigSO.cs.meta',
    'PayloadCrackPrismEffectSO':
        'Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Payload Prism Effects/PayloadCrackPrismEffectSO.cs.meta',
}

CONFIG = 'Assets/Resources/PayloadConfig.asset'
CONFIG_GUID = '6ae58718773b4e95a1bbe46e25737f66'
EFFECT_DIR = 'Assets/_SO_Assets/Effects/Payload Prism Effects'
EFFECT_DIR_GUID = '69f2bf7725e64b88a3c86239ce48e10a'
EFFECT = EFFECT_DIR + '/PayloadCrackPrismEffect.asset'
EFFECT_GUID = '21b48b5625e24e20b6d2edca3495fcc3'

# The tuning and the authored text. Starting values: a payload is six plates of an Ark's ~150,
# the hold keeps a dozen, and the burst reaches about a cell-core cluster.
SPEC = {
    'grabReach': 160,
    'parcelPrisms': 6,
    'hullReserve': 12,
    'carryOffset': (0, 4.5, -3),
    'carrySpacing': 1.6,
    'carryScale': 0.4,
    'seatSpeed': 120,
    'burstRadius': 60,
    'witherSeconds': 0.8,
    'grabbedLine': 'PAYLOAD ABOARD - RB TO RELEASE',
    'releasedLine': '',
    'outOfReachLine': 'BRING IT ALONGSIDE THE ARK FOR A PAYLOAD',
    'holdEmptyLine': 'THE ARK HAS NO HULL TO SPARE',
    'bannerSeconds': 3,
}
EFFECT_SPEC = {'alsoShielded': 1, 'leaveRegularShield': 1}
SOUNDS = ('grabSound', 'releaseSound', 'refusedSound')  # shipped EMPTY (the FMOD rule)


def script_guid(name):
    path = os.path.join(ROOT, SCRIPTS[name])
    for line in open(path, encoding='utf-8'):
        if line.startswith('guid:'):
            return line.split(':', 1)[1].strip()
    raise SystemExit(f'no guid in {SCRIPTS[name]}')


def num(v):
    return str(int(v)) if float(v).is_integer() else repr(float(v))


def text(s):
    return ' ' if s == '' else ' ' + ("'" + s.replace("'", "''") + "'" if any(c in s for c in ":#'\"") else s)


def header(script, name):
    return ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
            '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n'
            '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n'
            '  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            f'  m_Script: {{fileID: 11500000, guid: {script_guid(script)}, type: 3}}\n'
            f'  m_Name: {name}\n  m_EditorClassIdentifier: \n')


def empty_event(field):
    return (f'  {field}:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n'
            '    Path: \n')


def config_asset(spec=SPEC):
    out = header('PayloadConfigSO', 'PayloadConfig')
    for key in ('grabReach', 'parcelPrisms', 'hullReserve'):
        out += f'  {key}: {num(spec[key])}\n'
    x, y, z = spec['carryOffset']
    out += f'  carryOffset: {{x: {num(x)}, y: {num(y)}, z: {num(z)}}}\n'
    for key in ('carrySpacing', 'carryScale', 'seatSpeed', 'burstRadius'):
        out += f'  {key}: {num(spec[key])}\n'
    out += '  burstEffects:\n'
    out += f'  - {{fileID: 11400000, guid: {EFFECT_GUID}, type: 2}}\n'
    out += f'  witherSeconds: {num(spec["witherSeconds"])}\n'
    for key in ('grabbedLine', 'releasedLine', 'outOfReachLine', 'holdEmptyLine'):
        out += f'  {key}:{text(spec[key])}\n'
    out += f'  bannerSeconds: {num(spec["bannerSeconds"])}\n'
    for s in SOUNDS:
        out += empty_event(s)
    return out


def effect_asset(spec=EFFECT_SPEC):
    out = header('PayloadCrackPrismEffectSO', 'PayloadCrackPrismEffect')
    for key, value in spec.items():
        out += f'  {key}: {value}\n'
    return out


def asset_meta(guid):
    return (f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n'
            '  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def folder_meta(guid):
    return (f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n'
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def outputs(spec=SPEC, effect_spec=EFFECT_SPEC):
    return {
        CONFIG: config_asset(spec),
        CONFIG + '.meta': asset_meta(CONFIG_GUID),
        EFFECT_DIR + '.meta': folder_meta(EFFECT_DIR_GUID),
        EFFECT: effect_asset(effect_spec),
        EFFECT + '.meta': asset_meta(EFFECT_GUID),
    }


def validate(files):
    """Checks on what this script emits, independent of disk."""
    errors = []
    cfg = files[CONFIG]
    if EFFECT_GUID not in cfg:
        errors.append('PayloadConfig does not name the crack effect: the burst would do nothing')
    for s in SOUNDS:
        block = cfg.split(f'  {s}:\n', 1)
        if len(block) < 2 or 'Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0' not in block[1][:120]:
            errors.append(f'{s} is not an empty EventReference (the FMOD rule ships it empty)')
    if SPEC['parcelPrisms'] < 1 or SPEC['hullReserve'] < 1:
        errors.append('a payload takes at least one prism and the hold keeps at least one')
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
    """Negative controls: each must be caught, or the gate is not a gate."""
    ok = True
    files = outputs()
    bad = dict(files); bad[CONFIG] = bad[CONFIG].replace(EFFECT_GUID, '0' * 32)
    if not validate(bad):
        print('self-test FAILED: a config without the crack effect validated'); ok = False
    bad = dict(files); bad[CONFIG] = bad[CONFIG].replace('  grabSound:\n    Guid:\n      Data1: 0', '  grabSound:\n    Guid:\n      Data1: 7')
    if not validate(bad):
        print('self-test FAILED: a wired placeholder sound validated'); ok = False
    if not drift({CONFIG: files[CONFIG] + '# drift\n'}) and os.path.exists(os.path.join(ROOT, CONFIG)):
        print('self-test FAILED: --check did not see a changed asset'); ok = False
    if drift({CONFIG + '.missing': 'x'}) != [CONFIG + '.missing (missing)']:
        print('self-test FAILED: --check did not see a missing asset'); ok = False
    print('self-test passed' if ok else 'self-test FAILED')
    return 0 if ok else 1


def main():
    if '--self-test' in sys.argv:
        return self_test()
    files = outputs()
    errors = validate(files)
    if errors:
        for e in errors: print('ERROR:', e)
        return 1
    changed = drift(files)
    if '--check' in sys.argv:
        if changed:
            print('payload assets DRIFT from author_payload_assets.py:'); [print('  ' + c) for c in changed]
            return 1
        print(f'payload assets match ({len(files)} files).')
        return 0
    for rel, body in files.items():
        path = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(body)
    print(f'wrote {len(changed)} of {len(files)} payload files.' if changed else 'payload assets already up to date.')
    return 0


if __name__ == '__main__':
    sys.exit(main())

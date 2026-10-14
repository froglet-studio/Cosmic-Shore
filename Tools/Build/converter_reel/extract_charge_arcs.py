#!/usr/bin/env python3
"""Extracts the Charge light's shipped numbers for the converter reel lab into data/charge_arcs.json.

Reads, from the Elemental Converter's config (Assets/_SO_Assets/Environment/OmniCombine/OmniCombineConfig.asset):
  - chargeArcs        -> CrystalChargeArcsProfile.asset: every serialized field (src/chargeArcs.js settings() turns them
                         into ChargeArcSettings exactly as CrystalChargeArcsProfileSO.Settings does);
  - cadence           -> OmniCrystalCadence.asset: plateThickness (the omni's pentagons are built this much thicker);
  - omniArcsMaterial  -> OmniChargeArcs.mat: the arc shader's property values (the material BOTH converter arc layers
                         draw with), plus its sibling ChargeCrystalArcs.mat (the in-play Charge crystal's, for reference);
  - the shader those materials use (CrystalChargeArcsShader.shader): its declared property defaults and blend state.
Colours are given as authored (sRGB, as Unity stores them) and linearized (the project is in Linear colour space, so Unity
uploads Color.linear): the reel shader takes the linear ones.

Stdlib only. Re-run when the assets change; `--check` exits 1 if data/charge_arcs.json is stale.
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets')
OUT = os.path.join(HERE, 'data', 'charge_arcs.json')
CONFIG = 'Assets/_SO_Assets/Environment/OmniCombine/OmniCombineConfig.asset'


def rel(p):
    return os.path.relpath(p, ROOT).replace(os.sep, '/')


def read(p):
    with open(p, encoding='utf-8') as f:
        return f.read()


def guid_paths(wanted):
    """guid -> asset path, by scanning the .meta files under Assets/."""
    found = {}
    wanted = set(wanted)
    for dirpath, _, files in os.walk(ASSETS):
        for name in files:
            if not name.endswith('.meta'):
                continue
            p = os.path.join(dirpath, name)
            try:
                with open(p, encoding='utf-8', errors='replace') as f:
                    head = f.read(256)
            except OSError:
                continue
            m = re.search(r'^guid: ([0-9a-f]{32})', head, re.M)
            if m and m.group(1) in wanted:
                found[m.group(1)] = p[:-5]
                if len(found) == len(wanted):
                    return found
    missing = wanted - set(found)
    if missing:
        sys.exit(f'extract_charge_arcs: no asset for guid(s) {sorted(missing)}')
    return found


def ref_guid(text, field):
    m = re.search(r'^\s*' + re.escape(field) + r': \{fileID: \d+, guid: ([0-9a-f]{32}), type: \d+\}', text, re.M)
    if not m:
        sys.exit(f'extract_charge_arcs: {field} is not wired in {CONFIG}')
    return m.group(1)


def number(s):
    s = s.strip()
    try:
        return int(s)
    except ValueError:
        return float(s)


def mono_fields(text):
    """The serialized fields of a ScriptableObject asset (the 2-space-indented scalars after m_EditorClassIdentifier)."""
    body = text.split('m_EditorClassIdentifier:', 1)[1].split('\n', 1)[1]
    out = {}
    for line in body.splitlines():
        m = re.match(r'^  ([A-Za-z_]\w*): (-?[0-9.eE+-]+)\s*$', line)
        if m:
            out[m.group(1)] = number(m.group(2))
    return out


def srgb_to_linear(c):
    # Mathf.GammaToLinearSpace (the sRGB curve), as Color.linear applies it per channel.
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(path):
    text = read(path)
    shader = re.search(r'm_Shader: \{fileID: \d+, guid: ([0-9a-f]{32})', text).group(1)
    floats, colors = {}, {}
    section = None
    for line in text.splitlines():
        s = line.strip()
        if s in ('m_Floats:', 'm_Colors:', 'm_Ints:', 'm_TexEnvs:', 'm_TexEnvs: []', 'm_Ints: []'):
            section = s.rstrip(':')
            continue
        m = re.match(r'^- (_\w+): (.+)$', s)
        if not m:
            if not s.startswith('-') and section and not line.startswith('    '):
                section = None
            continue
        if section == 'm_Floats':
            floats[m.group(1)] = number(m.group(2))
        elif section == 'm_Colors':
            c = re.match(r'\{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}', m.group(2))
            rgba = [float(c.group(i)) for i in range(1, 5)]
            colors[m.group(1)] = {
                'srgb': rgba,
                'linear': [round(srgb_to_linear(x), 9) for x in rgba[:3]] + [rgba[3]],
            }
    return {'path': rel(path), 'shaderGuid': shader, 'floats': floats, 'colors': colors}


def shader_info(path):
    text = read(path)
    props = {}
    for m in re.finditer(r'^\s*(\[HDR\]\s*)?(_\w+)\s*\("([^"]*)",\s*(Color|Range\([^)]*\)|Float)\)\s*=\s*([^\n]+)$', text, re.M):
        default = m.group(5).strip()
        if m.group(4) == 'Color':
            val = [float(x) for x in default.strip('()').split(',')]
        else:
            val = number(default)
        props[m.group(2)] = {'label': m.group(3), 'type': m.group(4), 'hdr': bool(m.group(1)), 'default': val}
    state = {}
    for key in ('Blend', 'ZWrite', 'ZTest', 'Cull', 'Offset'):
        m = re.search(r'^\s*' + key + r' ([^\n/]+)', text, re.M)
        if m:
            state[key] = m.group(1).strip()
    queue = re.search(r'"Queue"\s*=\s*"([^"]+)"', text)
    return {'path': rel(path), 'properties': props, 'renderState': state, 'queue': queue.group(1) if queue else None}


def color_space():
    m = re.search(r'm_ActiveColorSpace: (\d)', read(os.path.join(ROOT, 'ProjectSettings', 'ProjectSettings.asset')))
    return 'Linear' if m and m.group(1) == '1' else 'Gamma'


def build():
    config = read(os.path.join(ROOT, CONFIG))
    g_profile, g_cadence, g_mat = ref_guid(config, 'chargeArcs'), ref_guid(config, 'cadence'), ref_guid(config, 'omniArcsMaterial')
    paths = guid_paths([g_profile, g_cadence, g_mat])
    profile_path, cadence_path, mat_path = paths[g_profile], paths[g_cadence], paths[g_mat]
    omni_mat = material(mat_path)
    sibling = os.path.join(os.path.dirname(mat_path), 'ChargeCrystalArcs.mat')
    mats = {'OmniChargeArcs': omni_mat}
    if os.path.exists(sibling):
        mats['ChargeCrystalArcs'] = material(sibling)
    shader_path = guid_paths([omni_mat['shaderGuid']])[omni_mat['shaderGuid']]
    cadence = mono_fields(read(cadence_path))
    space = color_space()
    return {
        'generatedBy': 'Tools/Build/converter_reel/extract_charge_arcs.py (re-run; --check fails when stale)',
        'sources': {
            'config': CONFIG,
            'profile': {'path': rel(profile_path), 'guid': g_profile},
            'cadence': {'path': rel(cadence_path), 'guid': g_cadence},
            'omniArcsMaterial': {'path': rel(mat_path), 'guid': g_mat},
            'shader': {'path': rel(shader_path), 'guid': omni_mat['shaderGuid']},
        },
        'colorSpace': space,
        'profile': mono_fields(read(profile_path)),
        'plateThickness': cadence['plateThickness'],
        'materials': mats,
        'arcMaterial': 'OmniChargeArcs',
        'shader': shader_info(shader_path),
    }


def main():
    data = json.dumps(build(), indent=2, sort_keys=True) + '\n'
    if '--check' in sys.argv:
        current = read(OUT) if os.path.exists(OUT) else ''
        if current != data:
            print(f'STALE  {rel(OUT)} differs from the assets: run python3 {rel(os.path.abspath(__file__))}')
            return 1
        print(f'OK  {rel(OUT)} matches the assets')
        return 0
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8') as f:
        f.write(data)
    print(f'wrote {rel(OUT)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())

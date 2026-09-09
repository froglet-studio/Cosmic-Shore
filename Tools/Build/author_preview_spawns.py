#!/usr/bin/env python3
"""
Author each ModePreview_*.asset's SPAWN block from the mode's own SCENE.

A preview used to author its own standoff, so where a card put you was an
independent guess at where the mode puts you - and the two disagreed by a lot
(Skim Race starts 728u out on a track facing down it; its preview opened 70u
from a core facing nothing).  The scene is the authority, so this reads it:

  * ServerPlayerVesselInitializer.arrangeSpawnPointsAroundCell / -Distance-
    OutsideNucleus / spawnRingRadiusFloor / spawnFormation, for the modes that
    COMPUTE their ring, and
  * the hand-placed playerSpawnPoints transforms, resolved to world poses and
    then expressed RELATIVE TO THE SCENE'S CELL, for the modes that do not.

Relative to the cell because a preview arena is parked 120k units away: an
absolute scene coordinate would put the vessel back at the menu's origin.

Usage:
    python3 Tools/Build/author_preview_spawns.py            # write
    python3 Tools/Build/author_preview_spawns.py --check    # verify only
"""
import os, re, sys, glob, math

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, 'Assets')

# GameModes id -> scene file name.  Only modes whose scene exists can be authored;
# a preview for a mode with no scene keeps whatever it has.
SCENE_FOR_MODE = {
    2:  'MinigameRampage',
    28: 'MinigameFreestyleMultiplayer_Gameplay',
    29: 'MinigameDuelForCellMultiplayer_Gameplay',
    30: 'ArcadeGameMultiplayer2v2CoOpVsAI',
    32: 'MinigameWildlifeBlitzMultuplayerCoOp',
    33: 'MinigameSkimRace',
    34: 'MinigameJoust_Gameplay',
    35: 'MinigameScurryMultiplayer_Gameplay',
    36: 'MinigameAstroLeague',
    37: 'MinigameAstroLeague',
    38: 'MinigameBroodRush',
    39: 'MinigamePeelTheCage',
    40: 'MinigameWildlifeLiberation',
    41: 'MinigameDogFight',
    42: 'MinigameBends',
    43: 'MinigameScarabScramble',
    44: 'MinigameSalvo',
    8:  'MinigameDuelForTheCell',
    26: 'MinigameWildlifeBlitz',
}


# ── Unity YAML: just enough of it ────────────────────────────────────────────

def load_docs(path):
    txt = open(path, encoding='utf-8', errors='ignore').read()
    docs = {}
    for m in re.finditer(r'^--- !u!(\d+) &(\d+).*$', txt, re.M):
        start = m.end()
        nxt = txt.find('\n--- !u!', start)
        docs[m.group(2)] = (m.group(1), txt[start:nxt if nxt > 0 else len(txt)])
    return docs


def vec3(body, key):
    m = re.search(r'^\s*' + key + r':\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+),\s*z:\s*([-\d.eE]+)', body, re.M)
    return tuple(float(x) for x in m.groups()) if m else (0.0, 0.0, 0.0)


def quat(body, key):
    m = re.search(r'^\s*' + key + r':\s*\{x:\s*([-\d.eE]+),\s*y:\s*([-\d.eE]+),\s*z:\s*([-\d.eE]+),\s*w:\s*([-\d.eE]+)', body, re.M)
    return tuple(float(x) for x in m.groups()) if m else (0.0, 0.0, 0.0, 1.0)


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def qrot(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    tx = 2 * (y * vz - z * vy)
    ty = 2 * (z * vx - x * vz)
    tz = 2 * (x * vy - y * vx)
    return (vx + w * tx + (y * tz - z * ty),
            vy + w * ty + (z * tx - x * tz),
            vz + w * tz + (x * ty - y * tx))


def world_pose(docs, tid):
    """World (position, rotation) of the Transform with fileID tid."""
    pos, rot = (0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0)
    seen = set()
    while tid and tid in docs and tid not in seen:
        seen.add(tid)
        cls, body = docs[tid]
        if cls not in ('4', '224'):
            break
        lp, lr, ls = vec3(body, 'm_LocalPosition'), quat(body, 'm_LocalRotation'), vec3(body, 'm_LocalScale')
        scaled = (pos[0] * ls[0], pos[1] * ls[1], pos[2] * ls[2])
        r = qrot(lr, scaled)
        pos = (lp[0] + r[0], lp[1] + r[1], lp[2] + r[2])
        rot = qmul(lr, rot)
        m = re.search(r'^\s*m_Father:\s*\{fileID:\s*(\d+)', body, re.M)
        tid = m.group(1) if m and m.group(1) != '0' else None
    return pos, rot


def transform_of_gameobject(docs, goid):
    for fid, (cls, body) in docs.items():
        if cls not in ('4', '224'):
            continue
        m = re.search(r'^\s*m_GameObject:\s*\{fileID:\s*(\d+)', body, re.M)
        if m and m.group(1) == goid:
            return fid
    return None


def cell_origin(docs, cell_script_guids):
    """World position of the scene's Cell, or the origin when it has none."""
    for fid, (cls, body) in docs.items():
        if cls != '114':
            continue
        m = re.search(r'^\s*m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})', body, re.M)
        if not m or m.group(1) not in cell_script_guids:
            continue
        go = re.search(r'^\s*m_GameObject:\s*\{fileID:\s*(\d+)', body, re.M)
        if not go:
            continue
        tid = transform_of_gameobject(docs, go.group(1))
        if tid:
            return world_pose(docs, tid)[0]
    return (0.0, 0.0, 0.0)


def script_guid(rel_path):
    meta = os.path.join(ROOT, rel_path + '.meta')
    if not os.path.exists(meta):
        return None
    m = re.search(r'^guid:\s*([0-9a-f]{32})', open(meta, encoding='utf-8', errors='ignore').read(), re.M)
    return m.group(1) if m else None


# ── The initializer's authored block ─────────────────────────────────────────

def read_scene_spawn(scene_path, cell_guids):
    docs = load_docs(scene_path)

    init = None
    for _fid, (cls, body) in docs.items():
        if cls == '114' and ('arrangeSpawnPointsAroundCell:' in body or 'playerSpawnPoints:' in body):
            init = body
            break
    if init is None:
        return None

    def num(key, default):
        m = re.search(r'^\s*' + key + r':\s*([-\d.eE]+)', init, re.M)
        return float(m.group(1)) if m else default

    ring = num('arrangeSpawnPointsAroundCell', 0) != 0
    result = {
        'ring': ring,
        'distance': num('spawnDistanceOutsideNucleus', 40.0),
        'floor': num('spawnRingRadiusFloor', 0.0),
        'formation': int(num('spawnFormation', 0)),
        'points': [],
    }

    if ring:
        return result   # the ring wins; hand-placed transforms are unused

    m = re.search(r'^\s*playerSpawnPoints:\s*\n((?:\s*-\s*\{fileID:.*\n)+)', init, re.M)
    if not m:
        return result

    origin = cell_origin(docs, cell_guids)
    for fid in re.findall(r'fileID:\s*(\d+)', m.group(1)):
        if fid == '0':
            continue
        tid = fid if fid in docs and docs[fid][0] in ('4', '224') else transform_of_gameobject(docs, fid)
        if not tid:
            continue
        p, q = world_pose(docs, tid)
        result['points'].append(((p[0] - origin[0], p[1] - origin[1], p[2] - origin[2]), q))
    return result


# ── Writing the asset ────────────────────────────────────────────────────────

def fmt(x):
    return f'{x:.4f}'.rstrip('0').rstrip('.') or '0'


def spawn_block(data):
    lines = [
        f"  SpawnFromCellRing: {1 if data['ring'] else 0}",
        f"  SpawnDistanceOutsideNucleus: {fmt(data['distance'])}",
        f"  SpawnRingRadiusFloor: {fmt(data['floor'])}",
        f"  SpawnFormation: {data['formation']}",
    ]
    if not data['points']:
        lines.append('  SpawnPoints: []')
    else:
        lines.append('  SpawnPoints:')
        for (px, py, pz), (qx, qy, qz, qw) in data['points']:
            lines.append(f'  - position: {{x: {fmt(px)}, y: {fmt(py)}, z: {fmt(pz)}}}')
            lines.append(f'    rotation: {{x: {fmt(qx)}, y: {fmt(qy)}, z: {fmt(qz)}, w: {fmt(qw)}}}')
    return '\n'.join(lines) + '\n'


FIELDS = ('SpawnFromCellRing', 'SpawnDistanceOutsideNucleus', 'SpawnRingRadiusFloor',
          'SpawnFormation', 'SpawnPoints')


def strip_fields(text):
    """Remove any existing spawn fields (and SpawnPoints' list body) from the asset."""
    out, skipping = [], False
    for line in text.splitlines(keepends=True):
        key = re.match(r'^  ([A-Za-z_]\w*):', line)
        if key:
            skipping = key.group(1) in FIELDS
        elif skipping and not re.match(r'^  [-\s]', line):
            skipping = False
        if not skipping:
            out.append(line)
    return ''.join(out)


def main():
    check = '--check' in sys.argv

    cell_guids = {g for g in (script_guid('Assets/_Scripts/Controller/Environment/Cell.cs'),) if g}
    if not cell_guids:
        print('WARN: could not resolve Cell.cs guid - hand-placed poses will be relative to the '
              'scene origin instead of its cell.')

    scenes = {}
    for path in glob.glob(os.path.join(ASSETS, '_Scenes', '**', '*.unity'), recursive=True):
        scenes[os.path.basename(path)[:-6]] = path

    changed, checked, problems = [], 0, []

    for asset in sorted(glob.glob(os.path.join(ASSETS, '**', 'ModePreview_*.asset'), recursive=True)):
        text = open(asset, encoding='utf-8').read()
        m = re.search(r'^  Mode:\s*(\d+)', text, re.M)
        if not m:
            continue
        mode = int(m.group(1))
        name = os.path.basename(asset)[:-6]

        scene_name = SCENE_FOR_MODE.get(mode)
        if not scene_name or scene_name not in scenes:
            problems.append(f'{name}: mode {mode} has no scene mapping - left alone')
            continue

        data = read_scene_spawn(scenes[scene_name], cell_guids)
        if data is None:
            problems.append(f'{name}: {scene_name} has no spawn initializer - left alone')
            continue

        block = spawn_block(data)
        stripped = strip_fields(text)
        # Insert after SpawnDistanceOutsideNucleus' old home: end of file is fine for a
        # MonoBehaviour asset, but keep it tidy by appending before any trailing blank line.
        updated = stripped.rstrip('\n') + '\n' + block

        checked += 1
        if updated != text:
            changed.append(name)
            if not check:
                open(asset, 'w', encoding='utf-8').write(updated)

        where = ('ring' if data['ring'] else f"{len(data['points'])} authored")
        print(f"  {name:<34} {scene_name:<44} {where:<12} "
              f"dist={fmt(data['distance'])} floor={fmt(data['floor'])} form={data['formation']}")

    for p in problems:
        print(f'  ! {p}')

    print(f'\n{checked} preview definition(s) inspected, {len(changed)} '
          f'{"would change" if check else "written"}.')
    if check and changed:
        print('  ' + ', '.join(changed))
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())

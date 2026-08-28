#!/usr/bin/env python3
"""Author ModePreviewDefinitionSO.TrackSpawnablesByIntensity from the scenes' own spawners.

Three arcade modes stand their arena from a SCENE-level SegmentSpawner rather than from their
cell config, so the card's scale model - which reads the cell - showed open water for all of
them. This script reads the shape straight out of the scene files (never hand-copied guids):

  - Joust / Scurry author `spawnableByIntensity` - four prefabs, one per intensity - which maps
    1:1 onto TrackSpawnablesByIntensity.
  - Skim Race's track is a scene-local SpawnableWaypointTrack (an object, not a prefab), so it
    was baked once into Assets/_Prefabs/Environment/Spawners/HexRaceWaypointTrack.prefab; the
    preview references that single intensity-aware entry. If the scene's track is ever retuned,
    re-bake the prefab (the bake is a verbatim copy of the component body) and re-run this.

Run with --check to verify the assets already say what the scenes say (CI-style), no args to
write them.
"""
import os, re, sys, glob

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..'))
SCENES = os.path.join(ROOT, 'Assets', '_Scenes')
PREVIEWS = os.path.join(ROOT, 'Assets', '_SO_Assets', 'Mode Previews')
SEGMENT_SPAWNER_GUID = 'b5d3599573f5d3348865c8d5efe8c29f'
HEXRACE_TRACK_PREFAB = os.path.join(ROOT, 'Assets', '_Prefabs', 'Environment', 'Spawners',
                                    'HexRaceWaypointTrack.prefab')

# mode preview asset -> scene that stands its arena (None = the baked prefab case)
PLAN = {
    'ModePreview_Joust.asset': os.path.join(SCENES, 'Multiplayer Scenes',
                                            'MinigameJoust_Gameplay.unity'),
    'ModePreview_Scurry.asset': os.path.join(SCENES, 'Multiplayer Scenes',
                                             'MinigameCrystalCaptureMultiplayer_Gameplay.unity'),
    'ModePreview_SkimRace.asset': None,
}

def read(path):
    with open(path, encoding='utf-8', errors='ignore') as f:
        return f.read()

def prefab_guid(prefab_path):
    return re.search(r'^guid:\s*([0-9a-f]{32})', read(prefab_path + '.meta'), re.M).group(1)

def spawnable_component_fileid(prefab_path):
    """The SpawnableBase component's fileID inside a prefab: the first MonoBehaviour carrying the
    base class's serialized `domain` field. (An age marker like layBudgetMsPerFrame does not work
    - Unity omits fields a prefab was serialized before, and the older spawnables predate it.)"""
    text = read(prefab_path)
    for doc in text.split('--- !u!')[1:]:
        m = re.match(r'114 &(\d+)', doc)
        if not m:
            continue
        if re.search(r'^  domain:\s', doc, re.M):
            return m.group(1)
    raise SystemExit(f'no SpawnableBase component found in {prefab_path}')

def scene_by_intensity(scene_path, guid_to_prefab):
    """The scene SegmentSpawner's spawnableByIntensity prefab list, in order."""
    text = read(scene_path)
    for doc in text.split('--- !u!')[1:]:
        if f'm_Script: {{fileID: 11500000, guid: {SEGMENT_SPAWNER_GUID}' not in doc:
            continue
        m = re.search(r'^\s*spawnableByIntensity:\s*\n((?:\s*-\s*\{fileID.*\n)+)', doc, re.M)
        if not m:
            continue
        guids = re.findall(r'guid:\s*([0-9a-f]{32})', m.group(1))
        if guids:
            return [guid_to_prefab[g] for g in guids]
    raise SystemExit(f'no populated spawnableByIntensity in {scene_path}')

def build_guid_map():
    out = {}
    for meta in glob.glob(os.path.join(ROOT, 'Assets', '_Prefabs', '**', '*.prefab.meta'),
                          recursive=True):
        m = re.search(r'^guid:\s*([0-9a-f]{32})', read(meta), re.M)
        if m:
            out[m.group(1)] = meta[:-5]
    return out

def desired_block(prefabs):
    lines = ['  TrackSpawnablesByIntensity:']
    for p in prefabs:
        lines.append(f'  - {{fileID: {spawnable_component_fileid(p)}, guid: {prefab_guid(p)}, type: 3}}')
    return '\n'.join(lines) + '\n'

def apply(asset_path, block, check):
    text = read(asset_path)
    # replace an existing list (with entries or empty) or insert after StructurePrefab
    pattern = re.compile(r'^  TrackSpawnablesByIntensity:.*\n(?:  - .*\n)*', re.M)
    if pattern.search(text):
        new = pattern.sub(block, text, count=1)
    else:
        anchor = re.search(r'^  StructurePrefab:.*\n', text, re.M)
        if not anchor:
            raise SystemExit(f'{asset_path}: no StructurePrefab anchor')
        new = text[:anchor.end()] + block + text[anchor.end():]

    changed = new != text
    if check:
        return changed
    if changed:
        with open(asset_path, 'w', encoding='utf-8') as f:
            f.write(new)
    return changed

def main():
    check = '--check' in sys.argv
    guid_to_prefab = build_guid_map()
    dirty = []

    for asset_name, scene in PLAN.items():
        asset_path = os.path.join(PREVIEWS, asset_name)
        prefabs = ([HEXRACE_TRACK_PREFAB] if scene is None
                   else scene_by_intensity(scene, guid_to_prefab))
        block = desired_block(prefabs)
        if apply(asset_path, block, check):
            dirty.append(asset_name)
        names = ', '.join(os.path.basename(p)[:-7] for p in prefabs)
        print(f'{asset_name}: {names}')

    if check and dirty:
        raise SystemExit(f'STALE - re-run without --check: {", ".join(dirty)}')
    print('check OK' if check else f'wrote {len(dirty)} asset(s)')

if __name__ == '__main__':
    main()

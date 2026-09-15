"""
Author ModePreviewDefinitionSO.PreviewCellsByIntensity from each mode's OWN scene.

A mode whose scene Cell runs CellTypeChoiceOptions.IntensityWise already declares one
CellConfigDataSO per intensity, in intensity order. The preview must show the arena the
player is about to get, so it copies that list verbatim rather than re-deriving it - the
scene is the authority and any second list is a second answer.

Read-only on scenes. --check reports without writing.
"""
import glob, os, re, sys

ROOT = "Assets"
CHECK = "--check" in sys.argv

# Modes whose per-intensity configs differ in something a PREVIEW cannot show. Standing a
# satellite is the expensive half of the feature (Docs/ModePreview/ARCHITECTURE.md), so a rebuild
# that lands on a visually identical arena is pure cost - these keep their single PreviewCell and
# simply do not rebuild when the intensity row moves.
#   Rampage: its four configs hold an IDENTICAL 9,830-prism forest and differ only in crystal
#   count and wildlife scale - and a satellite has no CrystalManager at all, so the one thing
#   that varies is the one thing the preview cannot draw. Its own definition Notes say so.
EXCLUDE_MODES = {2}   # GameModes.Rampage

# mode int -> scene name, from the arcade cards
mode_to_scene = {}
for card in glob.glob(f"{ROOT}/_SO_Assets/Games/*.asset"):
    t = open(card, encoding="utf-8", errors="replace").read()
    m = re.search(r"^  Mode: (-?\d+)", t, re.M)
    s = re.search(r"^  SceneName: (.+)$", t, re.M)
    if m and s:
        scene = s.group(1).strip()
        if scene:
            mode_to_scene[int(m.group(1))] = scene

scene_paths = {os.path.splitext(os.path.basename(p))[0]: p
               for p in glob.glob(f"{ROOT}/_Scenes/**/*.unity", recursive=True)}

CELL_BLOCK = re.compile(
    r"^  CellConfigs:\n((?:  - \{fileID: \d+, guid: [0-9a-f]+, type: \d+\}\n)+)"
    r"  cellTypeChoiceOptions: (\d+)", re.M)

def intensity_configs(scene_name):
    p = scene_paths.get(scene_name)
    if not p:
        return None, f"scene '{scene_name}' not on disk"
    text = open(p, encoding="utf-8", errors="replace").read()
    for block, choice in CELL_BLOCK.findall(text):
        if choice != "1":            # 1 = IntensityWise
            continue
        refs = [l.strip()[2:] for l in block.strip().split("\n")]
        return refs, None
    return None, "no IntensityWise Cell"

wrote = skipped = 0
for asset in sorted(glob.glob(f"{ROOT}/_SO_Assets/Mode Previews/ModePreview_*.asset")):
    text = open(asset, encoding="utf-8").read()
    name = os.path.basename(asset)
    m = re.search(r"^  Mode: (-?\d+)", text, re.M)
    if not m:
        print(f"SKIP {name}: no Mode"); skipped += 1; continue

    mode_int = int(m.group(1))
    if mode_int in EXCLUDE_MODES:
        print(f"SKIP {name}: mode {mode_int} excluded (intensity is not visible in a preview)")
        skipped += 1; continue

    scene = mode_to_scene.get(mode_int)
    if not scene:
        print(f"SKIP {name}: mode {mode_int} has no scene name"); skipped += 1; continue

    refs, why = intensity_configs(scene)
    if not refs:
        print(f"SKIP {name}: {why}"); skipped += 1; continue
    if len(refs) < 2:
        print(f"SKIP {name}: only {len(refs)} config"); skipped += 1; continue

    if "PreviewCellsByIntensity:" in text:
        print(f"SKIP {name}: already authored"); skipped += 1; continue

    lines = ["  PreviewCellsByIntensity:"] + [f"  - {r}" for r in refs]
    new = re.sub(r"^  StructurePrefab:", "\n".join(lines) + "\n  StructurePrefab:",
                 text, count=1, flags=re.M)
    if new == text:
        print(f"SKIP {name}: no StructurePrefab anchor"); skipped += 1; continue

    print(f"{'WOULD WRITE' if CHECK else 'WROTE'} {name}: {len(refs)} arenas (from {scene})")
    if not CHECK:
        open(asset, "w", encoding="utf-8").write(new)
    wrote += 1

print(f"\n{wrote} authored, {skipped} skipped.")

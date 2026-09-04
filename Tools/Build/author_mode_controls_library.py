#!/usr/bin/env python3
"""
Author Resources/ModeControlsLibrary.asset from data the modes ALREADY carry.

Each previewable mode gets one entry so its per-mode fields (Abilities filter,
Vessel override, ShowAbilityRows) have a home. The entry's Rows start EMPTY:
"how you win" lives in the launch panel's OBJECTIVE BOX (bound from the mode's
ModePreview ObjectiveText/ObjectiveMetric), not in the CONTROLS section — an
objective row here would say the same thing twice on one card.

This script previously SEEDED one objective row per mode; it now RETIRES them.
It removes exactly the row it once owned — the one whose headline equals that
mode's current ObjectiveText — and passes every hand-authored row, Abilities,
Vessel and ShowAbilityRows value through untouched.

Usage:
    python3 Tools/Build/author_mode_controls_library.py            # write
    python3 Tools/Build/author_mode_controls_library.py --check    # verify only
"""
import os, re, sys, glob

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSET = os.path.join(ROOT, 'Assets', 'Resources', 'ModeControlsLibrary.asset')


def objectives():
    """mode id -> ObjectiveText, from the ModePreview assets."""
    out = {}
    for path in sorted(glob.glob(os.path.join(ROOT, 'Assets', '**', 'ModePreview_*.asset'),
                                 recursive=True)):
        text = open(path, encoding='utf-8').read()
        mode = re.search(r'^  Mode:\s*(\d+)', text, re.M)
        obj = re.search(r"^  ObjectiveText:\s*(.*)$", text, re.M)
        if not mode or not obj:
            continue
        value = obj.group(1).strip()
        if value.startswith("'") and value.endswith("'"):
            value = value[1:-1].replace("''", "'")
        elif value.startswith('"') and value.endswith('"'):
            value = value[1:-1]
        if value:
            out[int(mode.group(1))] = value
    return out


def yaml_quote(s):
    return "'" + s.replace("'", "''") + "'"


def parse_entries(text):
    """Existing Entries as raw blocks, keyed by mode id. Preserves hand-authored fields."""
    m = re.search(r'^  Entries:\s*(\[\]\s*)?$', text, re.M)
    if m:
        return {}, []
    entries, order = {}, []
    block = re.search(r'^  Entries:\n((?:  - .*\n(?:    .*\n)*)+)', text, re.M)
    if not block:
        return {}, []
    for raw in re.findall(r'(  - Mode:.*\n(?:    .*\n)*)', block.group(1)):
        mode = int(re.search(r'Mode:\s*(\d+)', raw).group(1))
        entries[mode] = raw
        order.append(mode)
    return entries, order


def strip_owned_row(raw, obj):
    """Remove the seeded objective row (headline == the mode's ObjectiveText), if present.
    An emptied Rows list collapses back to []. Hand-authored rows are untouched."""
    if not obj:
        return raw
    quoted = re.escape(yaml_quote(obj))
    row = (r'    - Headline: ' + quoted +
           r'\n      Description: .*\n      Icon: \{fileID: 0\}\n      Control: 0\n')
    stripped = re.sub(row, '', raw, count=1)
    stripped = re.sub(r'^    Rows:\n(?=    [A-Z])', '    Rows: []\n', stripped, count=1, flags=re.M)
    return stripped


def build_entry(mode, existing_raw):
    if existing_raw is not None:
        return existing_raw
    return ('  - Mode: ' + str(mode) + '\n'
            + '    Rows: []\n'
            + '    ShowAbilityRows: 1\n'
            + '    Abilities: []\n'
            + '    Vessel: -1\n')


def main():
    check = '--check' in sys.argv
    text = open(ASSET, encoding='utf-8').read()
    objs = objectives()
    existing, order = parse_entries(text)

    modes = order + [m for m in sorted(objs) if m not in existing]
    blocks = []
    for mode in modes:
        raw = existing.get(mode)
        if raw is None and mode not in objs:
            continue
        entry = build_entry(mode, raw)
        blocks.append(strip_owned_row(entry, objs.get(mode)))

    head = text.split('  Entries:')[0]
    updated = head + '  Entries:\n' + ''.join(blocks)

    if updated == text:
        print(f'{len(blocks)} entries; asset already up to date.')
        return 0
    if check:
        print(f'{len(blocks)} entries; asset WOULD change.')
        return 1
    open(ASSET, 'w', encoding='utf-8').write(updated)
    print(f'{len(blocks)} entries written.')
    return 0


if __name__ == '__main__':
    sys.exit(main())

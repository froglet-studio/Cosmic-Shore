#!/usr/bin/env python3
"""
Author Resources/ModeControlsLibrary.asset from data the modes ALREADY carry.

Each previewable mode gets one entry whose CONTROLS section opens with the mode's
objective - the same ObjectiveText its ModePreview_*.asset authors for the in-preview
HUD - so a card says what you are supposed to DO before it lists the buttons, and a
mode with no vessel of its own (the duel cards) still has a section to show.

The rows are data, not code: re-run after editing any ObjectiveText. Hand-authored
additions survive - the script only replaces the one objective row it owns (matched by
its headline being exactly the previous objective text, or the row list being empty)
and never touches Abilities / Vessel / ShowAbilityRows on an existing entry.

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


def objective_row(text_value):
    return (f'    - Headline: {yaml_quote(text_value)}\n'
            '      Description: \n'
            '      Icon: {fileID: 0}\n'
            '      Control: 0\n')


def build_entry(mode, obj, existing_raw, prior_obj):
    if existing_raw is None:
        return ('  - Mode: ' + str(mode) + '\n'
                + '    Rows:\n' + objective_row(obj)
                + '    ShowAbilityRows: 1\n'
                + '    Abilities: []\n'
                + '    Vessel: -1\n')

    # Replace only the row whose headline is the PREVIOUS objective (or seed an empty list);
    # everything hand-authored - extra rows, Abilities, Vessel - passes through untouched.
    raw = existing_raw
    if re.search(r'^    Rows:\s*\[\]\s*$', raw, re.M):
        raw = re.sub(r'^    Rows:\s*\[\]\s*$', '    Rows:\n' + objective_row(obj).rstrip('\n'),
                     raw, count=1, flags=re.M)
        return raw
    if prior_obj:
        quoted = re.escape(yaml_quote(prior_obj))
        row = (r'    - Headline: ' + quoted +
               r'\n      Description: .*\n      Icon: \{fileID: 0\}\n      Control: 0\n')
        if re.search(row, raw):
            return re.sub(row, objective_row(obj), raw, count=1)
    if yaml_quote(obj) not in raw:
        # No owned row found - put the objective first without disturbing the rest.
        raw = re.sub(r'^    Rows:\n', '    Rows:\n' + objective_row(obj), raw, count=1, flags=re.M)
    return raw


def main():
    check = '--check' in sys.argv
    text = open(ASSET, encoding='utf-8').read()
    objs = objectives()
    existing, order = parse_entries(text)

    modes = order + [m for m in sorted(objs) if m not in existing]
    blocks = []
    for mode in modes:
        obj = objs.get(mode)
        raw = existing.get(mode)
        if obj is None and raw is not None:
            blocks.append(raw)
            continue
        if obj is None:
            continue
        blocks.append(build_entry(mode, obj, raw, prior_obj=obj))

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

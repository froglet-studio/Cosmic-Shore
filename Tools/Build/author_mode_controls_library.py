#!/usr/bin/env python3
"""
Author Resources/ModeControlsLibrary.asset from data the modes ALREADY carry.

"How you win" lives in the launch panel's OBJECTIVE BOX (bound from the mode's
ModePreview ObjectiveText/ObjectiveMetric), not in the CONTROLS section — an
objective row here would say the same thing twice on one card.

This script previously SEEDED one entry (with one objective row) per previewable
mode; it now RETIRES those rows. It removes exactly the row it once owned — the
one whose headline equals that mode's current ObjectiveText — and passes every
hand-authored row, Abilities, Vessel and ShowAbilityRows value through untouched.

It no longer ADDS entries. An entry with no rows, no filter and no vessel is
what ModeControlsLibrarySO.EntryFor already answers for a mode with NO entry
(except that it would shadow DefaultRows), so seeding one per preview bought
nothing and made --check fail every time a new mode shipped a preview. A mode
gets an entry when someone has something to say in it - added in the inspector,
then left alone by this script.

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
    # `[ \t]` rather than `\s`: `\s*` crosses the newline, so `Entries:\s*$` matched EVERY
    # populated list too, reported it empty, and a write rebuilt the list from scratch -
    # deleting every hand-authored Abilities filter and Vessel override in the asset.
    if re.search(r'^  Entries:[ \t]*\[\][ \t]*$', text, re.M):
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


def main():
    check = '--check' in sys.argv
    text = open(ASSET, encoding='utf-8').read()
    objs = objectives()
    existing, order = parse_entries(text)

    # Existing entries only, in their authored order (the module docstring says why this no
    # longer seeds one per previewable mode).
    blocks = [strip_owned_row(existing[mode], objs.get(mode)) for mode in order]
    if not blocks:
        print('no entries; nothing to retire.')
        return 0

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

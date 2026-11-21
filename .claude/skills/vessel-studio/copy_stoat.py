#!/usr/bin/env python3
"""copy_stoat.py - make the hub copy Docs/Studios/VesselStudio/stoat.html from its source of truth,
Docs/Studios/StoatFlightStudio.html (the /vessel-studio skill, section 4). The copy differs in exactly
three ways, and nothing else may differ:

  1. its header comment names the source and the hub;
  2. a "<- Vessel Studio" back link opens the page header;
  3. shared studio scripts are loaded from beside it (VesselStudio/x.js -> x.js), because the hub
     copy lives in VesselStudio/ while the source lives one folder up.

    python3 .claude/skills/vessel-studio/copy_stoat.py            # write the copy
    python3 .claude/skills/vessel-studio/copy_stoat.py --check    # exit 1 if the copy is stale
"""
import re
import sys

SRC = 'Docs/Studios/StoatFlightStudio.html'
DST = 'Docs/Studios/VesselStudio/stoat.html'
HEAD_OLD = '<!-- Stoat Flight Studio. '
HEAD_NEW = ('<!-- Stoat Flight Studio, as a page of the Vessel Studio (Docs/Studios/VesselStudio/). Source of truth: '
            'Docs/Studios/StoatFlightStudio.html; re-copy it here with .claude/skills/vessel-studio/copy_stoat.py. ')
ANCHOR = '<div class="wrap">\n  <header>\n'
BACK = ("    <a href=\"index.html\" style=\"font:600 13px/1.2 'Chakra Petch',system-ui,sans-serif;letter-spacing:.06em;"
        "text-transform:uppercase;color:inherit;opacity:.75;text-decoration:none;white-space:nowrap\">&larr; Vessel Studio</a>\n")


def make(src):
    if src.count(HEAD_OLD) != 1 or src.count(ANCHOR) != 1:
        raise SystemExit('copy_stoat: the source no longer has exactly one header comment / page header to anchor on')
    out = src.replace(HEAD_OLD, HEAD_NEW, 1).replace(ANCHOR, ANCHOR + BACK, 1)
    return re.sub(r'<script src="VesselStudio/([A-Za-z0-9_.-]+\.js)"></script>', r'<script src="\1"></script>', out)


if __name__ == '__main__':
    want = make(open(SRC, encoding='utf-8').read())
    if '--check' in sys.argv:
        have = open(DST, encoding='utf-8').read()
        print('copy_stoat: ' + ('up to date' if have == want else 'STALE - run without --check'))
        sys.exit(0 if have == want else 1)
    open(DST, 'w', encoding='utf-8', newline='').write(want)
    print('copy_stoat: wrote ' + DST)

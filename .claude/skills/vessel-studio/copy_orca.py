#!/usr/bin/env python3
"""copy_orca.py - make the hub copy Docs/Studios/VesselStudio/orca.html from its source of truth,
Docs/Studios/CrewOfFourLab.html (Garrett's Crew of Four Lab, the Orca; /vessel-studio section 4, the copy_stoat.py
pattern). The copy differs in exactly four ways, and nothing else may differ:

  1. its header comment names the source and the hub;
  2. a "<- Vessel Studio" back link opens the page header;
  3. it loads studio-theme.js in <head> (the one look and host helper every hub page loads, parity_gate.py);
  4. it carries the Sync panel tag (sync.js) once, before </body>;
  5. its header names only the device (PC / Phone), never the host shell: every surface looks the same (D34);
  6. it drops the lab's own Google Fonts link (studio-theme.js loads the studio fonts; build_artifact.py --check).

    python3 .claude/skills/vessel-studio/copy_orca.py            # write the copy
    python3 .claude/skills/vessel-studio/copy_orca.py --check    # exit 1 if the copy is stale
"""
import sys

SRC = 'Docs/Studios/CrewOfFourLab.html'
DST = 'Docs/Studios/VesselStudio/orca.html'
HEAD_OLD = '  CREW OF FOUR LAB - a /labmaker browser studio '
HEAD_NEW = ('  CREW OF FOUR LAB (the Orca), as a page of the Vessel Studio (Docs/Studios/VesselStudio/). Source of truth: '
            'Docs/Studios/CrewOfFourLab.html; re-copy it here with .claude/skills/vessel-studio/copy_orca.py.\n'
            '  CREW OF FOUR LAB - a /labmaker browser studio ')
ANCHOR = '<div id="app">\n  <header>\n'
BACK = ("    <a href=\"index.html\" style=\"font:600 13px/1.2 'Chakra Petch',system-ui,sans-serif;letter-spacing:.06em;"
        "text-transform:uppercase;color:inherit;opacity:.75;text-decoration:none;white-space:nowrap\">&larr; Vessel Studio</a>\n")
HEAD_END = '</style>\n</head>\n'
THEME = '</style>\n<script src="studio-theme.js"></script>\n</head>\n'
BODY_END = '</body>'
SYNC = '<script src="sync.js"></script>\n</body>'
SHELL_OLD = "$('platform').textContent = (PLATFORM.shell === 'web' ? 'Web' : PLATFORM.shell) + ' · ' + (PLATFORM.device === 'phone' ? 'Phone' : 'PC');"
FONTS = ('<link rel="preconnect" href="https://fonts.googleapis.com">\n'
         '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Saira+Condensed:wght@500;700&family=IBM+Plex+Sans:wght@400;600&family=IBM+Plex+Mono:wght@400;600&display=swap">\n')
SHELL_NEW = "$('platform').textContent = PLATFORM.device === 'phone' ? 'Phone' : 'PC';"


def make(src):
    for k in (HEAD_OLD, ANCHOR, HEAD_END, BODY_END, SHELL_OLD, FONTS):
        if src.count(k) != 1:
            raise SystemExit('copy_orca: the source no longer has exactly one ' + repr(k[:40]) + ' to anchor on')
    if 'studio-theme.js' in src or 'sync.js' in src:
        raise SystemExit('copy_orca: the source already loads a hub script; update this copier')
    return (src.replace(HEAD_OLD, HEAD_NEW, 1).replace(ANCHOR, ANCHOR + BACK, 1)
            .replace(HEAD_END, THEME, 1).replace(BODY_END, SYNC, 1).replace(SHELL_OLD, SHELL_NEW, 1).replace(FONTS, '', 1))


if __name__ == '__main__':
    want = make(open(SRC, encoding='utf-8').read())
    if '--check' in sys.argv:
        try:
            have = open(DST, encoding='utf-8').read()
        except FileNotFoundError:
            have = None
        print('copy_orca: ' + ('up to date' if have == want else 'STALE - run without --check'))
        sys.exit(0 if have == want else 1)
    open(DST, 'w', encoding='utf-8', newline='').write(want)
    print('copy_orca: wrote ' + DST)

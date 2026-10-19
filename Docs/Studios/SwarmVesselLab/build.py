#!/usr/bin/env python3
"""Build the Swarm Vessel Lab into one self-contained page.

    python3 Docs/Studios/SwarmVesselLab/build.py          # writes Docs/Studios/SwarmVesselLab.html
    python3 Docs/Studios/SwarmVesselLab/build.py --check  # exits 1 if the committed page is stale

The page is src/shell.html with the five script files concatenated, in order, into ONE strict-mode IIFE
(so the lab leaks nothing onto window except window.__lab). The order matters only for top-level code:
core (model) -> species (data) -> game (economy, AI, simStep) -> toys (data) -> ui (DOM, loop, hooks).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'src')
OUT = os.path.join(os.path.dirname(HERE), 'SwarmVesselLab.html')
ORDER = ['core.js', 'species.js', 'game.js', 'toys.js', 'ui.js']


def build():
    shell = open(os.path.join(SRC, 'shell.html'), encoding='utf-8').read()
    assert shell.count('/*__SCRIPT__*/') == 1, 'shell.html must carry exactly one /*__SCRIPT__*/ marker'
    parts = []
    for name in ORDER:
        parts.append('// ---- src/' + name + ' ----\n' + open(os.path.join(SRC, name), encoding='utf-8').read())
    script = "(() => {\n'use strict';\n" + '\n'.join(parts) + '\n})();'
    return shell.replace('/*__SCRIPT__*/', script)


def main():
    page = build()
    if '--check' in sys.argv:
        cur = open(OUT, encoding='utf-8').read() if os.path.exists(OUT) else ''
        if cur != page:
            print('SwarmVesselLab.html is stale: run python3 Docs/Studios/SwarmVesselLab/build.py')
            return 1
        print('SwarmVesselLab.html is up to date (%d bytes)' % len(page))
        return 0
    open(OUT, 'w', encoding='utf-8').write(page)
    print('wrote %s (%d bytes)' % (os.path.relpath(OUT), len(page)))
    return 0


if __name__ == '__main__':
    sys.exit(main())

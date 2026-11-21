#!/usr/bin/env python3
"""Builds the FTUE Creator artifact page from the game's own sources.

  python3 Docs/Activation/FTUECreator/build.py          # writes out/ftue-creator.html
  python3 Docs/Activation/FTUECreator/build.py --check  # round-trip test only

It reads the enums the Quest Graph uses (GameModes, InputEvents, HapticType, UserActionType) from
Assets/_Scripts, seeds the page with the shipped MainQuest from Assets/FTUE/DataContainer, and proves
every shipped phase survives parse -> write -> parse field for field before it writes the page.
Republish with the Artifact tool to https://claude.ai/artifact/UMruaarAt2D2UY85aM93rU."""
import json, os, re, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
ENUM_SRC = {'GameModes': 'Assets/_Scripts/Data/Enums/GameModes.cs', 'InputEvents': 'Assets/_Scripts/Data/Enums/InputEvents.cs',
            'HapticType': 'Assets/_Scripts/Controller/IO/HapticController.cs', 'UserActionType': 'Assets/_Scripts/Data/Enums/UserActionType.cs'}

def enums():
    out = {}
    for k, f in ENUM_SRC.items():
        t = open(os.path.join(REPO, f), encoding='utf-8').read()
        body = re.search(r'enum\s+' + k + r'\b[^{]*\{(.*?)\}', t, re.S).group(1)
        body = re.sub(r'//.*', '', body); body = re.sub(r'/\*.*?\*/', '', body, flags=re.S); body = re.sub(r'\[[^\]]*\]', '', body)
        items, v = [], -1
        for p in (x.strip() for x in body.split(',')):
            if not p: continue
            n, val = ([y.strip() for y in p.split('=', 1)] + [None])[:2]
            v = int(val, 0) if val is not None else v + 1
            items.append([n, v])
        out[k] = items
    return out

def main():
    os.makedirs(os.path.join(HERE, 'out'), exist_ok=True)
    core = open(os.path.join(HERE, 'core.src.js'), encoding='utf-8').read().replace('__ENUMS__', json.dumps(enums()))
    core_path = os.path.join(HERE, 'out', 'core.js')
    open(core_path, 'w', encoding='utf-8').write(core)
    r = subprocess.run(['node', os.path.join(HERE, 'roundtrip.js'), core_path, REPO], capture_output=True, text=True)
    sys.stdout.write(r.stdout); sys.stderr.write(r.stderr)
    if r.returncode: sys.exit('round trip FAILED')
    if '--check' in sys.argv: return
    seed = open(os.path.join(HERE, 'out', 'seed.json'), encoding='utf-8').read()
    head = subprocess.run(['git', '-C', REPO, 'log', '-1', '--format=%h, %cs', '--', 'Assets/FTUE/DataContainer'], capture_output=True, text=True).stdout.strip()
    page = open(os.path.join(HERE, 'template.html'), encoding='utf-8').read()
    page = page.replace('__SIMCSS__', open(os.path.join(HERE, 'sim.css'), encoding='utf-8').read()).replace('__SIM__', open(os.path.join(HERE, 'sim.js'), encoding='utf-8').read())
    page = page.replace('__CORE__', core).replace('__SEED__', seed.replace('</', '<\\/')).replace('__SEEDDATE__', 'commit ' + head)
    open(os.path.join(HERE, 'out', 'ftue-creator.html'), 'w', encoding='utf-8').write(page)
    print('wrote out/ftue-creator.html')

main()

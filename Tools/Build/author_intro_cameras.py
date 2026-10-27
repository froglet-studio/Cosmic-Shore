#!/usr/bin/env python3
"""Author Resources/IntroCameraLibrary.asset: every mode's pre-game intro camera.

The intros were designed and approved in the Intro Cam Lab artifact
(https://claude.ai/artifact/Py9YqiPqdUZQsPfmNU7N1f, source Docs/IntroCameras/intro-cam-lab.html).
Here each one is restated in ANCHORS (the cell centre, the pilot, rivals, crystals, race gates,
the biggest arena geometry) instead of the artifact's stand-in coordinates, so it plays in any
arena of its FAMILY. A match picks one intro from its mode's family at random
(IntroCameraLibrarySO.TryPick) and shows its own mode's words over it.

Distances in Arena space are artifact units (a 240-unit cell); the game scales them by the real
cell's radius. LocalUnits / Units are world units (vessel-sized moves).

    python3 Tools/Build/author_intro_cameras.py           # write the asset
    python3 Tools/Build/author_intro_cameras.py --check   # validate + diff against disk, write nothing
    python3 Tools/Build/author_intro_cameras.py --self-test

--check fails when a mode on the Arcade or Arena list has no family or no words, when a mode is
in two families, when a shot is malformed, or when the asset on disk differs from what this
script would write. Docs/IntroCameras/README.md.
"""
import argparse
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SCRIPT_GUID = 'f5bfc470371c4cebb5230024612ef57a'   # IntroCameraLibrarySO.cs.meta
ASSET = 'Assets/Resources/IntroCameraLibrary.asset'
ASSET_GUID = '0b7e3c5a9d2f4e61a8c47f19d6e2b305'
GAME_LISTS = ['Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset',
              'Assets/_SO_Assets/Games/GameLists/ArenaGames.asset']

ANCHOR = {'Center': 0, 'Player': 1, 'Rival': 2, 'Teammate': 3, 'Pilots': 4, 'Crystal': 5, 'Gate': 6, 'Landmark': 7}
SPACE = {'arena': 0, 'units': 1, 'local': 2, 'localunits': 3}
KIND = {'move': 0, 'orbit': 1, 'path': 2, 'vertigo': 3, 'cuts': 4, 'hold': 5}
EASE = {'smooth': 0, 'linear': 1, 'inOut': 2, 'out': 3, 'in': 4, 'expoOut': 5, 'expoIn': 6, 'back': 7}
PATH = {'gates': 0, 'crystals': 1, 'ring': 2}
SLOT = {'title': 0, 'tagline': 1, 'objective': 2}


def game_modes():
    src = open(os.path.join(ROOT, 'Assets/_Scripts/Data/Enums/GameModes.cs')).read()
    body = src[src.index('enum GameModes'):]
    return {m.group(1): int(m.group(2)) for m in re.finditer(r'^\s*(\w+)\s*=\s*(\d+)\s*,', body, re.M)}


GM = game_modes()

# ---------------------------------------------------------------- points

def P(anchor, index=0, off=(0, 0, 0), space='arena'):
    return {'anchor': ANCHOR[anchor], 'index': index, 'offset': tuple(off), 'space': SPACE[space]}

def C(x=0, y=0, z=0): return P('Center', 0, (x, y, z))
def PL(x=0, y=0, z=0): return P('Player', 0, (x, y, z), 'localunits')
def RV(i=0, off=(0, 0, 0), space='arena'): return P('Rival', i, off, space)
def PI(i=-1, off=(0, 0, 0), space='arena'): return P('Pilots', i, off, space)
def CR(i=0, off=(0, 0, 0)): return P('Crystal', i, off)
def G(i=0, off=(0, 0, 0), space='local'): return P('Gate', i, off, space)
def LM(i=0, off=(0, 0, 0)): return P('Landmark', i, off)

ZERO = C()

# ---------------------------------------------------------------- shots

def shot(kind, name, dur, ease='smooth', **kw):
    s = {
        'name': name, 'kind': KIND[kind], 'duration': dur, 'ease': EASE[ease],
        'from': ZERO, 'to': ZERO, 'lookFrom': ZERO, 'lookTo': ZERO,
        'orbitCenter': ZERO, 'radius0': 0, 'radius1': 0, 'height0': 0, 'height1': 0, 'angle0': 0, 'angle1': 0,
        'path': 0, 'pathStart': 0, 'pathEnd': 0, 'pathOffset': (0, 0, 0), 'pathAhead': 0.03, 'pathLookUp': 0.6,
        'bank': 0, 'ringRadius': 150, 'ringHeight': 0,
        'vertigoTarget': ZERO, 'vertigoDirection': (0, 0, -1), 'distance0': 0, 'distance1': 0,
        'cutAnchor': 0, 'cutCount': 5, 'cutDistance': 30, 'cutPush': 6, 'cutZoom': 4,
        'distanceSpace': 0, 'fov0': 60, 'fov1': 60, 'roll0': 0, 'roll1': 0, 'spin': 0,
        'fovWaveAmplitude': 0, 'fovWaveFrequency': 0, 'shake': 0, 'shakeDecay': 0,
        'cues': [], 'flashes': [],
    }
    for k, v in kw.items():
        if k == 'f':
            s['fov0'], s['fov1'] = (v, v) if not isinstance(v, tuple) else v
        elif k == 'cue':
            s['cues'] = [{'slot': SLOT[c[0]], 'at': c[1], 'duration': c[2]} for c in v]
        elif k == 'flash':
            s['flashes'] = [{'at': a, 'duration': d} for a, d in v]
        elif k == 'units':
            s['distanceSpace'] = SPACE['units'] if v else 0
        else:
            assert k in s, f'unknown shot field {k}'
            s[k] = v
    return s

def move(name, dur, frm, to, look_from, look_to=None, ease='smooth', **kw):
    return shot('move', name, dur, ease, **{'from': frm, 'to': to, 'lookFrom': look_from, 'lookTo': look_to or look_from}, **kw)

def orbit(name, dur, center, r, h, a, ease='smooth', **kw):
    r0, r1 = r if isinstance(r, tuple) else (r, r)
    h0, h1 = h if isinstance(h, tuple) else (h, h)
    return shot('orbit', name, dur, ease, orbitCenter=center, radius0=r0, radius1=r1, height0=h0, height1=h1,
                angle0=a[0], angle1=a[1], **kw)

def path(name, dur, source, u, ease='smooth', off=(0, 0, 0), ahead=0.03, **kw):
    return shot('path', name, dur, ease, path=PATH[source], pathStart=u[0], pathEnd=u[1], pathOffset=off,
                pathAhead=ahead, **kw)

def vertigo(name, dur, target, direction, d, f0, ease='smooth', **kw):
    return shot('vertigo', name, dur, ease, vertigoTarget=target, vertigoDirection=direction,
                distance0=d[0], distance1=d[1], f=f0, **kw)

def cuts(name, dur, anchor, count, dist, push, zoom, f0, **kw):
    return shot('cuts', name, dur, 'linear', cutAnchor=ANCHOR[anchor], cutCount=count, cutDistance=dist,
                cutPush=push, cutZoom=zoom, f=f0, **kw)

def seq(name, mode, *shots):
    return {'name': name, 'designedFor': GM[mode], 'shots': list(shots)}

TITLE = lambda at, d=1.8: ('title', at, d)
TAG = lambda at, d=1.8: ('tagline', at, d)
OBJ = lambda at=0.2, d=1.6: ('objective', at, d)

# ---------------------------------------------------------------- the intros, by family

CRYSTAL_FIELDS = [
    seq('Trail skim', 'SkimRace',
        path('Skim the arena', 2.8, 'ring', (0.05, 0.25), 'inOut', off=(0, 3, 0), ahead=0.05, f=(76, 98), bank=1,
             ringRadius=150, ringHeight=-10, cue=[TITLE(0.45, 1.9)]),
        move('Rise to the prize', 2.2, C(-10, 0, 150), C(-40, 70, 100), C(-40, 0, 150), CR(0), 'inOut', f=(92, 52),
             cue=[TAG(0.3)]),
        vertigo('Crystal vertigo', 1.8, CR(0), (0.35, 0.25, -1), (34, 125), 74, 'inOut', cue=[OBJ()]),
        move('Grid pan', 1.8, PI(-1, (58, 6, 18)), PI(-1, (-46, 5, 18)), PI(-1, (18, 0, 0)), PI(-1, (-18, 0, 0)), f=42)),
    seq('Standoff', 'Joust',
        move('Standoff', 2.3, PI(-1, (150, 6, -26)), PI(-1, (150, 6, 26)), PI(-1, (0, 2, -8)), PI(-1, (0, 2, 8)),
             f=(34, 30), cue=[TITLE(0.3)]),
        orbit('Lance to lance', 2.4, PI(-1), (34, 30), (3, 6), (90, 270), 'inOut', f=72, cue=[TAG(0.4, 1.7)]),
        move('Over the shoulder', 1.7, PL(5, 4, -14), PL(3, 3.4, 18), RV(0), RV(0), 'inOut', f=(52, 30), cue=[OBJ()])),
    seq('Crystal roll call', 'Scurry',
        cuts('Roll call', 3.3, 'Crystal', 6, 30, 10, 6, 50, cue=[TITLE(0.2, 1.6), TAG(1.9, 1.4)]),
        move('All of them', 2.3, C(0, 60, 0.1), C(0, 330, 0.1), C(), C(), 'inOut', f=(70, 58), roll0=0, roll1=40,
             cue=[OBJ()]),
        move('Drop to the line', 1.6, C(0, 330, 0.1), PI(-1, (40, 30, -40)), C(), PI(-1), 'inOut', f=(58, 55))),
    seq('Eye of the storm', 'Maelstrom',
        orbit('Into the storm', 3.8, C(), (230, 50), (170, 12), (0, 300), 'inOut', f=(62, 74),
              cue=[TITLE(0.5, 2.0), TAG(2.3, 1.4)]),
        vertigo('The eye', 1.8, C(), (0.2, 1, 0.25), (45, 140), 70, 'inOut', cue=[OBJ()]),
        orbit('Lineup', 1.6, PI(-1), 70, 8, (200, 260), f=58)),
]

GATE_RACES = [
    seq('Flat out', 'Headlong',
        path('Flat out', 3.2, 'gates', (0.06, 0.6), 'linear', off=(0, 2.4, 0), ahead=0.025, f=(88, 100), bank=1.3,
             cue=[TITLE(0.6)]),
        orbit('The loop', 2.3, C(), (200, 250), (90, 150), (30, 90), 'inOut', cue=[TAG(0.3, 1.9)]),
        move('Start gate', 1.5, G(0, (0, 25, 40)), G(0, (0, 4, 8)), G(0), PI(-1), 'inOut', f=(60, 55), cue=[OBJ()])),
    seq('Rev to the limit', 'Redline',
        path('Ramp up', 3.2, 'gates', (0.02, 0.42), 'in', off=(0, 3, 0), f=(55, 112), bank=1,
             cue=[TITLE(3.05, 1.9)], flash=[(3.05, 0.45)]),
        orbit('Corner study', 2.1, G(4), 70, 22, (20, 95), f=55, cue=[TAG(0.2)]),
        move('Grid', 1.5, PI(-1, (40, 30, 0)), PI(-1, (15, 8, -10)), PI(-1), G(0), 'inOut', f=55, cue=[OBJ()])),
    seq('Thread the needle', 'Switchback',
        path('Gate to gate', 3.4, 'gates', (0.0, 0.5), 'inOut', ahead=0.035, f=(80, 86), bank=1.6, spin=35,
             cue=[TITLE(0.5)]),
        move('Course reveal', 2.2, G(5, (0, 10, 0), 'arena'), C(-40, 170, -280), G(5), C(), 'inOut', f=(80, 58),
             cue=[TAG(0.3)]),
        move('First gate', 1.5, C(-40, 170, -280), G(0, (0, 8, -60)), C(), G(0), 'inOut', f=(58, 56), cue=[OBJ()])),
    seq('Station approach', 'Breakwater',
        move('Approach', 2.7, G(1, (0, 6, -110)), G(1, (0, 0, 14)), G(1), G(1, (0, 0, 60)), 'in', f=(50, 84),
             cue=[TITLE(0.3, 1.9)], flash=[(2.45, 0.35)]),
        path('Chain of walls', 2.4, 'gates', (0.12, 0.42), 'inOut', off=(0, 46, 0), ahead=0.06, pathLookUp=-0.4, f=66,
             cue=[TAG(0.25, 1.9)]),
        move('Start station', 1.5, G(0, (0, 60, 0)), G(0, (0, 10, -30)), G(0), G(0), 'inOut', f=(60, 56), cue=[OBJ()])),
    seq('Knot and strand', 'Skein',
        orbit('The knot', 2.8, C(), (300, 260), (40, 120), (0, 100), 'inOut', f=56, cue=[TITLE(0.4, 1.9)]),
        path('Ride a strand', 2.8, 'gates', (0.05, 0.25), 'linear', off=(0, 2.5, 0), ahead=0.02, f=(80, 92), bank=1,
             cue=[TAG(0.3, 1.9)]),
        move('Start collar', 1.5, G(0, (60, 40, 40)), G(0, (0, 14, -60)), G(0), G(0), 'inOut', f=(60, 56), cue=[OBJ()])),
    seq('Migration', 'Waystation',
        path('The coil', 2.5, 'gates', (0.0, 0.3), 'inOut', ahead=0.06, f=74, bank=1, cue=[TITLE(0.4)]),
        move('Fold', 1.0, G(4), G(6, (0, -12, -30)), G(6), G(7), 'expoOut', f=(118, 66), flash=[(0, 0.5)]),
        path('Next coil', 1.9, 'gates', (0.4, 0.6), 'out', ahead=0.06, f=70, bank=1, cue=[TAG(0.1)]),
        move('Migration map', 1.8, G(8, (0, 20, 0), 'arena'), C(0, 330, -60), G(8), C(), 'inOut', f=(70, 60),
             cue=[OBJ()])),
    seq('Bomb launch', 'GrizzlyTime',
        move('Fuse', 1.1, PL(3, 4, -14), PL(2, 3.5, -11), PL(0, 1, 20), PL(0, 1, 20), 'in', f=(55, 50), shake=0.15,
             cue=[TITLE(0.15, 1.6)]),
        path('Blast launch', 2.4, 'gates', (0.0, 0.3), 'expoOut', off=(0, 3, 0), f=(105, 70), bank=1, shake=0.8,
             shakeDecay=1, flash=[(0, 0.45)], cue=[TAG(0.7, 1.6)]),
        orbit('The circuit', 2.0, C(), 210, 110, (-40, 20), 'inOut', f=58, cue=[OBJ()])),
    seq('Gravity well', 'Slingshot',
        orbit('Gravity well', 2.4, G(2), (46, 18), (10, 3), (0, 330), 'in', f=(60, 78), cue=[TITLE(0.4, 1.7)]),
        path('Sling out', 2.1, 'gates', (0.22, 0.47), 'expoOut', off=(0, 3, 0), f=(96, 76), bank=1.2,
             flash=[(0, 0.3)], cue=[TAG(0.4, 1.7)]),
        orbit('The circuit', 1.9, C(), 220, 120, (120, 170), 'inOut', f=58, cue=[OBJ()])),
    seq('Bent light', 'Warpline',
        path('Long leg', 3.1, 'gates', (0.02, 0.27), 'linear', off=(0, 3, 0), f=72, fovWaveAmplitude=22,
             fovWaveFrequency=2.3, bank=0.8, cue=[TITLE(0.5, 1.9)]),
        orbit('Dipole', 2.1, G(3), 90, 26, (-40, 50), f=56, cue=[TAG(0.2, 1.9)]),
        move('Grid', 1.4, PI(-1, (40, 40, 0)), PI(-1, (10, 9, -12)), PI(-1), G(0), 'inOut', f=(58, 55), cue=[OBJ()])),
    seq('Sling release', 'ThresherTime',
        orbit('Wind the chain', 2.4, P('Player'), (44, 16), (6, 2), (0, 520), 'in', f=(58, 80), units=True,
              cue=[TITLE(0.3, 1.7)]),
        path('Release', 2.0, 'gates', (0.05, 0.3), 'expoOut', off=(0, 3, 0), f=(100, 72), bank=1,
             flash=[(0, 0.3)], cue=[TAG(0.4, 1.5)]),
        orbit('The circuit', 1.9, C(), 220, 120, (60, 110), 'inOut', f=58, cue=[OBJ()])),
    seq('Kite line', 'WrasseRun',
        move('Tether', 2.1, PL(6, 2, 4), PL(5, 19, -18), P('Player'), PL(0, 16, -26), 'inOut', f=(52, 60),
             cue=[TITLE(0.3)]),
        path('Through the lit ring', 2.6, 'gates', (0.0, 0.3), 'inOut', ahead=0.04, f=78, bank=1, cue=[TAG(0.3, 1.9)]),
        orbit('The circuit', 1.7, C(), 220, 110, (0, 50), 'inOut', f=58, cue=[OBJ()])),
    seq('Grid walk', 'Regatta',
        move('Grid walk', 3.1, PI(6, (16, 3, 0), 'localunits'), PI(0, (16, 3, 10), 'localunits'), PI(6), PI(0), f=46,
             cue=[TITLE(0.5, 1.9)]),
        path('Braided rails', 2.4, 'gates', (0.1, 0.38), 'inOut', off=(0, 16, 0), ahead=0.04, pathLookUp=-0.2, f=70,
             bank=0.7, cue=[TAG(0.2)]),
        orbit('Gold gate', 1.7, C(), 230, 110, (0, 40), 'inOut', f=58, cue=[OBJ()])),
]

BONEYARD = [
    seq('Wreck recon', 'DogFight',
        move('Through the wreck', 2.7, LM(0, (0, 0, -60)), LM(0, (0, 0, 60)), LM(0, (0, 0, -20)), LM(0, (0, 0, 100)),
             f=72, shake=0.45, cue=[TITLE(0.6, 1.9)]),
        move('Canyon run', 2.0, LM(0, (0, 0, 60)), LM(1, (0, 10, -40)), LM(1), RV(0), 'inOut', f=82,
             cue=[TAG(0.3, 1.6)]),
        move('Rivals', 1.7, RV(0, (-110, 10, -90)), RV(0, (-70, 4, -50)), RV(0), RV(0), 'inOut', f=(30, 20), shake=0.2,
             cue=[OBJ()])),
    seq('Target lock', 'Salvo',
        cuts('Target lock', 3.0, 'Landmark', 5, 90, 10, 8, 45, cue=[TITLE(0.2)]),
        move('Missile cam', 1.4, C(140, 150, 170), LM(2, (10, 8, 14)), LM(2), LM(2), 'expoIn', f=(60, 100),
             flash=[(1.3, 0.5)], cue=[TAG(0.1, 1.2)]),
        move('Debris drift', 1.9, LM(2, (20, 14, 30)), LM(2, (70, 30, 90)), LM(2), LM(2), 'out', f=62, shake=0.5,
             shakeDecay=1, cue=[OBJ()])),
    seq('Overhead pass', 'Dustup',
        orbit('Overhead', 2.4, RV(0), 4, (56, 42), (0, 120), f=50, units=True, cue=[TITLE(0.4)]),
        move('Dust fall', 1.9, RV(0, (4, 40, 6), 'units'), RV(0, (14, -34, 10), 'units'), RV(0), RV(0), 'inOut',
             f=(50, 64), cue=[TAG(0.3, 1.5)]),
        orbit('The Boneyard', 1.9, C(), 240, 90, (150, 200), 'inOut', f=56, cue=[OBJ()])),
    seq('Roll call', 'Broadside',
        cuts('Roll call', 3.85, 'Pilots', 7, 15, 3, 4, 40, units=True),
        orbit('The Boneyard', 2.3, C(), (150, 260), (30, 100), (180, 220), 'inOut', f=60, flash=[(0, 0.35)],
              cue=[TITLE(0.1, 1.9)]),
        orbit('Points', 1.4, C(), 260, 100, (220, 232), 'linear', f=60, cue=[TAG(0, 1.3), OBJ(0.1, 1.2)])),
]

FORESTS = [
    seq('Canopy breach', 'Rampage',
        path('Through the thorns', 2.8, 'ring', (0.15, 0.32), 'linear', ahead=0.02, f=82, ringRadius=192,
             cue=[TITLE(0.5, 1.9)]),
        move('Canopy breach', 2.2, C(-81, 0, 174), C(-80, 90, 90), C(-95, 0, 165), CR(0), 'inOut', f=(82, 50),
             cue=[TAG(0.3)]),
        vertigo('Lonely crystal', 1.7, CR(0), (-0.7, 0.6, 0.7), (30, 110), 72, 'inOut', cue=[OBJ()])),
    seq('High noon', 'Bends',
        move('High noon', 2.3, PI(-1, (-10, -26, -170)), PI(-1, (8, -24, -150)), PI(-1, (0, -4, 0)), PI(-1, (0, -2, 0)),
             f=(30, 28), cue=[TITLE(0.3, 1.9)]),
        vertigo('The draw', 1.6, RV(0), (-1, 0.15, -0.25), (70, 22), 24, 'expoOut', flash=[(0, 0.2)],
                cue=[TAG(0.4, 1.2)]),
        orbit('Circle', 2.0, PI(-1), 90, 14, (200, 300), f=52, cue=[OBJ()])),
    seq('Storm front', 'Sirocco',
        path('Storm front', 3.0, 'ring', (0.5, 0.7), 'linear', off=(0, 4, 0), ahead=0.06, f=78, shake=0.35,
             ringRadius=150, ringHeight=-40, cue=[TITLE(0.5, 1.9)]),
        move('Erosion', 1.9, LM(0, (-70, 50, -70)), LM(0, (-30, 60, -30)), LM(0), LM(0), 'inOut', f=(54, 40),
             shake=0.2, cue=[TAG(0.3, 1.5)]),
        orbit('The storm', 1.8, C(), 250, 130, (220, 250), 'inOut', f=58, cue=[OBJ()])),
    seq('Be the ball', 'WreckingBall',
        path('Be the ball', 2.7, 'ring', (0.0, 0.3), 'linear', ahead=0.05, f=88, spin=360, ringRadius=100,
             ringHeight=-20, cue=[TITLE(0.5, 1.9)]),
        move('Over the wall', 2.3, C(0, -20, 100), C(150, 120, 240), C(-50, -20, 90), C(), 'inOut', f=(80, 56),
             cue=[TAG(0.3)]),
        orbit('Court', 1.7, C(), 290, 120, (32, 60), f=56, cue=[OBJ()])),
    seq('Ground zero', 'GrizzlyCharge',
        move('Fall', 2.2, C(8, 230, 10), C(4, 30, 6), C(0, -20, 0), C(0, -20, 0), 'expoIn', f=(46, 84),
             cue=[TITLE(0.2)]),
        move('Shockwave', 2.0, C(4, 30, 6), C(60, 120, 170), C(0, -20, 0), C(0, -20, 0), 'expoOut', f=(84, 60),
             shake=1.4, shakeDecay=1, flash=[(0, 0.6)], cue=[TAG(0.5, 1.5)]),
        orbit('Crater', 1.8, C(0, -20, 0), 200, 110, (20, 55), f=58, cue=[OBJ()])),
    seq('Blade pass', 'Cleave',
        path('Blade edge', 3.0, 'ring', (0.55, 0.78), 'linear', off=(0, 3, 0), f=84, bank=0.6, ringRadius=125,
             ringHeight=30, cue=[TITLE(0.5, 1.9)]),
        move('Pull out', 2.4, C(-80, 36, -98), C(140, 150, 240), C(-60, 30, -110), C(), 'inOut', f=(84, 56),
             cue=[TAG(0.3, 1.9)]),
        orbit('Orbit', 1.8, C(), 290, 150, (30, 65), f=56, cue=[OBJ()])),
]

CAGES = [
    seq('Breach', 'WildlifeLiberation',
        move('Outer swarm', 2.1, C(14, 22, 300), C(8, 14, 180), C(), C(), 'inOut', f=(58, 64), cue=[TITLE(0.3)]),
        move('Middle room', 1.8, C(8, 14, 165), C(4, 10, 96), C(), C(), 'inOut', f=(64, 62), cue=[TAG(0.1, 1.7)]),
        move('The core', 1.6, C(4, 10, 80), C(2, 6, 40), C(), C(), 'out', f=(62, 46), shake=0.25, flash=[(0, 0.35)]),
        move('Snap out', 1.3, C(2, 6, 40), PI(-1, (0, 30, -60)), C(), PI(-1), 'expoOut', f=(46, 64),
             cue=[OBJ(0.3, 1.4)])),
    seq('Crab along the cage', 'Undertow',
        orbit('Crab', 2.7, C(), 132, 6, (-30, 20), f=60, cue=[TITLE(0.4, 1.9)]),
        orbit('Plate dash', 0.9, C(), 132, 6, (20, 62), 'expoOut', f=72, flash=[(0, 0.25)], cue=[TAG(0.1, 1.6)]),
        orbit('The cages', 2.2, C(), (150, 320), (10, 130), (62, 110), 'inOut', f=60, cue=[OBJ(0.5, 1.6)])),
]

COURTS = [
    seq('Hoop tour', 'ScarabScramble',
        path('Hoop tour', 3.6, 'crystals', (0.0, 0.85), 'back', ahead=0.035, f=78, bank=1.4,
             cue=[TITLE(0.5, 1.9), TAG(2.0, 1.5)]),
        orbit('The court', 2.0, C(), (150, 220), (90, 160), (40, 90), 'inOut', f=60, cue=[OBJ()]),
        move('Drop in', 1.4, C(120, 150, 150), PI(-1, (20, 10, 30)), C(), PI(-1), 'back', f=(60, 56))),
    seq('Toll posts', 'Tollway',
        cuts('Toll posts', 3.2, 'Landmark', 5, 40, 8, 5, 58, cue=[TITLE(0.3, 1.7)]),
        move('Monument', 2.1, LM(1, (30, -20, 30)), LM(1, (40, 40, 50)), LM(1), LM(1), 'inOut', f=(56, 52),
             cue=[TAG(0.2)]),
        orbit('The court', 1.8, C(), 240, 110, (30, 60), f=58, cue=[OBJ()])),
    seq('Grind line', 'Hijack',
        path('Grind', 2.8, 'ring', (0.05, 0.24), 'linear', off=(0, 4, 0), ahead=0.025, f=86, ringRadius=150,
             cue=[TITLE(0.5)]),
        move('Burr', 1.2, LM(0, (-30, 8, -30)), LM(0, (20, -4, 20)), LM(0), LM(0, (60, -10, 60)), 'in', f=(70, 90),
             flash=[(0.55, 0.3)]),
        orbit('Three rails', 2.3, C(), 360, 100, (0, 70), 'inOut', f=54, cue=[TAG(0.2), OBJ(0.8, 1.4)])),
    seq('Reef bloom', 'Bloomrush',
        path('Reef drift', 3.0, 'ring', (0.6, 0.8), off=(0, 4, 0), ahead=0.08, f=64, shake=0.15, ringRadius=140,
             ringHeight=-80, cue=[TITLE(0.6, 1.9)]),
        vertigo('Bloom', 2.0, CR(0), (0.6, 0.3, -1), (30, 120), 72, 'inOut', flash=[(1.2, 0.5)], cue=[TAG(0.2)]),
        orbit('The reef', 1.9, C(0, -80, 0), 250, 110, (180, 215), f=58, cue=[OBJ()])),
    seq('Blank canvas', 'Tapestry',
        move('Empty sky', 1.6, C(-150, 30, -150), C(-120, 30, -110), C(), C(), f=55, cue=[TITLE(0.2)]),
        path('The brush', 3.0, 'ring', (0.0, 0.35), off=(6, 10, -20), ahead=0.08, f=64, ringRadius=110,
             cue=[TAG(0.5, 1.9)]),
        move('Territory', 2.2, C(60, 30, 100), C(0, 360, 0.1), C(30, 0, 60), C(), 'inOut', f=(64, 60), roll0=0,
             roll1=29, cue=[OBJ()])),
    seq('Kickoff broadcast', 'AstroLeague',
        move('Broadcast wide', 2.8, C(-40, 90, -260), C(30, 60, -60), C(), C(0, 0, 60), 'inOut', f=(58, 62),
             cue=[TITLE(0.5, 1.9)]),
        move('Your side', 1.6, PL(-40, 4, 22), PL(40, 4, 22), P('Player'), P('Player'), f=44, cue=[TAG(0.1, 2.6)]),
        move('Their side', 1.4, RV(0, (40, 4, 22), 'localunits'), RV(0, (-40, 4, 22), 'localunits'), RV(0), RV(0),
             f=44),
        vertigo('Kickoff', 1.7, C(), (-0.4, 0.4, -1), (26, 110), 72, 'inOut', cue=[OBJ()])),
    seq('Nucleus pulse', 'BroodRush',
        move('Inside the nucleus', 2.6, C(0, 4, 14), C(10, 26, 130), C(), C(), 'out', f=(80, 60), flash=[(0, 0.5)],
             cue=[TITLE(0.6)]),
        orbit('Claim ring', 2.4, C(), 150, 40, (0, 110), f=58, cue=[TAG(0.2, 2.0)]),
        vertigo('Pulse', 1.5, C(), (0.5, 0.5, 1), (70, 170), 64, 'inOut', cue=[OBJ()])),
]

FAMILIES = [
    ('Crystal fields', ['SkimRace', 'Joust', 'Scurry', 'Maelstrom'], CRYSTAL_FIELDS),
    ('Gate races', ['Headlong', 'Redline', 'Switchback', 'Breakwater', 'Skein', 'Waystation', 'GrizzlyTime',
                    'Slingshot', 'Warpline', 'BurrowRun', 'ThresherTime', 'WrasseRun', 'Regatta'], GATE_RACES),
    ('Boneyard', ['DogFight', 'Salvo', 'Dustup', 'Broadside'], BONEYARD),
    ('Forests and demolition', ['Rampage', 'Bends', 'Sirocco', 'WreckingBall', 'GrizzlyCharge', 'Cleave'], FORESTS),
    ('Cages', ['WildlifeLiberation', 'Undertow'], CAGES),
    ('Courts and cells', ['ScarabScramble', 'Tollway', 'Hijack', 'Bloomrush', 'Tapestry', 'AstroLeague', 'BroodRush'],
     COURTS),
]

# The words each mode shows over whichever intro plays: title, tagline, objective, lower-third line.
WORDS = [
    ('SkimRace', 'Skim Race', 'Skim trails for speed. Beat everyone to the crystal.', 'First to the crystal', 'Squirrel'),
    ('Joust', 'Joust', 'Fly straight at them. Hit first.', 'First domain to the joust target', 'Squirrel'),
    ('Scurry', 'Scurry', 'Every crystal. As fast as you can.', 'Capture every crystal', 'Squirrel'),
    ('Maelstrom', 'Maelstrom', 'Skim Race, Joust, Scurry: one tournament.', 'Race to the crown', 'Every domain'),
    ('Headlong', 'Headlong', 'Thread the corner flat out, or pay six seconds.', 'Lead runner home wins', 'Rhino'),
    ('Redline', 'Redline', 'Both triggers buried is four times cruise.', 'Lead runner home wins', 'Manta'),
    ('Switchback', 'Switchback', 'Every gate in order. Every gate twisted.', 'Lead runner home wins', 'Dolphin'),
    ('Breakwater', 'Breakwater', 'Shoot a door. Saw it open. Or thread the eye.', 'Fourteen stations, two laps', 'Sparrow'),
    ('Skein', 'Skein', 'Change strands. The fast line runs inside.', 'Thread the last ring', 'Urchin'),
    ('Waystation', 'Waystation', 'Thread the last ring on the line you want to leave on.', 'Lead runner home wins', 'Butterfly'),
    ('GrizzlyTime', 'Grizzly Time', 'Your speed is your bombs.', 'Against the clock', 'Grizzly'),
    ('Slingshot', 'Slingshot', 'You cruise slow and turn wide. Your poles do the steering.', 'Lead runner home wins', 'Stoat'),
    ('Warpline', 'Warpline', 'Bend the tunnel. Dig the line. Ride it out.', 'Time is everything', 'Meerkat'),
    ('BurrowRun', 'Burrow Run', 'Eight rings. Dig to every one.', 'Lead runner home wins', 'Meerkat'),
    ('ThresherTime', 'Thresher Time', 'Plant the ball. Orbit it. Let go.', 'Against the clock', 'Thresher'),
    ('WrasseRun', 'Wrasse Run', 'The Grouper flies the plane. The Wrasse takes the ring.', 'Only the Wrasse counts', 'Wrasse and Grouper'),
    ('Regatta', 'Regatta', 'Every hull. One circuit. Three laps.', 'Lead runner home wins', 'Every hull'),
    ('DogFight', 'Dog Fight', 'Guns score 1. Missiles score 50.', 'First domain to the target', 'Sparrow'),
    ('Salvo', 'Salvo', 'This time the Boneyard is the target.', 'First domain to the prism target', 'Sparrow'),
    ('Dustup', 'Dustup', 'No guns. Fly over them.', 'First domain to the target', 'Butterfly'),
    ('Broadside', 'Broadside', 'Every hull that can fight, each with its own weapon.', 'Every hit priced by the weapon that landed it', 'Every fighting hull'),
    ('Rampage', 'Rampage', 'One crystal. A whole forest in the way.', 'First domain to the target', 'Dolphin'),
    ('Bends', 'The Bends', 'No guns. Only the blast.', 'First domain to the bend target', 'Dolphin'),
    ('Sirocco', 'Sirocco', 'Your dust is your blade.', 'First domain to the prism target', 'Butterfly'),
    ('WreckingBall', 'Wrecking Ball', 'Every bright crystal is a ball. Plow the forest.', 'First domain to the target', 'Scarab'),
    ('GrizzlyCharge', 'Grizzly Charge', 'A cell full of prisms. Nuke them.', 'Most prisms destroyed', 'Grizzly'),
    ('Cleave', 'Cleave', 'Four arenas. One blade. The arena is the score.', 'Cut the prism target first', 'Rhino'),
    ('WildlifeLiberation', 'Wildlife Liberation', 'Three cages. Wildlife everywhere.', 'First pilot to the kill target', 'Sparrow'),
    ('Undertow', 'Undertow', 'Dash beside them. Strip them bare.', 'Bends and kills: first domain to the target', 'Scarab'),
    ('ScarabScramble', 'Scarab Scramble', 'Fly through a bright crystal. It is your ball now.', 'Roll it through any hoop', 'Scarab'),
    ('Tollway', 'Tollway', 'Plant a ring. Every ball through it pays you.', 'First domain to the toll target', 'Scarab'),
    ('Hijack', 'Hijack', 'Grind your colour. Steal theirs.', 'Steal the prism target', 'Urchin'),
    ('Bloomrush', 'Bloomrush', 'Skim to arm. Tag to plant. Beat the fuse.', '120 seconds: biggest bloom wins', 'Manta'),
    ('Tapestry', 'Tapestry', 'Paint the sky. Raid what they paint.', 'Most mass standing wins', 'Butterfly'),
    ('AstroLeague', 'Astro League', 'Hypersea soccer. Played with a sword.', 'Kickoff', 'Rhino'),
    ('BroodRush', 'Brood Rush', 'Hold the nucleus. Every brood born in your colour scores.', 'A brood every 30 seconds', 'Every domain'),
]

# ---------------------------------------------------------------- validation

def listed_modes():
    """Mode ids on the Arcade and Arena lists, read from the shipped cards."""
    guid_to_path = {}
    games = os.path.join(ROOT, 'Assets/_SO_Assets/Games')
    for dp, _, fs in os.walk(games):
        for f in fs:
            if f.endswith('.asset.meta'):
                m = re.search(r'^guid: (\w+)', open(os.path.join(dp, f)).read(), re.M)
                if m:
                    guid_to_path[m.group(1)] = os.path.join(dp, f[:-5])
    out = {}
    for lst in GAME_LISTS:
        for g in re.findall(r'guid: (\w+), type: 2', open(os.path.join(ROOT, lst)).read()):
            card = guid_to_path.get(g)
            if not card:
                continue
            m = re.search(r'^  Mode: (\d+)', open(card).read(), re.M)
            if m:
                out[int(m.group(1))] = os.path.basename(card)
    return out


def validate():
    errors = []
    seen = {}
    for fname, modes, seqs in FAMILIES:
        if not seqs:
            errors.append(f'family {fname} has no intros')
        ids = []
        for m in modes:
            if m not in GM:
                errors.append(f'family {fname}: {m} is not a GameModes member')
                continue
            if m in seen:
                errors.append(f'{m} is in two families ({seen[m]}, {fname})')
            seen[m] = fname
            ids.append(GM[m])
        for s in seqs:
            if s['designedFor'] not in ids:
                errors.append(f'intro {s["name"]} is in {fname} but was designed for a mode outside it')
            if not s['shots']:
                errors.append(f'intro {s["name"]} has no shots')
            for sh in s['shots']:
                if sh['duration'] <= 0:
                    errors.append(f'{s["name"]} / {sh["name"]}: duration must be positive')
                for c in sh['cues'] + sh['flashes']:
                    if c['at'] < 0 or c['at'] >= sh['duration']:
                        errors.append(f'{s["name"]} / {sh["name"]}: a cue or flash starts outside the shot')
    worded = {w[0] for w in WORDS}
    for w in WORDS:
        if w[0] not in GM:
            errors.append(f'words for unknown mode {w[0]}')
    for mid, card in sorted(listed_modes().items()):
        name = next((k for k, v in GM.items() if v == mid), str(mid))
        if name not in seen:
            errors.append(f'{name} ({card}) is on a game list but in no intro family')
        if name not in worded:
            errors.append(f'{name} ({card}) is on a game list but has no intro words')
    return errors

# ---------------------------------------------------------------- YAML

def num(v):
    v = float(v)
    if v == int(v) and abs(v) < 1e9:
        return str(int(v))
    return format(v, '.7g')

def vec(v):
    return '{x: %s, y: %s, z: %s}' % tuple(num(c) for c in v)

SAFE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 .,!?()'\-]*$")

def text(s):
    if s == '':
        return ''
    if SAFE.match(s) and not s.endswith(' '):
        return s
    return "'" + s.replace("'", "''") + "'"

def point(lines, ind, key, p):
    lines.append(f'{ind}{key}:')
    lines.append(f'{ind}  anchor: {p["anchor"]}')
    lines.append(f'{ind}  index: {p["index"]}')
    lines.append(f'{ind}  offset: {vec(p["offset"])}')
    lines.append(f'{ind}  space: {p["space"]}')

SHOT_ORDER = ['name', 'kind', 'duration', 'ease', 'from', 'to', 'lookFrom', 'lookTo', 'orbitCenter', 'radius0',
              'radius1', 'height0', 'height1', 'angle0', 'angle1', 'path', 'pathStart', 'pathEnd', 'pathOffset',
              'pathAhead', 'pathLookUp', 'bank', 'ringRadius', 'ringHeight', 'vertigoTarget', 'vertigoDirection',
              'distance0', 'distance1', 'cutAnchor', 'cutCount', 'cutDistance', 'cutPush', 'cutZoom',
              'distanceSpace', 'fov0', 'fov1', 'roll0', 'roll1', 'spin', 'fovWaveAmplitude', 'fovWaveFrequency',
              'shake', 'shakeDecay', 'cues', 'flashes']
POINTS = {'from', 'to', 'lookFrom', 'lookTo', 'orbitCenter', 'vertigoTarget'}
VECS = {'pathOffset', 'vertigoDirection'}

def shot_yaml(lines, ind, s):
    first = True
    for k in SHOT_ORDER:
        lead = f'{ind[:-2]}- ' if first else ind
        first = False
        v = s[k]
        if k in POINTS:
            point(lines, lead, k, v)   # never first: a shot opens on its name
        elif k in VECS:
            lines.append(f'{lead}{k}: {vec(v)}')
        elif k == 'name':
            lines.append(f'{lead}name: {text(v)}')
        elif k == 'cues':
            if not v:
                lines.append(f'{lead}cues: []')
            else:
                lines.append(f'{lead}cues:')
                for c in v:
                    lines.append(f'{ind}- slot: {c["slot"]}')
                    lines.append(f'{ind}  at: {num(c["at"])}')
                    lines.append(f'{ind}  duration: {num(c["duration"])}')
        elif k == 'flashes':
            if not v:
                lines.append(f'{lead}flashes: []')
            else:
                lines.append(f'{lead}flashes:')
                for c in v:
                    lines.append(f'{ind}- at: {num(c["at"])}')
                    lines.append(f'{ind}  duration: {num(c["duration"])}')
        else:
            lines.append(f'{lead}{k}: {num(v)}')


def build():
    L = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:',
         '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
         '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
         f'  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}', '  m_Name: IntroCameraLibrary',
         '  m_EditorClassIdentifier: ', '  families:']
    for fname, modes, seqs in FAMILIES:
        L.append(f'  - name: {text(fname)}')
        L.append('    modes:')
        for m in modes:
            L.append(f'    - {GM[m]}')
        L.append('    sequences:')
        for s in seqs:
            L.append(f'    - name: {text(s["name"])}')
            L.append(f'      designedFor: {s["designedFor"]}')
            L.append('      shots:')
            for sh in s['shots']:
                shot_yaml(L, '        ', sh)
    L.append('  modeWords:')
    for mode, title, tagline, objective, pilots in WORDS:
        L.append(f'  - mode: {GM[mode]}')
        L.append(f'    title: {text(title)}')
        L.append(f'    tagline: {text(tagline)}')
        L.append(f'    objective: {text(objective)}')
        L.append(f'    pilots: {text(pilots)}')
    L += ['  referenceRadius: 240', '  scaleMultiplier: 1', '  letterbox: 0.085', '  captionFont: {fileID: 0}',
          '  titleFontSize: 110', '  taglineFontSize: 32', '  objectiveFontSize: 24', '  lowerTitleFontSize: 44',
          '  lowerLineFontSize: 18']
    return '\n'.join(L) + '\n'


META = f"""fileFormatVersion: 2
guid: {ASSET_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def self_test():
    # Negative controls: each must be caught by validate().
    global FAMILIES, WORDS
    saved = (FAMILIES, WORDS)
    try:
        FAMILIES = saved[0] + [('Dupe', ['Joust'], [CRYSTAL_FIELDS[1]])]
        assert any('two families' in e for e in validate()), 'a mode in two families was not caught'
        FAMILIES = [(n, [m for m in ms if m != 'Hijack'], s) for n, ms, s in saved[0]]
        FAMILIES = [(n, ms, [x for x in s if x['designedFor'] != GM['Hijack']]) for n, ms, s in FAMILIES]
        assert any('Hijack' in e and 'no intro family' in e for e in validate()), 'a listed mode with no family was not caught'
        FAMILIES = saved[0]
        WORDS = [w for w in saved[1] if w[0] != 'Cleave']
        assert any('Cleave' in e and 'no intro words' in e for e in validate()), 'missing words were not caught'
        WORDS = saved[1]
        bad = shot('hold', 'bad', 1.0, cue=[TITLE(1.5)])
        FAMILIES = saved[0] + [('Bad', [], [{'name': 'x', 'designedFor': -1, 'shots': [bad]}])]
        assert any('outside the shot' in e for e in validate()), 'a cue outside its shot was not caught'
    finally:
        FAMILIES, WORDS = saved
    assert not validate(), validate()
    assert text("It's") == "It's" and text('a: b') == "'a: b'" and num(0.5) == '0.5' and num(2.0) == '2'
    print('self-test passed: 4 negative controls fired, the shipped tables validate')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    if a.self_test:
        self_test()
        return 0
    errors = validate()
    if errors:
        print('\n'.join('ERROR: ' + e for e in errors))
        return 1
    out = build()
    path, meta = os.path.join(ROOT, ASSET), os.path.join(ROOT, ASSET + '.meta')
    n_intros = sum(len(s) for _, _, s in FAMILIES)
    if a.check:
        drift = []
        if not os.path.exists(path) or open(path).read() != out:
            drift.append(ASSET)
        if not os.path.exists(meta) or open(meta).read() != META:
            drift.append(ASSET + '.meta')
        if drift:
            print('DRIFT: ' + ', '.join(drift) + ' differ from what author_intro_cameras.py writes')
            return 1
        print(f'ok: {len(FAMILIES)} families, {n_intros} intros, {len(WORDS)} modes worded, asset matches')
        return 0
    open(path, 'w').write(out)
    open(meta, 'w').write(META)
    print(f'wrote {ASSET}: {len(FAMILIES)} families, {n_intros} intros, {len(WORDS)} modes worded')
    return 0


if __name__ == '__main__':
    sys.exit(main())

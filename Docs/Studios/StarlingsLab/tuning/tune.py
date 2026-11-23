# python3 tune.py <iters> <nseeds>: scales each Time card's power until the look-ahead picks use every card about equally
import json, subprocess, os, sys, collections, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import pool
S = pool.S; REPO = pool.REPO
cards = json.load(open(os.path.join(REPO, 'Docs', 'Studios', 'SwarmCardVessel', 'cards.json')))['cards']
C = {c['id']: c for c in cards if c['element'] == 'Time'}
# the knob of each card: (stat index, 'up' = bigger is stronger | 'down' = smaller is stronger)
KNOB = {'time-dragonfly': [(0, 'up')], 'time-quickening': [(0, 'up')], 'time-slipstream': [(0, 'up')], 'time-skim-rush': [(0, 'up')],
  'time-agility': [(0, 'up')], 'time-hummingbird': [(0, 'up'), (1, 'up')], 'time-lead-time': [(0, 'up')], 'time-skip-ahead': [(0, 'down')],
  'time-blink-step': [(0, 'up')], 'time-out-of-phase': [(0, 'up')], 'time-slingshot': [(0, 'up')], 'time-save-point': [(0, 'up')],
  'time-fast-forward': [(0, 'up')], 'time-metronome': [(1, 'up')], 'time-synchrony': [(1, 'up')], 'time-mayfly-couriers': [(1, 'up')],
  'time-rewind': [(0, 'up')], 'time-echo-swarm': [(0, 'up')], 'time-stopwatch': [(0, 'up')], 'time-hurry-up': [(0, 'up')]}
CAP = {'time-blink-step': 95, 'time-rewind': 95, 'time-stopwatch': 95, 'time-hurry-up': 60}   # chance / percent stats stay below these at L15
def tune_of(k):
    t = {}
    for cid, knobs in KNOB.items():
        t[cid] = {}
        for i, d in knobs:
            s = C[cid]['stats'][i]; a, b = s['a'], s['b']
            if d == 'up':
                a, b = a * k[cid], b * k[cid]
                if cid in CAP and a + 14 * b > CAP[cid]: f = CAP[cid] / (a + 14 * b); a, b = a * f, b * f
            else: a = a / k[cid]
            t[cid][i] = [round(a, 4), round(b, 4)]
    return t
iters, n = int(sys.argv[1]), int(sys.argv[2])
kf = f'{S}/k.json'; k = json.load(open(kf)) if os.path.exists(kf) else {c: 1.0 for c in KNOB}
LAST = {}
for it in range(iters):
    json.dump(tune_of(k), open(f'{S}/tune.json', 'w'))
    out = subprocess.run(['python3', os.path.join(pool.HERE, 'run.py'), f'it{it}', str(n), f'{S}/tune.json'], capture_output=True, text=True).stdout
    print(out, flush=True)
    rows = json.load(open(f'{S}/it{it}_rows.json')); use = collections.Counter(); tot = 0
    for r in rows:
        if r['mode'] == 'look':
            for c in r['picks']: use[c] += 1; tot += 1
    tgt = 1 / 20
    for c in KNOB:
        now = use[c] / max(1, tot); sh = max((now + LAST.get(c, now)) / 2, 0.004); LAST[c] = now   # smoothed over two rounds
        k[c] = max(0.3, min(25.0, k[c] * (tgt / sh) ** 0.45))
    json.dump(k, open(kf, 'w'), indent=1)
    print('  k: ' + '  '.join(f'{c[5:]}:{k[c]:.2f}' for c in KNOB), flush=True)

# python3 solohz.py <hazard> <nenv> <level> [tune.json]: each card alone in races with only that hazard
import json, sys, os, statistics, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import pool
hz, n, L = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]); tune = json.load(open(sys.argv[4])) if len(sys.argv) > 4 else {}
H = {k: k == hz for k in ['gunners', 'drift', 'keys', 'reefs']}
cards = [c['id'] for c in json.load(open(pool.CARDS))['cards'] if c['element'] == 'Time']
jobs = [{'id': [c, e], 'opts': {'seed': 7000 + e, 'intensity': 1 + e % 4, 'hazards': H, 'give': ({c: L} if c != 'none' else {})}} for c in ['none'] + cards for e in range(n)]
rows = pool.run('solohz', jobs, tune)
T = collections.defaultdict(dict); lost = collections.defaultdict(list)
for r in rows: T[r['id'][0]][r['id'][1]] = r['time']; lost[r['id'][0]].append(r['lost'])
print(hz, 'no cards', round(statistics.mean(T['none'].values())), 'lost', round(statistics.mean(lost['none'])))
out = sorted((statistics.mean(T[c][e] / T['none'][e] for e in range(n)), c) for c in cards)
print('  '.join(f'{c[5:]} x{m:.2f}/{statistics.mean(lost[c]):.0f}' for m, c in out))

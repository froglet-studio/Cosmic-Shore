# python3 solo.py <nenv> <level> [tune.json]: each Time card alone at one level vs no cards, on the same races
import json, sys, os, statistics, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import pool
n, L = int(sys.argv[1]), int(sys.argv[2]); tune = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else {}
cards = [c['id'] for c in json.load(open(pool.CARDS))['cards'] if c['element'] == 'Time']
jobs = [{'id': [c, e], 'opts': {'seed': 7000 + e, 'intensity': 1 + e % 4, 'give': ({c: L} if c != 'none' else {})}} for c in ['none'] + cards for e in range(n)]
rows = pool.run('solo', jobs, tune)
T = collections.defaultdict(dict); hz = {}
for r in rows: T[r['id'][0]][r['id'][1]] = r['time']; hz[r['id'][1]] = r['hz']
out = []
for c in cards:
    rat = [T[c][e] / T['none'][e] for e in range(n)]
    out.append((statistics.mean(rat), c, min(rat), max(rat)))
for m, c, lo, hi in sorted(out): print(f'{c[5:]:16s} x{m:.2f}  best x{lo:.2f}  worst x{hi:.2f}')

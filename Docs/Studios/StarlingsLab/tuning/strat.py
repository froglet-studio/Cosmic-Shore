# python3 strat.py <nfresh>: fit a strategy (card priority from the race's course + hazards) from the look-ahead picks of the
# r5 rounds, then race random / strategy / look-ahead on fresh seeds
import json, sys, os, collections, statistics, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import pool
S = pool.S; tune = json.load(open(sys.argv[2] if len(sys.argv) > 2 else f'{S}/final_tune.json')); n = int(sys.argv[1])
rows = [r for it in range(int(sys.argv[4]) if len(sys.argv) > 4 else 5) for r in json.load(open(f'{S}/{sys.argv[3] if len(sys.argv) > 3 else "r5it"}{it}_rows.json')) if r['mode'] == 'look']
cards = [c['id'] for c in json.load(open(pool.CARDS))['cards'] if c['element'] == 'Time']
HZ = ['gunners', 'drift', 'keys', 'reefs']
def feats(course, hz, inten): return ['c:' + course] + ['h:' + h for h in HZ if hz[h]] + ['i:%d' % inten]
base = collections.Counter(); tot = 0; fc = collections.defaultdict(collections.Counter); ft = collections.Counter()
for r in rows:
    f = feats(r['course'], r['hz'], r['intensity'])
    for c in r['picks']:
        base[c] += 1; tot += 1
        for x in f: fc[x][c] += 1; ft[x] += 1
def score(c, f):   # log-lift of the card under each feature, added up (naive Bayes)
    s = math.log((base[c] + 1) / (tot + 20))
    for x in f: s += math.log(((fc[x][c] + 1) / (ft[x] + 20)) / ((base[c] + 1) / (tot + 20)))
    return s
# the fresh races' environments: race once with no cards to read course + hazards
envs = pool.run('env', [{'id': e, 'opts': {'seed': 9000 + e, 'intensity': 1 + e % 4, 'give': {}}} for e in range(n)], tune)
env = {r['id']: r for r in envs}
jobs = []
for e in range(n):
    o = {'seed': 9000 + e, 'intensity': 1 + e % 4}
    pr = sorted(cards, key=lambda c: -score(c, feats(env[e]['course'], env[e]['hz'], o['intensity'])))
    jobs += [{'id': ['random', e], 'opts': o}, {'id': ['strategy', e], 'opts': dict(o, priority=pr)}, {'id': ['look', e], 'look': True, 'opts': o}]
    # the fixed favourite: one priority for every race (no reading of the race)
    jobs.append({'id': ['fixed', e], 'opts': dict(o, priority=sorted(cards, key=lambda c: -base[c]))})
res = pool.run('strat', jobs, tune)
T = collections.defaultdict(dict)
for r in res: T[r['id'][0]][r['id'][1]] = r['time']
for k in ['random', 'fixed', 'strategy', 'look']:
    print(f'{k:9s} mean {statistics.mean(T[k].values()):6.1f}s  vs random x{statistics.mean(T[k][e] / T["random"][e] for e in range(n)):.3f}  beats random in {sum(T[k][e] < T["random"][e] for e in range(n))}/{n}')
for f in ['c:open', 'c:bends', 'c:hairpins', 'h:gunners', 'h:drift', 'h:keys', 'h:reefs']:
    lift = sorted(cards, key=lambda c: -(fc[f][c] + 1) / (ft[f] + 20) / ((base[c] + 1) / (tot + 20)))
    print(f'  {f:11s} favours ' + ', '.join(c[5:] for c in lift[:4]))

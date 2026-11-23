# python3 combos.py <nenv> [tune.json]: every pair of Time cards (each at L10) raced on the same nenv random races
import json, sys, os, itertools, collections, statistics
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import pool
S = pool.S; nenv = int(sys.argv[1]); tune = json.load(open(sys.argv[2])) if len(sys.argv) > 2 else {}
cards = ['time-dragonfly','time-quickening','time-slipstream','time-skim-rush','time-agility','time-hummingbird','time-lead-time','time-skip-ahead','time-blink-step','time-out-of-phase','time-slingshot','time-save-point','time-fast-forward','time-metronome','time-synchrony','time-mayfly-couriers','time-rewind','time-echo-swarm','time-stopwatch','time-hurry-up']
envs = [{'seed': 5000 + e, 'intensity': 1 + e % 4} for e in range(nenv)]
jobs = []
for e, env in enumerate(envs):
    jobs.append({'id': ['none', e], 'opts': dict(env, give={})})
    for a, b in itertools.combinations(cards, 2): jobs.append({'id': [a + '+' + b, e], 'opts': dict(env, give={a: 10, b: 10})})
rows = pool.run('combo', jobs, tune)
T = collections.defaultdict(dict); envinfo = {}
for r in rows: T[r['id'][0]][r['id'][1]] = r['time']; envinfo[r['id'][1]] = (r['course'], [h for h, v in r['hz'].items() if v], r['opts']['intensity'])
json.dump({'T': T, 'env': envinfo}, open(f'{S}/combos.json', 'w'))
pairs = [k for k in T if k != 'none']
mean = {k: statistics.mean(T[k].values()) for k in T}
srt = sorted(pairs, key=lambda k: mean[k])
print(f'no cards {mean["none"]:.1f}s; pairs best {mean[srt[0]]:.1f}s ({srt[0]}), median {mean[srt[len(srt)//2]]:.1f}s, worst {mean[srt[-1]]:.1f}s ({srt[-1]})')
win = collections.Counter(); top = collections.Counter()
for e in envinfo:
    best = min(pairs, key=lambda k: T[k][e])
    for c in best.split('+'): win[c] += 1
    for k in sorted(pairs, key=lambda k: T[k][e])[:5]:
        for c in k.split('+'): top[c] += 1
print('cards in each race\'s winning pair (of', len(envinfo), 'races):', '  '.join(f'{c[5:]}:{win[c]}' for c in sorted(cards, key=lambda c: -win[c])))
print('cards in each race\'s top-5 pairs:', '  '.join(f'{c[5:]}:{top[c]}' for c in sorted(cards, key=lambda c: -top[c])))
print('top 10 pairs overall:', '; '.join(f'{k.replace("time-","")} {mean[k]:.0f}' for k in srt[:10]))
print('bottom 5 pairs:', '; '.join(f'{k.replace("time-","")} {mean[k]:.0f}' for k in srt[-5:]))

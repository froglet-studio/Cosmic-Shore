# python3 run.py <tag> <nseeds> [tune.json]  -> runs random + look-ahead races on I1-I4 over 4 workers, prints a report
import json, subprocess, sys, os, collections, statistics, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import pool
S = pool.S; tag = sys.argv[1]; n = int(sys.argv[2])
tune = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else {}
jobs = [{'seed': 1000 + i, 'intensity': 1 + i % 4, 'mode': m} for i in range(n) for m in ('look', 'random')]
W = 4; outs = []; procs = []
for w in range(W):
    jf = f'{S}/{tag}_jobs{w}.json'; of = f'{S}/{tag}_out{w}.jsonl'
    json.dump({'tune': tune, 'jobs': jobs[w::W]}, open(jf, 'w')); open(of, 'w').close(); outs.append(of)
    procs.append(subprocess.Popen(['node', os.path.join(pool.HERE, 'worker.js'), jf, of]))
t0 = time.time()
for p in procs: p.wait()
rows = [json.loads(l) for f in outs for l in open(f) if l.strip()]
errs = [r for r in rows if 'errors' in r]; rows = [r for r in rows if 'errors' not in r]
json.dump(rows, open(f'{S}/{tag}_rows.json', 'w'))
look = {r['seed']: r for r in rows if r['mode'] == 'look'}; rnd = {r['seed']: r for r in rows if r['mode'] == 'random'}
both = [s for s in look if s in rnd]
gain = [rnd[s]['time'] / look[s]['time'] for s in both]
print(f'{tag}: {len(both)} races in {time.time()-t0:.0f}s, errors {errs[:1]}')
print(f'  random mean {statistics.mean(rnd[s]["time"] for s in both):.1f}s  look-ahead mean {statistics.mean(look[s]["time"] for s in both):.1f}s  random/look {statistics.mean(gain):.3f}')
use = collections.Counter(); total = 0
for s in both:
    for c in look[s]['picks']: use[c] += 1; total += 1
first = collections.Counter(look[s]['picks'][0] for s in both)
cards = ['time-dragonfly','time-quickening','time-slipstream','time-skim-rush','time-agility','time-hummingbird','time-lead-time','time-skip-ahead','time-blink-step','time-out-of-phase','time-slingshot','time-save-point','time-fast-forward','time-metronome','time-synchrony','time-mayfly-couriers','time-rewind','time-echo-swarm','time-stopwatch','time-hurry-up']
print('  look-ahead picks (share of choices), card: share')
print('  ' + '  '.join(f'{c[5:]}:{100*use[c]/max(1,total):.0f}%' for c in sorted(cards, key=lambda c: -use[c])))

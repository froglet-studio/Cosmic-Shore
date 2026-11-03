import json, subprocess, os
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
CARDS = os.path.join(REPO, 'Docs', 'Studios', 'SwarmCardVessel', 'cards.json')
S = os.environ.get('STARLINGS_TUNING_OUT', '/tmp/starlings_tuning')   # job and result files (never in the repo)
os.makedirs(S, exist_ok=True)
def run(tag, jobs, tune, W=4):
    import random as _r; jobs = list(jobs); _r.Random(1).shuffle(jobs)   # spread the slow look-ahead jobs over the workers
    outs, procs = [], []
    for w in range(W):
        jf, of = f'{S}/{tag}_j{w}.json', f'{S}/{tag}_o{w}.jsonl'
        json.dump({'tune': tune, 'jobs': jobs[w::W]}, open(jf, 'w')); open(of, 'w').close(); outs.append(of)
        procs.append(subprocess.Popen(['node', os.path.join(HERE, 'worker2.js'), jf, of]))
    for p in procs: p.wait()
    rows = [json.loads(l) for f in outs for l in open(f) if l.strip()]
    errs = [r for r in rows if 'errors' in r]
    if errs: print('ERRORS', errs[:2])
    return [r for r in rows if 'errors' not in r]

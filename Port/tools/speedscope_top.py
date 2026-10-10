#!/usr/bin/env python3
"""Rank a dotnet-trace profile by self and inclusive time per function (ROADMAP C7).

    dotnet-trace collect -p PID --profile dotnet-sampled-thread-time --duration 00:00:25 \
        -o trace.nettrace --format speedscope
    python3 Port/tools/speedscope_top.py trace.speedscope.json --list          # threads by time
    python3 Port/tools/speedscope_top.py trace.speedscope.json --thread "(11871)" --under GameLoop.Tick
    python3 Port/tools/speedscope_top.py trace.speedscope.json --thread "(11871)" --children CoroutineRunner.RunFrame
    python3 Port/tools/speedscope_top.py trace.speedscope.json --thread "(11871)" --callers PollGCWorker

Reads the speedscope JSON dotnet-trace writes (its "evented" open/close form, or a sampled
profile), attributes each slice of time to the frame on top of the stack (the CPU_TIME /
UNMANAGED_CODE_TIME pseudo-leaves count for their parent) and to every frame on the stack once.
--under keeps only the time while a frame containing the substring is on the stack (GameLoop.Tick
is the game's frame; ParityRun.AfterTick is the parity harness). --callers / --children name the
neighbours of a frame. The loop thread is the one with the lowest id in --list; a
dotnet-sampled-thread-time profile measures WALL time, so under load every figure inflates alike
and only the shares are comparable between two traces. This is what ranked the C7 table in
docs/ROADMAP.md.
"""
import json, sys, collections, argparse
ap = argparse.ArgumentParser()
ap.add_argument('file'); ap.add_argument('--thread', default=None); ap.add_argument('--top', type=int, default=40)
ap.add_argument('--callers', default=None); ap.add_argument('--children', default=None)
ap.add_argument('--list', action='store_true', help='list threads by total time')
ap.add_argument('--under', default=None, help='only count time while a frame containing this substring is on the stack')
a = ap.parse_args()
d = json.load(open(a.file))
names = [f.get('name','?') for f in d['shared']['frames']]

def walk(p):
    """yield (stack list of frame indices, weight) slices"""
    if p['type'] == 'sampled':
        for s, w in zip(p['samples'], p['weights']):
            yield s, w
    else:
        stack = []; last = None
        for ev in p['events']:
            t = ev['at']
            if last is not None and stack and t > last:
                yield list(stack), t - last
            if ev['type'] == 'O': stack.append(ev['frame'])
            else:
                if stack: stack.pop()
            last = t

if a.list:
    rows = []
    for p in d['profiles']:
        tot = sum(w for _, w in walk(p))
        rows.append((tot, p.get('name')))
    for tot, n in sorted(rows, reverse=True)[:10]: print(f"{tot:12.1f} {p.get('unit','')}  {n}")
    sys.exit(0)

self_t = collections.Counter(); incl_t = collections.Counter(); total = 0.0
callers = collections.Counter(); children = collections.Counter()
profiles = [p for p in d['profiles'] if a.thread is None or a.thread in p.get('name','')]
for p in profiles:
    for s, w in walk(p):
        if a.under is not None and not any(a.under in names[i] for i in s): continue
        total += w
        if s:
            k=len(s)-1
            while k>0 and names[s[k]] in ('CPU_TIME','UNMANAGED_CODE_TIME'): k-=1
            self_t[names[s[k]]] += w
        seen = set()
        for k, i in enumerate(s):
            n = names[i]
            if n not in seen: incl_t[n] += w; seen.add(n)
            if a.callers and a.callers in n and k > 0: callers[names[s[k-1]]] += w
            if a.children and a.children in n:
                children[names[s[k+1]] if k + 1 < len(s) else '<self>'] += w
unit = profiles[0]['unit'] if profiles else '?'
print(f"profiles: {[p.get('name') for p in profiles]}  total {total:.0f} {unit}")
def show(title, c):
    print(f"\n== {title}")
    for n, w in c.most_common(a.top): print(f"{w/total*100:6.2f}%  {w:10.1f}  {n[:160]}")
show("self time", self_t); show("inclusive time", incl_t)
if a.callers: show(f"callers of {a.callers}", callers)
if a.children: show(f"children of {a.children}", children)

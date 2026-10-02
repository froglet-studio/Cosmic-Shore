"""Quick scorecard: python quick.py <module> <Class> [minutes] [json params]"""
import sys, json, time, importlib
sys.path.insert(0, '.')
from harness import scorecard
mod, cls = sys.argv[1], sys.argv[2]
minutes = float(sys.argv[3]) if len(sys.argv) > 3 else 1.5
params = json.loads(sys.argv[4]) if len(sys.argv) > 4 else {}
t = time.time()
card, runs = scorecard(f'{mod}:{cls}', params, seeds=(7, 23, 41), minutes=minutes)
print(mod, json.dumps(card))
print("wall", round(time.time() - t, 1), "s")

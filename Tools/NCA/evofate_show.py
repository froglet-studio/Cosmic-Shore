"""Print the curve table from runs/evofate/*.jsonl."""
import json, sys
for p in sys.argv[1:]:
    for l in open(p):
        r = json.loads(l); f = r['feel']; m = f['mean']; s7 = r['screen']['7']; s23 = r['screen']['23']
        print(json.dumps(r['cfg']), 'own7', [round(v, 1) for v in s7['own'].values()], f"ok7 {s7['own_ok']}+{s7['sw_ok']} ok23 {s23['own_ok']}+{s23['sw_ok']}",
              f"jr {m['jerk_rel']} osc {m['osc']} stuck {m['stuck']} pe {f['planar_excess']} coh {m['coherence']} jit {m['jitter']} band {f['in_band']}",
              [k for k, v in f['checks'].items() if not v])

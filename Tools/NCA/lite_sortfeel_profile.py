"""lite_sortfeel profile: where does a sortfeel step spend its time? cProfile over 200 steps of a grown
body (1 thread), per plan, aggregated by function. Usage: python lite_sortfeel_profile.py [spec]"""
import cProfile, pstats, sys, time, io
import torch
import swarm_eval as se
import swarm_nca as sn

def grown(model, kind, seed=7, steps=240):
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([sn.load_targets()[kind]], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    return sw, gen

def main():
    torch.set_num_threads(1)
    spec = sys.argv[1] if len(sys.argv) > 1 else "sortfeel:results/sortfeel/params.json"
    nsteps = int(sys.argv[2]) if len(sys.argv) > 2 else 200
    model = se.load_model(spec)
    pr = cProfile.Profile(); tot = 0.0; n = 0
    for k in sn.KINDS:
        sw, gen = grown(model, k)
        pr.enable(); t0 = time.perf_counter()
        for _ in range(nsteps):
            sw = model(sw, gen)
        tot += time.perf_counter() - t0; pr.disable(); n += nsteps
        print(f"{k}: live {int((sw.active & sw.hatched).sum())}")
    print(f"ms/step {1000*tot/n:.3f}")
    s = io.StringIO(); pstats.Stats(pr, stream=s).sort_stats("tottime").print_stats(25); print(s.getvalue())

if __name__ == "__main__":
    main()

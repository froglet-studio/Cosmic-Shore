"""All-micro (every region expanded, no pilots, no structures) trajectories for quick design of micro rules."""
import sys, json
from multiprocessing import Pool
import os; sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from living_cell.cell import Cell
from living_cell import rounds

def job(a):
    name, cfg, seed, minutes, mode = a
    c = Cell(seed=seed, cfg=dict(cfg, force_hot=(mode == "micro"), traps=False, fortress=False, physarum=False), pilots=())
    rows = []
    for i in range(int(minutes * 600)):
        c.step(0.1)
        if i % 1200 == 0:
            cs = c.census(); rows.append(f"{int(c.w.t/60)}m g{cs['grazer']} l{cs['locust']} p{cs['pack']} t{cs['thief']} u{cs['lurker']} F{int(c.biomass()['flora']/1000)}k")
    return name, seed, mode, rows, c.guilds["pack"].births, c.guilds["pack"].prey_kills

if __name__ == "__main__":
    V = eval(sys.argv[1], vars(rounds))
    minutes = float(sys.argv[2]); mode = sys.argv[3] if len(sys.argv) > 3 else "micro"
    jobs = [(n, cfg, s, minutes, mode) for n, cfg in V.items() for s in (1, 2)]
    with Pool(4) as p:
        for name, seed, mode, rows, pb, pk in p.imap(job, jobs):
            print(name, seed, mode, "pack births", pb, "kills", pk, " | ".join(rows), flush=True)

"""Print markdown tables from bench.json and results/*.json for DISCOVERIES.md."""
import json, os
HERE = os.path.dirname(__file__)


def bench_table():
    b = json.load(open(os.path.join(HERE, "bench.json")))
    out = ["| backend | N | k | ms/step | us/agent-step | biggest stages (ms) |", "|---|---|---|---|---|---|"]
    for r in b["rows"]:
        st = dict(r["stages_ms"])
        if "steer" in st and "context" in st:
            st["steer_terms"] = round(st.pop("steer") - st["context"], 3)
        top = sorted(st.items(), key=lambda kv: -kv[1])[:4]
        out.append(f"| {r['backend']} | {r['N']:,} | {r['k']} | {r['ms_step']:.1f} | {r['us_per_agent_step']:.2f} | "
                   + ", ".join(f"{k} {v:.1f}" for k, v in top) + " |")
    return "\n".join(out)


if __name__ == "__main__":
    print(bench_table())

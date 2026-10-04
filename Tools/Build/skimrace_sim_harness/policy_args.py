#!/usr/bin/env python3
"""Print one authored policy (Tools/Build/author_skimrace_ai_config.py POLICIES) as sim key=value args.

    bash Tools/Build/skimrace_sim_harness/run.sh eval 2 40 $(python3 Tools/Build/skimrace_sim_harness/policy_args.py SkimRaceAIConfig_I2) ph.Seats=2
"""
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("author", os.path.join(HERE, "..", "author_skimrace_ai_config.py"))
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
ov = mod.POLICIES[sys.argv[1]]
print(" ".join(f"{k}={int(v) if isinstance(v, bool) else v}" for k, v in ov.items() if k != "PolicyVersion"))

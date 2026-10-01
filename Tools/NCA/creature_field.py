"""The creature's reaction SHELL on field's slot bodies instead of evo's learned body (the recommendation
of results/creature/NOTE.md, tested). FieldSwarm's own predator response is switched off (its
`predators` list stays empty); the vessel is fed to the shell (`vessels`), which perturbs the field
body with the same elastic offset, temperaments, escort and tells as creature_model.CreatureRule.

    model = CreatureField()          # field params from results/field/params.json
"""
import json
import os
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402
import creature_model as cm  # noqa: E402


def field_cfg():
    cfg = json.load(open(os.path.join(HERE, "results", "field", "params.json")))["cfg"]
    cfg["vmax"] = tuple(cfg["vmax"])
    return fs.FieldCfg(**cfg)


class CreatureField(fs.FieldSwarm):
    stateless = False
    _reset_shell = cm.CreatureRule._reset
    _shell = cm.CreatureRule._shell

    def __init__(self, **shell):
        super().__init__(field_cfg())
        self.shell_cfg = dict(cm.DEFAULTS); self.shell_cfg["wounds"] = 0; self.shell_cfg.update(shell)
        self.vessels = []
        self.react = True
        self.shell = True
        self._st = None
        self._rng = torch.Generator().manual_seed(1234)

    def __call__(self, sw, gen=None, **kw):
        self.predators = []
        if not self.shell:
            return super().__call__(sw, gen, **kw)
        with torch.no_grad():
            self._reset_shell(sw)
            st = self._st
            st["off"][~sw.active] = 0
            st["startle"][~sw.active] = 0
            out = super().__call__(sw, gen, **kw)
            self._shell(out, gen)
            st["alive"] = (out.active & out.hatched).clone()
            st["last"] = out.pos.clone()
            st["t"] += 1
            return out

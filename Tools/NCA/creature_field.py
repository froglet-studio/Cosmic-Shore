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
        self.shell_cfg = dict(cm.DEFAULTS); self.shell_cfg.update(wounds=0, native=1, fear=4.0); self.shell_cfg.update(shell)
        self.vessels = []
        self.react = True
        self.shell = True
        self._st = None
        self._rng = torch.Generator().manual_seed(1234)
        self._fear_rng = np.random.default_rng(99)

    def _afraid(self, b):
        """Fear suppresses breeding: with probability min(1, fear * threat) a step lays / molts nothing (production
        gating - nothing is culled). fear=0 (default) is field exactly."""
        f = self.shell_cfg.get("fear", 0.0)
        if f <= 0 or self._st is None or b >= self._st["threat"].shape[0]:
            return False
        return float(self._fear_rng.random()) < min(1.0, f * float(self._st["threat"][b]))

    def _lay(self, sw, b, plan, perm, gen):
        if self._afraid(b):
            return
        return super()._lay(sw, b, plan, perm, gen)

    def _molt(self, sw, b, idx, plan, counts, gen):
        if self._afraid(b):
            return
        return super()._molt(sw, b, idx, plan, counts, gen)

    def __call__(self, sw, gen=None, **kw):
        # native=1: ALSO hand the ship to field's own predator response (which can calm the body's homing -
        # the one thing an external shell cannot do); native_only=1: field round 1 exactly, no shell
        nat = self.shell_cfg.get("native", 0) or self.shell_cfg.get("native_only", 0)
        self.predators = list(self.vessels) if (self.shell and self.react and nat) else []
        if not self.shell or self.shell_cfg.get("native_only", 0):
            return super().__call__(sw, gen, **kw)
        with torch.no_grad():
            self._reset_shell(sw)
            st = self._st
            st["off"][~sw.active] = 0
            st["startle"][~sw.active] = 0
            out = super().__call__(sw, gen, **kw)
            st["drift"] = torch.zeros(out.B, 3)
            self._shell(out, gen)
            for b in range(out.B):                       # translation the shell applied (escort) moves the body frame
                if b in self.mem and bool(st["drift"][b].abs().sum() > 0):
                    self.mem[b]["anchor"] = (self.mem[b]["anchor"] + st["drift"][b].numpy()).astype(np.float32)
            st["alive"] = (out.active & out.hatched).clone()
            st["last"] = out.pos.clone()
            st["t"] += 1
            return out

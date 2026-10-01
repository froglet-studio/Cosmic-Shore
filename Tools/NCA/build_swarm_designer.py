"""Build swarm_designer.html: the four elemental body plans, with swarm_model.js and the live palette inlined.

The page and swarm_target.py share swarm_model.js, so the designer shows exactly the training
targets. Publish the output as the "Elemental Swarm Body Plans" artifact.
"""
import json
import os

import numpy as np

import prism_render as pr

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    src = open(os.path.join(HERE, "swarm_designer.src.html"), encoding="utf-8").read()
    model = open(os.path.join(HERE, "swarm_model.js"), encoding="utf-8").read()
    pal = np.round(pr.palette_colours()[:, :3], 4).tolist()          # [domain][plain, danger, shield]
    out = src.replace("/*SWARM_MODEL*/", model).replace("/*PALETTE*/", json.dumps(pal))
    dst = os.path.join(HERE, "swarm_designer.html")
    open(dst, "w", encoding="utf-8").write(out)
    print("wrote", dst, len(out), "bytes")


if __name__ == "__main__":
    main()

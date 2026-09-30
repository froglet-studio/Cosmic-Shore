"""Build whale_designer.html: the designer page with whale_model.js and the live palette inlined.

The page and whale_target.py share whale_model.js, so what the designer shows is exactly the
training target. Publish the output as the "Prism Whale Designer" artifact.
"""
import json
import os

import numpy as np

import prism_render as pr

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    src = open(os.path.join(HERE, "whale_designer.src.html"), encoding="utf-8").read()
    model = open(os.path.join(HERE, "whale_model.js"), encoding="utf-8").read()
    pal = np.round(pr.palette_colours()[:, :3], 4).tolist()          # [domain][plain, danger, shield]
    out = src.replace("/*WHALE_MODEL*/", model).replace("/*PALETTE*/", json.dumps(pal))
    dst = os.path.join(HERE, "whale_designer.html")
    open(dst, "w", encoding="utf-8").write(out)
    print("wrote", dst, len(out), "bytes")


if __name__ == "__main__":
    main()

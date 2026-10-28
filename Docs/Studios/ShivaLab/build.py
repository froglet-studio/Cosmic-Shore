# Assemble the Shiva's Cell lab page: head.html + the CDN scripts + the game's many-headed serpent plans + sim.js + app.js.
#   python3 Docs/Studios/ShivaLab/build.py            # writes plans.json and shiva-lab.html here (both git-ignored)
# The plans are re-read from the shipped JSON every build, so the swarm never wears a body the game no longer ships.
import json, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
PLANS = os.path.join(ROOT, "Assets", "_SO_Assets", "Swarm Fauna", "Tandava")
CDN = ["https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js",
       "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js",
       "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/shaders/CopyShader.js",
       "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/shaders/LuminosityHighPassShader.js",
       "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/postprocessing/EffectComposer.js",
       "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/postprocessing/RenderPass.js",
       "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/postprocessing/ShaderPass.js",
       "https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/postprocessing/UnrealBloomPass.js"]


def read(name):
    with open(os.path.join(HERE, name)) as f:
        return f.read().rstrip("\n")


subprocess.run([sys.executable, os.path.join(HERE, "build_plans.py"), PLANS, os.path.join(HERE, "plans.json")], check=True)
with open(os.path.join(HERE, "plans.json")) as f:
    plans = json.dumps(json.load(f), separators=(",", ":"))
page = "\n".join([read("head.html"), ""] + [f'<script src="{u}"></script>' for u in CDN] +
                 [f"<script>window.PLAN_DATA = {plans};</script>", "<script>", read("sim.js"), "</script>",
                  "<script>", read("app.js"), "</script>", ""])
with open(os.path.join(HERE, "shiva-lab.html"), "w") as f:
    f.write(page)
print(len(CDN), "cdn scripts;", len(page) // 1024, "KB")

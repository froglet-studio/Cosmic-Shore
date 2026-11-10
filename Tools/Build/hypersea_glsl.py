#!/usr/bin/env python3
"""The SHIPPED HyperSea sky (Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader) as GLSL, for the sky bench.

Like Tools/Build/bake_static_skybox.py (which compiles the same CGINCLUDE block as C++), this transpiles rather than
transcribes: HLSL types and intrinsics become GLSL through a prelude of #defines and overloads, the shader's globals
become uniforms, and the material's values (linearized exactly as bake_static_skybox.material_inits does) are set on
them. Anything the shader starts using that this prelude does not know fails to compile, loudly.

    from hypersea_glsl import hypersea_source, hypersea_uniforms
    src = hypersea_source()        # GLSL defining  vec4 hyperSeaFrag(vec3 viewDir)
    vals = hypersea_uniforms()     # {"_DeepColor": (r,g,b,a), "_StarDensity": 80.0, ...}
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bake_static_skybox as bake  # noqa: E402

PRELUDE = r"""
#define half float
#define half2 vec2
#define half3 vec3
#define half4 vec4
#define float2 vec2
#define float3 vec3
#define float4 vec4
#define lerp mix
#define frac fract
float saturate(float x){ return clamp(x, 0.0, 1.0); }
vec2 saturate(vec2 x){ return clamp(x, 0.0, 1.0); }
vec3 saturate(vec3 x){ return clamp(x, 0.0, 1.0); }
vec4 saturate(vec4 x){ return clamp(x, 0.0, 1.0); }
float atan2(float y, float x){ return atan(y, x); }
uniform vec4 _Time;
"""


def hypersea_source() -> str:
    text = bake.read(bake.SHADER)
    m = re.search(r"^\s*CGINCLUDE\s*$(.*?)^\s*ENDCG\s*$", text, re.S | re.M)
    body = m.group(1)
    body = re.sub(r'#include\s+"UnityCG\.cginc"', "", body)
    body = re.sub(r"struct\s+(appdata|v2f)\s*\{.*?\};", "", body, flags=re.S)
    # globals -> uniforms
    body = re.sub(r"^(\s*)(half|float)(\d?)(\s+_\w+\s*;)", r"\1uniform \2\3\4", body, flags=re.M)
    # HLSL scalar-to-vector initialisation: "half3 x = 0;" -> "half3 x = half3(0);"
    body = re.sub(r"\b((?:half|float)[234])\s+(\w+)\s*=\s*(-?[\d.]+)\s*;", r"\1 \2 = \1(\3);", body)
    # "return 0;" inside a half3 function (HLSL widens the scalar): widen it per function
    def widen(m):
        ret, fn = m.group(1), m.group(0)
        return re.sub(r"return\s+(-?[\d.]+)\s*;", lambda r: f"return {ret}({r.group(1)});", fn) if ret[-1] in "234" else fn
    body = re.sub(r"\b((?:half|float)[234]?)\s+\w+\s*\([^)]*\)\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\}", widen, body)
    return PRELUDE + body


def hypersea_uniforms() -> dict:
    text, mat = bake.read(bake.SHADER), bake.read(bake.MATERIAL)
    out = {}
    for line in bake.material_inits(text, mat).splitlines():
        m = re.match(r"\s*(_\w+) = (.*);", line)
        if not m:
            continue
        expr = re.sub(r"^float4\(", "", m.group(2))
        nums = [float(x.rstrip("f")) for x in re.findall(r"-?[\d.]+(?:e-?\d+)?f?", expr)]
        out[m.group(1)] = tuple(nums) if len(nums) > 1 else nums[0]
    return out


if __name__ == "__main__":
    print(hypersea_source())

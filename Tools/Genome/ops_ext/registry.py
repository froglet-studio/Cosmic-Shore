"""Extension-op registry: how an ops_ext operator plugs into the core genome without forking it.

Every extension op is a normal `agenome.ops.Op` (same byte codec, same mutation, same SPEC table) plus ONE of:
  glsl_t(i, d, ins, T)       -> GLSL body of `vec3 t<i>(vec2 uv)`              (texture computed per pixel)
  bake_t(d, inputs, seed)    -> float32 [N, N, 3] array                       (texture baked on the CPU, werkkzeug
                                                                               style, then sampled as uB<i>)
  glsl_s(i, d, ins, SA, MA)  -> (sd_body, mt_body) for `sd<i>`, `mt<i>`         (shape)
  agent(...)                 -> behaviour hooks (see agents_ext)               (agents / camera)

`install()` (ops_ext/__init__.py) appends the ops to agenome.ops.OPS_LIST / OPS / BY_CODE in place, so the codec,
mutation (ops_of_type) and the search see them, and wraps the compiler / renderer / sim so they compile them.

Opcode ranges reserved for extensions (core uses 0x01-0x0C, 0x20-0x48, 0x50-0x58, 0x70-0x73, 0x80, 0x90-0x93, 0xF0):
  0xA0-0xBF  textures (baked spectral / linear-algebra ops at 0xA0-0xAF, per-pixel GLSL ops at 0xB0-0xBF)
  0xC0-0xCF  shapes        0xD0-0xDF  agents        0xE0-0xE7  cameras
"""
from dataclasses import dataclass
from typing import Callable, Optional

from agenome.ops import Op, P  # noqa: F401  (re-exported for the op modules)


@dataclass
class ExtOp:
    op: Op
    glsl_t: Optional[Callable] = None
    bake_t: Optional[Callable] = None
    glsl_s: Optional[Callable] = None
    agent: Optional[object] = None
    family: str = ""

    @property
    def name(self):
        return self.op.name


EXT = {}


def register(op, family="", **hooks):
    e = ExtOp(op, family=family, **hooks)
    if op.name in EXT:
        raise ValueError(f"duplicate ext op {op.name}")
    for other in EXT.values():
        if other.op.code == op.code:
            raise ValueError(f"opcode 0x{op.code:02X} used by {other.name} and {op.name}")
    EXT[op.name] = e
    return e


def hsvp(prefix="", h=128, s=128, v=200):
    return (P(prefix + "h", "hue", default=h), P(prefix + "s", "lin", 0, 1, s), P(prefix + "v", "exp", 0.01, 4.0, v))

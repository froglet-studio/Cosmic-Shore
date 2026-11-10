"""ops_ext: spectral, linear-algebra, fractal and dynamical operators for the Amoebius genome.

    import ops_ext; ops_ext.install()     # before loading genomes that use ext ops (idempotent)

Families (one module each; every op is a normal byte-coded agenome Op):
  tex_spectral  baked textures: FFT spectral synthesis / filtering / convolution / phase swap, SVD low rank,
                colour & patch PCA, Laplacian eigenmodes (Chladni), Gray-Scott reaction-diffusion, Haar wavelets,
                Poisson / field lines / bump, fractal flames (IFS), strange attractors
  tex_glsl      per-pixel textures: iq domain-warped fbm (animated), polar unwrap, kaleidoscope, flow warp
  sdf_ext       shapes: kaleidoscopic IFS, Mandelbox, spherical-harmonic blobs, Fourier tube loops, superformula,
                heightfields from any texture
  agents_ext    agents / cameras: curl-noise flow, Fourier (epicycle) pilots, normal-mode (eigen) spring bodies,
                texture flow fields, Fourier camera rails
See docs/OPS_EXT.md for the measurements.
"""
import importlib
import os

from .registry import EXT, register  # noqa: F401

_INSTALLED = False
FAMILIES = ["tex_spectral", "tex_glsl", "sdf_ext", "agents_ext"]


def _load_families():
    for m in FAMILIES:
        try:
            importlib.import_module(f"ops_ext.{m}")
        except ModuleNotFoundError as e:
            if e.name != f"ops_ext.{m}":
                raise


def install(families=None):
    """Register every ext op into the core tables and wrap the compiler / renderer / sim. Idempotent."""
    global _INSTALLED
    if _INSTALLED:
        return EXT
    _load_families()
    from agenome import ops, glsl, render, sim
    from . import bake

    # ---------------------------------------------------------------- op tables (in place: everyone sees them)
    for e in EXT.values():
        if families and e.family not in families:
            continue
        if e.family == "dataprior" and os.environ.get("OPS_EXT_DATAPRIOR", "0") != "1" and not (
                families and "dataprior" in families):
            continue  # decoder-side data (reel statistics): opt in explicitly, it is not counted in genome bytes
        if e.op.name in ops.OPS:
            continue
        if e.op.code in ops.BY_CODE:
            raise ValueError(f"ext opcode 0x{e.op.code:02X} ({e.op.name}) collides with core "
                             f"{ops.BY_CODE[e.op.code].name}")
        ops.OPS_LIST.append(e.op)
        ops.OPS[e.op.name] = e.op
        ops.BY_CODE[e.op.code] = e.op

    def _d(n):
        o = ops.OPS[n.op]
        return {p.name: p.decode(b) for p, b in zip(o.params, n.params)}

    # ---------------------------------------------------------------- GLSL: textures
    orig_tex = glsl.compile_textures

    def compile_textures(g, live):
        src = orig_tex(g, live)
        ext = {i for i in live if g.nodes[i].op in EXT and ops.OPS[g.nodes[i].op].out == "T"}
        if not ext:
            return src
        lines = src.split("\n")
        out = []
        for ln in lines:
            if ln.startswith("vec3 t"):
                i = int(ln[6:ln.index("(")])
                if i in ext:
                    n = g.nodes[i]
                    e = EXT[n.op]
                    d = _d(n)
                    if e.bake_t is not None:
                        ln = bake.baked_glsl(i, d)
                    else:
                        ins = n.inputs

                        def T(k, default="vec3(0.5)", ins=ins):
                            j = ins[k] if k < len(ins) else None
                            return (lambda uv: f"t{j}({uv})") if j is not None else (lambda uv: default)
                        ln = f"vec3 t{i}(vec2 uv){{ {e.glsl_t(i, d, ins, T)} }}"
            out.append(ln)
        return "\n".join(out)

    glsl.compile_textures = compile_textures

    # ---------------------------------------------------------------- GLSL: shapes
    orig_shp = glsl.compile_shapes

    def compile_shapes(g, live):
        src = orig_shp(g, live)
        ext = {i for i in live if g.nodes[i].op in EXT and ops.OPS[g.nodes[i].op].out == "S"}
        if not ext:
            return src
        out = []
        for ln in src.split("\n"):
            if ln.startswith("float sd") or ln.startswith("Mat mt"):
                i = int(ln[8 if ln[0] == "f" else 6:ln.index("(")])
                if i in ext:
                    n = g.nodes[i]
                    e = EXT[n.op]
                    d = _d(n)
                    ins = n.inputs
                    a = ins[0] if len(ins) > 0 else None

                    def SA(q, a=a):
                        return f"sd{a}({q})" if a is not None else "1e9"

                    def MA(q, nn="n", a=a):
                        return f"mt{a}({q},{nn})" if a is not None else "mdef()"

                    sd, mt = e.glsl_s(i, d, ins, SA, MA)
                    if ln[0] == "f":
                        ln = f"float sd{i}(vec3 p){{ {sd or 'return 1e9;'} }}"
                    else:
                        ln = f"Mat mt{i}(vec3 p, vec3 n){{ {mt or 'return mdef();'} }}"
            out.append(ln)
        return "\n".join(out)

    glsl.compile_shapes = compile_shapes

    # ---------------------------------------------------------------- meshes: a true triangular prism ("wedge")
    if "wedge" not in sim.MESHES:
        sim.MESHES.append("wedge")  # same list object as render.MESHES; core enums index only the first six
        orig_mesh = render._mesh

        def _mesh(name):
            if name == "wedge":  # triangular prism along z: the Amoebius prism-wall cross-section
                import numpy as _np
                P = _np.array([(-0.5, -0.5, -0.5), (0.5, -0.5, -0.5), (0.0, 0.5, -0.5),
                               (-0.5, -0.5, 0.5), (0.5, -0.5, 0.5), (0.0, 0.5, 0.5)], float)
                tris = [(0, 2, 1), (3, 4, 5), (0, 1, 4), (0, 4, 3), (1, 2, 5), (1, 5, 4), (2, 0, 3), (2, 3, 5)]
                return render._flat(P - P.mean(0), tris)
            return orig_mesh(name)
        render._mesh = _mesh

    # ---------------------------------------------------------------- renderer: bake before the frame loop
    R = render.Renderer
    orig_render = R.render
    orig_prog = R.scene_program

    def r_render(self, g, n_frames=60, *a, **kw):
        if not hasattr(self, "_ext_baker"):
            self._ext_baker = bake.Baker(self)
        g2 = g.copy().ensure_root()
        if any(is_b for is_b in (bake.is_baked(g2.nodes[i].op) for i in g2.live())):
            self._ext_baker.bake_all(g2)
        else:
            self._ext_baker.release()
        for fb in self.out_fbo:  # the feedback pass reads the previous render's last frame otherwise
            fb.clear(0, 0, 0, 1)
        return orig_render(self, g, n_frames, *a, **kw)

    def r_prog(self, src):
        pv = orig_prog(self, src)
        if hasattr(self, "_ext_baker"):
            self._ext_baker.bind(pv[0])
        return pv

    R.render = r_render
    R.scene_program = r_prog

    # ---------------------------------------------------------------- agents / cameras (hooks live in agents_ext)
    import sys as _sys
    agents_ext = _sys.modules.get("ops_ext.agents_ext")
    if agents_ext is not None and hasattr(agents_ext, "install_hooks"):
        agents_ext.install_hooks(sim, render)
    _INSTALLED = True
    return EXT


def apply_weight(w):
    """Bias mutation's op choice: w < 1 repeats core ops round(1/w) times, w > 1 repeats ext ops round(w) times."""
    from agenome import genome, ops
    if abs(w - 1.0) < 1e-6:
        return
    base = ops.ops_of_type
    rc = max(1, round(1 / w)) if w < 1 else 1
    re_ = max(1, round(w)) if w > 1 else 1

    def ops_of_type(t):
        out = []
        for o in base(t):
            out.extend([o] * (re_ if o.name in EXT else rc))
        return out
    genome.ops_of_type = ops_of_type


def worker_init(*args, **kw):
    """Drop-in for agenome.search._init in spawned search workers: install the ext ops (unless OPS_EXT_OFF=1),
    then the core init."""
    import os
    if os.environ.get("OPS_EXT_OFF", "0") != "1":
        fams = os.environ.get("OPS_EXT_FAMILIES")
        install(fams.split(",") if fams else None)
    from agenome import search
    if not hasattr(search, "_orig_init"):
        search._orig_init = search.__dict__["_init"] if search._init is not worker_init else None
    return search._orig_init(*args, **kw)

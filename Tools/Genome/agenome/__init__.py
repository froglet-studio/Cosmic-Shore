"""agenome: a .kkrieger-style operator-graph genome judged by Amoebius footage (see SPEC.md and CONTRACT.md).

Extension operators (ops_ext/, next to this package: FFT / SVD / PCA / Laplacian eigenmodes / reaction-diffusion /
flames / KIFS / curl flows / prism ribbons ...) install themselves on import, so the search, its workers, the CLI,
the report and the judge all know them. AGENOME_EXT=0 runs the core set alone.

The part-by-part thread's 13 hx_ operators (agenome/hops.py: hex lattices, spherical harmonics, sky beams and stars,
Gabor noise, pyramid fields, and FFT / PCA / difference-of-Gaussians / histogram post passes) install the same way;
AGENOME_HOPS=0 leaves them out.
"""
import os as _os
import sys as _sys

# ops_ext imports agenome.ops itself: when ops_ext is the module being imported first, it is already in sys.modules
# (half-initialised) and its own entry points call install(), so only install from here when agenome comes first
if _os.environ.get("AGENOME_EXT", "1") != "0" and "ops_ext" not in _sys.modules:
    try:
        import ops_ext as _ext
    except ImportError:  # ops_ext not beside agenome (an old mirror): the core set alone
        _ext = None
    if _ext is not None and hasattr(_ext, "install"):
        _ext.install()

if _os.environ.get("AGENOME_HOPS", "1") != "0":
    from . import hops as _hops

    _hops.install()  # idempotent; asserts that no opcode or name is taken twice

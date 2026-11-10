"""agenome.search with the extension ops installed (in the parent and in every spawned worker).

  python3 -m ops_ext.run_search [--ext-off] [--ext-families spectral,linalg,...] <agenome.search args>

--ext-off runs the identical code path without registering any ext op: the control arm of an A/B search.
--ext-weight W scales how often mutation picks an ext op relative to a core op of the same type (default 1 = uniform
over all ops; the ext set roughly doubles the texture op count, so 0.5 keeps the core/ext mix near 50/50).
"""
import os
import random
import sys


def main():
    argv = sys.argv[1:]
    off = "--ext-off" in argv
    argv = [a for a in argv if a != "--ext-off"]
    fams = None
    w = 1.0
    for flag in ("--ext-families", "--ext-weight"):
        if flag in argv:
            k = argv.index(flag)
            val = argv[k + 1]
            del argv[k:k + 2]
            if flag == "--ext-families":
                fams = val.split(",")
            else:
                w = float(val)
    os.environ["OPS_EXT_OFF"] = "1" if off else "0"
    if fams:
        os.environ["OPS_EXT_FAMILIES"] = ",".join(fams)
    os.environ["OPS_EXT_WEIGHT"] = str(w)
    from agenome import search
    import ops_ext
    if not off:
        ops_ext.install(fams)
        ops_ext.apply_weight(w)
    search._orig_init = search._init
    search._init = ops_ext.worker_init
    search.main(argv)


if __name__ == "__main__":
    main()

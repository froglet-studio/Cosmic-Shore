# Merging ops_ext into the genome (for the genome/search thread)

ops_ext needs **no edits to agenome** to run: `import ops_ext; ops_ext.install()` appends its 39 ops to
`agenome.ops.OPS_LIST/OPS/BY_CODE` in place and wraps `glsl.compile_textures`, `glsl.compile_shapes`,
`Renderer.render`, `Renderer.scene_program`, `render._mesh` (adds a `wedge` triangular-prism mesh as the 7th entry of
`MESHES`; core enums still index only the first six) and the agent sim (`sim/render.build_populations`,
`World.__init__/step/_step_movers/instances`, `Camera.update`, `render.look_at` for camera banking).
Entry points: `python3 -m ops_ext.run_search` (search; installs itself in spawned workers by swapping
`search._init` for `ops_ext.worker_init`), `python3 -m ops_ext.cli` (render/sheet/score), `ops_ext/island_ext.sh`.

## Proposed merge (3 small changes)

1. Copy `ops_ext/` next to `agenome/` (`Tools/Genome/ops_ext/`) and install it on package import, so every entry
   point (search workers, cli, report, judge scoring) knows the ops. Tested on a copy: 87 ops visible.

```python
# Tools/Genome/agenome/__init__.py
# Extension operators (spectral / linear-algebra / fractal / dynamics). AGENOME_EXT=0 disables.
import os as _os
if _os.environ.get("AGENOME_EXT", "1") != "0":
    try:
        import ops_ext as _ext
        _ext.install()
    except ImportError:
        pass
```

2. **Render determinism bug (core, independent of ops_ext):** the composite pass mixes `uPrev` (the previous output
   texture) with weight `feedback`, and `out_tex` is never cleared between renders, so the first frames of a clip
   carry the LAST genome's final frame. Same genome rendered twice in one Renderer differed by 1.2/255 mean over 20
   frames (all in the first frames); with feedback 0.3+ this moves the judge. Fix at the top of `Renderer.render`:

```python
for fb in self.out_fbo:
    fb.clear(0, 0, 0, 1)
```

3. Optional, found while building the shape ops:
   - `Genome.repair()` fills an OPTIONAL slot (lowercase type) with the nearest earlier node of that type whenever one
     exists, so optional inputs are almost never None (a genome with a heightfield texture gets it as its sky too).
     Leaving None alone in optional slots would make optional inputs mean something.
   - `glsl.f()` prints negatives as `-0.3`, so a template `a-{f(x)}` becomes `a--0.3` (GLSL syntax error).
     `f()` could wrap negatives in parentheses.
   - `sdf.polar` only repeats content on +x of its plane (content on +y vanishes); worth a SPEC line.

Opcodes: 0xA0-0xAC baked textures, 0xB0-0xB6 GLSL textures, 0xC0-0xC9 shapes, 0xD0-0xD5 and 0xD7 agents,
0xE0-0xE1 cameras (see `docs/OPS_EXT.md` for the table). SPEC regeneration: `agenome.ops.spec_markdown()` after
`install()` lists them with lineages.

# Amoebius genome — SPEC (format `amoebius-genome/0`, 2026-11-10)

Source of truth: `Tools/Genome/agenome/ops.py` on branch `claude/amoebius-genome` of froglet-studio/cosmic-shore
(mirrored to `/mnt/project-files/amoebius-genome/genome/agenome/`). This file is generated from it.

## The idea
A werkkzeug3/.kkrieger operator stack, generalized: every node is one operator with typed inputs and a fixed list of
**byte parameters** (0..255). Types: **T** texture (uv->rgb), **S** shape (signed distance field + material),
**A** agents (movers, static instances, prism trails, bodies), **C** camera, **P** post, **H** hud, **O** root.
T and S compile to GLSL (raymarched, demoscene style); A runs as a small numpy simulation drawn as instanced meshes;
the root `out` wires scene S, agents A, camera C, post P, sky T and hud H. Renders the CONTRACT §1 clip
(256x144, 15 fps of game time) headless on llvmpipe.

## JSON form (what seeds are written in)
```json
{"format": "amoebius-genome/0", "seed": 7, "meta": {"lineage": "kkrieger", "name": "...", "source": "..."},
 "nodes": [
   {"id": 0, "op": "tex.perlin", "in": [], "p": {"freq": 60, "oct": 200}},
   {"id": 1, "op": "sdf.sphere", "in": [], "values": {"r": 40.0}},
   {"id": 2, "op": "sdf.material", "in": [1, 0], "p": {"ev": 180, "emit": 120}},
   {"id": 3, "op": "out", "in": [2, null, null, null, 0, null]}
 ]}
```
- `p` = raw bytes by param name (missing -> default). `values` = decoded values by name (the nearest byte is
  chosen) — use whichever is natural; translators from real data should prefer `values`.
- `in` = node ids, one per input slot of the op (null = none). Inputs must point to EARLIER nodes. Wrong types
  are repaired to the nearest earlier node of the right type, so a seed never fails to load.
- Lowercase input types in the table are optional.

## Byte form (what compactness measures)
`A6 00 seed` then per node `opcode, ref per input slot (distance back 1..255, 0 = none), byte per param`.
Compactness objective = `len(zlib.compress(bytes, 9))` (kkrunchy stand-in). The .kx layout (op-type table + params
grouped per op type) was measured by the seed thread on 2026-11-10: about 4% WORSE at these sizes, so this layout
stays. The population concatenated compresses 5x, so a shared-scaffold dictionary (`zlib` `zdict`) is the next
candidate for an honest size measure. Operator proposals: seeds/OP_PROPOSALS.md.

## Param kinds
`lin lo..hi`, `exp lo..hi` (geometric), `int lo..hi`, `enum n` (byte*n/256), `hue` (0..1), `scub hi`
(signed cubic: hi*((b-127.5)/127.5)^3, fine near 0), `bits mask`.

## Operators
| code | op | out | inputs | params (byte -> value) | lineage |
|---|---|---|---|---|---|
| 0x01 | `tex.flat` | T | - | h:hue, s:lin[0,1], v:exp[0.01,4] | Bitmap_Flat |
| 0x02 | `tex.gradient` | T | - | ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4], angle:lin[0,6.28319], pos:lin[-1,1], length:exp[0.05,4], mode:enum3 | Bitmap_Gradient |
| 0x03 | `tex.perlin` | T | - | freq:exp[1,32], oct:int[1,6], persist:lin[0.2,0.8], seed:int[0,255], gamma:exp[0.25,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Bitmap_Perlin |
| 0x04 | `tex.cell` | T | - | count:exp[2,64], seed:int[0,255], mode:enum3, gamma:exp[0.25,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Bitmap_Cell |
| 0x05 | `tex.blend` | T | T,T | mode:enum7, amount:lin[0,1] | Bitmap_Merge |
| 0x06 | `tex.distort` | T | T,T | amount:lin[0,0.5] | Bitmap_Distort |
| 0x07 | `tex.rotozoom` | T | T | angle:lin[0,6.28319], zoom:exp[0.25,8], tx:lin[0,1], ty:lin[0,1] | Bitmap_Rotate |
| 0x08 | `tex.hscb` | T | T | hue:lin[-0.5,0.5], sat:exp[0.1,4], contrast:exp[0.25,4], bright:exp[0.25,4] | Bitmap_HSCB |
| 0x09 | `tex.colorize` | T | T | ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Bitmap_Range |
| 0x0A | `tex.twirl` | T | T | strength:scub[0,10], radius:exp[0.05,1] | Bitmap_Twirl |
| 0x0B | `tex.glowrect` | T | T | cx:lin[0,1], cy:lin[0,1], rx:exp[0.01,1], ry:exp[0.01,1], power:exp[0.25,8], h:hue, s:lin[0,1], v:exp[0.01,4] | Bitmap_GlowRect |
| 0x0C | `tex.bricks` | T | - | tx:int[1,32], ty:int[1,32], fuge:lin[0,0.3], seed:int[0,255], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Bitmap_Bricks |
| 0x20 | `sdf.sphere` | S | - | r:exp[0.05,200] | Mesh_Sphere |
| 0x21 | `sdf.box` | S | - | sx:exp[0.02,100], sy:exp[0.02,100], sz:exp[0.02,100], round:lin[0,0.5] | Mesh_Cube |
| 0x22 | `sdf.torus` | S | - | R:exp[0.05,100], r:lin[0.01,0.9] | Mesh_Torus |
| 0x23 | `sdf.cylinder` | S | - | r:exp[0.02,100], h:exp[0.02,200] | Mesh_Cylinder |
| 0x24 | `sdf.octa` | S | - | r:exp[0.02,100] |  |
| 0x25 | `sdf.gyroid` | S | - | scale:exp[0.02,5], thick:lin[0.02,1], bound:exp[1,400] | Cosmic Shore flora |
| 0x26 | `sdf.plane` | S | - | theta:lin[0,3.14159], phi:lin[0,6.28319], offset:scub[0,200] | Mesh_Grid |
| 0x27 | `sdf.shell` | S | S | thick:exp[0.005,10] |  |
| 0x28 | `sdf.invert` | S | S |  | Mesh_Invert |
| 0x30 | `sdf.union` | S | S,S | k:exp[0.001,20] | Mesh_Add |
| 0x31 | `sdf.subtract` | S | S,S | k:exp[0.001,20] | Mesh_BSP |
| 0x32 | `sdf.intersect` | S | S,S | k:exp[0.001,20] | Mesh_BSP |
| 0x38 | `sdf.translate` | S | S | x:scub[0,200], y:scub[0,200], z:scub[0,200] | Mesh_Transform |
| 0x39 | `sdf.rotate` | S | S | ax:lin[-3.14159,3.14159], ay:lin[-3.14159,3.14159], az:lin[-3.14159,3.14159] | Mesh_Transform |
| 0x3A | `sdf.scale` | S | S | s:exp[0.05,20] | Mesh_Transform |
| 0x3B | `sdf.repeat` | S | S | axes:bits7, px:exp[0.1,200], py:exp[0.1,200], pz:exp[0.1,200] | Mesh_Multiply |
| 0x3C | `sdf.polar` | S | S | count:int[1,32], axis:enum3 | Mesh_Multiply2 |
| 0x3D | `sdf.twist` | S | S | amount:scub[0,2], axis:enum3 | Mesh_Bend |
| 0x3E | `sdf.displace` | S | S | freq:exp[0.05,20], amp:exp[0.001,5], seed:int[0,255] | Mesh_Perlin |
| 0x3F | `sdf.mirror` | S | S | axes:bits7 |  |
| 0x40 | `sdf.spin` | S | S | axis:enum3, speed:scub[0,3] | anim |
| 0x41 | `sdf.bob` | S | S | axis:enum3, amp:exp[0.001,20], freq:exp[0.05,5] | anim |
| 0x48 | `sdf.material` | S | S,t | ah:hue, as:lin[0,1], av:exp[0.01,4], eh:hue, es:lin[0,1], ev:exp[0.01,4], emit:exp[0.001,10], glow:exp[0.0001,2], falloff:exp[0.2,50], texscale:exp[0.005,10], texmix:lin[0,1], spec:lin[0,1] | Material_Add |
| 0x50 | `ag.spawn` | A | - | n:exp[1,512], radius:exp[0.5,300], shape:enum4, speed:exp[0.1,200], seed:int[0,255], teams:int[1,4], hue0:hue, huestep:lin[0,1], sat:lin[0,1], val:exp[0.05,4] | boids |
| 0x51 | `ag.boids` | A | A | coh:exp[0.001,10], ali:exp[0.001,10], sep:exp[0.001,10], radius:exp[0.1,100], turn:exp[0.05,20] | boids |
| 0x52 | `ag.wander` | A | A | freq:exp[0.02,5], amp:exp[0.01,50] |  |
| 0x53 | `ag.attract` | A | A | cx:scub[0,200], cy:scub[0,200], cz:scub[0,200], strength:exp[0.001,20], orbit:scub[0,2], radius:exp[0.1,300] |  |
| 0x54 | `ag.bound` | A | A | radius:exp[1,1000], stiff:exp[0.01,100] |  |
| 0x55 | `ag.trail` | A | A | interval:exp[0.02,2], sx:exp[0.01,20], sy:exp[0.01,20], sz:exp[0.01,20], life:exp[0.2,120], emit:exp[0.01,20], colormode:enum2, edge:lin[0,4], mesh:enum6, h:hue, s:lin[0,1], v:exp[0.01,4] | Cosmic Shore prism trails |
| 0x56 | `ag.body` | A | A | shape:enum6, sx:exp[0.01,20], sy:exp[0.01,20], sz:exp[0.01,20], emit:exp[0.01,20], colormode:enum2, edge:lin[0,4], h:hue, s:lin[0,1], v:exp[0.01,4] | vessels |
| 0x57 | `ag.merge` | A | A,A |  |  |
| 0x58 | `ag.scatter` | A | - | n:exp[1,4096], radius:exp[0.5,400], shape:enum4, mesh:enum6, size:exp[0.01,50], sizevar:lin[0,1], spin:scub[0,3], emit:exp[0.01,20], seed:int[0,255], huevar:lin[0,1], edge:lin[0,4], h:hue, s:lin[0,1], v:exp[0.01,4] | Mesh_Multiply |
| 0x70 | `cam.chase` | C | A | agent:int[0,15], dist:exp[0.1,200], height:scub[0,100], lag:lin[0,0.98], fov:lin[20,120], look:exp[0.01,20] | game camera |
| 0x71 | `cam.orbit` | C | - | radius:exp[0.5,1000], height:scub[0,400], speed:scub[0,2], fov:lin[20,120], phase:lin[0,6.28319] | demo camera |
| 0x72 | `cam.fly` | C | - | radius:exp[0.5,1000], speed:exp[0.01,5], fov:lin[20,120], seed:int[0,255] | demo camera |
| 0x73 | `cam.shake` | C | C | amp:exp[0.0001,2], freq:exp[0.5,30] |  |
| 0x80 | `post.grade` | P | - | exposure:exp[0.25,8], thresh:lin[0,2], bloom:exp[0.001,4], bloomrad:exp[0.5,8], fh:hue, fs:lin[0,1], fv:exp[0.01,4], fog:exp[1e-05,1], vignette:lin[0,1.5], sat:exp[0.25,4], contrast:exp[0.5,2], feedback:lin[0,0.95], grain:lin[0,0.2], aberr:lin[0,0.02] | GenIPP |
| 0x90 | `hud.rect` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], w:exp[0.003,1], hh:exp[0.003,1], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1], outline:lin[0,0.03] | overlay |
| 0x91 | `hud.ring` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], r:exp[0.005,0.5], thick:exp[0.001,0.2], arc:lin[0,1], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1] | overlay |
| 0x92 | `hud.bar` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], w:exp[0.003,1], hh:exp[0.003,1], segs:int[1,20], fill:lin[0,1], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1] | overlay |
| 0x93 | `hud.text` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], size:exp[0.005,0.1], chars:int[1,40], rows:int[1,8], seed:int[0,255], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1] | Bitmap_Text |
| 0xF0 | `out` | O | s,a,c,p,t,h | sky:exp[0.001,4], steps:int[16,128], far:exp[10,4000], ltheta:lin[0,3.14159], lphi:lin[0,6.28319], ambient:lin[0,1], warmup:lin[0,30], lh:hue, ls:lin[0,1], lv:exp[0.01,4] | KOp root |

## Variation operators (for the search)
param tweak (gaussian in byte space), op swap within a type, insert filter, delete node, rewire input, duplicate
node, typed subgraph crossover at a root slot. All followed by `repair()`. The search also spends `--tune` (default
0.25) of each generation on parameter-only steps around its best few genomes.

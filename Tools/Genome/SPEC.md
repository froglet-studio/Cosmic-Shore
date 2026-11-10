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
- Lowercase input types in the table are optional. A stored genome keeps an optional input that is null (no sky
  stays no sky); mutation wires a new node's inputs to the nearest earlier node of the right type.

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
| 0x0D | `tex.mirror` | T | T | axes:enum3 | Bitmap_Flip |
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
| 0x49 | `sdf.rim` | S | S | fh:hue, fs:lin[0,1], fv:exp[0.01,4], rh:hue, rs:lin[0,1], rv:exp[0.01,4], power:exp[0.5,16], emit:exp[0.05,10] | Cosmic Shore prism shader |
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
| 0x59 | `ag.rim` | A | A | power:exp[0.5,16], bright:exp[0.1,8] | Cosmic Shore prism shader |
| 0x70 | `cam.chase` | C | A | agent:int[0,15], dist:exp[0.1,200], height:scub[0,100], lag:lin[0,0.98], fov:lin[20,120], look:exp[0.01,20] | game camera |
| 0x71 | `cam.orbit` | C | - | radius:exp[0.5,1000], height:scub[0,400], speed:scub[0,2], fov:lin[20,120], phase:lin[0,6.28319] | demo camera |
| 0x72 | `cam.fly` | C | - | radius:exp[0.5,1000], speed:exp[0.01,5], fov:lin[20,120], seed:int[0,255] | demo camera |
| 0x74 | `cam.lens` | C | C | panini:lin[0,1], crop:lin[0,1] | URP PaniniProjection |
| 0x73 | `cam.shake` | C | C | amp:exp[0.0001,2], freq:exp[0.5,30] |  |
| 0x80 | `post.grade` | P | - | exposure:exp[0.25,8], thresh:lin[0,2], bloom:exp[0.001,4], bloomrad:exp[0.5,8], fh:hue, fs:lin[0,1], fv:exp[0.01,4], fog:exp[1e-05,1], vignette:lin[0,1.5], sat:exp[0.25,4], contrast:exp[0.5,2], feedback:lin[0,0.95], grain:lin[0,0.2], aberr:lin[0,0.02] | GenIPP |
| 0x90 | `hud.rect` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], w:exp[0.003,1], hh:exp[0.003,1], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1], outline:lin[0,0.03] | overlay |
| 0x91 | `hud.ring` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], r:exp[0.005,0.5], thick:exp[0.001,0.2], arc:lin[0,1], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1] | overlay |
| 0x92 | `hud.bar` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], w:exp[0.003,1], hh:exp[0.003,1], segs:int[1,20], fill:lin[0,1], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1] | overlay |
| 0x93 | `hud.text` | H | h | anchor:enum9, x:lin[-0.5,0.5], y:lin[-0.5,0.5], size:exp[0.005,0.1], chars:int[1,40], rows:int[1,8], seed:int[0,255], h:hue, s:lin[0,1], v:exp[0.01,4], alpha:lin[0,1] | Bitmap_Text |
| 0xF0 | `out` | O | s,a,c,p,t,h | sky:exp[0.001,4], steps:int[16,128], far:exp[10,4000], ltheta:lin[0,3.14159], lphi:lin[0,6.28319], ambient:lin[0,1], warmup:lin[0,30], lh:hue, ls:lin[0,1], lv:exp[0.01,4] | KOp root |
| 0xA0 | `tex.spectral` | T | - | beta:exp[0.25,5], fc:exp[0.5,96], bw:exp[0.15,12], aniso:lin[0,0.95], angle:lin[0,3.14159], mode:enum5, levels:int[1,16], seed:int[0,255], gamma:exp[0.25,4], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | spectral synthesis (Voss / Musgrave) |
| 0xA1 | `tex.fftfilter` | T | T | mode:enum6, fc:exp[0.5,128], bw:exp[0.1,8], angle:lin[0,3.14159], gain:exp[0.25,8], mix:lin[0,1], drift:scub[0,4] | werkkzeug Blur, generalized |
| 0xA2 | `tex.convolve` | T | T,t | mode:enum3, size:exp[0.01,1], gain:exp[0.25,8], mix:lin[0,1], drift:scub[0,4] | convolution theorem |
| 0xA3 | `tex.phaseswap` | T | T,t | amount:lin[0,1], gain:exp[0.25,4], drift:scub[0,4] | Oppenheim-Lim phase swap |
| 0xA4 | `tex.svd` | T | T | mode:enum4, rank:int[1,64], gain:exp[0.25,4], mix:lin[0,1], drift:scub[0,4] | SVD low-rank |
| 0xA5 | `tex.pca` | T | T | mode:enum4, k:int[0,15], w1:exp[0.1,10], w2:exp[0.1,10], w3:exp[0.1,10], rot:lin[0,6.28319], mix:lin[0,1], drift:scub[0,4] | PCA / Karhunen-Loeve |
| 0xA6 | `tex.eigen` | T | t | dom:enum6, mode:int[0,60], mode2:int[0,60], w2:lin[0,1], aspect:exp[0.5,2], pot:exp[0.01,1000], render:enum4, sharp:exp[1,60], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | spectral geometry (Chladni, Kac) |
| 0xA7 | `tex.rd` | T | t | feed:lin[0.01,0.09], delta:exp[0.0005,0.03], scale:enum3, steps:int[2,16], init:enum4, out:enum3, seed:int[0,255], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Turing / Gray-Scott / Pearson 1993 |
| 0xA8 | `tex.wavelet` | T | T | keep:bits255, orient:bits7, shrink:lin[0,0.5], gain:exp[0.25,8], approx:lin[0,1], mix:lin[0,1], drift:scub[0,4] | Haar / Mallat |
| 0xA9 | `tex.poisson` | T | T | mode:enum5, scale:exp[0.1,20], angle:lin[0,6.28319], elev:lin[0.05,1.5], levels:int[1,32], gain:exp[0.25,4], mix:lin[0,1], drift:scub[0,4] | werkkzeug Bump+Normals, spectral |
| 0xAA | `tex.flame` | T | - | maps:int[2,5], var:enum8, var2:enum8, vmix:lin[0,1], contract:lin[0.3,0.98], sym:int[1,8], zoom:exp[0.25,4], gamma:exp[0.15,1.5], bright:exp[0.1,8], seed:int[0,255], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Draves 1992 flam3 / Electric Sheep |
| 0xAB | `tex.attractor` | T | - | kind:enum3, pa:lin[-3,3], pb:lin[-3,3], pc:lin[-3,3], pd:lin[-3,3], zoom:exp[0.25,4], gamma:exp[0.15,1.5], bright:exp[0.1,8], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Pickover / de Jong attractors |
| 0xAC | `tex.physarum` | T | t | agents:exp[1000,40000], steps:int[2,16], sangle:lin[0.1,1.5], sdist:exp[2,30], rangle:lin[0.1,1.5], decay:lin[0.8,0.99], scale:enum3, seed:int[0,255], gamma:exp[0.25,4], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | Physarum polycephalum model |
| 0xAD | `tex.distance` | T | T | thresh:lin[0,1], mode:enum4, width:exp[0.002,0.2], gamma:exp[0.25,4], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | distance transform (Danielsson) |
| 0xB0 | `tex.warpfbm` | T | - | freq:exp[1,16], oct:int[1,6], warp:lin[0,2], warp2:lin[0,2], contrast:exp[0.5,4], cmix:lin[0,1], seed:int[0,255], drift:scub[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4], ch:hue, cs:lin[0,1], cv:exp[0.01,4] | iq warp |
| 0xB1 | `tex.palette` | T | T | ar:lin[0,1], ag:lin[0,1], ab:lin[0,1], br:lin[0,1], bg:lin[0,1], bb:lin[0,1], freq:exp[0.25,4], ph:lin[0,1], spread:lin[0,0.5], gain:exp[0.25,4] | iq palettes |
| 0xB2 | `tex.polar` | T | T | mode:enum2, repeat:int[1,16], twist:scub[0,8], power:exp[0.25,4], zoom:exp[0.25,8] | Bitmap_Unwrap |
| 0xB3 | `tex.kaleido` | T | T | count:int[1,16], angle:lin[0,6.28319], zoom:exp[0.25,4], cx:lin[0,1], cy:lin[0,1] | kaleidoscope |
| 0xB4 | `tex.flowwarp` | T | T,T | steps:int[1,6], amount:exp[0.001,0.2], swirl:lin[0,3.14159] | flow noise / LIC |
| 0xB5 | `tex.kali` | T | - | form:lin[0.4,0.6], iter:int[4,18], vol:int[2,16], tile:lin[0.5,1], step:exp[0.05,0.3], bright:exp[0.0003,0.01], dark:lin[0,1], distfade:lin[0.5,0.95], sat:lin[0,1], zoom:exp[0.25,4], drift:scub[0,4], tmix:lin[0,1], gain:exp[0.1,10], ah:hue, as:lin[0,1], av:exp[0.01,4] | Star Nest (Kali, Shadertoy XlfGRj) |
| 0xB6 | `tex.galaxy` | T | t | mode:enum2, lon:lin[0,6.28319], lat:lin[-1.4,1.4], size:exp[0.05,2], pa:lin[0,3.14159], incl:lin[0,1.5], arms:int[1,6], pitch:lin[0.15,1.2], sharp:exp[0.5,8], spin:scub[0,4], bulge:exp[0.1,10], gain:exp[0.05,8], seed:int[0,255], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | log spiral / galactic band |
| 0xC0 | `sdf.kifs` | S | s | iters:int[1,10], fold:enum5, ax:lin[-3.14159,3.14159], ay:lin[-3.14159,3.14159], scale:exp[1.2,3.5], ox:scub[0,3], oy:scub[0,3], oz:scub[0,3], anim:scub[0,1], size:exp[0.1,100] | Knighty KIFS (fractalforums 2010) |
| 0xC1 | `sdf.mandelbox` | S | - | scale:lin[-1,1], minr:lin[0.05,1], fixr:lin[0.5,2], iters:int[4,14], bound:exp[0.5,12], anim:scub[0,0.5], size:exp[0.1,100] | Tom Lowe Mandelbox (2010) |
| 0xC2 | `sdf.harmonic` | S | - | R:exp[0.2,100], amp:lin[0,0.9], lmax:int[2,4], decay:lin[0,3], seed:int[0,255], anim:scub[0,3] | spherical harmonics (spectral shape synthesis) |
| 0xC3 | `sdf.loop` | S | - | R:exp[0.2,200], r:exp[0.01,40], K:int[1,4], ramp:lin[0,0.6], yamp:lin[0,0.6], seed:int[0,255], sides:int[2,8], twist:int[-8,8], spin:scub[0,4], anim:scub[0,2], mode:enum3, ribs:int[0,64] | Fourier curves; Amoebius prism tunnels |
| 0xC4 | `sdf.superformula` | S | - | m1:int[0,16], n11:exp[0.1,40], n12:exp[0.7,40], n13:exp[0.7,40], m2:int[0,16], n21:exp[0.1,40], n22:exp[0.7,40], n23:exp[0.7,40], morph:lin[0,1.5], size:exp[0.1,100] | Gielis 2003 superformula |
| 0xC5 | `sdf.heightfield` | S | s,T | height:exp[0.05,100], scale:exp[0.001,2], gamma:exp[0.25,4], lip:lin[0.1,1], y0:scub[0,100], mode:enum3, size:exp[0.1,100], gain:exp[0.25,16] | werkkzeug Mesh_Displace / heightmap terrain |
| 0xC6 | `sdf.warp` | S | S | mode:enum3, freq:exp[0.01,10], amp:exp[0.01,20], speed:scub[0,4] | Mesh_Bend / iq domain warping |
| 0xC7 | `sdf.voronoi` | S | - | cell:exp[0.1,50], thick:lin[0,0.4], jitter:lin[0,1], bound:exp[1,400], mode:enum2, seed:int[0,255], anim:scub[0,3] | Worley 1996 cellular; cell tissue / foam |
| 0xC8 | `sdf.prism` | S | - | w:exp[0.02,50], len:exp[0.02,200], mode:enum2, round:lin[0,0.5], axis:enum3 | Amoebius prism walls; iq sdTriPrism / sdHexPrism |
| 0xC9 | `sdf.path` | S | S | a1:exp[0.01,100], f1:exp[0.001,2], a2:exp[0.01,100], f2:exp[0.001,2], scroll:scub[0,4], phase:lin[0,6.28319] | demoscene tunnel paths |
| 0xD0 | `ag.curl` | A | A | freq:exp[0.002,0.5], strength:exp[0.05,20], evolve:exp[0.001,3], seed:int[0,255] | Bridson 2007 curl noise |
| 0xD1 | `ag.fourier` | A | A | K:int[1,4], radius:exp[0.5,300], speed:exp[0.05,4], spread:lin[0,1], gain:exp[0.05,20], tube:exp[0.01,50], seed:int[0,255] | epicycles / Fourier series |
| 0xD2 | `ag.modes` | A | A | first:int[1,32], count:int[1,8], amp:lin[0,1], freq:exp[0.02,4], dir:enum4, k:int[3,12], gain:exp[0.05,20] | graph Laplacian eigenmodes / modal analysis |
| 0xD3 | `ag.texflow` | A | A,t | map:enum2, scale:exp[1,1000], perp:lin[0,1], strength:exp[0.05,20], height:exp[0.1,300], radius:exp[1,500], seed:int[0,255] | flow-field steering (Reynolds) |
| 0xD4 | `ag.vortex` | A | A | count:int[1,6], strength:exp[0.05,20], core:exp[0.2,100], spread:exp[0.5,300], tilt:lin[0,1], inflow:lin[0,1], updraft:scub[0,1], drift:scub[0,1], seed:int[0,255] | point vortices / Rankine vortex |
| 0xD7 | `ag.ribbon` | A | A | width:exp[0.01,20], height:exp[0.01,20], interval:exp[0.02,2], life:exp[0.2,120], twist:scub[0,6], emit:exp[0.01,20], edge:lin[0,4], facets:enum3, colormode:enum2, h:hue, s:lin[0,1], v:exp[0.01,4] | Cosmic Shore prism walls, continuous |
| 0xE0 | `cam.fourier` | C | - | K:int[1,4], radius:exp[0.5,1000], hamp:lin[0,1], speed:exp[0.005,2], lookahead:exp[0.05,20], aim:lin[0,1], bank:lin[0,1], fov:lin[20,120], seed:int[0,255], phase:lin[0,6.28319] | demo camera spline |
| 0xE1 | `cam.track` | C | A | dist:exp[0.3,8], height:scub[0,1], speed:scub[0,2], fov:lin[20,120], lag:lin[0,0.98], phase:lin[0,6.28319] | game camera |
| 0xD5 | `ag.flow` | A | A | field:enum4, scale:exp[2,500], a:lin[0,2], b:lin[0,2], c:lin[0,2], gain:exp[0.05,20], drift:scub[0,2] | Arnold-Beltrami-Childress flow |
| 0xB7 | `tex.lines` | T | t | mode:enum3, n:exp[1,256], vpx:lin[0,1], vpy:lin[0,1], theta:lin[0,3.14159], spread:lin[0,1], width:exp[0.3,8], profile:enum3, grow:exp[0.01,8], taper:exp[0.01,1], fill:lin[0,1], dashf:exp[0.5,40], duty:lin[0.05,1], speed:scub[0,40], halo:exp[0.05,4], gain:exp[0.05,8], seed:int[0,255], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | fitness lab tex.lines |
| 0xE8 | `post.bandgain` | P | p | g1:exp[0.25,4], g2:exp[0.25,4], g3:exp[0.25,4], g4:exp[0.25,4], tailcut:lin[0,1], chroma:exp[0.25,12] | fitness lab post.bandgain |
| 0xE2 | `cam.kick` | C | C | rate:exp[0.05,4], alpha:lin[1.05,3], amp:exp[0.002,1], decay:exp[0.5,20], freq:exp[0.3,20], tilt:lin[0,1], seed:int[0,255] | fitness lab cam.kick |
| 0x18 | `tex.hx_fbm_warp` | T | - | freq:exp[1,16], oct:int[1,6], warp:lin[0,8], seed:int[0,255], speed:scub[0,0.05], contrast:exp[1,8], gamma:exp[0.25,4], streak:lin[0,4], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4], ch:hue, cs:lin[0,1], cv:exp[0.01,4] | hops |
| 0x19 | `tex.hx_spectral` | T | - | K:int[1,24], beta:lin[0,3], fmin:exp[0.5,32], fmax:exp[1,128], angle:lin[0,3.14159], spread:lin[0,1], seed:int[0,255], map:enum2, speed:scub[0,2], contrast:exp[0.5,8], gamma:exp[0.25,4], mode:enum3, ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | hops (inverse FFT synthesis) |
| 0x1A | `tex.hx_hexgrid` | T | t | kind:enum3, scale:exp[2,96], width:exp[0.005,0.3], glow:lin[0,1], falloff:exp[2,200], h:hue, s:lin[0,1], v:exp[0.01,4], asp:lin[0.5,3], angle:lin[0,3.14159], mode:enum3, pulse:lin[0,1] | hops |
| 0x1B | `tex.hx_chladni` | T | - | n:int[1,12], m:int[1,12], mix:lin[-1,1], sharp:exp[2,200], rep:int[1,8], morph:exp[0.01,2], asp:lin[0.5,3], fill:lin[0,1], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | hops |
| 0x1C | `tex.hx_sph` | T | - | l:int[0,8], m:int[-8,8], sharp:exp[1,100], tilt:lin[0,3.14159], spin:scub[0,1], contrast:exp[0.5,8], line:lin[0,2], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4], ch:hue, cs:lin[0,1], cv:exp[0.01,4] | hops |
| 0x1D | `tex.hx_beam` | T | t | count:int[1,4], az:lin[0,6.28319], el:lin[-1.5708,1.5708], spacing:lin[0,1.5], width:exp[0.002,0.2], halo:exp[1,40], length:exp[0.2,6.3], intensity:exp[0.05,20], drift:scub[0,0.5], phase:lin[0,6.28319], ch:hue, cs:lin[0,1], cv:exp[0.01,4], gh:hue, gs:lin[0,1], gv:exp[0.01,4] | hops |
| 0x1E | `tex.hx_stars` | T | t | density:exp[8,300], prob:lin[0,1], size:exp[0.02,0.5], bright:exp[0.1,20], bvar:lin[0,1], h:hue, s:lin[0,1], v:exp[0.01,4], huevar:lin[0,1], twinkle:lin[0,1], seed:int[0,255] | hops |
| 0x1F | `tex.hx_gabor` | T | - | freq:exp[0.5,8], cells:exp[2,64], angle:lin[0,3.14159], spread:lin[0,1], density:int[1,4], seed:int[0,255], asp:lin[0.5,3], contrast:exp[0.5,8], gamma:exp[0.25,4], speed:scub[0,3], ah:hue, as:lin[0,1], av:exp[0.01,4], bh:hue, bs:lin[0,1], bv:exp[0.01,4] | hops |
| 0x2D | `sdf.hx_pyramidfield` | S | - | pitch:exp[0.2,60], height:lin[0,2], fill:lin[0.3,1], jitter:lin[0,1], thick:exp[0.05,100], seed:int[0,255], axis:enum3, gap:exp[0.5,400] | hops |
| 0x8C | `post.hx_fft` | P | p | slope:lin[-1.5,1.5], f0:exp[0.01,0.5], bgain:lin[-1,3], fb:exp[0.01,0.5], bw:exp[0.005,0.3], oamt:lin[0,1], oang:lin[0,3.14159], chan:enum2, amount:lin[0,1] | hops |
| 0x8D | `post.hx_pca` | P | p | mode:enum2, amount:lin[0,1], mh:hue, ms:lin[0,1], mv:exp[0.01,4], s1:exp[0.01,0.5], s2:exp[0.01,0.5], s3:exp[0.01,0.5], ah:hue, tilt:lin[0,1], roll:lin[0,3.14159] | hops (Monge-Kantorovich linear) |
| 0x8E | `post.hx_dog` | P | p | s1:exp[0.5,16], s2:exp[1,32], gain:lin[-1,3], chan:enum2, amount:lin[0,1] | hops |
| 0x8F | `post.hx_histeq` | P | p | a:exp[0.3,10], b:exp[0.3,10], amount:lin[0,1], mode:enum2 | hops |

114 operators: 58 core, 43 extension ops (opcodes 0xA0-0xEF, `ops_ext/`) and 13 `hx_` ops from the part-by-part thread (0x18-0x1F, 0x2D, 0x8C-0x8F, `agenome/hops.py`), all installed on import; `AGENOME_EXT=0` and `AGENOME_HOPS=0` leave them out. The table is `agenome.ops.spec_markdown()` after that install.

## Variation operators (for the search)
param tweak (gaussian in byte space), op swap within a type, insert filter, delete node, rewire input, duplicate
node, typed subgraph crossover at a root slot. All followed by `repair()`. The search also spends `--tune` (default
0.25) of each generation on parameter-only steps around its best few genomes.

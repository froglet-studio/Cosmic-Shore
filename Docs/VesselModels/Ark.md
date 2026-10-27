# Ark (mothership) - model dossier

Built 2026-10-27 by `/vessel-model` from `Tools/Build/vessel_model/specs/ark.py` (Garrett asked for it the same day). It's a new hull with no artist file. No prefab draws it yet.
Contact sheet: `Ark_sheet.png`.

**Superstructure:** one valve of a scallop shell. Eight crowned rib plates (`shell.0`-`shell.7`) fan out from a hinge hub (`hub`), with a constant 5.4-unit groove between neighbours. Under them sits a lens core (`hull`), which has a domain waist band and a bridge window at the prow. Behind the hinge are three floating engine nacelles (`engine.L/C/R`). It is a hovering assembly: `assembly_report.py --floating shell.*,hub,omni.ring,omni,payload,engine.*` passes.

**Prism sockets (the ark's health):** each socket has one bone, named `socket.<tier>.<nn>`. The bone's head is at the socketed prism's centre, its tail runs along the prism's length, and its Z (roll) is the surface's up. Spawn a prism at the bone, scaled to 3 x 1.5 x 7 (across x up x along). Its top then lies flush with the rib edges. Each cradle is a U channel with an Engine-slot regen emitter in its floor. The cradle is skinned to its own socket bone, so Unity keeps the bone.

| tier | where | count |
|---|---|---|
| supershielded | innermost groove sockets, nearest the omni crystal | 7 |
| shielded | middle groove sockets | 6 |
| prism | outer groove sockets | 6 |
| danger | along the lip, one per plate | 8 |

**Omni crystal:** a faceted bipyramid (`omni`) in the Window slot, which reads as dormant glass. It floats inside a gimballed ring (`omni.ring`) above the hub. To activate it, swap its material.

**Payload:** a spear (`payload`) lying in the centre groove on two forked clamps (`payload.dock`, with regen emitters). The spear's head reaches past the lip. When the spear is taken, regrow it at `payload` relative to `payload.dock`.

**Shapes:** Charge stretches the spear and swells the crystal. Mass bulks out the core and plates. Space fans the shell wider. Time stretches the engines. The shapes do not move the socket bones.
**Takes:** `rest`, `clap` (the lip lifts about the hinge), `release` (the spear slides forward 14 units).

Size: 127 x 100 x 38 (Blender metres). 27k tris.

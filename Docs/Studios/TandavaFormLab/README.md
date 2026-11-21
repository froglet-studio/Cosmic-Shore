# Tandava Form Lab

The prototype rig for the Tandava arena hunt (`Assets/_Scripts/Controller/Arcade/TANDAVA.md`): every shipped body plan
drawn in 3D, a JS port of `TandavaDirectorCore` over the harness's food model, a simplified flock, and a click-flown dart
with two autopilot wingmates. Use it to look at a form, a pose (feed, the three coils, the Many-Headed rear/strike, the
Antlion gape/snap) or a whole hunt before touching the C#.

- **Live page:** https://claude.ai/artifact/7VC39yjjCMShsA7DasDRjf (version 9, the seventh pass).
- **Build:** `python3 Docs/Studios/TandavaFormLab/build.py` re-reads the 51 shipped plans
  (`Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_*.json`) into `plans.json` and writes
  `tandava-form-lab.html` beside it (both generated and git-ignored).
- **Headless check:** `FORM=3 node Docs/Studios/TandavaFormLab/lunge_check.js` (the Antlion; `FORM=1` the Many-Headed
  Serpent) runs three hunts per variant with a loitering pilot and prints each lunge's charge pose and how far off the
  jaws/heads closed. Run `build.py` first.

| File | What it is |
|---|---|
| `head.html` | the page shell, styles and HUD |
| `sim.js` | `TandavaSim`: the director port, the flock, the food model, the pilots (no DOM; runs under node) |
| `app.js` | the three.js view, input, labels, toasts and sound |
| `build_plans.py` | cuts the shipped plan JSON down to what the lab draws |

**What it is NOT.** The flock is a stand-in for `SwarmSortCore`. It does not model members, thinning or the well clip,
so the harness (`Tools/Build/swarm_core_harness`, mode `tandava`) is the measurement and the lab is the look. The lab's
bots are sharper than `AIPilot`, the dart is a point mass with a hull-like lateral grip cap, and there are no colliders.
What the lab flagged is recorded in `TANDAVA.md` §4.1 as items to check in the Editor (QA-TANDAVA-15/16), not as
defects.

The director port is kept in lockstep with the C# by hand. A director change lands in `TandavaDirectorCore.cs` first,
then in `sim.js`, and the lab must still reproduce harness T7 (unopposed it completes in about 210-240 s) before any
new flag is trusted.

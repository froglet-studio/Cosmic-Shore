# Amoebius session prompt — the Stoat and both wormhole pair styles

Paste everything below the line into a new Claude Code session started at the repository root, with
the branch based on `claude/peaceful-rubin-hhw49n`.

---

**Goal.** Make the Stoat vessel and BOTH wormhole pair styles testable in Amoebius (our .NET port under
`Port/`): a tester, or you through MCP, can fly the Stoat on a gamepad, create, inspect and swap the
two pair styles live, and see them drawn faithfully enough to choose between them.

**Branch and scope.** Work on a new branch from `claude/peaceful-rubin-hhw49n`, which is where the
Stoat (`VesselClassType.Stoat = 14`), its Slingshot race (`GameModes.Slingshot = 64`) and both pair
styles live. This is an ENGINE session: follow `Port/CLAUDE.md` § "The one rule" and "Who works where".
Edit `Port/` only, never `Assets/`. When the game itself is wrong (not an engine gap), do not fix it:
list it for a Unity PR on `claude/peaceful-rubin-hhw49n`, with file:line and how you saw it.

**Read first, in this order:** `/prisma` (`.claude/skills/prisma/SKILL.md`), `Port/CLAUDE.md`,
`Port/docs/ARCHITECTURE.md` §7 (rendering) and §13 (known gaps), `Port/docs/milestones.json` (the C2a
note on hand-written `.shader` files), `Docs/BLACK_HOLE.md` §0.1, §5.1 and §11–13,
`Docs/CRYSTAL_WORMHOLE.md`, `Docs/WORMHOLES.md`, `Assets/_Scripts/Controller/Vessel/R_VesselActions/STOAT.md`,
`Assets/_Scripts/Controller/Arcade/SLINGSHOT.md`, and `Docs/Studios/README.md`. The web studio
(`Docs/Studios/StoatFlightStudio.html`; live copy https://claude.ai/artifact/Busc3KW6DmVzbsiA2qxoHc) is
the reference for how each style should look and feel, and it already models the game's dual-stick mix.
If the user has recorded decisions in it, ask them to paste its "Copy log" output.

**The two styles, one switch** (`BlackHoleConfig.crystalPairs`, `Docs/BLACK_HOLE.md` §13):
- **Drift pair** (off, shipped): horizon holes. Paczyński–Wiita pull into a 90 u/s channel, owner-only.
  Shadow and Einstein ring on the attractor; the repulsor's lens diverges under a white-hot core.
  Prisms are emitted at the point reflection. Life: drift apart, stop, return, annihilate (4 s).
- **Crystal wormhole** (on, from `cece/charming-cerf-alf1j1`): smooth Plummer wells, the graded lens,
  and the felt pull in cruise units. Seamless mouths carry the slinger through by pure translation.
  Life: form 0.6 s, stand 0.05 s, annihilate 3.35 s along `CrystalWormhole.Curve`.
- The Stoat lays either one with LT/RT: squeeze and release, sized by the DEEPEST squeeze
  (`StoatSlingMath.Peak`), with the attractor on the pressed side. Live controls in the game are the
  Black Hole tool (B, runtime uGUI, with a **Pair style** button) and the dev console:
  `blackhole tool on|off | style drift|crystal | pair left|right | list | annihilate [s] | spawn …`.

**Work, in order. Prove each step with evidence (a screenshot path, a log line, or a number):**

1. **Baseline.** Check `prisma_tracks` and `prisma_board` first. Then `engine_build`, `engine_smoke`,
   `game_start`, and load `MinigameSlingshot`, then `Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity`.
   Record what already runs and what doesn't:
   - the Stoat spawning, and its procedural hull and lope;
   - `BlackHoleRegistry` and its Burst jobs (through Compat), and `GravityBody` prisms moving;
   - the vessel pull;
   - `CrystalWormhole` forming;
   - `WormholeMouth` transit.

   Expect engine gaps here: `RenderPipelineManager.begin/endCameraRendering`, `ScriptableRenderPass`
   with `RecordRenderGraph` (`BlackHoleLensPass`), a `CommandBuffer` drawing into the slices of a
   Tex2DArray `RenderTexture` (`BlackHoleSky`), `Shader.SetGlobalVectorArray` banks (the smooth-well
   lens and the `PrismGravityWarp` tides), and cameras rendering to textures (the mouths' exact view
   and panorama). Fix gaps in the engine, smallest first.
2. **A control surface an agent can drive.** If the control port and MCP cannot run the game's dev
   console, add a `console` command (`ControlServer.cs`) and a `game_console` MCP tool. Also add a way
   to read the registry's live state if `game_get` cannot reach static members. Done when this works
   from MCP: `blackhole style crystal`, then `blackhole pair left`, then `blackhole list`. The list
   must show an attractor and a repulsor with smooth wells (softening above 0), and `blackhole style
   drift` must switch back.
3. **Gamepad input for scripted tests.** Add analog stick and trigger verbs to `game_input` if they
   are missing. Done when a scripted LT squeeze that ramps 0 → 1 → 0 over about 10 frames slings
   strength 12 (the peak, not the last sample), and the same with RT lays the attractor on the right.
4. **Draw them.** Use hand translations under `Port/src/CosmicShore.Content/Shaders/Hand/`, keyed by
   guid, and post-pass stages, following the C2a route. In priority order:
   1. The `PrismGravityWarp` tide deform, spliced before the cradle in `BlockGraph` and
      `ExplodingBlockGraph`, reading the global bank, with the softened tide for smooth wells.
   2. The lens as a screen-space pass: the signed Schwarzschild trace from `BlackHoleLens.hlsl`
      (port its maths once to GLSL), the shadow, the source's white core (unbent impact parameter
      below 2.598 r_s), and the summed smooth-well deflection. Sample Amoebius's own HyperSea sky
      instead of `BlackHoleSky`'s faces if that is simpler. `Tools/Shaders/verify_black_hole_lens.py`
      lists the properties the trace must keep.
   3. The seamless mouth (`Wormhole.shader` with `_SoftEdge`, `WormholeSeamless.mat`). It is listed
      as Missing in `Port/docs/PARITY.md`.

   Done when side-by-side screenshots of one pair in each style, from the same camera, show what
   §13's table says, and the web studio's compare view is a fair visual reference.
5. **The tool in Amoebius.** Check that the runtime Black Hole tool (B) draws and works in Amoebius
   (uGUI): the Pair style button, the Pair buttons, the live rows and the Config view. The goal the
   user stated is to create, inspect and swap both styles while flying the Stoat. If the uGUI tool
   already does that in Amoebius, it is the deliverable. Add a Prisma-native panel or MCP tool only for
   what it cannot do, and say why.
6. **Fly it end to end.** Fly `MinigameSlingshot` with scripted gamepad input:
   - sling a drift pair: the shadow, ring and core are visible, the Stoat is thrown, and an AI vessel
     is NOT pulled (`BlackHoleVesselPull.TryGetPull` is zero for it);
   - prisms captured by the attractor come out of the repulsor (`ThroatTransitsTotal` rises);
   - switch to crystal and sling: the pair forms and spirals out, and flying into the attractor's
     mouth moves the Stoat to the repulsor by Δ in one frame;
   - pass a ring or two with each style;
   - compare the session report's frame times with no pair, a drift pair and a crystal pair.
7. **Close out.** `engine_test` and `unity_isolation_check` must be green. Update
   `Port/docs/milestones.json` and `Port/docs/ROADMAP.md`, and use `prisma_board_suggest` (with a
   "done when") for anything left. `game_stop`.

**Report back** in a short report:
- what works in Amoebius now, with screenshots;
- each engine gap you closed;
- each one still open, and why;
- the Unity-side bugs you found, as a list for a separate PR;
- which checks did not run.

Never accept the consent prompt on the user's behalf. Reuse a profile name to skip the first-run
prompts. Keep the defaults `COSMIC_SHORE_NET=off` and `COSMIC_SHORE_AUDIO=off`. Under xvfb the game
renders in software at a few frames per second, so plan your waits; use `headless: true` when you
need no pictures.

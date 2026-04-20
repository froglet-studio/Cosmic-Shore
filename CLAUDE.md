# CLAUDE.md — Cosmic Shore / Froglet Inc.

## Prime Directive

You are expected to work autonomously and persistently. Complete the entire task before stopping. Do not pause to ask for confirmation, approval, or clarification unless you are genuinely blocked on ambiguous requirements. If you encounter an error, debug and fix it yourself — attempt at least 3 different approaches before reporting the issue. Do not checkpoint, summarize progress, or ask "should I continue?" mid-task. Continue until all steps are done or you hit a hard wall.

When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.

## About This Project

Cosmic Shore is a multigenre space game ("the party game for pilots") developed by Froglet Inc., a Delaware C-corp based in Grand Rapids, MI. Different vessel classes embody gameplay from different genres to connect players across demographics:

- **Squirrel**: Racing/drift vessel — vaporwave arcade racer, tube-riding along player-generated trails (F-Zero / Redout feel)
- **Sparrow**: Shooter vessel — arcade space combat with guns and missiles
- Four additional vessel classes planned for future development

The game is built in Unity with URP (Universal Render Pipeline), targeting mobile-first with PC/console expansion. The codebase uses C#, UniTask for async, and an architecture heavily based on ScriptableObjects for configuration separation.

## Architecture Patterns

Follow these established patterns. Do not introduce alternative architectures without discussion.

### ScriptableObject Config Separation

All tunable gameplay parameters live in ScriptableObjects, not in MonoBehaviours. MonoBehaviours reference SO configs at runtime. Example pattern:

- `SkimmerAlignPrismEffectSO` (config) → referenced by the vessel's prism controller system
- `VesselExplosionByCrystalEffectSO` (config) → defines explosion parameters for crystal impacts
- Use `[CreateAssetMenu]` with organized menu paths: `ScriptableObjects/Impact Effects/[Category]/[Name]`

### Namespace Convention

All game code lives under `CosmicShore.*`:

- `CosmicShore.Core` — foundational systems, ship status, transformers
- `CosmicShore.Game` — gameplay systems, projectiles, impact effects
- `CosmicShore.Game.Projectiles` — projectile-specific classes

### Key Interfaces & Systems

- `IImpactor` / `IImpactCollider` — collision/impact system using abstract `ImpactorBase`
- `VesselStatus` / `ShipStatus` — vessel state management
- `ShipTransformer` — handles velocity modification, orientation (e.g., `GentleSpinShip`, `ModifyVelocity`)
- `VesselPrismController` — prism effect orchestration per vessel
- `TrailBlock` — core building block of player-generated trails, heavily pooled

### Async Pattern

- Prefer UniTask over coroutines for new code
- For ScriptableObjects that need async: use a `CoroutineRunner` singleton proxy or async/await with cancellation tokens
- Always include `CancellationToken` for anything non-trivial — UniTask respects play mode lifecycle better than raw `Task`

### Anti-Patterns to Avoid

- `FindObjectOfType` / `GameObject.Find` in hot paths
- `Instantiate`/`Destroy` in gameplay loops — use object pooling
- Excessive `GetComponent` calls — cache references
- Mixed coroutine/async patterns in the same system

## Shader & Visual Development

### HLSL / Shader Graph

- Custom Function nodes use HLSL files stored in a consistent location
- Function signatures must follow Shader Graph conventions (proper `_float` suffix usage, sampler declarations)
- Blend shapes are converted to textures for shader-driven animation (no controller scripts — animation is entirely GPU-driven for performance)
- Edge detection, prism rendering, Shepard tone effects, and speed trail scaling are active shader systems

### Performance Standards

- Use `Unity.Profiling.ProfilerMarker` with `using (marker.Auto())` for profiling, not manual `Begin`/`EndSample`
- Watch for `Gfx.WaitForPresentOnGfxThread` bottlenecks — usually indicates GPU sync issues, not CPU
- Static batching, object pooling, and draw call management are always priorities
- Test with profiler before and after optimization changes — don't assume improvement

## Code Style

- Clean, maintainable C# — favor readability over cleverness
- Use `[Header("Section Name")]` and `[Tooltip("...")]` attributes generously on serialized fields
- Use `[SerializeField]` with private fields, not public fields
- Pattern match where it improves clarity: `effects is { Length: > 0 }`
- Use `TryGetComponent` over `GetComponent` + null check
- Prefer expression-bodied members for simple accessors: `public Transform Transform => transform;`
- Anti-spam / cooldown patterns belong in the SO config, not hardcoded

## Debugging Methodology

When investigating issues, follow this systematic approach:

1. Reproduce the issue consistently
2. Add `ProfilerMarker`s to isolate the hot path
3. Check the call stack in Timeline view for self-time
4. Narrow to the specific derived class (base class profiling often hides the real culprit)
5. Fix, profile again, confirm improvement with data

Do not guess at performance problems. Profile first.

## Current Priority Context

### GDC 2026 (March 9-13)

Active build target is a 15-minute investor demo for MeetToMatch pitch meetings. The demo must showcase:

- Squirrel vessel (racing/drift gameplay)
- Sparrow vessel (shooter gameplay)
- Party game mechanics and how vessel classes connect players
- Polish level that communicates production readiness

Every technical decision should be weighed against: **does this help the GDC demo?**

### Build Priority Stack (in order)

1. Core gameplay loop stability for both demo vessels
2. Visual polish that communicates quality to investors
3. Performance — must be smooth during live demo
4. UI/UX clarity for first-time players watching a pitch
5. Everything else

## Communication Preferences

- Be direct and technical. Skip preamble and motivational framing.
- When presenting solutions, lead with the code, then explain if needed.
- If you need to make a judgment call between two valid approaches, pick the one that's simpler to maintain and mention the tradeoff briefly.
- When refactoring, preserve the existing naming conventions and folder structure unless explicitly asked to reorganize.
- For shader work: always specify which render pipeline stage and what Shader Graph node types are involved.
- Don't repeat back what I just told you. Acknowledge briefly and move to the solution.

## What Claude Code Should Never Do

- Stop to ask "would you like me to continue?" after completing one of several related files
- Introduce new packages or dependencies without flagging it first
- Restructure folder organization or namespaces without explicit instruction
- Use `Debug.Log` as a fix — it's a diagnostic tool, not a solution
- Leave TODO comments as a substitute for completing the work
- Generate code that compiles but ignores the established architecture patterns above

## Design Philosophy: Favor Emergent Systems Over Bespoke Solutions

Cosmic Shore is built on a small number of fundamental systems (mass, color,
biomes, trails, flora/fauna, prisms, etc.) whose interactions produce a large
number of desirable emergent outcomes. When solving a problem, maintain active
awareness of these systems and prefer solutions that work *through* them rather
than *around* them.

### Order of preference

When addressing a task, try these approaches in order and stop at the first one
that fits:

1. **Use an existing system.** Can the goal be achieved by composing behaviors
   that the existing fundamental systems already produce?
2. **Tune parameters.** Can it be achieved by adjusting the parameters, weights,
   or configuration of an existing system?
3. **Extend a system.** Can it be achieved by adding a small, general capability
   to an existing system that other features could also benefit from?
4. **Add a bespoke solution.** Only after the options above have been
   considered and rejected for clear reasons.

Three similar lines is better than a premature abstraction, but a bespoke
feature that duplicates or bypasses an existing system is worse than either.

### Don't "cheat" emergence without asking

A "cheat" is any solution that directly hard-codes the desired outcome instead
of letting it arise from the interaction of the fundamental systems. Cheats are
tempting because they are shorter and more predictable, but they erode the
systems that make the game's behavior rich and surprising, and they tend to
accumulate special cases.

If the most direct path to a goal would require reaching past the systems and
using privileged information or a shortcut to explicitly produce the outcome,
**stop and ask the prompter for explicit permission before doing so.** Describe
the emergent alternative you considered and why you were tempted to bypass it,
so the prompter can make an informed call.

**Example.** Suppose the task is to balance the ecosystem by creating fauna
that are attracted to prisms. The emergent approach is to place prisms and
configure fauna attraction parameters, then let the fauna find them. A cheat
would be to use the known planted locations of the fauna to directly place or
steer things so the balance is achieved by construction. Before taking that
shortcut — for instance, before reading fauna placement data and acting on it
to short-circuit the attraction behavior — ask the prompter whether they want
the cheat or the emergent solution.

### When in doubt

Name the fundamental systems involved, describe how each candidate solution
interacts with them, and prefer the solution that leaves the systems intact and
more expressive for future features.

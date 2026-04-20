# CLAUDE.md

Guidance for Claude when working in this repository.

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

# Tooling feedback log

What collaborators say after using, or deciding not to use, the tools under evaluation:
- **Amoebius:** the engine or the launcher.
- **The Vessel Studio:** the pages, the artifact, or its Sync / requests / decisions.

The team decides with this log ([`TOOLING_DECISIONS.md`](TOOLING_DECISIONS.md) §7, review on 2026-11-05).

**How sessions use it (CLAUDE.md "Tools under evaluation"):**
- At the end of a task that used or touched one of these tools, if the log has no row for that tool from the
  last three days, ask the person ONE short question: "Did the Vessel Studio help with this: keep, change or
  remove? Why, in a line?".
- Append their answer as a row, newest first, and commit it with the work.
- Never put words in their mouth: a row records only what the person said. If they do not want to answer,
  record nothing.
- The person is the human you are working with: their name from [`TEAM.json`](TEAM.json) (match their git
  email or ask), not "Claude".

| Date | Who | Tool | Task | Helped? | Keep / change / remove | In their words |
|---|---|---|---|---|---|---|
| 2026-10-15 | Yash | Amoebius | The lava-lamp fps regression: found by an Amoebius A/B of two commits (per-script, no editor), then confirmed in Unity's Profiler | yes | (not said) | "Anything running things in Amoebas is more efficient and performant than in editor." |
| 2026-10-15 | Yash | Amoebius, Vessel Studio | Raising the question | (undecided) | (undecided) | "This Amoebius development is in a grey area: we need to decide whether it will help us in future or not, and whether the Vessel Studio needs to be removed or not." |

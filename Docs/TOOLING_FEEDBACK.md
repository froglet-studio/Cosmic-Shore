# Tooling feedback log

What collaborators say after using, or deciding not to use, these tools (the decision is recorded in
[`TOOLING_DECISIONS.md`](TOOLING_DECISIONS.md) §6, 2026-10-19):
- **Amoebius:** the engine or the launcher.
- **The Vessel Studio:** the pages, the artifact, or its Sync / requests / decisions.

**How sessions use it (since 2026-10-19):**
- **Do not ask** whether a tool helped: Shombith asked sessions to stop. Record a row only when someone
  volunteers what they think of a tool.
- Append it as a row, newest first, and commit it with the work.
- Never put words in their mouth: a row records only what the person said. If they do not want to answer,
  record nothing.
- The person is the human you are working with: their name from [`TEAM.json`](TEAM.json) (match their git
  email or ask), not "Claude".

| Date | Who | Tool | Task | Helped? | Keep / change / remove | In their words |
|---|---|---|---|---|---|---|
| 2026-10-19 | Shombith | Amoebius, Vessel Studio | The style-B menu branch (Amoebius CI fix) | (decision, not a rating) | keep Amoebius; the studio is not part of the engine | "Amoebius is a long term goal where we completely abandon unity and move to that engine; currently it is not plausible to do that, of course, considering legal challenges and other problems as well, porting and everything. So please stop asking these questions and I will drive what's good for Amoebius. Vessel studio on the other hand is an artifact and it should not be a part of the engine; the engine should solely be focused on the game." |
| 2026-10-15 | Yash | Amoebius | The lava-lamp fps regression: found by an Amoebius A/B of two commits (per-script, no editor), then confirmed in Unity's Profiler | yes | (not said) | "Anything running things in Amoebas is more efficient and performant than in editor." |
| 2026-10-15 | Yash | Amoebius, Vessel Studio | Raising the question | (undecided) | (undecided) | "This Amoebius development is in a grey area: we need to decide whether it will help us in future or not, and whether the Vessel Studio needs to be removed or not." |
| 2026-10-18 | Garrett | Vessel Studio | Brittle Star tuning pass and the five-fold ring explosion (`cece/gallant-bell-eq7kul`); Claude ported both to the studio page and checked them there | (did not use it) | (no opinion from this task) | "Didn't use it" |

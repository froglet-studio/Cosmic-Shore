---
name: proven-win
description: Find, prove and hand over ONE categorical, cost-free improvement to the Cosmic Shore Unity project at a time - a change that beats the shipped version on a named machine-read metric (GPU ms, vertex instructions, build bytes, allocations, ms per event) in every ABBA round in real Unity on a real GPU, with output proven identical (null and negative controls), the project's gates green, and a blind A/B page a human signs off on. Use on "/proven-win", "prove a win", "find one clean improvement", "measure this change in real Unity", "is this stand-in really free", "A/B this shader/change for sign-off", or to take the next item off Docs/ProvenWin/WINS.md's runner-up list.
---

# Proven Win

Read `Docs/ProvenWin/PLAYBOOK.md` (the method) and `Docs/ProvenWin/WINS.md` (what has been proven, the runner-ups).
Then do these steps in order. Do not skip a control: a win without its null and negative control is a claim.

1. **Pick one candidate** by probability of a clean proof (PLAYBOOK §1). Name the metric, its unit and instrument
   before measuring. Write the two runner-ups down.
2. **Isolate** on the GPU machine: own worktree from `origin/bleeding-edge`, warm Library copied, never the person's
   open checkout (PLAYBOOK §2). Make variants A, A' (null), B (the change), C (deliberately wrong).
3. **Measure** with the bench (`ShaderABBench` for a material; a sibling bench with the same shape otherwise):
   three runs, null / change / negative control. Pass = A' identical and its timing gap small; C caught; B
   identical to A and faster in every round by more than the null gap (PLAYBOOK §3).
4. **Gates** on the branch with only B applied: `unity_refcompile`, `verify_unity.sh --filter <area>`,
   `.claude/skills/ship/gates.sh`.
5. **Human A/B**: publish the blind sign-off page from the real frames (PLAYBOOK §4); a "B worse" vote stops it.
6. **Record** a row in `WINS.md`, open the one-change PR only when the person says so, queue the area owner with
   `approvals.py add`, and say plainly what is unmeasured (phone GPU, other graphics APIs, frame-level weight).

---
name: launch-flow-liveops
description: Work on the Cosmic Shore launch for Live ops (UGS deploys, launch flags, the UGS environment per build) in its own chat - lists that platform's open launch blockers from the Launch Console (live checklist + pipeline records), READINESS.md and the launch PRs, says whether it is Go, At risk or No-go, and starts a plan to clear the blockers, then works the item you pick. Use on "/launch-flow-liveops", "Live ops launch", "what blocks Live ops", or a session started from the Launch Console's "Work on this in Claude" button on the Live ops panel.
---

# /launch-flow-liveops

Load `.claude/skills/launch-flow/SKILL.md` (the shared procedure) and follow it with **platform = `liveops`**. Its row in
`.claude/skills/launch-flow/references/PLATFORMS.md` (the "Live ops" row) names the checklist keys, pipeline components,
READINESS sections, launch PRs, the docs to read first and who decides. Start at §1 straight away; do not ask which
platform.

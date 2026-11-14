# /ship §5.6 Refresh the Fleet Codex in full

<!-- Moved word for word out of .claude/skills/ship/SKILL.md on 2026-11-13 (Shombith: "/ship is very slow; it does not need to read every line every time"). SKILL.md keeps the procedure; this file is read when `ship_triage.py` or the step says so. -->

Read when SKILL.md's step says so (its trigger matched this branch).


The Fleet Codex (https://claude.ai/artifact/JDvgFqu1bCfgcHVfCdXaA8, `/fleet-codex`) is the page the team reads to learn
what every vessel does: its abilities, level-5 upgrades and whether each is wired, scaling numbers, controls, cards
and docs. A ship that changes a vessel and leaves the codex stale has shipped half the change. Shombith, 2026-11-03:
"make sure /ship includes changes push to fleet codex artifact" - and "codex should also read from bleeding edge".

**When.** After §5.5, on a GO (and after a `/ship-tools` push), whenever the branch touched anything the codex reads:

```sh
git diff --name-only <merge-base>..HEAD | grep -E \
  'ElementalAbilityMaps/|Spacevessels/|SO_Class_|VesselClassType\.cs|ToyVesselRoster|R_VesselActions/|/Vessel/|AbilityIcons/|VesselHUD/|icon_kit/specs/|_SO_Assets/Games/|author_touch_profiles|fleet_codex|FleetCodex/'
```

No hit: one report line, "no codex surface touched". A NO-GO publishes nothing.

**How: always on bleeding-edge, never the feature branch alone.** `fleet_codex.py` refuses a tree that does not
contain `origin/bleeding-edge`. Build in a detached worktree of bleeding-edge with every shipped-but-unmerged branch
merged on top - this branch, plus each branch the published page already lists as pending and that has not landed:

```sh
git fetch origin bleeding-edge
git worktree add --detach "$SCRATCH/be" origin/bleeding-edge
# pending = this branch + the published fleet.json's "pending" entries still not in bleeding-edge
#   (Artifact read, path fleet.json; drop a branch once `git merge-base --is-ancestor origin/<it> origin/bleeding-edge`)
git -C "$SCRATCH/be" merge --no-ff --no-edit origin/<each pending branch>    # local only, never pushed
python3 "$SCRATCH/be/Tools/Build/fleet_codex.py" --out "$SCRATCH/codex" --files-json \
  --branch-label "bleeding-edge + <pending, comma-separated>" --pending <branch> [--pending <branch> ...]
cp "$SCRATCH/be/Docs/FleetCodex/index.html" "$SCRATCH/codex-index.html"
```

A pending branch that no longer merges cleanly onto bleeding-edge is left out (abort that merge) and named in the
report. Then generate and publish exactly as `/fleet-codex` §1 steps 2-5 (its `--render-domains` rule, `img/dom/*`
in its own publish call first, compare `contentHash`, publish to the SAME url with `root` and the printed `FILES`,
remove the worktree). The `--pending` stamp is what lets the daily refresh keep a
shipped branch's facts until its PR merges instead of reverting them overnight.

**Report:** the content hash, the tree read (the stamp), the pending list, and what changed for the vessels this branch
touched (L5 wired counts, new abilities). If the publish is refused, say so in one line; it never holds a GO.

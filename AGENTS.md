# AGENTS.md — read `CLAUDE.md`

**This file is a POINTER, not a copy. The project's instructions live in one place:
[`CLAUDE.md`](CLAUDE.md) at the repository root. Read that.**

Everything an agent needs is there: the prime directive, the LOCKED ecosystem invariants, the
architecture patterns and anti-patterns, the vessel / arcade / ecology contracts, the fundamentals
and the emergence rules, the documentation index, and the skills to route a task through
(`/ecology`, `/vessel`, `/fauna`, `/arcadegame`, `/arenagame`, `/refactor`, `/ship`, `/reorient`).

## Why this file is a pointer

It used to be a hand-made copy of `CLAUDE.md`, and by 2026-09-20 it had drifted **1,559 lines and
357 KB behind** it (3,572 lines / 403,987 bytes against 5,131 / 761,251) while still opening with
`CLAUDE.md`'s own title. The heading sets were identical and every body paragraph was older, so
the two files did not disagree visibly — they disagreed in the paragraphs nobody re-read.

What made that a defect rather than untidiness is **which** paragraphs were stale. A prose sweep
that correctly fixed three files after the LIT fundamental landed (`Docs/LIT.md`: an own-domain
explosion now LIGHTS its own mass rather than shielding it for two seconds) missed this one, so
`AGENTS.md:926` went on stating the retired rule — and that rule has food-web and
targeting-grid consequences, because shielded mass is not food and leaves the cell's fauna
targeting grids. An agent reading only this file would have reasoned from it.

The general rule, which `CLAUDE.md` states for a deleted SDK and applies here: **a copy with no
generator drifts, and the drift is invisible because both files look authored.** The fix is one
source of truth, not a more careful copy. Do not re-expand this file; if something genuinely
belongs to non-Claude agents and not to `CLAUDE.md`, add it *below* as a short delta and say why
it is not in `CLAUDE.md`.

## Delta: `.junie/skills` is a symlink to `.claude/skills`

Not in `CLAUDE.md` because it only matters to JetBrains Junie, which loads project skills from
`.junie/skills/<name>/SKILL.md` and never reads `.claude/skills` itself. **Edit skills in
`.claude/skills/` only.** `.junie/skills` is one committed symlink (`../.claude/skills`), so Junie
sees every skill, including new ones, the moment it lands. There is nothing to sync.
`python3 Tools/Build/check_junie_skills.py` runs in both CI workflows. It fails if the link ever
becomes a real directory again, and its failure message prints the commands that restore it.

It used to be a copy. Junie offers to import `.claude/skills` into `.junie/skills`, and that import
COPIES. The 2026-08-25 merge `def29f5e1` committed one such copy: the nine skills that existed
then, byte-identical to that branch's `.claude/skills`. Every later branch edited `.claude/skills`
alone. By 2026-10-08 the copies were 3,920 `diff -r` lines behind (asset-surgery 2,138, vessel 744,
ship 660), and a Junie session was following rules that `.claude/skills` had already retired. If
Junie offers that import again, decline it. The link already gives Junie everything the import
would copy.

A Windows checkout with `core.symlinks=false` turns the link into a one-line text file, so Junie
on that machine sees no skills. Enable symlinks (`git config core.symlinks true` plus Developer
Mode) rather than committing a real directory in its place.

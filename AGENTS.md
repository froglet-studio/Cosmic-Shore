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

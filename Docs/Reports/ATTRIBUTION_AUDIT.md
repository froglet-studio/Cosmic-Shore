# Attribution audit: did Yash's work take credit for anyone else's?

> **Verdict (2026-10-27): NO.** Across all 553 `origin/*` branches and the full history (16,295 commits), no
> commit, document line, area owner or team role moved credit from another collaborator to Yash. No commit of
> anyone else's was copied in under Yash's name. Every commit Yash pushed that another person wrote still names
> its real author. Two of Yash's design calls changed things in Garrett's area; Garrett is still credited in both
> (section 3).
>
> **Check it yourself:** `git fetch --unshallow origin` (once, on a shallow clone), `git fetch origin`, then
> `python3 Tools/Build/attribution_audit.py` (`--person <name>` audits anyone). It reads git and writes nothing.

**The same report as a page to share:** [Attribution Audit](https://claude.ai/artifact/5Z7fdnLhs7kqAvFXw85TFP).
**Asked by:** Yash, 2026-10-27 ("did I put my name on someone else's work, accidentally or intentionally?").
**Run by:** a Claude session on `Ys-bleeding-edge`, on a full (unshallowed) fetch of every branch.

## 1. What was checked

Most commits here are authored by Claude, so git authorship alone cannot show who drove a change. The audit
therefore reads both git authorship and the text where credit is written: quotes, owner fields and roles.

| # | Check | How | Result |
|---|---|---|---|
| 1 | **Re-authored commits** (someone's commit copied or cherry-picked with the author reset to Yash) | `git patch-id` of Yash's 999 commits against the other people's 2,999 | **0 against Yash.** The one match is the other way round: Yash's `73f6750851` (2025-10-16, CrystalManager / SnowChanger part 1) was re-applied by Shombith as `6de47f8142` the next day, under Shombith's own name. Normal. |
| 2 | **Others' commits pushed by Yash** | committer is Yash, author is another human | **1, author kept:** `b266e7ed83` (2026-10-10) is authored by Grok Bot (an automated agent) and only pushed by Yash. |
| 3 | **Name swaps in text** (a line naming Garrett, Caleb, Shombith, Iggy, Andrew or another collaborator rewritten to name Yash) | every `.md`, `.json`, `.cs`, `.py`, `.html`, `.js`, `.txt`, `.yml`, `.asset` diff on every branch | **0 credit swaps.** The 11 lines reported are one log entry: the AI-learning sweep's file noting that the Warpline hold was lifted (section 3, item 1). |
| 4 | **Area owners** (`Docs/Reports/areas.json`) | every version of the file | **0 changed to Yash.** No area has ever changed owner. |
| 5 | **Yash's role** (`Docs/TEAM.json`) | every version of the file | **1 change, and it narrowed Yash's role:** on 2026-10-26 "party / multiplayer (UGS)" became "party / multiplayer hardening (the UGS backend itself is Shombith's)". |

The name-swap check was proved to fire with a negative control: a planted "(Garrett, 2026-10-18)" →
"(Yash, 2026-10-18)" edit in a scratch repository is reported, while a regenerated table that still lists
Garrett is not.

## 2. One thing that looked wrong and was not Yash's

On 2026-10-24 and 2026-10-26, Shombith's "Heads tooling" line disappeared from `Docs/TEAM.json` (commits
`c49e61cab3`, `63fea94552`). Both are on Shombith's own anti-cheat branch `claude/vigilant-archimedes-euoz8z`
(merged by Shombith, #1562). That branch started from a copy of the file older than the line. The line was put
back in that branch's merge `b66075f5b5` (2026-10-26) and is in `Docs/TEAM.json` today.

## 3. Two design calls in Garrett's area (credit kept; worth telling Garrett)

These are not credit claims: Garrett is still credited in both. They are listed so nothing about them surprises
him.

1. **The Stoat's AI in Warpline** (`f442a7e0a7`, `8ebff7ee6c`, 2026-10-26). Yash's AI-learning sweep had put
   Warpline on hold "until they say so" while the Stoat's AI waited for Garrett's configuration (issue 1198).
   Garrett's configuration had already landed on 10-13 and 10-21. Yash's sessions lifted the hold at Yash's
   request and retrained the AI Stoat's **Warpline-only** numbers from Yash's races
   (`StoatDipoleConfig_Warpline`, `StoatSlingConfig_Warpline`). Garrett's own Stoat configs are unchanged, and
   both commits say "Affects: Garrett's Stoat / Warpline". Garrett's sign-off on lifting the hold is not on
   record.
2. **The Gibbon** (`6e8487298a`, 2026-10-18). Garrett's review (2026-10-17, `Docs/Studios/CONCEPT_FLEET.md` §9)
   chose a two-mode Gibbon on the Tether prototype. It was built (§8.2), and Yash then redirected it to a
   web-slinger (§8.3) that is credited to Yash, because it is Yash's design. Garrett's review, the two-mode
   version and the `tether` kind all stay on record. Garrett's sign-off on the redirect is not on record.

## 4. Re-running this report

`python3 Tools/Build/attribution_audit.py` takes a few minutes on the full history. Read each line of check 3
yourself: a log or table that mentions several people can trip it. Record any new run here with its date.

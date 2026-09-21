# Steam checkpoint — the revision series

There are **five** revisions of the Steam checkpoint. **All five are now in this repository** (Revisions 2 and 4 were both recovered on 21 Sep 2026). This file is the
index, and it exists because the series is the thing that drifts: each revision changed the
*destination*, and a runbook that cites "the checkpoint" without a revision number inherits
whichever one it happens to open.

**If you only read one, read Revision 5** — it is the current state and it corrects three of
Revision 4's claims. The live work board derived from the series is `Docs/STEAM_RELEASE_TASKS.md`.

> **The six-week window Revision 2 was written against closed on 11 September 2026.** Every revision
> after it measures a project that is past its own plan date, and Revision 5 states that first
> because no earlier revision did.

---

## The series

| Rev | Date | Destination | In repo? | File |
|---|---|---|---|---|
| **1** | 28 Jul 2026 | **Paid Early Access**, one app, `beta`/`default` branches | ✅ yes | `Docs/STEAM_EA_INVESTOR_CHECKPOINT.html` / `.pdf` |
| **2** | 31 Jul 2026 | **Invite-only Steam Playtest** — the pivot | ✅ **yes, as of 21 Sep 2026** | `Docs/STEAM_CHECKPOINT_REV2_PLAYTEST.pdf` |
| **3** | 10 Sep 2026 | Engineering readiness audit *against* Rev 2 | ✅ yes | `Docs/STEAM_CHECKPOINT_REV3_READINESS_AUDIT.html` / `.pdf` |
| **4** | 12 Sep 2026 | Re-run of the readiness audit — **three claims corrected by Rev 5** | ✅ **yes, as of 21 Sep 2026** | `Docs/STEAM_CHECKPOINT_REV4_READINESS_AUDIT.html` / `.pdf` |
| **5** | 21 Sep 2026 | Re-run; corrects three Rev 4 claims; states the elapsed window | ✅ yes | `Docs/STEAM_CHECKPOINT_REV5_READINESS_AUDIT.html` / `.pdf` |

---

## Revision 2 was missing for six weeks, and is now recovered

**Closed 21 Sep 2026.** Revision 2 was supplied by the owner and committed as
`Docs/STEAM_CHECKPOINT_REV2_PLAYTEST.pdf` (7 pages, 31 July 2026), exactly as the previous version
of this file asked. Its presence is verified rather than assumed: the file carries the 31 July date
and the `B2` **"Wwise audio init"** string recorded as an error below — which is also what confirms
it is the real document and not a reconstruction.

**What its absence had cost, recorded so the shape is not forgotten.** For six weeks the Playtest
pivot was only recoverable second-hand, through Revision 3's summary of it and through the work
board. The item IDs the board and every audit use (`A4`, `A5`, `A6`, `B2`, `B4`, `B7`, `C4`, `C7`,
`C8`, `D1`–`D6`, `E1`–`E10`, `F1`–`F4`) had **no in-repo definition**, so a reader could see what the
audits said *about* `B2` but never what `B2` said. That is the mechanism behind the error below, and
it is the argument for committing a checkpoint at the moment it is issued rather than circulating
it as a file: a document that governs a plan but lives outside the repository is one the repository
cannot be checked against.

**The general rule this leaves behind.** *A plan the work is measured against belongs in the tree
with the work.* The gap was found by `git log --all --diff-filter=A` on 2026-09-11 — only
Revision 1's and Revision 3's pairs had ever been added — and it was closed only when someone who
happened to hold the file was asked for it.

Note the recovered copy is a **PDF only**; Revisions 1, 3 and 5 carry an `.html` source beside the
render. There is no HTML for Revision 2 in the tree. Do not reconstruct one.

### Known error in Revision 2, correctable only here

Revision 2's item **B2** (the PC platform sanity pass) lists **"Wwise audio init"**. The project's
audio middleware is **FMOD**, and has been for the whole life of the shipped audio system.

Measured 2026-09-11:

- **17** files under `Assets/_Scripts` reference `FMODUnity`.
- **0** files anywhere in first-party code reference `AkSoundEngine`, `AkAudioListener` or
  `AkGameObj`. `Assets/Wwise/` survived from an earlier middleware evaluation and was **inert**.
  *(Update, 12 Sep 2026: that folder has since been **deleted** — `Docs/THIRD_PARTY_REGISTER.md` §0
  row 8. The measurement above stands as taken; there is now no Wwise in the tree at all.)*

A PC sanity pass written against Wwise would test nothing. Because Revision 2 is a PDF that is not
in the repository, the correction is carried where the work is actually executed from:
`Docs/STEAM_RELEASE_TASKS.md` (item **R5**, the B2 item) and `Docs/AudioSystem/FMOD_AUDIT.md` §0.
Note R5's **code** half closed on 11 Sep 2026; B2's audio step is part of the **manual** PC sanity
pass, which the board routes to **R1/R3**. That is who has to know the middleware is FMOD.

---

## Revision 1 is kept intact — deliberately

Revision 1 is a historical investor document. It has **not** been edited and must not be. The only
change ever made to it is an additive **supersession notice** on a new leading page of the HTML
(2026-09-11, insertion-only: 38 lines added, 0 removed), pointing forward to Revision 3.

**The `.pdf` sibling does not carry that notice.** It is a rendered artifact produced by
HeadlessChrome, and it was left byte-identical on purpose: re-rendering a document that went to
investors would silently re-flow every page of it through a different browser build, and a
re-render of the *unmodified* HTML was measured against the committed PDF and did **not** reproduce
it exactly (19,105 vs 19,096 glyph runs). So the PDF is trusted as shipped rather than rebuilt.

**If you do regenerate the PDF**, know two things first:

1. The notice sheet adds a page — **7 → 8**. The cover page has no headroom, so the notice is its
   own leading sheet rather than a box on the cover; this keeps the original cover rendering
   exactly as authored.
2. The page footers are **static text** (`Page 1 of 7`, …). They are authored per-section in the
   HTML and will not renumber themselves.

---

*Written 2026-09-11 by the documentation-drift sweep (`Docs/STEAM_RELEASE_TASKS.md`, item R8).*

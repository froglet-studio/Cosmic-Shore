# Steam checkpoint — the revision series

There are **three** revisions of the Steam checkpoint. Two are in this repository. This file is the
index, and it exists because the series is the thing that drifts: each revision changed the
*destination*, and a runbook that cites "the checkpoint" without a revision number inherits
whichever one it happens to open.

**If you only read one, read Revision 3.** The live work board derived from it is
`Docs/STEAM_RELEASE_TASKS.md`.

---

## The series

| Rev | Date | Destination | In repo? | File |
|---|---|---|---|---|
| **1** | 28 Jul 2026 | **Paid Early Access**, one app, `beta`/`default` branches | ✅ yes | `Docs/STEAM_EA_INVESTOR_CHECKPOINT.html` / `.pdf` |
| **2** | 31 Jul 2026 | **Invite-only Steam Playtest** — the pivot | ❌ **no — never committed** | — |
| **3** | 10 Sep 2026 | Engineering readiness audit *against* Rev 2 | ✅ yes | `Docs/STEAM_CHECKPOINT_REV3_READINESS_AUDIT.html` / `.pdf` |

---

## Revision 2 is missing, and that is a real gap

Revision 2 **exists** — Revision 3 is explicitly measured against it, and the work board cites its
item IDs (`A4`, `A5`, `A6`, `B2`, `B4`, `B7`, `C4`, `C7`, `C8`, `D1`–`D6`, `E1`–`E10`, `F1`–`F4`) —
but it has never been committed to this repository. Confirmed 2026-09-11 from
`git log --all --diff-filter=A`: the only checkpoint files ever added are Revision 1's pair and
Revision 3's pair.

**What that costs.** The Playtest pivot is only recoverable second-hand, through Revision 3's
summary of it and through the work board. The item IDs the board and the audit both use have **no
in-repo definition** — you cannot look up what `B2` says, only what Revision 3 and the board say
about it. That is the mechanism behind the `B2` error recorded below.

**If you hold a copy, commit it** beside the other two as
`Docs/STEAM_CHECKPOINT_REV2_PLAYTEST.{html,pdf}` and update the table above. Do not re-derive it
from Revision 3 — a reconstruction would read as the source document and be wrong in exactly the
way this file exists to prevent.

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

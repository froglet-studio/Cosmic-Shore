# Cosmic Shore — Style Guide source art

**The source art is not in this repository.** The studio style guide — Main
Colors, Additional Colors, Typography, Icons, Buttons, and three UI Elements
pages — lives outside version control. No JPGs will be committed here.

**Inside this repo, the transcriptions in this folder are authoritative.** They
are the record of what the art says, and they are what `STYLE_FOUNDATION.md` was
written against. If you need to check a detail and cannot reach the original
guide, this folder is the reference — not a placeholder for one.

## How this ranks against the spec

`Docs/STYLE_FOUNDATION.md` is a transcription of the guide plus the things the
guide does not cover (spacing, layering, motion, safe area, the numeric type
role). Where the two disagree **by accident**, the guide wins and the spec is
corrected — a transcription can drift.

**But some divergences are deliberate.** Design has resolved specific points
*against* the guide, and those resolutions are recorded in the spec with a
`*(Resolved — …)*` note. **A resolution outranks the art.** Do not "restore" a
guide detail that the spec explicitly retires; see §4 for the four current ones.

Anything else that looks like a mismatch goes in the design feedback queue in
`Docs/UI_REDESIGN_TASKS.md` — do not edit `STYLE_FOUNDATION.md` in passing.

## Pages

| Guide page | Feeds |
|---|---|
| Main Colors | §2 Main colours |
| Additional Colors | §2 Additional colours |
| Typography | §4 |
| Icons | §9 |
| Buttons | §10.1 |
| UI Elements — end-of-game headers, port side navigation, class selection nav | §10.9, §10.12, §10.13 |
| UI Elements — game configure, arcade explore cards, settings slider, settings toggle, leaderboard | §10.6, §10.7, §10.8, §10.10, §10.11 |
| UI Elements — text input, popups, currency bars, secondary tab nav, daily deals | §10.2, §10.3, §10.4, §10.5, §10.6 |

## Typography page — transcription of record

| Block | Content |
|---|---|
| Headings — Aldrich | Heading 1 (24) · Heading 2 (20) · Heading 3 (16) |
| Body — Aldrich | 16 px. "Normal type. Information text in the hangar, chat text in the port, descriptions in the store." |
| Text Emphasis | Different colours **or** *italics in the Chakra Petch font* |
| Buttons — Chakra Petch Semibold | 16 pt. "Text used on buttons is almost always in caps, **with the exception of the 'used' state of the 'request knowledge' button on the port**." A second button size is **12 pt** — the example is a mixed-case countdown, `Next request in 3:25:19`, with a Knowledge icon. |

**§4's six transcribed rows match this page exactly** — H1 24, H2 20, H3 16,
Body 16, Button 16, Button small 12.

### What §4 does with it — all four settled in v0.3.1

| Page says | Spec says | Why |
|---|---|---|
| Emphasis is colour **or** Chakra Petch italic | **Colour only** — italic retired | One emphasis channel; no italic face is installed |
| Buttons "almost always" caps, one Port exception | **Caps, unconditional** | The Port screen is cut from the overhaul, so the exception retires with it |
| Only H1–H3, Body, and two button sizes exist | Display, Body small, Data ×3 **kept, daggered `†`** | They are spec-authored additions with no guide backing, and §4 now says so |
| Button-small example is a live countdown in Chakra Petch | `<mspace>` applies to **any live-updating numeric in any face** | A countdown in the button face jitters exactly as an Aldrich score does |

The first two are **deliberate divergences** — the resolution outranks the art.
The last two are the spec absorbing what the page revealed.

### One trap

**The guide sheet spells the family "Aldritch". The real font is "Aldrich"** —
which is what the spec uses and what ~1,670 project references bind to. This is
a typo in the art, not a naming decision. Do not apply the "art wins" rule here.

# Cosmic Shore — Style Guide source art

The JPG pages in this folder are the **authoritative source** for
`Docs/STYLE_FOUNDATION.md` §9 (Icons) and §10 (Components), and for the colour
tables in §2.

**Where the written spec and these images disagree, the images win and the spec
gets corrected.** `STYLE_FOUNDATION.md` is a transcription of this guide plus the
things the guide does not cover (spacing, layering, motion, safe area, the
numeric type role). A transcription can drift; the art cannot. If you find a
mismatch, fix the spec — do not "fix" the art, and do not implement from the
spec's version of a disputed detail.

Raise the mismatch in the design feedback queue in `Docs/UI_REDESIGN_TASKS.md`
rather than editing `STYLE_FOUNDATION.md` in passing.

## Pages

| File | Guide page | Feeds |
|---|---|---|
| `01-colors-main.jpg` | Main Colors | §2 Main colours |
| `02-colors-additional.jpg` | Additional Colors | §2 Additional colours |
| `03-typography.jpg` | Typography | §4 |
| `04-icons.jpg` | Icons | §9 |
| `05-buttons.jpg` | Buttons | §10.1 |
| `06-ui-elements-headers-nav.jpg` | UI Elements — end-of-game headers, port side navigation, class selection nav | §10.9, §10.12, §10.13 |
| `07-ui-elements-configure-leaderboard.jpg` | UI Elements — game configure, arcade explore cards, settings slider, settings toggle, leaderboard | §10.6, §10.7, §10.8, §10.10, §10.11 |
| `08-ui-elements-input-popups-deals.jpg` | UI Elements — text input, popups, currency bars, secondary tab nav, daily deals | §10.2, §10.3, §10.4, §10.5, §10.6 |

## Status — art not yet committed

⚠ **The JPGs are not in this folder yet.** Every page has been supplied to a
session as an inline conversation image rather than as a file, so there were no
binaries on disk to commit. The table above is the agreed naming so the art can
be dropped in without renegotiating filenames. Nothing under `Docs/` is
gitignored (verified with `git check-ignore`), so the files add normally — no
`-f` needed.

Pages seen so far: Main Colors, Additional Colors, **Typography**, Icons
(supplied twice), Buttons, and three UI Elements pages.

## Typography page — transcription of record

The Typography page arrived after `STYLE_FOUNDATION.md` v0.3 was written, so
until the JPG lands this is the record of what it says. **It is source art and
therefore outranks §4.**

| Block | Content |
|---|---|
| Headings — Aldrich | Heading 1 (24) · Heading 2 (20) · Heading 3 (16) |
| Body — Aldrich | 16 px. "Normal type. Information text in the hangar, chat text in the port, descriptions in the store." |
| Text Emphasis | Different colours **or** *italics in the Chakra Petch font* |
| Buttons — Chakra Petch Semibold | 16 pt. "Text used on buttons is almost always in caps, **with the exception of the 'used' state of the 'request knowledge' button on the port**." A second button size is **12 pt** — the example is a mixed-case countdown, `Next request in 3:25:19`, with a Knowledge icon. |

**§4's Mobile @800 column is a faithful transcription** — H1 24, H2 20, H3 16,
Body 16, Button 16, Button small 12 all match the page exactly, and the emphasis
rule matches. Four things the page settles that the spec does not:

1. **The guide sheet spells the family "Aldritch"; the real font is "Aldrich".**
   The spec is right and the art has the typo — do not "correct" the spec to
   match the art here. (~1,670 project references use Aldrich.)
2. **Display, Body small, and the three Data roles do not exist on the page.**
   They are spec-authored additions, not transcriptions. That matters most for
   the Data roles, since they carry the whole `<mspace>` decision.
3. **The button caps rule has a documented exception** — the "used" state of the
   Port's "request knowledge" button is not caps. §4 and §10.1 both state caps
   unconditionally, so this rule is currently lost.
4. **Emphasis requires a Chakra Petch *Italic* face.** No task asks for one.

All four are logged in the design feedback queue in `Docs/UI_REDESIGN_TASKS.md`.

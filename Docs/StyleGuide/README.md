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

⚠ **The eight JPGs are not in this folder yet.** They were attached to the
session that wrote this README as inline conversation images rather than as
files, so there were no binaries on disk to commit. The table above is the
agreed naming so the art can be dropped in without renegotiating filenames.

Two things to know when adding them:

- **`03-typography.jpg` was not among the eight pages supplied.** The eight
  images were: Main Colors, Additional Colors, Icons (**supplied twice**),
  Buttons, and three UI Elements pages. There was no typography page, so §4's
  type scale currently has **no source art backing it** — it rests on the
  v0.3 spec text alone. Supply that page, or treat §4 as spec-authored rather
  than guide-authored.
- Nothing under `Docs/` is gitignored (verified with `git check-ignore`), so the
  files add normally — no `-f` needed.

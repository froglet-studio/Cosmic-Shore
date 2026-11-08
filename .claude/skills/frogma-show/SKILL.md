---
name: frogma-show
description: Show Frogma, the team's design board (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2), right away - one Artifact open call and a one-line answer, no reading, planning or checks. Use on "/frogma-show", "show me frogma", "open frogma", "open the design board", "show the board", "pull up the style guide". For anything more (what is new, adding a round, sections, comments) use /frogma.
---

# /frogma-show - open the board, nothing else

Fast on purpose. **One tool call, then one line.**

```
Artifact  action=open  url=https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2
```

Then answer in one line: the link, and that it opens on its launch tab (the **Style Guide** is the first tab; the
canvas remembers where each viewer was). Do not read the index, run `frogma.py`, re-arm watches or list what is new:
that is `/frogma` §1, and the person asked only to see the board.

- A tab named in the request ("show the Fleet Codex tab"): still just open it, and say which tab to click in the page
  bar. Switching the launch tab is a publish, which is `/frogma`'s job, not a quick look.
- The open fails: give the link and say to open it in the browser.

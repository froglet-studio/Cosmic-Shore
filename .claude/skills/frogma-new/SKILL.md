---
name: frogma-new
description: Open a NEW TAB (page) on Frogma, the team's design board - the one claude.ai Design canvas (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2, formerly "Froglet Design Board" and "Menu Redesign B") - for a new UI area or a new redesign pass. Asks the person for the tab's name when none was given, adds the tab, opens the board on it, and records it in the repo copy and Docs/MENU_REDESIGN_B.md; the rounds that follow go on it through /frogma. Use on "/frogma-new", "/frogma-new <name>", "/design-board-new" (the old name), "new page on the design board", "new tab on the design board", "start a fresh board page for <area>", "a new redesign tab". Never creates a second canvas.
---

# /frogma-new - a new tab on Frogma

The board is ONE canvas, **Frogma** (https://claude.ai/artifact/W8DBZaW6pu3L28egCjrKo2). Its pages are
**tabs**: **Menu Redesign** (`menu-redesign`) is the master tab and holds every earlier round (Rev 3 to Rev 40, each old
page a titled section on it), **Compute Console** (`compute-console`) holds Rev 38, 41 and 42, and every new area or
redesign pass gets a tab of its own, made here (the first was **Menu Redesign C**, 2026-11-02). A round never costs a
tab: it is a titled section on one (`/frogma` §3). The canvas allows 40 tabs.

The helper is `/frogma`'s: `python3 .claude/skills/frogma/frogma.py new-tab` (and `tabs` to list them).
Read `.claude/skills/frogma/SKILL.md` §1 first: the same bring-up, the same rules.

## 1. The name comes first

- `/frogma-new <name>`: the words after the command are the tab's name, as the person wrote them.
- No name given: **ask for it** before anything else, with `AskUserQuestion` (header "Tab name"). Offer two or three
  names that fit what they asked for (for example "Menu Redesign C", "Voyage", "HUD Redesign"); they type any other
  in "Other". Never make up a name and go on.
- A name that a tab already has is refused by the helper; say so and ask again.

The canvas's own page bar belongs to the Design canvas type and cannot be changed from the repo, so the naming
question is asked here, in the session, before the tab is made.

## 2. Make the tab

One message, in parallel: `Artifact open url=<board>`, `Artifact read url=<board> paths=["project/canvas.json"]
out_dir=<scratchpad>/board-live`. Then:

```
python3 .claude/skills/frogma/frogma.py new-tab --live <scratchpad>/board-live/project/canvas.json \
    --root <scratchpad>/newtab --name "<the name>"
python3 .claude/skills/frogma/frogma.py check <scratchpad>/newtab --no-node   (the index gate; no boards yet)
```

It adds `{"id": <slug of the name>, "name": <the name>}` to `pages`, sets `launch.page` to it so the board opens on the
new tab, and keeps every other key byte for byte. It refuses a 41st tab (fold old ones together with
`frogma.py fold`, the board owner's call) and a name that exists.

Publish ONLY the index:

```
Artifact publish url=<board> root=<scratchpad>/newtab file_path=<scratchpad>/newtab/project/canvas.json
```

A refusal ("you haven't viewed the latest version"): read `project/canvas.json` again, re-run `new-tab` on it, publish
again. A third refusal: stop and say so.

## 3. Record it

- `python3 .claude/skills/frogma/frogma.py pull --src <scratchpad>/newtab --version <the publish's version>`
  (the repo copy's index and README now list the tab).
- Add one row to the **Tabs** table at the top of `Docs/MENU_REDESIGN_B.md` (tab, id, what it is for, who asked, date).
- Commit with the round that first fills it when one follows in the same session; a tab made on its own is
  `docs(frogma): new tab <name>`.

## 4. Tell the person

The link (it opens on the new tab), the tab's name, and that the next round goes on it: `/frogma` with
`plan --tab <id>`. Nothing about the mechanism.

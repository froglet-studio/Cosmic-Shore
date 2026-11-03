---
name: froglet-skills
description: Show every Froglet skill in this repo (.claude/skills/) - its name, when it was created and by whom, the pull request that landed it, and what it is for - read from each SKILL.md and git history by froglet_skills.py, never typed by hand. Use on "/froglet-skills", "what skills do we have", "list the skills", "show me all the froglet skills", "which skill do I use for X", "is there a skill for X", "when was /<skill> made", "what is /<skill> for", "what are the newest skills", or before writing a new skill (to find one to extend instead).
---

# Froglet Skills — every skill, when it was made, what it is for

The skills under `.claude/skills/` are the team's packaged workflows. This skill lists them from the repo itself so
the answer is never stale: `froglet_skills.py` reads each `SKILL.md`'s frontmatter (`name`, `description`) and dates
it from git. It is a READER: it writes nothing.

## 1. Run it

```bash
python3 .claude/skills/froglet-skills/froglet_skills.py
```

That prints a markdown table, oldest skill first:

| Column | Where it comes from |
|---|---|
| Skill | the frontmatter `name` (invoke as `/<name>`) |
| Created | date of the oldest commit that touched the skill's `SKILL.md` |
| By | the person behind that commit (`Docs/TEAM.json`); for a Claude commit, the owner of the branch it landed from (`branch_prefixes`); else "a Claude session" |
| Landed | the first `Merge pull request #N` on `origin/bleeding-edge` that carried it, its date and who merged it |
| What it is for | the first sentence of the frontmatter `description` |

Options:

| Ask | Flag |
|---|---|
| newest first / by name | `--sort newest` / `--sort name` |
| the full description, last-changed date and file of every skill | `--full` |
| one or a few skills in full | `--skill ship,reorient` |
| "is there a skill for X" | `--find <word>` (matches name and description) |
| machine-readable | `--json` |
| the clone is shallow | `--deepen` (see §2) |
| prove the parser | `--self-test` |

## 2. Shallow clones

Cloud sessions start from a shallow clone, so git cannot see a skill's first commit if it is older than the clone's
cut-off. The script then says **"This clone is shallow (history starts <date>)"** and any skill dated on that day may
be older. Run with `--deepen` (it fetches every commit and tree but no file contents, about 0.3 GB, a minute or two)
before reporting dates. Do not report a date as "created" while that warning is printed.

## 3. Answer the person

- **"Show me all the skills"**: run the table and give it to them as it prints. Add one line on the newest few if
  that helps. If they want something to keep or share, offer to publish the table as a page in one line.
- **"Which skill do I use for X"**: run `--find <word>` with one or two words from their task, read the matching
  descriptions (`--skill <name>` for the full text) and name the one skill that fits, with its trigger in a sentence.
  When two overlap (e.g. `/ship`, `/ship-quick`, `/ship-deep`, `/ship-tools`), say which and why.
- **"When was /X made / what is it for"**: `--skill X`.
- **Before writing a new skill**: run `--find` on its subject first. Extend a skill that exists rather than adding a
  second one (CLAUDE.md, Team rules: look for what exists before building anything new), and check its owner with
  `python3 Tools/Build/who_works_where.py query .claude/skills/<name>`.

Never edit another skill from here. Its owner is whoever `who_works_where.py` names; tooling skills go through
Shombith (CLAUDE.md § Team).

## 4. What is and is not listed

- Listed: every folder in `.claude/skills/` with a `SKILL.md`. A new skill appears the moment its folder exists;
  there is no manifest to update. `.junie/skills` is a symlink to the same folder (`Tools/Build/check_junie_skills.py`).
- Footnoted, not listed: folders without a `SKILL.md` (`cosmic-shore-deck/` is the session-deck plugin,
  `Docs/claude/SESSION_DECK.md`).
- Not listed: Claude's built-in and account skills (`/code-review`, `/simplify`, the `anthropic-skills:*` ones).
  They are not in the repo and not the team's.
- A `SKILL.md` with no `name` or `description` is printed as a PROBLEM and the script exits 1: that skill is
  invisible to Claude's skill picker until it is fixed.

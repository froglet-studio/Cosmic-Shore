# UI build specs (/frogma-to-unity)

One file per picked Frogma board (or style-guide decision) built in Unity: `Docs/UI/specs/<id>.md`. The spec is what
the board decides, in tokens, and what it leaves open, as questions. It is written by
`python3 .claude/skills/frogma-to-unity/frogma_to_unity.py extract <board.dc.html> --id <id>` and finished by hand, and
`spec-check` is its gate.

## The format

Header lines:

| Line | What |
|---|---|
| `# Spec <id>: <title>` | the id is the file name |
| `Board:` | the board file on Frogma, its tab and the canvas version it was read at |
| `Status:` | `draft`, `approved (who, date: where)`, `built` / `in PR #N` |
| `Allowed overrides: N` | new scene overrides on existing shared-prefab instances the spec needs (normally 0) |
| `Generators:` | the generators the build runs, comma separated; `gate` runs each with `--check` |

Sections, all required, in this order: **Target**, **Regions**, **Components** (a table: component, role, tokens, type
role, spacing, sliver, Unity), **States** (rest, hover, focus, pressed, disabled, and any the board adds), **Text**,
**Icons**, **Motion**, **Devices** (one line each for PC, Phone, iPad, Gamepad), **Literals** (every colour the board
paints and its token, `| #RRGGBB | count | Token (web name) |`), **Questions** (`Q<n>.` the question, `Board:` what the
board shows, `Meanwhile:` what the build does until answered), **Build map** (change, file, prefab / scene / code,
shared?), **Differs from the board**.

Rules `spec-check` holds: every section present; a status and a board line; every hex anywhere in the spec appears in
Literals with a token or a question; every question has its board reading and its meanwhile; no section left as the
draft's `?`.

| Spec | Board | Status |
|---|---|---|
| [sg5-play-ready](sg5-play-ready.md) | Style Guide 5, the Play / Ready row | built, in PR |

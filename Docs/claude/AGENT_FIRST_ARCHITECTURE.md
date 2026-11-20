# Agent-first architecture: code is designed for the session that writes it

Yash, 2026-11-10: "all I want you is to use the best code practices that can benefit both you and us, but you as priority, as we dont read codes much, we want you to design architectures that benefits you 100percent"

**Status: Yash's rule for the code a session writes; proposed to Garrett, Caleb and Shombith, who have not answered
yet.** It changes no LOCKED rule and no owner's design. Where it and an area's own rule disagree, the area's rule wins
and the disagreement goes to that owner's `/approve` queue.

## Who "the session" is

A session remembers nothing from the last one. Every chat, every scheduled sweep and every subagent starts cold, with
a limited context window, and has to rediscover this codebase before it can change it. So "benefits the agent" means:
**the codebase explains itself to a newcomer every time, and a wrong change fails a check instead of reaching a
person.** The rules below are that sentence, taken apart. They are ordered by how much they cost when broken.

## The rules

1. **A rule that matters has a machine check.** A gate, a test or a `--check`, never only a paragraph. A cold
   session trusts a check that fails; it may never read the paragraph. When you write a rule, write its check in the
   same change, show it fail once (a negative control), and name that failure in the commit. A rule with no possible
   check is advice: say so where it is written.
2. **Locality: a change is possible by reading one folder.** One job per file, config beside the code that reads it,
   no file a session must read whole to change one thing. **A new file stays under 1,500 lines** and a file already
   over it does not grow: split it by job. *Gated (`size`).*
3. **One way to do each thing.** Before building, search for what exists and extend it (`CLAUDE.md` § Team says the
   same of tools, skills and docs). A second copy is the most expensive thing a cold session can meet: it cannot tell
   which one is real. When a second way is found, one is retired in the same change or a backlog row names both.
4. **No hidden links: the compiler sees every connection.** No member looked up by a string
   (`GetField("x")`, `Invoke("Tick")`, `SendMessage`, `Type.GetType("...")`); use `nameof`, a typed reference or an
   interface. No scene lookup (`Find*ByType`, `GameObject.Find`) where a serialized reference, Reflex injection or a
   SOAP variable can carry it. A link the compiler cannot see is one a rename silently breaks. *The string lookups are
   gated (`reflection`); the scene lookups are measured, not gated (106 sites on 2026-10-25, `REFACTOR_BACKLOG.md` M14:
   converting them needs a scene scope first).*
5. **Names say what they are.** File name equals type name; the folder says the domain; a renamed concept is renamed
   everywhere (no `Ship` that is a `Vessel`). A session's first move is a search, and a misleading name sends it to
   the wrong code without an error. *Gated for scripts an asset binds (`names`), where a wrong name also breaks the
   binding in Amoebius.*
6. **Behaviour you can repeat.** Randomness is seeded where its result is kept or compared, serialized names are
   stable (a rename carries `[FormerlySerializedAs]`), enum members carry explicit values, and generated outputs have
   a drift check. Then "nothing changed" can be proven instead of believed.
7. **An abstraction arrives with its second case.** No interface, base class or pattern "for later": it is one more
   file a cold session reads to learn it does nothing yet. Injection seams and test doubles are a second case.

## What this drops

Things that were kept for a human reader or a human typist and are not owed any more:

- **Essays in code comments.** One line of *why*; the history belongs in the commit and the doc.
- **Hand-written copies of what code states.** A table a script can print is generated, with a `--check`.
- **Leaving a wrong name because the rename is tedious.** A mechanical change proved by the compiler and the gates is
  cheap for a session (`REFACTOR_BACKLOG.md` M5, M12, M15 are the worked examples).

It does **not** drop: the LOCKED rules, the player-facing-text rule, the tooltips and headers on serialized fields
(those are for the person in the Inspector), or anything an owner asked for.

## The gate

`python3 Tools/Build/check_agent_first.py` (`--self-test`: 14 checks). Each rule is a ratchet over
`Tools/Build/agent_first_baseline.json`: what breaks it today is recorded and may only shrink.

| Check | Fails when | Baseline, 2026-11-20 |
|---|---|---|
| `size` | a runtime `.cs` file not in the baseline is over 1,500 lines, or a baselined one grows more than 40 lines | 40 files (largest: `Cell.cs` 4,226, `PrismSpatialIndex.cs` 3,649, `ArcadeGameConfigureModal.cs` 3,535) |
| `names` | a script some scene, prefab or asset binds declares no type named like its file | 1 (`MinigameHUDView.cs` holds `MiniGameHUDView`: case only, both engines bind it) |
| `reflection` | a file gains a member lookup by string | 28 lookups in 13 files |

- Scope: `Assets/_Scripts` and `Assets/FTUE`, without `Editor/` and `Tests/` folders.
- Fixed something? `--update` shrinks the baseline. It refuses to record a new violation.
- Negative control (2026-11-20): a planted `GetField("Empty")` in `TemplateText.cs` failed the gate, naming the file.
- **Not wired into CI or `/ship` yet.** That is Shombith's tooling; it waits in his `/approve` queue. Until then a
  session runs it before committing C#.

Already holding parts of these rules: `Tools/CI/validate_project.py` (`monobehaviour-name`, orphan and missing
`.meta`), `check_generated_assets.py` (a bound script must declare its class; serialized keys must be fields),
`check_enum_member_references.py`, `EnumIntegrityTests`, `check_using_directives.py`, and every generator's `--check`.

## Not machine-checked yet (rules 3, 6 and 7)

These are held by review, and by the measurements that found them: duplicate systems
(`Docs/Reports/DEAD_CODE_M8.md` is the last dead-code sweep), unseeded randomness, single-implementer interfaces. A
check for any of them is a welcome change; the shape is the one above (measure, baseline, ratchet, negative control).

# Cosmic Shore — Engineering Documentation

Canonical engineering reference for the Cosmic Shore party / presence /
network subsystems, plus cross-cutting infrastructure docs. This README
is the navigation index; each linked doc is self-contained.

## Layout 

```
Docs/
├── README.md                    ← you are here
│
├── PartySystem/                 ← the Relay-backed party-session layer
│   ├── ARCHITECTURE.md          locked design, investigation Q&A,
│   │                            error-handling matrix, exit criteria
│   ├── REFACTOR.md              active backlog + deferred items
│   │                            + per-commit revision protocol
│   ├── BUGS.md                  open bugs (B2, B5, B7; B3/B8/B9/B10 fixed)
│   ├── TESTS.md                 manual procedures (S1-S8)
│   ├── TODOS.md                 parking-lot items
│   ├── INVITE_ENHANCEMENTS.md   planning: in-party invite guard,
│   │                            panel-gated refresh, party-merge on accept,
│   │                            SOAP confirm-popup, invite chain (member-
│   │                            sent invites join the member's current party)
│   ├── UI.md                    party/friends UI surface: component
│   │                            inventory, invite UX flow, scene wiring
│   └── MPPM_SESSION_LOG.md      chronological MPPM session journal
│
├── PresenceSystem/              ← the lobby-only discovery layer
│   ├── ARCHITECTURE.md          locked design, ForceReset semantics
│   ├── REFACTOR.md              backlog for PresenceLobbyService
│   ├── BUGS.md                  open bugs (B1, B4, B6)
│   ├── TESTS.md                 manual procedures (P1-P6)
│   └── TODOS.md
│
├── NetworkDiagnostics/          ← the NetDiag overlay (cross-cutting)
│   ├── ARCHITECTURE.md          what the overlay does, where it's wired
│   ├── TESTS.md                 Tests A-E
│   └── TODOS.md                 deferred adoption + extensions
│
├── ScoringSystem/               ← in-game score HUD + final scoreboard
│   ├── ARCHITECTURE.md          both surfaces, data flow, per-mode table,
│   │                            target architecture (unified networked scoring)
│   ├── REFACTOR.md              sequenced backlog + ground rules
│   ├── BUGS.md                  open correctness issues (B1-B5)
│   └── TESTS.md                 manual procedures (T1-T10)
│
├── TournamentSystem/            ← session-level meta chaining the 3 domain games
│   └── ARCHITECTURE.md          load model, controller brain, standings,
│                                end-game flow, data + file index
│
├── ShuffleSystem/               ← "Shuffle" = display name of Tournament mode
│   └── ARCHITECTURE.md          pointer to TournamentSystem + a deferred list of
│                                planned Shuffle behavior deltas (NOT a separate mode)
│
├── ASSEMBLY_SPLIT.md            splitting the single-assembly monolith:
│                                the one-way extraction rule, the compile-timing
│                                protocol + baseline, phase-1 result, phase-2 plan
├── THREADING.md                 main-thread affinity rules
│                                (.AsMainThread() contract, MainThreadDispatcher)
├── SCENES.md                    scene inventory, game-mode reference,
│                                launch pipeline
├── UNITY_VERIFICATION_CHECKLIST.md  changes that landed without an in-editor
│                                pass — verify these when you next open Unity
└── CameraMigrationReview.md     camera system migration tracking
```

The `PartySystem/`, `PresenceSystem/`, `NetworkDiagnostics/`, and
`ScoringSystem/` folders share a consistent shape: ARCHITECTURE for current
state, REFACTOR for active backlog, BUGS for open issues, TESTS for manual
procedures, TODOS for parking-lot items (the Scoring System folds its
parking-lot into REFACTOR). The PartySystem also keeps a
chronological session journal because MPPM testing produces
session-scoped findings that benefit from a timeline view.

## How to read these for the first time

| If you want to … | Start with |
|---|---|
| Understand the party system | `PartySystem/ARCHITECTURE.md` |
| Understand presence vs party | `PresenceSystem/ARCHITECTURE.md` § "Why it's separate from the party session" |
| See known issues + their status | `PartySystem/BUGS.md` + `PresenceSystem/BUGS.md` |
| See what we're refactoring next | `PartySystem/REFACTOR.md` § "Sequencing" |
| Add an `.asmdef`, or know why tests live under `Editor/` | `ASSEMBLY_SPLIT.md` |
| See the next multiplayer TODOs / big-picture roadmap | `MultiplayerArchitecture/ROADMAP.md` |
| Run the manual smoke / stress tests | `PartySystem/TESTS.md` § "Smoke gate" |
| See the latest MPPM session findings | `PartySystem/MPPM_SESSION_LOG.md` |
| Understand the diagnostic overlay | `NetworkDiagnostics/ARCHITECTURE.md` |
| Understand the scoring system (HUD + end-game) | `ScoringSystem/ARCHITECTURE.md` |
| See scoring-system cleanup work / open issues | `ScoringSystem/REFACTOR.md` + `ScoringSystem/BUGS.md` |
| Understand the tournament meta-mode (chains the 3 domain games) | `TournamentSystem/ARCHITECTURE.md` |
| Find "Shuffle" (it's Tournament's card display name) | `ShuffleSystem/ARCHITECTURE.md` → `TournamentSystem/ARCHITECTURE.md` |
| Understand the threading rules | `THREADING.md` |
| Confirm changes that landed without an editor pass | `UNITY_VERIFICATION_CHECKLIST.md` |
| Find a scene | `SCENES.md` |

## Shared conventions

These apply across all three folders. They live here once; the
per-folder docs point back to this section instead of restating them.

### MPPM test convention

- All manual tests run in the Unity Editor under **Multiplayer Play
  Mode (MPPM)**. Each **VP** ("virtual player") is a separate Editor
  instance. "VP1" is the first / host VP; "VP2"–"VP4" are the other /
  joining VPs.
- Tests reference NetDiag log classes (`class=Offline`,
  `class=SessionGone`, etc.) — see `NetworkDiagnostics/ARCHITECTURE.md`
  for the classifier and `NetworkDiagnostics/TESTS.md` for the
  diagnostic procedures (Tests A–E).

### How we work bugs

- One bug at a time, in the priority order each `BUGS.md` lists.
- For each: confirm the root cause (capture a NetDiag log line where
  possible) → agree the approach → ship as its own commit with an
  inline risk table → update the bug's status (see "Status legend"
  below).
- Before touching the locked-design area (`HostConnectionService`,
  `PresenceLobbyService`, `PartySessionService`, `PartyInviteController`,
  invite services), read the relevant `ARCHITECTURE.md` first. **Do not
  reintroduce LAZY / on-first-invite session creation** (see "Locked
  design" below).

### Per-commit refactor protocol

The per-commit risk gate, commit cadence, and the 6-step "read the
source fresh" revision protocol that governs every party/presence
refactor commit live canonically in `PartySystem/REFACTOR.md`
(§ "Per-refactor commit cadence" + § "Per-commit revision protocol").
Presence-side refactors follow the same protocol.

## Status legend (used in BUGS.md files)

| Marker | Meaning |
|---|---|
| 🔴 | Open — root cause not understood, or fix not yet attempted |
| 🟡 | Partially mitigated — workaround / symptom-suppression landed; root cause persists or full retest pending |
| 🟢 | Fixed — root cause addressed; needs verification only |
| ⚪ | Deferred — known issue, not actively worked |

## Locked design — the most important rule

The party / invite / lobby system uses **EAGER per-user Relay**: every
authenticated player hosts their own Relay-backed party session from
the moment they enter `Menu_Main` (the "Always InParty" model). **Do
not reintroduce LAZY / on-first-invite Relay creation.** The
shutdown-and-recreate cascade it caused is the root of every recurring
party-invite bug. If a future bug appears to argue for lazy creation,
re-examine the root cause through the lens of
`PartySystem/ARCHITECTURE.md` "Unbreakable exit criteria" first.

See `PartySystem/ARCHITECTURE.md` § "Locked design" for the full set of
locked decisions.

## Cross-references from code

Inline code comments throughout `Assets/_Scripts/Controller/Party/` and
related files reference these docs by specific section (e.g.
"see Docs/PartySystem/ARCHITECTURE.md Q4"). When refactoring, keep the
cross-reference up to date so the docs and the code stay in sync.

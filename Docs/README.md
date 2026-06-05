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
│   ├── BUGS.md                  open bugs (B2, B3, B5, B7)
│   ├── TESTS.md                 manual procedures (S1-S8)
│   ├── TODOS.md                 parking-lot items
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
│   ├── README.md                what the overlay does, where it's wired
│   ├── TESTS.md                 Tests A-E
│   └── TODOS.md                 deferred adoption + extensions
│
├── THREADING.md                 main-thread affinity rules
│                                (.AsMainThread() contract, MainThreadDispatcher)
├── SCENES.md                    scene inventory, game-mode reference,
│                                launch pipeline
└── CameraMigrationReview.md     camera system migration tracking
```

The `PartySystem/`, `PresenceSystem/`, and `NetworkDiagnostics/`
folders share a consistent shape: ARCHITECTURE for current state,
REFACTOR for active backlog, BUGS for open issues, TESTS for manual
procedures, TODOS for parking-lot items. The PartySystem also keeps a
chronological session journal because MPPM testing produces
session-scoped findings that benefit from a timeline view.

## How to read these for the first time

| If you want to … | Start with |
|---|---|
| Understand the party system | `PartySystem/ARCHITECTURE.md` |
| Understand presence vs party | `PresenceSystem/ARCHITECTURE.md` § "Why it's separate from the party session" |
| See known issues + their status | `PartySystem/BUGS.md` + `PresenceSystem/BUGS.md` |
| See what we're refactoring next | `PartySystem/REFACTOR.md` § "Sequencing" |
| Run the manual smoke / stress tests | `PartySystem/TESTS.md` § "Smoke gate" |
| See the latest MPPM session findings | `PartySystem/MPPM_SESSION_LOG.md` |
| Understand the diagnostic overlay | `NetworkDiagnostics/README.md` |
| Understand the threading rules | `THREADING.md` |
| Find a scene | `SCENES.md` |

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

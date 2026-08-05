<div class="sec-eyebrow">Part I · Overview</div>

# Executive summary

::: lead
Cosmic Shore is a real-time multiplayer space game. Its online stack is built on **Unity Netcode
for GameObjects** for in-game replication and **Unity Gaming Services (UGS)** for everything around
the match — identity, discovery, party formation, and transport. This document explains how those
pieces fit together, the decisions that shaped them, and the bugs we fought to make party play
feel seamless.
:::

The defining idea is a **two-level session model**. A lobby-only **Presence Lobby** lets every
signed-in player discover others and exchange invites cheaply, while a Relay-backed **Party Session**
carries the actual networked gameplay. Crucially, every authenticated player **eagerly** hosts their
own party session the moment they reach the main menu — the "Always-InParty" model — so an invite is
a simple *join*, never a fragile *create-then-hand-off* dance.

On top of that sit two more load-bearing rules: **single-writer SOAP** data flow (one service owns
each piece of shared state; everyone else reads through ScriptableObject events), and a strict
**main-thread affinity contract** (`.AsMainThread()`) at every UGS / Netcode `await` to stop
off-thread callbacks from crashing Unity. Player and vessel spawning is **server-authoritative** and
flows through one Netcode + SOAP pipeline for menu, AI, and gameplay alike.

<div class="kpi-row">
  <div class="kpi"><div class="num">2</div><div class="lbl">session layers — Presence Lobby (discovery) + Party Session (Relay gameplay)</div></div>
  <div class="kpi"><div class="num">5</div><div class="lbl">UGS services — Auth · Sessions · Lobby · Relay · Friends</div></div>
  <div class="kpi"><div class="num">7</div><div class="lbl">party lifecycle states in a single validated state machine</div></div>
  <div class="kpi"><div class="num">8</div><div class="lbl">“unbreakable” exit criteria gating every commit</div></div>
</div>

The party subsystem was hardened through a **17-commit refactor** that decomposed a 2,000-line
orchestrator into one state machine plus nine focused services behind interfaces. The result is a
system designed to be *unbreakable* under adversarial conditions — network drops, client crashes,
fast input, concurrent invites — with every failure path classified and every catch block mapped to
an explicit recovery action.

::: insight Why this is worth reading
Most of the hard problems here are not Unity-specific — they are the problems of any distributed,
event-driven, async system: where state lives, which thread you are on when a remote callback fires,
and what happens when two views of "who is in the party" disagree. The fixes are reusable lessons.
:::

**How the rest reads.** Part I continues with the mental model — the system context, the two-level
architecture, the player journey, the key decisions, and a highlight reel of the five most
instructive bugs. **Part II** is the engineering reference: each subsystem in full, the complete
B-series bug catalogue, the resilience matrix, the threading model, tests, and diagnostics.

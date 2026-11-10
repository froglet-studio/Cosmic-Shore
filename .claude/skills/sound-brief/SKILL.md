---
name: sound-brief
description: Audit, brief and wire the SOUND of Cosmic Shore - every FMOD EventReference slot (238 of 277 were empty on 2026-11-07), what the code expects of a slot (a one-shot or a started-and-stopped instance), what the FMOD Studio project says about the event (does it loop, is it in the built banks), the banks per platform, and the brief a sound designer works from. Use on "sound", "sfx", "audio", "FMOD", "EventReference", "no sound when", "silent", "boost is silent", "wire a sound", "sound for the <ability>", "brief for Charles", "empty sound slots", "mobile banks", "music event", or any file under Assets/_Scripts/Controller/Vessel/Audio, System/Audio, Controller/FX/FmodSafe.cs, or the FMOD project under "Cosmic Shore/". The brief is the deliverable; the event is the audio owner's.
---

# /sound-brief: every slot audited, every empty slot briefed

## 0. The bar, in one paragraph

Every sound is an exposed, editable `EventReference` field, shipped empty, never a temp event: an unwired slot is a
visible TODO, a temp event is one nobody ever finds (the LOCKED convention, `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`).
A looping event belongs in an instance slot (started, stopped, released), a one-shot in a one-shot slot: a looping
event fired as a one-shot leaked one immortal instance per boost and made boost silent (`CHARLES_TASKS.md` C2); a
one-shot in a loop slot plays once and drives nothing (C4). The language a sound follows is
`Docs/AudioSystem/SOUND_LANGUAGE.md` (DRAFT: a sound names the act and the element, never the domain). The audio
owner authors in FMOD Studio; a session audits, briefs and wires.

## 1. Read before you touch

- `Docs/AudioSystem/SOUND_LANGUAGE.md` (the draft language, what exists, the act vocabulary, the slot rules, mobile).
- `Docs/AudioSystem/FMOD_AUDIT.md` (the 2026-09-02 findings: the slider trap, the leaked loop, Live Update) and
  `CHARLES_TASKS.md` (the audio owner's open items; the generated block at its end is this skill's).
- `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`, `Docs/ElementalAbilitySystem/SILENCE.md`.
- `Assets/_Scripts/Controller/FX/FmodSafe.cs` (TryCreateInstance, StopAndRelease, Attach), the vessel audio
  controllers under `Controller/Vessel/Audio`, `System/Audio/AudioSystem`.
- Who to ask: Charles (FMOD Studio, the banks), Yash (the Meerkat's sounds, the held-vessel bed), Shombith
  (licences, `Docs/LAUNCH/READINESS.md` #10, the mobile banks before the mobile launch).

## 2. The pipeline

```
python3 Tools/Build/sound_slots.py                  # a table per owner: field, expectation, event, loops?, banked?
python3 Tools/Build/sound_slots.py --owners         # owners with their slot / empty / filled counts
python3 Tools/Build/sound_slots.py --empties Meerkat
python3 Tools/Build/sound_slots.py --check          # the gate: C2 / C4 mis-wirings, unbanked, unknown, no banks per platform
python3 Tools/Build/sound_slots.py --charles        # the generated block at the end of CHARLES_TASKS.md
python3 Tools/Build/sound_slots.py --self-test
```

The real tree on 2026-11-07: 13 failures. Three one-shot events in loop slots (the Goby's, Squirrel's and
Thresher's `boostLoopEvent` wired to `.../Skim`: C4), seven events authored in Studio but not in the built banks
(`Build/GUIDs.txt` is a 10-28 export: the music, the Dolphin's drift, the Rhino engine, the Thresher's Morningstar
loop among them), one event renamed or deleted in Studio (`Morning Star Right trigger`), and no banks for Android or
iOS while the project builds Desktop only. The `--self-test` has one assertion still open (the expectation of a
field that flows into another settings object); the scan, the FMOD readers and the gate run.

## 3. The round (one owner per round)

1. **Audit**: `--empties <owner>`; read the owner's abilities (`element_ability_table.py <Vessel>`), executors and
   animation takes for the durations.
2. **Brief**: for each empty slot one paragraph under `Docs/AudioSystem/briefs/<OWNER>.md`: the slot (asset:field),
   the trigger, the act in one line, the duration measured from the code, one-shot or instance and its parameters,
   the element colouring from the language, two sibling events in the banks to match, and what it must never be.
   Order by what a player hears first (engine, boost, skim, hit, the ability acts).
3. **Charles authors** in FMOD Studio and rebuilds the banks (`--check` must see the event banked).
4. **Engineering wires** the field (a `[SerializeField] EventReference` per ability, per trigger; the instance or
   one-shot path the brief named), `--check` green.
5. **The owner tests** in the editor; LESSONS appended (`references/LESSONS.md`, to create with the first round).

## 4. Rules that keep being relearned

- The three audio sliders were field-of-view sliders: binding one saved full volume (FMOD_AUDIT 1.0).
- A looping event fired as a one-shot leaks an immortal instance; boost was silent and logged once a session (C2).
- A one-shot in a loop slot plays once and drives nothing; the `Boost Amount` parameter never moves (C4).
- Live Update must be off in a shipped build (a wedged socket hung the editor's play exit).
- The VCAs route no bus; volume is applied per instance until Charles assigns them (C1).
- `GUIDs.txt` is an export, not the banks: an event can be authored and still unbanked.
- Banks are per platform: a mobile launch with Desktop banks has no sound.

## 5. Wiring and hand-off

- A new sound is a new `EventReference` field on the ability, trigger or emitter, shipped empty and named in the
  brief; never a path in code, never a placeholder event.
- The generated block in `CHARLES_TASKS.md` is the audio owner's work list; the hand-written C1-C9 above it stay his.
- Decisions waiting (`Docs/SKILL_TRAINING_PLAN.md` D5): briefs only, or synthesised reference stems too; the
  mobile banks and the licences before the mobile launch.

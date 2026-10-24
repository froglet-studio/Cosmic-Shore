# Team tasks

The team's hand-off tickets live on **GitHub Issues** (label `team-task`) since 2026-10-24; this file no longer holds
them. Rules: [`Docs/TEAM.md`](TEAM.md) rule 9.

- **The board:** the Froglet Tasks page, https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f
- **From a session:** the `/team-tasks` skill, or `python3 Tools/Build/team_tasks.py list`
- **On GitHub:** issues labelled [`team-task`](https://github.com/froglet-studio/Cosmic-Shore/issues?q=label%3Ateam-task)

A task a branch still writes below the marker in the old format is filed as a ticket by the next sweep
(`.github/workflows/team-tasks.yml`); prefer `/team-tasks add`.

<!-- team-tasks -->

### T-20261024-7fc8 · open · test · Menu_Main
- **Task:** Play-test Garrett's palette and attention pass (claude/new-session-76hnyc) in the editor: no element colours or panel rims anywhere in the menu or HUD; PLAY/JOIN/ACCEPT lime, UPGRADE/UNLOCK/CONVERT/SPEND gold, DELETE MY DATA fire, Spectate/Invite/Main menu neutral; the selection blue only on the picked tile or open tab; the lattice, orb and bonds hold still; the leaderboard podium lime/white/neutral; the in-match ability row has no element colour or lava motion; the scoreboard rows are plain hairlines
- **Done when:** Every item in Docs/UNITY_VERIFICATION_CHECKLIST.md 'Garrett's palette and attention pass' is ticked or filed as a bug
- **Who can take it:** Shombith (Menu_Main owner) or Yash (offered); Garrett for the look
- **Added:** 2026-10-24 by a Claude session (`claude/new-session-76hnyc`)
- **See:** Docs/UI_COLOR_RULES.md

### T-20261024-6756 · open · decision · Menu_Main
- **Task:** Friends orb polish: the orb lost its orbiting particles in Garrett's attention pass (2026-10-24) but has not had a full polish pass. Shombith has more direction to give; remind him and do the pass from it
- **Done when:** Shombith's direction for the orb is recorded in Docs/MENU_REDESIGN_B.md and built
- **Who can take it:** Shombith (owner, has the direction)
- **Added:** 2026-10-24 by a Claude session (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md 61.1; Tools/Build/author_ui_lattice.py PRESENCE

### T-20261016-cfc3 · done · decision · Vessel HUD
- **Task:** Review the rev-3 lava-lamp ability row (element-coloured plates/edges/keycaps; petals untouched) against the HUD's colour vocabulary - Mass blue / Time teal sit near the petal ladder's blue / green; retune AbilityLockupStyle colours or lavaAlpha if they compete
- **Done when:** Keep / retune / turn off decided on AbilityLockupStyle.asset
- **Who can take it:** Garrett (vessel HUD owner)
- **Added:** 2026-10-16 by Claude for Shombith (`claude/new-session-76hnyc`)
- **See:** Docs/claude/VESSEL_HUD_AND_ELEMENTS.md (Four-Icon row note), canvas Rev 3 R3Hud boards
- **Done:** 2026-10-24, Garrett answered 2026-10-24: elements carry no fixed colour in the UI; the lockup's elementColour and lavaMotion are off (Docs/UI_COLOR_RULES.md, claude/new-session-76hnyc).

### T-20261017-35e3 · done · test · Menu_Main
- **Task:** Play-test the rev 6 presence orb and party molecule in the editor: the orb bottom-left (glow, turning ring, one dot per player online, 'N online' / 'FRIENDS'), pressing it opens the party panel, it sits over the four windows without covering their cards; in a two-device party each member shows on the orb's inner ring with a bond in their domain colour; the panel draws the party as a molecule (leader in the middle, bonds to seated pilots, violet '+ Invite a friend' tiles).
- **Done when:** Every step of Docs/UNITY_VERIFICATION_CHECKLIST.md 'Rev 6 step 4: presence orb + party molecule' passes (no pink shaders, no canvas rebuild from the orb in the Profiler) or the failures are reported in this task.
- **Who can take it:** Shombith (Menu_Main owner) or Yash (offered: party / multiplayer); a second device for step 4
- **Added:** 2026-10-17 by Claude (claude/new-session-76hnyc) (`claude/new-session-76hnyc`)
- **See:** Docs/MENU_REDESIGN_B.md §45.4
- **Done:** 2026-10-24, Superseded 2026-10-24: the orb no longer turns or carries dots and its bonds are solid (Garrett's attention pass); its look is part of the new palette play-test task.

### T-20261024-ae82 · done · dev · Amoebius
- **Task:** Amoebius does not compile the game since bleeding-edge 98c25995b9 (2026-10-22): Assets/_Scripts/Utility/OrthographicLens.cs uses UnityEngine.Rendering.VolumeFrameworkUpdateMode and Camera.GetVolumeFrameworkUpdateMode / SetVolumeFrameworkUpdateMode (URP extension methods), which Port/src/CosmicShore.Engine/Rendering lacks (CS0246 at OrthographicLens.cs(32,44)). Add the enum (EveryFrame, ViaScripting, UsePipelineSettings) and the two camera methods. Until then the perf-daily 'amoebius' job is red on every branch carrying that file (seen on PR #1201).
- **Done when:** engine_build of the player on bleeding-edge compiles OrthographicLens.cs, and perf-daily's amoebius job is green
- **Who can take it:** Shombith (the Amoebius engine)
- **Added:** 2026-10-24 by Yash's session (`Ys-bleeding-edge`)
- **See:** PR #1201 comment; .github/workflows/perf-daily.yml
- **Done:** 2026-10-24, Fixed on claude/new-session-76hnyc (PR #1491): VolumeFrameworkUpdateMode, Get/SetVolumeFrameworkUpdateMode, UpdateVolumeStack, UniversalAdditionalCameraData.volumeStack/requiresVolumeFrameworkUpdate, plus GravitySensor, TouchControl.startTime and Vector3.RotateTowards in Port/src/CosmicShore.Engine; dotnet build Port/src/CosmicShore.Player -c Release has 0 errors.

# Team tasks

The team's hand-off tickets live on **GitHub Issues** (label `team-task`) since 2026-10-24; this file no longer holds
them. Rules: [`Docs/TEAM.md`](TEAM.md) rule 9.

- **The board:** the Froglet Tasks page, https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f
- **From a session:** the `/team-tasks` skill, or `python3 Tools/Build/team_tasks.py list`
- **On GitHub:** issues labelled [`team-task`](https://github.com/froglet-studio/Cosmic-Shore/issues?q=label%3Ateam-task)

A task a branch still writes below the marker in the old format is filed as a ticket by the next sweep
(`.github/workflows/team-tasks.yml`); prefer `/team-tasks add`.

<!-- team-tasks -->

### T-20261022-0351 · open · decision · Crew vessels
- **Task:** The Pistol's aimed jump is off (aimedJump on GobyPistolConfig), so Time's base card 'Quick Jump' (the jump's cooldown) drives nothing. Decide what Time does on the Pistol, or turn the jump back on; then refresh the class card's description in author_goby_assets.py (it still says the Goby flies on two thumbs and the Pistol launches into clusters).
- **Done when:** Time's card names a live mechanic and the class description matches the kit
- **Who can take it:** Garrett
- **Added:** 2026-10-22 by a Claude session (cece/cool-shannon-aulgpe) (`cece/cool-shannon-aulgpe`)
- **See:** .claude/skills/duo-vessel/references/designs/goby-and-pistol.md (top section, Time)

### T-20261023-a6e1 · open · decision · Crew vessels
- **Task:** Should a person's Goby show, in the world, when its next dart will perch (900+ u from the last perch and the Pistol's island queue not full)? Today a dart that falls short just dashes with no cue.
- **Done when:** Garrett decides yes (and how: a light or shape in the world, per the LOCKED world-not-screen rule) or no
- **Who can take it:** Garrett
- **Added:** 2026-10-23 by a Claude session (cece/cool-shannon-aulgpe) (`cece/cool-shannon-aulgpe`)
- **See:** .claude/skills/duo-vessel/references/designs/goby-and-pistol.md (2026-10-23 movement pass, [read])

### T-20261024-3791 · open · test · Crew vessels
- **Task:** Fly the Pistol rebuilt from the Tiny Wings study (Docs/PISTOL_TINY_WINGS_STUDY.md; Toybox > Vessel Changer > Goby and Pistol, dart 900+ u, double tap the small view). Judge it against Tiny Wings side by side: the hills (every one a different size, level crests and troughs, the island starting with a dive and ending on a ramp), the heavy dive (hold on a downslope and it rockets; hold on an upslope and it dies), the launch off a crest when you let go, landings that keep speed along the slope, the pace (one to two screens a second), the camera (shrimp near the left, zoom out when you fly high). Say which of these is still off.
- **Done when:** Garrett says the ride reads as Tiny Wings, or names the number (the study's §3 table maps each feel to its field)
- **Who can take it:** Garrett (feel call); Yash (offered) for a phone run
- **Added:** 2026-10-24 by a Claude session (cece/cool-shannon-aulgpe) (`cece/cool-shannon-aulgpe`)
- **See:** Docs/PISTOL_TINY_WINGS_STUDY.md §3; .claude/skills/duo-vessel/references/designs/goby-and-pistol.md (2026-10-24)

### T-20261024-5282 · open · decision · Crew vessels
- **Task:** The Tiny Wings study's next steps for the Pistol (Docs/PISTOL_TINY_WINGS_STUDY.md §4): (1) fever for the pair - three perfect slides in a row feed the den or the ring; (2) speed coins in the island's troughs (crystals or the Goby's marks); (3) a sound and a world effect on the perfect slide and the launch; (4) a clock on the Pistol. Decide which to build and in what order.
- **Done when:** Garrett picks and orders them
- **Who can take it:** Garrett
- **Added:** 2026-10-24 by a Claude session (cece/cool-shannon-aulgpe) (`cece/cool-shannon-aulgpe`)
- **See:** Docs/PISTOL_TINY_WINGS_STUDY.md §4

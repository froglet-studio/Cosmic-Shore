# Voyages: the outside sources and what in them is out of date

**Status: AUDIT, 2026-10-28.** Garrett: "a lot of these docs and ideas are outdated. we want to get our docs
consistent up to date and ready to build". The repo docs are the current design: [`VOYAGES.md`](VOYAGES.md),
[`../Pilots/ARTIFACTS.md`](../Pilots/ARTIFACTS.md) and [`../Pilots/CALLSIGNS.md`](../Pilots/CALLSIGNS.md). This file
lists every outside source read for them, what still holds, what is out of date, and what is still open. Where a
source and the repo disagree, the repo wins unless the row says the point is open.

## The sources

| Source | Where | Age |
|---|---|---|
| FigJam *How players get, do, and learn things* | figma.com/board/D0I4I9BYyhU8bwVsZn8SYR, sections *Information Architecture for Early Access*, *for Open Beta*, *for Full Release* (an *Archive* section is older still) | undated |
| Figma *Cosmic Shore for Alpha*, page *UI for vLater* | figma.com/design/fjZtY2LkKpbVEku4H4dyum, node 2281:37673 (the Port faction boards) | 2024 |
| Notion *Mission Overview* | Design Documents / Mission Design Documents | edited 2026-05-01 |
| Notion *Cosmic Shore — Initial Onboarding* | Design Documents | edited 2026-10-06 |
| Notion *Ark Design Document* | Design Documents | edited 2026-10-07 |
| Notion *Pilot Design Document* | Design Documents | edited 2026-04-22 |
| Notion *Faction Design Document* | Design Documents | edited 2026-04-02, almost empty |
| Notion *Episode stories* | Lore | edited 2026-04-02 |

## Still holds (and is now in the repo)

| Idea | Source | Repo |
|---|---|---|
| Three domains, faction types inside each domain, many factions of each type | Onboarding, Mission Overview | `VOYAGES.md` §6.2 |
| Ruby notoriety grows with missions, decays, and raises the bounty on you | Mission Overview | §6.2 (bounties) |
| Pilots carry a callsign and a faction, with a backstory that grows with key events | FigJam (Ark, Crew), Pilot Design Document | §3.4, §6.2.1 |
| Pilot search by vessel or faction type with omnicrystals, then recruit one of three | FigJam (Port, Ark) | §3.4 |
| Promote a pilot to captain to change faction | FigJam (Ark) | §6.2.1 |
| Accept or reject an emissary into your crew; factions grant free emissaries by rank; an emissary opens a bunk | FigJam (Home), Ark Design Document | §6.2.1 |
| Primary and ally factions | Figma Port boards | §6.2.1 (confirmed by Garrett, 2026-10-28) |
| Four saved squads, each slot's vessel, element, pilot and artifact | FigJam (Ark, Voyages) | §3.4 |
| The Voyages screen: pick from three missions, estimated time, difficulty, reward range, the lore before it, the main track, episodes | FigJam (Voyages) | §3.4 |
| Faction chat, clout, daily faction leaderboard, promotions (member, team leader, general, faction leader) | FigJam (Port), Figma Port boards | §6.2.1 (ranks open) |
| The Ark's bunks, bought with omnicrystals; the crew lives aboard | Ark Design Document, FigJam | §2 |
| The Ark as "the persistent player representation in the hyper-sea", named, with cosmetics (banner, skins) | Ark Design Document, FigJam (Ark, Decor) | not yet; see open questions |

## Out of date (do not build from these)

| Source says | Current decision | Who decided |
|---|---|---|
| **Missions** as the fourth location, "future scope, not in development" (Onboarding, FigJam Early Access) | **Voyages** replace missions as the fourth location, the culminating experience | Garrett, 2026-10-28 |
| Base missions per domain: Ruby **Explore**, Gold **Escort**, Jade **Protect** (Mission Overview) | **Jobs** your pilots drum up at port for their own faction: courier, hunt, defence, pirate tip, bounty. Escort is dropped | Garrett, 2026-10-28 |
| Missions have **nine difficulty levels** (Onboarding) | Intensity **1 to 4**, the game's one scale (FigJam's Missions screen already says 1-4) | the game's intensity ladder |
| **Sport** is a Gold faction type (Mission Overview) | Sport and UGC live in the **Arena** (the same page's own framework section says so) | Mission Overview itself |
| Each pilot "carries a unique ability" (Onboarding); "crew of three pilots, one per domain" (Pilot Design Document) | A pilot is a **unique person layered on a shared callsign**; a crew holds one of each callsign; the crew grows past three | Garrett, 2026-10-27 and 2026-10-28 |
| Pilots make a "qualitative change to vessel classes tied to keywords" and have **compatible vessels** (Pilot Design Document, FigJam Hangar) | A pilot grants a **sight** on LB, and **any vessel seats any pilot** | Garrett, 2026-10-27 (`CALLSIGNS.md`) |
| Artifacts "compatible with this class of vessel", a **quantitative change** (FigJam Hangar) | An artifact is a **payload transformation**, and no artifact names a vessel class | Garrett, 2026-10-27 (`ARTIFACTS.md`) |
| The Ark is the **Caddis**; when its omnicrystal is captured the team **loses and is dropped from the instance**, then pays a **rescue fee to Jade** (Ark Design Document) | The Ark falls when its **last hull prism** goes; everything aboard goes to the squad that felled it, or spills if fauna did | Garrett, 2026-10-27 (`ARTIFACTS.md` §5) |
| Squad slots: 2 free, up to 6 bought (Mission Overview) | Four saved squads (FigJam, newer) | open: see below |
| Episode 1 is a **sports comedy with Base Battle** (Onboarding) | Episode 1 in the game is **"The Hypersea"**, the quest track in the Voyage window | Shombith, 2026-10-25 (`MENU_REDESIGN_B.md` §71) |
| "Six initial classes" (Onboarding) | Many more classes ship in the Hangar (`Docs/claude/ABOUT_PROJECT.md` § Vessel Classes) | the shipped roster |
| The "XP track" (FigJam Early Access, Profile) | The **main story track** (FigJam Open Beta and Full Release) | the FigJam board's later sections |

## Open: sources and the repo disagree, or the repo is silent

1. **One Ark per account or one per squad?** The Ark Design Document says one Ark per account, permanently named,
   the player's identity in the HyperSea. `ARTIFACTS.md` says one Ark per squad of three. Both hold if a squad sails
   in **its leader's Ark** and the other players' Arks stay home. Is that it?
2. **Can artifacts be traded?** The FigJam board lets members **post artifacts to faction chat** and **bid on
   artifacts that appear in chat**. `ARTIFACTS.md` says artifacts are found on voyages, never bought, and an Ark's
   fall is the only theft. Keep a faction auction, or drop it?
3. **Faction missions on a daily ticket.** The Figma Port boards give one faction-mission ticket every 24 hours. Do
   jobs cost a ticket, or are they free and limited only by your crew and the news?
4. **Knowledge impart.** The Figma Port boards let a member request knowledge of a vessel's ability and other
   members impart it. Does it survive, and what does knowledge buy now that elements carry upgrades?
5. **Squad slots.** Four saved squads (FigJam) or 2 free and up to 6 (Mission Overview)?
6. **The faction types themselves.** Mission Overview's lists (hunter, pirate, explorer, activist, thief; merchant,
   bounty hunter, gatherer, contractor; aristocrat, researcher, preservationist, first responder, priest) are the
   only full list. Are they current?
7. **The Ark's own systems in the Ark Design Document:** speed from your domain's mass share in the cell, and power
   drawn only from fauna in the current cell. Keep them as Ark rules for voyages?

## How to keep this current

When a decision lands, change the repo doc it belongs to and move its row here from *Open* to *Out of date* (or
delete it if no source disagrees any more). The outside sources are not edited from the repo; how they are brought
in line is Garrett's call.

# The HyperSea: world, vocabulary and copy

> **What this is.** The one place the world is written down and the one place player-facing copy
> lives. Anyone writing a store page, a trailer script, a mode description, a captain line, a
> menu label or a devlog writes from here. If two pieces of copy disagree, this file is right and
> the other one is stale.
>
> **Why it exists.** The game shipped thirteen modes, eight flyable vessels, a toybox and an
> ecology, and described itself four different ways depending on who was asked. The four things
> do not unify at the mechanics layer, which is why every attempt to compress them into one
> sentence about genre produced a compromise. They unify as **places in one world**. That is the
> frame this file holds.
>
> **The voice already existed.** Twenty-four captain lines and eleven vessel descriptions were
> authored years before this document. They are quoted throughout and they are the standard. This
> file collects and extends them; it does not replace them.
>
> Adopted 2026-09-11. Supersedes the tagline and positioning sections of
> `Docs/MarketAnalysis/MARKET_GAP_PLAN_2026.md` §4.

---

## 1. The premise

This is the world statement. It is the top of every pitch, the first thing a new hire reads, and
the spine of the trailer.

> Between the stars there is a sea.
>
> Not empty space. A living medium, thick with light, where things grow and graze and die. Fly
> through it and you leave a wake of solid matter behind you, and that wake is the only permanent
> thing anyone makes out here. Everything in the HyperSea is building one, breaking one, or
> eating one.
>
> You are a pilot. Your vessel is not a costume. A Dolphin does not fly anything like a Rhino,
> and taking a new one out is learning a new body. There are crews out there who have made a
> whole philosophy out of how they move.
>
> The Shore is where you launch from.

**The three facts that premise commits us to**, and which every other piece of copy must respect:

1. **The sea is alive.** Flora grow, fauna graze, populations crash. Not decoration, not a timer.
2. **A wake is matter.** What you leave is solid, permanent until something removes it, and
   removable only by an active force. Nothing decays on a clock.
3. **A vessel is a body.** Eight fly today and no two move alike. Switching is relearning.

---

## 2. The four destinations

The canonical vocabulary. These are the words used in the UI, in copy, in design discussion and
in code comments. They replace "toybox", "party games", "competitive modes" and "missions".

| Place | What it is | The promise | What it is **not** |
|---|---|---|---|
| **The Shore** | Freestyle sandbox and toys | Nothing here is trying to beat you | Not a tutorial, not a mode, never scored |
| **The Arcade** | Short party games, up to four | Somebody is going to lose and it will be funny | Not balanced, not ranked |
| **The Arena** | Balanced competitive multi-vessel play | Bring your best vessel. So did they | Not where you learn, not forgiving |
| **Voyages** | PvEvP out in the HyperSea with friends | Out here nothing is arranged for you | Not a campaign, not a solo story |

Vessels are the constant. They are not a fifth destination. They are the thing you take to all four.

### The Shore

> The Shore is where pilots tie up.
>
> There are things to play with here and not one of them is keeping score. Grow a reef and watch
> it eat your own trail. Paint something enormous out of light. Take a vessel you have never
> flown and find out what it does to your hands. Change your colours. Change the world you are
> floating in, or wander off into open water and come back when you feel like it.
>
> Nothing on the Shore is trying to beat you, and nothing here ends.

**Writing rule.** The Shore is deliberately not a game and copy must never imply otherwise. No
objectives, no progress, no "unlock", no "complete". If a sentence about the Shore would still be
true of a level, rewrite it.

### The Arcade

> Crews have always played games in port.
>
> The Arcade is where you and up to three friends pick something short and ridiculous and settle
> who is worst at it. Hoops. Races. Demolition. A ball that is also a crystal that is also,
> somehow, yours. Matches are quick, the rules fit on one line, and sooner or later somebody is
> getting knocked into the wildlife.
>
> Nobody has ever asked for a rematch politely.

**Writing rule.** The Arcade sells laughs, not fairness. Never promise balance here. Comeback
assistance is on by default in the Arcade and that is a feature, said out loud.

### The Arena

> The Arena is the same sea with the jokes taken out.
>
> Everybody brings a vessel, every vessel moves completely differently, and the whole thing comes
> alive in the second the roster locks and you see what you are up against. A Rhino with a sword
> is not a problem a Sparrow solves the same way. Neither of them solves a Serpent at all.
>
> Balanced rules. Real stakes. No hidden help.

**Writing rule.** The Arena is where "no hidden help" is a promise we keep in code: comeback
assistance forced off, bot difficulty and count shown, no result reaching a board that had either.
Do not write Arena copy that cannot survive a player reading the rules.

### Voyages

> Past the Shore the sea stops arranging itself around you.
>
> A Voyage takes you and your friends out into the HyperSea, against whatever is living in it and
> whoever else is out there wanting the same thing you want. You will not meet the whole galaxy
> at once. Nobody does. Ships meet, ships part, and what happened out there comes back as news.
>
> The sea changes because of what crews did in it. Including yours.

**Writing rule.** Voyages are the future and copy must say so plainly wherever they appear. Never
imply a Voyage is playable before it is. See §8.

**Why this fiction is honest about the technology.** The shared galaxy runs one instance at a
time with information exchanged between them and pivotal events broadcast. In persistent-world
language that is a compromise to be explained away. In sea language it is simply how a sea works:
nobody shares a room with an ocean, they share the water, and ships carry news. The architecture
and the fiction agree, which is why we can promise it without hedging.

---

## 3. Vessels

**How to write a vessel.** One sentence on how it moves, one on what that lets you do to the
world, and nothing about statistics. The existing descriptions are the standard:

> **Dolphin.** Thread the narrowest needles at the highest speeds to build max charge, then drift
> sideways into a crystal to release one of the biggest blasts in the HyperSea.
>
> **Squirrel.** Get in your flow and stay there. Hard decisions are for other classes. You're
> here to bounce around and ride your speed tubes, leaving a wake of creation and destruction.

**The fleet, honestly.** Eight vessels fly today. Four have complete ability maps. Copy should say
"eight vessels" on the page and let the Early Access Q&A carry the completion state. Do not claim
eleven; three are unbuilt.

| Vessel | Flies today | Map complete | How it moves, in one clause |
|---|---|---|---|
| Squirrel | yes | yes | flow and drift, rides its own speed tubes |
| Sparrow | yes | yes | the classic arcade flyer, guns and rockets |
| Dolphin | yes | yes | needle-threading charge, sideways release |
| Scarab | yes | yes | analog throttle and juke, rolls a ball it made |
| Rhino | yes | no | smashes with its face, swings a sword |
| Manta | yes | no | soars wide, lays the biggest mass in the sea |
| Serpent | yes | no | one thumb, seeds walls out of what it steals |
| Urchin | yes | no | spikes that chain, rides and projects track |
| Grizzly | partial | no | gunner |
| Termite | no | no | drones that shrink mass and build |
| Falcon, Shrike | no | no | unbuilt |

### The captain voices

Each vessel carries four captains, one per element, each a single line of philosophy. Twenty-four
are authored. Twelve were `[Flavor text placeholder]` and are drafted below in the established
voice, ready to paste into the assets.

The voice: first person, one sentence, wry or menacing, always about how this pilot moves. Never
about numbers.

**Grizzly** (gunner, blunt, comic menace)

| Element | Line |
|---|---|
| Charge | "I don't hold a grudge. I hold a charge. Same result." |
| Mass | "They keep telling me to pick on someone my own size. There isn't one." |
| Space | "I don't need to see you. I need to know roughly where you were." |
| Time | "I'm not slow. I arrive later, on purpose, when it costs you more." |

**Termite** (drones, collective, patient, faintly unsettling)

| Element | Line |
|---|---|
| Charge | "One of me is a nuisance. The rest of me is a problem." |
| Mass | "I don't take it all at once. I take it all eventually." |
| Space | "Everywhere you aren't, I already am." |
| Time | "Give me a minute. Then give me all the rest of them." |

**Urchin** (spines, chain reactions, rides and lays track)

| Element | Line |
|---|---|
| Charge | "Touch one. Go on. See what the others do." |
| Mass | "I'm mostly spines. The rest is opinion." |
| Space | "If there's no road I'll put one down. It only has to last as long as I'm on it." |
| Time | "I don't chase. I wait on the line you already committed to." |

**Also to fix.** `SO_Class_Grizzly` and `SO_Class_Urchin` currently share the identical
description, "Don't mess with me or I will shoot. I will shoot anyway, but don't mess with me."
It is a copy-paste. Keep it on Grizzly, where it fits. Urchin needs its own:

> **Urchin.** Fire a ring of spikes that sets off every spike it touches, then ride the wreckage.
> When there is nothing to ride, lay a stretch of track ahead of you and ride that instead.

---

## 4. The line set

Four lines, four jobs. Do not let them swap places.

| Slot | Line | Where it lives |
|---|---|---|
| **Internal thesis** | A casual game for hardcore gamers | Team decision rule. Never public. |
| **Tagline** | Leave a wake. | Trailer end card, banner, shirt, Discord |
| **One-liner** | A flight game set in a living sea between the stars, where everything you fly through was left behind by somebody. | The thing you say at a party |
| **Short description** | See §5 | Steam, storefronts, press |

**On the tagline.** "Leave a wake" is not invented here. The Squirrel's shipped description already
ends "leaving a wake of creation and destruction". It is two words, it is literally the core
mechanic, and it is an idiom about consequence, which is exactly what a Voyage is meant to be.

Alternates, if a longer end card is wanted:
- **The sea remembers.** Darker. Points at persistence and at news travelling.
- **A sea between the stars.** Pure setting, no verb, safest.

**On the thesis.** "A casual game for hardcore gamers" stays as the internal decision rule and the
investor line. It never goes on a store page: a self-applied "casual" reads as shallow to a Steam
buyer, and the paradox needs trust we do not have from a cold scroll. Apply it as a question
instead: *does this make a five-minute run more measurable, or a first five minutes more readable?*

**"Platform" is the investor word.** Cosmic Shore genuinely is a platform, and that is the right
word in a deck. On a store page it reads as "platformer", it is language from inside the building,
and it promises the breadth that is our diagnosed risk. Keep it off player-facing copy.

---

## 5. Steam page

### Short description

Launch version, under 300 characters:

> A flight game set in a living sea between the stars. Every vessel flies completely differently,
> and every trail you lay is solid matter the world can break, eat or take. Play the Shore alone,
> the Arcade with friends, or the Arena when you want it to count.

Coming Soon version (October), identical minus the Arena if the Arena has not been built by then.

### About This Game

> ## Between the stars there is a sea
>
> Not empty space. A living medium, thick with light, where things grow and graze and die. Fly
> through it and you leave a wake of solid matter behind you, and that wake is the only permanent
> thing anyone makes out here.
>
> ## Your wake is real
>
> Everything you lay stays where you left it. Other pilots ride it, break it, or take it for
> their own colour. The wildlife eats it. Nothing in the HyperSea disappears on a timer, so the
> shape of an arena at the end of a match is a record of what everybody in it did.
>
> ## Eight vessels, eight bodies
>
> A vessel is not a skin. The Dolphin threads needles at speed and releases sideways into a
> crystal. The Rhino smashes things with its face. The Serpent flies on one thumb and grows walls
> out of what it steals. Picking up a new one is learning to move again, and the moment a match
> roster locks is the moment you find out what you are up against.
>
> ## The Shore
>
> Where pilots tie up. Things to play with, none of them keeping score. Grow a reef, paint
> something enormous out of light, change your colours, change the world you are floating in.
> Nothing here is trying to beat you and nothing here ends.
>
> ## The Arcade
>
> Short, loud games for up to four. Hoops, races, demolition, a ball that is also a crystal that
> is also somehow yours. The rules fit on one line and somebody is getting knocked into the
> wildlife.
>
> ## The Arena
>
> The same sea with the jokes taken out. Balanced rules, every vessel on the field, no hidden
> help. Bots are labelled and their difficulty is on the card.
>
> ## Voyages, on the horizon
>
> Past the Shore the sea stops arranging itself around you. Voyages will take you and your friends
> out into the HyperSea against what lives there and whoever else wants the same thing. They are
> not in Early Access yet. They are what Early Access is for.

### Early Access Q&A

**Why Early Access?**

> Cosmic Shore is a sea with a lot of coastline. The Shore and the Arcade are real and playable
> now, the Arena is being tuned, and Voyages are the reason the whole thing exists. We would
> rather build the far half of that with players in the water than guess at it alone.

**How long will this be in Early Access?**

> Roughly eighteen months. That is our honest estimate for getting Voyages in and the fleet
> finished, and we will say so on the page the moment it stops being true.

**How will the full version differ?**

> Voyages: shared-galaxy PvEvP with your friends, with events that travel between crews. The rest
> of the fleet finished and flying. More Arcade games. A deeper Arena.

**What is the state of the Early Access version?**

> Eight vessels fly, four of them with their full ability sets. Twelve Arcade games. The Shore
> with its toys. Solo play against bots in everything. Online play is friends-only parties of up
> to four, with bots filling the rest of the seats. There is no public matchmaking, no voice chat,
> and no recovery if the host drops. Windows only, English only.

**Will the price change after Early Access?**

> The price will rise modestly at 1.0. Anyone who buys during Early Access keeps the game.

**How are you involving the community?**

> A devlog every two weeks, a patch every month, and a named update every quarter. Voyages will
> be designed in public because the whole point of them is other people.

### Tags, in order

Racing, Arcade, Flight, Space, Competitive, Multiplayer, Sandbox, Time Attack, Colorful,
Local Co-Op.

The first five decide which queues and seasonal fests we land in, so they are racing-shaped on
purpose. Nothing life-sim in the first ten: the ecology is a hook the trailer shows, not a genre
we compete in.

---

## 6. In-game naming

The four destinations are the top-level navigation. What the menu calls a thing and what the
store page calls it must be the same word.

| Surface | Says | Not |
|---|---|---|
| Menu root | The Shore | Freestyle, Toybox, Sandbox, Menu |
| Mode grid | The Arcade | Minigames, Party, Games |
| Competitive grid | The Arena | Ranked, Pro, Competitive |
| Future | Voyages | Missions, Campaign, Story, Adventure |
| A thing on the Shore | a toy | a mode, a minigame |
| A thing in the Arcade | a game | a mode, a minigame |
| The place a match happens | a cell | a level, a map, a stage |
| What a vessel lays | a trail, or a wake | blocks, prisms (internal only) |

"Prism" stays the engineering word and should not appear in player-facing text. Players see
trails, wakes and mass.

---

## 7. Trailer beats

Sixty seconds, in this order. Each beat is one shot and one line of the premise.

1. **The sea.** Open on the HyperSea with no ship in frame. Light, motes, something alive moving
   through it. *"Between the stars there is a sea."*
2. **The wake.** A single vessel crosses frame and the trail stays. Hold on the trail after the
   ship has gone. *"Fly through it and you leave something solid behind."*
3. **The sea eats it.** Fauna arrive and graze the trail down. No commentary. This is the shot
   nobody else can film.
4. **Vessels.** Fast cuts, four hulls, four completely different motions. *"Your vessel is not a
   costume."*
5. **The Arcade.** Four ships, a ball, a ring, a collision, a toast that says BANK x3.
6. **The Arena.** Roster lock. Slow. Read the vessels.
7. **The Shore.** Something being built or painted, no HUD, no score.
8. **Voyages.** A wide shot heading out, captioned *Voyages. Coming to Early Access.*
9. **End card.** *Leave a wake.*

Beat 3 is the one to protect if the edit runs long. It is the only second of footage in the trailer
that no competitor can produce.

---

## 8. Fiction rules

The guard against the failure this whole exercise exists to prevent: copy that promises what the
build does not do.

1. **Name only what ships, except where the label says otherwise.** Voyages may appear in copy
   only with an explicit "coming" or "not in Early Access yet" attached, every time.
2. **Never claim the sea inside a scored run.** A leaderboard time has to come from a
   deterministic arena with the ecology off. Copy about the living world is Arcade, Shore and
   trailer copy, never Arena-leaderboard copy.
3. **Never say "no queue" or imply matchmaking.** Online is friends-only parties. Say that in
   plain words and follow it with "fully playable solo against bots" in the same breath.
4. **Never say "esport".** It is an outcome other people award you, never a claim you make. The
   Arena is described by its rules, not its aspirations.
5. **One hull number everywhere.** Eight fly. Four are finished. Do not round up to eleven.
6. **No number we have not measured.** Match lengths are emergent, not authored, and nobody has
   timed them. Any copy that says "five-minute matches" needs a measurement first.
7. **"Prism", "platform", "system", "resource" and "content" are internal words.** They do not
   appear in player-facing text.
8. **The Shore is never described as a game.** See §2.

---

## 9. Voice

Take it from the captains. Short. First person where a person is speaking. Confident without
selling. A little wry. Concrete every time, because the game's own copy already is:

> "A layered orange of prism bone, and you are the blade."
>
> "Sparrows only, in the wreck of a world that already lost."
>
> "Graze the thicket to charge your jaws."
>
> "Every bright crystal you fly through becomes YOUR ball."

Not one abstraction in any of those. Match that.

**Things this voice does not do:** exclamation marks, "epic", "immersive", "unleash", "features",
bullet lists of nouns, or any sentence that would survive being pasted onto a different game.

---

## 10. What this supersedes

- `Docs/MarketAnalysis/MARKET_GAP_PLAN_2026.md` §4, the tagline and positioning section. The
  plan's evidence, horizons, gates and initiatives all stand. Its copy does not.
- The "arcade space racer" one-liner and every variant of it.
- "Missions" as a name for the shared-galaxy layer. It is Voyages.
- "Toybox" as a player-facing word. It is the Shore. The word stays in code and in
  `Docs/ToySystem/ARCHITECTURE.md`, where it names the system rather than the place.

## 11. Open copy work

| Item | Who | Notes |
|---|---|---|
| Paste the twelve captain lines into the Grizzly, Termite and Urchin assets | any | §3, ready to use |
| Replace the duplicated Urchin class description | any | §3 |
| Measure match length per Arcade game | tester | unblocks any time claim in copy |
| Rename menu surfaces to the four destinations | UI | §6 |
| Write the twelve Arcade game one-liners against this voice | design | existing ones are already good, audit rather than rewrite |
| Falcon, Shrike, Termite captain voices | any | when those vessels are built |

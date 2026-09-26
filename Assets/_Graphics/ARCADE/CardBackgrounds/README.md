# Card backgrounds — one screenshot per live mode

`GameCard.UpdateCardView` does `BackgroundImage.sprite = game.CardBackground`, so a card's
backdrop is authored per mode on its own `SO_ArcadeGame`. Drop a capture of each mode **at
intensity 2** in here and run

    python3 Tools/Build/author_card_backgrounds.py

and the tool writes the import settings and rewires every matching card. Run it with
`--check` to see, per live card, exactly which filename it is waiting for and what it is
wearing today.

## Naming

`<card asset name minus "ArcadeGame">.png` — `ArcadeGameWreckingBall` → `WreckingBall.png`.
`.jpg` works too. A file no live card is named for **fails** the tool rather than being
silently ignored, because the alternative failure is a card quietly keeping its old art.

## Capturing

The shortest route is the Screenshot Director (`Docs/SCREENSHOT_DIRECTOR.md`): fly the mode
at intensity 2 and press **0** (or the pad's Select). It renders its own camera into a
RenderTexture, so the shot is UI-free by construction, and it lands in `<repo>/Recordings`
named for the capture concept — rename it to the card and move it here.

Two notes on what makes a good one. The plate is **313×208** and sits behind a card whose
title, avatar row, vessel icon, favourite star and genre petal all draw over it, so the
subject wants to be the *world* rather than anything small and central. And the arcade grid
shows twenty-five of these at once — a card reads first as a colour and a silhouette, so an
establishing shot of the arena beats a close pass.

## Why the tool does not capture them

Because a real screenshot needs the running editor and a built arena, which is play-testing
rather than asset work. Everything after the capture is mechanical, and that is the half the
tool owns.

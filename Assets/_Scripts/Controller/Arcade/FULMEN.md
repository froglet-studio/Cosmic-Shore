# Fulmen (`GameModes.Fulmen = 120`)

The Stoat's **SPACE** game: a burn race through Rampage's cactus forest. Every omni crystal you take throws a **Fulmen**
bolt from the heart of the cell: its branching streams set the forest burning, and every omni crystal a branch reaches
throws a smaller bolt of its own. The crystals are contested in the nucleus, so the race is for the crystals and the
forest is the score. First **domain** to destroy the hostile-prism target wins, golf-timed.

Built 2026-11-15 for Garrett's "make all the missing games" (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 30, which
reserved the ids: `GameModes` 120, hint toast 195). The Stoat's Mass is skipped on the roster (Heavy Launch touches no
mass), so this is its Space game.

> **Status.** Authored headless on 2026-11-15 (`claude/stoat-arcade-games`). **Nothing here has been run in the Unity
> editor, and nobody has flown it.** The target (1,200) is a first cut from the bolt's reach, not a measurement.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameFulmen.unity` (cloned from `MinigameUndertow.unity`, as Sirocco is) |
| Controller | `FulmenController : MultiplayerDomainGamesController` (no settings asset: nothing to tune but the target) |
| AI | none: the platform autopilot's crystal seeking IS the loop (Rampage's rule) |
| Metric | `ScoringMetric.PrismsDestroyed` (5), reused |
| Rule | `FulmenScoringRuleSO : RampageScoringRuleSO`, only the authored words are its own |
| Turn monitor | `FulmenPrismTurnMonitor` (Harvest's shape, its own getter) |
| Target | `EndConditionOverridesSO.fulmenPrismTarget` (**1,200**) |
| Comeback | rate **0.005**: a quarter behind (300 prisms) buys 1.5 element levels |
| Arena | **Rampage's cactus forest**, referenced read-only (four intensity configs), Rampage's spawn ring and crystal counts |
| Objective arrow | `RampageObjectiveProvider`: the nearest contested omni crystal |
| Toasts | 83 (prism milestone), 128-130 (shared race beats via `DomainRaceToasts`), 195 (the hint), 30 (comeback) |
| Generator | `Tools/Build/author_fulmen_assets.py` (`--check`) |

## The loop

Fulmen is the Stoat's omni-crystal ability (`Environment/Fulmen/README.md`, `FulmenByCrystalEffectSO` in
`StoatImpactorDataContainer`). As shipped (`FulmenConfig.asset`): every crystal fires (`crystalsToFire` 1); the bolt
roots at the collector's **cell centre** and reaches 0.6 of the cell radius (720 u in Rampage's 1,200 u cell) in five
roots; prisms within `burnRadius` (200 u) of a stream ignite, burn for 12 s and spread to neighbours; every omni crystal
within 12 u of a branch throws a child bolt (0.4 of the reach, two generations deep, up to six children).

Rampage's forest fills the cell from the nucleus out, and its omni crystals respawn in the nucleus: at the bolt's root.
So every crystal you take lights the forest round the heart, and at intensity 1 (twice the roster in crystals) a bolt
that reaches the other crystals chains. A burnout is `Prism.BurnOut`, which is `Prism.Damage` in the igniter's name, so
every prism a bolt (or its children) burns scores for the pilot who took the crystal. Forest flora is hostile to
everyone; your own trail never scores.

Nothing about the Stoat changes: the bolt, its reach and its burn are the vessel's, the crystals and the forest are
Rampage's.

## Intensity

Rampage's ladder, referenced (`RAMPAGE.md`): intensity 1 has five times the plants and twice the roster in crystals (so
chains), intensity 4 the shipped forest and one crystal for the whole lobby (so every bolt is fought for). The target
is held under a quarter of intensity 4's forest (9,830 prisms) by the generator.

## Verification and known limits

- Authored headless. The bolt is server-decided and replays on clients (`FulmenNetSync`), so an AI's crystal fires like a
  human's; not run in a session.
- The bolt roots at the cell centre whoever takes the crystal, so where you take it does not aim it. That is the
  ability as built; a mode never changes it.
- No offline race model; not a self-play training ground yet.
